// =====================================================================
//  ToonOutlineNormalBaker.cs
//  에디터 도구: 메시의 스무스 노멀을 UV2 또는 Tangent에 베이크
//  아웃라인이 하드 엣지(sharp edges)에서 깨지는 문제를 해결합니다.
//  사용법: Unity 메뉴 → Tools → Toon → Bake Smooth Normals
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Toon.Editor
{
    public class ToonOutlineNormalBaker : EditorWindow
    {
        private GameObject  _targetObject;
        private bool        _bakeToTangent  = true;    // true = tangent.w, false = UV2
        private bool        _processChildren = true;

        [MenuItem("Tools/Toon/Bake Smooth Normals for Outline")]
        public static void OpenWindow()
        {
            var w = GetWindow<ToonOutlineNormalBaker>("Toon Outline Normal Baker");
            w.minSize = new Vector2(360, 200);
            w.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Toon Outline – Smooth Normal Baker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "하드 엣지 메시에서 아웃라인이 깨질 때 사용하세요.\n" +
                "스무스 노멀을 계산해 탄젠트 W 채널 또는 UV2에 저장합니다.",
                MessageType.Info);

            EditorGUILayout.Space();
            _targetObject    = (GameObject)EditorGUILayout.ObjectField("Target GameObject", _targetObject, typeof(GameObject), true);
            _bakeToTangent   = EditorGUILayout.Toggle("Bake to Tangent (권장)", _bakeToTangent);
            _processChildren = EditorGUILayout.Toggle("Process Children", _processChildren);

            EditorGUILayout.Space();
            GUI.enabled = _targetObject != null;
            if (GUILayout.Button("Bake Smooth Normals", GUILayout.Height(36)))
                BakeSmoothNormals(_targetObject, _processChildren, _bakeToTangent);
            GUI.enabled = true;
        }

        // ── Core logic ────────────────────────────────────────────────
        private static void BakeSmoothNormals(GameObject root, bool children, bool toTangent)
        {
            var filters = children
                ? root.GetComponentsInChildren<MeshFilter>(true)
                : root.GetComponents<MeshFilter>();

            int count = 0;
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                var mesh = Instantiate(mf.sharedMesh);
                mesh.name = mf.sharedMesh.name + "_SmoothOutline";

                BakeMesh(mesh, toTangent);

                // Save as asset next to the original mesh if possible
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!string.IsNullOrEmpty(path))
                {
                    string dir  = Path.GetDirectoryName(path);
                    string save = Path.Combine(dir, mesh.name + ".asset");
                    AssetDatabase.CreateAsset(mesh, save);
                }

                mf.sharedMesh = mesh;
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ToonOutlineNormalBaker] Baked {count} mesh(es).");
            EditorUtility.DisplayDialog("Done", $"Baked smooth normals for {count} mesh(es).", "OK");
        }

        private static void BakeMesh(Mesh mesh, bool toTangent)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals  = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            int       vCount   = vertices.Length;

            // Compute average normal for each unique position
            var posNormalMap = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vCount; i++)
            {
                if (!posNormalMap.ContainsKey(vertices[i]))
                    posNormalMap[vertices[i]] = Vector3.zero;
                posNormalMap[vertices[i]] += normals[i];
            }

            // Normalize averaged normals
            var smoothNormals = new Vector3[vCount];
            for (int i = 0; i < vCount; i++)
                smoothNormals[i] = posNormalMap[vertices[i]].normalized;

            if (toTangent)
            {
                // Transform smooth normal to tangent space and store in tangent XYZ, preserve W
                var newTangents = new Vector4[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    Vector3 n = normals[i];
                    Vector3 t = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    Vector3 b = Vector3.Cross(n, t) * tangents[i].w;

                    // TBN matrix (row vectors)
                    float x = Vector3.Dot(smoothNormals[i], t);
                    float y = Vector3.Dot(smoothNormals[i], b);
                    float z = Vector3.Dot(smoothNormals[i], n);

                    newTangents[i] = new Vector4(x, y, z, tangents[i].w);
                }
                mesh.tangents = newTangents;
            }
            else
            {
                // Store in UV2
                var uv2 = new Vector2[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    // Pack XY only – Z is reconstructed in shader
                    uv2[i] = new Vector2(smoothNormals[i].x, smoothNormals[i].y);
                }
                mesh.uv2 = uv2;
            }
        }
    }
}
