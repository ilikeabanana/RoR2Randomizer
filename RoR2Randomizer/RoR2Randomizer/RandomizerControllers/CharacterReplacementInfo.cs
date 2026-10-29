using R2API.Networking;
using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.Networking;
using RoR2Randomizer.Networking.Generic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace RoR2Randomizer.RandomizerControllers
{
    public abstract class CharacterReplacementInfo : MonoBehaviour, INetMessageProvider
    {
        bool _initializeOnEnable;

        protected CharacterMaster _master;
        protected CharacterBody _body;

        public void SetOriginalMasterIndex(MasterCatalog.MasterIndex index)
        {
            _masterPrefab = MasterCatalog.GetMasterPrefab(index)?.GetComponent<CharacterMaster>();
        }

        CharacterMaster _masterPrefab;
        protected virtual CharacterMaster originalMasterPrefab => _masterPrefab;

        protected CharacterBody originalBodyPrefab
        {
            get
            {
                CharacterMaster originalMaster = originalMasterPrefab;
                if (originalMaster && originalMaster.bodyPrefab && originalMaster.bodyPrefab.TryGetComponent(out CharacterBody body))
                {
                    return body;
                }

                return null;
            }
        }

        protected abstract bool isNetworked { get; }
        bool INetMessageProvider.SendMessages => isNetworked;

        protected virtual SetSubtitleMode subtitleOverrideMode => SetSubtitleMode.DontOverride;

        void Awake()
        {
            _master = GetComponent<CharacterMaster>();

            if (isNetworked)
            {
                NetworkingManager.RegisterMessageProvider(this);
            }
        }

        void OnDestroy()
        {
            if (isNetworked)
            {
                NetworkingManager.UnregisterMessageProvider(this);
            }
        }

        void Start()
        {
            if (_initializeOnEnable)
            {
                _initializeOnEnable = false;
                Initialize();
            }
        }

        public void Initialize()
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(waitForBodyInitialized());

                if (NetworkServer.active)
                {
                    initializeServer();
                }

                initializeClient();
            }
            else
            {
                _initializeOnEnable = true;
            }
        }

        IEnumerator waitForBodyInitialized()
        {
            if (!_body)
            {
                _body = _master.GetBody();
                if (!_body)
                {
                    while (_master && !_master.hasBody)
                    {
                        yield return 0;
                    }

                    if (!_master)
                        yield break;

                    _body = _master.GetBody();
                }
            }

            if (_body)
            {
                bodyResolved();
            }
        }

        protected virtual void bodyResolved()
        {
            Inventory inventory = _master.inventory;
            if (inventory)
            {
                giveAdaptiveArmorFromOriginalMaster(inventory);

                GivePickupsOnStart givePickupsOnStart = _master.GetComponent<GivePickupsOnStart>();

                if (_body.bodyIndex == BodyCatalog.FindBodyIndex("EquipmentDroneBody"))
                {
                    giveDroneEquipment(inventory, givePickupsOnStart);
                }
                else if (_body.bodyIndex == BodyCatalog.FindBodyIndex("DroneCommanderBody")) // Col. Droneman
                {
                    const int NUM_DRONE_PARTS = 1;

                    Patches.Reverse.DroneWeaponsBehavior.SetNumDroneParts(inventory, NUM_DRONE_PARTS);

#if DEBUG
                    Log.Debug($"Gave {NUM_DRONE_PARTS} drone parts to {Language.GetString(_body.baseNameToken)}");
#endif
                }
            }

            tryOverrideBodySubtitle();
        }

        void giveDroneEquipment(Inventory inventory, GivePickupsOnStart droneGivePickupsOnStart)
        {
            if ((!droneGivePickupsOnStart || !droneGivePickupsOnStart.HasAnyEquipment()) && inventory.GetEquipmentIndex() == EquipmentIndex.None)
            {
                EquipmentIndex equipment = CharacterReplacements.AvailableDroneEquipments.GetRandomOrDefault(RoR2Application.rng, EquipmentIndex.None);
                inventory.SetEquipmentIndex(equipment);

#if DEBUG
                Log.Debug($"Gave {Language.GetString(EquipmentCatalog.GetEquipmentDef(equipment).nameToken)} to {Language.GetString(_body.baseNameToken)}");
#endif
            }
        }

        void giveAdaptiveArmorFromOriginalMaster(Inventory inventory)
        {
            CharacterMaster originalMaster = originalMasterPrefab;
            if (originalMaster)
            {
                GivePickupsOnStart originalGivePickupsOnStart = originalMaster.GetComponent<GivePickupsOnStart>();

                ItemIndex adaptiveArmorItemIndex = RoR2Content.Items.AdaptiveArmor.itemIndex;
                if (originalGivePickupsOnStart && originalGivePickupsOnStart.HasItems(adaptiveArmorItemIndex, out int originalAdaptiveArmorCount))
                {
                    inventory.GiveItemPermanent(adaptiveArmorItemIndex, originalAdaptiveArmorCount);
                }
            }
        }

        protected virtual void initializeClient()
        {
            syncDontDestroyOnLoadStatusWithOriginal();
        }

        void syncDontDestroyOnLoadStatusWithOriginal()
        {
            if (!_master || _master.GetComponent<PlayerCharacterMasterController>())
                return;

            CharacterMaster originalMaster = originalMasterPrefab;
            if (!originalMaster)
                return;

            if (originalMaster.GetComponent<SetDontDestroyOnLoad>())
            {
                _master.gameObject.GetOrAddComponent<SetDontDestroyOnLoad>();
            }
            else if (_master.TryGetComponent<SetDontDestroyOnLoad>(out SetDontDestroyOnLoad setDontDestroyOnLoad))
            {
                Destroy(setDontDestroyOnLoad);

                if (RoR2.Util.IsDontDestroyOnLoad(_master.gameObject))
                {
                    SceneManager.MoveGameObjectToScene(_master.gameObject, SceneManager.GetActiveScene()); // Remove DontDestroyOnLoad "flag"
                }
            }
        }

        protected virtual void initializeServer()
        {
            if (isNetworked && !NetworkServer.dontListen)
            {
                foreach (NetworkMessageBase message in getNetMessages())
                {
                    message.SendTo(NetworkDestination.Clients);
                }
            }
        }

        protected virtual IEnumerable<NetworkMessageBase> getNetMessages()
        {
            yield break;
        }
        IEnumerable<NetworkMessageBase> INetMessageProvider.GetNetMessages()
        {
            return getNetMessages();
        }

        protected enum SetSubtitleMode : byte
        {
            DontOverride,
            OnlyIfExistingIsNull,
            DontOverrideIfBothNotNull,
            AlwaysOverride
        }

        void tryOverrideBodySubtitle()
        {
            if (!_body)
                return;

            CharacterBody originalBody = originalBodyPrefab;
            if (!originalBody)
                return;

            string overrideToken = originalBody.subtitleNameToken;
            string currentToken = _body.subtitleNameToken;
            if (string.Equals(currentToken, overrideToken))
                return;

            SetSubtitleMode mode = subtitleOverrideMode;
            if (mode == SetSubtitleMode.DontOverride)
            {
                return;
            }
            else if (mode == SetSubtitleMode.OnlyIfExistingIsNull)
            {
                if (!string.IsNullOrEmpty(_body.subtitleNameToken))
                    return;
            }
            else if (mode == SetSubtitleMode.DontOverrideIfBothNotNull)
            {
                if (!string.IsNullOrEmpty(_body.subtitleNameToken) && !string.IsNullOrEmpty(overrideToken))
                    return;
            }

            _body.subtitleNameToken = overrideToken;

            // Update BossGroup
            if (_master && _master.isBoss)
            {
                forceRefreshBossGroupSubtitle();
            }
        }

        void forceRefreshBossGroupSubtitle()
        {
            foreach (BossGroup group in InstanceTracker.GetInstancesList<BossGroup>())
            {
                for (int i = 0; i < group.bossMemoryCount; i++)
                {
                    if (group.bossMemories[i].cachedMaster == _master)
                    {
                        // Force a refresh of the boss subtitle
                        group.bestObservedName = string.Empty;
                        group.bestObservedSubtitle = string.Empty;
                        return;
                    }
                }
            }
        }
    }
}
