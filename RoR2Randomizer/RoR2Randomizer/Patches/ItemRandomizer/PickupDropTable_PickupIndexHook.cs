#if !DISABLE_ITEM_RANDOMIZER
using RoR2;
using RoR2Randomizer.RandomizerControllers.Item;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RoR2Randomizer.Patches.ItemRandomizer
{
    [PatchClass]
    static class PickupDropTable_PickupIndexHook
    {
        static void Apply()
        {
#if DEBUG
            Log.Info($"Patching the pickupdrop tblaes shit");
#endif
            On.RoR2.PickupDropTable.GenerateDrop += PickupDropTable_GenerateDrop;
            On.RoR2.PickupDropTable.GeneratePickup += PickupDropTable_GeneratePickup;
            On.RoR2.PickupDropTable.GenerateUniqueDrops += PickupDropTable_GenerateUniqueDrops;
        }

        private static UniquePickup PickupDropTable_GeneratePickup(On.RoR2.PickupDropTable.orig_GeneratePickup orig, PickupDropTable self, Xoroshiro128Plus rng)
        {
            UniquePickup origami = orig(self, rng);
            UniquePickup rando = new UniquePickup(ItemRandomizerController.GetReplacementPickupIndex(origami.pickupIndex));

#if DEBUG
            Log.Info($"Replacing {origami.pickupIndex.pickupDef.internalName} with {rando.pickupIndex.pickupDef.internalName}");
#endif

            return rando;
        }

        static void Cleanup()
        {
            On.RoR2.PickupDropTable.GenerateDrop -= PickupDropTable_GenerateDrop;
            On.RoR2.PickupDropTable.GeneratePickup -= PickupDropTable_GeneratePickup;
            On.RoR2.PickupDropTable.GenerateUniqueDrops -= PickupDropTable_GenerateUniqueDrops;
        }

        static PickupIndex PickupDropTable_GenerateDrop(On.RoR2.PickupDropTable.orig_GenerateDrop orig, PickupDropTable self, Xoroshiro128Plus rng)
        {
            return ItemRandomizerController.GetReplacementPickupIndex(orig(self, rng));
        }

        static PickupIndex[] PickupDropTable_GenerateUniqueDrops(On.RoR2.PickupDropTable.orig_GenerateUniqueDrops orig, PickupDropTable self, int maxDrops, Xoroshiro128Plus rng)
        {
            PickupIndex[] drops = orig(self, maxDrops, rng);

            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = ItemRandomizerController.GetReplacementPickupIndex(drops[i]);
            }

            return drops;
        }
    }
}
#endif