using EntityStates;
using EntityStates.BrotherMonster;
using HG;
using R2API;
using RoR2;
using RoR2Randomizer.Networking.BossRandomizer;
using RoR2Randomizer.Utility;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.Boss.BossReplacementInfo
{
    public sealed class MainMithrixReplacement : BaseMithrixReplacement
    {
        class MithrixDialogueFormatOverlayHolder : IDisposable
        {
            bool _disposed;

            public readonly MainMithrixReplacement Owner;
            public readonly LanguageAPI.LanguageOverlay Overlay;

            public MithrixDialogueFormatOverlayHolder(MainMithrixReplacement owner, string replacementToken)
            {
                Owner = owner;

                const string BROTHER_DIALOGUE_FORMAT = "BROTHER_DIALOGUE_FORMAT";
                Overlay = LanguageAPI.AddOverlay(BROTHER_DIALOGUE_FORMAT, Language.GetString(BROTHER_DIALOGUE_FORMAT).Replace(Language.GetString("BROTHER_BODY_NAME"), Language.GetString(replacementToken)));
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                Overlay?.Remove();

                if (Owner)
                    Owner._dialogueOverlayHolder = null;

                _disposed = true;
            }
        }

        static MithrixDialogueFormatOverlayHolder _mostRecentDialogueOverlayHolder;
        MithrixDialogueFormatOverlayHolder _dialogueOverlayHolder;

        public bool IsHurt;

        protected override BossReplacementType replacementType => IsHurt ? BossReplacementType.MithrixHurt : BossReplacementType.MithrixNormal;

        protected override CharacterMaster originalMasterPrefab => replacementType switch
        {
            BossReplacementType.MithrixNormal => Caches.MasterPrefabs["BrotherMaster"],
            BossReplacementType.MithrixHurt => Caches.MasterPrefabs["BrotherHurtMaster"],
            _ => null
        };

        protected override void bodyResolved()
        {
            base.bodyResolved();

#if DEBUG
            Log.Debug($"body.subtitleNameToken={_body.subtitleNameToken}");
#endif
            
            if (NetworkServer.active)
            {
                if (IsHurt)
                {
                    // Prevent low-health replacements from instantly dying
                    _body.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, SpellChannelEnterState.duration);

                    EntityStateMachine bodyState = EntityStateMachine.FindByCustomName(_body.gameObject, "Body");
                    if (bodyState)
                    {
                        if (bodyState.state != null)
                        {
                            Type hurtStateType = BossRandomizerController.Mithrix.MithrixHurtInitialState.stateType;
                            EntityState newState = hurtStateType != null
                                ? EntityStateCatalog.InstantiateState(hurtStateType)
                                : null;

                            if (newState != null)
                            {
                                bodyState.SetState(newState);
                                Log.Warning($"hurt state now: {bodyState.state?.GetType().Name}, stealCtrl on body: {_body.GetComponentInChildren<ItemStealController>()}");

                                GameObject vanillaBody = Caches.MasterPrefabs["BrotherHurtMaster"].bodyPrefab;
                                var vanillaSM = EntityStateMachine.FindByCustomName(vanillaBody, "Body");
                                Log.Warning($"vanilla initial state: {vanillaSM.initialStateType.stateType?.Name}, vanilla has stealCtrl: {vanillaBody.GetComponentInChildren<ItemStealController>()}");
                            }
                            else
                            {
                                bodyState.initialStateType = BossRandomizerController.Mithrix.MithrixHurtInitialState;
                            }
                        }
                    }
                    else
                    {
                        Log.Warning($"Body entityState for {_body.GetDisplayName()} could not be found!");
                    }

                    if (!_body.GetComponent<ReturnStolenItemsOnGettingHit>())
                    {
                        AddReturnStolenItemsOnGettingHit(_body.gameObject, _body.healthComponent);
                    }
                }
            }

            _mostRecentDialogueOverlayHolder?.Dispose();
            _mostRecentDialogueOverlayHolder = _dialogueOverlayHolder = new MithrixDialogueFormatOverlayHolder(this, _body.baseNameToken);
        }

        void OnDestroy()
        {
            _dialogueOverlayHolder?.Dispose();
            _dialogueOverlayHolder = null;
        }

        public static ReturnStolenItemsOnGettingHit AddReturnStolenItemsOnGettingHit(GameObject bodyObj, HealthComponent healthComponent)
        {
            ReturnStolenItemsOnGettingHit newReturnItems = bodyObj.AddComponent<ReturnStolenItemsOnGettingHit>();

            newReturnItems.healthComponent = healthComponent;

            ReturnStolenItemsOnGettingHit mithrixReturnItems = BossRandomizerController.Mithrix.MithrixReturnItemsComponent.Get;
            newReturnItems.minPercentagePerItem = mithrixReturnItems.minPercentagePerItem;
            newReturnItems.maxPercentagePerItem = mithrixReturnItems.maxPercentagePerItem;

            newReturnItems.initialPercentageToFirstItem = mithrixReturnItems.initialPercentageToFirstItem;

            // Re-run awake
            newReturnItems.Awake();

            if (healthComponent)
            {
                ArrayUtils.ArrayAppend(ref healthComponent.onTakeDamageReceivers, newReturnItems);
            }

            return newReturnItems;
        }
    }
}
