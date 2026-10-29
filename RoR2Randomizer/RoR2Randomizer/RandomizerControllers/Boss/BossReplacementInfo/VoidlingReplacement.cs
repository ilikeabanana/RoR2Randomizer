using EntityStates;
using RoR2;
using RoR2Randomizer.CustomContent;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.Networking.BossRandomizer;
using RoR2Randomizer.Patches.BossRandomizer.Voidling;
using RoR2Randomizer.Utility;
using System;
using UnityEngine;

namespace RoR2Randomizer.RandomizerControllers.Boss.BossReplacementInfo
{
    public class VoidlingReplacement : BaseBossReplacement
    {
        public uint Phase;

        protected override BossReplacementType replacementType
        {
            get
            {
                if (Phase >= 1 && Phase <= VoidlingPhaseTracker.TotalNumPhases)
                {
                    return BossReplacementType.VoidlingPhase1 + Phase - 1;
                }
                else
                {
                    return BossReplacementType.Invalid;
                }
            }
        }

        protected override CharacterMaster originalMasterPrefab => Caches.MasterPrefabs[$"MiniVoidRaidCrabMasterPhase{Phase}"];

        protected override void bodyResolved()
        {
            base.bodyResolved();

            

            _body.bodyFlags |= CharacterBody.BodyFlags.IgnoreFallDamage;
            
            CharacterDeathBehavior deathBehavior = _body.gameObject.GetOrAddComponent<CharacterDeathBehavior>();
            if (!deathBehavior.deathStateMachine)
            {
                EntityStateMachine bodyState = EntityStateMachine.FindByCustomName(_body.gameObject, "Body");
                if (bodyState)
                {
                    deathBehavior.deathStateMachine = bodyState;
                }
                else
                {
                    Log.Warning($"Body entityState for {_body.GetDisplayName()} could not be found!");
                }
            }

            bool isFinalDeathState = Phase == VoidlingPhaseTracker.TotalNumPhases;
            SerializableEntityStateType voidlingDeathState = isFinalDeathState ? BossRandomizerController.Voidling.FinalDeathState
                                                                               : BossRandomizerController.Voidling.EscapeDeathState;

            Type deathStateType = deathBehavior.deathState.stateType;
            if (deathStateType == null || deathStateType == BossRandomizerController.Voidling.EscapeDeathState.stateType) // Don't combine anything with the escape death state, since that always opens a portal to the next gauntlet, where there is nothing if it happens on phase 3
            {
                deathBehavior.deathState = voidlingDeathState;
            }
            else if (deathStateType != voidlingDeathState.stateType) // If the death state is already the one we want, nothing needs to be done
            {
                MultiEntityState.StateMachineSubStatesData subStatesData = deathBehavior.gameObject.AddComponent<MultiEntityState.StateMachineSubStatesData>();
                subStatesData.StateTypes = new SerializableEntityStateType[] { deathBehavior.deathState, voidlingDeathState };
                subStatesData.AlwaysExecuteEnter = true;

                subStatesData.MinActiveDuration = isFinalDeathState ? BossRandomizerController.Voidling.FinalDeathStateAnimationDuration : 0.75f;

                deathBehavior.deathState = MultiEntityState.SerializableStateType;
            }

            deathBehavior.idleStateMachine ??= Array.Empty<EntityStateMachine>();

            if (originalBodyPrefab.TryGetComponent<FogDamageController>(out FogDamageController originalFogDamage) && originalBodyPrefab.TryGetComponent<BaseZoneBehavior>(out BaseZoneBehavior originalBaseZone))
            {
                FogDamageController fogDamage = _body.gameObject.GetOrAddComponent<FogDamageController>();

                fogDamage.teamFilter = _body.gameObject.AddComponent<TeamFilter>();
                fogDamage.teamFilter.defaultTeam = originalFogDamage.teamFilter.defaultTeam;
                fogDamage.teamFilter.Awake(); // Re-run awake

                fogDamage.invertTeamFilter = originalFogDamage.invertTeamFilter;

                fogDamage.tickPeriodSeconds = originalFogDamage.tickPeriodSeconds;
                fogDamage.healthFractionPerSecond = originalFogDamage.healthFractionPerSecond;
                fogDamage.healthFractionRampCoefficientPerSecond = originalFogDamage.healthFractionRampCoefficientPerSecond;

                fogDamage.dangerBuffDef = originalFogDamage.dangerBuffDef;
                fogDamage.dangerBuffDuration = originalFogDamage.dangerBuffDuration;

                Type zoneType = originalBaseZone.GetType();
                BaseZoneBehavior baseZone = (BaseZoneBehavior)_body.gameObject.GetOrAddComponent(zoneType);
                if (baseZone is SphereZone sphereZone)
                {
                    SphereZone originalSphereZone = originalBaseZone as SphereZone;

                    sphereZone.radius = originalSphereZone.radius;

                    sphereZone.rangeIndicator = GameObject.Instantiate<Transform>(originalSphereZone.rangeIndicator, _body.transform);

                    sphereZone.indicatorSmoothTime = originalSphereZone.indicatorSmoothTime;

                    sphereZone.isInverted = originalSphereZone.isInverted;
                }
                else
                {
                    Log.Warning($"Zone type '{zoneType.FullName}' not accounted for!");
                }

                fogDamage.initialSafeZones = new BaseZoneBehavior[] { baseZone };
            }
        }
    }
}
