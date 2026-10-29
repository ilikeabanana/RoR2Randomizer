using RoR2;
using RoR2Randomizer.Networking.BossRandomizer;
using RoR2Randomizer.Utility;

namespace RoR2Randomizer.RandomizerControllers.Boss.BossReplacementInfo
{
    public sealed class SolusWingReplacement : BaseBossReplacement
    {
        protected override BossReplacementType replacementType => BossReplacementType.SolusWing;

        protected override CharacterMaster originalMasterPrefab => Caches.MasterPrefabs["SolusWingMaster"];
    }
}