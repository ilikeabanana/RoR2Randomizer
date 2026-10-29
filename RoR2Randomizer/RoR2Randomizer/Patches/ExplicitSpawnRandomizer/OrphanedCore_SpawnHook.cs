using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.RandomizerControllers.ExplicitSpawn;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace RoR2Randomizer.Patches.ExplicitSpawnRandomizer
{
    [PatchClass]
    static class OrphanedCore_SpawnHook
    {
        static readonly ILContext.Manipulator _replaceBestBuddySpawnManipulator =
            ExplicitSpawnRandomizerController.GetSimpleDirectorSpawnRequestHook(ConfigManager.ExplicitSpawnRandomizer.RandomizeDrones);

        static void Apply()
        {
            IL.RoR2.Items.PhysicsProjectileBehavior.TrySpawnDrone += _replaceBestBuddySpawnManipulator;
            IL.RoR2.Items.PhysicsProjectileBehavior.OnMasterSpawned += PhysicsProjectileBehavior_OnMasterSpawned;
        }

        static void Cleanup()
        {
            IL.RoR2.Items.PhysicsProjectileBehavior.TrySpawnDrone -= _replaceBestBuddySpawnManipulator;
            IL.RoR2.Items.PhysicsProjectileBehavior.OnMasterSpawned -= PhysicsProjectileBehavior_OnMasterSpawned;
        }

        static void PhysicsProjectileBehavior_OnMasterSpawned(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            // arg 0 = this, arg 1 = SpawnCard.SpawnResult spawnResult
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate((SpawnCard.SpawnResult spawnResult) =>
            {
                if (NetworkServer.active && ConfigManager.ExplicitSpawnRandomizer.RandomizeDrones && spawnResult.success && spawnResult.spawnedInstance)
                {
                    ExplicitSpawnRandomizerController.RegisterSpawnedReplacement(spawnResult.spawnedInstance);
                }
            });

            int patchLocationsFound = 0;
            while (c.TryGotoNext(x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<UnityEngine.Component>(_ => _.GetComponent<Deployable>()))))
            {
                patchLocationsFound++;

                c.Emit(OpCodes.Dup);
                c.Index++;
                c.EmitDelegate((UnityEngine.Component component, Deployable deployable) =>
                {
                    if (!deployable)
                    {
                        if (ExplicitSpawnRandomizerController.IsActive && ConfigManager.ExplicitSpawnRandomizer.RandomizeDrones)
                        {
                            deployable = component.gameObject.AddComponent<Deployable>();
                            deployable.onUndeploy = new UnityEvent();
                        }
                    }

                    return deployable;
                });
            }

            if (patchLocationsFound == 0)
            {
                Log.Warning("failed to find any patch locations in PhysicsProjectileBehavior.OnMasterSpawned");
            }
#if DEBUG
            else
            {
                Log.Debug($"found {patchLocationsFound} patch locations in PhysicsProjectileBehavior.OnMasterSpawned");
            }
#endif
        }
    }
}