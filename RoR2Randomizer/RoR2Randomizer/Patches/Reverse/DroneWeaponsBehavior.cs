using HarmonyLib;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System.Reflection;

namespace RoR2Randomizer.Patches.Reverse
{
    [HarmonyPatch(typeof(RoR2.DroneWeaponsBehavior))]
    public static class DroneWeaponsBehavior
    {
        public static void SetNumDroneParts(Inventory inventory, int numParts)
        {
            setNumDroneParts(numParts, inventory, CharacterBody.BodyFlags.Mechanical, numParts);
        }

        [HarmonyPatch(nameof(RoR2.DroneWeaponsBehavior.UpdateMinionInventory), new System.Type[] {typeof(Inventory), typeof(CharacterBody.BodyFlags), typeof(int) })]
        [HarmonyReversePatch]
        static void setNumDroneParts(int numParts, Inventory inventory, CharacterBody.BodyFlags bodyFlags, int newStack)
        {
            void Manipulator(ILContext il)
            {
                ILCursor c = new ILCursor(il);

                FieldInfo DroneWeaponsBehavior_stack_FI = AccessTools.DeclaredField(typeof(BaseItemBodyBehavior), nameof(BaseItemBodyBehavior.stack));
                while (c.TryGotoNext(x => x.MatchLdarg(0),
                                     x => x.MatchLdfld(DroneWeaponsBehavior_stack_FI)))
                {
                    c.Index++; // Move before ldfld

                    // Remove ldfld, arg0 is the numParts argument
                    c.Remove();
                }
            }

            Manipulator(default);
        }
    }
}
