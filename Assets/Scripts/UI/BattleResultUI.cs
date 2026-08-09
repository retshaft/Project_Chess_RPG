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
            canvasObj.transform.SetParent(this.transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            _uiContainer = new GameObject("Container");
            _uiContainer.transform.SetParent(canvasObj.transform, false);
            var containerRect = _uiContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;
            var bgImage = _uiContainer.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.8f);

            // Title Text
            var textObj = new GameObject("ResultText");
            textObj.transform.SetParent(_uiContainer.transform, false);
            _resultText = textObj.AddComponent<TextMeshProUGUI>();
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0, 100);
            textRect.sizeDelta = new Vector2(600, 200);
            _resultText.fontSize = 80;
            _resultText.alignment = TextAlignmentOptions.Center;
            _resultText.fontStyle = FontStyles.Bold;

            // Reward Summary Text Block
            var rewardTextObj = new GameObject("RewardSummaryText");
            rewardTextObj.transform.SetParent(_uiContainer.transform, false);
            _rewardSummaryText = rewardTextObj.AddComponent<TextMeshProUGUI>();
            var rTextRect = rewardTextObj.GetComponent<RectTransform>();
            rTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            rTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            rTextRect.anchoredPosition = new Vector2(0, 0);
            rTextRect.sizeDelta = new Vector2(600, 140);
            _rewardSummaryText.fontSize = 24;
            _rewardSummaryText.alignment = TextAlignmentOptions.Center;
            _rewardSummaryText.color = Color.white;
            _rewardSummaryText.text = "";

            // Restart Button
            var restartBtnObj = CreateButton("RestartButton", "Restart", new Vector2(0, -90), _uiContainer.transform);
            _restartButton = restartBtnObj.GetComponent<Button>();
            _restartButton.onClick.AddListener(OnRestartClicked);

            // Title Button -> Map Button
            var mapBtnObj = CreateButton("MapButton", "Back to Map", new Vector2(0, -170), _uiContainer.transform);
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
            rect.sizeDelta = new Vector2(250, 60);

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var btn = btnObj.AddComponent<Button>();

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
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
                _resultText.text = "VICTORY";
                _resultText.color = new Color(0.2f, 0.8f, 0.2f); // Green

                int earnedTP = 50;
                string stageName = "Tactical Operation";
                string unitRewardsStr = "None";

                if (CheckmateRPG.Progression.StageManager.Instance != null && CheckmateRPG.Progression.StageManager.Instance.CurrentStage != null)
                {
                    var stage = CheckmateRPG.Progression.StageManager.Instance.CurrentStage;
                    stageName = stage.StageName;
                    earnedTP = stage.RewardTP > 0 ? stage.RewardTP : 50;

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
                if (CheckmateRPG.Progression.SaveManager.Instance != null && CheckmateRPG.Progression.SaveManager.Instance.CurrentData != null)
                {
                    var saveData = CheckmateRPG.Progression.SaveManager.Instance.CurrentData;
                    saveData.TacticalPoints += earnedTP;
                    saveData.KingProgression.PlayTP += earnedTP;
                    saveData.KingProgression.SuitLevel++;
                    newKingLevel = saveData.KingProgression.SuitLevel;
                    CheckmateRPG.Progression.SaveManager.Instance.SaveGame();
                    Debug.Log($"[BattleResultUI] Saved game progress: +{earnedTP} TP (PlayTP: {saveData.KingProgression.PlayTP}), King Lv -> {newKingLevel}");
                }

                if (_rewardSummaryText != null)
                {
                    _rewardSummaryText.text = $"<b>[ MISSION ACCOMPLISHED: {stageName} ]</b>\n\n" +
                                              $"• Tactical Points: <color=#00FFFF>+{earnedTP} TP</color>\n" +
                                              $"• King Suit Advancement: <color=#FFD700>Lv. {newKingLevel}</color>\n" +
                                              $"• Operator Synced: <color=#00FFCC>{unitRewardsStr}</color>";
                }
            }
            else
            {
                _resultText.text = "DEFEAT";
                _resultText.color = new Color(0.8f, 0.2f, 0.2f); // Red
                if (_rewardSummaryText != null)
                {
                    _rewardSummaryText.text = "<color=#AAAAAA>No rewards acquired.\nAdjust your Synchro Edicts and Operator Resonance to overcome the tactical impasse.</color>";
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
