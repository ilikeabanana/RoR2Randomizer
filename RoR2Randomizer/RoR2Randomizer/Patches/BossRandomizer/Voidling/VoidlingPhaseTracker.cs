using RoR2;
using RoR2Randomizer.Utility;
using UnityEngine;

namespace RoR2Randomizer.Patches.BossRandomizer.Voidling
{
    [PatchClass]
    public sealed class VoidlingPhaseTracker : BossPhaseTracker<VoidlingPhaseTracker>
    {
        static uint? _totalNumPhases = null;
        public static uint TotalNumPhases => _totalNumPhases.Value;

        class PhaseEncounterData : MonoBehaviour
        {
            public uint Phase;
            public ScriptedCombatEncounter ScriptedEncounter;

            public void Initialize()
            {
                SpawnCardTracker.VoidlingPhasesSpawnCards ??= new SpawnCard[TotalNumPhases];

                if (!SpawnCardTracker.VoidlingPhasesSpawnCards[Phase - 1])
                    SpawnCardTracker.VoidlingPhasesSpawnCards[Phase - 1] = ScriptedEncounter.spawns[0].spawnCard;
            }
        }

        static void ApplyPatches()
        {
            new VoidlingPhaseTracker().applyPatches();
        }

        static void CleanupPatches()
        {
            Instance?.cleanupPatches();
        }

        protected override void applyPatches()
        {
            base.applyPatches();

            On.RoR2.VoidRaidGauntletController.Start += VoidRaidGauntletController_Start;
            On.RoR2.ScriptedCombatEncounter.BeginEncounter += ScriptedCombatEncounter_BeginEncounter;
            On.EntityStates.VoidRaidCrab.DeathState.OnEnter += DeathState_OnEnter;
        }

        protected override void cleanupPatches()
        {
            base.cleanupPatches();

            On.RoR2.VoidRaidGauntletController.Start -= VoidRaidGauntletController_Start;
            On.RoR2.ScriptedCombatEncounter.BeginEncounter -= ScriptedCombatEncounter_BeginEncounter;
            On.EntityStates.VoidRaidCrab.DeathState.OnEnter -= DeathState_OnEnter;
        }

        static void VoidRaidGauntletController_Start(On.RoR2.VoidRaidGauntletController.orig_Start orig, VoidRaidGauntletController self)
        {
            orig(self);

            if (!_totalNumPhases.HasValue)
                _totalNumPhases = (uint)self.phaseEncounters.Length;

            for (uint i = 0; i < TotalNumPhases; i++)
            {
                ScriptedCombatEncounter scriptedEncounter = self.phaseEncounters[i];
                PhaseEncounterData encounterData = scriptedEncounter.gameObject.AddComponent<PhaseEncounterData>();
                encounterData.ScriptedEncounter = scriptedEncounter;
                encounterData.Phase = i + 1;
                encounterData.Initialize();
            }
        }

        void ScriptedCombatEncounter_BeginEncounter(On.RoR2.ScriptedCombatEncounter.orig_BeginEncounter orig, ScriptedCombatEncounter self)
        {
            if (self.TryGetComponent<PhaseEncounterData>(out PhaseEncounterData encounterData))
            {
                IsInFight.Value = true;
                Phase.Value = encounterData.Phase;
            }

            orig(self);
        }

        void DeathState_OnEnter(On.EntityStates.VoidRaidCrab.DeathState.orig_OnEnter orig, EntityStates.VoidRaidCrab.DeathState self)
        {
            orig(self);
            IsInFight.Value = false;
        }
    }
}