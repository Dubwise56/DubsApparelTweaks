
using HarmonyLib;
using RimWorld;
using Verse;

namespace QuickFast.Source
{
    [StaticConstructorOnStartup]
    public static class H_EquipSpeed
    {
        private static void ModifyEquipDuration(JobDriver_Wear instance)
        {
            if (!Settings.ChangeEquipSpeed) return;
            
            instance.duration = Settings.FlatRate 
                ? Settings.EquipModTicks 
                : (int)(instance.duration * Settings.EquipModPC);
        }

        [HarmonyPatch(typeof(JobDriver_RemoveApparel), nameof(JobDriver_RemoveApparel.Notify_Starting))]
        public static class h_JobDriver_RemoveApparel
        {
            public static void Postfix(JobDriver_RemoveApparel __instance)
            {
                if (!Settings.ChangeEquipSpeed) return;

                __instance.duration = Settings.FlatRate
                    ? Settings.EquipModTicks
                    : (int)(__instance.duration * Settings.EquipModPC);
            }
        }

        [HarmonyPatch(typeof(JobDriver_Wear), nameof(JobDriver_Wear.Notify_Starting))]
        public static class h_JobDriver_Wear
        {
            public static void Postfix(JobDriver_Wear __instance) => ModifyEquipDuration(__instance);
        }
    }
}