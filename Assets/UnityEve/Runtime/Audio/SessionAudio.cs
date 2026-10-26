using Unity.Shared.Core;
using UnityEngine;

namespace Unity.Eve
{
    // How the CLI mute (-muted / MINDATTIC_CLI_MUTED=1) reaches Unity.Eve: Unity.Shared.ProfilesBootstrap
    // reads it before the first scene loads and sets Profiles.SessionMuted. AudioService and MusicDirector
    // don't read any profile settings, so this zeroes the engine-wide AudioListener instead: with
    // AudioListener.volume at 0, every AudioSource in the scene is silent for this run only. Nothing here
    // ever touches settings.json.
    public static class SessionAudio
    {
        // True once Apply found the session muted (the CLI mute; Eve has no player-facing mute setting yet).
        public static bool Muted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnLoad() => Apply();

        // Reads Unity.Shared's session mute and silences the listener if it's on. Never unmutes what
        // something else muted. Returns Muted.
        public static bool Apply()
        {
            Muted = Profiles.SessionMuted;
            if (Muted)
            {
                AudioListener.volume = 0f;
                Debug.Log("Unity.Eve: audio muted for this session (Unity.Shared session mute; -muted / MINDATTIC_CLI_MUTED=1).");
            }
            return Muted;
        }
    }
}
