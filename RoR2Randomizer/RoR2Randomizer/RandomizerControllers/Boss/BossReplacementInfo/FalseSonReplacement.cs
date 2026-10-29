using EntityStates;
using EntityStates.FalseSonBoss;
using R2API;
using RoR2;
using RoR2Randomizer.Networking.BossRandomizer;
using RoR2Randomizer.Patches.BossRandomizer.FalseSon;
using RoR2Randomizer.Utility;
using System;

namespace RoR2Randomizer.RandomizerControllers.Boss.BossReplacementInfo
{
    public class FalseSonReplacement : BaseBossReplacement
    {

        class FalseSonDialogueFormatOverlayHolder : IDisposable
        {
            bool _disposed;

            public readonly FalseSonReplacement Owner;
            public readonly LanguageAPI.LanguageOverlay Overlay;

            public FalseSonDialogueFormatOverlayHolder(FalseSonReplacement owner, string replacementToken)
            {
                Owner = owner;

                const string BROTHER_DIALOGUE_FORMAT = "FALSESONBOSS_DIALOGUE_FORMAT";
                Overlay = LanguageAPI.AddOverlay(BROTHER_DIALOGUE_FORMAT, Language.GetString(BROTHER_DIALOGUE_FORMAT).Replace(Language.GetString("FALSESONBOSS_BODY_NAME"), Language.GetString(replacementToken)));
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

        static FalseSonDialogueFormatOverlayHolder _mostRecentDialogueOverlayHolder;
        FalseSonDialogueFormatOverlayHolder _dialogueOverlayHolder;

        public uint Phase;

        protected override BossReplacementType replacementType
        {
            get
            {
                if (Phase >= 1 && Phase <= FalseSonPhaseTracker.TotalNumPhases) // TODO: confirm FalseSonPhaseTracker exposes TotalNumPhases like VoidlingPhaseTracker does
                {
                    return BossReplacementType.FalseSonBoss + Phase - 1;
                }
                else
                {
                    return BossReplacementType.Invalid;
                }
            }
        }

        protected override CharacterMaster originalMasterPrefab
        {
            get
            {
                switch (Phase)
                {
                    case 1:
                        return Caches.MasterPrefabs[$"FalseSonBoss"];
                    case 2:
                        return Caches.MasterPrefabs[$"FalseSonBossBodyLunarShard"];
                    case 3:
                        return Caches.MasterPrefabs[$"FalseSonBossBodyBrokenLunarShard"];
                    default:
                        return Caches.MasterPrefabs[$"FalseSonBoss"];
                }
                
            }
        }

        protected override void bodyResolved()
        {
            base.bodyResolved();
            if(Phase == 3)
            {
                _master.onBodyDeath.AddListener(() =>
                {
                    EntityStateMachine machine = _body.GetComponent<EntityStateMachine>();
                    if (machine != null)
                    {
                        EntityStateIndex index = EntityStateCatalog.GetStateIndex(typeof(SkyJumpDeathState));
                        EntityState state = EntityStateCatalog.InstantiateState(index);
                        machine.SetNextState(state);
                    }
                });
            }
            _mostRecentDialogueOverlayHolder?.Dispose();
            _mostRecentDialogueOverlayHolder = _dialogueOverlayHolder = new FalseSonDialogueFormatOverlayHolder(this, _body.baseNameToken);
        }

        void OnDestroy()
        {
            _dialogueOverlayHolder?.Dispose();
            _dialogueOverlayHolder = null;
        }
    }
}