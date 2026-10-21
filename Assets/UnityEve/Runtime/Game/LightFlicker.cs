using UnityEngine;

namespace Unity.Eve.Game
{
    /// <summary>A failing bulb: mostly steady, with irregular dropouts. Pure atmosphere.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float _dropoutChance = 0.04f;

        private Light _light;
        private float _base;
        private float _until;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
        }

        private void Update()
        {
            if (Time.time < _until) return;
            var dropout = Random.value < _dropoutChance;
            _light.intensity = dropout ? _base * Random.Range(0.05f, 0.4f) : _base * Random.Range(0.92f, 1.04f);
            _until = Time.time + (dropout ? Random.Range(0.04f, 0.12f) : Random.Range(0.08f, 0.5f));
        }
    }
}
