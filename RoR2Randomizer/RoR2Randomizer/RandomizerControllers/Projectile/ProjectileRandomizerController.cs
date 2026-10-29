using HarmonyLib;
using R2API.Networking;
using RoR2;
using RoR2.Artifacts;
using RoR2.Orbs;
using RoR2.Projectile;
using RoR2Randomizer.Configuration;
using RoR2Randomizer.Extensions;
using RoR2Randomizer.Networking.Generic;
using RoR2Randomizer.Networking.ProjectileRandomizer;
using RoR2Randomizer.RandomizerControllers.Projectile.BulletAttackHandling;
using RoR2Randomizer.RandomizerControllers.Projectile.Orbs.DamageOrbHandling;
using RoR2Randomizer.RandomizerControllers.Projectile.Orbs.LightningOrbHandling;
using RoR2Randomizer.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.Projectile
{
    [RandomizerController]
    public class ProjectileRandomizerController : BaseRandomizerController
    {
        static ProjectileRandomizerController _instance;
        public static ProjectileRandomizerController Instance => _instance;

        static int[] _projectileIndicesToRandomize;

        [SystemInitializer(typeof(ProjectileCatalog))]
        static void Init()
        {
            _projectileIndicesToRandomize =
                ProjectileCatalog.projectilePrefabProjectileControllerComponents
                                 .Where(projectile =>
                                 {
                                     if (!projectile)
                                         return false;

                                     if (projectile.TryGetComponent<ProjectileFireChildren>(out ProjectileFireChildren projectileFireChildren)
                                      && (!projectileFireChildren.childProjectilePrefab || projectileFireChildren.childProjectilePrefab == null))
                                     {
#if DEBUG                            
                                         Log.Debug($"Excluding {projectile.name} due to invalid {nameof(ProjectileFireChildren)} setup");
#endif

                                         return false;
                                     }

                                     switch (projectile.name)
                                     {
                                         case "AACannon": // Does nothing
                                         case "AncientWispCannon": // Does nothing
                                         case "BanditBomblets": // Does nothing
                                         case "BanditClusterBombSeed": // Clusterbombs fall through ground and do nothing
                                         case "BanditClusterGrenadeProjectile": // No collision, cannot deal damage
                                         case "BeetleQueenAcid": // Does nothing
                                         case "BellBallSmall": // Does nothing
                                         case "DroneRocket": // Does nothing
                                         case "EngiMineDeployer": // Constant NullRef in FixedUpdate
                                         case "EngiSeekerGrenadeProjectile": // Does nothing
                                         case "EngiWallShield": // Unfinished engi shield
                                         case "GatewayProjectile": // Does nothing
                                         case "MinorConstructOnKillProjectile": // Does nothing
                                         case "NullifierBombProjectile": // Does nothing
                                         case "PaladinBigRocket": // Does nothing
                                         case "RedAffixMissileProjectile": // Does nothing
                                         case "ScoutGrenade": // Does nothing
                                         case "Rocket": // Does nothing
                                         case "Spine": // No collision, cannot deal damage
                                         case "ToolbotDroneHeal": // Does nothing
                                         case "ToolbotDroneStun": // Does nothing
                                         case "TreebotPounderProjectile": // Does nothing
                                         case "TreebotFlower1": // Does nothing
                                         case "ChefCleaverProjectile": // Assumes owner has a ChefController
                                         case "ChefCleaver": // Same thing ig?
                                         // Excluded because I think it's more fun that way
                                         case "MageIcewallWalkerProjectile":
                                         case "MageFirewallWalkerProjectile":
                                         case "ThrownObjectProjectile":
                                         case "ThrownObjectProjectileNoStun":
                                         case "HaulerThrowProjectile":

                                         // Excluded because it seems like a huge pain getting it to work, might look into it in the future.
                                         case "LunarSunProjectile":
#if DEBUG                            
                                             Log.Debug($"Excluding {projectile.name} due to being in blacklist");
#endif                               
                                             return false;
                                     }

#if DEBUG
                                     if (projectile.TryGetComponent<ProjectileSpawnMaster>(out ProjectileSpawnMaster projectileSpawnMaster))
                                     {
                                         Log.Debug($"{projectile.name} spawns {projectileSpawnMaster} ({projectileSpawnMaster.gameObject})");
                                     }

                                     if (projectileFireChildren)
                                     {
                                         Log.Debug($"{projectile.name} is {nameof(ProjectileFireChildren)}");
                                     }
#endif

                                     return true;
                                 })
                                 .Select(p => p.catalogIndex)
                                 .ToArray();
        }

        static readonly RunSpecific<bool> _hasReceivedProjectileReplacementsFromServer = new RunSpecific<bool>();

        static IEnumerable<ProjectileTypeIdentifier> getAllProjectileIdentifiers()
        {
            IEnumerable<ProjectileTypeIdentifier> identifiers = _projectileIndicesToRandomize.Select(static i => new ProjectileTypeIdentifier(ProjectileType.OrdinaryProjectile, i));

            if (ConfigManager.ProjectileRandomizer.RandomizeHitscanAttacks)
            {
                identifiers = identifiers.Concat(BulletAttackCatalog.Instance.GetAllBulletAttackProjectileIdentifiers());
            }

            identifiers = identifiers.Concat(DamageOrbCatalog.Instance.GetAllDamageOrbProjectileIdentifiers());
            identifiers = identifiers.Concat(LightningOrbCatalog.Instance.GetAllLightningOrbProjectileIdentifiers());

            identifiers = identifiers.AddItem(ProjectileTypeIdentifier.SpiteBomb);

            // Ensure all identifiers are strictly unique to prevent duplicate key dictionary crashes on clients
            identifiers = identifiers.Distinct();

            if (ConfigManager.ProjectileRandomizer.ExcludeInstakillProjeciles)
            {
                identifiers = identifiers.Where(static i =>
                {
                    bool isInstaKill = i.IsInstaKill;
#if DEBUG
                    if (isInstaKill)
                    {
                        Log.Debug($"Excluding projectile identifier {i} from all identifiers due to instakill projectiles not allowed");
                    }
#endif
                    return !isInstaKill;
                });
            }

            return identifiers;
        }

        static readonly RunSpecific<ReplacementDictionary<ProjectileTypeIdentifier>> _projectileIndicesReplacements = new RunSpecific<ReplacementDictionary<ProjectileTypeIdentifier>>((out ReplacementDictionary<ProjectileTypeIdentifier> result) =>
        {
            if (shouldBeActive && Instance)
            {
                result = ReplacementDictionary<ProjectileTypeIdentifier>.CreateFrom(getAllProjectileIdentifiers(), Instance.RNG);
                return true;
            }

            result = default;
            return false;
        });

        static readonly RunSpecific<Dictionary<ProjectileTypeIdentifier, ProjectileTypeIdentifier>> _appendedProjectileReplacements = new RunSpecific<Dictionary<ProjectileTypeIdentifier, ProjectileTypeIdentifier>>();


        static readonly RunSpecific<bool> _shouldRandomizeHitscanServer = new RunSpecific<bool>((out bool result) =>
        {
            if (NetworkServer.active)
            {
                result = ConfigManager.ProjectileRandomizer.RandomizeHitscanAttacks;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        });

        static bool shouldBeActive => NetworkServer.active && ConfigManager.ProjectileRandomizer.Enabled;
        public static bool IsActive => (shouldBeActive || (NetworkClient.active && _hasReceivedProjectileReplacementsFromServer)) && _projectileIndicesReplacements.HasValue;

        public static bool ShouldRandomizeBulletAttacks => IsActive && _shouldRandomizeHitscanServer;

        public override bool IsRandomizerEnabled => IsActive;

        protected override bool isNetworked => true;

        static bool _replacingTempDisabled = false;


        protected override IEnumerable<NetworkMessageBase> getNetMessages()
        {
#if DEBUG
            Log.Debug($"Sending {nameof(SyncProjectileReplacements)} to clients");
#endif

            if (_projectileIndicesReplacements.HasValue)
            {
                yield return new SyncProjectileReplacements(_projectileIndicesReplacements, false);
            }

            if (_appendedProjectileReplacements.HasValue)
            {
                yield return new SyncProjectileReplacements(new ReplacementDictionary<ProjectileTypeIdentifier>(_appendedProjectileReplacements.Value), true);
            }
        }

        static void onProjectileReplacementsReceivedFromServer(ReplacementDictionary<ProjectileTypeIdentifier> replacements, bool isAppendedReplacements)
        {
            if (isAppendedReplacements)
            {
                _appendedProjectileReplacements.Value = new Dictionary<ProjectileTypeIdentifier, ProjectileTypeIdentifier>(replacements);
            }
            else
            {
                _projectileIndicesReplacements.Value = replacements;
                _hasReceivedProjectileReplacementsFromServer.Value = true;
            }

            if (!_shouldRandomizeHitscanServer)
            {
                _shouldRandomizeHitscanServer.Value |= replacements.Keys.Any(static i => i.Type == ProjectileType.Bullet);
            }
        }

        static void appendProjectileReplacement(in ProjectileTypeIdentifier identifier)
        {
            if (!NetworkServer.active)
                return;

            if (!_appendedProjectileReplacements.HasValue)
                _appendedProjectileReplacements.Value = new Dictionary<ProjectileTypeIdentifier, ProjectileTypeIdentifier>();

            if (!_appendedProjectileReplacements.Value.ContainsKey(identifier))
            {
                _appendedProjectileReplacements.Value.Add(identifier, getAllProjectileIdentifiers().GetRandomOrDefault(Instance.RNG));

                if (!NetworkServer.dontListen)
                {
                    // Ensure this replacement is sent to clients immediately!
                    new SyncProjectileReplacements(new ReplacementDictionary<ProjectileTypeIdentifier>(_appendedProjectileReplacements.Value), true).SendTo(NetworkDestination.Clients);
                }
            }
        }
        static void BulletAttackCatalog_OnIdentifierAppendedServer(in BulletAttackIdentifier identifier)
        {
            if (Run.instance && NetworkServer.active)
            {
                appendProjectileReplacement(identifier);
            }
        }

        static void DamageOrbCatalog_OnIdentifierAppended(in DamageOrbIdentifier identifier)
        {
            if (Run.instance && NetworkServer.active)
            {
                appendProjectileReplacement(identifier);
            }
        }

        static void LightningOrbCatalog_OnIdentifierAppended(in LightningOrbIdentifier identifier)
        {
            if (Run.instance && NetworkServer.active)
            {
                appendProjectileReplacement(identifier);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            SyncProjectileReplacements.OnReceive += onProjectileReplacementsReceivedFromServer;

            BulletAttackCatalog.Instance.OnIdentifierAppended += BulletAttackCatalog_OnIdentifierAppendedServer;
            DamageOrbCatalog.Instance.OnIdentifierAppended += DamageOrbCatalog_OnIdentifierAppended;
            LightningOrbCatalog.Instance.OnIdentifierAppended += LightningOrbCatalog_OnIdentifierAppended;

            SingletonHelper.Assign(ref _instance, this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            SyncProjectileReplacements.OnReceive -= onProjectileReplacementsReceivedFromServer;

            BulletAttackCatalog.Instance.OnIdentifierAppended -= BulletAttackCatalog_OnIdentifierAppendedServer;
            DamageOrbCatalog.Instance.OnIdentifierAppended -= DamageOrbCatalog_OnIdentifierAppended;
            LightningOrbCatalog.Instance.OnIdentifierAppended -= LightningOrbCatalog_OnIdentifierAppended;

            _projectileIndicesReplacements.Dispose();
            _appendedProjectileReplacements.Dispose();
            _shouldRandomizeHitscanServer.Dispose();
            _hasReceivedProjectileReplacementsFromServer.Dispose();

            SingletonHelper.Unassign(ref _instance, this);
        }

        public static bool TryReplaceProjectileInstantiateFire(ref GameObject projectilePrefab, out GameObject originalPrefab, Vector3 origin, Quaternion rotation, float damage, float force, bool isCrit, GenericFireProjectileArgs genericArgs)
        {
            originalPrefab = projectilePrefab;

            if (TryGetOverrideProjectileIdentifier(ProjectileTypeIdentifier.FromProjectilePrefab(projectilePrefab), out ProjectileTypeIdentifier replacement))
            {
                switch (replacement.Type)
                {
                    case ProjectileType.OrdinaryProjectile:
                        projectilePrefab = ProjectileCatalog.GetProjectilePrefab(replacement.Index);
                        return true;
                    default:
                        _replacingTempDisabled = true;
                        replacement.Fire(origin, rotation, damage, force, isCrit, genericArgs);
                        _replacingTempDisabled = false;
                        return false;
                }
            }

            return true;
        }

        public static bool TryReplaceFire(Orb orb)
        {
            if (!IsActive)
                return false;

            ProjectileTypeIdentifier identifier = ProjectileTypeIdentifier.Invalid;

            float damage = 0f;
            float force = 0f;
            bool isCrit = false;

            GenericFireProjectileArgs genericArgs = new GenericFireProjectileArgs
            {
                Target = orb.target
            };

            bool tryReplaceOrb()
            {
                Vector3 origin = orb.origin;

                Quaternion rotation;
                if (orb.target)
                {
                    rotation = Util.QuaternionSafeLookRotation((orb.target.transform.position - orb.origin).normalized);
                }
                else
                {
                    rotation = Quaternion.identity;
                }

                return identifier.IsValid && TryReplaceFire(identifier, orb.origin, rotation, damage, force, isCrit, genericArgs);
            }

            if (orb is GenericDamageOrb damageOrb)
            {
                damage = damageOrb.damageValue;
                force = damageOrb is SquidOrb squidOrb ? squidOrb.forceScalar : 0f;
                isCrit = damageOrb.isCrit;

                DamageOrbIdentifier damageOrbIdentifier = DamageOrbCatalog.Instance.GetIdentifier(damageOrb);
                if (damageOrbIdentifier.IsValid)
                {
                    genericArgs.Owner = damageOrb.attacker;
                    genericArgs.DamageType = damageOrb.damageType;

                    identifier = damageOrbIdentifier;
                    return tryReplaceOrb();
                }
            }
            else if (orb is LightningOrb lightningOrb)
            {
                damage = lightningOrb.damageValue;
                force = 0f;
                isCrit = lightningOrb.isCrit;

                LightningOrbIdentifier lightningOrbIdentifier = LightningOrbCatalog.Instance.GetIdentifier(lightningOrb);
                if (lightningOrbIdentifier.IsValid)
                {
                    genericArgs.Owner = lightningOrb.attacker;
                    genericArgs.DamageType = lightningOrb.damageType;
                    genericArgs.Weapon = lightningOrb.inflictor;

                    identifier = lightningOrbIdentifier;
                    return tryReplaceOrb();
                }
            }

#if DEBUG
            Log.Debug($"unhandled Orb type {orb?.GetType()?.FullName ?? "null"}");
#endif
            return false;
        }

        public static bool TryReplaceFire(BulletAttack bulletAttack, Vector3 fireDirection)
        {
            BulletAttackIdentifier identifier = BulletAttackCatalog.Instance.GetIdentifier(bulletAttack);
            return identifier.IsValid && TryReplaceFire(identifier, bulletAttack.origin, Util.QuaternionSafeLookRotation(fireDirection), bulletAttack.damage, bulletAttack.force, bulletAttack.isCrit, new GenericFireProjectileArgs(bulletAttack));
        }

        public static bool TryReplaceFire(in FireProjectileInfo info, GameObject weapon)
        {
            if (!IsActive)
                return false;

            HurtBox targetHurtBox = null;
            if (info.target)
            {
                if (info.target.TryGetComponent<HurtBox>(out HurtBox hurtBox))
                {
                    targetHurtBox = hurtBox;
                }
                else if (info.target.TryGetComponent<HurtBoxGroup>(out HurtBoxGroup hurtBoxGroup))
                {
                    if (hurtBoxGroup.mainHurtBox)
                    {
                        targetHurtBox = hurtBoxGroup.mainHurtBox;
                    }
                    else
                    {
                        targetHurtBox = hurtBoxGroup.hurtBoxes.GetRandomOrDefault(RoR2Application.rng);
                    }
                }
                else
                {
                    if (!info.target.TryGetComponent<CharacterBody>(out CharacterBody body))
                    {
                        if (info.target.TryGetComponent<CharacterMaster>(out CharacterMaster master))
                        {
                            body = master.GetBody();
                        }
                    }

                    if (body)
                    {
                        targetHurtBox = body.mainHurtBox;
                    }
                }

                if (!targetHurtBox)
                {
                    Log.Warning($"projectile has target GO ({info.target.name}), but no HurtBox reference could be found");
                }
            }

            return TryReplaceFire(ProjectileTypeIdentifier.FromProjectilePrefab(info.projectilePrefab), info.position, info.rotation, info.damage, info.force, info.crit, new GenericFireProjectileArgs
            {
                Owner = info.owner,
                Weapon = weapon,
                DamageType = info.damageTypeOverride,
                Target = targetHurtBox
            });
        }

        public static bool TryReplaceFire(in BombArtifactManager.BombRequest spiteBombRequest)
        {
            if (!IsActive)
                return false;

            const float DEVIATION = 5f;
            Quaternion rotation = Util.QuaternionSafeLookRotation(Quaternion.Euler(UnityEngine.Random.Range(-DEVIATION, DEVIATION),
                                                                                   UnityEngine.Random.Range(0, 360f),
                                                                                   UnityEngine.Random.Range(-DEVIATION, DEVIATION)) * Vector3.up);

            return TryReplaceFire(ProjectileTypeIdentifier.SpiteBomb, spiteBombRequest.spawnPosition + new Vector3(0f, 1f, 0f), rotation, spiteBombRequest.bombBaseDamage, 0f, false, new GenericFireProjectileArgs
            {
                Owner = spiteBombRequest.attacker
            });
        }

        public static bool TryReplaceFire(in ProjectileTypeIdentifier identifier, Vector3 origin, Quaternion rotation, float damage, float force, bool isCrit, GenericFireProjectileArgs genericArgs)
        {
            if (!IsActive || _replacingTempDisabled)
                return false;

            if (identifier.Type == ProjectileType.Invalid)
                return false;

            if (identifier.Type == ProjectileType.Bullet && !ShouldRandomizeBulletAttacks)
                return false;

            if (TryGetOverrideProjectileIdentifier(identifier, out ProjectileTypeIdentifier replacement) && replacement.IsValid)
            {
                _replacingTempDisabled = true;
                replacement.Fire(origin, rotation, damage, force, isCrit, genericArgs);
                _replacingTempDisabled = false;

                if (genericArgs.Owner && identifier.Type == ProjectileType.OrdinaryProjectile && replacement.Type != ProjectileType.OrdinaryProjectile)
                {
                    GameObject originalProjectilePrefab = ProjectileCatalog.GetProjectilePrefab(identifier.Index);

                    if (originalProjectilePrefab.GetComponent<ProjectileGrappleController>())
                    {
                        const string STATE_MACHINE_NAME = "Hook";

                        EntityStateMachine hookStateMachine = EntityStateMachine.FindByCustomName(genericArgs.Owner, STATE_MACHINE_NAME);
                        if (hookStateMachine)
                        {
                            if (hookStateMachine.state is EntityStates.Loader.FireHook fireHook)
                            {
                                // Force Hook to retract next update
                                //fireHook.hadHookInstance = true; Commented out
                            }
                        }
                        else
                        {
                            Log.Warning($"Tried to initialize grapple {replacement.Type}, but owner has no '{STATE_MACHINE_NAME}' state machine");
                        }
                    }
                }

                return true;
            }

            return false;
        }

        public static bool TryGetOriginalProjectilePrefab(GameObject replacementPrefab, out GameObject originalPrefab)
        {
            if (IsActive)
            {
                int projectileIndex = ProjectileCatalog.GetProjectileIndex(replacementPrefab);
                if (TryGetOriginalProjectileIdentifier(new ProjectileTypeIdentifier(ProjectileType.OrdinaryProjectile, projectileIndex), out ProjectileTypeIdentifier original))
                {
                    if (original.Type == ProjectileType.OrdinaryProjectile)
                    {
                        originalPrefab = ProjectileCatalog.GetProjectilePrefab(original.Index);
                        return (bool)originalPrefab;
                    }
                }
            }

            originalPrefab = default;
            return false;
        }

        public static bool TryGetOriginalProjectileIdentifier(in ProjectileTypeIdentifier replacement, out ProjectileTypeIdentifier original)
        {
            if (IsActive)
            {
                return _projectileIndicesReplacements.Value.TryGetOriginal(replacement, out original);
            }

            original = default;
            return false;
        }

        public static bool TryGetOverrideProjectileIdentifier(in ProjectileTypeIdentifier original, out ProjectileTypeIdentifier replacement)
        {
            if (IsActive)
            {
#if DEBUG
        ProjectileTypeIdentifier debugId = ConfigManager.ProjectileRandomizer.DebugProjectileIdentifier;
        Log.Debug($"[OverrideCheck] original={original}, DebugProjectileIdentifier={debugId}, IsValid={debugId.IsValid}");
#endif
                if (
#if DEBUG
            (replacement = ConfigManager.ProjectileRandomizer.DebugProjectileIdentifier).IsValid ||
#endif
                    _projectileIndicesReplacements.Value.TryGetReplacement(original, out replacement) ||
                    (_appendedProjectileReplacements.HasValue && _appendedProjectileReplacements.Value.TryGetValue(original, out replacement)))
                {
#if DEBUG
                    Log.Debug($"[OverrideCheck] RESOLVED original={original} -> replacement={replacement}");
#endif
                    return true;
                }
            }

            replacement = default;
            return false;
        }
    }
}
