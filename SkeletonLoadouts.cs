using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    internal enum SkeletonLoadout
    {
        Random,
        SwordAndShield,
        Bow
    }

    // Humanoid.GiveDefaultItems rolls a creature's equipment from m_seed (stored in its ZDO) every time
    // it loads. For a freshly summoned skeleton we pick a seed whose roll gives the preferred loadout,
    // so the choice persists across reloads and is the same for every client.
    internal static class SkeletonLoadouts
    {
        private const double NewSpawnSeconds = 5.0;
        private const int MaxAttempts = 200;

        private static readonly AccessTools.FieldRef<Humanoid, int> Seed =
            AccessTools.FieldRefAccess<Humanoid, int>("m_seed");

        // Called before Humanoid.GiveDefaultItems.
        public static void Apply(Humanoid humanoid)
        {
            var loadout = SkeletonTrackerPlugin.NextSummonLoadout.Value;
            if (loadout == SkeletonLoadout.Random || !SummonedSkeletons.IsSummonedSkeleton(humanoid))
                return;

            // Only the summoner owns a new skeleton; older skeletons keep the weapon they already have.
            var nview = humanoid.GetComponent<ZNetView>();
            var ai = humanoid.GetBaseAI();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()
                || ai == null || ai.GetTimeSinceSpawned().TotalSeconds > NewSpawnSeconds)
                return;

            int seed = Seed(humanoid);
            for (int i = 0; i < MaxAttempts; i++)
            {
                int candidate = unchecked(seed + i * 7919);
                if (candidate == 0 || HasBow(humanoid, candidate) != (loadout == SkeletonLoadout.Bow))
                    continue;
                if (candidate != seed)
                {
                    Seed(humanoid) = candidate;
                    nview.GetZDO().Set(ZDOVars.s_seed, candidate);
                }
                return;
            }
        }

        private static bool HasBow(Humanoid humanoid, int seed)
        {
            foreach (var prefab in RollItems(humanoid, seed))
            {
                var itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (itemDrop != null && SummonedSkeletons.IsRangedWeapon(itemDrop.m_itemData))
                    return true;
            }
            return false;
        }

        // Mirrors the roll order in Humanoid.GiveDefaultItems.
        private static List<GameObject> RollItems(Humanoid humanoid, int seed)
        {
            var items = new List<GameObject>(humanoid.m_defaultItems);
            var state = Random.state;
            Random.InitState(seed);

            if (humanoid.m_randomShield.Length != 0)
                items.Add(humanoid.m_randomShield[Random.Range(0, humanoid.m_randomShield.Length)]);
            if (humanoid.m_randomWeapon.Length != 0)
                items.Add(humanoid.m_randomWeapon[Random.Range(0, humanoid.m_randomWeapon.Length)]);
            if (humanoid.m_randomArmor.Length != 0)
                items.Add(humanoid.m_randomArmor[Random.Range(0, humanoid.m_randomArmor.Length)]);
            if (humanoid.m_randomSets.Length != 0)
                items.AddRange(humanoid.m_randomSets[Random.Range(0, humanoid.m_randomSets.Length)].m_items);
            var filledSlots = new HashSet<ItemDrop.ItemData.ItemType>();
            foreach (var randomItem in humanoid.m_randomItems)
            {
                if (randomItem.m_prefab == null || !(Random.value > randomItem.m_chance))
                    continue;
                var itemType = randomItem.m_prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_itemType;
                if (filledSlots.Add(itemType))
                    items.Add(randomItem.m_prefab);
            }

            Random.state = state;
            return items;
        }
    }
}
