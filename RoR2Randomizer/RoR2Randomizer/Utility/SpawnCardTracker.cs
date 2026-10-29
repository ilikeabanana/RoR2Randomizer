using RoR2;
using System;

namespace RoR2Randomizer.Utility
{
    public static class SpawnCardTracker
    {
        public static SpawnCard MithrixNormalSpawnCard;
        public static SpawnCard MithrixHurtSpawnCard;
        public static SpawnCard[] MithrixPhase2SpawnCards;

        public static SpawnCard[] VoidlingPhasesSpawnCards;
        public static SpawnCard[] FalseSonPhasesSpawnCards;

        public static SpawnCard AurelioniteSpawnCard;

        public static SpawnCard SolusWingSpawnCard;

        public static MultiCharacterSpawnCard LunarScavSpawnCard;

        public static SpawnCard AlloyWorshipUnitSpawnCard;

        static bool isPartOf(SpawnCard[] array, SpawnCard card, out int index)
        {
            if (array != null)
            {
                index = Array.IndexOf(array, card);
                return index != -1;
            }

            index = -1;
            return false;
        }

        public static bool IsPartOfMithrixPhase2(SpawnCard card)
        {
            return isPartOf(MithrixPhase2SpawnCards, card, out _);
        }

        public static bool IsAnyVoidlingPhase(SpawnCard card)
        {
            return isPartOf(VoidlingPhasesSpawnCards, card, out _);
        }

        public static bool IsAnyFalseSonPhase(SpawnCard card)
        {
            return card.name.Contains("FalseSon");
        }
    }
}
