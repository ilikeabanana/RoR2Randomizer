using BepInEx.Configuration;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoR2Randomizer.Configuration.ConfigValue.ParsedList
{
    public class ParsedItemIndexListConfigValue : ParsedListConfigValue<ItemIndex>
    {
        public ParsedItemIndexListConfigValue(ConfigEntry<string> entry) : base(entry)
        {
            if (!ItemCatalog.availability.available)
            {
                ItemCatalog.availability.CallWhenAvailable(() => tryParseToList());
                //ItemCatalog.availability.onAvailable += tryParseToList;
            }
        }

        public static ItemIndex FindItemIndexCaseInsensitive(string itemName)
        {
            return ItemCatalog.itemDefs.FirstOrDefault(item => string.Equals(item.name, itemName, StringComparison.OrdinalIgnoreCase))?.itemIndex ?? ItemIndex.None;
        }

        protected override IEnumerable<ItemIndex> parse(string[] values)
        {
            if (!ItemCatalog.availability.available)
                return Enumerable.Empty<ItemIndex>();

            return values.Select(itemName =>
            {
                itemName = itemName.Trim();

                ItemIndex itemIndex = FindItemIndexCaseInsensitive(itemName);
                if (itemIndex == ItemIndex.None)
                {
                    Log.Warning($"Could not find item with name \"{itemName}\"");
                }

                return itemIndex;
            }).Where(i => i != ItemIndex.None)
              .Distinct()
              .OrderBy(i => i);
        }
    }
}
