using MonoMod.Cil;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.RandomizerControllers.ExplicitSpawn;
using System;
using System.Collections.Generic;
using System.Text;

namespace RoR2Randomizer.Patches.ExplicitSpawnRandomizer
{
    [PatchClass]
    static class MinePod_SpawnHook
    {
        static readonly ILContext.Manipulator _replaceMinePodSpawnManipulator = ExplicitSpawnRandomizerController.GetSimpleDirectorSpawnRequestHook(ConfigManager.ExplicitSpawnRandomizer.RandomizeDrones);

        static void Apply()
        {
            IL.EntityStates.MinePod.MinePlant.SpawnMine += _replaceMinePodSpawnManipulator;
        }

        static void Cleanup()
        {
            IL.EntityStates.MinePod.MinePlant.SpawnMine -= _replaceMinePodSpawnManipulator;
        }
    }
}
