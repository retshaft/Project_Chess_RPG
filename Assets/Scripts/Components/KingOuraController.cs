using UnityEngine;
using CheckmateRPG.Progression;

namespace CheckmateRPG.Components
{
    public class KingOuraController : MonoBehaviour
    {
        [SerializeField] private float _rotationSpeed = 30f;
        [SerializeField] private float _auraScale = 1.8f;
        [SerializeField] private float _auraHeight = 0.01f;
        
        private GameObject _auraObject;
        private Material _auraMaterial;
        
        public void Initialize(EdictData absoluteEdict)
        {
            if (_auraObject == null)
            {
                _auraObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(_auraObject.GetComponent<Collider>());
                _auraObject.transform.SetParent(transform, false);
                _auraObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                _auraObject.transform.localScale = new Vector3(_auraScale, _auraHeight, _auraScale);
                
                var renderer = _auraObject.GetComponent<Renderer>();
                
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                    
                _auraMaterial = new Material(shader);
                renderer.material = _auraMaterial;
            }
            
            UpdateAura(absoluteEdict);
        }
        
        public void UpdateAura(EdictData absoluteEdict)
        {
            if (_auraMaterial == null || _auraObject == null) return;
            
            if (absoluteEdict == null || absoluteEdict.Kind != EdictKind.Absolute)
            {
                _auraObject.SetActive(false);
                return;
            }
            
            _auraObject.SetActive(true);
            
            Color auraColor = absoluteEdict.Polarity switch
            {
                PolarityType.Order => new Color(0.2f, 0.6f, 1f, 0.5f),
                PolarityType.Chaos => new Color(1f, 0.2f, 0.2f, 0.5f),
                PolarityType.Neutral => new Color(0.8f, 0.8f, 0.8f, 0.5f),
                _ => new Color(1f, 1f, 1f, 0.5f)
            };
            
            _auraMaterial.color = auraColor;
            if (_auraMaterial.HasProperty("_BaseColor"))
            {
                _auraMaterial.SetColor("_BaseColor", auraColor);
            }
            
            // Standard shader transparent setup
            _auraMaterial.SetFloat("_Surface", 1); // 1 = Transparent
            _auraMaterial.SetFloat("_Mode", 3); // 3 = Transparent
            _auraMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _auraMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _auraMaterial.SetInt("_ZWrite", 0);
            _auraMaterial.DisableKeyword("_ALPHATEST_ON");
            _auraMaterial.EnableKeyword("_ALPHABLEND_ON");
            _auraMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _auraMaterial.renderQueue = 3000;
        }
        
        private void Update()
        {
            if (_auraObject != null && _auraObject.activeSelf)
            {
                _auraObject.transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.Self);
            }
        }
        
        private void OnDestroy()
        {
            if (_auraMaterial != null)
            {
                if (Application.isPlaying) Destroy(_auraMaterial);
                else DestroyImmediate(_auraMaterial);
            }
        }
    }
}
