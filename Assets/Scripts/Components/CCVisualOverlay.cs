using UnityEngine;
using CheckmateRPG.Core;

namespace CheckmateRPG.Components
{
    [RequireComponent(typeof(StatusEffectComponent))]
    public class CCVisualOverlay : MonoBehaviour
    {
        private StatusEffectComponent _statusEffects;
        private GameObject _freezeOverlay;
        private GameObject _paralysisOverlay;

        private void Awake()
        {
            _statusEffects = GetComponent<StatusEffectComponent>();
        }

        private void OnEnable()
        {
            if (_statusEffects != null)
            {
                _statusEffects.OnStatusApplied += HandleStatusApplied;
                _statusEffects.OnStatusRemoved += HandleStatusRemoved;
            }
        }

        private void OnDisable()
        {
            if (_statusEffects != null)
            {
                _statusEffects.OnStatusApplied -= HandleStatusApplied;
                _statusEffects.OnStatusRemoved -= HandleStatusRemoved;
            }
        }

        private void HandleStatusApplied(StatusEffectType type)
        {
            if (type == StatusEffectType.Freeze || type == StatusEffectType.FrozenBossDebuff)
            {
                if (_freezeOverlay == null)
                    _freezeOverlay = CreateFreezeOverlay();
            }
            else if (type == StatusEffectType.Paralysis)
            {
                if (_paralysisOverlay == null)
                    _paralysisOverlay = CreateParalysisOverlay();
            }
        }

        private void HandleStatusRemoved(StatusEffectType type)
        {
            if (type == StatusEffectType.Freeze || type == StatusEffectType.FrozenBossDebuff)
            {
                if (!_statusEffects.HasStatus(StatusEffectType.Freeze) && 
                    !_statusEffects.HasStatus(StatusEffectType.FrozenBossDebuff))
                {
                    if (_freezeOverlay != null)
                    {
                        Destroy(_freezeOverlay);
                        _freezeOverlay = null;
                    }
                }
            }
            else if (type == StatusEffectType.Paralysis)
            {
                if (_paralysisOverlay != null)
                {
                    Destroy(_paralysisOverlay);
                    _paralysisOverlay = null;
                }
            }
        }

        private GameObject CreateFreezeOverlay()
        {
            GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            overlay.name = "FreezeOverlay";
            overlay.transform.SetParent(transform, false);
            
            overlay.transform.localPosition = Vector3.up * 0.5f;
            overlay.transform.localScale = new Vector3(1.5f, 2f, 1.5f);

            Renderer r = overlay.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            
            // Standard Shader Transparent Setup
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            
            mat.color = new Color(0.5f, 0.8f, 1f, 0.6f);
            r.material = mat;

            Destroy(overlay.GetComponent<Collider>());
            return overlay;
        }

        private GameObject CreateParalysisOverlay()
        {
            GameObject overlay = new GameObject("ParalysisOverlay");
            overlay.transform.SetParent(transform, false);
            overlay.transform.localPosition = Vector3.up * 1f;

            GameObject quad1 = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject quad2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
            
            quad1.transform.SetParent(overlay.transform, false);
            quad2.transform.SetParent(overlay.transform, false);
            
            quad1.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            quad2.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            quad2.transform.localRotation = Quaternion.Euler(0, 90, 0);

            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            
            quad1.GetComponent<Renderer>().material = mat;
            quad2.GetComponent<Renderer>().material = mat;

            Destroy(quad1.GetComponent<Collider>());
            Destroy(quad2.GetComponent<Collider>());

            overlay.AddComponent<ParalysisBlinkEffect>();

            return overlay;
        }
    }

    public class ParalysisBlinkEffect : MonoBehaviour
    {
        private Renderer[] _renderers;
        private float _timer;
        private bool _isVisible = true;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= 0.1f)
            {
                _timer = 0f;
                _isVisible = !_isVisible;
                foreach (var r in _renderers)
                {
                    r.enabled = _isVisible;
                }
                
                if (_isVisible)
                {
                    transform.localRotation = Quaternion.Euler(
                        Random.Range(-20f, 20f), 
                        Random.Range(0f, 360f), 
                        Random.Range(-20f, 20f));
                }
            }
        }
    }
}
