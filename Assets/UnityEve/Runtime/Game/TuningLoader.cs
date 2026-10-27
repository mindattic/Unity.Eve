using UnityEngine;
using Unity.Eve.Core.Battle;

namespace Unity.Eve.Game
{
    /// <summary>
    /// Applies the battle tuning file (Resources/UnityEve/battle-tuning.json) to <see cref="BattleConfig"/>
    /// before the first scene loads. A file that fails validation is not applied at all: the built-in values
    /// stay and every reason is logged, so a bad edit is loud but never leaves the battle half-tuned.
    /// </summary>
    public static class TuningLoader
    {
        public const string ResourcePath = "UnityEve/battle-tuning";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStart() => Load();

        /// <returns>True when the file was found and applied.</returns>
        public static bool Load()
        {
            var file = Resources.Load<TextAsset>(ResourcePath);
            if (file == null)
            {
                Debug.LogWarning($"[Unity.Eve] no battle tuning at Resources/{ResourcePath}.json; using built-in values");
                return false;
            }
            var errors = BattleConfig.Apply(file.text);
            Resources.UnloadAsset(file);
            if (errors.Count == 0) return true;
            Debug.LogWarning($"[Unity.Eve] battle tuning rejected, using built-in values:\n  {string.Join("\n  ", errors)}");
            return false;
        }
    }
}
