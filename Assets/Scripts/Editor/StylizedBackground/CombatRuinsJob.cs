using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Checkmate.Background.Editor
{
    // Explicit file request bridge for editor-only scene operations; no runtime scene mutations.
    [InitializeOnLoad]
    public static class CombatRuinsJob
    {
        const string Request = "Captures/CombatRuins/job.request";
        const string TestKey = "Checkmate.RuinArt.Validation";
        static string ValidationPath => SessionState.GetBool("Checkmate.RuinArt.ConnectionsValidation", false) ? "Captures/CombatRuins/connections-validation.txt" : SessionState.GetBool("Checkmate.RuinArt.SkyValidation", false) ? "Captures/CombatRuins/sky-validation.txt" : SessionState.GetBool("Checkmate.RuinArt.VolumeValidation", false) ? "Captures/CombatRuins/volume-validation.txt" : "Captures/CombatRuins/validation.txt";
        static double testStart;
        static bool captured;
        static CombatRuinsJob()
        {
            EditorApplication.delayCall += RunRequested;
            // Keep the bridge usable after the editor has already loaded the script (v2).
            // The callback is editor-only and returns immediately when no request exists.
            EditorApplication.update += RunRequested;
            EditorApplication.playModeStateChanged += OnPlayState;
            Application.logMessageReceived += OnLog;
        }
        static void RunRequested()
        {
            if (!File.Exists(Request)) return;
            string command;
            try { command = File.ReadAllText(Request).Trim(); }
            catch (IOException) { return; } // A request writer can still hold the file this frame.
            EditorApplication.update -= RunRequested;
            File.Delete(Request);
            try
            {
                var scene = SceneManager.GetActiveScene();
                var report = "project=" + Application.dataPath + "\nscene=" + scene.path + "\nplay=" + EditorApplication.isPlaying + "\ndirty=" + scene.isDirty + "\n";
                report += string.Join("\n", scene.GetRootGameObjects().Select(g => g.name + " | children=" + g.transform.childCount));
                report += "\nRenderers:\n" + string.Join("\n", scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Select(r => r.name + " | " + r.bounds + " | " + AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>()?.sharedMesh)));
                if (command == "capture")
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CombatRuinsArt.Root + "Prefabs" }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                        try
                        {
                            var rs = go.GetComponentsInChildren<Renderer>();
                            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); report += "\nASSET " + path + " " + b; }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(go); }
                    }
                    Capture("before.png");
                }
                else if (command == "validate")
                {
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Already playing.");
                    CombatRuinsArt.Prepare();
                    File.WriteAllText("Captures/CombatRuins/validation.txt", "Project: " + Application.dataPath + "\nScene: " + scene.path + "\n");
                    Capture("applied.png");
                    CaptureDetail("detail-decals-on.png");
                    var projectors = UnityEngine.Object.FindObjectsByType<DecalProjector>(FindObjectsSortMode.None);
                    try { foreach (var p in projectors) p.enabled = false; CaptureDetail("detail-decals-off.png"); }
                    finally { foreach (var p in projectors) p.enabled = true; }
                    report += "\nDECAL_COUNT=" + projectors.Length;
                    foreach (var shader in AssetDatabase.FindAssets("t:Shader", new[] { CombatRuinsArt.Root + "ShaderGraphs" }).Select(g => AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(g))))
                    {
                        File.AppendAllText(ValidationPath, shader.name + " errors=" + ShaderUtil.ShaderHasError(shader) + "\n");
                        foreach (var m in ShaderUtil.GetShaderMessages(shader)) File.AppendAllText(ValidationPath, m.severity + ": " + m.message + "\n");
                    }
                    SessionState.SetBool(TestKey, true);
                    EditorApplication.isPlaying = true;
                }
                else if (command == "volume")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Test/BattleScene.unity")
                        throw new InvalidOperationException("Open BattleScene in Edit Mode.");
                    const string backup = "Captures/CombatRuins/volume-before.unity";
                    if (!File.Exists(backup) && !UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, backup, true))
                        throw new IOException("Could not preserve the current scene.");
                    Capture("volume-before.png");
                    CombatRuinsVolume.Apply();
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                    Capture("volume-after.png");
                }
                else if (command == "volume-capture") Capture("volume-after.png");
                else if (command == "sky-apply") CombatRuinsAtmosphere.Apply();
                else if (command == "connections-finish") CombatRuinsConnections.FinishJoints();
                else if (command == "connections-apply") CombatRuinsConnections.Apply();
                else if (command == "connections-capture") { CombatRuinsConnections.Capture("connections-after.png",false); CombatRuinsConnections.Capture("connections-top-after.png",true); Capture("connections-game.png"); }
                else if (command == "connections-inspect") CombatRuinsConnections.Inspect();
                else if (command == "sky-city-build") CombatRuinsAtmosphere.RebuildCity();
                else if (command == "sky-capture")
                {
                    CombatRuinsAtmosphere.Capture("sky-after-4k.png", true, false);
                    CombatRuinsAtmosphere.Capture("sky-clouds-4k.png", true, true);
                }
                else if (command == "volume-overview") CombatRuinsVolume.CaptureOverview();
                else if (command == "volume-play" || command == "sky-play" || command == "connections-play")
                {
                    if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Test/BattleScene.unity")
                        throw new InvalidOperationException("Open BattleScene in Edit Mode.");
                    SessionState.SetBool("Checkmate.RuinArt.ConnectionsValidation", command == "connections-play");
                    SessionState.SetBool("Checkmate.RuinArt.VolumeValidation", true);
                    SessionState.SetBool("Checkmate.RuinArt.SkyValidation", command == "sky-play");
                    File.WriteAllText(ValidationPath, "Volume validation\nProject: " + Application.dataPath + "\nScene: " + scene.path + "\n");
                    SessionState.SetBool(TestKey, true);
                    EditorApplication.isPlaying = true;
                }
                else if (command == "prepare") CombatRuinsArt.Prepare();
                else if (command == "apply")
                {
                    CombatRuinsArt.Apply();
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                    Capture("applied.png");
                }
                else if (command == "mood")
                {
                    CombatRuinsArt.ApplyMood();
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                    Capture("mood.png");
                }
                else if (command != "inspect") throw new InvalidOperationException("Unsupported request: " + command);
                File.WriteAllText("Captures/CombatRuins/job-result.txt", report);
                Debug.Log("CombatRuins job completed: " + command);
            }
            catch (Exception e) { File.WriteAllText("Captures/CombatRuins/job-result.txt", e.ToString()); Debug.LogException(e); }
            finally { EditorApplication.update += RunRequested; }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(TestKey, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                File.AppendAllText(ValidationPath, type + ": " + message + "\n" + stack + "\n");
        }

        static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(TestKey, false)) return;
            File.AppendAllText(ValidationPath, state + "\n");
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                testStart = EditorApplication.timeSinceStartup;
                captured = false;
                EditorApplication.update += TickTest;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= TickTest;
                SessionState.SetBool(TestKey, false);
                File.AppendAllText(ValidationPath, "PlayModeStopped=" + !EditorApplication.isPlaying + "\n");
                SessionState.SetBool("Checkmate.RuinArt.ConnectionsValidation", false);
                SessionState.SetBool("Checkmate.RuinArt.VolumeValidation", false);
                SessionState.SetBool("Checkmate.RuinArt.SkyValidation", false);
            }
        }

        static void TickTest()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= TickTest; return; }
            if (EditorApplication.timeSinceStartup - testStart < 5) return;
            try
            {
                if (!captured) { captured = true; Capture(SessionState.GetBool("Checkmate.RuinArt.ConnectionsValidation", false) ? "connections-play.png" : SessionState.GetBool("Checkmate.RuinArt.SkyValidation", false) ? "sky-play.png" : SessionState.GetBool("Checkmate.RuinArt.VolumeValidation", false) ? "volume-play.png" : "play.png"); }
            }
            catch (Exception e) { File.AppendAllText(ValidationPath, e + "\n"); }
            finally { EditorApplication.update -= TickTest; EditorApplication.isPlaying = false; }
        }

        static void CaptureDetail(string filename)
        {
            var camera = Camera.main;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            float fov = camera.fieldOfView;
            try
            {
                camera.transform.position = new Vector3(-7.8f, 5.2f, -8);
                camera.transform.LookAt(new Vector3(-2.7f, 1.05f, -1.8f));
                camera.fieldOfView = 39;
                Capture(filename);
            }
            finally { camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov; }
        }

        public static void Capture(string filename, int width = 1440, int height = 900)
        {
            var camera = Camera.main;
            if (!camera) throw new InvalidOperationException("Main camera missing.");
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            float aspect = camera.aspect;
            try
            {
                camera.aspect = (float)width / height;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes("Captures/CombatRuins/" + filename, image.EncodeToPNG());
            }
            finally
            {
                camera.aspect = aspect;
                RenderTexture.active = old;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
