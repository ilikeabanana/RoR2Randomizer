using BepInEx.Configuration;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Configuration.ConfigValue;

namespace RoR2Randomizer.RandomizerControllers.Boss
{
    public class BossRandomizerConfig : BaseRandomizerConfig
    {
        public readonly BoolConfigValue RandomizeMithrix;
        public readonly BoolConfigValue RandomizeMithrixPhase2;
        public bool AnyMithrixRandomizerEnabled => Enabled && (RandomizeMithrix || RandomizeMithrixPhase2);

        public readonly BoolConfigValue RandomizeVoidling;

        public readonly BoolConfigValue RandomizeFalseSon;

        public readonly BoolConfigValue RandomizeAurelionite;

        public readonly BoolConfigValue RandomizeLunarScav;

        public readonly BoolConfigValue RandomizeAlloyWorshipUnit;

        public readonly BoolConfigValue RandomizeTeleporterBosses;

        public BossRandomizerConfig(ConfigFile file) : base("Boss", file)
        {
            RandomizeMithrix = new BoolConfigValue(getEntry("Randomize Mithrix", "Randomizes the character type of Mithrix", true));
            RandomizeMithrixPhase2 = new BoolConfigValue(getEntry("Randomize Mithrix Phase 2", "Randomizes the character type of all the characters spawned during Mithrix phase 2", true));

            RandomizeVoidling = new BoolConfigValue(getEntry("Randomize Voidling", "Randomizes the character type of Voidling", true));
            RandomizeFalseSon = new BoolConfigValue(getEntry("Randomize False Son", "Randomizes the character type of False Son", true));

            RandomizeAurelionite = new BoolConfigValue(getEntry("Randomize Aurelionite", "Randomizes the character type of Aurelionite", true));

            RandomizeLunarScav = new BoolConfigValue(getEntry("Randomize Twisted Scavengers", "Randomizes the character type of Twisted Scavengers", true));

            RandomizeAlloyWorshipUnit = new BoolConfigValue(getEntry("Randomize Alloy Worship Unit", "Randomizes the character type of Alloy Worship Unit on Siren's Call", true));

            RandomizeTeleporterBosses = new BoolConfigValue(getEntry("Randomize Teleporter Bosses", "Randomizes the character types of teleporter bosses", true));
        }
    }
}
