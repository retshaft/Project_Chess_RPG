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
            bgImage.color = new Color(0.025f, 0.035f, 0.05f, 0.94f);
            var card = BattleHudView.Rect(_uiContainer.transform, "ResultCard", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 660));
            BattleHudView.Panel(card, BattleHudView.Surface);
            BattleHudView.Panel(BattleHudView.Rect(card, "Accent", new Vector2(0, 1), Vector2.zero, new Vector2(860, 4)), BattleHudView.Cyan);
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
            mapBtnObj.GetComponent<Image>().color = BattleHudView.Cyan;
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
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
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

            return btnObj;
        }

        public void ShowResult(bool isWin)
        {
            _uiContainer.SetActive(true);
            if (isWin)
            {
                _resultText.text = "작전 성공";
                _resultText.color = BattleHudView.Cyan;

                int earnedTP = 50;
                string stageName = "Tactical Operation";
                string stageKey = stageName; // 저장 기록용 불변 키(스테이지 에셋 이름 권장)
                int firstClearBonusTP = 0;
                string unitRewardsStr = "None";

                if (CheckmateRPG.Progression.StageManager.Instance != null && CheckmateRPG.Progression.StageManager.Instance.CurrentStage != null)
                {
                    var stage = CheckmateRPG.Progression.StageManager.Instance.CurrentStage;
                    stageName = stage.StageName;
                    stageKey = !string.IsNullOrEmpty(stage.StageID) ? stage.StageID : stage.name;
                    earnedTP = stage.RewardTP > 0 ? stage.RewardTP : 50;
                    firstClearBonusTP = Mathf.Max(0, stage.FirstClearBonusTP);

                    if (stage.ClearRewards != null && stage.ClearRewards.Count > 0)
                    {
                        unitRewardsStr = "";
                        foreach (var reward in stage.ClearRewards)
                        {
                            if (reward == null) continue;
                            CheckmateRPG.Progression.ResonanceSystem.AddUnitResonance(reward.name);
                            unitRewardsStr += $"{reward.UnitName} ";
                        }
                    }
                }

                int newKingLevel = 1;
                bool firstClear = false;
                if (!_rewardGranted && CheckmateRPG.Progression.SaveManager.Instance != null && CheckmateRPG.Progression.SaveManager.Instance.CurrentData != null)
                {
                    var saveData = CheckmateRPG.Progression.SaveManager.Instance.CurrentData;
                    // R01a: 보상 지급은 BattleRewardService 단일 경로로 — 첫 클리어/반복을 구분해 기록하고,
                    // 같은 결과 인스턴스의 재호출로는 두 번 지급되지 않는다.
                    CheckmateRPG.Progression.BattleRewardService.TryGrantStageClearReward(
                        saveData, stageKey, earnedTP, firstClearBonusTP, out int grantedTP, out firstClear);
                    saveData.KingProgression.SuitLevel++;
                    newKingLevel = saveData.KingProgression.SuitLevel;
                    _rewardGranted = true;
                    CheckmateRPG.Progression.SaveManager.Instance.SaveGame();
                    Debug.Log($"[BattleResultUI] Saved game progress: +{grantedTP} TP (PlayTP: {saveData.KingProgression.PlayTP}), King Lv -> {newKingLevel}, firstClear={firstClear}");

                    if (_rewardSummaryText != null)
                    {
                        string clearTag = firstClear ? "첫 클리어" : "반복 클리어";
                        string bonusLine = firstClear && firstClearBonusTP > 0
                            ? $"\n• First Clear Bonus: <color=#FFD700>+{firstClearBonusTP} TP</color>"
                            : "";
                        _rewardSummaryText.text = $"<b>[ MISSION ACCOMPLISHED: {stageName} ]</b>\n\n" +
                                                  $"• Clear Type: <color=#FFD700>{clearTag}</color>\n" +
                                                  $"• Tactical Points: <color=#00FFFF>+{grantedTP} TP</color>{bonusLine}\n" +
                                                  $"• King Suit Advancement: <color=#FFD700>Lv. {newKingLevel}</color>\n" +
                                                  $"• Operator Synced: <color=#00FFCC>{unitRewardsStr}</color>";
                    }
                }
                else
                {
                    // 이미 지급됐거나 저장 시스템이 없는 경우 — 표시만 유지하고 재지급하지 않는다.
                    if (_rewardSummaryText != null && !_rewardGranted)
                    {
                        _rewardSummaryText.text = $"<b>[ MISSION ACCOMPLISHED: {stageName} ]</b>\n\n<color=#AAAAAA>저장 시스템을 찾을 수 없어 보상이 기록되지 않았습니다.</color>";
                    }
                }
            }
            else
            {
                _resultText.text = "작전 실패";
                _resultText.color = new Color(0.8f, 0.2f, 0.2f); // Red
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
            SceneManager.LoadScene("BattleScene");
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
