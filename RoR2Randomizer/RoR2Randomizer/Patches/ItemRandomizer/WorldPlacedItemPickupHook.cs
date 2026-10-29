using HarmonyLib;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.RandomizerControllers.Item;

namespace RoR2Randomizer.Patches.ItemRandomizer
{
    [PatchClass]
    static class WorldPlacedItemPickupHook
    {
        static void Apply()
        {
            IL.RoR2.GenericPickupController.SetPickupIndexFromString += GenericPickupController_SetPickupIndexFromString;
        }

        static void Cleanup()
        {
            IL.RoR2.GenericPickupController.SetPickupIndexFromString -= GenericPickupController_SetPickupIndexFromString;
        }

        static void GenericPickupController_SetPickupIndexFromString(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(x => x.MatchCallOrCallvirt(AccessTools.DeclaredPropertySetter(typeof(GenericPickupController), nameof(GenericPickupController.Network_pickupState)))))
            {
#if DEBUG
                c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                c.EmitDelegate((PickupIndex pickupIndex, GenericPickupController instance) =>
#else
                c.EmitDelegate((PickupIndex pickupIndex) =>
#endif
                {
                    PickupIndex replacementPickup = ItemRandomizerController.GetReplacementPickupIndex(pickupIndex);

#if DEBUG
                    Log.Debug($"Replaced pickup on {instance} at {instance.transform.GetScenePath()}");
#endif

                    return replacementPickup;
                });
            }
        }
    }
}
