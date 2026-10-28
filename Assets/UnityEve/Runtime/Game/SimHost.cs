using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Eve.Core.Sim;

namespace Unity.Eve.Game
{
    /// <summary>
    /// Hands every MonoBehaviour in a scene the same <see cref="GameAuthority"/>, whichever of them wakes first
    /// (the player's body, the battle director, the session that ticks it). A scene loaded fresh gets a fresh one.
    /// </summary>
    public static class SimHost
    {
        private static GameAuthority _current;
        private static Scene _scene;

        public static GameAuthority For(Scene scene)
        {
            if (_current == null || _scene != scene)
            {
                _current = new GameAuthority { World = UnityWorld.Instance };
                _scene = scene;
            }
            return _current;
        }

        /// <summary>The scene that owned <paramref name="authority"/> is going: the next scene starts clean.</summary>
        public static void Release(GameAuthority authority)
        {
            if (authority == null || authority != _current) return;
            _current = null;
            _scene = default;
        }

        /// <summary>The scene's authority, if one was made.</summary>
        public static GameAuthority Current => _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            _scene = default;
        }
    }
}
