using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.CustomContent;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.RandomizerControllers.Item;
using UnityEngine.Networking;

namespace RoR2Randomizer.Patches.ItemRandomizer
{
    [PatchClass]
    static class InfiniteTower_GenericPickupFix
    {
        static void Apply()
        {
            IL.RoR2.InfiniteTowerRun.AdvanceWave += InfiniteTowerRun_AdvanceWave;
            On.RoR2.InfiniteTowerWaveController.OnCombatSquadMemberDiscovered += InfiniteTowerWaveController_OnCombatSquadMemberDiscovered;
        }

        static void Cleanup()
        {
            IL.RoR2.InfiniteTowerRun.AdvanceWave -= InfiniteTowerRun_AdvanceWave;
            On.RoR2.InfiniteTowerWaveController.OnCombatSquadMemberDiscovered -= InfiniteTowerWaveController_OnCombatSquadMemberDiscovered;
        }

        static void InfiniteTowerWaveController_OnCombatSquadMemberDiscovered(On.RoR2.InfiniteTowerWaveController.orig_OnCombatSquadMemberDiscovered orig, InfiniteTowerWaveController self, CharacterMaster master)
        {
            orig(self, master);

            if (NetworkServer.active)
            {
                master.inventory.CopyEquipmentFrom(self.enemyInventory);

                ItemRandomizerController.HandleCharacterGrantedRandomizedEquipment(master);
            }
        }

        static void InfiniteTowerRun_AdvanceWave(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;

            int enemyItemEntryLocalIndex = -1;
            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdfld<InfiniteTowerRun>(nameof(InfiniteTowerRun.enemyItemPattern)),
                              x => x.MatchLdelemAny<InfiniteTowerRun.EnemyItemEntry>(),
                              x => x.MatchStloc(out enemyItemEntryLocalIndex)))
            {
#if DEBUG
                Log.Debug($"{nameof(enemyItemEntryLocalIndex)}={enemyItemEntryLocalIndex}");
#endif
            }
            else
            {
                Log.Error("Failed to find enemyItemEntry local index");
                return;
            }

            int pickupDefLocalIndex = -1;
            if (c.TryFindNext(out foundCursors,
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo(() => PickupCatalog.GetPickupDef(default))),
                              x => x.MatchStloc(out pickupDefLocalIndex)))
            {
#if DEBUG
                Log.Debug($"{nameof(pickupDefLocalIndex)}={pickupDefLocalIndex}");
#endif
            }
            else
            {
                Log.Error("Failed to find pickupDef local index");
                return;
            }

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdloc(pickupDefLocalIndex),
                              x => x.MatchBrfalse(out _)))
            {
                ILCursor cursor = foundCursors[1];
                cursor.Index++;

                cursor.Emit(OpCodes.Ldloc, enemyItemEntryLocalIndex);
                cursor.Emit(OpCodes.Ldloc, pickupDefLocalIndex);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((InfiniteTowerRun.EnemyItemEntry itemEntry, PickupDef pickup, InfiniteTowerRun instance) =>
                {
                    if (pickup == null ||
                        pickup.IsItem()) // If Item: Use default code
                    {
                        return false;
                    }

                    int count;
                    if (pickup.IsEquipment())
                    {
                        count = 1;
                    }
                    else
                    {
                        count = itemEntry.stacks;
                    }

                    return pickup.TryGrantTo(instance.enemyInventory, count, true);
                });

                ILLabel afterGiveItemLabel = il.DefineLabel();

                cursor.Emit(OpCodes.Brtrue, afterGiveItemLabel);

                if (cursor.TryGotoNext(MoveType.After,
                       x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<Inventory>(_ => _.GiveItemPermanent(default(ItemIndex), default)))))
                {
                    cursor.MarkLabel(afterGiveItemLabel);
                }
                else
                {
                    Log.Error($"Failed to find {afterGiveItemLabel} target");
                    return;
                }
            }
            else
            {
                Log.Error("Failed to find patch location");
                return;
            }
        }
    }
}
