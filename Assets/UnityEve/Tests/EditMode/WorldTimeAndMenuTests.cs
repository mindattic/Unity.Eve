using NUnit.Framework;
using UnityEngine;
using Unity.Eve.Game;
using Unity.Eve.UI;

namespace Unity.Eve.Tests
{
    /// <summary>
    /// WorldTime is the single owner of Time.timeScale (AGENTS law 3), so overlapping pause and
    /// slow-motion requests must never freeze or speed up the game; MenuModel is every command list.
    /// </summary>
    public class WorldTimeAndMenuTests
    {
        private GameObject _go;
        private WorldTime _time;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            _go = new GameObject("WorldTime");
            _time = _go.AddComponent<WorldTime>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Time.timeScale = 1f;
        }

        // --- WorldTime ------------------------------------------------------------------------

        [Test]
        public void Any_pause_key_holds_time_at_zero_until_every_key_resumes()
        {
            object menu = "menu", targeting = "targeting";
            _time.Pause(menu);
            _time.Pause(targeting);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(_time.IsPaused, Is.True);
            _time.Resume(menu);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            _time.Resume(targeting);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_time.IsPaused, Is.False);
        }

        [Test]
        public void A_pause_beats_any_slow_motion_and_the_scale_returns_after()
        {
            _time.SetScale("focus", 0.15f);
            _time.Pause("menu");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            _time.Resume("menu");
            Assert.That(Time.timeScale, Is.EqualTo(0.15f));
        }

        [Test]
        public void The_slowest_scale_request_wins()
        {
            _time.SetScale("shootdodge", 0.3f);
            _time.SetScale("focus", 0.15f);
            Assert.That(Time.timeScale, Is.EqualTo(0.15f));
            _time.SetScale("focus", 1f);
            Assert.That(Time.timeScale, Is.EqualTo(0.3f));
        }

        [Test]
        public void A_scale_of_one_or_more_removes_the_key()
        {
            _time.SetScale("focus", 0.5f);
            _time.SetScale("focus", 2f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "scales above 1 never speed the world up");
        }

        [Test]
        public void Negative_scales_clamp_to_zero()
        {
            _time.SetScale("broken", -3f);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
        }

        [Test]
        public void ClearAll_restores_normal_time()
        {
            _time.Pause("menu");
            _time.SetScale("focus", 0.15f);
            _time.ClearAll();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_time.IsPaused, Is.False);
        }

        // --- MenuModel ------------------------------------------------------------------------

        private static MenuModel Menu(int entries, System.Action onSelect = null)
        {
            var menu = new MenuModel("Test");
            for (var i = 0; i < entries; i++) menu.Add(new MenuEntry($"Entry {i}", onSelect));
            return menu;
        }

        [TestCase(-1, 2)]
        [TestCase(1, 1)]
        [TestCase(3, 0)]
        [TestCase(7, 1)]
        [TestCase(-7, 2)]
        public void Move_wraps_in_both_directions(int delta, int expected)
        {
            var menu = Menu(3);
            menu.Move(delta);
            Assert.That(menu.Cursor, Is.EqualTo(expected));
        }

        [Test]
        public void Move_and_Select_on_an_empty_menu_are_no_ops()
        {
            var menu = Menu(0);
            menu.Move(1);
            menu.Select(0);
            Assert.That(menu.Cursor, Is.EqualTo(0));
            Assert.That(menu.Version, Is.EqualTo(0));
            Assert.That(menu.Current, Is.Null);
            Assert.That(menu.Confirm(), Is.False);
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void Select_out_of_range_is_a_no_op(int index)
        {
            var menu = Menu(3);
            menu.Select(1);
            var version = menu.Version;
            menu.Select(index);
            Assert.That(menu.Cursor, Is.EqualTo(1));
            Assert.That(menu.Version, Is.EqualTo(version));
        }

        [Test]
        public void Confirm_on_a_disabled_entry_returns_false_without_selecting()
        {
            var fired = 0;
            var menu = new MenuModel("Test").Add(new MenuEntry("Escape", () => fired++, enabled: false));
            Assert.That(menu.Confirm(), Is.False);
            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void Confirm_on_an_enabled_entry_invokes_it()
        {
            var fired = 0;
            var menu = Menu(2, () => fired++);
            Assert.That(menu.Confirm(), Is.True);
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void Version_bumps_on_add_move_and_select()
        {
            var menu = new MenuModel("Test");
            menu.Add(new MenuEntry("A", null));
            Assert.That(menu.Version, Is.EqualTo(1));
            menu.Add(new MenuEntry("B", null));
            Assert.That(menu.Version, Is.EqualTo(2));
            menu.Move(1);
            Assert.That(menu.Version, Is.EqualTo(3));
            menu.Select(0);
            Assert.That(menu.Version, Is.EqualTo(4));
            menu.Move(0);
            Assert.That(menu.Version, Is.EqualTo(4), "a zero move changes nothing");
        }
    }
}
