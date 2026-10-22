using System.Collections.Generic;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    // Finds and commands the skeletons the local player summoned with the Dead Raiser.
    internal static class SummonedSkeletons
    {
        private const string Prefab = "Skeleton_Friendly";
        private const string DigitWidth = "0.55em";

        // The game only records the follow target (by player name) and clears it when a skeleton
        // is told to stay, so we remember the summoner's ID on the skeleton's ZDO ourselves.
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

        // Same RPC the game uses when a summon is unsummoned (Tameable.UnSummon is private).
        public static void Dismiss(Character skeleton)
        {
            var nview = skeleton.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
                nview.InvokeRPC(ZNetView.Everybody, "RPC_UnSummon");
        }

        public static string GetName(Character skeleton)
        {
            return Localization.instance.Localize(skeleton.GetHoverName());
        }

        // Classified by what the skeleton carries rather than the weapon in hand (the AI swaps weapons),
        // and not by m_skillType, which monster weapons leave at its default (Swords).
        public static string GetWeaponName(Character skeleton)
        {
            var humanoid = skeleton as Humanoid;
            if (humanoid == null)
                return "unarmed";

            bool hasWeapon = false;
            bool hasShield = false;
            foreach (var item in humanoid.GetInventory().GetAllItems())
            {
                if (IsRangedWeapon(item))
                    return "bow";
                hasWeapon |= item.IsWeapon();
                hasShield |= item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;
            }
            if (!hasWeapon)
                return "unarmed";
            return hasShield ? "sword & shield" : "sword";
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

        // One line used by both the chat list and the detailed HUD.
        public static string Describe(Character skeleton, Player player)
        {
            float distance = Vector3.Distance(player.transform.position, skeleton.transform.position);
            string state = IsFollowing(skeleton, player) ? "following" : "staying";
            string hpColor = ColorUtility.ToHtmlStringRGB(GetHealthColor(skeleton));
            // Numbers are padded and monospaced so the right-aligned HUD line keeps its width as they change.
            return $"{GetName(skeleton)} · {GetWeaponName(skeleton)} · <color=#{hpColor}>HP <mspace={DigitWidth}>{skeleton.GetHealth(),3:0}/{skeleton.GetMaxHealth(),-3:0}</mspace></color>"
                + $" · <mspace={DigitWidth}>{distance,3:0}</mspace> m · {state}";
        }
    }
}
