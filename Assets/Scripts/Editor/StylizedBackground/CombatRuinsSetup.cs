using UnityEditor;

namespace Checkmate.Background.Editor
{
    // Preserve the earlier menu entry; the discarded automatic layout is not rebuilt.
    public static class CombatRuinsSetup
    {
        [MenuItem("Tools/Checkmate/Background/Prepare Ruin ShaderGraph Materials")]
        public static void PrepareShaderGraphMaterials() => CombatRuinsArt.Prepare();

        public static void Build() => CombatRuinsArt.Apply();
    }
}
