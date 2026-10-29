using HarmonyLib;
using HG;
using RoR2;
using RoR2.Items;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.Networking.Generic;
using RoR2Randomizer.Networking.ItemTierRandomizer;
using RoR2Randomizer.PrefabMarkers;
using RoR2Randomizer.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.Item_Tier
{
    [RandomizerController]
    public sealed class ItemTierRandomizerController : BaseRandomizerController
    {
        static ItemTierRandomizerController _instance;
        public static ItemTierRandomizerController Instance => _instance;

        static ItemTier[] _originalItemTiers;

        static ItemIndex[] _itemIndicesToRandomize;

        [SystemInitializer(typeof(ItemCatalog))]
        static void InitOriginalItemTiers()
        {
            _originalItemTiers = ItemCatalog.GetPerItemBuffer<ItemTier>();

            foreach (ItemIndex index in ItemCatalog.allItems)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(index);
                if (!itemDef)
                {
#if DEBUG
                    Log.Warning($"Null ItemDef at index {index}");
#endif
                    continue;
                }

                _originalItemTiers[(int)index] = itemDef.tier;
            }

            _itemIndicesToRandomize = ItemCatalog.allItemDefs.Where(item =>
            {
                switch (item.tier)
                {
                    case ItemTier.Tier1:
                    case ItemTier.Tier2:
                    case ItemTier.Tier3:
                    case ItemTier.Lunar:
                    case ItemTier.Boss:
                    case ItemTier.VoidTier1:
                    case ItemTier.VoidTier2:
                    case ItemTier.VoidTier3:
                    case ItemTier.VoidBoss:
                        break;
                    default:
#if DEBUG
                        Log.Debug($"Excluding item {item.name} with tier {item.tier}");
#endif
                        return false;
                }

                if (item.hidden)
                {
#if DEBUG
                    Log.Debug($"Excluding hidden item {item.name}");
#endif
                    return false;
                }

                if (!item.pickupModelPrefab || item.pickupModelPrefab.GetComponent<NullModelMarker>())
                {
#if DEBUG
                    Log.Debug($"excluding pickup {item.name} due to: null {nameof(ItemDef.pickupModelPrefab)}");
#endif
                    return false;
                }

                if (string.IsNullOrEmpty(item.nameToken) || Language.GetString(item.nameToken) == item.nameToken)
                {
#if DEBUG
                    Log.Debug($"excluding pickup {item.name} due to: invalid name token ({item.nameToken})");
#endif
                    return false;
                }

                return true;
            }).Select(item => item.itemIndex).ToArray();
        }

        public static bool IsEnabled => (NetworkClient.active && _hasReceivedOverrideItemTiersFromServer) || (NetworkServer.active && ConfigManager.ItemTierRandomizer.Enabled);

        public override bool IsRandomizerEnabled => IsEnabled;

        protected override bool isNetworked => true;

        protected override IEnumerable<NetworkMessageBase> getNetMessages()
        {
            yield return new SyncItemTierReplacements(_overrideItemTiers);
        }

        static readonly RunSpecific<bool> _hasReceivedOverrideItemTiersFromServer = new RunSpecific<bool>();
        static readonly RunSpecific<ItemTier?[]> _overrideItemTiers = new RunSpecific<ItemTier?[]>((out ItemTier?[] result) =>
        {
            if (IsEnabled)
            {
                result = ItemCatalog.GetPerItemBuffer<ItemTier?>();

                if (result == null)
                {
#if DEBUG
                    Log.Error($"GetPerItemBuffer returned null!");
#endif
                    return false;
                }

                if (Instance == null)
                {
                    result = null;
                    return false;
                }

                IEnumerable<ItemIndex> itemIndicesToRandomize = _itemIndicesToRandomize.Where(static i =>
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(i);
                    if (!itemDef)
                        return false;

                    if (ConfigManager.ItemTierRandomizer.IsBlacklisted(i))
                    {
#if DEBUG
                        Log.Debug($"Excluding item {itemDef.name} due to: config blacklist");
#endif
                        return false;
                    }

                    if (!ConfigManager.ItemTierRandomizer.RandomizeScrap && (itemDef.ContainsTag(ItemTag.PriorityScrap) || itemDef.ContainsTag(ItemTag.Scrap)))
                    {
#if DEBUG
                        Log.Debug($"Excluding item {itemDef.name} due to: {nameof(ItemTierRandomizerConfig.RandomizeScrap)}=false");
#endif
                        return false;
                    }

                    return true;
                });

                List<ItemTier> availableItemTiers = new List<ItemTier>(itemIndicesToRandomize.Select(GetOriginalItemTier));

                foreach (ItemIndex itemIndex in itemIndicesToRandomize)
                {
                    if (availableItemTiers.Count == 0)
                    {
#if DEBUG
                        Log.Error($"collection length mismatch! Ran out of available tiers");
#endif
                        break;
                    }

                    ItemTier overrideTier = availableItemTiers.GetAndRemoveRandom(Instance.RNG);
                    if (GetOriginalItemTier(itemIndex) != overrideTier)
                    {
                        result[(int)itemIndex] = overrideTier;
                    }
                }

                if (availableItemTiers.Count > 0)
                {
#if DEBUG
                    Log.Error($"collection length mismatch! Ran out of item indices");
#endif
                }

                return true;
            }
            else
            {
                result = null;
                return false;
            }
        });

        static ulong _itemTierReplacementsCallbacksHandle;

        protected override void Awake()
        {
            base.Awake();

            SingletonHelper.Assign(ref _instance, this);

            _itemTierReplacementsCallbacksHandle = RunSpecificCallbacksManager.AddEntry(tryApplyItemTierReplacements, restoreItemTierReplacements, -1);

            SyncItemTierReplacements.OnReceive += SyncItemTierReplacements_OnReceive;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            SingletonHelper.Unassign(ref _instance, this);

            _overrideItemTiers.Dispose();
            _hasReceivedOverrideItemTiersFromServer.Dispose();

            SyncItemTierReplacements.OnReceive -= SyncItemTierReplacements_OnReceive;

            RunSpecificCallbacksManager.RemoveEntry(_itemTierReplacementsCallbacksHandle);
        }

        static void SyncItemTierReplacements_OnReceive(ItemTier?[] itemTierOverrides)
        {
            _overrideItemTiers.Value = itemTierOverrides;
            _hasReceivedOverrideItemTiersFromServer.Value = true;

            tryApplyItemTierReplacements();
        }

        static void refreshPickupDef(ItemDef item)
        {
            ItemTier tierIndex = item.tier;
            ItemTierDef tierDef = ItemTierCatalog.GetItemTierDef(tierIndex);
            if (!tierDef)
                return;

            PickupIndex pickupIndex = PickupCatalog.FindPickupIndex(item.itemIndex);
            if (!pickupIndex.isValid)
                return;

            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef == null)
                return;

            pickupDef.itemTier = tierIndex;

            pickupDef.baseColor = ColorCatalog.GetColor(tierDef.colorIndex);
            pickupDef.darkColor = ColorCatalog.GetColor(tierDef.darkColorIndex);

            pickupDef.isLunar = tierIndex == ItemTier.Lunar;
            pickupDef.isBoss = tierIndex == ItemTier.Boss;

            pickupDef.dropletDisplayPrefab = tierDef.dropletDisplayPrefab;

            pickupDef.iconSprite = item.pickupIconSprite;
            pickupDef.iconTexture = item.pickupIconTexture;
        }

        static void tryApplyItemTierReplacements()
        {
            if (!IsEnabled && Instance)
                return;

            if (!_overrideItemTiers.HasValue || _overrideItemTiers.Value == null)
            {
                Log.Warning("Trying to apply item tier replacements, but no override tiers are defined");
                return;
            }

            foreach (ItemIndex itemIndex in ItemCatalog.allItems)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (!itemDef)
                {
#if DEBUG
                    Log.Warning($"Null ItemDef at index {itemIndex}");
#endif
                    continue;
                }

                ItemTier originalItemTier = itemDef.tier;
                itemDef.tier = GetOverrideItemTier(itemIndex);

                if (originalItemTier == itemDef.tier)
                    continue;

                refreshPickupDef(itemDef);

                if ((originalItemTier == ItemTier.Boss) != (itemDef.tier == ItemTier.Boss))
                {
                    static bool itemCorrupts(ItemIndex itemIndex, out int index)
                    {
                        for (int i = 0; i < ContagiousItemManager.transformationInfos.Length; i++)
                        {
                            if (ContagiousItemManager.transformationInfos[i].originalItem == itemIndex)
                            {
                                index = i;
                                return true;
                            }
                        }

                        index = -1;
                        return false;
                    }

                    if (originalItemTier == ItemTier.Boss)
                    {
                        if (itemCorrupts(itemIndex, out int i))
                        {
                            RemoveTransformationInfoAt(i);
                        }
                    }
                    else
                    {
                        if (!itemCorrupts(itemIndex, out _))
                        {
                            AppendTransformationInfo(new ContagiousItemManager.TransformationInfo
                            {
                                originalItem = itemIndex,
                                transformedItem = DLC1Content.Items.VoidMegaCrabItem.itemIndex
                            });
                        }
                    }
                }

#if DEBUG
                Log.Debug($"Override item tier for {itemDef.name}: {originalItemTier} -> {itemDef.tier}");
#endif
            }

            Run.instance.BuildDropTable();

            FieldInfo runPickupsModifiedDelegateField = AccessTools.DeclaredField(typeof(Run), nameof(Run.onAvailablePickupsModified));
            if (runPickupsModifiedDelegateField != null)
            {
                Action<Run> pickupsModifiedDelegate = (Action<Run>)runPickupsModifiedDelegateField.GetValue(null);
                pickupsModifiedDelegate?.Invoke(Run.instance);
            }
            else
            {
#if DEBUG
                Log.Error($"{nameof(runPickupsModifiedDelegateField)} is null");
#endif
            }
        }

        static void AppendTransformationInfo(ContagiousItemManager.TransformationInfo info)
        {
            ContagiousItemManager.TransformationInfo[] backing = ContagiousItemManager.transformationInfos.ToArray();
            ArrayUtils.ArrayAppend(ref backing, info);
            ContagiousItemManager.transformationInfos = new ReadOnlyArray<ContagiousItemManager.TransformationInfo>(backing);
        }

        static void RemoveTransformationInfoAt(int index)
        {
            ContagiousItemManager.TransformationInfo[] backing = ContagiousItemManager.transformationInfos.ToArray();
            ArrayUtils.ArrayRemoveAtAndResize(ref backing, index);
            ContagiousItemManager.transformationInfos = new ReadOnlyArray<ContagiousItemManager.TransformationInfo>(backing);
        }

        static void restoreItemTierReplacements()
        {
            foreach (ItemIndex index in ItemCatalog.allItems)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(index);
                if (!itemDef)
                {
#if DEBUG
                    Log.Warning($"Null ItemDef at index {index}");
#endif
                    continue;
                }

                itemDef.tier = GetOriginalItemTier(index);
                refreshPickupDef(itemDef);
            }

            ContagiousItemManager.transformationInfos = Array.Empty<ContagiousItemManager.TransformationInfo>();
        }

        public static ItemTier GetOriginalItemTier(ItemIndex itemIndex)
        {
            return ArrayUtils.GetSafe(_originalItemTiers, (int)itemIndex, ItemTier.AssignedAtRuntime);
        }

        public static ItemTier GetOverrideItemTier(ItemIndex index)
        {
            if (IsEnabled && _overrideItemTiers.HasValue && _overrideItemTiers.Value != null)
            {
                ItemTier? overrideTier = ArrayUtils.GetSafe(_overrideItemTiers.Value, (int)index, null);
                if (overrideTier.HasValue)
                {
                    return overrideTier.Value;
                }
            }

            return GetOriginalItemTier(index);
        }
    }
}