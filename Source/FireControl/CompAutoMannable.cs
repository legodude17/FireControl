using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FireControl;

public class CompAutoMannable : CompMannable, INamed
{
    private Thing controller;
    public new CompProperties_AutoMannable Props => props as CompProperties_AutoMannable;
    public CompFireControl FireController { get; private set; }
    public string Name { get; set; }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_References.Look(ref controller, nameof(controller));
        if (Scribe.mode == LoadSaveMode.PostLoadInit) FireController = controller.TryGetComp<CompFireControl>();
        var name = Name;
        Scribe_Values.Look(ref name, nameof(name));
        Name = name;
    }

    public void Notify_Controlled(CompFireControl comp)
    {
        FireController = comp;
        controller = comp.parent;
    }

    public void Notify_RemoveControl()
    {
        FireController = null;
        controller = null;
    }

    public override void PostDeSpawn(Map map)
    {
        base.PostDeSpawn(map);
        FireController?.Notify_TurretLost((Building_Turret)parent);
        controller = null;
        FireController = null;
    }

    public override string TransformLabel(string label) => Name.NullOrEmpty() ? base.TransformLabel(label) : Name;

    public override IEnumerable<Gizmo> CompGetGizmosExtra() =>
        base.CompGetGizmosExtra()
           .Append(new Command_Action
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
            });
}

public class CompProperties_AutoMannable : CompProperties_Mannable
{
    public int complexity;

    public bool manuallyManable;

    public CompProperties_AutoMannable() => compClass = typeof(CompMannable);
}
