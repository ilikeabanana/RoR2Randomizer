using HG;
using R2API.Networking;
using RoR2;
using RoR2.Orbs;
using RoR2Randomizer.Networking;
using RoR2Randomizer.Networking.Generic;
using RoR2Randomizer.Networking.ProjectileRandomizer.Orbs.Lightning;
using RoR2Randomizer.Utility.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.Projectile.Orbs.LightningOrbHandling
{
    public class LightningOrbCatalog : GenericNetworkedCatalog<LightningOrb, LightningOrbIdentifier>
    {
        public static readonly LightningOrbCatalog Instance = new LightningOrbCatalog();

        static LightningOrbCatalog()
        {
            SyncLightningOrbCatalog.OnReceive += Instance.SyncCatalog_OnReceive;
            SyncLightningOrbIndexNeeded.OnReceive += Instance.SyncIndexNeeded_OnReceive;
        }

        [SystemInitializer(typeof(EntityStateCatalog))]
        static void Init()
        {
            static void initIdentifier(LightningOrb.LightningType lightningType, uint defaultNumBounces, uint targetsToFindPerBounce)
            {
                LightningOrbIdentifier identifier = new LightningOrbIdentifier(lightningType, defaultNumBounces, targetsToFindPerBounce);

#if DEBUG
                const bool IS_DEBUG = true;
#else
                const bool IS_DEBUG = false;
#endif
                Instance.appendIdentifier(ref identifier, IS_DEBUG);
            }

            for (LightningOrb.LightningType i = 0; i < LightningOrb.LightningType.Count; i++)
            {
                break;
                initIdentifier(i, i switch
                {
                    LightningOrb.LightningType.Ukulele => 2U,
                    LightningOrb.LightningType.Tesla => 2U,
                    LightningOrb.LightningType.HuntressGlaive => (uint)EntityStates.Croco.Disease.maxBounces,
                    LightningOrb.LightningType.Loader => 3U,
                    LightningOrb.LightningType.CrocoDisease => (uint)EntityStates.Huntress.HuntressWeapon.ThrowGlaive.maxBounceCount,
                    _ => 0U
                },
                i switch
                {
                    LightningOrb.LightningType.CrocoDisease => 2U,
                    _ => 1U
                });
            }
        }

        protected override LightningOrbIdentifier InvalidIdentifier => LightningOrbIdentifier.Invalid;

        protected override bool tryCreateIdentifierForObject(LightningOrb lightningOrb, out LightningOrbIdentifier identifier)
        {
            identifier = new LightningOrbIdentifier(lightningOrb);
            return true;
        }

        protected override NetworkMessageBase getSyncIdentifierNeededMessage(in LightningOrbIdentifier identifier)
        {
            return new SyncLightningOrbIndexNeeded(identifier);
        }

        public override IEnumerable<NetworkMessageBase> GetNetMessages()
        {
            yield return new SyncLightningOrbCatalog(_identifiers, _identifiersCount);
        }

        public IEnumerable<ProjectileTypeIdentifier> GetAllLightningOrbProjectileIdentifiers()
        {
            return this.Select<LightningOrbIdentifier, ProjectileTypeIdentifier>(i => i);
        }
    }
}
