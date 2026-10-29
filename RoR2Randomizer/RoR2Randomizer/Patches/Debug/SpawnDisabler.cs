#if DEBUG
using RoR2;
using UnityEngine;

namespace RoR2Randomizer.Patches.Debug
{
    [PatchClass]
    public static class SpawnDisabler
    {
        static bool _spawnsDisabled;

        static void ApplyPatches()
        {
            On.RoR2.CombatDirector.Spawn_SpawnCard_EliteDef_Transform_MonsterSpawnDistance_bool_float_PlacementMode_bool += CombatDirector_Spawn;
        }

        static void CleanupPatches()
        {
            On.RoR2.CombatDirector.Spawn_SpawnCard_EliteDef_Transform_MonsterSpawnDistance_bool_float_PlacementMode_bool -= CombatDirector_Spawn;
        }

        public static void ToggleSpawnsDisabled()
        {
            _spawnsDisabled = !_spawnsDisabled;
            Chat.AddMessage($"Spawns {(_spawnsDisabled ? "Disabled" : "Enabled")}");
        }

        static bool CombatDirector_Spawn(On.RoR2.CombatDirector.orig_Spawn_SpawnCard_EliteDef_Transform_MonsterSpawnDistance_bool_float_PlacementMode_bool orig, CombatDirector self, SpawnCard spawnCard, EliteDef eliteDef, Transform spawnTarget, DirectorCore.MonsterSpawnDistance spawnDistance, bool preventOverhead, float valueMultiplier, DirectorPlacementRule.PlacementMode placementMode, bool singleScaledBoss)
        {
            if (_spawnsDisabled)
                return false;

            return orig(self, spawnCard, eliteDef, spawnTarget, spawnDistance, preventOverhead, valueMultiplier, placementMode, singleScaledBoss);
        }
    }
}
#endif
