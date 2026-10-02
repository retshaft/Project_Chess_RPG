using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Checkmate.Background.Editor
{
    public static class CombatRuinsVolume
    {
        public const string ProfilePath = "Assets/Environment/CombatRuins/RuinsMoodVolume.asset";
        const string RootName = "Combat Ruins - Mood Volume";

        [MenuItem("Tools/Checkmate/Background/Apply Ruins Mood Volume")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Test/BattleScene.unity")
                throw new InvalidOperationException("Open BattleScene in Edit Mode before applying the volume.");
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (!camera || !camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                throw new InvalidOperationException("BattleScene requires its existing URP Main Camera.");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            // Neutral preserves the existing stone, water and semi-toon character palette.
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(.3f);
            color.contrast.Override(8f);
            color.saturation.Override(-3f);
            var balance = Get<WhiteBalance>(profile);
            balance.temperature.Override(-3f);
            balance.tint.Override(1f);
            var split = Get<SplitToning>(profile);
            split.shadows.Override(new Color(.485f, .502f, .515f));
            split.highlights.Override(new Color(.525f, .513f, .49f));
            split.balance.Override(5f);
            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(.18f);
            bloom.scatter.Override(.55f);
            bloom.tint.Override(new Color(1f, .97f, .91f));
            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(.1f);
            vignette.smoothness.Override(.45f);
            // Explicitly keep tactical tiles and character silhouettes sharp.
            Get<DepthOfField>(profile).mode.Override(DepthOfFieldMode.Off);
            Get<MotionBlur>(profile).intensity.Override(0f);
            Get<ChromaticAberration>(profile).intensity.Override(0f);
            Get<FilmGrain>(profile).intensity.Override(0f);

            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (!root)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create ruins mood volume");
            }
            var volume = root.GetComponent<Volume>();
            if (!volume) volume = Undo.AddComponent<Volume>(root);
            Undo.RecordObject(volume, "Apply ruins mood volume");
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            Undo.RecordObject(cameraData, "Enable ruins camera post processing");
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask |= 1 << root.layer;
            Undo.RecordObject(camera, "Enable ruins camera HDR");
            camera.allowHDR = true;
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("Ruins mood volume applied: Neutral, restrained bloom, cool shadows and warm highlights.");
        }

        public static void CaptureOverview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/Scenes/Test/BattleScene.unity")
                throw new InvalidOperationException("Capture the overview in BattleScene Edit Mode.");
            var source = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.camera : null;
            var camera = Camera.main;
            if (!source || !camera) throw new InvalidOperationException("Scene and Main Camera required.");
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var fov = camera.fieldOfView;
            var orthographic = camera.orthographic;
            var size = camera.orthographicSize;
            var data = camera.GetUniversalAdditionalCameraData();
            bool post = data.renderPostProcessing;
            try
            {
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.fieldOfView = source.fieldOfView;
                camera.orthographic = source.orthographic;
                camera.orthographicSize = source.orthographicSize;
                data.renderPostProcessing = false;
                CombatRuinsJob.Capture("volume-overview-before.png");
                data.renderPostProcessing = true;
                CombatRuinsJob.Capture("volume-overview-after.png");
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov;
                camera.orthographic = orthographic;
                camera.orthographicSize = size;
                data.renderPostProcessing = post;
            }
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(false);
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            Undo.RecordObject(component, "Configure ruins mood profile");
            return component;
        }
    }
}
