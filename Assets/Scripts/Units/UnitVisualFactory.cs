// UnitVisualFactory.cs
// Creates the visual child object for spawned units.
// Resolution order: UnitData.Prefab > default chess model in
// Resources/Models/ChessUnits > capsule primitive.

using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Data;

namespace CheckmateRPG.Units
{
    public static class UnitVisualFactory
    {
        private const string DefaultModelPathPrefix = "Models/ChessUnits/Mid";

        // Prefab에 유닛 로직이 통째로 들어 있는 경우(예: 테스트용 유닛 프리팹) 비주얼로 쓸 때
        // 로직 컴포넌트가 이중으로 등록되지 않도록 제거한다.
        private static readonly System.Type[] UnitLogicComponents =
        {
            typeof(UnitBrain),
            typeof(HealthComponent),
            typeof(MovementComponent),
            typeof(CombatComponent),
            typeof(StatusEffectComponent),
            typeof(SPComponent),
            typeof(TeamComponent),
            typeof(UnitVisualController)
        };

        // 대상 높이(월드 단위). 타일 크기 2를 기준으로 체스 기물 비율을 따른다 (2026-10-01 사용자 요청으로 1.6배 확대).
        private static float GetTargetHeight(ChessPieceType pieceType)
        {
            return pieceType switch
            {
                ChessPieceType.Pawn => 1.35f,
                ChessPieceType.Knight => 1.6f,
                ChessPieceType.Bishop => 1.75f,
                ChessPieceType.Rook => 1.5f,
                ChessPieceType.Queen => 2.1f,
                ChessPieceType.King => 2.4f,
                _ => 1.6f
            };
        }

        /// <summary>
        /// 유닛 루트 아래에 "Visual" 자식을 만들고 반환한다.
        /// 캐릭터 프리팹이 있으면 그것을, 없으면 기물 종류에 맞는 기본 체스 모델을,
        /// 둘 다 없으면 캡슐 프리미티브를 사용한다.
        /// </summary>
        public static GameObject CreateVisual(UnitData data, Transform parent, bool isEnemy)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);

            var pieceType = data != null ? data.PieceType : ChessPieceType.Pawn;
            GameObject source = data != null && data.Prefab != null
                ? data.Prefab
                : LoadDefaultModel(pieceType);

            if (source != null)
            {
                var instance = Object.Instantiate(source, visual.transform, false);
                StripColliders(instance.transform);
                StripUnitLogic(instance);
                NormalizeHeight(visual.transform, instance.transform, GetTargetHeight(pieceType));
                // 기본 체스 모델은 재질이 없으므로 팀 색상 재질을 만들어 구분한다.
                if (data == null || data.Prefab == null)
                    ApplyTeamTint(instance, isEnemy);
                return visual;
            }

            // 최종 폴백: 캡슐 프리미티브 (기물 스케일과 동일하게 1.6배)
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(visual.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            capsule.transform.localScale = new Vector3(1.04f, 0.8f, 1.04f);
            Object.Destroy(capsule.GetComponent<Collider>());
            ApplyTeamTint(capsule, isEnemy);
            return visual;
        }

        private static GameObject LoadDefaultModel(ChessPieceType pieceType)
        {
            var prefab = Resources.Load<GameObject>(DefaultModelPathPrefix + pieceType);
            if (prefab == null)
                Debug.LogWarning($"[UnitVisualFactory] Default model not found: {DefaultModelPathPrefix + pieceType}. Using capsule fallback.");
            return prefab;
        }

        /// <summary>
        /// 모델 원본 스케일과 무관하게 렌더러 경계를 기준으로 높이를 맞추고,
        /// 바닥이 y=0에 닿도록 배치한다. 기준 프레임은 wrapper이며
        /// 인스턴스 루트의 회전은 건드리지 않는다.
        /// </summary>
        private static void NormalizeHeight(Transform wrapper, Transform instance, float targetHeight)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            if (!TryComputeLocalBounds(wrapper, renderers, out var bounds)) return;

            float scale = targetHeight / Mathf.Max(bounds.size.y, 0.0001f);
            instance.localScale = instance.localScale * scale;
            instance.localPosition = new Vector3(
                -bounds.center.x * scale,
                -bounds.min.y * scale,
                -bounds.center.z * scale);
        }

        private static bool TryComputeLocalBounds(Transform frame, Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool initialized = false;
            foreach (var renderer in renderers)
            {
                var world = renderer.bounds;
                EncapsulateLocal(frame, world.min, ref bounds, ref initialized);
                EncapsulateLocal(frame, world.max, ref bounds, ref initialized);
                EncapsulateLocal(frame, new Vector3(world.min.x, world.min.y, world.max.z), ref bounds, ref initialized);
                EncapsulateLocal(frame, new Vector3(world.min.x, world.max.y, world.min.z), ref bounds, ref initialized);
                EncapsulateLocal(frame, new Vector3(world.max.x, world.min.y, world.min.z), ref bounds, ref initialized);
                EncapsulateLocal(frame, new Vector3(world.max.x, world.max.y, world.min.z), ref bounds, ref initialized);
                EncapsulateLocal(frame, new Vector3(world.max.x, world.min.y, world.max.z), ref bounds, ref initialized);
                EncapsulateLocal(frame, new Vector3(world.min.x, world.max.y, world.max.z), ref bounds, ref initialized);
            }
            return initialized;
        }

        private static void EncapsulateLocal(Transform frame, Vector3 worldPoint, ref Bounds bounds, ref bool initialized)
        {
            var local = frame.InverseTransformPoint(worldPoint);
            if (!initialized)
            {
                bounds.SetMinMax(local, local);
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(local);
            }
        }

        private static void StripColliders(Transform root)
        {
            // 물리는 그리드 기반으로 처리하므로 비주얼의 콜라이더는 제거한다.
            foreach (var collider in root.GetComponentsInChildren<Collider>())
                Object.Destroy(collider);
        }

        private static void StripUnitLogic(GameObject instance)
        {
            foreach (var type in UnitLogicComponents)
            {
                foreach (var component in instance.GetComponentsInChildren(type, true))
                    Object.Destroy(component);
            }
        }

        private static void ApplyTeamTint(GameObject visual, bool isEnemy)
        {
            // 체스 스타일: 아군 = 백색, 적 = 흑색 (조명에서 형태가 보이도록 순백/순흑은 피한다)
            var color = isEnemy
                ? new Color(0.10f, 0.10f, 0.10f)
                : new Color(0.95f, 0.95f, 0.95f);

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                // 기본 체스 모델 FBX에는 재질이 없으므로 폴백 셰이더를 만들어 준다.
                var mat = renderer.sharedMaterial != null
                    ? new Material(renderer.sharedMaterial)
                    : new Material(GetFallbackShader());
                mat.color = color;
                // URP uses _BaseColor; setting both is harmless
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                renderer.material = mat;
            }
        }

        private static Shader GetFallbackShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit");
        }
    }
}
