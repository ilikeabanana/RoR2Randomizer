using MonoMod.Cil;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.RandomizerControllers.ExplicitSpawn;
using System;
using System.Collections.Generic;
using System.Text;

namespace RoR2Randomizer.Patches.ExplicitSpawnRandomizer
{
    [PatchClass]
    static class DroneBoy_SpawnHook
    {
        static readonly ILContext.Manipulator _replaceDroneboySpawnManipulator = ExplicitSpawnRandomizerController.GetSimpleDirectorSpawnRequestHook(ConfigManager.ExplicitSpawnRandomizer.RandomizeDrones);

        static void Apply()
        {
            IL.RoR2.Items.DronesDropDynamiteBehaviour.TrySpawnDrone += _replaceDroneboySpawnManipulator;
        }

        static void Cleanup()
        {
            IL.RoR2.Items.DronesDropDynamiteBehaviour.TrySpawnDrone -= _replaceDroneboySpawnManipulator;
        }
    }
}
