using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace FireControl;

public static class TargetFinderFixes
{
    private static readonly Dictionary<Thing, Thing> realSearchers = new();

    public static void Do(Harmony harm)
    {
        harm.Patch(AccessTools.Method(typeof(Building_TurretGun), nameof(Building_TurretGun.TargSearcher)),
            postfix: new HarmonyMethod(typeof(TargetFinderFixes), nameof(TargSearcher_Postfix)));
        harm.Patch(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget)),
            postfix: new HarmonyMethod(typeof(TargetFinderFixes), nameof(BestAttackTarget_Postfix)));
        foreach (var method in typeof(AttackTargetFinder).GetNestedTypes(AccessTools.all)
                    .Concat(typeof(AttackTargetFinder))
                    .SelectMany(AccessTools.GetDeclaredMethods)
                    .Where(m => m.Name.Contains("BestAttackTarget")))
            harm.Patch(method, transpiler: new HarmonyMethod(typeof(TargetFinderFixes), nameof(FixSearcherThingPosition)));
#if v1_4
        harm.Patch(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.ShouldIgnoreNoncombatant)),
            new HarmonyMethod(typeof(TargetFinderFixes), nameof(ShouldIgnoreNoncombatant_Prefix)));
#endif
        harm.Patch(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanReach)),
            new HarmonyMethod(typeof(TargetFinderFixes), nameof(CanReach_Prefix)));
        harm.Patch(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanShootAtFromCurrentPosition)),
            new HarmonyMethod(typeof(TargetFinderFixes), nameof(ReplaceSearcher)));
        harm.Patch(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.GetShootingTargetScore)),
            new HarmonyMethod(typeof(TargetFinderFixes), nameof(ReplaceSearcher)));
        harm.Patch(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.HasRangedAttack)),
            new HarmonyMethod(typeof(TargetFinderFixes), nameof(HasRangedAttack_Prefix)));
        harm.Patch(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanSee)),
            new HarmonyMethod(typeof(TargetFinderFixes), nameof(CanSee_Prefix)));
        if (AccessTools.TypeByName("CombatExtended.HarmonyCE.Harmony_AttackTargetFinder") is { } outer
         && AccessTools.Inner(outer, "Harmony_AttackTargetFinder_BestAttackTarget") is { } inner
         && AccessTools.Method(inner, "FindAttackTargetForRangedAttack") is { } target)
        {
            Log.Message("[FireControl] Patching Combat Extended...");
            harm.Patch(target, transpiler: new HarmonyMethod(typeof(TargetFinderFixes), nameof(FixSearcherThingPosition)));
            harm.Patch(target, transpiler: new HarmonyMethod(typeof(TargetFinderFixes), nameof(FixManningCheck)));
        }
    }

    public static void TargSearcher_Postfix(Building_TurretGun __instance, IAttackTargetSearcher __result)
    {
        if (__result.Thing != __instance) realSearchers.Add(__result.Thing, __instance);
    }

    public static void BestAttackTarget_Postfix(IAttackTargetSearcher searcher)
    {
        realSearchers.Remove(searcher.Thing);
    }

    public static Thing GetRealSearcher(Thing thing) => realSearchers.TryGetValue(thing, thing);

    public static IAttackTargetSearcher GetRealSearcher(IAttackTargetSearcher searcher) =>
        realSearchers.TryGetValue(searcher.Thing) as IAttackTargetSearcher ?? searcher;

#if v1_4
    public static void ShouldIgnoreNoncombatant_Prefix(ref Thing searcherThing)
    {
        searcherThing = GetRealSearcher(searcherThing);
    }
#endif

    public static void CanReach_Prefix(ref Thing searcher)
    {
        searcher = GetRealSearcher(searcher);
    }

    public static void ReplaceSearcher(ref IAttackTargetSearcher searcher)
    {
        searcher = GetRealSearcher(searcher);
    }

    public static void HasRangedAttack_Prefix(ref IAttackTargetSearcher t)
    {
        t = GetRealSearcher(t);
    }

    public static void CanSee_Prefix(ref Thing seer)
    {
        seer = GetRealSearcher(seer);
    }

    public static IEnumerable<CodeInstruction> FixSearcherThingPosition(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var info1 = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));
        var info2 = AccessTools.PropertyGetter(typeof(IAttackTargetSearcher), nameof(IAttackTargetSearcher.CurrentEffectiveVerb));
        for (var i = 0; i < codes.Count; i++)
        {
            if (i > 2 && codes[i - 1].operand is FieldInfo { Name: "searcherThing" } && codes[i].Calls(info1))
                yield return CodeInstruction.Call(typeof(TargetFinderFixes), nameof(GetRealSearcher), new[] { typeof(Thing) });

            if (codes[i].Calls(info2))
                yield return CodeInstruction.Call(typeof(TargetFinderFixes), nameof(GetRealSearcher), new[] { typeof(IAttackTargetSearcher) });

            yield return codes[i];
        }
    }

    public static IEnumerable<CodeInstruction> FixManningCheck(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var info1 = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.CurJobDef));
        var idx1 = codes.FindIndex(ins => ins.Calls(info1));
        codes.RemoveRange(idx1, 3);
        codes.Insert(idx1, CodeInstruction.Call(typeof(TargetFinderFixes), nameof(IsManning)));
        return codes;
    }

    private static bool IsManning(Pawn pawn) => pawn.CurJobDef == JobDefOf.ManTurret || pawn.CurJobDef == FC_DefOf.FC_ManComputer;
}
