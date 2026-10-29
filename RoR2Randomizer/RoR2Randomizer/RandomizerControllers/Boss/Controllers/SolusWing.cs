using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Networking.BossRandomizer;
using RoR2Randomizer.Patches.BossRandomizer;
using RoR2Randomizer.Patches.BossRandomizer.SolusWing;
using RoR2Randomizer.RandomizerControllers.Boss.BossReplacementInfo;
using RoR2Randomizer.Utility;
using UnityEngine;

namespace RoR2Randomizer.RandomizerControllers.Boss
{
    public partial class BossRandomizerController
    {
        public static class SolusWing
        {
            static bool IsEnabled => _instance && _instance.IsRandomizerEnabled && (ConfigManager.BossRandomizer.RandomizeFalseSon || CharacterReplacements.IsAnyForcedCharacterModeEnabled);


            const string MASTER_NAME = "SolusWingMaster";

            static readonly RunSpecific<GameObject> _solusWingMasterReplacementPrefab = new RunSpecific<GameObject>((out GameObject overridePrefab) =>
            {
                if (IsEnabled)
                {
                    overridePrefab = CharacterReplacements.GetReplacementMasterPrefab(MASTER_NAME);
                    if (overridePrefab)
                    {
                        return true;
                    }
                }

                overridePrefab = null;
                return false;
            });

            public static bool TryGetSolusWingMasterReplacementPrefab(out GameObject replacementPrefab)
            {
                if (IsEnabled)
                {
#if DEBUG
                    if (CharacterReplacements.DebugMode != DebugMode.None)
                    {
                        replacementPrefab = CharacterReplacements.GetReplacementMasterPrefab(MASTER_NAME);
                    }
                    else
#endif
                    {
                        replacementPrefab = _solusWingMasterReplacementPrefab;
                    }

                    return (bool)replacementPrefab;
                }

                replacementPrefab = null;
                return false;
            }

            public static void Initialize()
            {
                if (SolusWingTracker.Instance != null)
                {
                    SolusWingTracker.Instance.IsInFight.OnChanged += IsInFight_OnChanged;
                }

                SyncBossReplacementCharacter.OnReceive += SyncBossReplacementCharacter_OnReceive;
            }

            public static void Uninitialize()
            {
                if (SolusWingTracker.Instance != null)
                {
                    SolusWingTracker.Instance.IsInFight.OnChanged -= IsInFight_OnChanged;
                }

                SyncBossReplacementCharacter.OnReceive -= SyncBossReplacementCharacter_OnReceive;

                _solusWingMasterReplacementPrefab.Dispose();
            }

            static void IsInFight_OnChanged(bool isInFight)
            {
                if (IsEnabled)
                {
                    if (isInFight)
                    {
                        GenericScriptedSpawnHook.OverrideSpawnPrefabFunc = (ref SpawnCard card, out GenericScriptedSpawnHook.ResetCardDelegate resetCardFunc) =>
                        {
                            if (card == SpawnCardTracker.SolusWingSpawnCard && TryGetSolusWingMasterReplacementPrefab(out GameObject overridePrefab))
                            {
                                GameObject originalPrefab = card.prefab;
                                resetCardFunc = (ref SpawnCard c) => c.prefab = originalPrefab;

                                card.prefab = overridePrefab;
                            }
                            else
                            {
                                resetCardFunc = null;
                            }
                        };

                        GenericScriptedSpawnHook.OnSpawned += handleSpawnedSolusWingCharacterServer;
                    }
                    else
                    {
                        GenericScriptedSpawnHook.OverrideSpawnPrefabFunc = null;
                        GenericScriptedSpawnHook.OnSpawned -= handleSpawnedSolusWingCharacterServer;
                    }
                }
            }

            static void SyncBossReplacementCharacter_OnReceive(GameObject masterObject, BossReplacementType replacementType, MasterCatalog.MasterIndex? originalMasterIndex)
            {
                switch (replacementType)
                {
                    case BossReplacementType.SolusWing:
                        // TODO: add "SolusWing" to the BossReplacementType enum
#if DEBUG
                        Log.Debug($"Running {nameof(handleSpawnedSolusWingCharacterClient)}");
#endif
                        handleSpawnedSolusWingCharacterClient(masterObject);
                        break;
                }
            }

            static void handleSpawnedSolusWingCharacterClient(GameObject masterObject)
            {
                SolusWingReplacement solusWingReplacement = masterObject.AddComponent<SolusWingReplacement>();
#if DEBUG
                Log.Debug($"Adding {nameof(SolusWingReplacement)} component to {masterObject}");
#endif
                solusWingReplacement.Initialize();
            }

            static void handleSpawnedSolusWingCharacterServer(SpawnCard.SpawnResult spawnResult)
            {
                if (IsEnabled &&
                    SolusWingTracker.Instance != null && SolusWingTracker.Instance.IsInFight &&
                    spawnResult.spawnRequest.spawnCard == SpawnCardTracker.SolusWingSpawnCard)
                {
                    SolusWingReplacement solusWingReplacement = spawnResult.spawnedInstance.AddComponent<SolusWingReplacement>();
#if DEBUG
                    Log.Debug($"Adding {nameof(SolusWingReplacement)} component to {spawnResult.spawnedInstance}");
#endif
                    solusWingReplacement.Initialize();
                }
            }
        }
    }
}