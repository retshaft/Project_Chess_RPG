using UnityEngine;
using UnityEditor;
using CheckmateRPG.UI;
using CheckmateRPG.Core;

namespace CheckmateRPG.Editor
{
    public class SetupM3Scene
    {
        [MenuItem("Tools/Checkmate RPG/Setup M3 Battle HUD")]
        public static void SetupScene()
        {
            string goName = "M3_HUD_Systems";
            GameObject systemsGo = GameObject.Find(goName);
            if (systemsGo == null)
            {
                systemsGo = new GameObject(goName);
            }

            // Ensure PlayerAPBar
            if (systemsGo.GetComponent<PlayerAPBar>() == null)
            {
                systemsGo.AddComponent<PlayerAPBar>();
            }

            // Ensure UnitInfoPanel
            if (systemsGo.GetComponent<UnitInfoPanel>() == null)
            {
                systemsGo.AddComponent<UnitInfoPanel>();
            }

            // Ensure Bootstrapper for spawned units
            if (systemsGo.GetComponent<M3UnitUIBootstrapper>() == null)
            {
                systemsGo.AddComponent<M3UnitUIBootstrapper>();
            }

            // Mark scene as dirty so the changes are saved
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }

            Debug.Log("[SetupM3Scene] M3 Systems (PlayerAPBar, UnitInfoPanel, Bootstrapper) have been successfully added to the scene!");
        }
    }
}
