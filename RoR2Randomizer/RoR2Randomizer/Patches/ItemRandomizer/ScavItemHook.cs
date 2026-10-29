#if !DISABLE_ITEM_RANDOMIZER
using EntityStates.ScavMonster;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.RandomizerControllers.Item;

namespace RoR2Randomizer.Patches.ItemRandomizer
{
    [PatchClass]
    static class ScavItemHook
    {
        static void Apply()
        {
            IL.EntityStates.ScavMonster.FindItem.OnEnter += IL_FindItem_OnEnter;
            On.EntityStates.ScavMonster.FindItem.OnEnter += On_FindItem_OnEnter;

            IL.EntityStates.ScavMonster.GrantItem.GrantPickupServer += GrantItem_GrantPickupServer;

            IL.RoR2.ScavengerItemGranter.Start += ScavengerItemGranter_Start;
        }

        static void Cleanup()
        {
            IL.EntityStates.ScavMonster.FindItem.OnEnter -= IL_FindItem_OnEnter;
            On.EntityStates.ScavMonster.FindItem.OnEnter -= On_FindItem_OnEnter;

            IL.EntityStates.ScavMonster.GrantItem.GrantPickupServer -= GrantItem_GrantPickupServer;

            IL.RoR2.ScavengerItemGranter.Start -= ScavengerItemGranter_Start;
        }

        static void On_FindItem_OnEnter(On.EntityStates.ScavMonster.FindItem.orig_OnEnter orig, FindItem self)
        {
            orig(self);

            if (self.dropPickup.isValid && self.dropPickup.pickupDef.IsEquipment())
            {
                self.itemsToGrant = 1;
            }
        }

        static void IL_FindItem_OnEnter(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;
            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdfld<FindItem>(nameof(FindItem.pickupDisplay)),
                              x => x.MatchLdarg(0),
                              x => x.MatchLdfld<FindItem>(nameof(FindItem.dropPickup)),
                              x => x.MatchCallOrCallvirt<PickupDisplay>(nameof(PickupDisplay.SetPickupIndex))))
            {
                ILCursor dropPickupCursor = foundCursors[2];
                dropPickupCursor.Index++;
                dropPickupCursor.Emit(OpCodes.Ldarg_0);
                dropPickupCursor.EmitDelegate(static (PickupIndex dropPickup, FindItem instance) =>
                {
                    if (instance != null && instance.isAuthority)
                    {
                        return instance.dropPickup = ItemRandomizerController.GetReplacementPickupIndex(dropPickup);
                    }

                    return dropPickup;
                });
            }
        }

        static void GrantItem_GrantPickupServer(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int pickupDefLocalIndex = -1;
            int itemIndexLocalIndex = -1;

            ILCursor[] foundCursors;
            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdloc(out pickupDefLocalIndex),
                              x => x.MatchLdfld<PickupDef>(nameof(PickupDef.itemIndex)),
                              x => x.MatchStloc(out itemIndexLocalIndex),
                              x => x.MatchLdloc(itemIndexLocalIndex)))
            {
                ILCursor cursor = foundCursors[3];

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc, pickupDefLocalIndex);
                cursor.Emit(OpCodes.Ldarg_2);
                cursor.EmitDelegate((GrantItem instance, PickupDef pickupDef, int countToGrant) =>
                {
                    if (instance == null || pickupDef == null)
                        return false;

                    if (pickupDef.IsItem()) // If it's an item, use default implementation
                        return false;

                    CharacterBody body = instance.characterBody;
                    if (!body)
                        return false;

                    CharacterMaster master = body.master;
                    if (!master)
                        return false;

                    return pickupDef.TryGrantTo(master, countToGrant);
                });

                ILLabel skipGiveItemLabel = c.DefineLabel();
                cursor.Emit(OpCodes.Brtrue, skipGiveItemLabel);

                if (cursor.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<Inventory>(_ => _.GiveItemPermanent(default(ItemIndex), default)))))
                {
                    cursor.MarkLabel(skipGiveItemLabel);
                }
                else
                {
                    Log.Warning("Failed to find skipGiveItemLabel location");
                }
            }
            else
            {
                Log.Warning("Failed to find patch location");
            }
        }

        static void ScavengerItemGranter_Start(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;

            int stackRollDataLocalIndex = -1;
            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdloc(out _),
                              x => x.MatchLdloc(out _),
                              x => x.MatchLdelemAny<ScavengerItemGranter.StackRollData>(),
                              x => x.MatchStloc(out stackRollDataLocalIndex)))
            {
#if DEBUG
                Log.Debug($"{nameof(stackRollDataLocalIndex)}={stackRollDataLocalIndex}");
#endif
            }
            else
            {
                Log.Error("Failed to find stackRollData local index");
                return;
            }

            int rolledItemPickupDefLocalIndex = -1;
            if (c.TryFindNext(out foundCursors,
                              x => x.MatchCallOrCallvirt<PickupDropTable>(nameof(PickupDropTable.GenerateDrop)),
                              x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo(() => PickupCatalog.GetPickupDef(default))),
                              x => x.MatchStloc(out rolledItemPickupDefLocalIndex)))
            {
#if DEBUG
                Log.Debug($"{nameof(rolledItemPickupDefLocalIndex)}={rolledItemPickupDefLocalIndex}");
#endif

                ILCursor cursor = foundCursors[2];
                cursor.Index++;

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc, rolledItemPickupDefLocalIndex);
                cursor.Emit(OpCodes.Ldloc, stackRollDataLocalIndex);
                cursor.EmitDelegate((ScavengerItemGranter instance, PickupDef pickupDef, ScavengerItemGranter.StackRollData stackRollData) =>
                {
                    if (pickupDef == null ||
                        pickupDef.IsItem()) // Pickup is an item: Use default code for granting it
                    {
                        return false;
                    }

                    int pickupCount;
                    if (pickupDef.IsEquipment())
                    {
                        pickupCount = 1;
                    }
                    else
                    {
                        pickupCount = stackRollData.stacks;
                    }

                    return pickupDef.TryGrantTo(instance.GetComponent<Inventory>(), pickupCount);
                });

                ILLabel afterGiveItem = il.DefineLabel();

                cursor.Emit(OpCodes.Brtrue, afterGiveItem);

                if (cursor.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<Inventory>(_ => _.GiveItemPermanent(default(ItemIndex), default)))))
                {
                    cursor.MarkLabel(afterGiveItem);
                }
                else
                {
                    Log.Error($"Unable to find {nameof(afterGiveItem)} label location");
                }
            }
            else
            {
                Log.Error("Failed to find PickupDef local index");
            }
        }
    }
}
#endif