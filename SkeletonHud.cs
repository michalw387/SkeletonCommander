using TMPro;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    // Skeleton counter/list drawn as part of the game's HUD canvas, so windows like the inventory
    // cover it the same way they cover the minimap, and it hides together with the HUD.
    internal class SkeletonHud
    {
        private TextMeshProUGUI _text;

        public void Show(string text, float positionY)
        {
            if (_text == null && !Create())
                return;

            _text.text = text;
            var rect = _text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f - positionY);
            _text.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_text != null)
                _text.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (_text != null)
                Object.Destroy(_text.gameObject);
            _text = null;
        }

        private bool Create()
        {
            var hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return false;

            var go = new GameObject("SkeletonTrackerHud", typeof(RectTransform));
            go.transform.SetParent(hud.m_rootObject.transform, worldPositionStays: false);
            // Draw beneath the other HUD elements.
            go.transform.SetAsFirstSibling();

            _text = go.AddComponent<TextMeshProUGUI>();
            // Reuse the HUD's font and material (with its outline) so the text matches the game's style.
            _text.font = hud.m_healthText.font;
            _text.fontSharedMaterial = hud.m_healthText.fontSharedMaterial;
            _text.fontSize = 18;
            _text.alignment = TextAlignmentOptions.TopRight;
            _text.richText = true;
            _text.raycastTarget = false;
            _text.textWrappingMode = TextWrappingModes.NoWrap;

            var rect = _text.rectTransform;
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(700f, 400f);
            rect.anchoredPosition = new Vector2(-20f, 0f);
            return true;
        }
    }
}
