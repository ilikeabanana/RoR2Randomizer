using RoR2;
using RoR2Randomizer.RandomizerControllers.Buff;

namespace RoR2Randomizer.Patches.BuffRandomizer
{
    [PatchClass]
    static class DotRandomizerPatch
    {
        public static uint SkipApplyBuffCount = 0;

        static void Apply()
        {
            On.RoR2.DotController.InflictDot_refInflictDotInfo += DotController_InflictDot_refInflictDotInfo;

            On.RoR2.DotController.OnDotStackRemovedServer += DotController_OnDotStackRemovedServer;
        }

        static void DotController_OnDotStackRemovedServer(On.RoR2.DotController.orig_OnDotStackRemovedServer orig, DotController self, DotController.DotStack dotStack)
        {
            BuffIndexPatch.SkipPatchCount++;

            orig(self, dotStack);

            BuffIndexPatch.SkipPatchCount--;
        }

        static void Cleanup()
        {
            On.RoR2.DotController.InflictDot_refInflictDotInfo -= DotController_InflictDot_refInflictDotInfo;

            On.RoR2.DotController.OnDotStackRemovedServer -= DotController_OnDotStackRemovedServer;
        }

        static void DotController_InflictDot_refInflictDotInfo(On.RoR2.DotController.orig_InflictDot_refInflictDotInfo orig, ref InflictDotInfo inflictDotInfo)
        {
            if (BuffRandomizerController.IsActive &&
                SkipApplyBuffCount == 0 &&
                BuffRandomizerController.TryGetBuffIndex(inflictDotInfo.dotIndex, out BuffIndex buff) &&
                BuffRandomizerController.TryReplaceBuffIndex(ref buff))
            {
                if (BuffRandomizerController.TryGetDotIndex(buff, out DotController.DotIndex overrideDot))
                {
                    inflictDotInfo.dotIndex = overrideDot;
                }
                else
                {
                    if (inflictDotInfo.victimObject && inflictDotInfo.victimObject.TryGetComponent<HealthComponent>(out HealthComponent healthComponent))
                    {
                        if (healthComponent.alive)
                        {
                            if (healthComponent.body)
                            {
                                // This is not an ideal way of doing things, but fuck it, I just want it to work
                                BuffIndexPatch.SkipPatchCount++;
                                GetBuffIndex_BuffIndex_ReplacePatch.ForceDisable = true;

                                float buffDuration = inflictDotInfo.duration;
                                if (buffDuration <= 0f)
                                {
                                    // TODO: Calculate this value according to how DotController.AddDot would
                                    buffDuration = 8f;
                                }

#if DEBUG
                                Log.Debug($"Replacing dot {inflictDotInfo.dotIndex} with timed buff {BuffCatalog.GetBuffDef(buff)?.name ?? "null"} for {buffDuration} seconds");
#endif
                                healthComponent.body.AddTimedBuff(buff, buffDuration);
                                GetBuffIndex_BuffIndex_ReplacePatch.ForceDisable = false;
                                BuffIndexPatch.SkipPatchCount--;
                            }
                            else
                            {
                                Log.Warning("victim object has no body ({inflictDotInfo.victimObject})");
                            }
                        }
                    }
                    else
                    {
                        Log.Warning("victim object has no health component ({inflictDotInfo.victimObject})");
                    }

                    return;
                }
            }

            BuffIndexPatch.SkipPatchCount++;
            orig(ref inflictDotInfo);
            BuffIndexPatch.SkipPatchCount--;
        }
    }
}
