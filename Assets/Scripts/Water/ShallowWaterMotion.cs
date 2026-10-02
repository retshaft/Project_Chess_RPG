using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Checkmate.Water
{
    /// <summary>Continuous, frame-rate independent motion; writes only renderer-local shader inputs.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Renderer))]
    public sealed class ShallowWaterMotion : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 2f), Tooltip("Overall motion speed. Colour and clarity are unchanged.")]
        float motionSpeed = 0.75f;
        [SerializeField, Range(0f, 0.35f), Tooltip("Slow variation in current speed and cross-current drift.")]
        float currentVariation = 0.18f;
        [SerializeField, Range(8f, 90f)] float currentPeriod = 32f;
        [SerializeField, Range(0f, 0.2f)] float swellVariation = 0.08f;
        [SerializeField] bool previewInEditor = true;

        static readonly int EnabledId = Shader.PropertyToID("_MotionEnabled");
        static readonly int PhasesId = Shader.PropertyToID("_MotionPhases");
        static readonly int FlowAId = Shader.PropertyToID("_MotionFlowA");
        static readonly int FlowBId = Shader.PropertyToID("_MotionFlowB");
        static readonly int FlowCId = Shader.PropertyToID("_MotionFlowC");
        static readonly int SwellId = Shader.PropertyToID("_MotionSwell");
        static readonly int LayerAId = Shader.PropertyToID("_LayerA");
        static readonly int LayerBId = Shader.PropertyToID("_LayerB");
        static readonly int LayerCId = Shader.PropertyToID("_LayerC");
        static readonly int FrequencyId = Shader.PropertyToID("_WaveFrequency");
        static readonly int SpeedId = Shader.PropertyToID("_WaveSpeed");
        Renderer surface;
        MaterialPropertyBlock properties;
        double clock, lastTime;
        bool hasTime;

        void OnEnable()
        {
            surface = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            hasTime = false;
            RenderPipelineManager.beginCameraRendering += BeforeRender;
        }

        // Integral of a gently varying speed. Never multiply an evolving speed by absolute time.
        public static double FlowClock(double time, double period, double variation, double phase)
        {
            double w = 2*Math.PI / Math.Max(8, period);
            return time + variation*(Math.Sin(time*w+phase)-Math.Sin(phase))/w;
        }

        public static Vector4 FlowOffset(double time, Vector4 layer, double period, double variation, double phase)
        {
            double length = Math.Sqrt(layer.y*layer.y + layer.z*layer.z);
            double x = length > 0.00001 ? layer.y/length : 1;
            double z = length > 0.00001 ? layer.z/length : 0;
            double along = layer.w * FlowClock(time,period,variation,phase);
            double cross = layer.w * (FlowClock(time,period*1.37,variation,phase+1.7)-time);
            // Repeat UVs are periodic: wrapping keeps long-running sessions numerically stable.
            return new Vector4(Repeat01(x*along-z*cross),Repeat01(z*along+x*cross),0,0);
        }

        static float Repeat01(double value) => (float)(value-Math.Floor(value));
        public static float WavePhase(double time, double k, double speed, double period, double variation)
        {
            double phase = FlowClock(time,period,variation,0)*speed*Math.Sqrt(9.81*Math.Max(k,0.0001));
            return (float)(phase-Math.Floor(phase/(2*Math.PI))*(2*Math.PI));
        }

        void BeforeRender(ScriptableRenderContext context, Camera camera)
        {
            if (!surface || !surface.enabled || camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview) return;
            var material = surface.sharedMaterial;
            if (!material || !material.HasProperty(EnabledId)) return;
            double now = Application.isPlaying ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
            if (!hasTime) { lastTime = now; hasTime = true; }
            // Once per timestamp, not once per camera. Scaled game time respects pause/timeScale.
            double dt = Math.Max(0,now-lastTime);
            lastTime = now;
            if (Application.isPlaying || previewInEditor) clock += dt*motionSpeed;
            float k = material.GetFloat(FrequencyId);
            float speed = material.GetFloat(SpeedId);
            surface.GetPropertyBlock(properties); // Preserve planar reflection and other renderer overrides.
            properties.SetFloat(EnabledId,1);
            properties.SetVector(PhasesId,new Vector4(
                WavePhase(clock,k,speed,currentPeriod,currentVariation),
                WavePhase(clock,k*1.73,speed,currentPeriod*1.11,currentVariation),
                WavePhase(clock,k*2.91,speed,currentPeriod*0.83,currentVariation),
                WavePhase(clock,k*4.37,speed,currentPeriod*1.37,currentVariation)));
            properties.SetVector(FlowAId,FlowOffset(clock,material.GetVector(LayerAId),currentPeriod,currentVariation,0));
            properties.SetVector(FlowBId,FlowOffset(clock,material.GetVector(LayerBId),currentPeriod*1.11,currentVariation,2.1));
            properties.SetVector(FlowCId,FlowOffset(clock,material.GetVector(LayerCId),currentPeriod*0.83,currentVariation,4.3));
            properties.SetFloat(SwellId,1-swellVariation*0.5f*(1-(float)Math.Sin(clock*2*Math.PI/(currentPeriod*1.63))));
            surface.SetPropertyBlock(properties);
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeRender;
            if (!surface || properties == null) return;
            surface.GetPropertyBlock(properties);
            properties.SetFloat(EnabledId,0);
            surface.SetPropertyBlock(properties);
        }
    }
}
