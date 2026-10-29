using EntityStates;
using RoR2;
using RoR2.Skills;
using RoR2Randomizer.Utility;
using UnityEngine;

namespace RoR2Randomizer.RandomizerControllers.ExplicitSpawn
{
    static class HereticReplacementPrefabModificationManager
    {
        static SkillLocator _hereticPrefabSkillLocator;

        static PrefabModificationTracker _hereticModificationTracker;

        [SystemInitializer(typeof(BodyCatalog))]
        static void Init()
        {
            GameObject hereticBodyPrefab = BodyCatalog.FindBodyPrefab(Constants.BodyNames.HERETIC_NAME);
            if (hereticBodyPrefab)
            {
                _hereticPrefabSkillLocator = hereticBodyPrefab.GetComponent<SkillLocator>();
            }

            FullExplicitSpawnInitListener.OnFullInit += static () =>
            {
                if (!Caches.Masters.Heretic.isValid)
                    return;

                if (!ExplicitSpawnRandomizerController.IsHereticRandomized)
                    return;

                MasterCatalog.MasterIndex hereticReplacementIndex = CharacterReplacements.GetReplacementForMasterIndex(Caches.Masters.Heretic);
                if (!hereticReplacementIndex.isValid)
                    return;

                GameObject hereticReplacementPrefab = MasterCatalog.GetMasterPrefab(hereticReplacementIndex);
                if (!hereticReplacementPrefab || !hereticReplacementPrefab.TryGetComponent<CharacterMaster>(out CharacterMaster masterPrefab))
                    return;

                GameObject bodyPrefab = masterPrefab.bodyPrefab;
                if (!bodyPrefab || !bodyPrefab.GetComponent<CharacterBody>())
                    return;

                _hereticModificationTracker = bodyPrefab.AddComponent<PrefabModificationTracker>();
                _hereticModificationTracker.PerformModification(initHereticReplacementPrefab_Equipment);
                _hereticModificationTracker.PerformModification(initHereticReplacementPrefab_Interaction);
                _hereticModificationTracker.PerformModification(initHereticReplacementPrefab_Skills);

#if DEBUG
                Log.Debug($"Finish Heretic body replacement setup for {bodyPrefab.name}");
#endif
            };

            Run.onRunDestroyGlobal += static _ =>
            {
                if (_hereticModificationTracker)
                {
                    _hereticModificationTracker.Undo();
                    _hereticModificationTracker = null;
                }
            };
        }

        static PrefabUndoModificationDelegate initHereticReplacementPrefab_Equipment(GameObject replacementPrefab)
        {
            if (!replacementPrefab.GetComponent<EquipmentSlot>())
            {
#if DEBUG
                Log.Debug($"Add EquipmentSlot to {replacementPrefab.name}");
#endif

                EquipmentSlot equipmentSlot = replacementPrefab.AddComponent<EquipmentSlot>();
                return _ =>
                {
                    GameObject.Destroy(equipmentSlot);
                };
            }

            return null;
        }

        static PrefabUndoModificationDelegate initHereticReplacementPrefab_Interaction(GameObject replacementPrefab)
        {
            const float REFERENCE_INTERACT_DISTANCE = 3f; // Base interaction distance for survivors

            PrefabUndoModificationDelegate undoCallback = null;

            if (!replacementPrefab.TryGetComponent(out Interactor interactor))
            {
#if DEBUG
                Log.Debug($"Adding {nameof(Interactor)} component to {replacementPrefab}");
#endif

                interactor = replacementPrefab.AddComponent<Interactor>();
                interactor.maxInteractionDistance = REFERENCE_INTERACT_DISTANCE; // Setting it explicitly here to not go below it later

                MiscUtils.AppendDelegate(ref undoCallback, _ =>
                {
                    GameObject.Destroy(interactor);
                });
            }

            if (!interactor.GetComponent<InteractionDriver>())
            {
#if DEBUG
                Log.Debug($"Adding {nameof(InteractionDriver)} component to {replacementPrefab}");
#endif

                InteractionDriver interactionDriver = interactor.gameObject.AddComponent<InteractionDriver>();
                interactionDriver.highlightInteractor = true;

                MiscUtils.AppendDelegate(ref undoCallback, _ =>
                {
                    GameObject.Destroy(interactionDriver);
                });
            }
            const float REFERENCE_RADIUS = 1.82f;
            float targetInteractionRadius;
            try
            {
                targetInteractionRadius = Caches.CharacterBodyRadius[replacementPrefab];
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                targetInteractionRadius = REFERENCE_RADIUS; // sane fallback, same units as REFERENCE_RADIUS above

#if DEBUG
                Log.Debug($"Could not determine body radius for {replacementPrefab.name}, using fallback {targetInteractionRadius}");
#endif
            }

            
            const float m = (REFERENCE_INTERACT_DISTANCE / REFERENCE_RADIUS) - 1f;

            const float BIAS = 4f;

            targetInteractionRadius *= 1f + (m * BIAS / (((targetInteractionRadius - BIAS) / BIAS) + BIAS));

            CharacterMotor motor = replacementPrefab.GetComponent<CharacterMotor>();
            if (!motor || motor.isFlying)
            {
                targetInteractionRadius *= 2f;

#if DEBUG
                Log.Debug($"Flying(?) body ({replacementPrefab.name}), increasing interaction radius");
#endif
            }

            float oldDistance = interactor.maxInteractionDistance;

            interactor.maxInteractionDistance = Mathf.Max(interactor.maxInteractionDistance, targetInteractionRadius);

            if (oldDistance != interactor.maxInteractionDistance)
            {
                MiscUtils.AppendDelegate(ref undoCallback, _ =>
                {
                    if (interactor)
                    {
                        interactor.maxInteractionDistance = oldDistance;
                    }
                });

#if DEBUG
                Log.Debug($"Override {nameof(Interactor.maxInteractionDistance)} for {replacementPrefab.name}: {oldDistance}->{interactor.maxInteractionDistance}");
#endif
            }

            return undoCallback;
        }

