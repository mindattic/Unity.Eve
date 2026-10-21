using System.Collections;
using UnityEngine;

namespace Unity.Eve.Battle
{
    /// <summary>
    /// Short-lived combat visuals: tracers, muzzle flashes, impact sparks, explosions, PE auras.
    /// Built from primitives and one additive material so the demo needs no VFX assets.
    /// </summary>
    public sealed class BattleFx : MonoBehaviour
    {
        [SerializeField] private Material _additive;

        public static BattleFx Instance { get; private set; }

        /// <summary>The shared additive material, for callers (the Line Rush aim preview) that draw
        /// their own short-lived geometry instead of asking BattleFx for an effect.</summary>
        public Material Additive => _additive;

        public void Configure(Material additive) => _additive = additive;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Tracer(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _additive;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.05f;
            line.endWidth = 0.02f;
            line.startColor = color;
            line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            StartCoroutine(FadeLine(line, 0.09f));
            Flash(from, color, 3f, 4f, 0.06f);
        }

        public void Flash(Vector3 at, Color color, float intensity, float range, float seconds)
        {
            var go = new GameObject("Flash");
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            StartCoroutine(FadeLight(light, seconds));
        }

        public void Burst(Vector3 at, Color color, int count, float speed, float size = 0.08f)
        {
            var go = new GameObject("Burst");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = 1.2f;
            main.useUnscaledTime = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = _additive;
            ps.Play();
        }

        public void Explosion(Vector3 at, float radius, Color color)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.name = "Explosion";
            ball.transform.position = at;
            var r = ball.GetComponent<Renderer>();
            r.sharedMaterial = _additive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            StartCoroutine(Expand(ball.transform, r, radius * 2f, color, 0.45f));
            Flash(at + Vector3.up * 0.5f, color, 8f, radius * 4f, 0.5f);
            Burst(at, color, 60, 7f, 0.14f);
            Burst(at, new Color(0.3f, 0.3f, 0.3f), 30, 3f, 0.3f);
        }

        /// <summary>A flat glowing disc that stays until its owner (<see cref="HazardPuddle"/>) destroys
        /// it — unlike every other effect here, this one outlives the frame it was spawned on.</summary>
        public GameObject Puddle(Vector3 at, Color color, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Puddle";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at + Vector3.up * 0.02f;
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _additive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(block);
            return go;
        }

        public void Aura(Transform around, Color color)
        {
            Flash(around.position + Vector3.up, color, 4f, 5f, 0.6f);
            var go = new GameObject("Aura");
            go.transform.position = around.position + Vector3.up * 0.05f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.9f;
            main.startSpeed = 0.2f;
            main.startSize = 0.12f;
            main.startColor = color;
            main.gravityModifier = -0.6f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = _additive;
            ps.Play();
        }

        private static IEnumerator FadeLine(LineRenderer line, float seconds)
        {
            var t = 0f;
            var c = line.startColor;
            while (t < seconds)
            {
                t += Time.deltaTime;
                c.a = 1f - t / seconds;
                line.startColor = c;
                line.endColor = c;
                yield return null;
            }
            Destroy(line.gameObject);
        }

        private static IEnumerator FadeLight(Light light, float seconds)
        {
            var start = light.intensity;
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                light.intensity = start * (1f - t / seconds);
                yield return null;
            }
            Destroy(light.gameObject);
        }

        private static IEnumerator Expand(Transform ball, Renderer r, float diameter, Color color, float seconds)
        {
            var block = new MaterialPropertyBlock();
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var k = t / seconds;
                ball.localScale = Vector3.one * Mathf.Lerp(0.2f, diameter, 1f - (1f - k) * (1f - k));
                block.SetColor("_BaseColor", color * (1f - k));
                r.SetPropertyBlock(block);
                yield return null;
            }
            Destroy(ball.gameObject);
        }
    }
}
