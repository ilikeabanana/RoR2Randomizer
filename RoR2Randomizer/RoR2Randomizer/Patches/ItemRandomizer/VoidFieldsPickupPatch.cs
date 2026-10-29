using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.CustomContent;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.RandomizerControllers.Item;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace RoR2Randomizer.Patches.ItemRandomizer
{
    [PatchClass]
    static class VoidFieldsPickupPatch
    {
        static void Apply()
        {
            On.RoR2.ArenaMissionController.ModifySpawnedMasters += ArenaMissionController_ModifySpawnedMasters_CopyEquipment;
            IL.RoR2.ArenaMissionController.AddItemStack += ArenaMissionController_AddItemStack_GenericPickupPatch;
        }

        static void Cleanup()
        {
            On.RoR2.ArenaMissionController.ModifySpawnedMasters -= ArenaMissionController_ModifySpawnedMasters_CopyEquipment;
            IL.RoR2.ArenaMissionController.AddItemStack -= ArenaMissionController_AddItemStack_GenericPickupPatch;
        }

        static void ArenaMissionController_ModifySpawnedMasters_CopyEquipment(On.RoR2.ArenaMissionController.orig_ModifySpawnedMasters orig, ArenaMissionController self, UnityEngine.GameObject targetGameObject)
        {
            orig(self, targetGameObject);

            if (NetworkServer.active &&
                targetGameObject &&
                targetGameObject.TryGetComponent(out CharacterMaster targetMaster))
            {
                targetMaster.inventory.CopyEquipmentFrom(self.inventory);

                ItemRandomizerController.HandleCharacterGrantedRandomizedEquipment(targetMaster);
            }
        }

        static void ArenaMissionController_AddItemStack_GenericPickupPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;

            int pickupDefLocalIndex = -1;
            if (c.TryFindNext(out foundCursors,
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo(() => PickupCatalog.GetPickupDef(default))),
                              x => x.MatchStloc(out pickupDefLocalIndex)))
            {
                ILCursor cursor = foundCursors[1];
                cursor.Index++;

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc, pickupDefLocalIndex);
                cursor.EmitDelegate((ArenaMissionController instance, PickupDef pickupDef) =>
                {
                    if (pickupDef == null ||
                        pickupDef.IsItem()) // Item: Use unmodified method code
                    {
                        return false;
                    }

                    if (!instance)
                        return false;

                    Inventory inventory = instance.inventory;
                    if (!inventory)
                        return false;

                    int grantCount;
                    if (pickupDef.IsEquipment())
                    {
                        grantCount = 1;
                    }
                    else
                    {
                        grantCount = instance.monsterItemStackOrder[instance.nextItemStackIndex].stacks;
                    }

                    return pickupDef.TryGrantTo(inventory, grantCount, true);
                });

                ILLabel afterGiveItemLabel = il.DefineLabel();

                cursor.Emit(OpCodes.Brtrue, afterGiveItemLabel);

                if (cursor.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<Inventory>(_ => _.GiveItemPermanent(default(ItemIndex), default)))))
                {
                    cursor.MarkLabel(afterGiveItemLabel);
                }
                else
                {
                    Log.Error($"Unable to find {nameof(afterGiveItemLabel)} location");
                }
            }
            else
            {
                Log.Error("Unable to find patch location");
            }
        }
    }
}
