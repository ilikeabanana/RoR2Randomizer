using RoR2;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RoR2Randomizer.Extensions
{
    public static class PickupExtensions
    {
        static bool tryGrantTo(this PickupDef pickupDef, Inventory inventory, int count, bool forceOverrideEquipment, out bool notify)
        {
            if (pickupDef == null || !inventory || count <= 0)
            {
                notify = false;
                return false;
            }

            if (pickupDef.IsItem())
            {
                if (inventory)
                {
                    inventory.GiveItemPermanent(pickupDef.itemIndex, count);
                    notify = true;
                }
                else
                {
                    notify = false;
                }
            }
            else if (pickupDef.IsEquipment())
            {
                if (inventory)
                {
                    if (forceOverrideEquipment)
                    {
                        for (uint i = 0; i < count; i++)
                        {
                            inventory.SetEquipmentIndexForSlot(pickupDef.equipmentIndex, i);
                        }
                    }
                    else
                    {
                        uint slotCount = (uint)inventory.GetEquipmentSlotCount();

                        bool giveEquipments(bool requireEmpty)
                        {
                            if (count <= 0)
                                return false;

                            for (uint i = 0; i < slotCount; i++)
                            {
                                EquipmentState equipmentState = inventory.GetEquipment(i);
                                if (equipmentState.equipmentDef && (requireEmpty ? equipmentState.equipmentDef.equipmentIndex == EquipmentIndex.None
                                                                                 : equipmentState.equipmentDef.equipmentIndex != pickupDef.equipmentIndex))
                                {
                                    count--;
                                    inventory.SetEquipmentIndexForSlot(pickupDef.equipmentIndex, i);

                                    if (count <= 0)
                                        return true;
                                }
                            }

                            return false;
                        }

                        if (!giveEquipments(true))
                        {
                            giveEquipments(false);
                        }
                    }

                    notify = true;
                }
                else
                {
                    notify = false;
                }
            }
            else
            {
                Log.Warning($"pickup {pickupDef} not implemented");
                notify = false;
                return false;
            }

            return true;
        }

        public static bool TryGrantTo(this PickupDef pickupDef, Inventory inventory, int count = 1, bool forceOverrideEquipment = false)
        {
            return pickupDef.tryGrantTo(inventory, count, forceOverrideEquipment, out _);
        }

        public static bool TryGrantTo(this PickupDef pickupDef, CharacterMaster master, int count = 1, bool forceOverrideEquipment = false)
        {
            if (master && pickupDef.tryGrantTo(master.inventory, count, forceOverrideEquipment, out bool notify))
            {
                if (notify && master.playerCharacterMasterController)
                {
                    
                    GenericPickupController.SendPickupMessage(master, new UniquePickup(pickupDef.pickupIndex));
                }

                return true;
            }

            return false;
        }

        public static int GetPickupCount(this CharacterMaster master, PickupDef pickupDef)
        {
            if (pickupDef == null || !master)
                return 0;

            return master.inventory.GetPickupCount(pickupDef);
        }

        public static int GetPickupCount(this Inventory inventory, PickupDef pickupDef)
        {
            if (pickupDef == null || !inventory)
                return 0;

            if (pickupDef.IsItem())
            {
                if (inventory)
                {
                    return inventory.GetItemCountEffective(pickupDef.itemIndex);
                }
            }
            else if (pickupDef.IsEquipment())
            {
                if (inventory)
                {
                    int count = 0;

                    int equipmentSlotCount = inventory.GetEquipmentSlotCount();
                    for (uint i = 0; i < equipmentSlotCount; i++)
                    {
                        EquipmentDef equipmentDef = inventory.GetEquipment(i).equipmentDef;
                        if (equipmentDef && equipmentDef.equipmentIndex == pickupDef.equipmentIndex)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }

            return 0;
        }

        public static void TryDeductFrom(this PickupDef pickupDef, CharacterMaster master, int count = 1)
        {
            if (pickupDef == null || !master || count <= 0)
                return;

            Inventory inventory = master.inventory;
            if (pickupDef.IsItem())
            {
                if (inventory)
                {
                    count = Mathf.Min(count, inventory.GetItemCountPermanent(pickupDef.itemIndex));
                    if (count > 0)
                    {
                        inventory.RemoveItemPermanent(pickupDef.itemIndex, count);
                    }
                }
            }
            else if (pickupDef.IsEquipment())
            {
                if (inventory)
                {
                    int slotCount = inventory.GetEquipmentSlotCount();
                    for (uint i = 0; i < slotCount; i++)
                    {
                        EquipmentState equipmentState = inventory.GetEquipment(i);
                        if (equipmentState.equipmentDef && equipmentState.equipmentDef.equipmentIndex == pickupDef.equipmentIndex)
                        {
                            count--;
                            inventory.SetEquipmentIndexForSlot(EquipmentIndex.None, i);

                            if (count <= 0)
                                break;
                        }
                    }
                }
            }
            else if (pickupDef.miscPickupIndex != MiscPickupIndex.None)
            {
                IReadOnlyList<MiscPickupDef> miscPickupDefs = MiscPickupCatalog.miscPickupDefs;
                if (miscPickupDefs != null)
                {
                    int miscPickupIndex = (int)pickupDef.miscPickupIndex;
                    if (miscPickupIndex >= 0 && miscPickupIndex < miscPickupDefs.Count)
                    {
                        MiscPickupDef miscPickupDef = miscPickupDefs[miscPickupIndex];
                        uint coinValue = miscPickupDef.coinValue;
                        if (miscPickupDef is LunarCoinDef lunarCoinDef)
                        {
                            NetworkUser networkUser = Util.LookUpBodyNetworkUser(master.GetBody());
                            if (networkUser)
                            {
                                networkUser.DeductLunarCoins(coinValue);
                            }
                        }
                        else if (miscPickupDef is VoidCoinDef voidCoinDef)
                        {
                            master.voidCoins -= coinValue;
                        }
                        else
                        {
                            Log.Warning($"{nameof(MiscPickupDef)} {miscPickupDef} is not implemented");
                        }
                    }
                }
            }
            else
            {
                Log.Warning($"pickup {pickupDef} not implemented");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsItem(this PickupDef pickupDef)
        {
            return pickupDef.itemIndex != ItemIndex.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEquipment(this PickupDef pickupDef)
        {
            return pickupDef.equipmentIndex != EquipmentIndex.None;
        }
    }
}
