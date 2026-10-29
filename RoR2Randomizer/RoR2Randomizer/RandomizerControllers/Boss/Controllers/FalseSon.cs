using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Networking.BossRandomizer;
using RoR2Randomizer.Patches.BossRandomizer;
using RoR2Randomizer.Patches.BossRandomizer.FalseSon;
using RoR2Randomizer.RandomizerControllers.Boss.BossReplacementInfo;
using RoR2Randomizer.Utility;
using UnityEngine;

namespace RoR2Randomizer.RandomizerControllers.Boss
{
    public partial class BossRandomizerController
    {
        public static class FalseSon
        {
            static bool IsEnabled => _instance && _instance.IsRandomizerEnabled && (ConfigManager.BossRandomizer.RandomizeFalseSon || CharacterReplacements.IsAnyForcedCharacterModeEnabled);
            // TODO: add a "RandomizeFalseSon" bool option to ConfigManager.BossRandomizer, mirroring RandomizeVoidling/RandomizeAurelionite

            public static void Initialize()
            {
                if (FalseSonPhaseTracker.Instance != null)
                {
                    FalseSonPhaseTracker.Instance.IsInFight.OnChanged += IsInFight_OnChanged;
                }

                SyncBossReplacementCharacter.OnReceive += SyncBossReplacementCharacter_OnReceive;
            }

            public static void Uninitialize()
            {
                if (FalseSonPhaseTracker.Instance != null)
                {
                    FalseSonPhaseTracker.Instance.IsInFight.OnChanged -= IsInFight_OnChanged;
                }

                SyncBossReplacementCharacter.OnReceive -= SyncBossReplacementCharacter_OnReceive;
            }

            static void IsInFight_OnChanged(bool isInFight)
            {
                if (IsEnabled)
                {
#if DEBUG
                    Log.Info($"Fighting False son!!!");
#endif
                    if (isInFight)
                    {
                        Log.Info($"We are in da fight!!!");
                        GenericScriptedSpawnHook.OverrideSpawnPrefabFunc = (ref SpawnCard card, out GenericScriptedSpawnHook.ResetCardDelegate resetCardFunc) =>
                        {
                            Log.Info($"HAHA Override this ass {card.name}");
                            if (SpawnCardTracker.IsAnyFalseSonPhase(card)) // TODO: add this helper to SpawnCardTracker, mirroring IsAnyVoidlingPhase
                            {
                                GameObject originalPrefab = card.prefab;

                                resetCardFunc = (ref SpawnCard c) => c.prefab = originalPrefab;

                                CharacterReplacements.TryReplaceMasterPrefab(ref card.prefab);
                            }
                            else
                            {
                                resetCardFunc = null;
                            }
                        };

                        GenericScriptedSpawnHook.OnSpawned += handleSpawnedFalseSonCharacterServer;
                    }
                    else
                    {
                        GenericScriptedSpawnHook.OverrideSpawnPrefabFunc = null;
                        GenericScriptedSpawnHook.OnSpawned -= handleSpawnedFalseSonCharacterServer;
                    }
                }
            }

            static void SyncBossReplacementCharacter_OnReceive(GameObject masterObject, BossReplacementType replacementType, MasterCatalog.MasterIndex? originalMasterIndex)
            {
                switch (replacementType)
                {
                    // TODO: list one case per phase, matching however many BossReplacementType.FalseSonPhaseN values you add
                    case BossReplacementType.FalseSonBoss:
                    case BossReplacementType.FalseSonLunar:
                    case BossReplacementType.FalseSonBroken:
#if DEBUG
                        Log.Debug($"Running {nameof(handleSpawnedFalseSonCharacterClient)}");
#endif
                        handleSpawnedFalseSonCharacterClient(masterObject, replacementType - BossReplacementType.FalseSonBoss + 1);
                        break;
                }
            }

            static void handleSpawnedFalseSonCharacterClient(GameObject masterObject, uint phase)
            {
                FalseSonReplacement falseSonReplacement = masterObject.AddComponent<FalseSonReplacement>();
                falseSonReplacement.Phase = phase;
#if DEBUG
                Log.Debug($"Adding {nameof(FalseSonReplacement)} component to {masterObject}");
#endif
                falseSonReplacement.Initialize();
            }

            static void handleSpawnedFalseSonCharacterServer(SpawnCard.SpawnResult spawnResult)
            {
                if (IsEnabled && FalseSonPhaseTracker.Instance != null && FalseSonPhaseTracker.Instance.IsInFight)
                {
                    FalseSonReplacement falseSonReplacement = spawnResult.spawnedInstance.AddComponent<FalseSonReplacement>();
                    falseSonReplacement.Phase = FalseSonPhaseTracker.Instance.Phase;
#if DEBUG
                    Log.Debug($"Adding {nameof(FalseSonReplacement)} component to {spawnResult.spawnedInstance}");
#endif
                    falseSonReplacement.Initialize();
                }
            }
        }
    }
}