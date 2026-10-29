using RoR2;
using System.Linq;

namespace RoR2Randomizer.Extensions
{
    public static class InventoryExtensions
    {
        public static bool HasItems(this GivePickupsOnStart givePickupsOnStart, ItemIndex item)
        {
            return HasItems(givePickupsOnStart, item, out _);
        }

        public static bool HasItems(this GivePickupsOnStart givePickupsOnStart, ItemIndex item, out int count)
        {
            if (item == ItemIndex.None)
            {
                return (count = givePickupsOnStart.itemDefInfos.Length + givePickupsOnStart.itemInfos.Length) > 0;
            }

            return (count = givePickupsOnStart.itemDefInfos.Count(i => i.itemDef.itemIndex == item) +
                            givePickupsOnStart.itemInfos.Count(i => ItemCatalog.FindItemIndex(i.itemString) == item)) > 0;
        }

        public static bool HasAnyItems(this GivePickupsOnStart givePickupsOnStart)
        {
            return givePickupsOnStart.itemDefInfos.Length > 0 || givePickupsOnStart.itemInfos.Length > 0;
        }

        public static bool HasEquipment(this GivePickupsOnStart givePickupsOnStart, EquipmentIndex equipment)
        {
            return (givePickupsOnStart.equipmentDef && givePickupsOnStart.equipmentDef.equipmentIndex == equipment)
                || (!string.IsNullOrEmpty(givePickupsOnStart.equipmentString) && EquipmentCatalog.FindEquipmentIndex(givePickupsOnStart.equipmentString) == equipment);
        }

        public static bool HasAnyEquipment(this GivePickupsOnStart givePickupsOnStart)
        {
            return givePickupsOnStart.equipmentDef || !string.IsNullOrEmpty(givePickupsOnStart.equipmentString);
        }

        public static void GiveItemIfMissing(this Inventory inventory, ItemDef item, int count = 1)
        {
            inventory.GiveItemIfMissing(item.itemIndex, count);
        }

        public static void GiveItemIfMissing(this Inventory inventory, ItemIndex item, int count = 1)
        {
            int currentItemCount = inventory.GetItemCountEffective(item);
            if (currentItemCount < count)
            {
                inventory.GiveItemPermanent(item, count - currentItemCount);
            }
        }
    }
}
