using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.Networking.Generic;
using RoR2Randomizer.Networking.SurvivorPodRandomizer;
using RoR2Randomizer.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.SurvivorPod
{
    [RandomizerController]
    public class SurvivorPodRandomizerController : BaseRandomizerController
    {
        static SurvivorPodRandomizerController _instance;
        public static SurvivorPodRandomizerController Instance => _instance;

        static CharacterBody[] _bodiesWithPods;
        static SpawnPodPrefabData[] _distinctPodPrefabs;

        [SystemInitializer(typeof(BodyCatalog))]
        static void Init()
        {
            IEnumerable<CharacterBody> bodiesWithPods = BodyCatalog.allBodyPrefabBodyBodyComponents.Where(b => b && (b.preferredPodPrefab || !b.preferredInitialStateType.IsNothing()));
            _bodiesWithPods = bodiesWithPods.ToArray();

            _distinctPodPrefabs = bodiesWithPods.Select(getDefaultSpawnData).Distinct(SpawnPodPrefabData.EqualityComparer).ToArray();
        }

        static SpawnPodPrefabData getDefaultSpawnData(CharacterBody body)
        {
            return new SpawnPodPrefabData(body.bodyIndex);
        }

        static readonly RunSpecific<Dictionary<BodyIndex, SpawnPodPrefabData>> _overrideSpawnPodPrefabs = new RunSpecific<Dictionary<BodyIndex, SpawnPodPrefabData>>((out Dictionary<BodyIndex, SpawnPodPrefabData> result) =>
        {
            if (shouldBeActive && Instance)
            {
                result = new Dictionary<BodyIndex, SpawnPodPrefabData>();

                foreach (CharacterBody body in _bodiesWithPods)
                {
                    SpawnPodPrefabData defaultSpawnData = getDefaultSpawnData(body);
                    result.Add(body.bodyIndex, _distinctPodPrefabs.PickWeighted(Instance.RNG, s => s == defaultSpawnData ? 0.5f : 1f));
                }

                return true;
            }

            result = default;
            return false;
        });

        static readonly RunSpecific<bool> _hasReceivedPodReplacementsFromServer = new RunSpecific<bool>();

        static bool shouldBeActive => (NetworkServer.active && ConfigManager.Misc.SurvivorPodRandomizerEnabled) || (NetworkClient.active && _hasReceivedPodReplacementsFromServer);
        public override bool IsRandomizerEnabled => shouldBeActive;

        static ulong _clearOverrideIntroBodiesListCallbackHandle;
        static readonly HashSet<CharacterBody> _bodiesToOverrideIntroAnimationForOncePodReplacementsReceived = new HashSet<CharacterBody>();

        protected override bool isNetworked => true;
        protected override IEnumerable<NetworkMessageBase> getNetMessages()
        {
            if (_overrideSpawnPodPrefabs.HasValue)
            {
                yield return new SyncSurvivorPodReplacements(_overrideSpawnPodPrefabs);
            }
        }

        static void SyncSurvivorPodReplacements_OnReceive(Dictionary<BodyIndex, SpawnPodPrefabData> overrideSpawnPods)
        {
            if (!NetworkServer.active && NetworkClient.active)
            {
                _overrideSpawnPodPrefabs.Value = overrideSpawnPods;
                _hasReceivedPodReplacementsFromServer.Value = true;

                foreach (CharacterBody body in _bodiesToOverrideIntroAnimationForOncePodReplacementsReceived)
                {
                    TryOverrideIntroAnimation(body);
                }

                _bodiesToOverrideIntroAnimationForOncePodReplacementsReceived.Clear();
            }
        }

        protected override void Awake()
        {
            base.Awake();

            SyncSurvivorPodReplacements.OnReceive += SyncSurvivorPodReplacements_OnReceive;

            _clearOverrideIntroBodiesListCallbackHandle = RunSpecificCallbacksManager.AddEntry(null, static _ =>
            {
                _bodiesToOverrideIntroAnimationForOncePodReplacementsReceived.Clear();
            }, 0);

            SingletonHelper.Assign(ref _instance, this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _overrideSpawnPodPrefabs.Dispose();
            _hasReceivedPodReplacementsFromServer.Dispose();

            RunSpecificCallbacksManager.RemoveEntry(_clearOverrideIntroBodiesListCallbackHandle);

            SyncSurvivorPodReplacements.OnReceive -= SyncSurvivorPodReplacements_OnReceive;

            SingletonHelper.Unassign(ref _instance, this);
        }

        public static void TryOverrideIntroAnimation(CharacterBody body)
        {
            if (shouldBeActive)
            {
                if (_overrideSpawnPodPrefabs.HasValue && _overrideSpawnPodPrefabs.Value.TryGetValue(body.bodyIndex, out SpawnPodPrefabData replacementPod))
                {
                    replacementPod.OverrideIntroAnimationOnBody(body);
                }
            }
            else if (!NetworkServer.active && NetworkClient.active && !_hasReceivedPodReplacementsFromServer)
            {
                _bodiesToOverrideIntroAnimationForOncePodReplacementsReceived.Add(body);
            }
        }
    }
}
