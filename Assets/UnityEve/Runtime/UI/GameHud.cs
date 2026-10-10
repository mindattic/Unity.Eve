using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEve.UI
{
    /// <summary>
    /// The whole screen, built in code on UI Toolkit and styled by Hud.uss. Shows the low-resolution
    /// PS1-style render target letterboxed to 4:3, and draws every HUD element on top of it.
    /// The HUD only presents state; it never changes game rules.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private StyleSheet _style;
        [SerializeField] private Font _font;
        [SerializeField] private RenderTexture _screenTexture;

        private VisualElement _root, _screen, _overlay;
        private VisualElement _status, _hpFill, _peFill, _atbFill, _atbRow;
        private Label _hpText, _peText, _levelText, _ammoText;
        private VisualElement _menuPanel;
        private Label _menuTitle;
        private VisualElement _menuList;
        private Label _menuDetail;
        private VisualElement _targetPanel, _targetFill;
        private Label _targetName;
        private Label _message, _prompt, _banner, _subBanner;
        private VisualElement _progress, _progressFill;
        private VisualElement _focus, _focusFill;
        private VisualElement _letterTop, _letterBottom, _fade, _hurt, _focusTint;
        private readonly List<(Label label, Vector3 world, float born, float life)> _popups = new List<(Label, Vector3, float, float)>();
        private MenuModel _shownMenu;
        private int _shownVersion = -1;
        private float _messageUntil;
        private float _bannerUntil;
        private float _hurtStart, _hurtPeak, _hurtDuration;
        private float _letterbox, _letterboxTarget;

        public static GameHud Instance { get; private set; }
        public Camera WorldCamera { get; set; }

        public void Configure(StyleSheet style, Font font, RenderTexture screen)
        {
            _style = style;
            _font = font;
            _screenTexture = screen;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;
            _root.Clear();
            if (_style != null) _root.styleSheets.Add(_style);
            if (_font != null) _root.style.unityFontDefinition = FontDefinition.FromFont(_font);
            _root.AddToClassList("hud-root");
            Build();
        }

        private VisualElement Add(VisualElement parent, string className, VisualElement child = null)
        {
            child ??= new VisualElement();
            child.AddToClassList(className);
            child.pickingMode = PickingMode.Ignore;
            parent.Add(child);
            return child;
        }

        private Label AddLabel(VisualElement parent, string className, string text = "")
        {
            var l = new Label(text);
            Add(parent, className, l);
            return l;
        }

        private VisualElement Bar(VisualElement parent, string label, string fillClass, out Label valueText)
        {
            var row = Add(parent, "bar-row");
            AddLabel(row, "bar-label", label);
            var track = Add(row, "bar-track");
            var fill = Add(track, "bar-fill");
            fill.AddToClassList(fillClass);
            valueText = AddLabel(row, "bar-value");
            return fill;
        }

        private void Build()
        {
            _screen = Add(_root, "screen");
            if (_screenTexture != null) _screen.style.backgroundImage = Background.FromRenderTexture(_screenTexture);
            _overlay = Add(_screen, "overlay");

            _focusTint = Add(_overlay, "focus-tint");
            _hurt = Add(_overlay, "hurt");
            _letterTop = Add(_overlay, "letterbox-top");
            _letterBottom = Add(_overlay, "letterbox-bottom");

            _status = Add(_overlay, "status");
            _levelText = AddLabel(_status, "level");
            _hpFill = Bar(_status, "HP", "fill-hp", out _hpText);
            _peFill = Bar(_status, "PE", "fill-pe", out _peText);
            _atbFill = Bar(_status, "AT", "fill-atb", out _);
            _atbRow = _atbFill.parent.parent;
            _ammoText = AddLabel(_status, "ammo");

            _targetPanel = Add(_overlay, "target");
            _targetName = AddLabel(_targetPanel, "target-name");
            var track = Add(_targetPanel, "bar-track");
            _targetFill = Add(track, "bar-fill");
            _targetFill.AddToClassList("fill-hp");

            _menuPanel = Add(_overlay, "menu");
            _menuTitle = AddLabel(_menuPanel, "menu-title");
            _menuList = Add(_menuPanel, "menu-list");
            _menuDetail = AddLabel(_menuPanel, "menu-detail");

            _progress = Add(_overlay, "progress");
            _progressFill = Add(Add(_progress, "bar-track"), "bar-fill");
            _progressFill.AddToClassList("fill-progress");

            _focus = Add(_overlay, "focus");
            AddLabel(_focus, "focus-title", "FOCUS");
            var moves = Add(_focus, "focus-moves");
            AddLabel(moves, "focus-move", "[X/1] Quick Heal");
            AddLabel(moves, "focus-move", "[Y/2] Target Vitals");
            AddLabel(moves, "focus-move", "[A/3] Counter");
            _focusFill = Add(Add(_focus, "bar-track"), "bar-fill");
            _focusFill.AddToClassList("fill-focus");

            _prompt = AddLabel(_overlay, "prompt");
            _message = AddLabel(_overlay, "message");
            _banner = AddLabel(_overlay, "banner");
            _subBanner = AddLabel(_overlay, "sub-banner");
            _fade = Add(_overlay, "fade");

            SetBattleMode(false);
            ShowMenu(null);
            ShowTarget(null, 0f);
            ShowPrompt(null);
            ShowProgress(-1f);
            ShowFocus(-1f);
            _banner.style.display = DisplayStyle.None;
            _subBanner.style.display = DisplayStyle.None;
            SetFade(0f);
        }

        // ---------------------------------------------------------------- public presentation API

        public void SetStatus(int level, int hp, int maxHp, float pe, float maxPe, float atb, bool atbFull, string ammo)
        {
            _levelText.text = $"LV {level}";
            _hpFill.style.width = Length.Percent(100f * hp / Mathf.Max(1, maxHp));
            _hpText.text = $"{hp}/{maxHp}";
            _peFill.style.width = Length.Percent(100f * pe / Mathf.Max(1f, maxPe));
            _peText.text = $"{Mathf.FloorToInt(pe)}";
            _atbFill.style.width = Length.Percent(100f * atb);
            _atbFill.EnableInClassList("fill-atb-full", atbFull && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.6f);
            _ammoText.text = ammo;
        }

        public void SetBattleMode(bool inBattle)
        {
            _atbRow.style.display = inBattle ? DisplayStyle.Flex : DisplayStyle.None;
            _letterboxTarget = inBattle ? 1f : 0f;
        }

        public void ShowMenu(MenuModel menu)
        {
            _shownMenu = menu;
            _shownVersion = -1;
            _menuPanel.style.display = menu == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ShowTarget(string name, float hpFraction)
        {
            _targetPanel.style.display = string.IsNullOrEmpty(name) ? DisplayStyle.None : DisplayStyle.Flex;
            _targetName.text = name ?? "";
            _targetFill.style.width = Length.Percent(100f * Mathf.Clamp01(hpFraction));
        }

        public void ShowMessage(string text, float seconds = 2.6f)
        {
            _message.text = text;
            _message.style.opacity = 1f;
            _messageUntil = Time.unscaledTime + seconds;
        }

        public void ShowBanner(string title, string subtitle = null, float seconds = 2f)
        {
            _banner.text = title;
            _banner.style.display = DisplayStyle.Flex;
            _subBanner.text = subtitle ?? "";
            _subBanner.style.display = string.IsNullOrEmpty(subtitle) ? DisplayStyle.None : DisplayStyle.Flex;
            _bannerUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
        }

        public void HideBanner() => _bannerUntil = 0f;

        public void ShowPrompt(string text)
        {
            _prompt.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            _prompt.text = text ?? "";
        }

        /// <param name="fraction">0..1, or negative to hide.</param>
        public void ShowProgress(float fraction)
        {
            _progress.style.display = fraction < 0f ? DisplayStyle.None : DisplayStyle.Flex;
            if (fraction >= 0f) _progressFill.style.width = Length.Percent(100f * fraction);
        }

        /// <param name="remaining01">Window remaining, or negative to hide.</param>
        public void ShowFocus(float remaining01)
        {
            var on = remaining01 >= 0f;
            _focus.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            _focusTint.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) _focusFill.style.width = Length.Percent(100f * remaining01);
        }

        public void SetFade(float alpha)
        {
            _fade.style.opacity = alpha;
            _fade.style.display = alpha <= 0.001f ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void Hurt(float severity)
        {
            _hurtStart = Time.unscaledTime;
            _hurtPeak = Mathf.Min(0.72f, 0.28f + 0.44f * severity);
            _hurtDuration = 0.5f + 0.35f * severity;
        }

        public void Popup(Vector3 world, string text, Color color, bool big = false)
        {
            var label = new Label(text);
            label.AddToClassList("popup");
            if (big) label.AddToClassList("popup-big");
            label.style.color = color;
            label.pickingMode = PickingMode.Ignore;
            _overlay.Add(label);
            _popups.Add((label, world, Time.unscaledTime, 1.1f));
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            if (_root == null) return;
            FitScreen();

            if (_shownMenu != null && _shownMenu.Version != _shownVersion) RedrawMenu();

            if (Time.unscaledTime > _messageUntil) _message.style.opacity = Mathf.Max(0f, _message.resolvedStyle.opacity - Time.unscaledDeltaTime * 5f);
            if (Time.unscaledTime > _bannerUntil)
            {
                _banner.style.display = DisplayStyle.None;
                _subBanner.style.display = DisplayStyle.None;
            }

            var hk = _hurtDuration > 0f ? Mathf.Clamp01((Time.unscaledTime - _hurtStart) / _hurtDuration) : 1f;
            _hurt.style.opacity = _hurtPeak * (1f - hk) * (1f - hk);

            _letterbox = Mathf.MoveTowards(_letterbox, _letterboxTarget, Time.unscaledDeltaTime / 0.35f);
            _letterTop.style.height = Length.Percent(7f * _letterbox);
            _letterBottom.style.height = Length.Percent(7f * _letterbox);

            UpdatePopups();
        }

        /// <summary>Letterbox the 4:3 render into whatever window shape the player picked.</summary>
        private void FitScreen()
        {
            var w = _root.resolvedStyle.width;
            var h = _root.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0f || h <= 0f) return;
            const float aspect = 4f / 3f;
            float sw = w, sh = w / aspect;
            if (sh > h)
            {
                sh = h;
                sw = h * aspect;
            }
            _screen.style.width = sw;
            _screen.style.height = sh;
            _screen.style.left = (w - sw) * 0.5f;
            _screen.style.top = (h - sh) * 0.5f;
        }

        private void RedrawMenu()
        {
            _shownVersion = _shownMenu.Version;
            _menuTitle.text = _shownMenu.Title;
            _menuList.Clear();
            for (var i = 0; i < _shownMenu.Entries.Count; i++)
            {
                var e = _shownMenu.Entries[i];
                var row = new Label((i == _shownMenu.Cursor ? "> " : "  ") + e.Label);
                row.AddToClassList("menu-item");
                if (i == _shownMenu.Cursor) row.AddToClassList("menu-item-selected");
                if (!e.Enabled) row.AddToClassList("menu-item-disabled");
                row.pickingMode = PickingMode.Ignore;
                _menuList.Add(row);
            }
            var detail = _shownMenu.Current?.Detail;
            _menuDetail.text = detail ?? "";
            _menuDetail.style.display = string.IsNullOrEmpty(detail) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void UpdatePopups()
        {
            var cam = WorldCamera;
            var w = _screen.resolvedStyle.width;
            var h = _screen.resolvedStyle.height;
            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                var (label, world, born, life) = _popups[i];
                var k = (Time.unscaledTime - born) / life;
                if (k >= 1f || cam == null)
                {
                    label.RemoveFromHierarchy();
                    _popups.RemoveAt(i);
                    continue;
                }
                var vp = cam.WorldToViewportPoint(world);
                label.style.display = vp.z > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                label.style.left = vp.x * w - 30f;
                label.style.top = (1f - vp.y) * h - 46f * k - 20f;
                label.style.opacity = 1f - k * k;
            }
        }
    }
}
