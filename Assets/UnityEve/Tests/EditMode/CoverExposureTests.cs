using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Eve.Battle;

namespace Unity.Eve.Tests
{
    /// <summary>
    /// Cover.Exposure: the fraction of Kat's 9-point silhouette a CoverProp hides from an attacker. It
    /// scales incoming damage by CoverMaxReduction, so a wrong value makes cover useless or invulnerable.
    /// </summary>
    public class CoverExposureTests
    {
        private static readonly Vector3 Feet = Vector3.zero;
        private static readonly Vector3 Eye = new Vector3(0f, 1.5f, -5f);

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made)
            {
                var prop = go.GetComponent<CoverProp>();
                if (prop != null) Call(prop, "OnDisable");
                Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        private static void Call(CoverProp prop, string method) =>
            typeof(CoverProp).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(prop, null);

        /// <summary>A box collider; registered as cover the way entering Play would (edit mode never runs OnEnable).</summary>
        private GameObject Box(Vector3 center, Vector3 size, bool cover)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = center;
            go.transform.localScale = size;
            _made.Add(go);
            if (cover) Call(go.AddComponent<CoverProp>(), "OnEnable");
            Physics.SyncTransforms();
            return go;
        }

        [Test]
        public void No_registered_cover_means_fully_exposed()
        {
            Box(new Vector3(0f, 1f, -2.5f), new Vector3(3f, 4f, 0.5f), cover: false);
            Assert.That(CoverProp.Registry.Count, Is.EqualTo(0));
            Assert.That(Cover.Exposure(Feet, Eye), Is.EqualTo(0f));
        }

        [Test]
        public void An_attacker_on_top_of_the_defender_sees_no_cover()
        {
            Box(new Vector3(0f, 1f, -2.5f), new Vector3(3f, 4f, 0.5f), cover: true);
            Assert.That(Cover.Exposure(Feet, new Vector3(0f, 1.5f, 0f)), Is.EqualTo(0f));
        }

        [Test]
        public void A_wall_of_cover_blocks_the_whole_silhouette()
        {
            Box(new Vector3(0f, 1f, -2.5f), new Vector3(3f, 4f, 0.5f), cover: true);
            Assert.That(Cover.Exposure(Feet, Eye), Is.EqualTo(1f));
        }

        [Test]
        public void A_low_prop_hides_only_the_lower_row()
        {
            // 0.6 m tall, just in front of her: hides the 0.25 m row, not the 0.85 m or 1.45 m rows.
            Box(new Vector3(0f, 0.3f, -0.5f), new Vector3(2f, 0.6f, 0.5f), cover: true);
            Assert.That(Cover.Exposure(Feet, Eye), Is.EqualTo(3f / 9f).Within(1e-5f));
        }

        [Test]
        public void Colliders_that_are_not_cover_props_are_ignored()
        {
            Box(new Vector3(0f, 1f, -2.5f), new Vector3(3f, 4f, 0.5f), cover: false);
            Box(new Vector3(20f, 0.5f, 20f), Vector3.one, cover: true); // registered, but off the line of fire
            Assert.That(Cover.Exposure(Feet, Eye), Is.EqualTo(0f));
        }
    }
}
