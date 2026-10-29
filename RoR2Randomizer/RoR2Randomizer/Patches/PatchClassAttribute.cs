using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModdingUtility;

namespace RoR2Randomizer.Patches
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    sealed class PatchClassAttribute : HG.Reflection.SearchableAttribute
    {
        readonly struct PatchClassInfo
        {
            readonly Type _type;
            readonly MethodInfo _applyMethod;
            readonly MethodInfo _cleanupMethod;

            public PatchClassInfo(Type type)
            {
                _type = type;

                const string APPLY_METHOD_NAME = "Apply";
                const string ALT_APPLY_METHOD_NAME = "ApplyPatches";

                const string CLEANUP_METHOD_NAME = "Cleanup";
                const string ALT_CLEANUP_METHOD_NAME = "CleanupPatches";

                const BindingFlags PATCH_METHOD_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                _applyMethod = type.GetMethod(APPLY_METHOD_NAME, PATCH_METHOD_FLAGS) ?? type.GetMethod(ALT_APPLY_METHOD_NAME, PATCH_METHOD_FLAGS);
                _cleanupMethod = type.GetMethod(CLEANUP_METHOD_NAME, PATCH_METHOD_FLAGS) ?? type.GetMethod(ALT_CLEANUP_METHOD_NAME, PATCH_METHOD_FLAGS);

                if (_applyMethod == null)
                    Log.Error_NoCallerPrefix($"Patch class {type.FullName} does not have a {APPLY_METHOD_NAME} or {ALT_APPLY_METHOD_NAME} method");

                if (_cleanupMethod == null)
                    Log.Error_NoCallerPrefix($"Patch class {type.FullName} does not have a {CLEANUP_METHOD_NAME} or {ALT_CLEANUP_METHOD_NAME} method");
            }

            public void Apply()
            {
                if (_applyMethod != null)
                {
                    _applyMethod.Invoke(null, Array.Empty<object>());

#if DEBUG
                    Log.Debug_NoCallerPrefix($"Applied patch class '{_applyMethod.DeclaringType.FullName}'");
#endif
                }
            }

            public void Cleanup()
            {
                if (_cleanupMethod != null)
                {
                    _cleanupMethod.Invoke(null, Array.Empty<object>());

#if DEBUG
                    Log.Debug_NoCallerPrefix($"Cleaned up patch class '{_cleanupMethod.DeclaringType.FullName}'");
#endif
                }
            }

            public override string ToString()
            {
                return $"{_type.FullName} (Patch)";
            }

            public static readonly IEqualityComparer<PatchClassInfo> Comparer = new EqualityComparer();

            class EqualityComparer : IEqualityComparer<PatchClassInfo>
            {
                public bool Equals(PatchClassInfo x, PatchClassInfo y)
                {
                    return x._type.Equals(y._type);
                }

                public int GetHashCode(PatchClassInfo obj)
                {
                    return obj._type.GetHashCode();
                }
            }
        }

        static readonly InitializeOnAccess<PatchClassInfo[]> _patchClasses = new InitializeOnAccess<PatchClassInfo[]>(() =>
        {
            return (from attr in GetInstances<PatchClassAttribute>() ?? Enumerable.Empty<HG.Reflection.SearchableAttribute>()
                    select new PatchClassInfo((Type)attr.target)).Distinct(PatchClassInfo.Comparer).ToArray();
        });

        public static void ApplyAllPatches()
        {
            var instances = GetInstances<PatchClassAttribute>();
#if DEBUG
            Log.Debug($"Found {instances?.Count() ?? 0} patch class attributes");
#endif
            foreach (PatchClassInfo patchClass in _patchClasses.Get)
            {
                try
                {
                    patchClass.Apply();
                }
                catch (Exception e)
                {
                    Log.Error($"Exception while applying {patchClass}: {e}");
                }
            }

#if DEBUG
            Log.Debug($"Applied {_patchClasses.Get.Length} patch classes");
#endif
        }

        public static void CleanupAllPatches()
        {
            foreach (PatchClassInfo patchClass in _patchClasses.Get)
            {
                try
                {
                    patchClass.Cleanup();
                }
                catch (Exception e)
                {
                    Log.Error($"Exception while cleaning up {patchClass}: {e}");
                }
            }

#if DEBUG
            Log.Debug($"Cleaned up {_patchClasses.Get.Length} patch classes");
#endif
        }
    }
}
