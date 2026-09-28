using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace CheckmateRPG.UI
{
    public class BattleResultUI : MonoBehaviour
    {
        private GameObject _uiContainer;
        private TextMeshProUGUI _resultText;
        private TextMeshProUGUI _rewardSummaryText;
        private Button _restartButton;
        private Button _titleButton;

        // R01a: 같은 결과 인스턴스에서 ShowResult 재호출 시 보상이 재지급되지 않도록 하는 가드.
        private bool _rewardGranted;

        private void Awake()
        {
            // Build the UI from script if not set up in the inspector
            BuildProgrammaticUI();
            Hide();
        }

        private void BuildProgrammaticUI()
        {
            // Create Canvas
            var canvasObj = new GameObject("BattleResultCanvas");
            canvasObj.transform.SetParent(this.transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            _uiContainer = new GameObject("Container");
            _uiContainer.transform.SetParent(canvasObj.transform, false);
            var containerRect = _uiContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;
            var bgImage = _uiContainer.AddComponent<Image>();
            bgImage.color = new Color(0.098f, 0.106f, 0.11f, 0.96f);
            var card = BattleHudView.Rect(_uiContainer.transform, "ResultCard", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 660));
            BattleHudView.Panel(card, BattleHudView.Surface);
            BattleHudView.Panel(BattleHudView.Rect(card, "Accent", new Vector2(0, 1), Vector2.zero, new Vector2(860, 4)), OutgameStyle.AccentOrange);
            BattleHudView.Label(card, "Eyebrow", "CHECKMATE / OPERATION REPORT", new Vector2(48, -28), new Vector2(760, 28), 16, BattleHudView.Muted);

            // Title Text
            var textObj = new GameObject("ResultText");
            textObj.transform.SetParent(_uiContainer.transform, false);
            _resultText = textObj.AddComponent<TextMeshProUGUI>();
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0, 192);
            textRect.sizeDelta = new Vector2(760, 100);
            _resultText.font = UiFontProvider.GetTmpFont();
            _resultText.fontSize = 64;
            _resultText.raycastTarget = false;
            _resultText.alignment = TextAlignmentOptions.Center;
            _resultText.fontStyle = FontStyles.Bold;

            // Reward Summary Text Block
            var rewardTextObj = new GameObject("RewardSummaryText");
            rewardTextObj.transform.SetParent(_uiContainer.transform, false);
            _rewardSummaryText = rewardTextObj.AddComponent<TextMeshProUGUI>();
            var rTextRect = rewardTextObj.GetComponent<RectTransform>();
            rTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            rTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            rTextRect.anchoredPosition = new Vector2(0, -5);
            rTextRect.sizeDelta = new Vector2(740, 260);
            _rewardSummaryText.font = UiFontProvider.GetTmpFont();
            _rewardSummaryText.enableAutoSizing = true;
            _rewardSummaryText.fontSizeMin = 16;
            _rewardSummaryText.fontSizeMax = 24;
            _rewardSummaryText.raycastTarget = false;
            _rewardSummaryText.fontSize = 24;
            _rewardSummaryText.alignment = TextAlignmentOptions.Center;
            _rewardSummaryText.color = Color.white;
            _rewardSummaryText.text = "";

            // Restart Button
            var restartBtnObj = CreateButton("RestartButton", "재도전", new Vector2(-195, -242), _uiContainer.transform);
            _restartButton = restartBtnObj.GetComponent<Button>();
            _restartButton.onClick.AddListener(OnRestartClicked);

            // Title Button -> Map Button
            var mapBtnObj = CreateButton("MapButton", "작전 복귀", new Vector2(195, -242), _uiContainer.transform);
            OutgameStyle.ApplyCombatButtonStyle(mapBtnObj.GetComponent<Button>());
            mapBtnObj.GetComponentInChildren<TextMeshProUGUI>().color = BattleHudView.Surface;
            _titleButton = mapBtnObj.GetComponent<Button>();
            _titleButton.onClick.AddListener(OnMapClicked);
        }

        private GameObject CreateButton(string name, string text, Vector2 anchoredPos, Transform parent)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(350, 66);

            var img = btnObj.AddComponent<Image>();
            img.color = OutgameStyle.SurfaceCard;
            var btn = btnObj.AddComponent<Button>();

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.font = UiFontProvider.GetTmpFont();
            tmp.raycastTarget = false;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 28;
            tmp.color = Color.white;
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            OutgameStyle.ApplyLightButtonStyle(btn);
            return btnObj;
        }

        public void ShowResult(bool isWin)
        {
            _uiContainer.SetActive(true);
            if (isWin)
            {
                _resultText.text = "작전 성공";
                _resultText.color = OutgameStyle.TextWhite;

                if (!_rewardGranted)
                {
                    var manager = CheckmateRPG.Progression.SaveManager.Instance;
                    var stage = CheckmateRPG.Progression.StageManager.Instance?.CurrentStage;
                    if (manager != null && CheckmateRPG.Progression.BattleRewardService.TryGrantStageClearReward(manager.CurrentData, stage, () => UnityEngine.Random.value, out var reward))
                    {
                        _rewardGranted = true;
                        manager.SaveGame();
                        _rewardSummaryText.text = $"{stage.StageName}  /  {(reward.FirstClear ? "첫 클리어" : "반복 클리어")}\n\n에테르 크레딧  +{reward.Credits:N0}\n공명석  +{reward.Crystals:N0}\n작전기록  T1 {reward.T1} · T2 {reward.T2} · T3 {reward.T3} · T4 {reward.T4}";
                    }
                    else _rewardSummaryText.text = "유효한 출격 기록이 없거나 이미 정산된 작전입니다.\n보상은 중복 지급되지 않습니다.";
                }
            }
            else
            {
                var save = CheckmateRPG.Progression.SaveManager.Instance;
                if (save != null) { CheckmateRPG.Progression.EconomyService.EndSortie(save.CurrentData); save.SaveGame(); }
                _resultText.text = "작전 실패";
                _resultText.color = OutgameStyle.AccentRed;
                if (_rewardSummaryText != null)
                {
                    _rewardSummaryText.text = "획득한 보상이 없습니다.\n\n편성과 스킬을 점검하고 다시 도전하세요.\n아군 킹을 보호하며 공격 위치를 확보하세요.";
                }
            }
        }

        public void Hide()
        {
            _uiContainer.SetActive(false);
        }

        private void OnRestartClicked()
        {
            if (!TryPaySortie()) return;
            SceneManager.LoadScene("BattleScene");
        }

        private bool TryPaySortie()
        {
            var save = CheckmateRPG.Progression.SaveManager.EnsureInstance();
            var stage = CheckmateRPG.Progression.StageManager.Instance?.CurrentStage;
            if (stage == null) { _rewardSummaryText.text = "작전 복귀 후 작전을 선택하세요."; return false; }
            if (!stage.TryGetReadiness(out string readiness)) { _rewardSummaryText.text = readiness; return false; }
            if (!CheckmateRPG.Progression.EconomyService.TryBeginSortie(save.CurrentData, stage, DateTime.UtcNow, out string reason))
            { _rewardSummaryText.text = reason + "\n작전 복귀 후 전력을 충전하세요."; return false; }
            save.SaveGame(); return true;
        }
        private void OnMapClicked()
        {
            Debug.Log("[BattleResultUI] Transitioning to OutgameScene...");
            try
            {
                SceneManager.LoadScene("OutgameScene");
            }
            catch (Exception)
            {
                Debug.LogWarning("[BattleResultUI] OutgameScene not found. Reloading BattleScene instead.");
                SceneManager.LoadScene("BattleScene");
            }
        }
    }
}
