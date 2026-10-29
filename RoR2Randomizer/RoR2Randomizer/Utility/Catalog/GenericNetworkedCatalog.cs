using HG;
using R2API.Networking;
using RoR2Randomizer.Networking;
using RoR2Randomizer.Networking.Generic;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace RoR2Randomizer.Utility.Catalog
{
    public abstract class GenericNetworkedCatalog<TObject, TIdentifier> : GenericCatalog<TObject, TIdentifier>, INetMessageProvider where TIdentifier : ICatalogIdentifier<TObject, TIdentifier>
    {
        protected GenericNetworkedCatalog() : base()
        {
            NetworkingManager.RegisterMessageProvider(this, MessageProviderFlags.Persistent);
        }

        protected abstract bool tryCreateIdentifierForObject(TObject obj, out TIdentifier identifier);

        public override TIdentifier GetIdentifier(TObject obj)
        {
            TIdentifier identifier = base.GetIdentifier(obj);
            if (!identifier.IsValid)
            {
                if (!tryCreateIdentifierForObject(obj, out identifier))
                    return InvalidIdentifier;

                if (NetworkServer.active)
                {
                    appendIdentifier(ref identifier, false);

#if DEBUG
                    Log.Debug(LOG_PREFIX_TYPE + $"Created {typeof(TIdentifier).Name} '{identifier}'");
#endif

                    if (!NetworkServer.dontListen)
                    {
                        this.TrySendAll(NetworkDestination.Clients);
                    }
                }
                else
                {
                    if (NetworkClient.active)
                    {
                        NetworkMessageBase syncIdentifierNeededMessage = getSyncIdentifierNeededMessage(identifier);
                        if (syncIdentifierNeededMessage != null)
                        {
                            syncIdentifierNeededMessage.SendTo(NetworkDestination.Server);
                        }
                        else
                        {
                            Log.Warning(LOG_PREFIX_TYPE + $"catalog is networked, but {nameof(syncIdentifierNeededMessage)} is null");
                        }
                    }

                    return InvalidIdentifier;
                }
            }

            return identifier;
        }

        protected abstract NetworkMessageBase getSyncIdentifierNeededMessage(in TIdentifier identifier);

        public virtual bool SendMessages => true;

        public abstract IEnumerable<NetworkMessageBase> GetNetMessages();

        protected void SyncCatalog_OnReceive(TIdentifier[] identifiers, int identifiersCount)
        {
            if (!NetworkServer.active && NetworkClient.active)
            {
#if DEBUG
                Log.Debug(LOG_PREFIX_TYPE + $"from server");
#endif

                _identifiersCount = identifiersCount;

                ArrayUtils.EnsureCapacity(ref _identifiers, identifiersCount);
                Array.Copy(identifiers, _identifiers, identifiersCount);
            }
        }

        protected void SyncIndexNeeded_OnReceive(TIdentifier required)
        {
            if (NetworkServer.active)
            {
#if DEBUG
                Log.Debug(LOG_PREFIX_TYPE + $"from client with identifier: {required}");
#endif

                bool foundExisting = false;
                for (int i = 0; i < _identifiersCount; i++)
                {
                    if (_identifiers[i].Equals(required, false))
                    {
                        required.Index = _identifiers[i].Index;
                        foundExisting = true;
                        break;
                    }
                }

                if (!foundExisting)
                {
                    appendIdentifier(ref required, true);
                }

                // Immediately broadcast the updated catalog payload back to all clients so indices match universally
                foreach (NetworkMessageBase msg in GetNetMessages())
                {
                    msg.SendTo(NetworkDestination.Clients);
                }
            }
        }
    }
}