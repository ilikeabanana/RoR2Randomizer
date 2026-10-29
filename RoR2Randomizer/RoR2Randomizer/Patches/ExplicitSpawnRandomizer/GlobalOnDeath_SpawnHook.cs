using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.RandomizerControllers.ExplicitSpawn;
using RoR2Randomizer.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RoR2Randomizer.Patches.ExplicitSpawnRandomizer
{
    [PatchClass]
    static class GlobalOnDeath_SpawnHook
    {
        static void Apply()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        static void Cleanup()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath -= GlobalEventManager_OnCharacterDeath;
        }

        static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] findCursors;

            // Malachite Urchin
            if (c.TryFindNext(out findCursors,
                              x => x.MatchLdstr("Prefabs/CharacterMasters/UrchinTurretMaster"),
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo(() => Resources.Load<GameObject>(default)))))
            {
                ILCursor last = findCursors[findCursors.Length - 1];
                last.Index++;

                last.EmitDelegate(static (GameObject urchinPrefab) =>
                {
                    if (ConfigManager.ExplicitSpawnRandomizer.RandomizeMalachiteUrchins)
                    {
                        return ExplicitSpawnRandomizerController.GetSummonReplacement(urchinPrefab);
                    }
                    else
                    {
                        return urchinPrefab;
                    }
                });
            }
            else
            {
                Log.Warning("unable to find Malachite Urchin spawn patch location");
            }

            // Soul wisp
            c.Index = 0;
            int patchCount = 0;
            while (c.TryGotoNext(MoveType.After, x => x.MatchLdsfld(typeof(GlobalEventManager.CommonAssets), nameof(GlobalEventManager.CommonAssets.wispSoulMasterPrefabMasterComponent))))
            {
                c.EmitDelegate((CharacterMaster soulWispOriginalMasterPrefab) =>
                {
                    if (ConfigManager.ExplicitSpawnRandomizer.RandomizeSoulWisps &&
                        ExplicitSpawnRandomizerController.TryGetReplacementMaster(soulWispOriginalMasterPrefab, out CharacterMaster replacementPrefab))
                    {
                        return replacementPrefab;
                    }
                    else
                    {
                        return soulWispOriginalMasterPrefab;
                    }
                });

                patchCount++;
            }

            if (patchCount > 0)
            {
#if DEBUG
                Log.Debug($"patched {patchCount} locations for Soul Wisp spawn hook");
#endif
            }
            else
            {
                Log.Warning("unable to find Soul Wisp spawn patch location");
            }

            // Healing Core
            c.Index = 0;
            patchCount = 0;
            while (c.TryGotoNext(MoveType.After, x => x.MatchLdsfld(typeof(GlobalEventManager.CommonAssets), nameof(GlobalEventManager.CommonAssets.eliteEarthHealerMaster))))
            {
                c.EmitDelegate(static (GameObject healingCoreMasterPrefab) =>
                {
                    if (ConfigManager.ExplicitSpawnRandomizer.RandomizeHealingCores)
                    {
                        return ExplicitSpawnRandomizerController.GetSummonReplacement(healingCoreMasterPrefab);
                    }
                    else
                    {
                        return healingCoreMasterPrefab;
                    }
                });

                patchCount++;
            }

            if (patchCount > 0)
            {
#if DEBUG
                Log.Debug($"patched {patchCount} locations for Healing Core spawn hook");
#endif
            }
            else
            {
                Log.Warning("unable to find Healing Core spawn patch location");
            }

            // Void Infestor (From killing void elite)
            c.Index = 0;
            if (c.TryFindNext(out findCursors,
                              x => x.MatchLdstr("RoR2/DLC1/EliteVoid/VoidInfestorMaster.prefab"),
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo(() => Addressables.LoadAssetAsync<GameObject>(default(object)))),
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<AsyncOperationHandle<GameObject>>(_ => _.WaitForCompletion()))))
            {
                ILCursor last = findCursors[findCursors.Length - 1];

                last.Index++;
                last.EmitDelegate(static (GameObject voidInfestorMasterPrefab) =>
                {
                    if (ConfigManager.ExplicitSpawnRandomizer.RandomizeVoidInfestors)
                    {
                        return ExplicitSpawnRandomizerController.GetSummonReplacement(voidInfestorMasterPrefab);
                    }
                    else
                    {
                        return voidInfestorMasterPrefab;
                    }
                });
            }
            else
            {
                Log.Warning("unable to find Void Infestor spawn patch location");
            }

            c.Index = 0;
            patchCount = 0;
            while (c.TryGotoNext(MoveType.Before, x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo(() => NetworkServer.Spawn(default)))))
            {
                c.Emit(OpCodes.Dup);
                c.EmitDelegate((GameObject instantiated) =>
                {
                    if (NetworkServer.active && instantiated && instantiated.TryGetComponent(out CharacterMaster masterPrefab))
                    {
                        MasterCatalog.MasterIndex prefabMasterIndex = masterPrefab.masterIndex;
                        if (!prefabMasterIndex.isValid)
                            return;

                        bool isMasterIndex(MasterCatalog.MasterIndex masterIndex)
                        {
                            return masterIndex.isValid && prefabMasterIndex == masterIndex;
                        }

                        if (isMasterIndex(Caches.Masters.MalachiteUrchin))
                        {
                            if (!ConfigManager.ExplicitSpawnRandomizer.RandomizeMalachiteUrchins)
                            {
                                return;
                            }
                        }
                        else if (isMasterIndex(Caches.Masters.VoidInfestor))
                        {
                            if (!ConfigManager.ExplicitSpawnRandomizer.RandomizeVoidInfestors)
                            {
                                return;
                            }
                        }

                        ExplicitSpawnRandomizerController.RegisterSpawnedReplacement(instantiated);
                    }
                });

                c.Index++;
                patchCount++;
            }

            if (patchCount > 0)
            {
#if DEBUG
                Log.Debug($"patched {patchCount} locations for NetworkServer.Spawn register replacement hook");
#endif
            }
            else
            {
                Log.Warning("unable to find patch location for NetworkServer.Spawn register replacement hook");
            }

            c.Index = 0;
            patchCount = 0;
            while (c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt<MasterSummon>(nameof(MasterSummon.Perform))))
            {
                c.Emit(OpCodes.Dup);
                c.EmitDelegate((CharacterMaster master) =>
                {
                    if (NetworkServer.active && master)
                    {
                        MasterCatalog.MasterIndex spawnedMasterIndex = master.masterIndex;
                        if (!spawnedMasterIndex.isValid)
                            return;

                        bool isMasterIndex(MasterCatalog.MasterIndex masterIndex)
                        {
                            return masterIndex.isValid && spawnedMasterIndex == masterIndex;
                        }

                        if (isMasterIndex(Caches.Masters.SoulWisp))
                        {
                            if (!ConfigManager.ExplicitSpawnRandomizer.RandomizeSoulWisps)
                            {
                                return;
                            }
                        }
                        else if (isMasterIndex(Caches.Masters.HealingCore))
                        {
                            if (!ConfigManager.ExplicitSpawnRandomizer.RandomizeHealingCores)
                            {
                                return;
                            }
                        }

                        ExplicitSpawnRandomizerController.RegisterSpawnedReplacement(master.gameObject);
                    }
                });

                patchCount++;
            }

            if (patchCount > 0)
            {
#if DEBUG
                Log.Debug($"patched {patchCount} locations for MasterSummon.Perform register replacement hook");
#endif
            }
            else
            {
                Log.Warning("unable to find patch location for MasterSummon.Perform register replacement hook");
            }
        }
    }
}
