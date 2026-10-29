using HarmonyLib;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.Items;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.RandomizerControllers.Item;
using System;

namespace RoR2Randomizer.Patches.ItemRandomizer
{
    [PatchClass]
    static class FuelArrayItemCostPatch
    {
        static CostTypeDef _fuelArrayCostType;

        static Hook _fuelArrayPayCost_OnHook;

        static Hook _fuelArrayIsAffordable_OnHook;
        static ILHook _fuelArrayIsAffordable_ILHook;

        [SystemInitializer(typeof(CostTypeCatalog))]
        static void InitCostTypeHooks()
        {
            _fuelArrayCostType = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.VolatileBattery);

            _fuelArrayIsAffordable_ILHook = new ILHook(_fuelArrayCostType.isAffordable.Method, FuelArrayCostType_IsAffordable_IL, new ILHookConfig { ManualApply = true });

            _fuelArrayIsAffordable_OnHook = new Hook(_fuelArrayCostType.isAffordable.Method, FuelArrayCostType_IsAffordable_On, new HookConfig { ManualApply = true });

            _fuelArrayPayCost_OnHook = new Hook(_fuelArrayCostType.payCost.Method, FuelArrayCostType_PayCost, new HookConfig { ManualApply = true });

            if (_patchActive)
            {
                _fuelArrayIsAffordable_OnHook.Apply();
                _fuelArrayIsAffordable_ILHook.Apply();

                _fuelArrayPayCost_OnHook.Apply();
            }
        }

        static PickupIndex _fuelArrayPickupIndex;

        [SystemInitializer(typeof(PickupCatalog), typeof(EquipmentCatalog))]
        static void InitFuelArrayPickupIndex()
        {
            _fuelArrayPickupIndex = PickupCatalog.FindPickupIndex(RoR2Content.Equipment.QuestVolatileBattery.equipmentIndex);
        }

        static bool _patchActive;

        static void Apply()
        {
            _fuelArrayIsAffordable_ILHook?.Apply();
            _fuelArrayIsAffordable_OnHook?.Apply();

            _fuelArrayPayCost_OnHook?.Apply();

            _patchActive = true;
        }

        static void Cleanup()
        {
            _fuelArrayIsAffordable_ILHook?.Undo();
            _fuelArrayIsAffordable_OnHook?.Undo();

            _fuelArrayPayCost_OnHook?.Undo();

            _patchActive = false;
        }

        static void FuelArrayCostType_PayCost(Action<CostTypeDef.PayCostContext, CostTypeDef.PayCostResults> orig, CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result)
        {
            if (_fuelArrayPickupIndex.isValid &&
                ItemRandomizerController.TryGetReplacementPickupIndex(_fuelArrayPickupIndex, out PickupIndex fuelArrayReplacementIndex))
            {
                PickupDef fuelArrayReplacement = fuelArrayReplacementIndex.pickupDef;
                if (fuelArrayReplacement != null && !fuelArrayReplacement.IsEquipment()) // If Equipment: orig code will handle it
                {
                    CharacterMaster activatorMaster = context.activatorMaster;
                    if (!activatorMaster)
                        return;

                    fuelArrayReplacement.TryDeductFrom(activatorMaster);

                    MultiShopCardUtils.OnNonMoneyPurchase(context);

                    return;
                }
            }

            orig(context, result);
        }
        static bool FuelArrayCostType_IsAffordable_On(Func<object, CostTypeDef, CostTypeDef.IsAffordableContext, bool> orig, object self, CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
        {
            if (_fuelArrayPickupIndex.isValid &&
                ItemRandomizerController.TryGetReplacementPickupIndex(_fuelArrayPickupIndex, out PickupIndex fuelArrayReplacementIndex))
            {
                PickupDef fuelArrayReplacement = fuelArrayReplacementIndex.pickupDef;
                if (fuelArrayReplacement != null && !fuelArrayReplacement.IsEquipment())
                {
                    CharacterBody characterBody = context.activator.GetComponent<CharacterBody>();
                    if (characterBody)
                    {
                        return characterBody.inventory.GetPickupCount(fuelArrayReplacement) >= context.cost;
                    }
                }
            }

            return orig(self, costTypeDef, context);
        }

        static void FuelArrayCostType_IsAffordable_IL(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdsfld(typeof(RoR2Content.Equipment), nameof(RoR2Content.Equipment.QuestVolatileBattery)),
                              x => x.MatchCallOrCallvirt(AccessTools.PropertyGetter(typeof(EquipmentDef), nameof(EquipmentDef.equipmentIndex)))))
            {
                ILCursor cursor = foundCursors[1];
                cursor.Index++;

                cursor.EmitDelegate((EquipmentIndex fuelArrayEquipmentIndex) =>
                {
                    if (!_fuelArrayPickupIndex.isValid)
                        return fuelArrayEquipmentIndex;

                    if (!ItemRandomizerController.TryGetReplacementPickupIndex(_fuelArrayPickupIndex, out PickupIndex fuelArrayReplacementIndex))
                        return fuelArrayEquipmentIndex;

                    PickupDef fuelArrayReplacement = fuelArrayReplacementIndex.pickupDef;
                    if (fuelArrayReplacement.IsEquipment())
                    {
                        return fuelArrayReplacement.equipmentIndex;
                    }
                    else
                    {
                        Log.Warning("IL Hook ran, but replacement pickup is not an equipment, using default equipment index");
                        return fuelArrayEquipmentIndex;
                    }
                });
            }
            else
            {
                Log.Error("Failed to find patch location");
            }
        }
    }
}
