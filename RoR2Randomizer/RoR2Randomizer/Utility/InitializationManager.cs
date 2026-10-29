using HarmonyLib;
using RoR2Randomizer.Patches;
using RoR2Randomizer.RandomizerControllers;
using RoR2Randomizer.RandomizerControllers.Projectile.Orbs;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace RoR2Randomizer.Utility
{
    public static class InitializationManager
    {
        static Harmony _harmonyInstance;

        static GameObject _controllersRoot;

        public static void Init()
        {
            Main.Instance.StartCoroutine(DelayedInitialize());
        }

        public static IEnumerator DelayedInitialize()
        {
            yield return new WaitForSeconds(1); // One Second delay

            _harmonyInstance = new Harmony(Main.PluginGUID);
            _harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

            PatchClassAttribute.ApplyAllPatches();

            _controllersRoot = new GameObject(Main.PluginName + ".Controllers");
            UnityEngine.Object.DontDestroyOnLoad(_controllersRoot);
            _controllersRoot.hideFlags = HideFlags.DontSaveInEditor;

            var instances = RandomizerControllerAttribute.GetInstances<RandomizerControllerAttribute>();
            if (instances != null)
            {
                foreach (HG.Reflection.SearchableAttribute attr in instances)
                {
                    if (attr != null && attr.target is Type type)
                    {
                        _controllersRoot.AddComponent(type);
#if DEBUG
                        Log.Debug($"Added randomizer controller {type.Name} to main object");
#endif
                    }
                }
            }
#if DEBUG
            else
            {
                Log.Debug("GetInstances<RandomizerControllerAttribute> returned null — assembly scan hasn't registered this type yet.");
            }
#endif

            OrbTargetDummyObjectMarker.InitNetworkPrefab();
        }

        public static void Cleanup()
        {
            UnityEngine.Object.Destroy(_controllersRoot);
            _controllersRoot = null;

            _harmonyInstance.UnpatchSelf();
            _harmonyInstance = null;

            PatchClassAttribute.CleanupAllPatches();
        }
    }
}
