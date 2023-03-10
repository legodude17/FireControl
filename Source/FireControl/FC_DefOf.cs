using RimWorld;
using Verse;

namespace FireControl;

[DefOf]
public static class FC_DefOf
{
    public static JobDef FC_ManComputer;

    static FC_DefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(FC_DefOf));
    }
}
