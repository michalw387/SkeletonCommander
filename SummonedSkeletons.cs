using System.Collections.Generic;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    // Finds and commands the skeletons the local player summoned with the Dead Raiser.
    internal static class SummonedSkeletons
    {
        private const string Prefab = "Skeleton_Friendly";

        // The game only records the follow target (by player name) and clears it when a skeleton
        // is told to stay, so we remember the summoner's ID on the skeleton's ZDO ourselves.
        private const string OwnerKey = "SkeletonTracker_owner";

        public static void FindOwned(Player player, List<Character> result)
        {
            result.Clear();
            long playerId = player.GetPlayerID();
            string playerName = player.GetPlayerName();

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character.IsDead() || Utils.GetPrefabName(character.gameObject) != Prefab)
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

        public static string GetWeaponName(Character skeleton)
        {
            var weapon = (skeleton as Humanoid)?.GetCurrentWeapon();
            if (weapon == null)
                return "brak broni";
            switch (weapon.m_shared.m_skillType)
            {
                case Skills.SkillType.Bows: return "łuk";
                case Skills.SkillType.Swords: return "miecz";
                case Skills.SkillType.Clubs: return "maczuga";
                case Skills.SkillType.Axes: return "topór";
                case Skills.SkillType.Unarmed: return "bez broni";
                default: return Localization.instance.Localize(weapon.m_shared.m_name);
            }
        }

        // One line used by both the chat list and the detailed HUD.
        public static string Describe(Character skeleton, Player player)
        {
            float distance = Vector3.Distance(player.transform.position, skeleton.transform.position);
            string state = IsFollowing(skeleton, player) ? "podąża" : "czeka";
            return $"{GetName(skeleton)} · {GetWeaponName(skeleton)} · HP {skeleton.GetHealth():0}/{skeleton.GetMaxHealth():0} · {distance:0} m · {state}";
        }
    }
}
