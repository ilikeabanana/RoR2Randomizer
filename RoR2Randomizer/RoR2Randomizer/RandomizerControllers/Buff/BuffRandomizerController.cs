using RoR2;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.Buff
{
    [RandomizerController]
    public class BuffRandomizerController : BaseRandomizerController
    {
        static BuffRandomizerController _instance;
        public static BuffRandomizerController Instance => _instance;

        static BuffDef[] _validBuffDefs;

        static BuffIndex[] _invincibilityBuffs;
        static BuffIndex[] _eliteBuffsOrdered;

        static Dictionary<BuffIndex, DotController.DotIndex> _buffToDotIndex;

        [SystemInitializer(typeof(BuffCatalog), typeof(DotController))]
        static void Init()
        {
            _invincibilityBuffs = new BuffIndex[]
            {
                BuffCatalog.FindBuffIndex("bdBodyArmor"),
                BuffCatalog.FindBuffIndex("bdGoldEmpowered"),
                BuffCatalog.FindBuffIndex("bdHiddenInvincibility"),
                BuffCatalog.FindBuffIndex("bdImmune"),
                BuffCatalog.FindBuffIndex("bdIntangible"),
                BuffCatalog.FindBuffIndex("bdBearVoidReady")
            }.OrderBy(i => i).ToArray();

            _buffToDotIndex = new Dictionary<BuffIndex, DotController.DotIndex>();
            for (int i = 0; i < DotController.dotDefs.Length; i++)
            {
                DotController.DotDef dotDef = DotController.dotDefs[i];
                if (dotDef == null)
                    continue;

                BuffDef buffDef = dotDef.associatedBuff;
                if (!buffDef)
                    continue;

                BuffIndex buffIndex = buffDef.buffIndex;
                if (_buffToDotIndex.ContainsKey(buffIndex))
                    continue;
                
                _buffToDotIndex.Add(buffIndex, (DotController.DotIndex)i);
            }

            _validBuffDefs = BuffCatalog.buffDefs.Where(b => b && b.buffIndex != BuffIndex.None && !b.isHidden).ToArray();

            _eliteBuffsOrdered = BuffCatalog.eliteBuffIndices.Where(bi => _validBuffDefs.Any(bd => bd.buffIndex == bi)).OrderBy(i => i).ToArray();
        }

        static bool shouldBeActive => NetworkServer.active && ConfigManager.BuffRandomizer.Enabled;
        public override bool IsRandomizerEnabled => shouldBeActive && _buffReplacements.HasValue;

        protected override bool isNetworked => false;

        public static bool IsActive => _instance && _instance.IsRandomizerEnabled;

        static readonly RunSpecific<IndexReplacementsCollection> _buffReplacements = new RunSpecific<IndexReplacementsCollection>((out IndexReplacementsCollection result) =>
        {
            if (shouldBeActive && Instance)
            {
                IEnumerable<BuffDef> buffsToRandomize = _validBuffDefs.Where(b =>
                {
                    if (ConfigManager.BuffRandomizer.IsBlacklisted(b.buffIndex))
                    {
#if DEBUG
                        Log.Debug($"Excluding buff {b} due to: Config blacklist");
#endif
                        return false;
                    }

                    if (ConfigManager.BuffRandomizer.ExcludeInvincibility && Array.BinarySearch(_invincibilityBuffs, b.buffIndex) >= 0)
                    {
#if DEBUG
                        Log.Debug($"Excluding buff {b} due to: Invincibility buff randomization disabled");
#endif
                        return false;
                    }

                    if (ConfigManager.BuffRandomizer.ExcludeEliteBuffs && Array.BinarySearch(_eliteBuffsOrdered, b.buffIndex) >= 0)
                    {
#if DEBUG
                        Log.Debug($"Excluding buff {b} due to: Elite buff randomization disabled");
#endif
                        return false;
                    }

                    return true;
                });
                
                ReplacementDictionary<BuffIndex> dict = ReplacementDictionary<BuffIndex>.CreateFrom<BuffDef>(buffsToRandomize, Instance.RNG, b => b.buffIndex, static (key, value) =>
                {
                    if ((!ConfigManager.BuffRandomizer.MixBuffsAndDebuffs || ConfigManager.BuffRandomizer.SwapBuffDebuffWeightMult == 0f) && key.isDebuff != value.isDebuff)
                    {
#if DEBUG
                        Log.Debug($"Not allowing replacement {toLogString(key)}->{toLogString(value)}, mixing buffs and debuffs is not enabled.");
#endif

                        return false;
                    }

                    return true;
                }, static (key, value) =>
                {
                    float weight = 1f;

                    if (key.isDebuff != value.isDebuff)
                    {
                        weight *= ConfigManager.BuffRandomizer.SwapBuffDebuffWeightMult;
                    }

                    return weight;
                });

                result = IndexReplacementsCollection.Create(dict, BuffCatalog.buffCount);

#if DEBUG
                foreach (KeyValuePair<BuffIndex, BuffIndex> pair in dict)
                {
                    Log.Debug($"Replaced buff: {toLogString(pair.Key)} -> {toLogString(pair.Value)}");
                }
#endif

                return true;
            }

            result = default;
            return false;
        });

#if DEBUG
        static string toLogString(BuffIndex buff)
        {
            return toLogString(BuffCatalog.GetBuffDef(buff));
        }

        static string toLogString(BuffDef buff)
        {
            if (!buff || buff == null)
                return "null";

            return $"{buff.name} ({(int)buff.buffIndex})";
        }
#endif

#if DEBUG
        static BuffIndex _debugIndex;

        void Update()
        {
            if (ConfigManager.BuffRandomizer.Enabled && ConfigManager.BuffRandomizer.BuffDebugMode == DebugMode.Manual)
            {
                bool changed;
                if (Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    if (--_debugIndex < 0)
                        _debugIndex = (BuffIndex)(BuffCatalog.buffCount - 1);

                    changed = true;
                }
                else if (Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    if (++_debugIndex >= (BuffIndex)BuffCatalog.buffCount)
                        _debugIndex = 0;

                    changed = true;
                }
                else
                {
                    changed = false;
                }

                if (changed)
                {
                    Log.Debug($"debug buff {toLogString(_debugIndex)}");
                }
            }
        }

        static bool tryGetDebugOverrideReplacement(BuffIndex original, out BuffIndex replacement)
        {
            if (_buffReplacements.Value.HasReplacement(original))
            {
                switch (ConfigManager.BuffRandomizer.BuffDebugMode.Entry.Value)
                {
                    case DebugMode.Manual:
                        replacement = _debugIndex;
                        return true;
                    case DebugMode.Forced:
                        replacement = BuffCatalog.FindBuffIndex(ConfigManager.BuffRandomizer.ForcedBuffName);
                        return replacement != BuffIndex.None;
                }
            }

            replacement = BuffIndex.None;
            return false;
        }
#endif

        protected override void Awake()
        {
            base.Awake();

            SingletonHelper.Assign(ref _instance, this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _buffReplacements.Dispose();

            SingletonHelper.Unassign(ref _instance, this);
        }

#if DEBUG
        public static uint SuppressBuffReplacementLogCount = 0;
#endif

        public static bool TryReplaceBuffIndex(ref BuffIndex index)
        {
            if (TryGetReplacementBuffIndex(index, out BuffIndex replacement))
            {
                index = replacement;
                return true;
            }

            return false;
        }

        public static bool TryGetReplacementBuffIndex(BuffIndex original, out BuffIndex replacement)
        {
            if (original != BuffIndex.None && IsActive)
            {
                if (
#if DEBUG
                    tryGetDebugOverrideReplacement(original, out replacement) ||
#endif
                    _buffReplacements.Value.TryGetReplacement(original, out replacement))
                {
#if DEBUG
                    if (SuppressBuffReplacementLogCount == 0)
                    {
                        Log.Debug($"Replaced {toLogString(original)} -> {toLogString(replacement)}");
                    }
#endif

                    return replacement != BuffIndex.None;
                }
            }

            replacement = BuffIndex.None;
            return false;
        }

        public static bool TryReplaceBuffIndex(ref int index)
        {
            BuffIndex buffIndex = (BuffIndex)index;
            bool result = TryReplaceBuffIndex(ref buffIndex);
            index = (int)buffIndex;

            return result;
        }

        public static bool TryGetDotIndex(BuffIndex buff, out DotController.DotIndex dot)
        {
            return _buffToDotIndex.TryGetValue(buff, out dot);
        }

        public static bool TryGetBuffIndex(DotController.DotIndex dot, out BuffIndex buff)
        {
            DotController.DotDef dotDef = DotController.GetDotDef(dot);
            if (dotDef != null)
            {
                BuffDef buffDef = dotDef.associatedBuff;
                if (buffDef)
                {
                    buff = buffDef.buffIndex;
                    return true;
                }
            }

            buff = BuffIndex.None;
            return false;
        }
    }
}
