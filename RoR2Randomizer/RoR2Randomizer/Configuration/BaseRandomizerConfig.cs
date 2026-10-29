using BepInEx.Configuration;
using RoR2Randomizer.Configuration.ConfigValue;

namespace RoR2Randomizer.Configuration
{
    public abstract class BaseRandomizerConfig : ConfigCategory
    {
        const string RANDOMIZER_NAME_POSTFIX = " Randomizer";

        public override ModCompatibilityFlags CompatibilityFlags => base.CompatibilityFlags | ModCompatibilityFlags.RiskOfOptions;

        public readonly BoolConfigValue Enabled;

        protected BaseRandomizerConfig(string randomizerType, ConfigFile file, bool EnabledByDefault = true) : base(randomizerType + RANDOMIZER_NAME_POSTFIX, file)
        {
            Enabled = new BoolConfigValue(getEntry<bool>($"Enabled", new ConfigDescription($"If the {randomizerType + RANDOMIZER_NAME_POSTFIX} should be enabled"), EnabledByDefault));
        }
    }
}
