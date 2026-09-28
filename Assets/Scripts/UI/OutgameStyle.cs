using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace CheckmateRPG.UI
{
    /// <summary>Neutral tactical surfaces. Colour is reserved for actions and state.</summary>
    public static class OutgameStyle
    {
        public static readonly Color BackgroundBase = Hex(0x191B1C);
        public static readonly Color SurfaceDark = Hex(0x222526);
        public static readonly Color SurfaceCard = Hex(0x303435);
        public static readonly Color SurfaceCardLight = Hex(0x454B4D);
        public static readonly Color BorderSubtle = Hex(0x606767);
        public static readonly Color Paper = Hex(0xE6E5DF);
        public static readonly Color Ink = Hex(0x191C1D);
        public static readonly Color AccentCyan = Hex(0x8CB9C1);
        public static readonly Color AccentTeal = Hex(0x8CB9C1);
        public static readonly Color AccentOrange = Hex(0xD7AA50);
        public static readonly Color AccentGold = Hex(0xD7BB79);
        public static readonly Color AccentRed = Hex(0xDB7671);
        public static readonly Color TextWhite = Hex(0xF3F2EC);
        public static readonly Color TextDefault = Hex(0xDEDFDA);
        public static readonly Color TextMuted = Hex(0xB1B7B4);
        public static readonly Color TextDim = Hex(0x7D8583);
        public static readonly Color TextDisabled = Hex(0x858B88);
        public const float TransitionDuration = 0.15f;

        public static Color Hex(uint value) => new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);

        public static void ApplyPrimaryButtonStyle(Button button, TextMeshProUGUI text = null, Color? accent = null)
        {
            if (!button) return;
            // The image must be white: Unity multiplies the target colour by ColorBlock.
            if (button.targetGraphic == null) button.targetGraphic = button.GetComponent<Image>();
            if (button.targetGraphic != null) button.targetGraphic.color = Color.white;
            var colors = button.colors;
            colors.normalColor = SurfaceCard;
            colors.highlightedColor = SurfaceCardLight;
            colors.pressedColor = Hex(0x555E60);
            colors.selectedColor = SurfaceCardLight;
            colors.disabledColor = Hex(0x292C2D);
            colors.colorMultiplier = 1;
            colors.fadeDuration = .08f;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = colors;
            text = text != null ? text : button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text) { text.color = TextWhite; text.font = UiFontProvider.GetTmpFont(); text.raycastTarget = false; }
        }

        public static void ApplyLightButtonStyle(Button button, TextMeshProUGUI text = null, bool combat = false)
        {
            ApplyPrimaryButtonStyle(button, text);
            if (!button) return;
            var colors = button.colors;
            colors.normalColor = combat ? AccentOrange : Paper;
            colors.highlightedColor = combat ? Hex(0xE9C576) : Color.white;
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = combat ? Hex(0xC99B42) : Hex(0xC6CDCA);
            colors.disabledColor = Hex(0x797E7A);
            button.colors = colors;
            text = text != null ? text : button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text) text.color = Ink;
        }

        public static void ApplySelectionStyle(Button button, bool selected)
        {
            if (selected) ApplyLightButtonStyle(button);
            else ApplyPrimaryButtonStyle(button);
        }

        public static void ApplyCombatButtonStyle(Button button, TextMeshProUGUI text = null) => ApplyLightButtonStyle(button, text, true);
        public static void ApplySurfaceStyle(Image image) { if (image) image.color = SurfaceDark; }
        public static void ApplyCardStyle(Image image) { if (image) image.color = SurfaceCard; }
    }
}
