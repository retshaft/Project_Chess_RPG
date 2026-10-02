using System.Collections;
using CheckmateRPG.Grid;
using CheckmateRPG.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheckmateRPG.Core
{
    // Owns virtual cameras only. The physical Camera remains the single raycasting/rendering camera.
    [DefaultExecutionOrder(-100)]
    public sealed class BattleCameraDirector : MonoBehaviour
    {
        private CinemachineCamera gameplay, cinematic, inspection;
        private bool inspecting;
        private float selectionTimeScale, previousTimeScale;
        public bool IsInspecting => inspecting;
        public const float SelectionSlowdown = .15f;
        private BattleManager battle;
        private Transform selection;
        private Vector3 home, homeOffset, pivot;
        private Quaternion homeRotation;
        private float yaw, zoom = 1;
        private Coroutine intro;
        private bool oldTickPause, oldAPEnabled, pausedSimulation;
        private int consumedFrame = -1;
        public bool IsIntroPlaying => intro != null;
        public bool BlocksCommands => IsIntroPlaying || consumedFrame == Time.frameCount;
        public float Zoom => zoom;

        public void Initialise(Camera output, BattleManager owner)
        {
            if (!output || gameplay) return;
            battle = owner;
            var grid = GridSystem.Instance;
            home = grid != null ? (grid.GridToWorld(Vector2Int.zero) + grid.GridToWorld(new Vector2Int(7, 7))) * .5f : Vector3.zero;
            pivot = home;
            homeOffset = output.transform.position - home;
            homeRotation = output.transform.rotation;
            var brain = output.GetComponent<CinemachineBrain>();
            if (!brain) brain = output.gameObject.AddComponent<CinemachineBrain>();
            brain.IgnoreTimeScale = true;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .3f);
            gameplay = CreateCamera("TacticalCamera", output, 10);
            cinematic = CreateCamera("CinematicCamera", output, 0);
            inspection = CreateCamera("SelectionCamera", output, 0);
        }

        private CinemachineCamera CreateCamera(string label, Camera output, int priority)
        {
            var root = new GameObject(label);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(output.transform.position, output.transform.rotation);
            var camera = root.AddComponent<CinemachineCamera>();
            camera.Lens = LensSettings.FromCamera(output);
            camera.Priority = priority;
            return camera;
        }

        public void SetSelection(Transform target) => selection = target;
        public void BeginInspection(Transform target)
        {
            if (!target || !gameplay || !inspection || BlocksCommands) return;
            if (!inspecting)
            {
                previousTimeScale = Time.timeScale;
                selectionTimeScale = previousTimeScale * SelectionSlowdown;
                Time.timeScale = selectionTimeScale;
                inspecting = true;
            }
            var offset = gameplay.transform.position - pivot;
            inspection.transform.SetPositionAndRotation(target.position + offset * .72f, gameplay.transform.rotation);
            var lens = gameplay.Lens;
            lens.OrthographicSize *= .72f;
            inspection.Lens = lens;
            inspection.Priority = 15;
        }

        // Return to the full board while choosing a destination; keep tactical slow motion.
        public void ShowTargetingBoard() { if (inspection) inspection.Priority = 0; }

        public void EndInspection()
        {
            if (inspection) inspection.Priority = 0;
            if (!inspecting) return;
            if (Mathf.Approximately(Time.timeScale, selectionTimeScale)) Time.timeScale = previousTimeScale;
            inspecting = false;
        }
        public static float ClampZoom(float value) => Mathf.Clamp(value, .75f, 1.4f);
        public void AdjustView(Vector2 pan, float rotation, float zoomDelta)
        {
            if (!gameplay || BlocksCommands) return;
            zoom = ClampZoom(zoom + zoomDelta);
            yaw = Mathf.Clamp(yaw + rotation, -65f, 65f);
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            pivot += right * pan.x + forward * pan.y;
            pivot.x = Mathf.Clamp(pivot.x, home.x - 4, home.x + 4);
            pivot.z = Mathf.Clamp(pivot.z, home.z - 4, home.z + 4);
            ApplyPose();
        }

        public void ResetView()
        {
            pivot = home; yaw = 0; zoom = 1;
            ApplyPose();
        }

        public void FocusSelection()
        {
            if (!selection || BlocksCommands) return;
            pivot = selection.position;
            pivot.y = home.y;
            AdjustView(Vector2.zero, 0, 0);
        }

        private void ApplyPose()
        {
            if (!gameplay) return;
            var turn = Quaternion.Euler(0, yaw, 0);
            gameplay.transform.SetPositionAndRotation(pivot + turn * homeOffset * zoom, turn * homeRotation);
        }

        private void Update()
        {
            if (!gameplay || !battle) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (IsIntroPlaying)
            {
                if ((mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                    (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)) SkipIntro();
                return;
            }
            if (inspecting || battle.CurrentPhase == BattleManager.BattlePhase.Ended || UILayers.PointerOverUI() ||
                (SkillCutInUI.Instance != null && SkillCutInUI.Instance.IsAnimating)) return;
            if (keyboard != null)
            {
                if (keyboard.homeKey.wasPressedThisFrame) ResetView();
                if (keyboard.fKey.wasPressedThisFrame) FocusSelection();
                float turn = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
                if (turn != 0) AdjustView(Vector2.zero, turn * 40 * Time.unscaledDeltaTime, 0);
            }
            if (mouse != null)
            {
                if (mouse.middleButton.isPressed)
                    AdjustView(-mouse.delta.ReadValue() * .012f, 0, 0);
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > .01f) AdjustView(Vector2.zero, 0, -Mathf.Sign(wheel) * .07f);
            }
        }

        public void PlayIntro()
        {
            if (!gameplay || IsIntroPlaying) return;
            oldTickPause = TickScheduler.EnsureExists().IsPaused;
            oldAPEnabled = APManager.Instance && APManager.Instance.enabled;
            TickScheduler.EnsureExists().IsPaused = true;
            if (APManager.Instance) APManager.Instance.enabled = false;
            pausedSimulation = true;
            cinematic.Lens = gameplay.Lens;
            cinematic.transform.SetPositionAndRotation(gameplay.transform.position + Vector3.up * 2, gameplay.transform.rotation);
            cinematic.Priority = 20;
            intro = StartCoroutine(IntroRoutine());
        }

        public void PlaySkillCloseup(CheckmateRPG.Units.UnitBrain actor)
        {
            if (actor == null || actor.UnitData == null || actor.UnitData.Rarity != 5 || !gameplay || IsIntroPlaying) return;
            PlayIntro();
            var target = actor.transform.position + Vector3.up * .8f;
            var offset = (gameplay.transform.position - target).normalized * 6f;
            cinematic.transform.SetPositionAndRotation(target + offset, Quaternion.LookRotation(-offset));
            var lens = gameplay.Lens;
            lens.OrthographicSize = Mathf.Min(lens.OrthographicSize, 3f);
            cinematic.Lens = lens;
        }

        private IEnumerator IntroRoutine()
        {
            yield return new WaitForSecondsRealtime(.7f);
            cinematic.Priority = 0;
            yield return new WaitForSecondsRealtime(.35f);
            FinishIntro();
        }

        public void SkipIntro()
        {
            if (intro != null) StopCoroutine(intro);
            FinishIntro();
        }

        private void FinishIntro()
        {
            intro = null;
            if (cinematic) cinematic.Priority = 0;
            consumedFrame = Time.frameCount;
            if (!pausedSimulation) return;
            if (TickScheduler.Instance) TickScheduler.Instance.IsPaused = oldTickPause;
            if (APManager.Instance) APManager.Instance.enabled = oldAPEnabled;
            pausedSimulation = false;
        }

        public void SetVictoryZoom(float progress)
        {
            if (!gameplay || !cinematic) return;
            cinematic.transform.SetPositionAndRotation(gameplay.transform.position, gameplay.transform.rotation);
            var lens = gameplay.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, Mathf.Max(30, lens.FieldOfView - 15), Mathf.Clamp01(progress));
            lens.OrthographicSize *= Mathf.Lerp(1, .8f, Mathf.Clamp01(progress));
            cinematic.Lens = lens; cinematic.Priority = progress > 0 ? 20 : 0;
        }

        private void OnDisable() { EndInspection(); if (intro != null) StopCoroutine(intro); FinishIntro(); }
    }
}
