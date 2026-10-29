using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using System.Reflection;

namespace RoR2Randomizer.Patches.BuffRandomizer
{
    static partial class GetBuffIndex_BuffIndex_ReplacePatch
    {
        [PatchClass]
        static class GenericOptIn
        {
            static ILContext.Manipulator getGenericManipulator(params MethodBase[] patchMethods)
            {
                return il =>
                {
                    ILCursor c = new ILCursor(il);

                    foreach (MethodBase method in patchMethods)
                    {
                        int numMatches = 0;
                        while (c.TryGotoNext(x => x.MatchCallOrCallvirt(method)))
                        {
                            c.Emit(OpCodes.Call, enablePatch_MI);
                            c.Index++;
                            c.Emit(OpCodes.Call, disablePatch_MI);

                            numMatches++;
                        }

                        if (numMatches == 0)
                        {
                            Log.Warning($"({il.Method.FullName}) no match found for {method.FullDescription()}");
                        }
#if DEBUG
                        else
                        {
                            Log.Debug($"({il.Method.FullName}) {numMatches} match(es) found for {method.FullDescription()}");
                        }
#endif

                        c.Index = 0;
                    }
                };
            }

            static readonly MethodInfo CharacterBody_GetBuffCount_BuffIndex_MI = SymbolExtensions.GetMethodInfo<CharacterBody>(_ => _.GetBuffCount(default(BuffIndex)));
            static readonly ILContext.Manipulator CharacterBody_GetBuffCount_BuffIndex_Manipulator = getGenericManipulator(CharacterBody_GetBuffCount_BuffIndex_MI);

            static readonly MethodInfo CharacterBody_GetBuffCount_BuffDef_MI = SymbolExtensions.GetMethodInfo<CharacterBody>(_ => _.GetBuffCount(default(BuffDef)));
            static readonly ILContext.Manipulator CharacterBody_GetBuffCount_BuffDef_Manipulator = getGenericManipulator(CharacterBody_GetBuffCount_BuffDef_MI);

            static readonly MethodInfo CharacterBody_HasBuff_BuffDef_MI = SymbolExtensions.GetMethodInfo<CharacterBody>(_ => _.HasBuff(default(BuffDef)));
            static readonly ILContext.Manipulator CharacterBody_HasBuff_BuffDef_Manipulator = getGenericManipulator(CharacterBody_HasBuff_BuffDef_MI);
            
            static readonly ILContext.Manipulator CharacterBody_GetBuffCount_BuffDef_CharacterBody_GetBuffCount_BuffDef_Manipulator = getGenericManipulator(CharacterBody_GetBuffCount_BuffDef_MI, CharacterBody_HasBuff_BuffDef_MI);

            static readonly ILHook RoR2_Items_ShockNearbyBodyBehavior_OnDisable = new ILHook(SymbolExtensions.GetMethodInfo<RoR2.Items.ShockNearbyBodyBehavior>(_ => _.OnDisable()), CharacterBody_HasBuff_BuffDef_Manipulator, new ILHookConfig { ManualApply = true });

            static readonly ILHook RoR2_VoidSurvivorController_get_isCorrupted = new ILHook(AccessTools.DeclaredPropertyGetter(typeof(RoR2.VoidSurvivorController), nameof(VoidSurvivorController.isCorrupted)), CharacterBody_HasBuff_BuffDef_Manipulator, new ILHookConfig { ManualApply = true });

