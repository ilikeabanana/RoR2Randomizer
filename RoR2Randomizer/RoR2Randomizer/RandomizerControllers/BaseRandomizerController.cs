using RoR2;
using RoR2Randomizer.Networking;
using RoR2Randomizer.Networking.Generic;
using RoR2Randomizer.Utility;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers
{
    public abstract class BaseRandomizerController : MonoBehaviour, INetMessageProvider
    {
        public abstract bool IsRandomizerEnabled { get; }

        protected abstract bool isNetworked { get; }

        public readonly RunSpecific<Xoroshiro128Plus> RNG = new RunSpecific<Xoroshiro128Plus>((out Xoroshiro128Plus result) =>
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

        bool INetMessageProvider.SendMessages => IsRandomizerEnabled;

        protected virtual void Awake()
        {
            if (isNetworked)
            {
                NetworkingManager.RegisterMessageProvider(this, MessageProviderFlags.Persistent);
            }
        }

        protected virtual void OnDestroy()
        {


            if (isNetworked)
            {
                NetworkingManager.UnregisterMessageProvider(this);
            }

            RNG.Dispose();
        }

        IEnumerable<NetworkMessageBase> INetMessageProvider.GetNetMessages()
        {
            return getNetMessages();
        }
        protected virtual IEnumerable<NetworkMessageBase> getNetMessages()
        {
            yield break;
        }
    }
}
