using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.Components
{
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(CombatComponent))]
    public class UnitVisualController : MonoBehaviour
    {
        private HealthComponent _health;
        private CombatComponent _combat;
        private Renderer _renderer;
        private Color _originalColor;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _combat = GetComponent<CombatComponent>();
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null && _renderer.material != null)
            {
                _originalColor = _renderer.material.color;
                if (_renderer.material.HasProperty("_BaseColor"))
                {
                    _originalColor = _renderer.material.GetColor("_BaseColor");
                }
            }
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken += HandleDamageTaken;
                _health.OnHealed += HandleHealReceived;
            }
            if (_combat != null)
                _combat.OnAttackPerformed += HandleAttackPerformed;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken -= HandleDamageTaken;
                _health.OnHealed -= HandleHealReceived;
            }
            if (_combat != null)
                _combat.OnAttackPerformed -= HandleAttackPerformed;
        }

        private void HandleDamageTaken(float amount, Core.DamageType damageType)
        {
            if (amount <= 0) return;
            
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRedCoroutine());
            
            Color popupColor = damageType switch
            {
                Core.DamageType.Physical => new Color(1f, 0.2f, 0.2f), // Red for Physical
                Core.DamageType.Magical => new Color(0.2f, 0.8f, 1f),  // Cyan/Blue for Magical
                Core.DamageType.True => Color.white,                   // White for True
                _ => Color.red
            };

            SpawnDamagePopup(amount, popupColor);
        }

        private void HandleHealReceived(float amount)
        {
            if (amount <= 0) return;
            SpawnDamagePopup(amount, Color.green);
        }

        private void HandleAttackPerformed(GameObject target)
        {
            if (target != null)
                StartCoroutine(AttackTrailCoroutine(target.transform.position));
        }

        private IEnumerator FlashRedCoroutine()
        {
            if (_renderer == null || _renderer.material == null) yield break;
            
            SetMaterialColor(Color.red);
            yield return new WaitForSeconds(0.15f);
            SetMaterialColor(_originalColor);
        }

        private void SetMaterialColor(Color color)
        {
            _renderer.material.color = color;
            if (_renderer.material.HasProperty("_BaseColor"))
                _renderer.material.SetColor("_BaseColor", color);
        }

        private void SpawnDamagePopup(float amount, Color color)
        {
            // 연타(Multi-hit) 타격 수치가 서로 완전히 겹치지 않고 입체적으로 튀어오르도록 랜덤 오프셋 부여
            Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(0f, 0.4f), UnityEngine.Random.Range(-0.2f, 0.2f));
            Vector3 spawnPos = transform.position + Vector3.up * 1.5f + offset;

            if (CheckmateRPG.UI.FloatingTextManager.Instance != null)
            {
                CheckmateRPG.UI.FloatingTextManager.Instance.SpawnText(
                    spawnPos, 
                    Mathf.RoundToInt(amount).ToString(), 
                    color
                );
            }
            else
            {
                // Fallback if Manager is not in scene
                GameObject textGO = new GameObject("DamagePopup");
                textGO.transform.position = spawnPos;
#if UNITY_EDITOR || !UNITY_EDITOR
                var tmpro = textGO.AddComponent<TMPro.TextMeshPro>();
                tmpro.text = Mathf.RoundToInt(amount).ToString();
                tmpro.fontSize = 6f;
                tmpro.alignment = TMPro.TextAlignmentOptions.Center;
                tmpro.color = color;
                
                var font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null) tmpro.font = font;

                // Fire and forget simple animation (no pooling)
                StartCoroutine(SimpleAnimateDamagePopup(textGO, tmpro));
#endif
            }
        }

        private IEnumerator SimpleAnimateDamagePopup(GameObject popup, TMPro.TextMeshPro tmpro)
        {
            float duration = 0.8f;
            float elapsed = 0f;
            Vector3 startPos = popup.transform.position;
            Color startColor = tmpro.color;
            Camera mainCam = Camera.main;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                popup.transform.position = startPos + Vector3.up * (t * 1.5f);
                
                if (mainCam != null)
                {
                    popup.transform.forward = mainCam.transform.forward;
                }

                Color c = startColor;
                c.a = 1f - t;
                tmpro.color = c;
                yield return null;
            }
            Destroy(popup);
        }

        private IEnumerator AttackTrailCoroutine(Vector3 targetPos)
        {
            GameObject lineGO = new GameObject("AttackTrail");
            LineRenderer lr = lineGO.AddComponent<LineRenderer>();
            lr.startWidth = 0.1f;
            lr.endWidth = 0.05f;
            
            // Unity 기본 Sprite/Default 메테리얼을 가져옵니다
            Material mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;
            
            lr.startColor = Color.yellow;
            lr.endColor = new Color(1f, 0.5f, 0f, 0f);
            
            lr.SetPosition(0, transform.position + Vector3.up * 0.5f);
            lr.SetPosition(1, targetPos + Vector3.up * 0.5f);

            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / duration);
                lr.startColor = new Color(1f, 1f, 0f, alpha);
                lr.endColor = new Color(1f, 0.5f, 0f, 0f);
                yield return null;
            }
            Destroy(lineGO);
        }
    }
}
