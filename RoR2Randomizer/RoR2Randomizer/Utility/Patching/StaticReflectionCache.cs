using EntityStates;
using HarmonyLib;
using RoR2;
using System;
using System.Reflection;
using UnityEngine;

namespace RoR2Randomizer.Utility.Patching
{
    public static class StaticReflectionCache
    {
        public static readonly MethodInfo Animator_GetFloat_name_MI = SymbolExtensions.GetMethodInfo<Animator>(_ => _.GetFloat(default(string)));

        public static readonly MethodInfo Physics_Raycast_Ray_outRaycastHit_float_MI = SymbolExtensions.GetMethodInfo(() => Physics.Raycast(default, out Discard<RaycastHit>.Value, default));

        public static readonly MethodInfo EntityState_get_skillLocator = AccessTools.PropertyGetter(typeof(EntityState), nameof(EntityState.skillLocator));

        public static readonly MethodInfo EntityStateCatalog_InstantiateState_Type_MI = SymbolExtensions.GetMethodInfo(() => EntityStateCatalog.InstantiateState(default(Type)));
        public static readonly MethodInfo EntityStateCatalog_InstantiateState_SerializableEntityStateType_MI = SymbolExtensions.GetMethodInfo(() => EntityStateCatalog.InstantiateState(default(EntityStateIndex)));
    }
}
