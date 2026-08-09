using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Core;
using CheckmateRPG.Components;
using CheckmateRPG.Units;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// 유저(디자이너)가 '칙령(EdictData)'에서 HasActiveSkill을 True로 설정하고 
    /// 전투 전 장착했을 때에만 선택적으로 활성화되는 [액티브 칙령 해방] HUD 버튼입니다. 
    /// 기본 기능이 아니며, 장착된 액티브 칙령이 없을 경우 전투 화면에 전혀 나타나지 않습니다.
    /// </summary>
    public class KingSynchroUI : MonoBehaviour
    {
        private static KingSynchroUI _instance;
        public static KingSynchroUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("KingSynchroUI_Auto");
                    _instance = go.AddComponent<KingSynchroUI>();
                    _instance.BuildUI();
                }
                return _instance;
            }
        }

        private Button _synchroButton;
        private TextMeshProUGUI _buttonText;
        private Image _buttonBG;

        private EdictData _equippedActiveEdict;
        private bool _isOverdriveActive = false;
        private float _cooldownRemaining = 0f;

        private Light _mainLight;
        private Color _origLightColor;
        private float _origLightIntensity;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (_synchroButton == null) BuildUI();
        }

        private void Start()
        {
            // 디자이너(유저)가 기획하고 플레이어가 장착한 액티브 칙령이 있는지 검사
            if (DeckManager.Instance != null)
            {
                _equippedActiveEdict = DeckManager.Instance.GetEquippedActiveEdict();
            }

            // 액티브 기능이 없는 칙령이거나 장착되지 않은 경우 버튼 전체를 숨김 처리 (기본 기능 아님)
            if (_equippedActiveEdict == null || !_equippedActiveEdict.HasActiveSkill)
            {
                if (_synchroButton != null) _synchroButton.gameObject.SetActive(false);
                return;
            }

            if (_synchroButton != null)
            {
                _synchroButton.gameObject.SetActive(true);
                if (_buttonText != null) _buttonText.text = $"👑 {_equippedActiveEdict.ActiveSkillName}";
            }
        }

        private void Update()
        {
            if (_equippedActiveEdict == null || !_equippedActiveEdict.HasActiveSkill || _isOverdriveActive) return;

            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
                if (_buttonText != null) _buttonText.text = $"충전 중 ({Mathf.CeilToInt(_cooldownRemaining)}s)";
                if (_buttonBG != null) _buttonBG.color = Color.gray;
                if (_synchroButton != null) _synchroButton.interactable = false;
            }
            else
            {
                if (_buttonText != null && _buttonText.text.StartsWith("충전 중"))
                {
                    _buttonText.text = $"👑 {_equippedActiveEdict.ActiveSkillName}";
                    if (_buttonBG != null) _buttonBG.color = new Color(0.85f, 0.25f, 0.15f, 1f);
                    if (_synchroButton != null) _synchroButton.interactable = true;
                }
            }
        }

        private void OnSynchroClicked()
        {
            if (_equippedActiveEdict == null || !_equippedActiveEdict.HasActiveSkill || _isOverdriveActive || _cooldownRemaining > 0f) 
                return;
            
            StartCoroutine(OverdriveRoutine());
        }

        private IEnumerator OverdriveRoutine()
        {
            _isOverdriveActive = true;
            float duration = _equippedActiveEdict.Duration;
            float cooldown = _equippedActiveEdict.Cooldown;

            Debug.Log($"[KingSynchroUI] 👑 Edict Liberation Activated: {_equippedActiveEdict.ActiveSkillName} (Duration: {duration}s)");

            if (_synchroButton != null) _synchroButton.interactable = false;
            if (_buttonBG != null) _buttonBG.color = new Color(1f, 0.7f, 0.1f, 1f); // Active Gold color

            // 디자이너가 칙령 에셋에 설정한 AP 보급 효과 적용
            if (_equippedActiveEdict.InstantAPBonus > 0f && APManager.Instance != null)
            {
                APManager.Instance.AddAP(_equippedActiveEdict.InstantAPBonus, APSource.Refund);
            }

            // 디자이너가 옵션으로 활성화한 경우에만 조명 오버드라이브 연출 적용
            if (_equippedActiveEdict.UseOverdriveLighting)
            {
                var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l != null && l.type == LightType.Directional && l.isActiveAndEnabled)
                    {
                        _mainLight = l;
                        _origLightColor = l.color;
                        _origLightIntensity = l.intensity;
                        l.color = new Color(1f, 0.65f, 0.4f);
                        l.intensity = _origLightIntensity * 1.35f;
                        break;
                    }
                }
            }

            // 디자이너가 칙령 에셋에 설정한 소대 버프 부여
            if (_equippedActiveEdict.ApplySquadStatusEffect)
            {
                var brains = UnityEngine.Object.FindObjectsByType<UnitBrain>(FindObjectsSortMode.None);
                foreach (var brain in brains)
                {
                    if (brain != null && brain.TryGetComponent(out TeamComponent teamComp) && !teamComp.IsEnemy && !brain.IsDead)
                    {
                        if (brain.TryGetComponent(out StatusEffectComponent status))
                        {
                            status.ApplyStatusEffect(_equippedActiveEdict.SquadStatusEffect, duration, 1);
                        }
                        if (FloatingTextManager.Instance != null)
                        {
                            FloatingTextManager.Instance.SpawnText(brain.transform.position + Vector3.up * 2f, $"{_equippedActiveEdict.ActiveSkillName}!", Color.yellow);
                        }
                    }
                }
            }

            // 지속 시간 카운트다운
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float rem = duration - elapsed;
                if (_buttonText != null) _buttonText.text = $"🔥 효과 지속 ({rem:0.0}s)";
                yield return null;
            }

            // 조명 복구
            if (_mainLight != null)
            {
                _mainLight.color = _origLightColor;
                _mainLight.intensity = _origLightIntensity;
                _mainLight = null;
            }

            _isOverdriveActive = false;
            _cooldownRemaining = cooldown;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("KingSynchroCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // HUD 레이어
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            var buttonGO = new GameObject("SynchroButton");
            buttonGO.transform.SetParent(canvasGO.transform, false);
            var btnRect = buttonGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 1f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.pivot = new Vector2(1f, 1f);
            btnRect.anchoredPosition = new Vector2(-20f, -20f);
            btnRect.sizeDelta = new Vector2(220f, 60f);

            _buttonBG = buttonGO.AddComponent<Image>();
            _buttonBG.color = new Color(0.85f, 0.25f, 0.15f, 1f);
            _synchroButton = buttonGO.AddComponent<Button>();
            _synchroButton.targetGraphic = _buttonBG;
            _synchroButton.onClick.AddListener(OnSynchroClicked);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            var txtRect = textGO.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            _buttonText = textGO.AddComponent<TextMeshProUGUI>();
            _buttonText.text = "👑 킹 싱크로 해방";
            _buttonText.fontSize = 22f;
            _buttonText.fontStyle = FontStyles.Bold;
            _buttonText.color = Color.white;
            _buttonText.alignment = TextAlignmentOptions.Center;

            // 초기 생성 시 일단 비활성화 (Start에서 액티브 칙령 장착 유무를 판정)
            _synchroButton.gameObject.SetActive(false);
        }
    }
}
