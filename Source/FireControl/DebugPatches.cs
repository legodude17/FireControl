using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace FireControl;

public static class DebugPatches
{
    private static int level;

    public static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.GetRandomShootingTargetByScore));
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestShootTargetFromCurrentPosition));
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget));
        yield return AccessTools.Method(typeof(AttackTargetsCache), nameof(AttackTargetsCache.GetPotentialTargetsFor));
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanShootAtFromCurrentPosition));
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanReach));
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanShootAtFromCurrentPosition));
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.GetShootingTargetScore));
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.HasRangedAttack));
        yield return AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanSee));
        foreach (var method in typeof(AttackTargetFinder).GetNestedTypes(AccessTools.all)
                    .Concat(typeof(AttackTargetFinder))
                    .SelectMany(AccessTools.GetDeclaredMethods)
                    .Where(m => m.Name.Contains("BestAttackTarget")))
            yield return method;
        if (AccessTools.TypeByName("CombatExtended.HarmonyCE.Harmony_AttackTargetFinder") is { } outer
         && AccessTools.Inner(outer, "Harmony_AttackTargetFinder_BestAttackTarget") is { } inner
         && AccessTools.Method(inner, "FindAttackTargetForRangedAttack") is { } target)
            yield return target;
    }

    public static void Do(Harmony harm)
    {
        foreach (var target in Targets())
            harm.Patch(target, new HarmonyMethod(typeof(DebugPatches), nameof(Prefix)), new HarmonyMethod(typeof(DebugPatches), nameof(Postfix)));
    }

    public static void Prefix(MethodBase __originalMethod, object[] __args)
    {
        Log.Message(
            $"{new string(' ', level * 4)}{__originalMethod.DeclaringType.Namespace}.{__originalMethod.DeclaringType.Name}.{__originalMethod.Name}({__args?.Join()})");
        level++;
    }

    public static void Postfix(MethodBase __originalMethod, object __result)
    {
        level--;
        Log.Message(
            $"{new string(' ', level * 4)}{__originalMethod.DeclaringType.Namespace}.{__originalMethod.DeclaringType.Name}.{__originalMethod.Name} -> {(__result is IEnumerable<object> a ? a.Join() : __result)}");
    }
}
