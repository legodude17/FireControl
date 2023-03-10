using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FireControl;

public class ITab_FireControl : ITab
{
    private Vector2 scrollPos;

    // Token: 0x06008FCB RID: 36811 RVA: 0x0033D167 File Offset: 0x0033B367
    public ITab_FireControl()
    {
        size = new Vector2(420f, 480f);
        labelKey = "FireControl.Tab";
    }

    public CompFireControl CompFireControl => SelThing.TryGetComp<CompFireControl>();

    public override void FillTab()
    {
        var comp = CompFireControl;
        var rect = new Rect(0f, 0f, 420f, 420f).ContractedBy(10f);
        if (comp.Active)
        {
            Widgets.Label(rect.TakeTopPart(20f),
                comp.Props.auto ? "FireControl.Autonomous".Translate() : "FireControl.MannedBy".Translate(comp.ManningPawn.NameShortColored));
            Widgets.Label(rect.TakeTopPart(20f), "FireControl.MaxTurrets".Translate(comp.MaxLoad));
            Widgets.Label(rect.TakeTopPart(20f), "FireControl.Efficiency".Translate(comp.Efficiency.ToStringPercentEmptyZero()));
        }
        else
            Widgets.Label(rect.TakeTopPart(20f), "FireControl.NotManned".Translate());

        rect.TakeTopPart(10f).DrawLineAcross();

        var i = 0;
        var actions = new List<Action>();
        var viewRect = new Rect(0, 0, rect.width - 20f, comp.TurretCount * 45f);
        Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
        foreach (var (turret, mannable) in comp.Turrets)
        {
            var color = GUI.color = i >= comp.LimitIndex || !(comp.MannedNow || comp.Props.auto) ? Color.gray : Color.white;
            var turretRect = viewRect.TakeTopPart(40f);
            var wholeRect = turretRect;
            GUI.DrawTexture(turretRect.TakeLeftPart(40f), turret.def.uiIcon);
            GUI.color = Color.white;
            var down = turretRect.TakeRightPart(24);
            var up = turretRect.TakeRightPart(24);
            if (i > 0 && Widgets.ButtonImage(up.Center(24, 24), TexButton.ReorderUp))
            {
                actions.Add(comp.GetReorderAction(i, -1));
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }

            if (i < comp.TurretCount - 1 && Widgets.ButtonImage(down.Center(24, 24), TexButton.ReorderDown))
            {
                actions.Add(comp.GetReorderAction(i, 1));
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }

            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = color;
            Widgets.Label(turretRect.TopHalf(), turret.LabelCap);
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = color;
            Widgets.Label(turretRect.BottomHalf(), "FireControl.Complexity".Translate() + ": " + (mannable.props as CompProperties_AutoMannable)!.complexity);
            GUI.color = Color.white;
            Text.Anchor = anchor;

            Widgets.DrawHighlightIfMouseover(wholeRect);
            if (Widgets.ButtonInvisible(wholeRect)) CameraJumper.TryJumpAndSelect(turret);
            viewRect.TakeTopPart(5f).DrawLineAcross();
            i++;
        }

        Widgets.EndScrollView();

        foreach (var action in actions) action();
    }
}
