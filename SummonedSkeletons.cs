using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SkeletonCommander
{
    // Finds and commands the skeletons the local player summoned with the Dead Raiser.
    internal static class SummonedSkeletons
    {
        private const string Prefab = "Skeleton_Friendly";
        private const string DigitWidth = "0.55em";

        // The game only records the follow target (by player name) and clears it when a skeleton
        // is told to stay, so we remember the summoner's ID on the skeleton's ZDO ourselves.
        // Keeps the mod's former name so skeletons summoned with older versions are still recognised.
        private const string OwnerKey = "SkeletonTracker_owner";

        public static bool IsSummonedSkeleton(Component component)
        {
            return Utils.GetPrefabName(component.gameObject) == Prefab;
        }

        public static void FindOwned(Player player, List<Character> result)
        {
            result.Clear();
            long playerId = player.GetPlayerID();
            string playerName = player.GetPlayerName();

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character.IsDead() || !IsSummonedSkeleton(character))
                    continue;

                var nview = character.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid())
                    continue;

                var zdo = nview.GetZDO();
                bool following = zdo.GetString(ZDOVars.s_follow) == playerName;
                if (following && nview.IsOwner() && zdo.GetLong(OwnerKey) != playerId)
                    zdo.Set(OwnerKey, playerId);

                if (following || zdo.GetLong(OwnerKey) == playerId)
                    result.Add(character);
            }

            // Newest first, so the oldest skeleton ends up at the bottom of the lists.
            result.Sort((a, b) => GetSpawnTime(b).CompareTo(GetSpawnTime(a)));
        }

        private static long GetSpawnTime(Character skeleton)
        {
            return skeleton.GetComponent<ZNetView>().GetZDO().GetLong(ZDOVars.s_spawnTime);
        }

        // Zones load by horizontal position, so distances that decide whether a skeleton stays loaded ignore
        // height (dungeon interiors sit high above their entrance, in the same zone).
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public static bool IsFollowing(Character skeleton, Player player)
        {
            var nview = skeleton.GetComponent<ZNetView>();
            return nview != null && nview.IsValid()
                && nview.GetZDO().GetString(ZDOVars.s_follow) == player.GetPlayerName();
        }

        // Tameable.Command toggles between following and staying, so only send it when the state differs.
        public static void SetFollowing(Character skeleton, Player player, bool follow)
        {
            if (IsFollowing(skeleton, player) == follow)
                return;
            var tameable = skeleton.GetComponent<Tameable>();
            if (tameable != null)
                tameable.Command(player, message: false);
        }

        // Same RPC the game uses when a summon is unsummoned (Tameable.UnSummon is private). Only the owner
        // of the skeleton's ZDO removes it, and a skeleton outside the player's active area can still be
        // loaded with no owner at all, so take it over first.
        public static void Dismiss(Character skeleton)
        {
            var nview = skeleton.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
                return;
            if (!nview.IsOwner() && !nview.GetZDO().HasOwner())
                nview.ClaimOwnership();
            nview.InvokeRPC(ZNetView.Everybody, "RPC_UnSummon");
        }

        // Dismisses the oldest skeletons above maxInstances from a list sorted newest first (as FindOwned
        // returns it) and removes them from the list. Returns how many were dismissed.
        public static int DismissOldest(List<Character> skeletons, int maxInstances)
        {
            int excess = skeletons.Count - maxInstances;
            for (int i = 0; i < excess; i++)
            {
                Dismiss(skeletons[skeletons.Count - 1]);
                skeletons.RemoveAt(skeletons.Count - 1);
            }
            return System.Math.Max(excess, 0);
        }

        // The summon limit the skeleton was summoned with (the Dead Raiser's upgrade level); 0 means no limit.
        public static int GetMaxInstances(Character skeleton)
        {
            return skeleton.GetComponent<ZNetView>().GetZDO().GetInt(ZDOVars.s_maxInstances);
        }

        public static string GetName(Character skeleton)
        {
            return Localization.instance.Localize(skeleton.GetHoverName());
        }

        // Classified by what the skeleton carries rather than the weapon in hand (the AI swaps weapons),
        // and not by m_skillType, which monster weapons leave at its default (Swords).
        public static string GetWeaponName(Character skeleton)
        {
            if (!(skeleton is Humanoid humanoid))
                return "unarmed";
            Classify(humanoid.GetInventory().GetAllItems(), out bool bow, out bool melee, out bool shield);
            if (bow)
                return "bow";
            if (!melee)
                return "unarmed";
            return shield ? "sword & shield" : "sword";
        }

        // Bow for archers, SwordAndShield for any melee skeleton (with or without a shield), Random when unarmed.
        public static SkeletonLoadout GetLoadout(Character skeleton)
        {
            if (!(skeleton is Humanoid humanoid))
                return SkeletonLoadout.Random;
            Classify(humanoid.GetInventory().GetAllItems(), out bool bow, out bool melee, out _);
            if (bow)
                return SkeletonLoadout.Bow;
            return melee ? SkeletonLoadout.SwordAndShield : SkeletonLoadout.Random;
        }

        public static void Classify(List<ItemDrop.ItemData> items, out bool bow, out bool melee, out bool shield)
        {
            bow = melee = shield = false;
            foreach (var item in items)
            {
                if (IsRangedWeapon(item))
                    bow = true;
                else if (item.IsWeapon())
                    melee = true;
                else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                    shield = true;
            }
        }

        public static bool IsRangedWeapon(ItemDrop.ItemData item)
        {
            return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow
                || (item.IsWeapon() && item.m_shared.m_attack != null
                    && item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile);
        }

        // Green above 60% health, yellow above 30%, red below.
        public static Color GetHealthColor(Character skeleton)
        {
            float health = skeleton.GetHealthPercentage();
            if (health > 0.6f)
                return new Color(0.3f, 0.9f, 0.3f);
            if (health > 0.3f)
                return new Color(1f, 0.85f, 0.2f);
            return new Color(1f, 0.25f, 0.2f);
        }

        // One line used by both the chat list and the detailed HUD; the HUD can leave parts out.
        // padded: pad numbers to a fixed width so a line keeps its width as they change (for text lists).
        public static string Describe(Character skeleton, Player player, bool name = true, bool weapon = true,
            bool health = true, bool distance = true, bool state = true, bool padded = true, bool shield = true)
        {
            var parts = new List<string>();
            if (name)
                parts.Add(GetName(skeleton));
            if (weapon)
                parts.Add(GetWeaponName(skeleton));
            // Numbers are padded and monospaced so the HUD line keeps its width as they change.
            if (health)
            {
                string hpColor = ColorUtility.ToHtmlStringRGB(GetHealthColor(skeleton));
                parts.Add($"<color=#{hpColor}>HP <mspace={DigitWidth}>{skeleton.GetHealth(),3:0}/{skeleton.GetMaxHealth(),-3:0}</mspace></color>");
            }
            if (distance)
            {
                float meters = Vector3.Distance(player.transform.position, skeleton.transform.position);
                parts.Add(padded ? $"<mspace={DigitWidth}>{meters,3:0}</mspace> m" : $"{meters:0} m");
            }
            if (shield && TryGetShield(skeleton, out float remaining, out float total, out float seconds))
                parts.Add($"<color=#73BFFF>shield {remaining:0}/{total:0} ({seconds:0} s)</color>");
            if (state)
                parts.Add(IsFollowing(skeleton, player) ? "following" : "staying");
            return string.Join(" · ", parts);
        }

        private static readonly AccessTools.FieldRef<SE_Shield, float> ShieldTotal =
            AccessTools.FieldRefAccess<SE_Shield, float>("m_totalAbsorbDamage");
        private static readonly AccessTools.FieldRef<SE_Shield, float> ShieldDamage =
            AccessTools.FieldRefAccess<SE_Shield, float>("m_damage");

        // The barrier from the Staff of Protection (SE_Shield is the game's only shield effect). Status
        // effects exist only on the skeleton's owner, normally its summoner; others see no shield.
        public static bool TryGetShield(Character skeleton, out float remaining, out float total, out float seconds)
        {
            foreach (var effect in skeleton.GetSEMan().GetStatusEffects())
            {
                if (!(effect is SE_Shield shield))
                    continue;
                total = ShieldTotal(shield);
                remaining = Mathf.Max(0f, total - ShieldDamage(shield));
                seconds = Mathf.Max(0f, shield.GetRemaningTime());
                return true;
            }
            remaining = total = seconds = 0f;
            return false;
        }
    }
}
