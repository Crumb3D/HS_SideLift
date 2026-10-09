using HarmonyLib;

[HarmonyPatch(typeof(TileEntityPowered), "get_IsPowered")]
public static class HSSideLiftLightPowerPatch
{
    static void Postfix(TileEntityPowered __instance, ref bool __result)
    {
        if (__result || __instance == null) return;
        try
        {
            var b = __instance.block;
            if (b is BlockHSSideLiftOutsidePanel) return;
            if (!(b is BlockPoweredLight) && !(b is BlockSpotlight)) return;
            var tog = __instance as TileEntityPoweredBlock;
            if (tog != null && !tog.IsToggled) return;
            var pos = __instance.ToWorldPos();
            var lifts = HSSideLiftConfiguration.Lifts;
            if (lifts == null) return;
            for (int i = 0; i < lifts.Count; i++)
            {
                var d = lifts[i];
                if (d == null || !HSSideLiftCabinLights.InCar(d, pos)) continue;
                if (HSSideLiftPower.CabinHasPower(d))
                {
                    __result = true;
                    return;
                }
            }
        }
        catch { }
    }
}
