using RimWorld;
using Verse;

namespace FireControl;

public class CompAutoMannable : CompMannable
{
    private Thing controller;
    public new CompProperties_AutoMannable Props => props as CompProperties_AutoMannable;
    public CompFireControl FireController { get; private set; }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_References.Look(ref controller, nameof(controller));
        if (Scribe.mode == LoadSaveMode.PostLoadInit) FireController = controller.TryGetComp<CompFireControl>();
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
}

public class CompProperties_AutoMannable : CompProperties_Mannable
{
    public int complexity;

    public bool manuallyManable;

    public CompProperties_AutoMannable() => compClass = typeof(CompMannable);
}
