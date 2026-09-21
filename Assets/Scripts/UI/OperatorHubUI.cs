using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    // Existing OutgameView route and progression services remain the owners of state.
    public class OperatorHubUI : OutgameViewBase
    {
        [SerializeField] private Button _backButton;
        private RectTransform _roster, _content;
        private TextMeshProUGUI _name, _subtitle, _notice;
        private UnitData _selectedUnit;
        private string _tab = "능력치";
        private NotationTreeData[] _trees;
        private readonly List<UnitData> _units = new();
        private readonly Dictionary<UnitData, Button> _unitButtons = new();
        private readonly Dictionary<string, Button> _tabs = new();
        private static readonly Color Surface = new(.04f, .055f, .085f, 1);
        private static readonly Color Accent = new(.23f, .86f, .87f, 1);

        private void Awake()
        {
            ViewType = OutgameViewType.OperatorHub;
            UiFontProvider.EnsureTmpKoreanFallback();
            Build();
            if (_backButton != null) _backButton.onClick.AddListener(Back);
        }
        public override void Show() { base.Show(); LoadUnits(); }
        private void Back() => OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby);

        private void Build()
        {
            var root = Panel(transform, "OperatorWorkspace", Surface);
            Stretch(root, Vector2.zero, Vector2.one);
            var header = Panel(root, "Header", Surface);
            Stretch(header, new(0.02f,.87f), new(.98f,.98f));
            var back = MakeButton(header, "로비로", Back, 48);
            Stretch((RectTransform)back.transform, Vector2.zero, new(.16f,1));
            _name = Label(header,"오퍼레이터",32,44);
            Stretch(_name.rectTransform,new(.2f,.35f),Vector2.one);
            _subtitle = Label(header,"편성과 육성",17,26);
            Stretch(_subtitle.rectTransform,new(.2f,0),new(1,.36f));
            _roster = Scroll(root,"Roster",new(.02f,.09f),new(.22f,.85f));
            var tabs = Panel(root,"Tabs",Surface);
            Stretch(tabs,new(.24f,.78f),new(.98f,.85f));
            var horizontal = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing=8; horizontal.childControlWidth=true; horizontal.childControlHeight=true;
            foreach(string title in new[]{"능력치","스킬","기보","공명"})
            {
                string key=title;
                _tabs[key]=MakeButton(tabs,key,()=>{_tab=key; Render();},46);
            }
            _content = Scroll(root,"Details",new(.24f,.09f),new(.98f,.76f));
            _notice = Label(root,"오퍼레이터를 선택하세요.",17,28);
            Stretch(_notice.rectTransform,new(.24f,.02f),new(.98f,.07f));
        }
        private void LoadUnits()
        {
            _units.Clear(); _unitButtons.Clear();
            _trees = Resources.LoadAll<NotationTreeData>("Notations");
            var source = DeckManager.Instance != null ? DeckManager.Instance.AllUnitsDatabase : new List<UnitData>(Resources.LoadAll<UnitData>("Units"));
            var save = SaveManager.EnsureInstance().CurrentData;
            foreach(var unit in source)
            {
                if(unit==null || unit.AIProfile!=null || unit.name.StartsWith("Enemy",StringComparison.Ordinal) || unit.name.EndsWith("_UnitData",StringComparison.Ordinal)) continue;
                // An empty ownership list is the legacy starter-save format.
                if(save.UnlockedUnits.Count>0 && !save.UnlockedUnits.Contains(unit.name) && !save.CurrentDeck.Contains(unit.name)) continue;
                if(!_units.Contains(unit)) _units.Add(unit);
            }
            _units.Sort((a,b)=>string.Compare(a.UnitName,b.UnitName,StringComparison.Ordinal));
            Clear(_roster);
            Label(_roster,"오퍼레이터  /  "+_units.Count,18,34);
            foreach(var unit in _units)
            {
                var chosen=unit;
                _unitButtons[unit]=MakeButton(_roster,unit.UnitName+"\n<size=70%>"+unit.BaseRole+"</size>",()=>SelectUnit(chosen),78);
            }
            if(_selectedUnit==null || !_units.Contains(_selectedUnit)) _selectedUnit=_units.Count>0?_units[0]:null;
            Render();
        }
        private void SelectUnit(UnitData unit) { _selectedUnit=unit; _notice.text=""; Render(); }
        private void Render()
        {
            Clear(_content);
            foreach(var item in _tabs) item.Value.image.color=item.Key==_tab?new Color(.12f,.36f,.4f):new Color(.1f,.17f,.23f);
            foreach(var item in _unitButtons) item.Value.image.color=item.Key==_selectedUnit?new Color(.12f,.36f,.4f):new Color(.1f,.17f,.23f);
            if(_selectedUnit==null) { Label(_content,"보유한 오퍼레이터가 없습니다.",22,65); return; }
            _name.text=_selectedUnit.UnitName;
            _subtitle.text=_selectedUnit.BaseRole+"  /  "+(_selectedUnit.Style==CombatStyle.Melee?"근접 공격":"원거리 공격")+"  /  "+_tab;
            switch(_tab)
            {
                case "스킬": RenderSkills(); break;
                case "기보": RenderNotation(); break;
                case "공명": RenderResonance(); break;
                default: RenderStats(); break;
            }
        }
        private void RenderStats()
        {
            var save=SaveManager.EnsureInstance().CurrentData;
            var p=save.UnitProgressions.Find(x=>x!=null&&x.UnitId==_selectedUnit.name);
            var manager=DeckManager.Instance;
            var loadout=MetaProgressionBuilder.BuildUnitLoadout(p??new UnitProgressionSaveData{UnitId=_selectedUnit.name},manager!=null?manager.DefaultResonanceProfile:null,manager!=null?manager.AllNotationNodes:new List<NotationNodeData>());
            var preview=MetaProgressionCalculator.CreateModifiedUnitData(_selectedUnit,loadout);
            var u=preview!=null?preview:_selectedUnit;
            Label(_content,"전투 능력치",27,44);
            Label(_content,"기보·공명 보너스 반영  /  괄호 안은 기본 능력치",17,36);
            Label(_content,$"체력  {u.MaxHealth:0.#}  ({_selectedUnit.MaxHealth:0.#})     공격력  {u.AttackDamage:0.#}  ({_selectedUnit.AttackDamage:0.#})\n방어력  {u.Defense:0.#}     마법 저항  {u.Resistance:P0}\n이동 AP  {u.MoveCostAP:0.#}     공격 AP  {u.AttackCostAP:0.#}\n공격 범위  {u.AttackRange}칸     무게  {u.Weight}",23,160);
            Label(_content,"성장",25,42);
            Label(_content,"기보에서 개인 TP로 능력치를 해방하고, 중복 획득 시 공명으로 추가 TP와 직군 보너스를 얻습니다.",20,68);
            MakeButton(_content,"기보 성장 확인",()=>{_tab="기보";Render();},48);
            if(preview!=null) Destroy(preview);
        }
        private void RenderSkills()
        {
            Label(_content,"출격 스킬 선택",27,42);
            Label(_content,"장착한 스킬 하나를 전투에서 사용합니다. 선택은 즉시 저장됩니다.",18,42);
            var skills=_selectedUnit.SelectableActiveSkills;
            if(skills==null || skills.Count==0){Label(_content,"선택 가능한 액티브 스킬이 없습니다.",21,70);return;}
            int selected=UnitSkillRuntime.GetSelectedSkillIndex(_selectedUnit.name);
            for(int i=0;i<skills.Count;i++)
            {
                var skill=skills[i]; if(skill==null)continue;
                int index=i;
                var card=Card(_content,"Skill_"+i);
                Label(card,(i==selected?"장착 중  ·  ":"")+skill.SkillName,24,42);
                Label(card,skill.Description,19,88);
                int level=_selectedUnit.GetSkillLevel(skill.AbilityId);
                var lv=skill.GetLevelData(level);
                Label(card,$"스킬 Lv.{level}   /   AP {lv.Cost:0.#}   /   SP {lv.SPCost}   /   재사용 {lv.Cooldown}틱",17,34);
                MakeButton(card,i==selected?"장착 중":"이 스킬 장착",()=>{UnitSkillRuntime.SetSelectedSkillIndex(_selectedUnit.name,index);_notice.text=skill.SkillName+" 장착 완료";Render();},44,i!=selected);
            }
        }
        private void RenderNotation()
        {
            if(_selectedUnit.PieceType==ChessPieceType.King)
            {
                Label(_content,"킹은 칙령으로 부대를 강화합니다.",24,70);
                MakeButton(_content,"싱크로 보드 열기",()=>OutgameUIManager.Instance.ChangeView(OutgameViewType.SynchroBoard),48);return;
            }
            NotationTreeData tree=null;
            if(_trees!=null)foreach(var candidate in _trees)if(candidate!=null&&candidate.TargetUnitId==_selectedUnit.name){tree=candidate;break;}
            if(tree==null){Label(_content,"이 오퍼레이터의 기보는 아직 준비되지 않았습니다.",21,76);return;}
            var state=NotationProgressState.CreateForUnit(_selectedUnit.name,tree);
            Label(_content,tree.NotationTitle,27,42);
            Label(_content,$"개인 TP  {state.RemainingTP} / {state.TotalMaxTP}    ·    사용 {state.SpentTP}",21,38);
            Label(_content,"선행 수를 해방해 다음 수로 진행하세요. 최종 체크메이트에서 고유 보상을 획득합니다.",17,48);
            var nodes=new List<NotationNodeData>();
            if(tree.RootNode!=null)nodes.Add(tree.RootNode);
            if(tree.Nodes!=null)foreach(var n in tree.Nodes)if(n!=null&&!nodes.Contains(n))nodes.Add(n);
            foreach(var node in nodes)
            {
                var card=Card(_content,"Notation_"+node.NodeId);
                bool unlocked=state.IsUnlocked(node);
                Label(card,node.NotationCoord+"  ·  "+(node.NodeType==NotationNodeType.UltimateCheckmate?"체크메이트":"기보 해방")+(unlocked?"  [완료]":""),23,38);
                Label(card,node.Description,19,58);
                var rewards=new StringBuilder();
                if(node.Bonuses!=null)foreach(var b in node.Bonuses)
                {
                    if(b.FlatBonus!=0) rewards.Append(StatName(b.Stat)).Append(" ").Append(b.FlatBonus.ToString("+0.##;-0.##")).Append("   ");
                    if(b.PercentBonus!=0) rewards.Append(StatName(b.Stat)).Append(" ").Append(b.PercentBonus.ToString("+0%;-0%")).Append("   ");
                }
                Label(card,rewards.Length>0?rewards.ToString():"보상 설명을 확인하세요.",18,38);
                string reason="";
                if(!unlocked&&node.Prerequisites!=null)foreach(var n in node.Prerequisites)if(n!=null&&!state.IsUnlocked(n))reason+="선행 "+n.NotationCoord+" 필요  ";
                if(!unlocked&&state.RemainingTP<node.TPCost)reason+=$"TP {node.TPCost-state.RemainingTP} 부족";
                if(reason.Length>0)Label(card,reason,17,36);
                MakeButton(card,unlocked?"해방 완료":$"개인 TP {node.TPCost} · 해방",()=>{if(state.TryUnlock(node)){_notice.text=node.NotationCoord+" 해방 완료";Render();}},44,state.CanUnlock(node));
            }
        }
        private void RenderResonance()
        {
            var p=SaveManager.EnsureInstance().CurrentData.UnitProgressions.Find(x=>x!=null&&x.UnitId==_selectedUnit.name);
            int stage=p?.ResonanceStage??1;
            Label(_content,$"공명  {stage} / 5",28,46);
            Label(_content,"같은 오퍼레이터를 중복 획득하면 공명이 진행됩니다.",20,55);
            Label(_content,$"기보 보너스 TP  +{ResonanceSystem.GetNotationTP(stage)}",23,42);
            Label(_content,"공명 보너스는 기보 예산과 전투 능력치에 반영됩니다. 직군별 AP 할인은 3단계부터 적용됩니다.",20,78);
        }
        private static string StatName(MetaStatType stat)=>stat switch
        {
            MetaStatType.MaxHealth=>"체력",MetaStatType.AttackDamage=>"공격력",MetaStatType.Defense=>"방어력",MetaStatType.Resistance=>"마법 저항",MetaStatType.MoveRange=>"이동 범위",MetaStatType.AttackRange=>"공격 범위",MetaStatType.MoveCostAP=>"이동 AP",MetaStatType.AttackCostAP=>"공격 AP",MetaStatType.MaxSP=>"최대 SP",_=>"행동 속도"
        };
        private static RectTransform Panel(Transform parent,string name,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=color;return (RectTransform)go.transform;
        }
        private static void Stretch(RectTransform rt,Vector2 min,Vector2 max){rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;}
        private static RectTransform Scroll(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var viewport=Panel(parent,name,Surface);Stretch(viewport,min,max);viewport.gameObject.AddComponent<RectMask2D>();
            var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
            content.anchorMin=new(0,1);content.anchorMax=Vector2.one;content.pivot=new(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new(12,12,10,16);layout.spacing=10;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            return content;
        }
        private static RectTransform Card(Transform parent,string name)
        {
            var card=Panel(parent,name,new(.08f,.105f,.16f));var layout=card.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new(18,18,14,14);layout.spacing=6;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;return card;
        }
        private static TextMeshProUGUI Label(Transform parent,string text,float size,float height)
        {
            var go=new GameObject("Text",typeof(RectTransform));go.transform.SetParent(parent,false);var tmp=go.AddComponent<TextMeshProUGUI>();
            tmp.font=UiFontProvider.GetTmpFont();tmp.text=text;tmp.fontSize=size;tmp.color=new(.88f,.93f,1);tmp.raycastTarget=false;tmp.overflowMode=TextOverflowModes.Ellipsis;go.AddComponent<LayoutElement>().preferredHeight=height;return tmp;
        }
        private static Button MakeButton(Transform parent,string text,UnityEngine.Events.UnityAction action,float height,bool enabled=true)
        {
            var rt=Panel(parent,text,enabled?new(.1f,.17f,.23f):new(.1f,.12f,.16f));var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=rt.GetComponent<Image>();button.interactable=enabled;button.onClick.AddListener(action);rt.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
            var label=Label(rt,text,20,height);label.alignment=TextAlignmentOptions.Center;label.color=enabled?Accent:new(.5f,.6f,.65f);Stretch(label.rectTransform,Vector2.zero,Vector2.one);label.margin=new(8,4,8,4);return button;
        }
        private static void Clear(Transform parent){foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
    }
}
