using UnityEngine;
using UnityEditor;
using CheckmateRPG.PlayerInput;
using CheckmateRPG.UI;
using CheckmateRPG.Core;

namespace CheckmateRPG.Editor
{
    public class SetupM2Scene
    {
        [MenuItem("Tools/Checkmate RPG/Setup M2 Battle Scene")]
        public static void SetupScene()
        {
            // Create or find a central object for M2 Systems
            string goName = "M2_InputAndOverlaySystems";
            GameObject systemsGo = GameObject.Find(goName);
            if (systemsGo == null)
            {
                systemsGo = new GameObject(goName);
            }

            // Ensure GridRaycaster
            if (systemsGo.GetComponent<GridRaycaster>() == null)
            {
                systemsGo.AddComponent<GridRaycaster>();
            }

            // Ensure PlayerInputController
            if (systemsGo.GetComponent<PlayerInputController>() == null)
            {
                systemsGo.AddComponent<PlayerInputController>();
            }

            // Ensure TileHighlighter
            if (systemsGo.GetComponent<TileHighlighter>() == null)
            {
                systemsGo.AddComponent<TileHighlighter>();
            }

            // Optional: If they have a CheckmateRPG.Core.BattleManager, we might want to disable its internal selection overlay
            // to avoid overlapping highlights, but it can also just coexist or the user can turn it off.
            var testManager = Object.FindFirstObjectByType<CheckmateRPG.Core.BattleManager>();
            if (testManager != null)
            {
                var oldOverlay = testManager.GetComponent<BattleSelectionOverlayController>();
                if (oldOverlay != null)
                {
                    oldOverlay.enabled = false;
                    Debug.Log("[SetupM2Scene] Disabled old BattleSelectionOverlayController on CheckmateRPG.Core.BattleManager to prevent visual overlap.");
                }
            }

            // Mark scene as dirty so the changes are saved
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }

            Debug.Log("[SetupM2Scene] M2 Systems (GridRaycaster, PlayerInputController, TileHighlighter) have been successfully added to the scene!");
        }
    }
}


