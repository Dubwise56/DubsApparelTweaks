using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace QuickFast.Source
{
    [StaticConstructorOnStartup]
    public static class H_ApparelPatches
    {
        public static void ApparelChanged(Pawn pawn)
        {
          //  if (!UnityData.IsInMainThread) return;
            if (Settings.apparelHidingMode == ApparelHidingMode.Never)
            {
                return;
            }
            pawn?.apparel?.Notify_ApparelChanged();
            PortraitsCache.SetDirty(pawn);
        }
        
        public static void PatherCheck(Pawn pawn, IntVec3 nextCell, IntVec3 lastCell, bool startPath)
        {
           // if (UnityData.IsInMainThread is false) return;
            if (Settings.apparelHidingMode != ApparelHidingMode.IndoorsOrNotDrafted)
            {
                return;
            }
            
            var map = pawn.Map;

            if (map == null) return;
            
            if (pawn.Drafted) return;
            
            if (!pawn.RaceProps.Humanlike) return;
            
            if (startPath)
            {
                // For start path, always call ApparelChanged regardless of mode/condition
                ApparelChanged(pawn);
                return;
            }
            
            if (!nextCell.InBounds(map) || !lastCell.InBounds(map)) return;
            
            // Check for indoor/outdoor transitions
            var wasOutdoor = lastCell.UsesOutdoorTemperature(pawn.MapHeld);
            var isOutdoor = nextCell.UsesOutdoorTemperature(pawn.MapHeld);

            if (wasOutdoor != isOutdoor)
            {
                ApparelChanged(pawn);
            }
        }
        
        [HarmonyPatch(typeof(Pawn_DraftController))]
        [HarmonyPatch(nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
        public static class H_Drafted
        {
            public static void Postfix(Pawn_DraftController __instance)
            {
                ApparelChanged(__instance.pawn);
            }
        }

        [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath))]
        public static class H_StartPath
        {
            public static void Postfix(Pawn_PathFollower __instance)
            {
                PatherCheck(__instance.pawn, __instance.nextCell, __instance.lastCell, true);
            }
        }

        [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.TryEnterNextPathCell))]
        [StaticConstructorOnStartup]
        public static class H_TryEnterNextPathCell
        {
            public static void Postfix(Pawn_PathFollower __instance)
            {
                PatherCheck(__instance.pawn, __instance.nextCell, __instance.lastCell, false);
            }
        }
        
        [HarmonyPatch(typeof(PawnRenderNode_Apparel), nameof(PawnRenderNode_Apparel.GraphicsFor))]
        public static class H_ApparelGraphicsFor
        {
            public static IEnumerable<Graphic> Postfix(IEnumerable<Graphic> __result, PawnRenderNode_Apparel __instance, Pawn pawn)
            {
                var apparel = __instance.apparel;

                if (ShouldHideApparel(pawn, apparel))
                {
                    yield break;
                }

                foreach (var graphic in __result)
                    yield return graphic;
            }
        }

        [HarmonyPatch(typeof(PawnRenderNode_Apparel), nameof(PawnRenderNode_Apparel.MeshSetFor))]
        public static class H_MeshSetFor
        {
            public static void Postfix(ref GraphicMeshSet __result, PawnRenderNode_Apparel __instance, Pawn pawn)
            {
                if (!Settings.ShowHairUnderHats)
                {
                    return;
                }
                
                if (pawn == null || !pawn.Spawned || pawn.Map == null || !pawn.RaceProps.Humanlike)
                {
                    return;
                }

                if (Math.Abs(Settings.hairMeshScale - 1f) < 0.001f)
                {
                    return;
                }

                if (!__instance.useHeadMesh)
                {
                    return;
                }

                if (__instance.Props.overrideMeshSize != null)
                {
                    __result = MeshPool.GetMeshSetForSize(__instance.Props.overrideMeshSize.Value.x * Settings.hairMeshScale, __instance.Props.overrideMeshSize.Value.y * Settings.hairMeshScale);
                    return;
                }

                __result = HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn, 1f * Settings.hairMeshScale, 1f * Settings.hairMeshScale);
            }
        }
        
        [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.LayerFor))]
        public static class H_LayerFor
        {
            public static void Postfix(PawnRenderNode node, ref float __result)
            {
                if (Settings.ShowHairUnderHats && node is PawnRenderNode_Apparel apparelNode && apparelNode.useHeadMesh && Math.Abs(Settings.hairMeshScale - 1f) >= 0.001f)
                {
                    __result += 0.5f;
                }
            }
        }

        private static readonly Dictionary<ThingDef, bool> hidesHeadCache = new Dictionary<ThingDef, bool>();

        // any mod extension with a bool hideHead field, checked by name so there is no dependency on VEF
        public static bool HidesHead(ThingDef def)
        {
            if (!hidesHeadCache.TryGetValue(def, out var hides))
            {
                hides = def.modExtensions != null && def.modExtensions.Any(e => e.GetType().GetField("hideHead") is FieldInfo f && f.FieldType == typeof(bool) && (bool)f.GetValue(e));
                hidesHeadCache[def] = hides;
            }
            return hides;
        }

        public static bool ShouldHideApparel(Pawn pawn, Apparel apparel)
        {
            if (Settings.apparelHidingMode == ApparelHidingMode.Never)
            {
                return false;
            }
            
            if (!pawn.Spawned || pawn.Map == null || apparel == null || pawn.Drafted || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            // apparel that replaces the head (VFE Pirates warcasket helmets via VEF ApparelExtension.hideHead) would leave a headless pawn
            if (HidesHead(apparel.def))
            {
                return false;
            }

            // Check if pawn should have apparel hidden based on mod settings
            if (Settings.apparelHidingMode == ApparelHidingMode.WhenNotDrafted || Settings.apparelHidingMode == ApparelHidingMode.IndoorsOrNotDrafted)
            {
                if (pawn.Drafted)
                {
                    return false;  
                }
            }
            
            if (Settings.apparelHidingMode == ApparelHidingMode.IndoorsOrNotDrafted)
            {
                if (pawn.Position.UsesOutdoorTemperature(pawn.MapHeld))
                {
                    return false;
                }
            }

            // Check if this apparel matches your filter criteria
            foreach (var apparelLayerDef in apparel.def.apparel.layers)
            {
                if (Settings.LayerVis.Contains(apparelLayerDef.defName))
                {
                    // Check if it's in the hat filter exception list
                    if (Settings.hatfilter.Contains(apparel.def))
                    {
                        return false; // Don't hide this one
                    }

                    return true; // Hide this apparel
                }
            }

            return false;
        }
    }
}