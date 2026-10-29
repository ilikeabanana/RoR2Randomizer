using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.CustomContent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RoR2Randomizer.Patches.Item
{
    [PatchClass]
    static class MonsterUseEquipmentDummyItemHook
    {
        static void Apply()
        {
            IL.EntityStates.GoldGat.BaseGoldGatState.FixedUpdate += AddAutoCastEquipmentItemCount;
            IL.RoR2.EquipmentSlot.FixedUpdate += AddAutoCastEquipmentItemCount;
            IL.RoR2.UI.EquipmentIcon.SetDisplayData += AddAutoCastEquipmentItemCount;
        }

        static void Cleanup()
        {
            IL.EntityStates.GoldGat.BaseGoldGatState.FixedUpdate -= AddAutoCastEquipmentItemCount;
            IL.RoR2.EquipmentSlot.FixedUpdate -= AddAutoCastEquipmentItemCount;
            IL.RoR2.UI.EquipmentIcon.SetDisplayData -= AddAutoCastEquipmentItemCount;
        }

        static void AddAutoCastEquipmentItemCount(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;
            while (c.TryFindNext(out foundCursors,
                                 x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.AutoCastEquipment)),
                                 x => x.MatchCallOrCallvirt(SymbolExtensions.GetMethodInfo<Inventory>(_ => _.GetItemCountEffective(default(ItemDef))))))
            {
                // Duplicate inventory instance
                foundCursors[0].Emit(OpCodes.Dup);

                // Move all incoming label targets to Dup instruction
                foreach (ILLabel label in foundCursors[0].IncomingLabels)
                {
                    label.Target = foundCursors[0].Previous;
                }

                ILCursor cursor = foundCursors[1];
                cursor.Index++;

                cursor.EmitDelegate((Inventory inventory, int autoCastEquipmentItemCount) =>
                {
                    return autoCastEquipmentItemCount + inventory.GetItemCountEffective(ContentPackManager.Items.MonsterUseEquipmentDummyItem);
                });

                c.Index = cursor.Index + 1;
            }
        }
    }
}
