#if !DISABLE_ITEM_RANDOMIZER
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.Items;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.RandomizerControllers.Item;
using System;

namespace RoR2Randomizer.Patches.ItemRandomizer.ArtifactKey
{
    [PatchClass]
    static class ArtifactKeyCostTypePatch
    {
        [SystemInitializer(typeof(CostTypeCatalog))]
        static void Init()
        {
            CostTypeDef artifactKeyCostType = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.ArtifactShellKillerItem);
            if (artifactKeyCostType != null)
            {
                static void applyHookIfPatchClassActive(IDetour hook)
                {
                    if (_patchActive)
                    {
                        hook.Apply();
                    }
                }

                CostTypeDef.IsAffordableDelegate isAffordable = artifactKeyCostType.isAffordable;
                if (isAffordable != null)
                {
                    _artifactKeyIsAffordableILHook = new ILHook(isAffordable.Method, ArtifactKeyCostType_IsAffordable_IL, new ILHookConfig { ManualApply = true });

                    applyHookIfPatchClassActive(_artifactKeyIsAffordableILHook);

                    _artifactKeyIsAffordableOnHook = new Hook(isAffordable.Method, ArtifactKeyCostType_IsAffordable_On, new HookConfig { ManualApply = true });
                    applyHookIfPatchClassActive(_artifactKeyIsAffordableOnHook);
                }

                CostTypeDef.PayCostDelegate payCost = artifactKeyCostType.payCost;
                if (payCost != null)
                {
                    _artifactKeyPayCostHook = new Hook(payCost.Method, ArtifactKeyCostType_PayCost, new HookConfig { ManualApply = true });
                    applyHookIfPatchClassActive(_artifactKeyPayCostHook);
                }
            }
        }

        static bool _patchActive = false;

        static ILHook _artifactKeyIsAffordableILHook;
        static Hook _artifactKeyIsAffordableOnHook;
        static Hook _artifactKeyPayCostHook;

        static void Apply()
        {
            _artifactKeyIsAffordableILHook?.Apply();
            _artifactKeyIsAffordableOnHook?.Apply();
            _artifactKeyPayCostHook?.Apply();
            _patchActive = true;
        }

        static void Cleanup()
        {
            _artifactKeyIsAffordableILHook?.Undo();
            _artifactKeyIsAffordableOnHook?.Undo();
            _artifactKeyPayCostHook?.Undo();
            _patchActive = false;
        }

        static bool ArtifactKeyCostType_IsAffordable_On(Func<object, CostTypeDef, CostTypeDef.IsAffordableContext, bool> orig, object self, CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
        {
            if (ItemRandomizerController.IsEnabled)
            {
                if (ItemRandomizerController.TryGetReplacementPickupIndex(PickupCatalog.FindPickupIndex(RoR2Content.Items.ArtifactKey.itemIndex), out PickupIndex artifactKeyReplacement))
                {
                    PickupDef artifactKeyReplacementPickup = artifactKeyReplacement.pickupDef;
                    if (!artifactKeyReplacementPickup.IsItem())
                    {
                        if (!context.activator)
                            return false;

                        if (!context.activator.TryGetComponent(out CharacterBody body))
                            return false;

                        CharacterMaster master = body.master;
                        if (!master)
                            return false;

                        return master.GetPickupCount(artifactKeyReplacementPickup) >= context.cost;
                    }
                }
            }

            return orig(self, costTypeDef, context);
        }

        static void ArtifactKeyCostType_IsAffordable_IL(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            while (c.TryGotoNext(x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.ArtifactKey))))
            {
                c.Index++;
                c.EmitDelegate(static (ItemDef artifactKey) =>
                {
                    if (ItemRandomizerController.TryGetReplacementPickupIndex(PickupCatalog.FindPickupIndex(artifactKey.itemIndex), out PickupIndex artifactKeyReplacement))
                    {
                        return ItemCatalog.GetItemDef(artifactKeyReplacement.pickupDef.itemIndex);
                    }

                    return artifactKey;
                });
            }
        }

        static void ArtifactKeyCostType_PayCost(Action<object, CostTypeDef.PayCostContext, CostTypeDef.PayCostResults> orig, object self, CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result)
        {
            if (ItemRandomizerController.IsEnabled)
            {
                if (ItemRandomizerController.TryGetReplacementPickupIndex(PickupCatalog.FindPickupIndex(RoR2Content.Items.ArtifactKey.itemIndex), out PickupIndex artifactKeyReplacement))
                {
                    PickupDef artifactKeyReplacementPickup = artifactKeyReplacement.pickupDef;
                    if (!artifactKeyReplacementPickup.IsItem())
                    {
                        artifactKeyReplacementPickup.TryDeductFrom(context.activatorMaster, context.cost);
                        MultiShopCardUtils.OnNonMoneyPurchase(context);
                        return;
                    }
                }
            }

            orig(self, context, result);
        }
    }
}
#endif