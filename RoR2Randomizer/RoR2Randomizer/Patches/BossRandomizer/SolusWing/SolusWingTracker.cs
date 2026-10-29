using RoR2Randomizer.Patches.BossRandomizer.Aurelionite;
using RoR2Randomizer.RandomizerControllers.Boss;
using RoR2Randomizer.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace RoR2Randomizer.Patches.BossRandomizer.SolusWing
{
    [PatchClass]
    public class SolusWingTracker : BossTracker<SolusWingTracker>
    {
        static void ApplyPatches()
        {
            new SolusWingTracker().applyPatches();
        }

        static void CleanupPatches()
        {
            Instance?.cleanupPatches();
        }

        protected override void applyPatches()
        {
            base.applyPatches();
            On.EntityStates.SolusWing2.Mission2Fight.OnEnter += Mission2Fight_OnEnter;
            On.EntityStates.SolusWing2.Mission5Death.OnEnter += Mission5Death_OnEnter;
        }

        private void Mission5Death_OnEnter(On.EntityStates.SolusWing2.Mission5Death.orig_OnEnter orig, EntityStates.SolusWing2.Mission5Death self)
        {

            IsInFight.Value = false;
#if DEBUG
            Log.Info($"Exiting solus wing fight!");
#endif
            orig(self);
        }

        private void Mission2Fight_OnEnter(On.EntityStates.SolusWing2.Mission2Fight.orig_OnEnter orig, EntityStates.SolusWing2.Mission2Fight self)
        {
#if DEBUG
            string a = self.solutionalHauntReferences == null ? "null" : "not null";
            Log.Info($"References are {a}");
            Log.Info($"spawns are {self.solutionalHauntReferences.solusWingScriptedCombatEncounter.spawns.Length} long");
            foreach (var spawns in self.solutionalHauntReferences.solusWingScriptedCombatEncounter.spawns)
            {
                Log.Info($"Spawn card: {spawns.spawnCard.name}");
            }
#endif
            SpawnCardTracker.SolusWingSpawnCard = self.solutionalHauntReferences.solusWingScriptedCombatEncounter.spawns[0].spawnCard;
            IsInFight.Value = true;
#if DEBUG
            Log.Info($"Entering solus wing fight!");
#endif
            orig(self);
        }

        protected override void cleanupPatches()
        {
            base.cleanupPatches();
            On.EntityStates.SolusWing2.Mission2Fight.OnEnter -= Mission2Fight_OnEnter;
            On.EntityStates.SolusWing2.Mission5Death.OnEnter -= Mission5Death_OnEnter;
        }
    }
}
