using HG;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RoR2Randomizer.Utility.Catalog
{
    public abstract class GenericCatalog<TObjects, TIdentifier> : IEnumerable<TIdentifier> where TIdentifier : ICatalogIdentifier<TObjects, TIdentifier>
    {
        protected static readonly string LOG_PREFIX_TYPE = $"({nameof(TObjects)}={typeof(TObjects).Name}, {nameof(TIdentifier)}={typeof(TIdentifier).Name}) ";

        public delegate void IdentifierAppendedDelegate(in TIdentifier identifier);

        public event IdentifierAppendedDelegate OnIdentifierAppended;

        protected int _identifiersCount = 0;
        protected TIdentifier[] _identifiers = new TIdentifier[20];

        protected virtual TIdentifier InvalidIdentifier => default;

        protected GenericCatalog()
        {
        }

        public TIdentifier GetIdentifier(int index)
        {
            if (index < 0 || index >= _identifiersCount)
                return InvalidIdentifier;

            return _identifiers[index];
        }

        protected void appendIdentifier(ref TIdentifier identifier, bool checkExisting)
        {
            if (checkExisting)
            {
                for (int i = 0; i < _identifiersCount; i++)
                {
                    if (_identifiers[i].Equals(identifier, false))
                    {
#if DEBUG
                        Log.Warning(LOG_PREFIX_TYPE + $"duplicate attack identifier {identifier}");
#endif

                        return;
                    }
                }
            }

            identifier.Index = _identifiersCount;

#if DEBUG
            Log.Debug(LOG_PREFIX_TYPE + $"appended {identifier}");
#endif

            ArrayUtils.ArrayAppend(ref _identifiers, ref _identifiersCount, identifier);

            OnIdentifierAppended?.Invoke(identifier);
        }

        protected bool tryGetAttackIdentifier(TObjects obj, out TIdentifier identifier)
        {
            foreach (TIdentifier existingIdentifier in _identifiers.Take(_identifiersCount))
            {
                bool isMatch = existingIdentifier.Matches(obj);
#if DEBUG
                Log.Debug(LOG_PREFIX_TYPE + $"[MatchCheck] candidate={existingIdentifier}, isMatch={isMatch}");
#endif
                if (isMatch)
                {
                    identifier = existingIdentifier;
#if DEBUG
                    Log.Debug(LOG_PREFIX_TYPE + $"[MatchCheck] MATCHED obj against {existingIdentifier}");
#endif
                    return true;
                }
            }

            identifier = default;
            return false;
        }

        public virtual TIdentifier GetIdentifier(TObjects obj)
        {
            if (!tryGetAttackIdentifier(obj, out TIdentifier identifier))
            {
                return InvalidIdentifier;
            }

            return identifier;
        }

        public IEnumerator<TIdentifier> GetEnumerator()
        {
            return _identifiers.Take(_identifiersCount).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
