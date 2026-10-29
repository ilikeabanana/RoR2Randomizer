#if !DISABLE_ITEM_RANDOMIZER
using HG;
using R2API;
using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.CustomContent;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.Networking.Generic;
using RoR2Randomizer.Networking.ItemRandomizer;
using RoR2Randomizer.PrefabMarkers;
using RoR2Randomizer.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.Item
{
    [RandomizerController]
    public class ItemRandomizerController : BaseRandomizerController
    {
        static ItemRandomizerController _instance;

        static int[] _pickupIndicesToRandomize;

        static readonly ulong _registerItemNameLanguageOverlaysCallbackHandle;

        static ItemRandomizerController()
        {
            _registerItemNameLanguageOverlaysCallbackHandle = RunSpecificCallbacksManager.AddEntry(registerItemNameLanguageOverlays, null, -1);
        }

        [SystemInitializer(typeof(PickupCatalog), typeof(ItemCatalog), typeof(NullModelMarker))]
        static void InitPickupIndicesToRandomize()
        {
            _pickupIndicesToRandomize = PickupCatalog.allPickups
                                                     .Where(static pd =>
                                                     {
                                                         if (pd.artifactIndex != ArtifactIndex.None)
                                                         {
#if DEBUG
                                                             Log.Debug($"excluding artifact pickup {pd.pickupIndex}");
#endif
                                                             return false;
                                                         }

                                                         if (pd.miscPickupIndex != MiscPickupIndex.None)
                                                         {
#if DEBUG
                                                             Log.Debug($"excluding misc pickup {pd.pickupIndex}");
#endif
                                                             return false;
                                                         }

                                                         if (pd.internalName.StartsWith("ItemTier."))
                                                         {
#if DEBUG
                                                             Log.Debug($"excluding {nameof(ItemTier)} pickup {pd.pickupIndex}");
#endif
                                                             return false;
                                                         }

                                                         if (pd.IsEquipment() && ReadOnlyArray<EquipmentIndex>.BinarySearch(CharacterReplacements.AvailableDroneEquipments, pd.equipmentIndex) < 0)
                                                         {
#if DEBUG
                                                             Log.Debug($"excluding invalid equipment pickup {pd.pickupIndex}");
#endif
                                                             return false;
                                                         }

                                                         if (!pd.dropletDisplayPrefab)
                                                         {
#if DEBUG
                                                             Log.Debug($"excluding pickup {pd.pickupIndex} due to: null {nameof(PickupDef.dropletDisplayPrefab)}");
#endif
                                                             return false;
                                                         }

                                                         if (!pd.displayPrefab || pd.displayPrefab.GetComponent<NullModelMarker>())
                                                         {
#if DEBUG
                                                             Log.Debug($"excluding pickup {pd.pickupIndex} due to: null {nameof(PickupDef.displayPrefab)}");
#endif
                                                             return false;
                                                         }

                                                         if (string.IsNullOrEmpty(pd.nameToken) || Language.GetString(pd.nameToken) == pd.nameToken)
                                                         {
#if DEBUG
                                                             Log.Debug($"excluding pickup {pd.pickupIndex} due to: invalid name token {pd.nameToken}");
#endif
                                                             return false;
                                                         }

                                                         if (pd.IsItem())
                                                         {
                                                             ItemDef itemDef = ItemCatalog.GetItemDef(pd.itemIndex);
                                                             if (itemDef == null || itemDef.hidden)
                                                             {
#if DEBUG
                                                                 Log.Debug($"excluding pickup {pd.pickupIndex} due to: hidden item ({itemDef})");
#endif
                                                                 return false;
                                                             }
                                                         }

                                                         return true;
                                                     })
                                                     .Select(static pd => pd.pickupIndex.value)
                                                     .ToArray();
        }

        public override bool IsRandomizerEnabled => IsEnabled;

        public static bool IsEnabled => (NetworkClient.active && _hasReceivedPickupIndexReplacementsFromServer) || (NetworkServer.active && ConfigManager.ItemRandomizer.Enabled);

        static readonly RunSpecific<bool> _hasReceivedPickupIndexReplacementsFromServer = new RunSpecific<bool>();

        static readonly RunSpecific<IndexReplacementsCollection> _pickupIndexReplacements = new RunSpecific<IndexReplacementsCollection>(tryComputePickupIndexReplacements);

        // Extracted into a named method (instead of an inline lambda) so it can also be
        // invoked from Awake() below to self-heal if the RunSpecific's one-shot onRunStart
        // callback fired before this controller's Awake() had a chance to assign _instance.
        static bool tryComputePickupIndexReplacements(out IndexReplacementsCollection result)
        {
            // _instance may not be assigned yet if Run.Start() (and therefore this RunSpecific's
            // onRunStart callback) fires before this controller's own Awake() has run - e.g. on
            // scene load / network spawn ordering. Bail out safely instead of NREing on _instance.RNG;
            // Awake() will retry this once _instance becomes available.
            if (IsEnabled && _instance)
            {
                IEnumerable<int> pickupIndicesToRandomize = _pickupIndicesToRandomize.Where(i =>
                {
                    if (ConfigManager.ItemRandomizer.IsBlacklisted(new PickupIndex(i)))
                    {
#if DEBUG
                        Log.Debug($"Excluding pickup {new PickupIndex(i)} due to: config blacklist");
#endif
                        return false;
                    }

                    return true;
                });

                result = new IndexReplacementsCollection(ReplacementDictionary<int>.CreateFrom(pickupIndicesToRandomize, _instance.RNG), PickupCatalog.pickupCount);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        protected override bool isNetworked => true;

        protected override IEnumerable<NetworkMessageBase> getNetMessages()
        {
            yield return new SyncItemReplacements(_pickupIndexReplacements);
        }

        protected override void Awake()
        {
            base.Awake();

            SyncItemReplacements.OnReceive += SyncItemReplacements_OnReceive;

            SingletonHelper.Assign(ref _instance, this);

            // If the run already started before _instance was assigned above, RunSpecific's
            // onRunStart callback would have run with _instance still null and (safely) bailed
            // out without setting a value. Retry now that _instance is actually available.
            if (IsEnabled && !_pickupIndexReplacements.HasValue && tryComputePickupIndexReplacements(out IndexReplacementsCollection result))
            {
                _pickupIndexReplacements.Value = result;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _pickupIndexReplacements.Dispose();

            RunSpecificCallbacksManager.RemoveEntry(_registerItemNameLanguageOverlaysCallbackHandle);

            SyncItemReplacements.OnReceive -= SyncItemReplacements_OnReceive;

            SingletonHelper.Unassign(ref _instance, this);
        }

        static void SyncItemReplacements_OnReceive(in IndexReplacementsCollection itemIndexReplacements)
        {
            _pickupIndexReplacements.Value = itemIndexReplacements;
            _hasReceivedPickupIndexReplacementsFromServer.Value = true;

            registerItemNameLanguageOverlays();
        }

        static void registerItemNameLanguageOverlays()
        {
            if (!IsEnabled)
                return;

#if DEBUG
            Log.Debug("Registering item name replacements");
#endif

            static void createCostTypeOverlays(PickupIndex originalPickupIndex, string key)
            {
                if (TryGetReplacementPickupIndex(originalPickupIndex, out PickupIndex replacementPickupIndex))
                {
                    PickupDef replacementPickup = replacementPickupIndex.pickupDef;

                    foreach (Language lang in Language.GetAllLanguages())
                    {
                        LanguageAPI.LanguageOverlay overlay = LanguageAPI.AddOverlay(key, $"{{0}} {lang.GetLocalizedStringByToken(replacementPickup.nameToken)}", lang.name);

                        RunSpecificLanguageOverlay.AddRunLanguageOverlay(overlay);
                    }
                }
            }

            PickupIndex artifactKeyIndex = PickupCatalog.FindPickupIndex(RoR2Content.Items.ArtifactKey.itemIndex);
            createCostTypeOverlays(artifactKeyIndex, "COST_ARTIFACTSHELLKILLERITEM_FORMAT");

            PickupIndex fuelArrayIndex = PickupCatalog.FindPickupIndex(RoR2Content.Equipment.QuestVolatileBattery.equipmentIndex);
            createCostTypeOverlays(fuelArrayIndex, "COST_VOLATILEBATTERY_FORMAT");
        }

        public static bool TryGetReplacementPickupIndex(in PickupIndex original, out PickupIndex replacement)
        {
            if (IsEnabled &&
                original.isValid &&
                _pickupIndexReplacements.HasValue &&
                _pickupIndexReplacements.Value.TryGetReplacement(original.value, out int replacementIndex))
            {
                replacement = new PickupIndex(replacementIndex);
#if DEBUG
                Log.Debug($"Replaced pickup: {original} -> {replacement}");
#endif
                return replacement.isValid;
            }

            replacement = PickupIndex.none;
            return false;
        }

        public static PickupIndex GetReplacementPickupIndex(PickupIndex pickupIndex)
        {
            if (TryGetReplacementPickupIndex(pickupIndex, out PickupIndex replacement))
            {
                return replacement;
            }
            else
            {
                return pickupIndex;
            }
        }

        public static void HandleCharacterGrantedRandomizedEquipment(CharacterMaster master)
        {
            if (!master || !IsEnabled)
                return;

            if (master.playerCharacterMasterController)
                return;

            Inventory inventory = master.inventory;
            if (!inventory || inventory.currentEquipmentIndex == EquipmentIndex.None)
                return;

            // You wanted AI to activate equipment? Too bad, can't be bothered B)
            inventory.GiveItemIfMissing(ContentPackManager.Items.MonsterUseEquipmentDummyItem);
        }
    }
}
#endif