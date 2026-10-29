using RoR2;
using RoR2Randomizer.Utility;

namespace RoR2Randomizer.Patches.BossRandomizer.FalseSon
{
    [PatchClass]
    public sealed class FalseSonPhaseTracker : BossPhaseTracker<FalseSonPhaseTracker>
    {
        public const uint TotalNumPhases = 3;

        static void ApplyPatches()
        {
            new FalseSonPhaseTracker().applyPatches();
        }

        static void CleanupPatches()
        {
            Instance?.cleanupPatches();
        }

        protected override void applyPatches()
        {
            base.applyPatches();

            On.RoR2.ScriptedCombatEncounter.BeginEncounter += ScriptedCombatEncounter_BeginEncounter;

            On.EntityStates.MeridianEvent.MeridianEventStart.OnEnter += MeridianEventStart_OnEnter;
            On.EntityStates.MeridianEvent.Phase1.OnEnter += Phase1_OnEnter;
            On.EntityStates.MeridianEvent.Phase2.OnEnter += Phase2_OnEnter;
            On.EntityStates.MeridianEvent.Phase3.OnEnter += Phase3_OnEnter;
            On.EntityStates.MeridianEvent.MeridianEventCleared.OnEnter += MeridianEventCleared_OnEnter;
        }

        protected override void cleanupPatches()
        {
            base.cleanupPatches();

            On.RoR2.ScriptedCombatEncounter.BeginEncounter -= ScriptedCombatEncounter_BeginEncounter;

            On.EntityStates.MeridianEvent.MeridianEventStart.OnEnter -= MeridianEventStart_OnEnter;
            On.EntityStates.MeridianEvent.Phase1.OnEnter -= Phase1_OnEnter;
            On.EntityStates.MeridianEvent.Phase2.OnEnter -= Phase2_OnEnter;
            On.EntityStates.MeridianEvent.Phase3.OnEnter -= Phase3_OnEnter;
            On.EntityStates.MeridianEvent.MeridianEventCleared.OnEnter -= MeridianEventCleared_OnEnter;
        }

        void ScriptedCombatEncounter_BeginEncounter(On.RoR2.ScriptedCombatEncounter.orig_BeginEncounter orig, ScriptedCombatEncounter self)
        {
            MeridianEventTriggerInteraction trigger = MeridianEventTriggerInteraction.instance;
            if (trigger && self == trigger.falseSonBossEncounter && self.spawns.Length > 0)
            {
                SpawnCardTracker.FalseSonPhasesSpawnCards[Phase - 1] ??= self.spawns[0].spawnCard;
                // singular, not an array — False Son only ever spawns from one card
            }

            orig(self);
        }

        void MeridianEventStart_OnEnter(On.EntityStates.MeridianEvent.MeridianEventStart.orig_OnEnter orig, EntityStates.MeridianEvent.MeridianEventStart self)
        {
            IsInFight.Value = true;
            orig(self);
        }

        void Phase1_OnEnter(On.EntityStates.MeridianEvent.Phase1.orig_OnEnter orig, EntityStates.MeridianEvent.Phase1 self)
        {
            Phase.Value = 1;
            orig(self);
        }

        void Phase2_OnEnter(On.EntityStates.MeridianEvent.Phase2.orig_OnEnter orig, EntityStates.MeridianEvent.Phase2 self)
        {
            Phase.Value = 2;
            orig(self);
        }

        void Phase3_OnEnter(On.EntityStates.MeridianEvent.Phase3.orig_OnEnter orig, EntityStates.MeridianEvent.Phase3 self)
        {
            Phase.Value = 3;
            orig(self);
        }

        void MeridianEventCleared_OnEnter(On.EntityStates.MeridianEvent.MeridianEventCleared.orig_OnEnter orig, EntityStates.MeridianEvent.MeridianEventCleared self)
        {
            IsInFight.Value = false;
            orig(self);
        }
    }
}