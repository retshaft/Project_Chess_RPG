using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Checkmate.Background.Editor
{
    public static class CombatRuinsArt
    {
        public const string Root = "Assets/Environment/CombatRuins/";
        public const string ScenePath = "Assets/Scenes/Test/BattleScene.unity";
        const string Models = "Assets/Resources/Models/Ruin/";
        const uint DecalLayer = 1u << 6;
        const float BarrierGeneratorHeight = .55f;
        const string BoardBarrierRootName = "Board Barrier - Emission Line";
        const string BoardBarrierCoreMaterialName = "Board Barrier - Emission Core";
        const string BoardBarrierGlowMaterialName = "Board Barrier - Emission Glow";
        const float BoardBarrierCoreWidth = .035f;
        const float BoardBarrierGlowWidth = .16f;
        const float BoardBarrierInset = .02f;
        static readonly string[] ModelNames = { "BigGear", "BorderBarrierGenerator", "BrokenBigGear", "LowWall", "Path", "SmallPillar", "Stairs" };

        [MenuItem("Tools/Checkmate/Background/Build Ruin Art Assets")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            foreach (string folder in new[] { "Materials", "Prefabs" }) Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
            var shader = LoadShader("RuinSurface_Master");
            foreach (string model in ModelNames)
            {
                string basePath = Directory.GetFiles(Models + "Textures", model + "_*_BaseColor.png").Single().Replace('\\', '/');
                string prefix = basePath.Substring(0, basePath.Length - "_BaseColor.png".Length);
                var mat = MaterialAsset(model + " - Ruin Surface", shader);
                foreach (var map in new[] { ("BaseColor", "_BaseMap"), ("Normal", "_NormalMap"), ("Metallic", "_MetallicMap"), ("Roughness", "_RoughnessMap"), ("Emissive", "_EmissionMap") })
                {
                    string path = prefix + "_" + map.Item1 + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
                    bool normal = map.Item1 == "Normal";
                    bool srgb = map.Item1 == "BaseColor" || map.Item1 == "Emissive";
                    var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    if (importer.textureType != type || importer.sRGBTexture != srgb)
                    {
                        importer.textureType = type;
                        importer.sRGBTexture = srgb;
                        importer.SaveAndReimport();
                    }
                    mat.SetTexture(map.Item2, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                }
                mat.SetColor("_BaseColor", new Color(.96f, .98f, .94f));
                mat.SetFloat("_NormalStrength", .65f);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", .85f);
                mat.SetFloat("_Wetness", .18f);
                mat.SetFloat("_WetSmoothness", .72f);
                mat.SetColor("_WetTint", new Color(.50f, .71f, .76f));
                mat.SetColor("_EmissionColor", new Color(.08f, .20f, .22f));
                EditorUtility.SetDirty(mat);
                CreateModelPrefab(model, mat);
            }
            var moss = MaterialAsset("Moss - Projected", LoadShader("RuinMoss_Decal"));
            moss.SetTexture("_Color", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/Moss002_Color.png"));
            moss.SetTexture("_MaskMap", Texture2D.whiteTexture);
            moss.SetColor("_Tint", new Color(.66f, .79f, .60f, 1));
            moss.SetFloat("_Opacity", .8f);
            moss.SetFloat("_EdgeFalloff", 64f);
            moss.SetFloat("_Smoothness", .24f);
            var stain = MaterialAsset("Waterline - Projected", LoadShader("RuinWaterline_Decal"));
            stain.SetTexture("_Color", Texture2D.whiteTexture);
            stain.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/Leaking012C_Mask.png"));
            stain.SetColor("_Tint", new Color(.12f, .25f, .28f, 1));
            stain.SetFloat("_Opacity", .6f);
            stain.SetFloat("_EdgeFalloff", 80f);
            stain.SetFloat("_Smoothness", .78f);
            EditorUtility.SetDirty(moss);
            EditorUtility.SetDirty(stain);
            CreateDecalPrefab("Moss Projector", moss, new Vector3(2.8f, 2.2f, .6f));
            CreateDecalPrefab("Waterline Projector", stain, new Vector3(3f, 1.1f, .6f));
            var vineSource = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Vines/IvyCladStem.glb");
            if (!vineSource) throw new InvalidOperationException("glTFast did not import IvyCladStem.glb.");
            var vine = UnityEngine.Object.Instantiate(vineSource);
            try
            {
                vine.name = "Ivy Clad Stem - CC0";
                foreach (var collider in vine.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var renderer in vine.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    {
                        bool leaf = original && original.name.IndexOf("leaf", StringComparison.OrdinalIgnoreCase) >= 0;
                        var material = MaterialAsset(leaf ? "Ivy - Leaves" : "Ivy - Weathered Wood", shader);
                        material.SetColor("_BaseColor", leaf ? new Color(.09f, .21f, .055f) : new Color(.19f, .14f, .085f));
                        material.SetFloat("_Metallic", 0);
                        material.SetFloat("_NormalStrength", 0);
                        material.SetFloat("_Wetness", .05f);
                        material.SetFloat("_WetSmoothness", .3f);
                        material.SetFloat("_Smoothness", .15f);
                        material.SetColor("_EmissionColor", Color.black);
                        EditorUtility.SetDirty(material);
                        return material;
                    }).ToArray();
                }
                PrefabUtility.SaveAsPrefabAsset(vine, Root + "Prefabs/Ivy Clad Stem.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(vine); }
            AssetDatabase.SaveAssets();
            Debug.Log("Ruin art prepared: 7 PBR material/prefab pairs, 2 Decal Projectors, imported ivy with 2 material variants.");
        }

        [MenuItem("Tools/Checkmate/Background/Apply Ruin Art to Battle Scene")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open BattleScene in Edit Mode.");
            Prepare();
            EnableDecals();
            int changed = 0;
            foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                var filter = r.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (!meshPath.StartsWith(Models, StringComparison.Ordinal)) continue;
                string model = Path.GetFileNameWithoutExtension(meshPath);
                if (!ModelNames.Contains(model)) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + model + " - Ruin Surface.mat");
                Undo.RecordObject(r, "Apply Ruin Shader Graph");
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                r.renderingLayerMask |= DecalLayer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                changed++;
            }
            ComposeDressing(scene);
            ComposeBoardBarrier(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Ruin art applied to " + changed + " existing renderers. Ready-to-place prefabs are in " + Root + "Prefabs.");
        }

        [MenuItem("Tools/Checkmate/Background/Build Board Barrier Emission Line")]
        public static void BuildBoardBarrierEmissionLine()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use Edit Mode.");
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ComposeBoardBarrier(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Board barrier emission line rebuilt from BorderBarrierGenerator anchors.");
        }

        [MenuItem("Tools/Checkmate/Background/Apply Ruin Mood Color Grade")]
        public static void ApplyMood()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open BattleScene in Edit Mode.");

            RenderSettings.fogColor = new Color(.43f, .60f, .68f, 1f);
            RenderSettings.ambientSkyColor = new Color(.42f, .56f, .66f, 1f);
            RenderSettings.ambientEquatorColor = new Color(.25f, .37f, .41f, 1f);
            RenderSettings.ambientGroundColor = new Color(.11f, .20f, .23f, 1f);

            var directional = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(l => l.type == LightType.Directional);
            if (directional)
            {
                Undo.RecordObject(directional, "Apply ruin mood lighting");
                directional.color = new Color(1f, .89f, .76f, 1f);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Ruin mood color grade applied: cool teal shadows, restrained warm highlights, and blue atmospheric haze.");
        }

        public static void BatchApplyRuinMood()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply();
            ApplyMood();
            EditorSceneManager.SaveScene(scene);
        }

        static void ComposeDressing(Scene scene)
        {
            const string rootName = "Ruin Art - Shader Graph Dressing";
            var existingRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
            if (existingRoot)
            {
                ResizeBarrierGenerators(existingRoot.transform, BarrierGeneratorHeight);
                return;
            }
            var root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Compose textured ruin dressing");
            var structures = new GameObject("Textured Architecture").transform;
            structures.SetParent(root.transform, false);
            var weathering = new GameObject("Moss and Waterline Projectors").transform;
            weathering.SetParent(root.transform, false);
            foreach (var p in new[] { new Vector3(-2.7f, -.4f, -1.8f), new Vector3(18.7f, -.4f, -1.8f), new Vector3(-3.2f, -.4f, 19), new Vector3(19.2f, -.4f, 19) })
            {
                float height = p.z > 10 ? 4.8f : 2.6f;
                var pillar = Place("SmallPillar", p, height, 0, structures);
                var b = BoundsOf(pillar);
                Place("BorderBarrierGenerator", new Vector3(p.x, b.max.y, p.z), BarrierGeneratorHeight, 0, structures);
                AddWeathering(b, weathering);
            }
            foreach (var p in new[] { new Vector3(-2.5f, -.35f, 5), new Vector3(18.5f, -.35f, 11), new Vector3(3, -.35f, 18.2f), new Vector3(13, -.35f, 18.2f) })
            {
                var wall = Place("LowWall", p, 1.25f, p.x < 0 || p.x > 16 ? 90 : 0, structures);
                AddWeathering(BoundsOf(wall), weathering);
            }
            Place("BigGear", new Vector3(-8, -2, 17), 10, 75, structures);
            Place("BrokenBigGear", new Vector3(23, -1.2f, 20), 5.5f, 100, structures);
            Place("Stairs", new Vector3(8, -1.1f, -3.5f), 1.8f, 0, structures);
            Place("Path", new Vector3(8, -1.65f, -8), 1.25f, 0, structures);
            Selection.activeGameObject = root;
        }

        static void ComposeBoardBarrier(Scene scene)
        {
            var boardBounds = FindBoardBounds(scene);
            if (!boardBounds.HasValue)
            {
                Debug.LogWarning("Board barrier skipped: Tile_x_y transforms were not found.");
                return;
            }

            var generators = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.Trim().Equals("BorderBarrierGenerator - Textured", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (generators.Count < 4)
            {
                Debug.LogWarning("Board barrier skipped: at least four BorderBarrierGenerator objects are required.");
                return;
            }

            var bounds = boardBounds.Value;
            var targets = new[]
            {
                new Vector3(bounds.min.x, 0, bounds.min.z),
                new Vector3(bounds.max.x, 0, bounds.min.z),
                new Vector3(bounds.max.x, 0, bounds.max.z),
                new Vector3(bounds.min.x, 0, bounds.max.z),
            };
            var anchors = targets.Select(target => generators
                .OrderBy(generator => HorizontalDistanceSquared(generator.position, target))
                .First()).ToArray();
            if (anchors.Distinct().Count() != 4)
            {
                Debug.LogWarning("Board barrier skipped: BorderBarrierGenerator anchors do not form four distinct corners.");
                return;
            }

            var oldRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == BoardBarrierRootName);
            if (oldRoot) Undo.DestroyObjectImmediate(oldRoot);

            var root = new GameObject(BoardBarrierRootName);
            Undo.RegisterCreatedObjectUndo(root, "Build board barrier emission line");
            root.transform.position = Vector3.zero;

            var coreMaterial = GetOrCreateBarrierMaterial(BoardBarrierCoreMaterialName, false);
            var glowMaterial = GetOrCreateBarrierMaterial(BoardBarrierGlowMaterialName, true);
            var energyShader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Shaders/BoardEnergyBarrier.shader");
            if (!energyShader) throw new InvalidOperationException("BoardEnergyBarrier shader is missing.");
            var energyMaterial = MaterialAsset("Board Barrier - Energy Surface", energyShader);
            var corePoints = anchors.Select(anchor => anchor.position).ToArray();
            var boardTop = bounds.max.y;
            var anchorHeight = anchors.Average(anchor => anchor.position.y);
            var lineY = Mathf.Max(boardTop + .035f, anchorHeight - .03f);
            for (var i = 0; i < corePoints.Length; i++)
            {
                var point = corePoints[i];
                point.y = lineY;
                point.x = Mathf.Lerp(point.x, targets[i].x, BoardBarrierInset);
                point.z = Mathf.Lerp(point.z, targets[i].z, BoardBarrierInset);
                corePoints[i] = point;
            }

            for (var i = 0; i < corePoints.Length; i++)
            {
                var next = corePoints[(i + 1) % corePoints.Length];
                CreateBarrierLine(root.transform, "Barrier Core " + i, corePoints[i], next, BoardBarrierCoreWidth, coreMaterial);
                CreateBarrierLine(root.transform, "Barrier Glow " + i, corePoints[i], next, BoardBarrierGlowWidth, glowMaterial);
                CreateEnergyPanel(root.transform, "Energy Barrier " + i, corePoints[i], next, energyMaterial);
            }

            root.transform.SetSiblingIndex(0);
            Selection.activeGameObject = root;
        }

        static void CreateEnergyPanel(Transform parent, string name, Vector3 from, Vector3 to, Material material)
        {
            const float height = 4f;
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = name;
            UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(parent, false);
            var direction = (to - from).normalized;
            // Unity's quad normal is -Z. Point that normal towards the board.
            panel.transform.SetPositionAndRotation((from + to) * .5f + Vector3.up * (height * .5f),
                Quaternion.LookRotation(Vector3.Cross(Vector3.up, direction), Vector3.up));
            panel.transform.localScale = new Vector3(Vector3.Distance(from, to), height, 1f);
            var renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static Bounds? FindBoardBounds(Scene scene)
        {
            var tiles = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("Tile_", StringComparison.Ordinal) && t.name.Count(c => c == '_') == 2)
                .ToList();
            if (tiles.Count == 0) return null;

            var renderers = tiles.SelectMany(tile => tile.GetComponentsInChildren<Renderer>(true)).ToArray();
            if (renderers.Length == 0) return null;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            var delta = new Vector2(a.x - b.x, a.z - b.z);
            return delta.sqrMagnitude;
        }

        static Material GetOrCreateBarrierMaterial(string name, bool glow)
        {
            var path = Root + "Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;

            Shader shader;
            if (glow)
            {
                shader = Shader.Find("Sprites/Default");
            }
            else
            {
                shader = LoadShader("RuinSurface_Master");
            }
            if (!shader) throw new InvalidOperationException("Barrier shader is missing: " + name);

            var material = new Material(shader) { name = name, enableInstancing = true };
            if (glow)
            {
                material.color = new Color(.03f, .65f, .72f, .16f);
                material.SetColor("_Color", new Color(.03f, .65f, .72f, .16f));
                material.renderQueue = 3000;
            }
            else
            {
                material.SetColor("_BaseColor", new Color(.015f, .20f, .22f, 1f));
                material.SetColor("_EmissionColor", new Color(1.5f, 8f, 9f, 1f));
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetTexture("_EmissionMap", Texture2D.whiteTexture);
                material.SetFloat("_Metallic", .1f);
                material.SetFloat("_Smoothness", .8f);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void CreateBarrierLine(Transform parent, string name, Vector3 from, Vector3 to, float width, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = material;
            line.renderingLayerMask = 1;
            go.isStatic = true;
        }

        static void ResizeBarrierGenerators(Transform root, float targetHeight)
        {
            foreach (var barrier in root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.IndexOf("BorderBarrierGenerator", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var renderers = barrier.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var before = renderers[0].bounds;
                foreach (var renderer in renderers) before.Encapsulate(renderer.bounds);
                if (before.size.y < .001f) continue;
                float ratio = targetHeight / before.size.y;
                barrier.localScale *= ratio;
                renderers = barrier.GetComponentsInChildren<Renderer>(true);
                var after = renderers[0].bounds;
                foreach (var renderer in renderers) after.Encapsulate(renderer.bounds);
                barrier.position += new Vector3(before.center.x - after.center.x, before.min.y - after.min.y, before.center.z - after.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(barrier);
                PrefabUtility.RecordPrefabInstancePropertyModifications(barrier.transform);
            }
        }

        static GameObject Place(string model, Vector3 bottom, float height, float yaw, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + model + " - Textured.prefab"));
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation;
            var b = BoundsOf(go);
            go.transform.localScale *= height / Mathf.Max(b.size.y, .001f);
            b = BoundsOf(go);
            go.transform.position += bottom - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var collider in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            return go;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void AddWeathering(Bounds bounds, Transform parent)
        {
            var moss = InstantiateProjector("Moss Projector", parent);
            moss.transform.position = new Vector3(bounds.center.x, bounds.max.y - .08f, bounds.center.z);
            moss.transform.rotation = Quaternion.Euler(90, 0, 0);
            moss.size = new Vector3(bounds.size.x * 1.03f, bounds.size.z * 1.03f, .45f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(moss);
            PrefabUtility.RecordPrefabInstancePropertyModifications(moss.transform);
            var stain = InstantiateProjector("Waterline Projector", parent);
            stain.transform.position = new Vector3(bounds.center.x, .35f, bounds.min.z + .10f);
            stain.size = new Vector3(bounds.size.x * 1.04f, 1.1f, .5f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(stain);
            PrefabUtility.RecordPrefabInstancePropertyModifications(stain.transform);
        }

        static DecalProjector InstantiateProjector(string name, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + name + ".prefab"));
            go.transform.SetParent(parent, false);
            return go.GetComponent<DecalProjector>();
        }

        static void EnableDecals()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (!renderer) throw new InvalidOperationException("PC renderer is missing.");
            var feature = renderer.rendererFeatures.OfType<DecalRendererFeature>().FirstOrDefault();
            if (!feature)
            {
                feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                feature.name = "Ruin Decals";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            var serialized = new SerializedObject(feature);
            serialized.FindProperty("m_Settings.technique").intValue = 1; // DBuffer
            serialized.FindProperty("m_Settings.maxDrawDistance").floatValue = 100f;
            serialized.FindProperty("m_Settings.decalLayers").boolValue = true;
            serialized.FindProperty("m_Settings.dBufferSettings.surfaceData").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            feature.Create();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssets();
        }

        static Shader LoadShader(string name)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "ShaderGraphs/" + name + ".shadergraph");
            if (!shader) throw new InvalidOperationException("Shader Graph did not import: " + name);
            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException(name + ": " + string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
            return shader;
        }

        static Material MaterialAsset(string name, Shader shader)
        {
            var path = Root + "Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.enableInstancing = true;
            return mat;
        }

        static void CreateModelPrefab(string model, Material mat)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (!source) throw new InvalidOperationException("Missing model: " + model);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = model + " - Textured";
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterials = Enumerable.Repeat(mat, renderer.sharedMaterials.Length).ToArray();
                    renderer.renderingLayerMask |= DecalLayer;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, Root + "Prefabs/" + model + " - Textured.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static void CreateDecalPrefab(string name, Material mat, Vector3 size)
        {
            var go = new GameObject(name);
            try
            {
                var projector = go.AddComponent<DecalProjector>();
                projector.material = mat;
                projector.size = size;
                projector.pivot = Vector3.zero;
                projector.drawDistance = 100;
                projector.fadeScale = .8f;
                projector.startAngleFade = 55;
                projector.endAngleFade = 85;
                projector.renderingLayerMask = DecalLayer;
                PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