            static void Apply()
            {
                IL.EntityStates.CaptainSupplyDrop.PlatingBuffMainState.GetInteractability += CharacterBody_GetBuffCount_BuffIndex_Manipulator;
                IL.EntityStates.CaptainSupplyDrop.PlatingBuffMainState.OnInteractionBegin += CharacterBody_GetBuffCount_BuffIndex_Manipulator;

                IL.RoR2.CharacterBody.AddTimedBuff_BuffDef_float_int += CharacterBody_GetBuffCount_BuffDef_Manipulator;

                IL.RoR2.MushroomVoidBehavior.OnEnable += CharacterBody_GetBuffCount_BuffDef_Manipulator;

                IL.RoR2.PrimarySkillShurikenBehavior.FixedUpdate += CharacterBody_GetBuffCount_BuffDef_Manipulator;
                IL.RoR2.PrimarySkillShurikenBehavior.OnDisable += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.EntityStates.Commando.CommandoWeapon.CastSmokescreen.OnExit += CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.EntityStates.Commando.CommandoWeapon.CastSmokescreenNoDelay.OnExit += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.EntityStates.VoidJailer.Capture.TargetIsTethered += CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.EntityStates.VoidJailer.Capture.UpdateTethers += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.EntityStates.VoidRaidCrab.FireFinalStand.OnEnter += CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.EntityStates.VoidRaidCrab.FireWardWipe.OnEnter += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.AffixVoidBehavior.FixedUpdate += CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.AffixVoidBehavior.OnDisable += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.AncientWispFireController.Update += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.BearVoidBehavior.FixedUpdate += CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.BearVoidBehavior.OnDisable += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.Items.ElementalRingsBehavior.FixedUpdate += CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.Items.ElementalRingsBehavior.OnDisable += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.ElementalRingVoidBehavior.FixedUpdate += CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.ElementalRingVoidBehavior.OnDisable += CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.Items.ImmuneToDebuffBehavior.FixedUpdate += CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.Items.ImmuneToDebuffBehavior.OnDisable += CharacterBody_GetBuffCount_BuffDef_CharacterBody_GetBuffCount_BuffDef_Manipulator;

                RoR2_Items_ShockNearbyBodyBehavior_OnDisable.Apply();

                RoR2_VoidSurvivorController_get_isCorrupted.Apply();
            }

            static void Cleanup()
            {
                IL.EntityStates.CaptainSupplyDrop.PlatingBuffMainState.GetInteractability -= CharacterBody_GetBuffCount_BuffIndex_Manipulator;
                IL.EntityStates.CaptainSupplyDrop.PlatingBuffMainState.OnInteractionBegin -= CharacterBody_GetBuffCount_BuffIndex_Manipulator;

                IL.RoR2.CharacterBody.AddTimedBuff_BuffDef_float_int -= CharacterBody_GetBuffCount_BuffDef_Manipulator;

                IL.RoR2.MushroomVoidBehavior.OnEnable -= CharacterBody_GetBuffCount_BuffDef_Manipulator;

                IL.RoR2.PrimarySkillShurikenBehavior.FixedUpdate -= CharacterBody_GetBuffCount_BuffDef_Manipulator;
                IL.RoR2.PrimarySkillShurikenBehavior.OnDisable -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.EntityStates.Commando.CommandoWeapon.CastSmokescreen.OnExit -= CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.EntityStates.Commando.CommandoWeapon.CastSmokescreenNoDelay.OnExit -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.EntityStates.VoidJailer.Capture.TargetIsTethered -= CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.EntityStates.VoidJailer.Capture.UpdateTethers -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.EntityStates.VoidRaidCrab.FireFinalStand.OnEnter -= CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.EntityStates.VoidRaidCrab.FireWardWipe.OnEnter -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.AffixVoidBehavior.FixedUpdate -= CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.AffixVoidBehavior.OnDisable -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.AncientWispFireController.Update -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.BearVoidBehavior.FixedUpdate -= CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.BearVoidBehavior.OnDisable -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.CharacterBody.AddTimedBuff_BuffDef_float -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.Items.ElementalRingsBehavior.FixedUpdate -= CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.Items.ElementalRingsBehavior.OnDisable -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.ElementalRingVoidBehavior.FixedUpdate -= CharacterBody_HasBuff_BuffDef_Manipulator;
                IL.RoR2.ElementalRingVoidBehavior.OnDisable -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.Items.ImmuneToDebuffBehavior.FixedUpdate -= CharacterBody_HasBuff_BuffDef_Manipulator;

                IL.RoR2.Items.ImmuneToDebuffBehavior.OnDisable -= CharacterBody_GetBuffCount_BuffDef_CharacterBody_GetBuffCount_BuffDef_Manipulator;

                RoR2_Items_ShockNearbyBodyBehavior_OnDisable.Undo();

                RoR2_VoidSurvivorController_get_isCorrupted.Undo();
            }
        }
    }
}
