using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FireControl;

[StaticConstructorOnStartup]
public static class HarmonyPatches
{
    public static Harmony Harm;

    static HarmonyPatches()
    {
        Harm = new Harmony("legodude17.fcc");
        Harm.Patch(AccessTools.Method(typeof(CompMannable), nameof(CompMannable.CompFloatMenuOptions)), Get(nameof(CheckManuallyMannable)));
#if v1_4
        Harm.Patch(AccessTools.Method(typeof(CompPowerTrader), nameof(CompPowerTrader.CompInspectStringExtra)), transpiler: Get(nameof(FixPowerDisplay)));
#endif
        Harm.Patch(AccessTools.Method(typeof(Building_TurretGun), nameof(Building_TurretGun.BurstCooldownTime)), postfix: Get(nameof(ModifyCooldown)));
        Harm.Patch(AccessTools.Method(typeof(ShotReport), nameof(ShotReport.HitFactorFromShooter), new[] { typeof(Thing), typeof(float) }),
            transpiler: Get(nameof(AddAccuracyCalc)));
        Harm.Patch(AccessTools.Method(typeof(Building_TurretGun), nameof(Building_TurretGun.SpawnSetup)), postfix: Get(nameof(CheckManning)));
        Harm.Patch(AccessTools.Method(typeof(Building), nameof(Building.SetFaction)), postfix: Get(nameof(CheckManning)));
        Harm.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.SetFactionDirect)), postfix: Get(nameof(CheckManning)));
        if (AccessTools.TypeByName("CombatExtended.Building_TurretGunCE") is { } type)
        {
            if (AccessTools.Method(type, "BurstCooldownTime") is { } target1) Harm.Patch(target1, postfix: Get(nameof(ModifyCooldown)));
            if (AccessTools.Method(type, "SpawnSetup") is { } target2) Harm.Patch(target2, postfix: Get(nameof(CheckManning)));
        }

        TargetFinderFixes.Do(Harm);
//        DebugPatches.Do(Harm);
    }

    public static HarmonyMethod Get(string name) => new(typeof(HarmonyPatches), name);

    public static bool CheckManuallyMannable(ref IEnumerable<FloatMenuOption> __result, CompMannable __instance)
    {
        if (__instance.props is not CompProperties_AutoMannable { manuallyManable: false }) return true;
        __result = Enumerable.Empty<FloatMenuOption>();
        return false;
    }

    public static void ModifyCooldown(Building_TurretGun __instance, ref float __result)
    {
        __result /= __instance.TryGetComp<CompAutoMannable>()?.FireController?.Efficiency ?? 1f;
    }

    public static IEnumerable<CodeInstruction> AddAccuracyCalc(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var info1 = AccessTools.Field(typeof(StatDefOf), nameof(StatDefOf.ShootingAccuracyTurret));
        var idx1 = codes.FindIndex(ins => ins.LoadsField(info1));
        var idx2 = codes.FindIndex(idx1, ins => ins.opcode == OpCodes.Call);
        codes.InsertRange(idx2 + 1, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            CodeInstruction.Call(typeof(HarmonyPatches), nameof(ModifyAccuracy))
        });
        return codes;
    }

    public static float ModifyAccuracy(float accuracy, Thing thing)
    {
        if (thing.TryGetComp<CompAutoMannable>() is { FireController.Efficiency: var efficiency }) return accuracy * efficiency;
        return accuracy;
    }

    public static void CheckManning(Thing __instance)
    {
        if (__instance is Building_TurretGun turret)
        {
            if (!turret.Faction.IsPlayerSafe() && turret.mannableComp is CompAutoMannable) turret.mannableComp = null;
            if (turret.Faction.IsPlayerSafe() && turret.mannableComp == null && turret.GetComp<CompAutoMannable>() is { } comp) turret.mannableComp = comp;
        }
    }
#if v1_4
    public static IEnumerable<CodeInstruction> FixPowerDisplay(IEnumerable<CodeInstruction> instructions)
    {
        var info = AccessTools.PropertyGetter(typeof(CompProperties_Power), nameof(CompProperties_Power.PowerConsumption));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(info))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return CodeInstruction.Call(typeof(HarmonyPatches), nameof(FixPowerConsumption));
            }
        }
    }

    public static float FixPowerConsumption(float num, CompPowerTrader trader)
    {
        if (trader.parent.TryGetComp<CompFireControl>() is { CurrentLoad: var load }) num *= load;
        return num;
    }
#endif
}
