using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Checkmate.Water
{
    /// <summary>Keeps screen-space water inputs enabled when water moves between scenes.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class ShallowWaterSceneSupport : MonoBehaviour
    {
        [SerializeField] bool configureAllCameras = true;

        void OnEnable() => ConfigureCameras();
        void OnValidate() => ConfigureCameras();

        void ConfigureCameras()
        {
            var cameras = configureAllCameras
                ? FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                : new[] { Camera.main };
            foreach (var camera in cameras)
            {
                if (!camera) continue;
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (!data) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.requiresDepthOption = CameraOverrideOption.On;
                data.requiresColorOption = CameraOverrideOption.On;
            }
        }
    }
}
