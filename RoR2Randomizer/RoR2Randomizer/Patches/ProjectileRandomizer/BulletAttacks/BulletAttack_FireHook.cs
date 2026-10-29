using RoR2;
using RoR2Randomizer.RandomizerControllers.Projectile;
using UnityEngine;

namespace RoR2Randomizer.Patches.ProjectileRandomizer.BulletAttacks
{
    [PatchClass]
    static class BulletAttack_FireHook
    {
        static void Apply()
        {
            On.RoR2.BulletAttack.FireSingle += BulletAttack_FireSingle;
        }


        static void Cleanup()
        {
            On.RoR2.BulletAttack.FireSingle -= BulletAttack_FireSingle;
        }

        static void BulletAttack_FireSingle(On.RoR2.BulletAttack.orig_FireSingle orig, BulletAttack self, BulletAttack.FireSingleArgs args)
        {
            if (ProjectileRandomizerController.TryReplaceFire(self, args.ray.direction))
            {
#if DEBUG
                Log.Debug($"{nameof(args.ray.direction)}={args.ray.direction}");
#endif

                return;
            }

            orig(self, args);
        }
    }
}
