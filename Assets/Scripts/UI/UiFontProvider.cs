using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// 런타임 UI 폰트의 단일 공급자. OS 설치 한글 폰트(맑은 고딕 등)를 동적 로드해
    /// 레거시 UI Text와 TMP 폴백 양쪽에 제공한다. 폰트 파일을 프로젝트에 재배포하지 않으므로
    /// 라이선스 부담이 없고, 한글이 렌더링되지 않던 문제(글리프 누락 tofu)를 해결한다.
    /// </summary>
    public static class UiFontProvider
    {
        private static Font _legacyFont;
        private static TMPro.TMP_FontAsset _tmpFontAsset;
        private static bool _tmpFallbackRegistered;

        static UiFontProvider()
        {
            // 플레이/빌드 시작 시 TMP 전역 폴백에 한글 폰트를 등록해
            // 기존 TMP 텍스트(상태바, 플로팅 텍스트 등)도 한글을 렌더링할 수 있게 한다.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureTmpKoreanFallback();
        }

        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",   // Windows 기본 한글 폰트
            "맑은 고딕",
            "Gulim",           // Windows 구형 한글 폰트
            "Dotum",
            "Apple SD Gothic Neo", // macOS
            "Noto Sans CJK KR",
            "Arial Unicode MS"
        };

        /// <summary>레거시 UnityEngine.UI.Text 용 한글 지원 폰트.</summary>
        public static Font GetLegacyFont()
        {
            if (_legacyFont == null)
            {
                foreach (string candidate in KoreanFontCandidates)
                {
                    Font font = Font.CreateDynamicFontFromOSFont(candidate, 16);
                    if (font != null)
                    {
                        _legacyFont = font;
                        break;
                    }
                }

                if (_legacyFont == null)
                    _legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _legacyFont;
        }

        /// <summary>
        /// TMP 텍스트가 한글 글리프를 사용할 수 있도록 OS 폰트 기반 동적 TMP 폰트 에셋을
        /// 전역 폴백으로 등록한다. 씬 로드 후 1회 호출하면 이후 모든 TMP 텍스트에 적용된다.
        /// </summary>
        public static void EnsureTmpKoreanFallback()
        {
            if (_tmpFallbackRegistered)
                return;

            try
            {
                if (_tmpFontAsset == null)
                {
                    foreach (string candidate in KoreanFontCandidates)
                    {
                        Font osFont = Font.CreateDynamicFontFromOSFont(candidate, 90);
                        if (osFont == null)
                            continue;

                        TMPro.TMP_FontAsset asset = TMPro.TMP_FontAsset.CreateFontAsset(osFont);
                        if (asset != null)
                        {
                            asset.name = $"KoreanDynamic({candidate})";
                            _tmpFontAsset = asset;
                            break;
                        }
                    }
                }

                if (_tmpFontAsset != null && TMPro.TMP_Settings.fallbackFontAssets != null &&
                    !TMPro.TMP_Settings.fallbackFontAssets.Contains(_tmpFontAsset))
                {
                    TMPro.TMP_Settings.fallbackFontAssets.Add(_tmpFontAsset);
                }

                _tmpFallbackRegistered = true;
            }
            catch (System.Exception ex)
            {
                // 폰트 파이프라인 실패가 게임을 막지 않도록 보수적으로 처리한다.
                Debug.LogWarning($"[UiFontProvider] TMP 한글 폴백 등록 실패: {ex.Message}");
                _tmpFallbackRegistered = true;
            }
        }
    }
}
