using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace CheckmateRPG.Progression
{
    public class NotationPuzzleScreen : MonoBehaviour
    {
        [SerializeField] private float _tileSize = 1.2f;
        [SerializeField] private float _nodeHeight = 0.4f;

        private NotationTreeData _treeData;
        private NotationProgressState _progressState;
        private Camera _puzzleCamera;
        
        private GameObject _boardRoot;
        private Dictionary<NotationNodeData, GameObject> _nodeObjects = new();
        private Dictionary<GameObject, NotationNodeData> _objectToNode = new();
        
        // UI
        private TextMeshProUGUI _tpText;
        private TextMeshProUGUI _infoText;
        
        public void Initialize(NotationTreeData treeData, NotationProgressState progressState)
        {
            _treeData = treeData;
            _progressState = progressState;
            
            SetupCamera();
            SetupUI();
            Build3DBoard();
            UpdateAllNodes();
        }

        private void SetupCamera()
        {
            if (_puzzleCamera != null) return;

            var camObj = new GameObject("PuzzleCamera");
            camObj.transform.SetParent(transform);
            _puzzleCamera = camObj.AddComponent<Camera>();
            // Center camera over an 8x8 board (center at 3.5 * tileSize)
            float center = 3.5f * _tileSize;
            _puzzleCamera.transform.position = new Vector3(center + 1.5f, 9.5f, center - 4.0f);
            _puzzleCamera.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
            _puzzleCamera.clearFlags = CameraClearFlags.SolidColor;
            _puzzleCamera.backgroundColor = new Color(0.08f, 0.09f, 0.14f);
            
            // Add simple light
            var lightGo = new GameObject("PuzzleLight");
            lightGo.transform.SetParent(transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.1f;
        }

        private void SetupUI()
        {
            if (_tpText != null && _infoText != null) return;

            var canvasObj = new GameObject("PuzzleCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            var tpObj = new GameObject("TPText");
            tpObj.transform.SetParent(canvasObj.transform, false);
            _tpText = tpObj.AddComponent<TextMeshProUGUI>();
            _tpText.font = Resources.GetBuiltinResource<TMP_FontAsset>("LiberationSans SDF.asset");
            _tpText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _tpText.rectTransform.anchorMax = new Vector2(0f, 1f);
            _tpText.rectTransform.pivot = new Vector2(0f, 1f);
            _tpText.rectTransform.anchoredPosition = new Vector2(25f, -25f);
            _tpText.rectTransform.sizeDelta = new Vector2(650f, 150f);
            _tpText.fontSize = 26;
            _tpText.color = Color.white;
            _tpText.richText = true;
            
            var infoObj = new GameObject("InfoText");
            infoObj.transform.SetParent(canvasObj.transform, false);
            _infoText = infoObj.AddComponent<TextMeshProUGUI>();
            _infoText.font = Resources.GetBuiltinResource<TMP_FontAsset>("LiberationSans SDF.asset");
            _infoText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _infoText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _infoText.rectTransform.pivot = new Vector2(0.5f, 0f);
            _infoText.rectTransform.anchoredPosition = new Vector2(0f, 35f);
            _infoText.rectTransform.sizeDelta = new Vector2(900f, 100f);
            _infoText.fontSize = 24;
            _infoText.alignment = TextAlignmentOptions.Center;
            _infoText.color = new Color(1f, 0.9f, 0.4f);
            _infoText.text = "Click a Chess Notation Node on the 3D board to inspect or unlock with TP.";
        }

        private void Build3DBoard()
        {
            if (_boardRoot != null) Destroy(_boardRoot);
            _nodeObjects.Clear();
            _objectToNode.Clear();

            _boardRoot = new GameObject("3DChessBoard");
            _boardRoot.transform.SetParent(transform);

            // Create 8x8 chessboard tiles
            for (int x = 0; x < 8; x++)
            {
                for (int z = 0; z < 8; z++)
                {
                    var baseTile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseTile.name = $"Tile_{x}_{z}";
                    baseTile.transform.SetParent(_boardRoot.transform);
                    baseTile.transform.position = new Vector3(x * _tileSize, -0.1f, z * _tileSize);
                    baseTile.transform.localScale = new Vector3(_tileSize * 0.96f, 0.15f, _tileSize * 0.96f);
                    
                    var renderer = baseTile.GetComponent<Renderer>();
                    renderer.material.color = (x + z) % 2 == 0 ? new Color(0.14f, 0.16f, 0.22f) : new Color(0.28f, 0.32f, 0.42f);
                    Destroy(baseTile.GetComponent<Collider>());
                }
            }

            if (_treeData == null) return;

            Dictionary<NotationNodeData, Vector2Int> nodePositions = new();
            HashSet<Vector2Int> usedTiles = new();

            // Place nodes based on their algebraic chess coordinates (a-h -> x=0-7, 1-8 -> z=0-7)
            foreach (var node in _treeData.Nodes)
            {
                if (node == null) continue;
                Vector2Int pos = ParseAlgebraicCoord(node.NotationCoord);
                if (usedTiles.Contains(pos))
                {
                    // Fallback shift if duplicate square
                    pos.x = (pos.x + 1) % 8;
                }
                nodePositions[node] = pos;
                usedTiles.Add(pos);
            }

            // Create 3D Notation Pieces/Cubes on the calculated coordinates
            foreach (var kvp in nodePositions)
            {
                var node = kvp.Key;
                var pos = kvp.Value;
                
                var nodeObj = GameObject.CreatePrimitive(node.NodeType == NotationNodeType.UltimateCheckmate ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                nodeObj.name = $"Node_{node.NodeId}_{node.NotationCoord}";
                nodeObj.transform.SetParent(_boardRoot.transform);

                float height = node.NodeType == NotationNodeType.UltimateCheckmate ? _nodeHeight * 2.2f : _nodeHeight;
                nodeObj.transform.position = new Vector3(pos.x * _tileSize, height / 2f + 0.05f, pos.y * _tileSize);
                nodeObj.transform.localScale = new Vector3(_tileSize * 0.55f, height / 2f, _tileSize * 0.55f);
                
                _nodeObjects[node] = nodeObj;
                _objectToNode[nodeObj] = node;
            }
        }

        private Vector2Int ParseAlgebraicCoord(string coord)
        {
            if (string.IsNullOrEmpty(coord)) return new Vector2Int(3, 3);
            
            int x = 3;
            int z = 3;

            coord = coord.ToLower().Replace("x", "").Replace("+", "").Replace("#", "");
            foreach (char c in coord)
            {
                if (c >= 'a' && c <= 'h')
                {
                    x = c - 'a';
                }
                else if (c >= '1' && c <= '8')
                {
                    z = c - '1';
                }
            }
            return new Vector2Int(Mathf.Clamp(x, 0, 7), Mathf.Clamp(z, 0, 7));
        }

        public void UpdateAllNodes()
        {
            if (_progressState == null) return;
            
            string unitName = _progressState.BoundUnitId;
            string title = _treeData != null ? _treeData.NotationTitle : "Notation Tree";
            int baseTP = _treeData != null ? _treeData.BaseNotationTP : 10;
            int bonusTP = _progressState.ResonanceBonusTP;
            
            _tpText.text = $"<b>♟️ [{unitName}] - {title}</b>\n" +
                           $"• Total Max TP: <color=#FFD700><b>{_progressState.TotalMaxTP} TP</b></color> <size=18>(Base {baseTP} + Resonance Bonus <color=#00FF44>+{bonusTP}</color>)</size>\n" +
                           $"• Remaining TP: <color=#00FFFF><b>{_progressState.RemainingTP} TP</b></color> / {_progressState.TotalMaxTP} <size=18>(Spent: {_progressState.SpentTP} TP)</size>";
            
            foreach (var kvp in _nodeObjects)
            {
                var node = kvp.Key;
                var obj = kvp.Value;
                var renderer = obj.GetComponent<Renderer>();
                
                if (_progressState.IsUnlocked(node))
                {
                    renderer.material.color = new Color(0.2f, 0.9f, 0.3f); // Green unlocked
                }
                else if (_progressState.CanUnlock(node))
                {
                    renderer.material.color = node.NodeType == NotationNodeType.UltimateCheckmate ? new Color(1f, 0.4f, 0.9f) : new Color(0.3f, 0.8f, 1f); // Cyan/Pink craftable
                }
                else
                {
                    renderer.material.color = new Color(0.35f, 0.35f, 0.4f); // Gray locked/insufficient TP
                }
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) && _puzzleCamera != null && _progressState != null)
            {
                Ray ray = _puzzleCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    if (_objectToNode.TryGetValue(hit.collider.gameObject, out NotationNodeData node))
                    {
                        if (_progressState.IsUnlocked(node))
                        {
                            _infoText.text = $"<b>[ {node.NotationCoord} - {node.NodeId} ]</b> (Already Unlocked)\n<color=#00FF88>✓ {node.Description}</color>";
                        }
                        else if (_progressState.CanUnlock(node))
                        {
                            if (_progressState.TryUnlock(node))
                            {
                                _infoText.text = $"<color=#00FF00><b>🎉 UNLOCKED [ {node.NotationCoord} - {node.NodeId} ]!</b> (-{node.TPCost} TP)</color>\n• Effect Applied: {node.Description}";
                                UpdateAllNodes();
                            }
                        }
                        else
                        {
                            string reason = _progressState.RemainingTP < node.TPCost ? $"<color=#FF3333>Insufficient TP ({_progressState.RemainingTP}/{node.TPCost} needed)</color>" : "<color=#FFAA33>Requires earlier notation prerequisites</color>";
                            _infoText.text = $"<b>[ {node.NotationCoord} - {node.NodeId} ]</b> ({node.NodeType} - {node.TPCost} TP)\n⚠️ LOCKED: {reason}\n📖 {node.Description}";
                        }
                    }
                }
            }
        }
    }
}