        static PrefabUndoModificationDelegate initHereticReplacementPrefab_Skills(GameObject replacementPrefab)
        {
            if (!replacementPrefab.TryGetComponent(out SkillLocator skillLocator))
                return null;

            PrefabUndoModificationDelegate undoCallback = null;

            void initializeHereticSkill(ref GenericSkill skill, GenericSkill hereticSkill, string fallbackName)
            {
                if (!skill)
                {
#if DEBUG
                    Log.Debug($"Adding skill {fallbackName} to {replacementPrefab.name}");
#endif

                    skill = replacementPrefab.gameObject.AddComponent<GenericSkill>();

                    // ref vars can't be used in lambdas
                    GenericSkill createdSkillDummyVar = skill;
                    MiscUtils.AppendDelegate(ref undoCallback, _ =>
                    {
                        GameObject.Destroy(createdSkillDummyVar);
                    });
                }

                ref string skillName = ref skill.skillName;
                if (string.IsNullOrEmpty(skillName))
                {
                    string originalName = skillName;

                    string hereticSkillName = hereticSkill.skillName;
                    if (!string.IsNullOrEmpty(hereticSkillName))
                    {
                        skillName = hereticSkillName;
                    }
                    else
                    {
                        skillName = Main.PluginGUID + "_" + fallbackName;
                    }

                    // ref vars can't be used in lambdas
                    GenericSkill skillDummyVar = skill;
                    MiscUtils.AppendDelegate(ref undoCallback, _ =>
                    {
                        if (skillDummyVar)
                        {
                            skillDummyVar.skillName = originalName;
                        }
                    });
                }

                if (!skill.skillFamily)
                {
                    SkillFamily originalSkillFamily = skill.skillFamily;
                    if (MiscUtils.TryAssign(ref skill._skillFamily, hereticSkill.skillFamily))
                    {
                        // ref vars can't be used in lambdas
                        GenericSkill skillDummyVar = skill;
                        MiscUtils.AppendDelegate(ref undoCallback, _ =>
                        {
                            if (skillDummyVar)
                            {
                                skillDummyVar._skillFamily = originalSkillFamily;
                            }
                        });
                    }
                }

                string stateMachineName = hereticSkill.skillFamily.defaultSkillDef.activationStateMachineName;
                if (!EntityStateMachine.FindByCustomName(replacementPrefab.gameObject, stateMachineName))
                {
                    EntityStateMachine stateMachine = replacementPrefab.gameObject.AddComponent<EntityStateMachine>();
                    stateMachine.customName = stateMachineName;
                    stateMachine.initialStateType = new SerializableEntityStateType(typeof(Idle));
                    stateMachine.mainStateType = new SerializableEntityStateType(typeof(Idle));

                    MiscUtils.AppendDelegate(ref undoCallback, _ =>
                    {
                        GameObject.Destroy(stateMachine);
                    });
                }
            }

#if DEBUG
            Log.Debug($"Add Heretic skills to {replacementPrefab.name}");
#endif

            if (_hereticPrefabSkillLocator)
            {
                const string FALLBACK_PREFIX = "randomizedHeretic";
                initializeHereticSkill(ref skillLocator.primary, _hereticPrefabSkillLocator.primary, $"{FALLBACK_PREFIX}Primary");
                initializeHereticSkill(ref skillLocator.secondary, _hereticPrefabSkillLocator.secondary, $"{FALLBACK_PREFIX}Secondary");
                initializeHereticSkill(ref skillLocator.utility, _hereticPrefabSkillLocator.utility, $"{FALLBACK_PREFIX}Utility");
                initializeHereticSkill(ref skillLocator.special, _hereticPrefabSkillLocator.special, $"{FALLBACK_PREFIX}Special");
            }

            return undoCallback;
        }
    }
}
