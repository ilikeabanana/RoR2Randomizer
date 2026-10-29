using BepInEx.Configuration;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoR2Randomizer.Configuration.ConfigValue.ParsedList
{
    public class ParsedSceneIndexListConfigValue : ParsedListConfigValue<SceneIndex>
    {
        public ParsedSceneIndexListConfigValue(ConfigEntry<string> entry) : base(entry)
        {
            if (!SceneCatalog.availability.available)
            {
                SceneCatalog.availability.CallWhenAvailable(() => tryParseToList());
                //SceneCatalog.availability.onAvailable += tryParseToList;
            }
        }

        public static SceneIndex FindSceneIndexCaseInsensitive(string sceneName)
        {
            return SceneCatalog.allSceneDefs.FirstOrDefault(sd => string.Equals(sd.cachedName, sceneName, StringComparison.OrdinalIgnoreCase))?.sceneDefIndex ?? SceneIndex.Invalid;
        }

        protected override IEnumerable<SceneIndex> parse(string[] values)
        {
            if (!SceneCatalog.availability.available)
                return Enumerable.Empty<SceneIndex>();

            return values.Select(sceneName =>
            {
                sceneName = sceneName.Trim();

                SceneIndex sceneIndex = FindSceneIndexCaseInsensitive(sceneName);

                if (sceneIndex == SceneIndex.Invalid)
                {
                    Log.Warning($"Could not find scene index with name \"{sceneName}\"");
                }

                return sceneIndex;
            }).Where(i => i != SceneIndex.Invalid)
              .Distinct()
              .OrderBy(i => i);
        }
    }
}
