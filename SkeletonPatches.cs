using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    [HarmonyPatch]
    internal static class SkeletonPatches
    {
        private static readonly System.Type HudDataType = AccessTools.Inner(typeof(EnemyHud), "HudData");
        private static readonly FieldInfo HudHoverTimer = AccessTools.Field(HudDataType, "m_hoverTimer");
        private static readonly FieldInfo HudHealthFast = AccessTools.Field(HudDataType, "m_healthFast");
        private static readonly FieldInfo HudHealthFastFriendly = AccessTools.Field(HudDataType, "m_healthFastFriendly");

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

        // Colors our skeletons' bars by health (ColorByHealth). Also, a creature's bar hides a while after it was
        // last hovered, so with AlwaysShow keep resetting that timer for our skeletons.
        [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
        [HarmonyPrefix]
        private static void UpdateOwnedHealthBars(object ___m_huds)
        {
            foreach (DictionaryEntry hud in (IDictionary)___m_huds)
            {
                var character = (Character)hud.Key;
                if (!SkeletonTrackerPlugin.IsOwned(character))
                    continue;

                if (SkeletonTrackerPlugin.HealthBars.Value)
                    HudHoverTimer.SetValue(hud.Value, 0f);

                SetBarColor((GuiBar)HudHealthFast.GetValue(hud.Value), character);
                SetBarColor((GuiBar)HudHealthFastFriendly.GetValue(hud.Value), character);
            }
        }

        // Resetting when disabled restores the game's colors if the option is turned off mid-game.
        private static void SetBarColor(GuiBar bar, Character character)
        {
            if (bar == null)
                return;
            if (SkeletonTrackerPlugin.HealthBarColors.Value)
                bar.SetColor(SummonedSkeletons.GetHealthColor(character));
            else
                bar.ResetColor();
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems))]
        [HarmonyPrefix]
        private static void ChooseSummonLoadout(Humanoid __instance)
        {
            SkeletonLoadouts.Apply(__instance);
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
