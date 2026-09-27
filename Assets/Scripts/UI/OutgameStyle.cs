using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// 명일방주(Arknights) 스타일의 테크웨어 / 고대비 미니멀 UI 디자인 토큰 및 공통 스타일 적용 유틸리티입니다.
    /// 어두운 다크 슬레이트 베이스, 네온 시안(#00E5FF) 강조, 택티컬 오렌지/앰버 골드 악센트를 적용합니다.
    /// </summary>
    public static class OutgameStyle
    {
        // ── 공통 색상 팔레트 (Arknights High-Contrast Cybernetics) ──
        public static readonly Color BackgroundBase = new Color(0.047f, 0.055f, 0.071f, 1f);      // #0C0E12
        public static readonly Color SurfaceDark = new Color(0.078f, 0.090f, 0.118f, 0.95f);       // #14171E
        public static readonly Color SurfaceCard = new Color(0.106f, 0.122f, 0.165f, 0.98f);       // #1B1F2A
        public static readonly Color SurfaceCardLight = new Color(0.141f, 0.165f, 0.224f, 1f);     // #242A39
        public static readonly Color BorderSubtle = new Color(0.176f, 0.208f, 0.267f, 0.8f);       // #2D3544
        
        // 악센트 컬러
        public static readonly Color AccentCyan = new Color(0.0f, 0.898f, 1.0f, 1f);               // #00E5FF (전술 네온 시안)
        public static readonly Color AccentTeal = new Color(0.15f, 0.7f, 0.7f, 1f);
        public static readonly Color AccentOrange = new Color(1.0f, 0.369f, 0.118f, 1f);           // #FF5E1E (작전/전투 주황)
        public static readonly Color AccentGold = new Color(1.0f, 0.702f, 0.0f, 1f);               // #FFB300 (앰버 골드 / 확인 / 정예)
        public static readonly Color AccentRed = new Color(1.0f, 0.220f, 0.314f, 1f);              // #FF3850 (경고 / 위험)

        // 텍스트 컬러
        public static readonly Color TextWhite = new Color(0.96f, 0.96f, 0.97f, 1f);               // 주 텍스트 (화이트)
        public static readonly Color TextDefault = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static readonly Color TextMuted = new Color(0.54f, 0.58f, 0.65f, 1f);               // 보조 텍스트 (테크 그레이)
        public static readonly Color TextDim = new Color(0.30f, 0.34f, 0.40f, 1f);                 // 비활성/워터마크
        public static readonly Color TextDisabled = new Color(0.4f, 0.4f, 0.4f, 1f);

        // 빠른 화면 전환 속도 (짧은 전환)
        public const float TransitionDuration = 0.15f;

        /// <summary>
        /// 버튼에 키보드 포커스와 선택/비활성 상태를 제공하는 공통 네온 시안 스타일을 적용합니다.
        /// </summary>
        public static void ApplyPrimaryButtonStyle(Button button, TextMeshProUGUI text = null, Color? accent = null)
        {
            if (button == null) return;
            var acc = accent ?? AccentCyan;
            
            var colors = button.colors;
            colors.normalColor = Color.Lerp(SurfaceCard, acc, 0.20f);
            colors.highlightedColor = Color.Lerp(SurfaceCardLight, acc, 0.45f);
            colors.pressedColor = acc;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.12f, 0.13f, 0.16f, 0.5f);
            button.colors = colors;

            if (text != null)
            {
                text.color = TextWhite;
                text.font = UiFontProvider.GetTmpFont();
            }
        }

        /// <summary>
        /// COMBAT / SORTIE 등 핵심 작전 출격 버튼에 강렬한 택티컬 오렌지 스타일을 적용합니다.
        /// </summary>
        public static void ApplyCombatButtonStyle(Button button, TextMeshProUGUI text = null)
        {
            ApplyPrimaryButtonStyle(button, text, AccentOrange);
        }

        /// <summary>
        /// 어두운 표면 패널 스타일을 적용합니다.
        /// </summary>
        public static void ApplySurfaceStyle(Image image)
        {
            if (image != null)
            {
                image.color = SurfaceDark;
            }
        }

        /// <summary>
        /// 카드 컨테이너 스타일을 적용합니다.
        /// </summary>
        public static void ApplyCardStyle(Image image)
        {
            if (image != null)
            {
                image.color = SurfaceCard;
            }
        }
    }
}
