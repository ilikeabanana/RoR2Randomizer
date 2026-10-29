using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.PrefabMarkers;
using RoR2Randomizer.RandomizerControllers.Boss;
using RoR2Randomizer.RandomizerControllers.ExplicitSpawn;
using System;
using UnityEngine;

namespace RoR2Randomizer.Patches.ExplicitSpawnRandomizer.Director
{
    [PatchClass]
    static class CombatDirector_SpawnHook
    {
        static void Apply()
        {
            IL.RoR2.CombatDirector.Spawn_SpawnArgs += CombatDirector_Spawn;
        }

        static void Cleanup()
        {
            IL.RoR2.CombatDirector.Spawn_SpawnArgs -= CombatDirector_Spawn;
        }

        static void CombatDirector_Spawn(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(x => x.MatchCallOrCallvirt<DirectorCore>(nameof(DirectorCore.TrySpawnObject))))
            {
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(static (DirectorSpawnRequest spawnRequest, CombatDirector combatDirector) =>
                {
                    if (combatDirector.GetComponent<HoldoutZoneController>() && string.Equals(combatDirector.customName, "Boss", StringComparison.OrdinalIgnoreCase))
                    {
#if DEBUG
                        Log.Debug($"Attempting holdout boss spawn replacement from {nameof(CombatDirector)} {combatDirector} ({combatDirector.customName})");
#endif

                        BossRandomizerController.HoldoutBoss.TryReplaceDirectorSpawnRequest(spawnRequest);
                        return;
                    }

                    bool shouldRandomizeAsExplicitSpawn()
                    {
                        if (ConfigManager.ExplicitSpawnRandomizer.RandomizeDirectorSpawns)
                            return true;

                        if (ConfigManager.ExplicitSpawnRandomizer.RandomizeVoidSeedMonsters)
                        {
                            if (combatDirector.TryGetComponent(out VoidSeedMarker voidSeedMarker) &&
                                voidSeedMarker.Type == VoidSeedMarker.MarkerType.Monsters_Interactibles)
                            {
                                return true;
                            }
                        }

                        return false;
                    }

                    if (shouldRandomizeAsExplicitSpawn())
                    {
#if DEBUG
                        Log.Debug($"Attempting spawn replacement from {nameof(CombatDirector)} {combatDirector} ({combatDirector.customName})");
#endif

                        ExplicitSpawnRandomizerController.TryReplaceDirectorSpawnRequest(spawnRequest);
                        return;
                    }

#if DEBUG
                    Log.Debug($"Not replacing spawn from {nameof(CombatDirector)} {combatDirector} ({combatDirector.customName})");
#endif
                });
            }
            else
            {
                Log.Warning("unable to find patch location");
            }
        }
    }
}
