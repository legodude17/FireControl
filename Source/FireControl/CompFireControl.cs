using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FireControl;

[StaticConstructorOnStartup]
public class CompFireControl : ThingComp, ITargetingSource, INamed
{
    private static readonly Texture2D ConnectTurretTex = ContentFinder<Texture2D>.Get("UI/TurretConnect");
    private static readonly Texture2D DisconnectTurretTex = ContentFinder<Texture2D>.Get("UI/TurretDisconnect");
    private static readonly Texture2D DisconnectAllTex = ContentFinder<Texture2D>.Get("UI/TurretDisconnectAll");

    public int LimitIndex;
    public Pawn ManningPawn;
    private bool add;
    private CompActiveGraphic compActiveGraphic;
    private List<CompMannable> compMannables = new();
    private CompPowerTrader compPower;
    private List<Building_Turret> controlledTurrets = new();

    private int lastTickUsed;
    public CompProperties_FireControl Props => props as CompProperties_FireControl;
    public int CurrentLoad => compMannables.Take(LimitIndex + 1).Sum(comp => (comp.props as CompProperties_AutoMannable)!.complexity);
    public bool Usable => compPower == null || compPower.PowerOn;

    public int MaxLoad =>
        Props.auto || ManningPawn == null ? Props.maxTurrets : Math.Min(ManningPawn.skills.GetSkill(SkillDefOf.Intellectual).Level, Props.maxTurrets);

    public bool MannedNow => Math.Abs(Find.TickManager.TicksGame - lastTickUsed) <= 1;
    public bool Active => Props.auto || MannedNow;

    public IEnumerable<(Building_Turret turret, CompMannable mannable)> Turrets =>
        controlledTurrets.Zip(compMannables, (turret, mannable) => (turret, mannable));

    public int TurretCount => controlledTurrets.Count;

    public float Efficiency =>
        Props.auto || ManningPawn == null || CurrentLoad == 0
            ? Props.efficiency
            : Props.efficiency + Mathf.Max(ManningPawn.skills.GetSkill(SkillDefOf.Shooting).Level * 10 / Mathf.Log(CurrentLoad + 1) / 100f, 0.01f);

    public string Name { get; set; }

    public bool CanHitTarget(LocalTargetInfo target) => target.HasThing;

    public bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
    {
        if (target.Thing is not Building_Turret turret)
        {
            if (showMessages) Messages.Message("FireControl.MustBeTurret".Translate(), MessageTypeDefOf.RejectInput, false);

            return false;
        }

        if (turret.TryGetComp<CompPowerTrader>() is not { } targetCompPower || compPower == null || compPower.PowerNet != targetCompPower.PowerNet)
        {
            if (showMessages) Messages.Message("FireControl.MustBeSamePower".Translate(), MessageTypeDefOf.RejectInput, false);

            return false;
        }

        if (turret.TryGetComp<CompMannable>() is not { props: CompProperties_AutoMannable { complexity: var complexity } })
        {
            if (showMessages) Messages.Message("FireControl.MustBeMannable".Translate(), MessageTypeDefOf.RejectInput, false);

            return false;
        }

        if (turret.Faction is not { IsPlayer: true })
        {
            if (showMessages) Messages.Message("FireControl.MustBeYours".Translate(), MessageTypeDefOf.RejectInput, false);

            return false;
        }

        if (turret.TryGetComp<CompAutoMannable>() is { FireController: { } controller })
            if (add)
            {
                if (controller == this)
                {
                    if (showMessages) Messages.Message("FireControl.AlreadyConnected".Translate(), MessageTypeDefOf.RejectInput, false);

                    return false;
                }
            }
            else
            {
                if (controller != this)
                {
                    if (showMessages) Messages.Message("FireControl.MustBeMine".Translate(), MessageTypeDefOf.RejectInput, false);

                    return false;
                }
            }

        return true;
    }

    public void DrawHighlight(LocalTargetInfo target)
    {
        if (target.HasThing) GenDraw.DrawTargetHighlight(target);
        if (target.Thing is Building_Turret turret) GenDraw.DrawLineBetween(parent.DrawPos, turret.DrawPos);
    }

    public void OrderForceTarget(LocalTargetInfo target)
    {
        if (target.Thing is Building_Turret turret)
            if (add)
            {
                controlledTurrets.Add(turret);
                compMannables.Add(turret.TryGetComp<CompMannable>());
                turret.TryGetComp<CompAutoMannable>()?.Notify_Controlled(this);
            }
            else if (controlledTurrets.Contains(turret))
            {
                controlledTurrets.Remove(turret);
                compMannables.Remove(turret.TryGetComp<CompMannable>());
                turret.TryGetComp<CompAutoMannable>()?.Notify_RemoveControl();
            }
    }

    public void OnGUI(LocalTargetInfo target) { }

    public bool CasterIsPawn => true;

    public bool IsMeleeAttack => false;

    public bool Targetable => true;

    public bool MultiSelect => false;

    public bool HidePawnTooltips => false;

    public Thing Caster => parent;

    public Pawn CasterPawn => null;

    public Verb GetVerb => null;

    public Texture2D UIIcon => null;

    public TargetingParameters targetParams => new() { canTargetAnimals = false, canTargetPawns = false, canTargetBuildings = true };

    public ITargetingSource DestinationSelector => null;

