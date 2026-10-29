using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.RandomizerControllers.Item;
using System.Reflection;

namespace RoR2Randomizer.Patches.ItemRandomizer
{
    [PatchClass]
    static class CaptainDefenseMatrix_GenericPickupPatch
    {
        static void Apply()
        {
            IL.RoR2.CaptainDefenseMatrixController.TryGrantItem += CaptainDefenseMatrixController_TryGrantItem;
            IL.RoR2.CaptainDefenseMatrixController.OnServerMasterSummonGlobal += CaptainDefenseMatrixController_OnServerMasterSummonGlobal;
        }

        static void Cleanup()
        {
            IL.RoR2.CaptainDefenseMatrixController.TryGrantItem -= CaptainDefenseMatrixController_TryGrantItem;
            IL.RoR2.CaptainDefenseMatrixController.OnServerMasterSummonGlobal -= CaptainDefenseMatrixController_OnServerMasterSummonGlobal;
        }

        [SystemInitializer(typeof(PickupCatalog), typeof(ItemCatalog))]
        static void InitPickupIndices()
        {
            _defenseMatrixPickupIndex = PickupCatalog.FindPickupIndex(RoR2Content.Items.CaptainDefenseMatrix.itemIndex);
        }

        static PickupIndex _defenseMatrixPickupIndex;

        static readonly FieldInfo _temporaryInventoryStorage_FI = AccessTools.DeclaredField(typeof(CaptainDefenseMatrix_GenericPickupPatch), nameof(_temporaryInventoryStorage));

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649 // Field 'CaptainDefenseMatrix_GenericPickupPatch._temporaryInventoryStorage' is never assigned to, and will always have its default value null
        static Inventory _temporaryInventoryStorage;
#pragma warning restore CS0649 // Field 'CaptainDefenseMatrix_GenericPickupPatch._temporaryInventoryStorage' is never assigned to, and will always have its default value null
#pragma warning restore IDE0044 // Add readonly modifier

        static bool tryGrantReplacementPickup(Inventory inventory, int defaultCount)
        {
            if (!inventory || !_defenseMatrixPickupIndex.isValid)
                return false;

            if (!ItemRandomizerController.TryGetReplacementPickupIndex(_defenseMatrixPickupIndex, out PickupIndex defenseMatrixReplacementIndex))
                return false;

            PickupDef defenseMatrixReplacement = defenseMatrixReplacementIndex.pickupDef;
            if (defenseMatrixReplacement == null ||
                defenseMatrixReplacement.IsItem()) // If Item: Use default code
            {
                return false;
            }

            int count;
            if (defenseMatrixReplacement.IsEquipment())
            {
                count = 1;
            }
            else
            {
                count = defaultCount;
            }

            bool result = defenseMatrixReplacement.TryGrantTo(inventory, count, true);
            ItemRandomizerController.HandleCharacterGrantedRandomizedEquipment(inventory.GetComponent<CharacterMaster>());
            return result;
        }

        static readonly MethodInfo getReplacementDefenseMatrixItemDef_MI = AccessTools.DeclaredMethod(typeof(CaptainDefenseMatrix_GenericPickupPatch), nameof(getReplacementDefenseMatrixItemDef));
        static ItemDef getReplacementDefenseMatrixItemDef(ItemDef defenseMatrixItemDef)
        {
            if (!defenseMatrixItemDef)
                return defenseMatrixItemDef;

            PickupIndex defenseMatrixPickupIndex = PickupCatalog.FindPickupIndex(defenseMatrixItemDef.itemIndex);
            if (!defenseMatrixPickupIndex.isValid)
                return defenseMatrixItemDef;

            if (!ItemRandomizerController.TryGetReplacementPickupIndex(defenseMatrixPickupIndex, out PickupIndex defenseMatrixReplacementIndex))
                return defenseMatrixItemDef;

            PickupDef defenseMatrixReplacementPickup = defenseMatrixReplacementIndex.pickupDef;
            if (!defenseMatrixReplacementPickup.IsItem())
            {
                Log.Warning("TryGrant returned false, but replacement is not an item, using original item");
                return defenseMatrixItemDef;
            }

            ItemDef replacementItem = ItemCatalog.GetItemDef(defenseMatrixReplacementPickup.itemIndex);
            if (replacementItem)
            {
                return replacementItem;
            }
            else
            {
                return defenseMatrixItemDef;
            }
        }

