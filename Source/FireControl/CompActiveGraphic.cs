using RimWorld;
using UnityEngine;
using Verse;

namespace FireControl;

public class CompActiveGraphic : ThingComp
{
    private int lastTickActive;
    public CompProperties_ActiveGraphic Props => props as CompProperties_ActiveGraphic;

    public void Used()
    {
        lastTickActive = Find.TickManager.TicksGame;
    }

    public override void PostDraw()
    {
        base.PostDraw();
        if (Mathf.Abs(Find.TickManager.TicksGame - lastTickActive) <= 1)
        {
            var drawPos = parent.DrawPos;
            drawPos.y += Altitudes.AltInc;
            var graphic = Props.graphicData.Graphic;
            var rot = parent.Rotation;
            Graphics.DrawMesh(graphic.MeshAt(rot), drawPos, graphic.QuatFromRot(rot), graphic.MatAt(rot, parent), 0);
        }
    }
}

// ReSharper disable InconsistentNaming
public class CompProperties_ActiveGraphic : CompProperties
{
    public GraphicData graphicData;

    public CompProperties_ActiveGraphic() => compClass = typeof(CompActiveGraphic);

    public override void DrawGhost(IntVec3 center, Rot4 rot, ThingDef thingDef, Color ghostCol, AltitudeLayer drawAltitude, Thing thing = null)
    {
        base.DrawGhost(center, rot, thingDef, ghostCol, drawAltitude, thing);
        var graphic = GhostUtility.GhostGraphicFor(graphicData.Graphic, thingDef, ghostCol);
        var loc = GenThing.TrueCenter(center, rot, thingDef.Size, drawAltitude.AltitudeFor(Altitudes.AltInc));
        Graphics.DrawMesh(graphic.MeshAt(rot), loc, graphic.QuatFromRot(rot), graphic.MatAt(rot), 0);
    }
}