    public Action GetReorderAction(int index, int by)
    {
        return delegate
        {
            var target = controlledTurrets[index];
            controlledTurrets.RemoveAt(index);
            controlledTurrets.Insert(index + by, target);
            var target2 = compMannables[index];
            compMannables.RemoveAt(index);
            compMannables.Insert(index + by, target2);
        };
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        compActiveGraphic = parent.TryGetComp<CompActiveGraphic>();
        compPower = parent.TryGetComp<CompPowerTrader>();
    }

    public override void PostDeSpawn(Map map)
    {
        foreach (var turret in controlledTurrets) turret.TryGetComp<CompAutoMannable>()?.Notify_RemoveControl();
        base.PostDeSpawn(map);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref controlledTurrets, nameof(controlledTurrets), LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) compMannables = controlledTurrets.Select(t => t.TryGetComp<CompMannable>()).ToList();
        var name = Name;
        Scribe_Values.Look(ref name, nameof(name));
        Name = name;
    }

    public void Used(Pawn user)
    {
        compActiveGraphic?.Used();
        ManningPawn = user;
        lastTickUsed = Find.TickManager.TicksGame;
        var maxLoad = MaxLoad;

        for (var i = 0; i < compMannables.Count; i++)
        {
            var mannable = compMannables[i];
            maxLoad -= (mannable.props as CompProperties_AutoMannable)!.complexity;
            LimitIndex = i;
            if (maxLoad < 0) return;

            mannable.ManForATick(user);
            (mannable as CompAutoMannable)?.Notify_Controlled(this);
        }

        LimitIndex = controlledTurrets.Count;
    }

    public override void CompTick()
    {
        base.CompTick();
        if (Props.auto && parent.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault() is { } pawn) Used(pawn);
        if (Active && controlledTurrets.Count > 0)
        {
#if v1_4
            compPower.PowerOutput = -compPower.Props.PowerConsumption * CurrentLoad;
#endif
#if v1_3
            compPower.PowerOutput = -compPower.Props.basePowerConsumption * CurrentLoad;
#endif
        }
        else
            compPower.PowerOutput = 0;
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        base.PostDrawExtraSelectionOverlays();
        for (var i = 0; i < controlledTurrets.Count; i++)
        {
            var turret = controlledTurrets[i];
            if (!Active) GenDraw.DrawLineBetween(parent.DrawPos, turret.DrawPos);
            else if (i >= LimitIndex) GenDraw.DrawLineBetween(parent.DrawPos, turret.DrawPos, SimpleColor.Red);
            else GenDraw.DrawLineBetween(parent.DrawPos, turret.DrawPos, SimpleColor.Green);
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra()) yield return gizmo;

        var connect = new Command_Action
        {
            defaultLabel = "FireControl.Connect".Translate(),
            defaultDesc = "FireControl.Connect.Desc".Translate(),
            icon = ConnectTurretTex,
            action = delegate
            {
                add = true;
                Find.Targeter.BeginTargeting(this);
            }
        };

        yield return connect;

        var disconnect = new Command_Action
        {
            defaultLabel = "FireControl.Disconnect".Translate(),
            defaultDesc = "FireControl.Disconnect.Desc".Translate(),
            icon = DisconnectTurretTex,
            action = delegate
            {
                add = false;
                Find.Targeter.BeginTargeting(this);
            }
        };

        if (controlledTurrets.Count == 0) disconnect.Disable("FireControl.NoTurrets".Translate());

        yield return disconnect;

        var disconnectAll = new Command_Action
        {
            defaultLabel = "FireControl.DisconnectAll".Translate(),
            defaultDesc = "FireControl.DisconnectAll.Desc".Translate(),
            icon = DisconnectAllTex,
            action = delegate
            {
                foreach (var turret in controlledTurrets) turret.TryGetComp<CompAutoMannable>()?.Notify_RemoveControl();

                controlledTurrets.Clear();
                compMannables.Clear();
            }
        };

        if (controlledTurrets.Count == 0) disconnectAll.Disable("FireControl.NoTurrets".Translate());

        yield return disconnectAll;

        yield return new Command_Action
        {
            icon = TexButton.Rename,
            defaultLabel = "CommandRenameZoneLabel".Translate(),
            action = delegate
            {
                var dialog = new Dialog_RenameSomething(this);
                if (KeyBindingDefOf.Misc1.IsDown) dialog.WasOpenedByHotkey();
                Find.WindowStack.Add(dialog);
            },
            hotKey = KeyBindingDefOf.Misc1
        };
    }

    public override string TransformLabel(string label) => Name.NullOrEmpty() ? base.TransformLabel(label) : Name;

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        if (!Usable) yield break;
        if (Props.auto) yield break;

        if (selPawn.WorkTagIsDisabled(WorkTags.Violent))
            yield return new FloatMenuOption("CannotManThing".Translate(parent.LabelShort, parent) +
                                             " (" + "IsIncapableOfViolenceLower".Translate(selPawn.LabelShort, selPawn) + ")", null);
        else
            yield return new FloatMenuOption("OrderManThing".Translate(parent.LabelShort, parent), delegate
            {
                var job = JobMaker.MakeJob(FC_DefOf.FC_ManComputer, parent);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
    }

    public void Notify_TurretLost(Building_Turret turret)
    {
        controlledTurrets.Remove(turret);
        compMannables.Remove(turret.TryGetComp<CompMannable>());
    }
}

// ReSharper disable InconsistentNaming
public class CompProperties_FireControl : CompProperties
{
    public bool auto;
    public float efficiency;
    public int maxTurrets;
    public CompProperties_FireControl() => compClass = typeof(CompFireControl);
}
