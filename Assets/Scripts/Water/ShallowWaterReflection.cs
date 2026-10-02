using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Checkmate.Water
{
    /// <summary>One bounded reflection render for visible water, with reusable camera and texture.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Renderer))]
    public sealed class ShallowWaterReflection : MonoBehaviour
    {
        [SerializeField, Range(128, 1024)] int resolution = 512;
        [SerializeField, Range(20, 250)] float reflectionDistance = 140;
        [SerializeField, Range(0.01f, 0.3f)] float clipOffset = 0.08f;
        static bool rendering;
        static readonly int EnabledId = Shader.PropertyToID("_PlanarEnabled");
        static readonly int TextureId = Shader.PropertyToID("_PlanarReflectionTex");
        static readonly int MatrixId = Shader.PropertyToID("_PlanarVP");
        Renderer surface;
        Camera reflectionCamera;
        RenderTexture texture;
        MaterialPropertyBlock properties;
        readonly Plane[] frustum = new Plane[6];
        readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();

        void OnEnable()
        {
            surface = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            RenderPipelineManager.beginCameraRendering += RenderReflection;
        }

        void SetAvailable(bool available)
        {
            if (!surface || properties == null) return;
            surface.GetPropertyBlock(properties);
            properties.SetFloat(EnabledId, available ? 1 : 0);
            if (available)
            {
                properties.SetTexture(TextureId, texture);
                properties.SetMatrix(MatrixId, GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true) * reflectionCamera.worldToCameraMatrix);
            }
            else properties.SetTexture(TextureId, Texture2D.blackTexture);
            surface.SetPropertyBlock(properties);
        }

        void Prepare(Camera source)
        {
            if (!reflectionCamera)
            {
                var go = new GameObject("Water Reflection (temporary)", typeof(Camera));
                go.hideFlags = HideFlags.HideAndDontSave;
                reflectionCamera = go.GetComponent<Camera>();
                reflectionCamera.enabled = false;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.antialiasing = AntialiasingMode.None;
            }
            int width = Mathf.Clamp(resolution,128,1024);
            int height = Mathf.Max(64, Mathf.RoundToInt(width / Mathf.Max(source.aspect,0.1f)));
            height = Mathf.Min(height,1024);
            if (texture && (texture.width != width || texture.height != height)) ReleaseTexture();
            if (!texture)
            {
                texture = new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf)
                {
                    name = "Water reflection", hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = true, autoGenerateMips = true, filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.Create();
            }
            reflectionCamera.CopyFrom(source);
            reflectionCamera.enabled = false;
            reflectionCamera.cameraType = CameraType.Reflection;
            reflectionCamera.allowMSAA = false;
            reflectionCamera.useOcclusionCulling = false;
            reflectionCamera.farClipPlane = Mathf.Min(source.farClipPlane,reflectionDistance);
            reflectionCamera.targetTexture = texture;
        }

        void RenderReflection(ScriptableRenderContext context, Camera source)
        {
            if (rendering || !surface || !surface.enabled || !source) return;
            SetAvailable(false);
            if (source.stereoEnabled || (source.cameraType != CameraType.SceneView && (source.cameraType != CameraType.Game || !source.CompareTag("MainCamera")))) return;
            var material = surface.sharedMaterial;
            if (!material || !material.HasProperty("_PlanarStrength") || material.GetFloat("_PlanarStrength") <= 0) return;
            if (source.transform.position.y <= transform.position.y + clipOffset) return;
            GeometryUtility.CalculateFrustumPlanes(source, frustum);
            if (!GeometryUtility.TestPlanesAABB(frustum, surface.bounds)) return;
            bool oldCulling = GL.invertCulling;
            bool oldHidden = surface.forceRenderingOff;
            rendering = true;
            try
            {
                Prepare(source);
                float waterY = transform.position.y;
                var mirror = Matrix4x4.identity;
                mirror.m11 = -1;
                mirror.m13 = 2*waterY;
                Vector3 position = source.transform.position;
                position.y = 2*waterY-position.y;
                reflectionCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.Reflect(source.transform.forward,Vector3.up),Vector3.Reflect(source.transform.up,Vector3.up)));
                reflectionCamera.worldToCameraMatrix = source.worldToCameraMatrix * mirror;
                Vector3 point = reflectionCamera.worldToCameraMatrix.MultiplyPoint(new Vector3(0,waterY+clipOffset,0));
                Vector3 normal = reflectionCamera.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
                var plane = new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal));
                reflectionCamera.ResetProjectionMatrix();
                reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(plane);
                surface.forceRenderingOff = true;
                GL.invertCulling = !oldCulling;
                request.destination = texture;
                RenderPipeline.SubmitRenderRequest(reflectionCamera,request);
                SetAvailable(true);
            }
            finally
            {
                GL.invertCulling = oldCulling;
                surface.forceRenderingOff = oldHidden;
                rendering = false;
            }
        }

        void ReleaseTexture()
        {
            if (!texture) return;
            if (reflectionCamera) reflectionCamera.targetTexture = null;
            texture.Release();
            Dispose(texture);
            texture = null;
        }

        static void Dispose(Object item)
        {
            if (!item) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= RenderReflection;
            SetAvailable(false);
            ReleaseTexture();
            if (reflectionCamera) Dispose(reflectionCamera.gameObject);
            reflectionCamera = null;
        }
    }
}

