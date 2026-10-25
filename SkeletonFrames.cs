using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SkeletonCommander
{
    internal struct SkeletonFrameData
    {
        public string Header;
        public bool ShowBar;
        public float Health;
        public float MaxHealth;
        public Color BarColor;
        // Barrier from the Staff of Protection, when the skeleton has one.
        public bool ShowShield;
        public float Shield;
        public float MaxShield;
        public float ShieldSeconds;
    }

    // Party-frame style panels below the skeleton counter: a translucent panel per skeleton with a header
    // line and an HP bar. Frames are pooled and reused on every scan. Sizes scale with the HUD font size.
    internal class SkeletonFrames
    {
        private const float Gap = 6f;
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color BarBackColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        private static readonly Color ShieldColor = new Color(0.45f, 0.75f, 1f);

        private class Bar
        {
            public RectTransform Back;
            public RectTransform Fill;
            public Image FillImage;
            public TextMeshProUGUI Text;
        }

        private class Frame
        {
            public RectTransform Root;
            public TextMeshProUGUI Header;
            public Bar Health;
            public Bar Shield;
        }

        private readonly List<Frame> _frames = new List<Frame>();
        private RectTransform _container;

        // anchor: the HUD box's anchor; top: distance from that anchor down to the first frame.
        public void Show(List<SkeletonFrameData> data, Vector2 anchor, float rightMargin, float top, float fontSize)
        {
            if (_container == null && !CreateContainer())
                return;

            float width = fontSize * 22f;
            float padding = fontSize * 0.35f;
            float headerHeight = fontSize * 1.45f;
            float barHeight = fontSize * 1.2f;
            float shieldHeight = fontSize * 0.95f;

            _container.anchorMin = _container.anchorMax = anchor;
            _container.anchoredPosition = new Vector2(-rightMargin, -top);
            _container.gameObject.SetActive(true);

            float y = 0f;
            for (int i = 0; i < data.Count; i++)
            {
                if (i == _frames.Count)
                    _frames.Add(CreateFrame(_container));
                var frame = _frames[i];
                var item = data[i];

                frame.Root.gameObject.SetActive(true);
                frame.Root.anchoredPosition = new Vector2(0f, -y);

                frame.Header.text = item.Header;
                frame.Header.fontSize = fontSize * 1.05f;
                frame.Header.rectTransform.anchoredPosition = new Vector2(padding, -padding * 0.5f);
                frame.Header.rectTransform.sizeDelta = new Vector2(width - padding * 2f, headerHeight);

                // Bars stack below the header; the frame grows with them.
                float bottom = padding * 0.5f + headerHeight;
                if (SetBar(frame.Health, item.ShowBar, item.Health, item.MaxHealth, item.BarColor,
                        $"{item.Health:0} / {item.MaxHealth:0}", bottom, width, padding, barHeight, fontSize * 0.9f))
                    bottom += barHeight + padding * 0.5f;
                if (SetBar(frame.Shield, item.ShowShield, item.Shield, item.MaxShield, ShieldColor,
                        $"shield {item.Shield:0} / {item.MaxShield:0} · {item.ShieldSeconds:0} s", bottom, width, padding, shieldHeight, fontSize * 0.75f))
                    bottom += shieldHeight + padding * 0.5f;
                float height = bottom + padding * 0.5f;

                frame.Root.sizeDelta = new Vector2(width, height);
                y += height + Gap;
            }
            for (int i = data.Count; i < _frames.Count; i++)
                _frames[i].Root.gameObject.SetActive(false);
        }

        private static bool SetBar(Bar bar, bool show, float value, float max, Color color, string text,
            float top, float width, float padding, float height, float fontSize)
        {
            bar.Back.gameObject.SetActive(show);
            if (!show)
                return false;
            bar.Back.anchoredPosition = new Vector2(padding, -top);
            bar.Back.sizeDelta = new Vector2(width - padding * 2f, height);
            bar.Fill.anchorMax = new Vector2(max > 0f ? Mathf.Clamp01(value / max) : 0f, 1f);
            bar.FillImage.color = color;
            bar.Text.text = text;
            bar.Text.fontSize = fontSize;
            return true;
        }

        public void Hide()
        {
            if (_container != null)
                _container.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (_container != null)
                Object.Destroy(_container.gameObject);
            _container = null;
            _frames.Clear();
        }

        private bool CreateContainer()
        {
            // The previous container went away with the previous HUD, and its frames with it.
            _frames.Clear();
            var hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return false;
            _container = CreateRect("SkeletonCommanderFrames", hud.m_rootObject.transform, new Vector2(1f, 1f));
            // Draw beneath the other HUD elements, like the counter.
            _container.SetAsFirstSibling();
            return true;
        }

        private static Frame CreateFrame(Transform parent)
        {
            var frame = new Frame();
            frame.Root = CreateRect("Frame", parent, new Vector2(1f, 1f));
            frame.Root.anchorMin = frame.Root.anchorMax = new Vector2(1f, 1f);
            AddImage(frame.Root, null, PanelColor);

            frame.Header = SkeletonHud.CreateText("Header", frame.Root, TextAlignmentOptions.Left, new Vector2(0f, 1f), Vector2.zero);
            frame.Header.rectTransform.anchorMin = frame.Header.rectTransform.anchorMax = new Vector2(0f, 1f);
            frame.Header.overflowMode = TextOverflowModes.Ellipsis;

            frame.Health = CreateBar("HealthBar", frame.Root);
            frame.Shield = CreateBar("ShieldBar", frame.Root);
            return frame;
        }

        private static Bar CreateBar(string name, Transform parent)
        {
            // The fill reuses the sprite of the player's own health bar so it matches the game's style.
            var barSprite = GetBarSprite();
            var bar = new Bar();
            bar.Back = CreateRect(name, parent, new Vector2(0f, 1f));
            bar.Back.anchorMin = bar.Back.anchorMax = new Vector2(0f, 1f);
            AddImage(bar.Back, barSprite, BarBackColor);

            bar.Fill = CreateRect("Fill", bar.Back, new Vector2(0f, 0.5f));
            bar.Fill.anchorMin = Vector2.zero;
            bar.Fill.offsetMin = bar.Fill.offsetMax = Vector2.zero;
            bar.FillImage = AddImage(bar.Fill, barSprite, Color.white);

            bar.Text = SkeletonHud.CreateText("Text", bar.Back, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero);
            var textRect = bar.Text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            return bar;
        }

        private static Sprite GetBarSprite()
        {
            var bar = Hud.instance != null ? Hud.instance.m_healthBarFast : null;
            var image = bar != null && bar.m_bar != null ? bar.m_bar.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.pivot = pivot;
            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            // Bordered sprites stretch without distorting their edges when sliced.
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
