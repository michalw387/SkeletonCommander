using TMPro;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    // Skeleton counter/list and short notices drawn as part of the game's HUD canvas, so windows like
    // the inventory cover them the same way they cover the minimap, and they hide together with the HUD.
    internal class SkeletonHud
    {
        private const float RightMargin = 20f;
        private const float NoticeDuration = 2f;
        private const float NoticeFadeDuration = 0.5f;

        private TextMeshProUGUI _list;
        private TextMeshProUGUI _notice;
        private float _noticeHideTime;
        private bool _noticeFading;

        public void Show(string text, float positionY, float fontSize)
        {
            if (_list == null)
                _list = CreateText("SkeletonTrackerHud", TextAlignmentOptions.TopLeft, new Vector2(1f, 1f), new Vector2(-RightMargin, 0f));
            if (_list == null)
                return;

            _list.text = text;
            _list.fontSize = fontSize;
            var rect = _list.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f - positionY);
            // Lines are left-aligned inside a box as wide as the longest line, and the box keeps a fixed
            // distance from the right screen edge, so the longest line ends at that margin.
            rect.sizeDelta = new Vector2(Mathf.Ceil(_list.GetPreferredValues(text).x), rect.sizeDelta.y);
            _list.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_list != null)
                _list.gameObject.SetActive(false);
        }

        // A single notice that replaces the previous one. Messages sent through MessageHud can be
        // stacked by other mods (e.g. notification overhauls), which hides which one is the latest.
        public void ShowNotice(string text)
        {
            if (_notice == null)
            {
                _notice = CreateText("SkeletonTrackerNotice", TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero);
                if (_notice == null)
                    return;
                var rect = _notice.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.65f);
                _notice.fontSize = 24;
                _notice.color = new Color(1f, 0.85f, 0.4f);
            }

            _notice.text = text;
            _notice.CrossFadeAlpha(1f, 0f, ignoreTimeScale: true);
            _noticeHideTime = Time.unscaledTime + NoticeDuration;
            _noticeFading = false;
        }

        public void Update()
        {
            if (_notice != null && !_noticeFading && Time.unscaledTime >= _noticeHideTime)
            {
                _notice.CrossFadeAlpha(0f, NoticeFadeDuration, ignoreTimeScale: true);
                _noticeFading = true;
            }
        }

        public void Destroy()
        {
            if (_list != null)
                Object.Destroy(_list.gameObject);
            if (_notice != null)
                Object.Destroy(_notice.gameObject);
            _list = null;
            _notice = null;
        }

        private static TextMeshProUGUI CreateText(string name, TextAlignmentOptions alignment, Vector2 pivot, Vector2 offset)
        {
            var hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return null;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(hud.m_rootObject.transform, worldPositionStays: false);
            // Draw beneath the other HUD elements.
            go.transform.SetAsFirstSibling();

            var text = go.AddComponent<TextMeshProUGUI>();
            // Reuse the HUD's font and material (with its outline) so the text matches the game's style.
            text.font = hud.m_healthText.font;
            text.fontSharedMaterial = hud.m_healthText.fontSharedMaterial;
            text.fontSize = 18;
            text.alignment = alignment;
            text.richText = true;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var rect = text.rectTransform;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(700f, 400f);
            rect.anchoredPosition = offset;
            return text;
        }
    }
}
