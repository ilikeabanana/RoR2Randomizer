using EntityStates;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2Randomizer.CustomContent;
using UnityEngine;

namespace RoR2Randomizer.Patches.MultiEntityStatePatches
{
    [PatchClass]
    public static class InitializeMultiStatePatch
    {
        static void Apply()
        {
            IL.RoR2.CharacterDeathBehavior.OnDeath += CharacterDeathBehavior_OnDeath;
        }

        static void Cleanup()
        {
            IL.RoR2.CharacterDeathBehavior.OnDeath -= CharacterDeathBehavior_OnDeath;
        }

        static void CharacterDeathBehavior_OnDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(x => x.MatchCall(SymbolExtensions.GetMethodInfo(() => EntityStateCatalog.InstantiateState(default(EntityStateIndex))))))
            {
                c.Index++; // Move after InstantiateState call

                c.Emit(OpCodes.Dup); // Dup EntityState

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(tryInitializeMultiState);
            }
            else
            {
                Log.Warning("unable to find patch location");
            }
        }

        static void tryInitializeMultiState(EntityState state, Component subStateDataHolder)
        {
            if (state is MultiEntityState multiState && subStateDataHolder.TryGetComponent<MultiEntityState.StateMachineSubStatesData>(out MultiEntityState.StateMachineSubStatesData subStateData))
            {
                multiState.Initialize(subStateData);
            }
        }
    }
}
