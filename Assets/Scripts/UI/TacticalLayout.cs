using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.UI
{
    /// <summary>Presentation helpers only; existing views retain all bindings and navigation.</summary>
    public static class TacticalLayout
    {
        public static void Place(Transform target, float x, float y, float w, float h)
        {
            if (!target) return;
            var rt = target as RectTransform;
            if (!rt) return;
            rt.anchorMin = new Vector2(x, y);
            rt.anchorMax = new Vector2(x + w, y + h);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        public static Image Panel(Transform parent, string name, Color color, float x, float y, float w, float h)
        {
            var child = parent.Find(name);
            if (!child) { child = new GameObject(name, typeof(RectTransform), typeof(Image)).transform; child.SetParent(parent, false); }
            Place(child, x, y, w, h);
            var image = child.GetComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, float x, float y, float w, float h)
        {
            var child = parent.Find(name);
            if (!child) { child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).transform; child.SetParent(parent, false); }
            Place(child, x, y, w, h);
            var label = child.GetComponent<TextMeshProUGUI>();
            label.font = UiFontProvider.GetTmpFont(); label.text = text; label.fontSize = size;
            label.color = color; label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        public static void Artwork(Transform root)
        {
            var child = root.Find("CommandArtwork");
            if (child) return;
            var texture = Resources.Load<Texture2D>("UI/CommandCenter_NoSecretary");
            if (!texture) return;
            var go = new GameObject("CommandArtwork", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(root, false);
            Place(go.transform, 0, 0, 1, 1);
            var image = go.GetComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
            go.transform.SetSiblingIndex(1);
        }

        public static void Page(OutgameViewBase view)
        {
            var root = view.transform;
            var bg = root.Find("Background");
            if (bg && bg.TryGetComponent<Image>(out var image)) image.color = OutgameStyle.BackgroundBase;
            var watermark = root.Find("TitleText");
            if (watermark) watermark.gameObject.SetActive(false);
            if (view.ViewType == OutgameViewType.Title || view.ViewType == OutgameViewType.MainLobby) return;
            Panel(root, "PageRule", OutgameStyle.BorderSubtle, .035f, .054f, .93f, .001f);
            Label(root, "PageFooter", "CHECKMATE  /  TACTICAL COMMAND SYSTEM", 14, OutgameStyle.TextMuted, .035f, .015f, .55f, .03f);
            var hint = Label(root, "PageNavigationHint", "ESC  이전 화면     /     HOME  사령부", 14, OutgameStyle.TextMuted, .65f, .015f, .315f, .03f);
            hint.alignment = TextAlignmentOptions.MidlineRight;
        }

        public static void Menu(Button button, string number, string title, string subtitle, float x, float y, float w, float h, bool light = false, bool combat = false)
        {
            if (!button) return;
            Place(button.transform, x, y, w, h);
            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (light || combat) OutgameStyle.ApplyLightButtonStyle(button, text, combat);
            else OutgameStyle.ApplyPrimaryButtonStyle(button, text);
            if (text)
            {
                Place(text.transform, .07f, .16f, .8f, .7f);
                text.text = "<size=14>" + number + "  /  " + subtitle + "</size>\n<size=" + (combat ? "42" : "29") + "><b>" + title + "</b></size>";
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var ink = light || combat ? OutgameStyle.Ink : OutgameStyle.TextWhite;
            Label(button.transform, "Direction", "/", combat ? 72 : 44, ink, .86f, .15f, .12f, .65f);
            Panel(button.transform, "Edge", ink, 0, 0, .008f, 1);
            var feedback = button.GetComponent<ButtonFeedback>();
            if (feedback) feedback.enabled = false;
        }
    }
}
