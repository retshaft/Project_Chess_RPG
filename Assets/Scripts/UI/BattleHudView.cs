using CheckmateRPG.Components;
using CheckmateRPG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.UI
{
    /// <summary>Presentation only. Selection and health events drive the card; no combat polling.</summary>
    public sealed class BattleHudView : MonoBehaviour
    {
        public static readonly Color Surface = new Color(0.07f, 0.085f, 0.105f, 0.96f);
        public static readonly Color Cyan = new Color(0f, 0.9f, 1f);
        public static readonly Color Muted = new Color(0.65f, 0.72f, 0.78f);
        private Text _name, _detail, _hp, _sp, _hint;
        private Image _hpFill, _spFill;
        private HealthComponent _health;
        private SPComponent _skillPoints;
        private GameObject _card;

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var rt = Rect(parent, name, new Vector2(0f, 1f), position, size);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = UiFontProvider.GetLegacyFont();
            text.fontSize = fontSize;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        public static Image Panel(RectTransform rt, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public void Build(Transform canvas)
        {
            var root = Rect(canvas, "OperatorCard", Vector2.zero, new Vector2(24, 24), new Vector2(370, 184));
            _card = root.gameObject;
            transform.SetParent(root, false);
            Panel(root, Surface);
            Panel(Rect(root, "Accent", new Vector2(0, 1), Vector2.zero, new Vector2(4, 184)), Cyan);
            Label(root, "Eyebrow", "OPERATOR / 선택 유닛", new Vector2(20, -12), new Vector2(330, 22), 13, Muted);
            _name = Label(root, "Name", "아군을 선택하세요", new Vector2(20, -37), new Vector2(330, 34), 24, Color.white);
            _detail = Label(root, "Detail", "유닛 선택 후 행동과 목적지를 지정합니다", new Vector2(20, -74), new Vector2(330, 24), 13, Muted);
            _hp = Label(root, "Health", "HP  —", new Vector2(20, -106), new Vector2(150, 24), 15, Color.white);
            _sp = Label(root, "Skill", "SP  —", new Vector2(196, -106), new Vector2(155, 24), 15, Cyan);
            _hpFill = MakeBar(root, "HealthBar", new Vector2(20, -140), new Color(0.55f, 0.85f, 0.65f));
            _spFill = MakeBar(root, "SkillBar", new Vector2(196, -140), Cyan);
            _hint = Label(canvas, "ActionHint", "아군 선택 → 행동 선택 → 목표 지정", new Vector2(24, -24), new Vector2(650, 36), 20, Color.white);
            Panel(Rect(canvas, "HintBackdrop", new Vector2(0, 1), new Vector2(16, -16), new Vector2(680, 52)), Surface).transform.SetAsFirstSibling();
            Select(null);
            SetBattleActive(false);
        }

        public void SetBattleActive(bool active)
        {
            _card.SetActive(active);
            Hint(active ? "아군 선택 → 행동 선택 → 목표 지정" : "배치 준비 · 유닛을 아래쪽 배치 칸으로 드래그하세요");
        }

        private static Image MakeBar(Transform root, string name, Vector2 position, Color color)
        {
            var track = Rect(root, name, new Vector2(0, 1), position, new Vector2(154, 5));
            Panel(track, new Color(0.22f, 0.27f, 0.3f));
            var fill = Rect(track, "Fill", Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMax = Vector2.one;
            return Panel(fill, color);
        }

        public void Select(UnitBrain unit)
        {
            Unbind();
            if (unit == null || unit.IsDead)
            {
                _name.text = "아군을 선택하세요";
                _detail.text = "유닛 선택 후 행동과 목적지를 지정합니다";
                _hp.text = "HP  —"; _sp.text = "SP  —";
                SetFill(_hpFill, 0); SetFill(_spFill, 0);
                return;
            }
            var data = unit.UnitData;
            _name.text = data != null ? data.UnitName : unit.name;
            _detail.text = data != null ? $"{data.PieceType}  /  {(data.Style == Data.CombatStyle.Melee ? "근접" : "원거리")}  /  체급 {data.Weight}" : "";
            _health = unit.Health;
            _skillPoints = unit.SPComponent;
            if (_health != null) { _health.OnHealthChanged += HealthChanged; HealthChanged(_health.CurrentHealth, _health.MaxHealth); }
            if (_skillPoints != null) { _skillPoints.OnSPChanged += SPChanged; SPChanged(_skillPoints.CurrentSP, _skillPoints.MaxSP); }
            else { _sp.text = "SP  —"; SetFill(_spFill, 0); }
        }

        public void Hint(string message) { if (_hint != null) _hint.text = message; }
        private void HealthChanged(float current, float max)
        {
            _hp.text = $"HP  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            SetFill(_hpFill, max > 0 ? current / max : 0);
        }
        private void SPChanged(float current, float max)
        {
            _sp.text = max > 0 && current >= max ? "SP  READY" : $"SP  {Mathf.FloorToInt(current)} / {Mathf.CeilToInt(max)}";
            SetFill(_spFill, max > 0 ? current / max : 0);
        }
        private static void SetFill(Image fill, float ratio) => fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        private void Unbind()
        {
            if (_health != null) _health.OnHealthChanged -= HealthChanged;
            if (_skillPoints != null) _skillPoints.OnSPChanged -= SPChanged;
            _health = null; _skillPoints = null;
        }
        private void OnDestroy() => Unbind();
    }
}
