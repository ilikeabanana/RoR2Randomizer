#if DEBUG
using HarmonyLib;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Reflection;

namespace RoR2Randomizer.Patches.Debug
{
    [PatchClass]
    static class InfiniteLunarCoin
    {
        public static void ToggleEnabled()
        {
            _enabled = !_enabled;
            Log.Debug_NoCallerPrefix($"Infinite Lunar Coins {(_enabled ? "Enabled" : "Disabled")}");
        }

        static bool _enabled;

        delegate bool orig_CostTypeDef_IsAffordable(object self, CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context);
        delegate void orig_CostTypeDef_PayCost(object self, CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result);

        static readonly Hook[] _lunarCoinDelegateHooks = new Hook[2];

        [SystemInitializer(typeof(CostTypeCatalog))]
        static void InitCostTypeHooks()
        {
            CostTypeDef lunarCoinCostType = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.LunarCoin);
            if (lunarCoinCostType == null)
            {
                Log.Warning($"{nameof(lunarCoinCostType)} is null");
                return;
            }

            CostTypeDef.IsAffordableDelegate isAffordable = lunarCoinCostType.isAffordable;
            if (isAffordable != null)
            {
                _lunarCoinDelegateHooks[0] = new Hook(isAffordable.Method, (orig_CostTypeDef_IsAffordable orig, object self, CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context) =>
                {
                    return _enabled || orig(self, costTypeDef, context);
                }, new HookConfig { ManualApply = true });
                _lunarCoinDelegateHooks[0].Apply();
            }

            CostTypeDef.PayCostDelegate payCost = lunarCoinCostType.payCost;
            if (payCost != null)
            {
                _lunarCoinDelegateHooks[1] = new Hook(payCost.Method, (orig_CostTypeDef_PayCost orig, object self, CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result) =>
                {
                    if (_enabled)
                        return;

                    orig(self, context, result);
                });
                _lunarCoinDelegateHooks[1].Apply();
            }
        }

        static void Apply()
        {
            On.RoR2.BazaarUpgradeInteraction.CanBeAffordedByInteractor += BazaarUpgradeInteraction_CanBeAffordedByInteractor;

            On.RoR2.NetworkUser.RpcDeductLunarCoins += NetworkUser_RpcDeductLunarCoins;

            if (_lunarCoinDelegateHooks != null)
            {
                foreach (Hook hook in _lunarCoinDelegateHooks)
                {
                    hook?.Apply();
                }
            }
        }

        static void Cleanup()
        {
            On.RoR2.BazaarUpgradeInteraction.CanBeAffordedByInteractor -= BazaarUpgradeInteraction_CanBeAffordedByInteractor;

            On.RoR2.NetworkUser.RpcDeductLunarCoins -= NetworkUser_RpcDeductLunarCoins;

            if (_lunarCoinDelegateHooks != null)
            {
                foreach (Hook hook in _lunarCoinDelegateHooks)
                {
                    hook?.Undo();
                }
            }
        }

        static bool BazaarUpgradeInteraction_CanBeAffordedByInteractor(On.RoR2.BazaarUpgradeInteraction.orig_CanBeAffordedByInteractor orig, BazaarUpgradeInteraction self, Interactor activator)
        {
            return _enabled || orig(self, activator);
        }

        static void NetworkUser_RpcDeductLunarCoins(On.RoR2.NetworkUser.orig_RpcDeductLunarCoins orig, NetworkUser self, uint count)
        {
            if (_enabled)
                return;

            orig(self, count);
        }
    }
}
#endif