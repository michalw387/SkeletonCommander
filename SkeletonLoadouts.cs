using System.Collections.Generic;
using HarmonyLib;

namespace ValheimSkeletonTracker
{
    internal enum SkeletonLoadout
    {
        Random,
        SwordAndShield,
        Bow
    }

    // Humanoid.GiveDefaultItems rolls a creature's equipment from m_seed (stored in its ZDO) every time
    // it loads. For a freshly summoned skeleton we reroll with other seeds until it gets the preferred
    // loadout and store that seed, so the choice persists across reloads and is the same for every client.
    // The roll is checked on the items actually given rather than predicted: giving an item instantiates
    // it, which can consume random numbers between the rolls.
    internal static class SkeletonLoadouts
    {
        private const double NewSpawnSeconds = 5.0;
        private const int MaxAttempts = 200;

        private static readonly AccessTools.FieldRef<Humanoid, int> Seed =
            AccessTools.FieldRefAccess<Humanoid, int>("m_seed");

        private static bool _rerolling;

        // Called after Humanoid.GiveDefaultItems.
        public static void Apply(Humanoid humanoid)
        {
            var loadout = SkeletonTrackerPlugin.NextSummonLoadout.Value;
            if (_rerolling || loadout == SkeletonLoadout.Random || !SummonedSkeletons.IsSummonedSkeleton(humanoid))
                return;

            // Only the summoner owns a new skeleton; older skeletons keep the weapon they already have.
            var nview = humanoid.GetComponent<ZNetView>();
            var ai = humanoid.GetBaseAI();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()
                || ai == null || ai.GetTimeSinceSpawned().TotalSeconds > NewSpawnSeconds)
                return;

            int seed = Seed(humanoid);
            var inventory = humanoid.GetInventory();
            _rerolling = true;
            try
            {
                for (int i = 1; i < MaxAttempts && !Matches(loadout, inventory.GetAllItems()); i++)
                {
                    int candidate = unchecked(seed + i * 7919);
                    if (candidate == 0)
                        continue;
                    humanoid.UnequipAllItems();
                    inventory.RemoveAll();
                    Seed(humanoid) = candidate;
                    nview.GetZDO().Set(ZDOVars.s_seed, candidate);
                    humanoid.GiveDefaultItems();
                }
            }
            finally
            {
                _rerolling = false;
            }
        }

        // A melee roll does not always include a shield, so SwordAndShield requires both a melee weapon and a shield.
        private static bool Matches(SkeletonLoadout loadout, List<ItemDrop.ItemData> items)
        {
            bool hasBow = false;
            bool hasMelee = false;
            bool hasShield = false;
            foreach (var item in items)
            {
                if (SummonedSkeletons.IsRangedWeapon(item))
                    hasBow = true;
                else if (item.IsWeapon())
                    hasMelee = true;
                else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                    hasShield = true;
            }
            return loadout == SkeletonLoadout.Bow ? hasBow : !hasBow && hasMelee && hasShield;
        }
    }
}
