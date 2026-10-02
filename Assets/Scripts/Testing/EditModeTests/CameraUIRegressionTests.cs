using CheckmateRPG.Core;
using CheckmateRPG.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.Tests
{
    public class CameraUIRegressionTests
    {
        [Test] public void InspectionSwitchAndTargetingRestoreOriginalSpeedAndCamera()
        {
            float original = Time.timeScale;
            var output = new GameObject("Camera fixture").AddComponent<Camera>();
            var root = new GameObject("Selection fixture");
            var target = new GameObject("Target fixture");
            try
            {
                Time.timeScale = 2;
                output.transform.SetPositionAndRotation(new Vector3(8,16,-3), Quaternion.Euler(60,0,0));
                var director = root.AddComponent<BattleCameraDirector>(); director.Initialise(output,null);
                var pose = root.transform.GetChild(0).position;
                director.BeginInspection(target.transform);
                Assert.AreEqual(.3f, Time.timeScale, .0001f);
                target.transform.position = Vector3.right * 3;
                director.BeginInspection(target.transform);
                Assert.AreEqual(.3f, Time.timeScale, .0001f, "Switching units must not compound slowdown.");
                director.ShowTargetingBoard();
                Assert.IsTrue(director.IsInspecting);
                Assert.AreEqual(pose, root.transform.GetChild(0).position);
                director.EndInspection(); director.EndInspection();
                Assert.AreEqual(2, Time.timeScale);
                director.BeginInspection(target.transform);
                // EditMode does not dispatch MonoBehaviour disable messages automatically.
                typeof(BattleCameraDirector).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(director, null);
                Assert.AreEqual(2, Time.timeScale, "Disable/scene exit restores the previous speed.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(output.gameObject); Time.timeScale=original; }
        }

        [Test] public void ReopeningModalDoesNotDuplicateCanvasOrRaycaster()
        {
            var root = new GameObject("Modal fixture", typeof(RectTransform));
            var parent = new GameObject("Root canvas", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(parent.transform, false);
            try
            {
                UILayers.SetOverlay(root, UILayers.Modal);
                UILayers.SetOverlay(root, UILayers.Modal);
                Assert.AreEqual(1, root.GetComponents<Canvas>().Length);
                Assert.AreEqual(1, root.GetComponents<GraphicRaycaster>().Length);
                Assert.IsTrue(root.GetComponent<Canvas>().overrideSorting);
                Assert.Greater(root.GetComponent<Canvas>().sortingOrder, UILayers.Hud);
                Assert.Greater(UILayers.Result, UILayers.Cinematic);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test] public void CheckmateSingletonBuildsOneCanvas()
        {
            var ui = CheckmateCinematicUI.Instance;
            try
            {
                Assert.AreSame(ui, CheckmateCinematicUI.Instance);
                Assert.AreEqual(1, ui.GetComponentsInChildren<Canvas>(true).Length);
            }
            finally { Object.DestroyImmediate(ui.gameObject); }
        }
        [Test] public void KingSynchroSingletonBuildsOneCanvas()
        {
            var ui = KingSynchroUI.Instance;
            try { Assert.AreEqual(1, ui.GetComponentsInChildren<Canvas>(true).Length); }
            finally { Object.DestroyImmediate(ui.gameObject); }
        }

        [Test] public void CameraControlsStayBoundedAndHomeRestoresVirtualPose()
        {
            var output = new GameObject("Camera fixture").AddComponent<Camera>();
            output.transform.SetPositionAndRotation(new Vector3(8,16,-3), Quaternion.Euler(60,0,0));
            var go = new GameObject("Director fixture");
            try
            {
                var director = go.AddComponent<BattleCameraDirector>();
                director.Initialise(output, null);
                director.Initialise(output, null);
                Assert.AreEqual(3, go.transform.childCount);
                var homePosition = go.transform.GetChild(0).position;
                director.AdjustView(new Vector2(100,100), 1000, -100);
                Assert.AreEqual(.75f, director.Zoom);
                Assert.AreEqual(new Vector3(8,16,-3), output.transform.position, "Director must drive virtual cameras, not write the output camera.");
                director.AdjustView(Vector2.zero, 0, 100);
                Assert.AreEqual(1.4f, director.Zoom);
                director.ResetView();
                Assert.AreEqual(homePosition, go.transform.GetChild(0).position);
                Assert.AreEqual(1, director.Zoom);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(output.gameObject); }
        }
    }
}
