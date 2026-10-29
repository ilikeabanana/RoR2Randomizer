using RoR2;
using RoR2Randomizer.Utility.Catalog;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.Projectile.BulletAttackHandling
{
    public struct BulletAttackIdentifier : ICatalogIdentifier<BulletAttack, BulletAttackIdentifier>
    {
        public static readonly BulletAttackIdentifier Invalid = new BulletAttackIdentifier(default, default, default, default) { Index = -1 };

        public readonly EffectIndex TracerEffectIndex;
        public readonly EffectIndex HitEffectIndex;

        public readonly DamageType DamageType;

        public readonly BulletAttackFlags Flags;

        public int Index { readonly get; set; }

        public readonly bool IsValid => Index != -1;

        public BulletAttackIdentifier(BulletAttack bulletAttack, BulletAttackFlags flags) : this(EffectCatalog.FindEffectIndexFromPrefab(bulletAttack.tracerEffectPrefab), EffectCatalog.FindEffectIndexFromPrefab(bulletAttack.hitEffectPrefab), bulletAttack.damageType, flags)
        {
        }

        public BulletAttackIdentifier(EffectIndex tracerEffectIndex, EffectIndex hitEffectIndex, DamageType damageType, BulletAttackFlags flags)
        {
            TracerEffectIndex = tracerEffectIndex;
            HitEffectIndex = hitEffectIndex;
            DamageType = damageType;
            Flags = flags;
        }

        public BulletAttackIdentifier(NetworkReader reader)
        {
            TracerEffectIndex = reader.ReadEffectIndex();
            HitEffectIndex = reader.ReadEffectIndex();
            DamageType = (DamageType)reader.ReadPackedUInt32();  // was: reader.ReadDamageType()
            Flags = (BulletAttackFlags)reader.ReadPackedUInt32();
            Index = (int)reader.ReadPackedUInt32();
        }

        public readonly bool Matches(BulletAttack bulletAttack)
        {
            return bulletAttack != null && EffectCatalog.FindEffectIndexFromPrefab(bulletAttack.tracerEffectPrefab) == TracerEffectIndex && EffectCatalog.FindEffectIndexFromPrefab(bulletAttack.hitEffectPrefab) == HitEffectIndex && bulletAttack.damageType == DamageType;
        }

        public readonly bool Equals(in BulletAttackIdentifier other, bool compareIndex)
        {
            return compareIndex ? Index == other.Index : TracerEffectIndex == other.TracerEffectIndex &&
                                                         HitEffectIndex == other.HitEffectIndex &&
                                                         DamageType == other.DamageType &&
                                                         Flags == other.Flags;
        }

        public readonly BulletAttack CreateInstance()
        {
            BulletAttack bulletAttack = new BulletAttack();
            bulletAttack.sniper = (Flags & BulletAttackFlags.Sniper) != 0;
            bulletAttack.damageType |= DamageType;
            return bulletAttack;
        }

        public readonly void Serialize(NetworkWriter writer)
        {
            writer.WriteEffectIndex(TracerEffectIndex);
            writer.WriteEffectIndex(HitEffectIndex);
            writer.WritePackedUInt32((uint)DamageType);      // was: writer.Write((int)DamageType)
            writer.WritePackedUInt32((uint)Flags);
            writer.WritePackedUInt32((uint)Index);
        }

        public override readonly string ToString()
        {
            return $"{nameof(Index)}={Index}, {nameof(TracerEffectIndex)}={TracerEffectIndex}, {nameof(HitEffectIndex)}={HitEffectIndex}, {nameof(DamageType)}={DamageType}, {nameof(Flags)}={Flags}";
        }

        public static implicit operator ProjectileTypeIdentifier(in BulletAttackIdentifier bulletAttack)
        {
            return new ProjectileTypeIdentifier(ProjectileType.Bullet, bulletAttack.Index);
        }
    }
}
