using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    [HarmonyPatch]
    internal static class SkeletonPatches
    {
        private static readonly FieldInfo HudHoverTimer =
            AccessTools.Field(AccessTools.Inner(typeof(EnemyHud), "HudData"), "m_hoverTimer");

        // The game shows creature health bars only within 10 m; extend that for our skeletons.
        [HarmonyPatch(typeof(EnemyHud), "TestShow")]
        [HarmonyPostfix]
        private static void ExtendHealthBarRange(Character c, Vector3 ___m_refPoint, ref bool __result)
        {
            if (__result || !SkeletonTrackerPlugin.HealthBars.Value || !SkeletonTrackerPlugin.IsOwned(c))
                return;
            float max = SkeletonTrackerPlugin.HealthBarDistance.Value;
            __result = (c.transform.position - ___m_refPoint).sqrMagnitude < max * max;
        }

        // A creature's bar hides a while after it was last hovered; keep resetting that timer for our skeletons.
        [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
        [HarmonyPrefix]
        private static void KeepHealthBarsVisible(object ___m_huds)
        {
            if (!SkeletonTrackerPlugin.HealthBars.Value)
                return;
            foreach (DictionaryEntry hud in (IDictionary)___m_huds)
            {
                if (SkeletonTrackerPlugin.IsOwned((Character)hud.Key))
                    HudHoverTimer.SetValue(hud.Value, 0f);
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        [HarmonyPostfix]
        private static void AddHotkeyHints(Tameable __instance, ref string __result)
        {
            if (SkeletonTrackerPlugin.IsOwned(__instance.GetComponent<Character>()))
                __result += SkeletonTrackerPlugin.GetHoverHints();
        }
    }
}