        static void CaptainDefenseMatrixController_OnServerMasterSummonGlobal(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.CaptainDefenseMatrix)),
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<Inventory>(_ => _.GiveItemPermanent(default(ItemDef), default)))))
            {
                ILCursor cursor = foundCursors[0];

                cursor.Emit(OpCodes.Stsfld, _temporaryInventoryStorage_FI)
                      .Emit(OpCodes.Ldsfld, _temporaryInventoryStorage_FI);

                cursor.Emit(OpCodes.Ldarg_0);

                cursor.EmitDelegate((Inventory inventory, CaptainDefenseMatrixController instance) =>
                {
                    return tryGrantReplacementPickup(inventory, instance.defenseMatrixToGrantMechanicalAllies);
                });

                ILLabel afterGiveItemLabel = il.DefineLabel();

                cursor.Emit(OpCodes.Brtrue, afterGiveItemLabel);

                cursor.Emit(OpCodes.Ldsfld, _temporaryInventoryStorage_FI);

                cursor.Index++;
                cursor.Emit(OpCodes.Call, getReplacementDefenseMatrixItemDef_MI);

                cursor.Index = foundCursors[1].Index + 1;
                cursor.MarkLabel(afterGiveItemLabel);
            }
            else
            {
                Log.Error("Failed to find patch location");
            }
        }

        static void CaptainDefenseMatrixController_TryGrantItem(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.CaptainDefenseMatrix)),
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<Inventory>(_ => _.GetItemCountEffective(default(ItemDef))))))
            {
                ILCursor cursor = foundCursors[0];

                cursor.Emit(OpCodes.Stsfld, _temporaryInventoryStorage_FI)
                      .Emit(OpCodes.Ldsfld, _temporaryInventoryStorage_FI);

                cursor = foundCursors[1];
                cursor.Index++;

                cursor.Emit(OpCodes.Ldsfld, _temporaryInventoryStorage_FI);

                cursor.EmitDelegate((int defenseMatrixItemCount, Inventory inventory) =>
                {
                    if (!inventory || !_defenseMatrixPickupIndex.isValid)
                        return defenseMatrixItemCount;

                    if (!ItemRandomizerController.TryGetReplacementPickupIndex(_defenseMatrixPickupIndex, out PickupIndex defenseMatrixReplacementIndex))
                        return defenseMatrixItemCount;

                    return inventory.GetPickupCount(defenseMatrixReplacementIndex.pickupDef);
                });

                c.Index = cursor.Index + 1;
            }
            else
            {
                Log.Error("Failed to find GetItemCount patch location");
            }

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.CaptainDefenseMatrix)),
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<Inventory>(_ => _.GiveItemPermanent(default(ItemDef), default)))))
            {
                ILCursor cursor = foundCursors[0];

                cursor.Emit(OpCodes.Stsfld, _temporaryInventoryStorage_FI)
                      .Emit(OpCodes.Ldsfld, _temporaryInventoryStorage_FI);

                cursor.Emit(OpCodes.Ldarg_0);

                cursor.EmitDelegate((Inventory inventory, CaptainDefenseMatrixController instance) =>
                {
                    return tryGrantReplacementPickup(inventory, instance.defenseMatrixToGrantPlayer);
                });

                ILLabel afterGiveItemLabel = il.DefineLabel();

                cursor.Emit(OpCodes.Brtrue, afterGiveItemLabel);

                cursor.Emit(OpCodes.Ldsfld, _temporaryInventoryStorage_FI);

                cursor.Index++;
                cursor.Emit(OpCodes.Call, getReplacementDefenseMatrixItemDef_MI);

                cursor = foundCursors[1];
                cursor.Index++;
                cursor.MarkLabel(afterGiveItemLabel);
            } 
            else
            {
                Log.Error("Failed to find GiveItem patch location");
            }
        }
    }
}
