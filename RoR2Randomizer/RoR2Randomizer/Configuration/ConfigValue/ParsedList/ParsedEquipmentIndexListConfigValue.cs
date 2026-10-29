using BepInEx.Configuration;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoR2Randomizer.Configuration.ConfigValue.ParsedList
{
    public class ParsedEquipmentIndexListConfigValue : ParsedListConfigValue<EquipmentIndex>
    {
        public ParsedEquipmentIndexListConfigValue(ConfigEntry<string> entry) : base(entry)
        {
            if (!EquipmentCatalog.availability.available)
            {
                EquipmentCatalog.availability.CallWhenAvailable(() => tryParseToList());
                //EquipmentCatalog.availability.onAvailable += tryParseToList;
            }
        }

        public static EquipmentIndex FindEquipmentIndexCaseInsensitive(string equipmentName)
        {
            return EquipmentCatalog.equipmentDefs.FirstOrDefault(ed => string.Equals(ed.name, equipmentName, StringComparison.OrdinalIgnoreCase))?.equipmentIndex ?? EquipmentIndex.None;
        }

        protected override IEnumerable<EquipmentIndex> parse(string[] values)
        {
            if (!EquipmentCatalog.availability.available)
                return Enumerable.Empty<EquipmentIndex>();

            return values.Select(equipmentName =>
            {
                equipmentName = equipmentName.Trim();

                EquipmentIndex equipmentIndex = FindEquipmentIndexCaseInsensitive(equipmentName);
                if (equipmentIndex == EquipmentIndex.None)
                {
                    Log.Warning($"Could not find equipment with name \"{equipmentName}\"");
                }

                return equipmentIndex;
            }).Where(i => i != EquipmentIndex.None)
              .Distinct()
              .OrderBy(i => i);
        }
    }
}
