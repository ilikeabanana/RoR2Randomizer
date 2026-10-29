using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.RandomizerControllers.ExplicitSpawn;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.Patches.ExplicitSpawnRandomizer
{
    [PatchClass]
    static class EquipmentSlot_FireDroneBackup_SpawnHook
    {
        static void Apply()
        {
            IL.RoR2.EquipmentSlot.FireDroneBackup += EquipmentSlot_FireDroneBackup;
        }

        static void Cleanup()
        {
            IL.RoR2.EquipmentSlot.FireDroneBackup -= EquipmentSlot_FireDroneBackup;
        }

        static void EquipmentSlot_FireDroneBackup(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryFindNext(out ILCursor[] foundCursors,
                              x => x.MatchLdstr("Prefabs/CharacterMasters/DroneBackupMaster"),
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo(() => Resources.Load<GameObject>(default)))))
            {
                ILCursor last = foundCursors[foundCursors.Length - 1];
                last.Index++;

                last.EmitDelegate(static (GameObject strikeDronePrefab) =>
                {
                    if (ConfigManager.ExplicitSpawnRandomizer.RandomizeDrones)
                    {
                        return ExplicitSpawnRandomizerController.GetSummonReplacement(strikeDronePrefab);
                    }
                    else
                    {
                        return strikeDronePrefab;
                    }
                });

                if (last.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt<EquipmentSlot>(nameof(EquipmentSlot.SummonMaster))))
                {
                    last.Emit(OpCodes.Dup);
                    last.EmitDelegate((CharacterMaster summoned) =>
                    {
                        if (summoned && NetworkServer.active && ConfigManager.ExplicitSpawnRandomizer.RandomizeDrones)
                        {
                            ExplicitSpawnRandomizerController.RegisterSpawnedReplacement(summoned.gameObject);
                        }
                    });
                }
                else
                {
                    Log.Warning("Failed to patch the summoning of the drone");
                }
            }
            else
            {
                Log.Warning("Failed to patch the loading of the drone prefab");
            }
        }
    }
}
