using RoR2Randomizer.Configuration;
using RoR2Randomizer.RandomizerControllers.Boss.BossReplacementInfo;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.RandomizerControllers.Boss
{
    [RandomizerController]
    public partial class BossRandomizerController : BaseRandomizerController
    {
        public static bool IsReplacedBossCharacter(GameObject masterObject)
        {
            return masterObject && masterObject.GetComponent<BaseBossReplacement>();
        }

        public override bool IsRandomizerEnabled => NetworkServer.active && (ConfigManager.BossRandomizer.Enabled || CharacterReplacements.IsAnyForcedCharacterModeEnabled);

        protected override bool isNetworked => false;

        static BossRandomizerController _instance;

        protected override void Awake()
        {
            base.Awake();

            SingletonHelper.Assign(ref _instance, this);

            Mithrix.Initialize();
            FalseSon.Initialize();
            SolusWing.Initialize();
            Voidling.Initialize();
            Aurelionite.Initialize();
            LunarScav.Initialize();
            AlloyWorshipUnit.Initialize();
            HoldoutBoss.Initialize();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Mithrix.Uninitialize();
            FalseSon.Uninitialize();
            Voidling.Uninitialize();
            SolusWing.Uninitialize();
            Aurelionite.Uninitialize();
            LunarScav.Uninitialize();
            AlloyWorshipUnit.Uninitialize();
            HoldoutBoss.Uninitialize();

            SingletonHelper.Unassign(ref _instance, this);
        }
    }
}
