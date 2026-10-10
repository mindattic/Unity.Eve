using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEve
{
    [Serializable]
    public sealed class SoundEntry
    {
        public SoundId Id;
        public AudioClip[] Clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float Volume = 0.8f;
        public Vector2 PitchRange = new Vector2(0.95f, 1.05f);
    }

    [CreateAssetMenu(menuName = "Unity Eve/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        public List<SoundEntry> Entries = new List<SoundEntry>();

        public SoundEntry Find(SoundId id) => Entries.Find(e => e.Id == id);
    }
}
