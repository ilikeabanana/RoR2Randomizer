using HG;
using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.Networking;
using RoR2Randomizer.Networking.CharacterReplacements;
using RoR2Randomizer.Networking.Generic;
using RoR2Randomizer.Patches.ExplicitSpawnRandomizer;
using RoR2Randomizer.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers
{
#if DEBUG
    public enum DebugMode : byte
    {
        None,
        Manual,
        Forced
    }
#endif

    public class CharacterReplacements : INetMessageProvider
    {
        public static event Action OnCharacterReplacementsInitialized;

        static CharacterReplacements _instance;

#if DEBUG
        public static DebugMode DebugMode => ConfigManager.Debug.CharacterDebugMode;
#endif

        public static ReadOnlyArray<EquipmentIndex> AvailableDroneEquipments;

        static int[] _masterIndicesToRandomize;

        static bool _hasInvokedReplacementsInitialized = false;
        static ulong _invokeReplacementsInitializedRunCallbackHandle;

        public static bool ShouldRandomizeMaster(CharacterMaster masterPrefab)
        {
            if (!masterPrefab)
                return false;

            if (!masterPrefab.bodyPrefab)
            {
#if DEBUG
                Log.Debug($"excluding master {masterPrefab.name}: no {nameof(CharacterMaster.bodyPrefab)}");
#endif

                return false;
            }

            if (!masterPrefab.bodyPrefab.TryGetComponent<CharacterBody>(out CharacterBody body))
            {
#if DEBUG
                Log.Debug($"excluding master {masterPrefab.name}: no {nameof(CharacterMaster.bodyPrefab)} {nameof(CharacterBody)} component");
#endif

                return false;
            }

            Transform modelTransform;
            if (!body.TryGetComponent<ModelLocator>(out ModelLocator modelLocator) || !(modelTransform = modelLocator.modelTransform))
            {
#if DEBUG
                Log.Debug($"excluding master {masterPrefab.name}: no model");
#endif

                return false;
            }

            if (modelTransform.childCount == 0)
            {
#if DEBUG
                Log.Debug($"excluding master {masterPrefab.name}: empty model");
#endif

                return false;
            }

            switch (masterPrefab.name)
            {
                case "AncientWispMaster": // Does nothing
                case "ArtifactShellMaster": // No model, does not attack, cannot be damaged
                case "ClaymanMaster": // No hitboxes
                case "EngiBeamTurretMaster": // Seems to ignore the player
                case "MinorConstructAttachableMaster": // Instantly dies
                case "VoidRaidCrabJointMaster": // Balls
                case "SolusHeartMaster": // ... Yeah i don't know what's wrong with this one it just breaks thats all
                case "SolusHeartMaster_Offering": // Can't be used
                case "SolusVendorMaster": // Can't be killed
                case "ExhaustPortWeakpointMaster": // annoying
                case "VoidRaidCrabMaster": // Beta voidling, half invisible
#if DEBUG
                    Log.Debug($"excluding master {masterPrefab.name}: blacklist");
#endif
                    return false;
            }

            return true;
        }

        [SystemInitializer(typeof(EquipmentCatalog), typeof(MasterCatalog))]
        static void Init()
        {
            AvailableDroneEquipments = EquipmentCatalog.equipmentDefs.Where(eq => eq && (eq.canDrop ||
                                                                                            eq.passiveBuffDef ||
                                                                                            eq == RoR2Content.Equipment.QuestVolatileBattery ||
                                                                                            eq == DLC1Content.Equipment.BossHunterConsumed ||
                                                                                            eq == DLC1Content.Equipment.LunarPortalOnUse)
                                                                                         && eq != DLC1Content.Equipment.EliteVoidEquipment)
                                                                     .Select(e => e.equipmentIndex)
                                                                     .OrderBy(static ei => ei)
                                                                     .ToArray();

#if DEBUG
            foreach (EquipmentIndex equipmentIndex in AvailableDroneEquipments)
            {
                Log.Debug($"{Language.GetString(EquipmentCatalog.GetEquipmentDef(equipmentIndex).nameToken)} is in {nameof(AvailableDroneEquipments)}");
            }
#endif

            _masterIndicesToRandomize = MasterCatalog.masterPrefabs.Where(master =>
            {
                if (!master)
                {
#if DEBUG
                    Log.Debug("excluding null master");
#endif

                    return false;
                }

                if (!master.TryGetComponent<CharacterMaster>(out CharacterMaster masterComponent))
                {
#if DEBUG
                    Log.Debug($"excluding master {master.name}: no {nameof(CharacterMaster)} component");
#endif

                    return false;
                }

                return ShouldRandomizeMaster(masterComponent);
            }).Distinct().Select(go => (int)MasterCatalog.FindMasterIndex(go)).ToArray();

            _instance = new CharacterReplacements();
            NetworkingManager.RegisterMessageProvider(_instance, MessageProviderFlags.Persistent);

            SyncCharacterMasterReplacements.OnReceive += onMasterReplacementsReceivedFromServer;
            SyncCharacterMasterReplacementMode.OnReceive += onMasterReplacementModeReceivedFromServer;

            _invokeReplacementsInitializedRunCallbackHandle = RunSpecificCallbacksManager.AddEntry(static _ =>
            {
                if (IsEnabled)
                {
                    tryInvokeCharacterReplacementsInitialized();
                }
            }, static _ =>
            {
                _hasInvokedReplacementsInitialized = false;
            }, 0);

#if DEBUG
            RoR2Application.onFixedUpdate += Update;
#endif
        }

        static readonly RunSpecific<Xoroshiro128Plus> _rng = new RunSpecific<Xoroshiro128Plus>((out Xoroshiro128Plus result) =>
        {
            if (RNGManager.RandomizerServerRNG.HasValue)
            {
                result = new Xoroshiro128Plus(RNGManager.RandomizerServerRNG.Value.Next());
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }, 9);

        static readonly RunSpecific<bool> _hasReceivedMasterIndexReplacementsFromServer = new RunSpecific<bool>(1);
        static readonly RunSpecific<IndexReplacementsCollection> _masterIndexReplacements = new RunSpecific<IndexReplacementsCollection>((out IndexReplacementsCollection result) =>
        {
            if (NetworkServer.active && _replacementMode.Value == CharacterReplacementMode.Random)
            {
                IEnumerable<int> masterIndicesToRandomize = _masterIndicesToRandomize.Where(i =>
                {
                    if (ConfigManager.ExplicitSpawnRandomizer.IsBlacklisted((MasterCatalog.MasterIndex)i))
                    {
#if DEBUG
                        Log.Debug($"Excluding master {MasterCatalog.GetMasterPrefab((MasterCatalog.MasterIndex)i)?.name ?? "null"} due to: config blacklist");
#endif
                        return false;
                    }

                    return true;
                });

                result = new IndexReplacementsCollection(ReplacementDictionary<int>.CreateFrom(masterIndicesToRandomize, _rng, (key, value) =>
                {
                    if (Caches.Masters.Heretic.isValid && key == Caches.Masters.Heretic.i)
                    {
                        GameObject valuePrefab = MasterCatalog.GetMasterPrefab((MasterCatalog.MasterIndex)value);
                        if (valuePrefab.TryGetComponent<CharacterMaster>(out CharacterMaster master) && master.bodyPrefab && master.bodyPrefab.TryGetComponent<CharacterBody>(out CharacterBody body))
                        {
                            if (body.baseMoveSpeed <= 0f)
                            {
#if DEBUG
                                Log.Debug($"Not allowing replacement {MasterCatalog.GetMasterPrefab(Caches.Masters.Heretic)?.name} -> {valuePrefab.name}: Heretic replacements must be mobile");
#endif

                                return false;
                            }
                        }
                    }

                    return true;
                }), MasterCatalog.masterPrefabs.Length);
                
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }, 1);

        static readonly RunSpecific<CharacterReplacementMode> _replacementMode = new RunSpecific<CharacterReplacementMode>((out CharacterReplacementMode result) =>
        {
            if (NetworkServer.active)
            {
                if (ConfigManager.Fun.GupModeActive)
                {
                    result = CharacterReplacementMode.Gup;
                }
                else
                {
                    result = CharacterReplacementMode.Random;
                }

                return true;
            }

            result = CharacterReplacementMode.None;
            return false;
        }, 2, CharacterReplacementMode.None);

        public static bool IsEnabled => NetworkServer.active || (NetworkClient.active && (_hasReceivedMasterIndexReplacementsFromServer || _replacementMode.HasValue));

        public static bool IsAnyForcedCharacterModeEnabled
        {
            get
            {
                return
#if DEBUG
                       ConfigManager.Debug.CharacterDebugMode.Entry.Value > DebugMode.None ||
#endif
                       (_replacementMode.HasValue && _replacementMode.Value >= CharacterReplacementMode.Gup);
            }
        }

        public bool SendMessages => _masterIndexReplacements.HasValue || IsAnyForcedCharacterModeEnabled;

        public IEnumerable<NetworkMessageBase> GetNetMessages()
        {
            if (_replacementMode.HasValue)
            {
#if DEBUG
                Log.Debug($"Sending {nameof(SyncCharacterMasterReplacementMode)} to clients");
#endif
                yield return new SyncCharacterMasterReplacementMode(_replacementMode);
            }

            if (!_replacementMode.HasValue || _replacementMode.Value == CharacterReplacementMode.Random)
            {
#if DEBUG
                Log.Debug($"Sending {nameof(SyncCharacterMasterReplacements)} to clients");
#endif
                yield return new SyncCharacterMasterReplacements(_masterIndexReplacements);
            }
        }

        static void onMasterReplacementsReceivedFromServer(in IndexReplacementsCollection masterIndexReplacements)
        {
#if DEBUG
            Log.Debug("Received master index replacements from server");
#endif

            _masterIndexReplacements.Value = masterIndexReplacements;
            _hasReceivedMasterIndexReplacementsFromServer.Value = true;

            tryInvokeCharacterReplacementsInitialized();
        }

        static void onMasterReplacementModeReceivedFromServer(CharacterReplacementMode mode)
        {
#if DEBUG
            Log.Debug($"Received master replacement mode '{mode}' from server");
#endif

            _replacementMode.Value = mode;

            if (IsAnyForcedCharacterModeEnabled)
            {
                tryInvokeCharacterReplacementsInitialized();
            }
        }

        static void tryInvokeCharacterReplacementsInitialized()
        {
            if (!_hasInvokedReplacementsInitialized)
            {
                OnCharacterReplacementsInitialized?.Invoke();
                _hasInvokedReplacementsInitialized = true;
            }
        }

        public static void Uninitialize()
        {
            _replacementMode.Dispose();

            _masterIndexReplacements.Dispose();
            _hasReceivedMasterIndexReplacementsFromServer.Dispose();

            SyncCharacterMasterReplacements.OnReceive -= onMasterReplacementsReceivedFromServer;
            SyncCharacterMasterReplacementMode.OnReceive -= onMasterReplacementModeReceivedFromServer;

            RunSpecificCallbacksManager.RemoveEntry(_invokeReplacementsInitializedRunCallbackHandle);

#if DEBUG
            RoR2Application.onFixedUpdate -= Update;
#endif

            _instance = null;
        }

#if DEBUG
        static int _forcedMasterIndex;
        static void Update()
        {
            if (DebugMode == DebugMode.Manual)
            {
                bool changed = false;
                if (Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    if (++_forcedMasterIndex >= _masterIndicesToRandomize.Length)
                        _forcedMasterIndex = 0;

                    changed = true;
                }
                else if (Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    if (--_forcedMasterIndex < 0)
                        _forcedMasterIndex = _masterIndicesToRandomize.Length - 1;

                    changed = true;
                }

                if (changed)
                {
                    Log.Debug($"Character master: {MasterCatalog.GetMasterPrefab((MasterCatalog.MasterIndex)_masterIndicesToRandomize[_forcedMasterIndex])?.name} ({_forcedMasterIndex})");
                }
            }
        }
#endif

        public static GameObject GetReplacementMasterPrefab(string originalMasterName)
        {
            MasterCatalog.MasterIndex replacementIndex = GetReplacementForMasterIndex(MasterCatalog.FindMasterIndex(originalMasterName));
            if (replacementIndex.isValid)
            {
                GameObject replacement = MasterCatalog.GetMasterPrefab(replacementIndex);

#if DEBUG
                Log.Debug($"{nameof(CharacterReplacements)}: Replaced {originalMasterName} with {replacement}");
#endif

                return replacement;
            }
            else
            {
                return null;
            }
        }

        public static bool TryReplaceMasterPrefab(ref GameObject prefab)
        {
            GameObject replacement = GetReplacementMasterPrefab(prefab.name);
            if (replacement)
            {
                prefab = replacement;
                return true;
            }
            else
            {
                return false;
            }
        }

        static MasterCatalog.MasterIndex getMasterIndexForBody(BodyIndex bodyIndex)
        {
            if (bodyIndex != BodyIndex.None)
            {
                CharacterMaster master = MasterCatalog.allMasters.FirstOrDefault(m => m && m.bodyPrefab && m.bodyPrefab.TryGetComponent<CharacterBody>(out CharacterBody body) && body.bodyIndex == bodyIndex);
                if (master)
                {
                    return master.masterIndex;
                }
            }

            return MasterCatalog.MasterIndex.none;
        }

        static BodyIndex getBodyIndexForMaster(MasterCatalog.MasterIndex index)
        {
            if (index.isValid)
            {
                GameObject prefab = MasterCatalog.GetMasterPrefab(index);
                if (prefab)
                {
                    if (prefab.TryGetComponent<CharacterMaster>(out CharacterMaster master))
                    {
                        if (master.bodyPrefab && master.bodyPrefab.TryGetComponent<CharacterBody>(out CharacterBody body))
                        {
                            return body.bodyIndex;
                        }
                    }
                }
            }

            return BodyIndex.None;
        }

        public static BodyIndex GetReplacementBodyIndex(BodyIndex original)
        {
            if (IsEnabled)
            {
                return getBodyIndexForMaster(GetReplacementForMasterIndex(getMasterIndexForBody(original)));
            }

            return BodyIndex.None;
        }

        public static BodyIndex GetOriginalBodyIndex(BodyIndex replacement)
        {
            if (IsEnabled)
            {
                return getBodyIndexForMaster(GetOriginalMasterIndex(getMasterIndexForBody(replacement)));
            }

            return BodyIndex.None;
        }

        public static MasterCatalog.MasterIndex GetReplacementForMasterIndex(MasterCatalog.MasterIndex original)
        {
            if (NetworkServer.active && ConfigManager.ExplicitSpawnRandomizer.IsBlacklisted(original))
                return MasterCatalog.MasterIndex.none;

            if (original.isValid && IsEnabled)
            {
#if DEBUG
                if (NetworkServer.active)
                {
                    if (DebugMode == DebugMode.Manual)
                    {
                        int masterIndex = ArrayUtils.GetSafe(_masterIndicesToRandomize, _forcedMasterIndex, -1);
                        if (masterIndex != -1)
                        {
                            return (MasterCatalog.MasterIndex)masterIndex;
                        }
                    }

                    if (DebugMode == DebugMode.Forced)
                    {
                        return MasterCatalog.FindMasterIndex(ConfigManager.Debug.ForcedMasterName);
                    }
                }
#endif

                if (_replacementMode.HasValue)
                {
                    switch (_replacementMode.Value)
                    {
                        case CharacterReplacementMode.Gup:
                            if (!Caches.Masters.Gup.isValid)
                            {
                                Log.Error($"{nameof(CharacterReplacementMode.Gup)} mode enabled, but master index is invalid!");
                                return MasterCatalog.MasterIndex.none;
                            }

                            return Caches.Masters.Gup;
                    }
                }

                if (_masterIndexReplacements.HasValue && _masterIndexReplacements.Value.TryGetReplacement((int)original, out int replacementIndex))
                {
                    return (MasterCatalog.MasterIndex)replacementIndex;
                }
            }

            return MasterCatalog.MasterIndex.none;
        }

        public static MasterCatalog.MasterIndex GetOriginalMasterIndex(MasterCatalog.MasterIndex replacement)
        {
            if (replacement.isValid && IsEnabled)
            {
                if (_masterIndexReplacements.HasValue && _masterIndexReplacements.Value.TryGetOriginal((int)replacement, out int originalIndex))
                {
                    return (MasterCatalog.MasterIndex)originalIndex;
                }
            }

            return MasterCatalog.MasterIndex.none;
        }

        public static ILContext.Manipulator FixMasterIndexReferences(Func<bool> patchEnabled, params string[] names)
        {
            return il =>
            {
                ILCursor c = new ILCursor(il);

                MemberReference member = null;
                int patchCount = 0;
                while (c.TryGotoNext(x => x.MatchGetMemberValue(out member)))
                {
                    c.Index++;

                    if (member != null && Array.IndexOf(names, member.Name) != -1)
                    {
                        TypeReference valueType = null;
                        if (member is MethodReference method)
                        {
                            valueType = method.ReturnType;
                        }
                        else if (member is FieldReference field)
                        {
                            valueType = field.FieldType;
                        }
                        else
                        {
                            Log.Warning($"unimplemented member type of {member.GetType().FullName}");
                        }

                        if (valueType != null)
                        {
                            MasterCatalog.MasterIndex replacefunc(MasterCatalog.MasterIndex original)
                            {
                                if (patchEnabled == null || patchEnabled())
                                {
                                    MasterCatalog.MasterIndex replacement = GetReplacementForMasterIndex(original);
                                    if (replacement.isValid)
                                    {
                                        return replacement;
                                    }
                                }

                                return original;
                            }

                            if (valueType.Is(typeof(MasterCatalog.MasterIndex)))
                            {
                                c.EmitDelegate(replacefunc);

                                patchCount++;
                            }
                            else if (valueType.Is(typeof(int)))
                            {
                                c.EmitDelegate((int original) =>
                                {
                                    return (int)replacefunc((MasterCatalog.MasterIndex)original);
                                });

                                patchCount++;
                            }
                            else
                            {
                                Log.Warning($"unimplemented value type of {valueType.FullName}");
                            }
                        }
                    }
                }

                if (patchCount > 0)
                {
#if DEBUG
                    Log.Debug($"[{string.Join(", ", names)}] patched {patchCount} locations");
#endif
                }
                else
                {
                    Log.Warning($"[{string.Join(", ", names)}] patched 0 locations");
                }
            };
        }

        public delegate void OnDirectorSpawnReplacedCallback(GameObject result, MasterCatalog.MasterIndex originalMasterIndex);

        public static void TryReplaceDirectorSpawnRequest(DirectorSpawnRequest spawnRequest, OnDirectorSpawnReplacedCallback onDirectorSpawnReplaced)
        {
            if (spawnRequest == null)
            {
#if DEBUG
                Log.Debug($"Not replacing due to: null {nameof(spawnRequest)}");
#endif
                return;
            }

            SpawnCard spawnCard = spawnRequest.spawnCard;
            if (spawnCard == null)
            {
#if DEBUG
                Log.Debug($"Not replacing due to: null {nameof(spawnCard)}");
#endif
                return;
            }

            MasterCatalog.MasterIndex replacementMasterIndex = GetReplacementForMasterIndex(MasterCatalog.FindMasterIndex(spawnCard.prefab));
            if (!replacementMasterIndex.isValid)
            {
#if DEBUG
                Log.Debug("Not replacing due to: invalid replacement MasterIndex");
#endif
                return;
            }

            GameObject replacementMasterPrefab = MasterCatalog.GetMasterPrefab(replacementMasterIndex);
            if (!replacementMasterPrefab)
            {
#if DEBUG
                Log.Debug("Not replacing due to: invalid replacement prefab");
#endif
                return;
            }

            GameObject originalPrefab = spawnCard.prefab;
            spawnCard.prefab = replacementMasterPrefab;

#if DEBUG
            Log.Debug($"Override spawn request ({spawnCard}) prefab {originalPrefab?.name ?? "null"} -> {spawnCard?.prefab?.name ?? "null"}");
#endif

            void trySpawnObjectPostfix(ref GameObject result, DirectorSpawnRequest spawnRequest)
            {
                if (spawnRequest.spawnCard == spawnCard)
                {
#if DEBUG
                    Log.Debug($"Reset spawn request ({spawnCard}) prefab {spawnCard?.prefab?.name ?? "null"} -> {originalPrefab?.name ?? "null"} (success: {(bool)result})");
#endif

                    spawnCard.prefab = originalPrefab;

                    if (result)
                    {
                        if (originalPrefab && originalPrefab.TryGetComponent<CharacterMaster>(out CharacterMaster originalMasterPrefab))
                        {
                            onDirectorSpawnReplaced?.Invoke(result, originalMasterPrefab.masterIndex);
                        }
                    }

                    DirectorCore_TrySpawnObject.Postfix -= trySpawnObjectPostfix;
                }
            }

            DirectorCore_TrySpawnObject.Postfix += trySpawnObjectPostfix;
        }
    }
}
