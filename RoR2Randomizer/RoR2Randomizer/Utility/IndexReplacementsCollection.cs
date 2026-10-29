using HG;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace RoR2Randomizer.Utility
{
    public readonly struct IndexReplacementsCollection : IEnumerable<int>, IEquatable<IndexReplacementsCollection>
    {
        public readonly int Length;

        readonly int[] _replacementIndices;
        readonly int[] _originalIndices;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IndexReplacementsCollection Create<T>(ReplacementDictionary<T> input, int length) where T : Enum
        {
            return new IndexReplacementsCollection(new ReplacementDictionary<int>(input.ToDictionary(kvp => (int)(object)kvp.Key, kvp => (int)(object)kvp.Value)), length);
        }

        public IndexReplacementsCollection(ReplacementDictionary<int> input, int length)
        {
            Length = length;

            _replacementIndices = new int[length];
            ArrayUtils.SetAll(_replacementIndices, -1);

            _originalIndices = new int[length];
            ArrayUtils.SetAll(_originalIndices, -1);

            for (int i = 0; i < length; i++)
            {
                if (input.TryGetReplacement(i, out int replacement))
                {
                    _replacementIndices[i] = replacement;
                    _originalIndices[replacement] = i;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IndexReplacementsCollection Create<T>(T[] replacementIndices) where T : Enum
        {
            return new IndexReplacementsCollection(Array.ConvertAll(replacementIndices, t => (int)(object)t));
        }

        public IndexReplacementsCollection(int[] replacementIndices, int? length = null)
        {
            Length = length ?? replacementIndices.Length;
            _replacementIndices = replacementIndices;

            _originalIndices = new int[Length];
            ArrayUtils.SetAll(_originalIndices, -1);

            for (int i = 0; i < Length; i++)
            {
                int replacementIndex = _replacementIndices[i];
                if (replacementIndex != -1)
                {
                    _originalIndices[replacementIndex] = i;
                }
            }
        }

        public readonly bool HasReplacement(int original)
        {
            return TryGetReplacement(original, out _);
        }
        public readonly bool TryGetReplacement(int original, out int replacement)
        {
            if (original < 0 || original >= Length)
            {
                replacement = -1;
                return false;
            }

            return (replacement = _replacementIndices[original]) != -1;
        }

        public readonly bool HasOriginal(int replacement)
        {
            return TryGetOriginal(replacement, out _);
        }
        public readonly bool TryGetOriginal(int replacement, out int original)
        {
            if (replacement < 0 || replacement >= Length)
            {
                original = -1;
                return false;
            }

            return (original = _originalIndices[replacement]) != -1;
        }

        public readonly void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)Length);
            foreach (int index in _replacementIndices)
            {
                writer.WritePackedIndex32(index);
            }
        }

        public static IndexReplacementsCollection Deserialize(NetworkReader reader)
        {
            uint length = reader.ReadPackedUInt32();
            int[] replacementIndices = new int[length];
            for (int i = 0; i < length; i++)
            {
                replacementIndices[i] = reader.ReadPackedIndex32();
            }

            return new IndexReplacementsCollection(replacementIndices);
        }

        public readonly IEnumerator<int> GetEnumerator()
        {
            return ((IEnumerable<int>)_replacementIndices).GetEnumerator();
        }

        readonly IEnumerator IEnumerable.GetEnumerator()
        {
            return _replacementIndices.GetEnumerator();
        }

        public bool Equals(IndexReplacementsCollection other)
        {
            // Yes, the arrays are being compared by reference, cry about it
            return Length == other.Length && _replacementIndices == other._replacementIndices;
        }
    }
}