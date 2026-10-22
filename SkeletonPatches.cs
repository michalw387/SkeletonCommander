using System.Collections;
using System.Collections.Generic;
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

        // When a summon starts following its player the game unsummons the oldest ones above the limit, but
        // it only counts skeletons following that player, so ones told to stay let you exceed the limit.
        // For Dead Raiser skeletons count all of the player's skeletons, staying ones included.
        [HarmonyPatch(typeof(Tameable), "UnsummonMaxInstances")]
        [HarmonyPrefix]
        private static bool EnforceSummonLimit(Tameable __instance, int maxInstances,
            ZNetView ___m_nview, MonsterAI ___m_monsterAI)
        {
            if (!SummonedSkeletons.IsSummonedSkeleton(__instance))
                return true;
            if (___m_nview == null || !___m_nview.IsValid() || !___m_nview.IsOwner() || ___m_monsterAI == null)
                return false;

            var followTarget = ___m_monsterAI.GetFollowTarget();
            var player = followTarget != null ? followTarget.GetComponent<Player>() : null;
            if (player == null)
                return false;

            // Sorted newest first, so the oldest skeletons are at the end.
            var skeletons = new List<Character>();
            SummonedSkeletons.FindOwned(player, skeletons);
            int excess = skeletons.Count - maxInstances;
            for (int i = 0; i < excess; i++)
                SummonedSkeletons.Dismiss(skeletons[skeletons.Count - 1 - i]);

            if (excess > 0 && Player.m_localPlayer != null)
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, __instance.m_maxSummonReached);
            return false;
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
