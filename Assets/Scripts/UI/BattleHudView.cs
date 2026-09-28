using CheckmateRPG.Components;
using CheckmateRPG.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.UI
{
    /// <summary>Presentation only. Selection and health events drive the card; no combat polling.</summary>
    public sealed class BattleHudView : MonoBehaviour
    {
        public static readonly Color Surface = OutgameStyle.SurfaceDark;
        public static readonly Color Cyan = OutgameStyle.AccentCyan;
        public static readonly Color Muted = OutgameStyle.TextMuted;
        private Text _name, _detail, _hp, _sp, _hint, _stats, _skill, _piece;
        private Image _hpFill, _spFill, _portrait;
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
            var root = Rect(canvas, "OperatorCard", Vector2.zero, new Vector2(24, 24), new Vector2(370, 340));
            _card = root.gameObject;
            transform.SetParent(root, false);
            Panel(root, Surface);
            Panel(Rect(root, "Accent", new Vector2(0, 1), Vector2.zero, new Vector2(4, 340)), OutgameStyle.Paper);
            Label(root, "Eyebrow", "SELECTED UNIT / 전술 정보", new Vector2(20, -12), new Vector2(330, 22), 13, Muted);
            _portrait = Panel(Rect(root,"Portrait",new Vector2(0,1),new Vector2(20,-46),new Vector2(84,100)),OutgameStyle.SurfaceCardLight);
            _portrait.preserveAspect = true;
            _piece = Label(root,"Piece","+",new Vector2(20,-46),new Vector2(84,100),48,OutgameStyle.Paper);
            _piece.alignment = TextAnchor.MiddleCenter;
            _name = Label(root, "Name", "아군을 선택하세요", new Vector2(120, -46), new Vector2(230, 42), 24, Color.white);
            _detail = Label(root, "Detail", "유닛 선택 후 행동을 지정하세요", new Vector2(120, -94), new Vector2(230, 52), 14, Muted);
            Panel(Rect(root,"Rule",new Vector2(0,1),new Vector2(20,-158),new Vector2(330,1)),OutgameStyle.BorderSubtle);
            _stats = Label(root,"Stats","기본 능력  /  —",new Vector2(20,-164),new Vector2(330,46),15,OutgameStyle.TextWhite);
            _hp = Label(root, "Health", "HP  —", new Vector2(20, -215), new Vector2(160, 24), 15, Color.white);
            _sp = Label(root, "Skill", "SP  —", new Vector2(196, -215), new Vector2(155, 24), 15, Cyan);
            _hpFill = MakeBar(root, "HealthBar", new Vector2(20, -246), OutgameStyle.Paper);
            _spFill = MakeBar(root, "SkillBar", new Vector2(196, -246), Cyan);
            _skill = Label(root,"SkillInfo","장착 스킬  /  —",new Vector2(20,-264),new Vector2(330,60),14,Muted);
            _hint = Label(canvas, "ActionHint", "아군 선택 → 행동 선택 → 목표 지정", new Vector2(24, -24), new Vector2(650, 36), 20, Color.white);
            Panel(Rect(canvas, "HintBackdrop", new Vector2(0, 1), new Vector2(16, -16), new Vector2(680, 52)), Surface).transform.SetAsFirstSibling();
            var stage = Progression.StageManager.Instance?.CurrentStage;
            string objective = stage != null && stage.Objective == Progression.ObjectiveType.Annihilation ? "적 전원 격파" : "적 킹 격파";
            var objectivePanel = Rect(canvas, "OperationObjective", new Vector2(1, 1), new Vector2(-24, -24), new Vector2(350, 88));
            objectivePanel.pivot = new Vector2(1, 1);
            Panel(objectivePanel, Surface);
            Label(objectivePanel, "Objective", (stage != null ? stage.StageID : "OPERATION") + "  /  " + objective + "\n아군 킹 생존 유지", new Vector2(18, -12), new Vector2(314, 64), 19, OutgameStyle.TextWhite);
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
                _stats.text="기본 능력  /  —"; _skill.text="장착 스킬  /  —";
                _portrait.sprite=null;_portrait.color=OutgameStyle.SurfaceCardLight;
                _piece.text="+";_piece.gameObject.SetActive(true);
                SetFill(_hpFill, 0); SetFill(_spFill, 0);
                return;
            }
            var data = unit.UnitData;
            _name.text = data != null ? data.UnitName : unit.name;
            _detail.text = data != null ? $"{data.PieceType}  /  {(data.Style == Data.CombatStyle.Melee ? "근접" : "원거리")}  /  체급 {data.Weight}" : "";
            _portrait.sprite=data!=null?data.Icon:null;
            _portrait.color=_portrait.sprite!=null?Color.white:OutgameStyle.SurfaceCardLight;
            _piece.gameObject.SetActive(_portrait.sprite==null);
            _piece.text=data!=null?(data.PieceType==Data.ChessPieceType.Knight?"N":data.PieceType.ToString().Substring(0,1)):"?";
            _stats.text=data!=null?$"기본 능력  /  ATK {data.AttackDamage:0.#}   DEF {data.Defense:0.#}\nRES {data.Resistance:P0}   사거리 {data.AttackRange}칸   지원 {data.SupportPower:0.##}":"기본 능력  /  —";
            var skill=data!=null?data.GetSelectedActiveSkill():null;
            _skill.text=skill!=null?"장착 스킬  /  "+skill.SkillName:"장착 스킬  /  없음";
            if(skill!=null)
            {
                var charge=data.ChargeType;
                if(skill.Levels!=null && skill.Levels.Count>0)
                {
                    var level=skill.GetLevelData(1);
                    charge=level.ChargeType!=Data.SPChargeType.Auto?level.ChargeType:skill.DefaultChargeType;
                }
                _skill.text+="\n"+(charge==Data.SPChargeType.OnAttack?"공격 회복":charge==Data.SPChargeType.OnHit?"피격 회복":"자동 회복");
            }
            _health = unit.Health;
            _skillPoints = unit.SPComponent;
            if (_health != null) { _health.OnHealthChanged += HealthChanged; _health.OnDeath += SelectedUnitDied; HealthChanged(_health.CurrentHealth, _health.MaxHealth); }
            else { _hp.text="HP  —";SetFill(_hpFill,0); }
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
            if (_health != null) { _health.OnHealthChanged -= HealthChanged; _health.OnDeath -= SelectedUnitDied; }
            if (_skillPoints != null) _skillPoints.OnSPChanged -= SPChanged;
            _health = null; _skillPoints = null;
        }
        private void SelectedUnitDied() => Select(null);
        private void OnDestroy() => Unbind();
    }
}
