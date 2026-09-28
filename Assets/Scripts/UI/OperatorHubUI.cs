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
        private RectTransform _portrait;
        private TextMeshProUGUI _count;
        private bool _sortByLevel;
        private TextMeshProUGUI _name, _subtitle, _notice;
        private UnitData _selectedUnit;
        private string _tab = "능력치";
        private NotationTreeData[] _trees;
        private readonly List<UnitData> _units = new();
        private readonly Dictionary<UnitData, Button> _unitButtons = new();
        private readonly Dictionary<string, Button> _tabs = new();
        private static readonly Color Surface = OutgameStyle.BackgroundBase;
        private static readonly Color Accent = OutgameStyle.TextWhite;

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
            Stretch(root, new Vector2(0,.065f),new Vector2(1,.9f));
            var header = Panel(root, "Header", Surface);
            Stretch(header, new(0.02f,.87f), new(.98f,.98f));
            var badge = Label(header,"OP / PERSONNEL",18,44);
            Stretch(badge.rectTransform,Vector2.zero,new(.35f,1));
            badge.color = OutgameStyle.TextMuted;
            _name = Label(header,"오퍼레이터",32,44);
            Stretch(_name.rectTransform,new(.6f,.35f),Vector2.one);
            _subtitle = Label(header,"편성과 육성",17,26);
            Stretch(_subtitle.rectTransform,new(.6f,0),new(1,.36f));
            _count = TacticalLayout.Label(root,"RosterCount","보유 대원",20,OutgameStyle.TextWhite,.025f,.81f,.32f,.05f);
            var sort = MakeButton(root,"희귀도순 / 레벨순",()=>{_sortByLevel=!_sortByLevel;LoadUnits();},40);
            TacticalLayout.Place(sort.transform,.37f,.815f,.19f,.045f);
            _roster = Scroll(root,"Roster",new(.02f,.09f),new(.56f,.79f),true);
            _roster.parent.GetComponent<Image>().color = OutgameStyle.Paper;
            var grid = _roster.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4; grid.cellSize = new Vector2(218,268);
            grid.spacing = new Vector2(12,16); grid.padding = new RectOffset(18,18,18,18);
            grid.childAlignment = TextAnchor.UpperCenter;
            _portrait = Panel(root,"SelectedPortrait",OutgameStyle.SurfaceCard);
            TacticalLayout.Place(_portrait,.59f,.62f,.39f,.23f);
            var tabs = Panel(root,"Tabs",Surface);
            Stretch(tabs,new(.59f,.54f),new(.98f,.60f));
            var horizontal = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing=8; horizontal.childControlWidth=true; horizontal.childControlHeight=true;
            foreach(string title in new[]{"능력치","레벨","스킬","기보","공명"})
            {
                string key=title;
                _tabs[key]=MakeButton(tabs,key,()=>{_tab=key; Render();},46);
            }
            _content = Scroll(root,"Details",new(.59f,.09f),new(.98f,.52f));
            _notice = Label(root,"오퍼레이터를 선택하세요.",17,28);
            Stretch(_notice.rectTransform,new(.02f,.02f),new(.98f,.07f));
        }
        private void LoadUnits()
        {
            Canvas.ForceUpdateCanvases();
            var grid = _roster.GetComponent<GridLayoutGroup>();
            float width = ((RectTransform)_roster.parent).rect.width;
            grid.cellSize = new Vector2(Mathf.Max(100,(width-72)/4),268);
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
            foreach(var unit in RecruitRosterCatalog.GetAllUnits())
            {
                if(unit!=null && !_units.Contains(unit) && save.UnlockedUnits.Contains(unit.name)) _units.Add(unit);
            }
            _units.Sort((a,b)=> {
                int order = _sortByLevel ? LevelOf(b).CompareTo(LevelOf(a)) : b.Rarity.CompareTo(a.Rarity);
                return order != 0 ? order : string.Compare(a.UnitName,b.UnitName,StringComparison.Ordinal);
            });
            Clear(_roster);
            _count.text=$"보유 대원  /  {_units.Count:00}     ·     {(_sortByLevel ? "레벨순" : "희귀도순")}";
            foreach(var unit in _units)
            {
                var chosen=unit;
                _unitButtons[unit]=CreatePersonnelCard(unit,()=>SelectUnit(chosen));
            }
            if(_selectedUnit==null || !_units.Contains(_selectedUnit)) _selectedUnit=_units.Count>0?_units[0]:null;
            _notice.text="목록에서 대원 선택  /  휠로 세부 정보 스크롤";
            Render();
        }
        private void SelectUnit(UnitData unit) { _selectedUnit=unit; _notice.text=""; Render(); }
        private void Render()
        {
            Clear(_content);
            foreach(var item in _tabs) OutgameStyle.ApplySelectionStyle(item.Value,item.Key==_tab);
            foreach(var item in _unitButtons)
            {
                TacticalLayout.Panel(item.Value.transform,"Selection",item.Key==_selectedUnit?OutgameStyle.AccentGold:OutgameStyle.BorderSubtle,0,0,1,.018f);
                item.Value.transform.Find("Level").GetComponent<TextMeshProUGUI>().text="Lv."+LevelOf(item.Key);
            }
            if(_selectedUnit==null) { Label(_content,"보유한 오퍼레이터가 없습니다.",22,65); return; }
            _name.text=_selectedUnit.UnitName;
            _subtitle.text=_selectedUnit.BaseRole+"  /  "+(_selectedUnit.Style==CombatStyle.Melee?"근접 공격":"원거리 공격")+"  /  "+_tab;
            RenderPortrait();
            switch(_tab)
            {
                case "스킬": RenderSkills(); break;
                case "레벨": RenderLevel(); break;
                case "기보": RenderNotation(); break;
                case "공명": RenderResonance(); break;
                default: RenderStats(); break;
            }
        }
        private static int LevelOf(UnitData unit)
        {
            var progress=SaveManager.EnsureInstance().CurrentData.UnitProgressions.Find(p=>p!=null&&p.UnitId==unit.name);
            return progress?.Level??1;
        }
        private Button CreatePersonnelCard(UnitData unit,UnityEngine.Events.UnityAction select)
        {
            var card=Panel(_roster,"Personnel_"+unit.name,OutgameStyle.SurfaceCard);
            var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card.GetComponent<Image>();
            OutgameStyle.ApplyPrimaryButtonStyle(button);button.onClick.AddListener(select);
            var art=TacticalLayout.Panel(card,"Portrait",OutgameStyle.SurfaceCardLight,.025f,.27f,.95f,.7f);
            if(unit.Icon!=null){art.sprite=unit.Icon;art.color=Color.white;art.preserveAspect=true;}
            else {
                var mark=TacticalLayout.Label(card,"Piece",unit.PieceType==ChessPieceType.Knight?"N":unit.PieceType.ToString().Substring(0,1),72,OutgameStyle.TextMuted,.12f,.40f,.76f,.4f);
                mark.alignment=TextAlignmentOptions.Center;mark.overflowMode=TextOverflowModes.Overflow;
            }
            TacticalLayout.Panel(card,"Nameplate",OutgameStyle.Ink,0,.018f,1,.29f);
            TacticalLayout.Label(card,"Rarity",new string('★',Mathf.Clamp(unit.Rarity,0,5)),19,OutgameStyle.AccentGold,.06f,.86f,.88f,.1f);
            TacticalLayout.Label(card,"Level","Lv."+LevelOf(unit),24,OutgameStyle.TextWhite,.07f,.30f,.86f,.13f).overflowMode=TextOverflowModes.Overflow;
            TacticalLayout.Label(card,"Name",unit.UnitName,23,OutgameStyle.TextWhite,.07f,.115f,.86f,.13f);
            TacticalLayout.Label(card,"Role",unit.BaseRole.ToString(),15,OutgameStyle.TextMuted,.07f,.035f,.86f,.075f);
            return button;
        }
        private void RenderPortrait()
        {
            var art=TacticalLayout.Panel(_portrait,"Art",OutgameStyle.SurfaceCardLight,0,0,.38f,1);
            art.sprite=_selectedUnit.Icon;art.preserveAspect=true;
            art.color=_selectedUnit.Icon!=null?Color.white:OutgameStyle.SurfaceCardLight;
            var mark=TacticalLayout.Label(_portrait,"Mark",_selectedUnit.PieceType==ChessPieceType.Knight?"N":_selectedUnit.PieceType.ToString().Substring(0,1),88,OutgameStyle.TextMuted,.04f,.14f,.30f,.7f);
            mark.gameObject.SetActive(_selectedUnit.Icon==null);mark.alignment=TextAlignmentOptions.Center;
            TacticalLayout.Label(_portrait,"Code","OPERATOR / PERSONNEL FILE",13,OutgameStyle.TextMuted,.43f,.78f,.54f,.12f);
            TacticalLayout.Label(_portrait,"Level","Lv."+LevelOf(_selectedUnit),48,OutgameStyle.TextWhite,.43f,.35f,.54f,.38f);
            TacticalLayout.Label(_portrait,"Rarity",new string('★',Mathf.Clamp(_selectedUnit.Rarity,0,5))+"  /  "+_selectedUnit.PieceType,18,OutgameStyle.AccentGold,.43f,.13f,.54f,.15f);
        }
        private void RenderLevel()
        {
            var save=SaveManager.EnsureInstance().CurrentData;
            var p=EconomyService.Unit(save,_selectedUnit.name);
            int rarity=Mathf.Clamp(_selectedUnit.Rarity,3,5), cap=EconomyService.GetMaxLevel(rarity,p.ResonanceStage);
            Label(_content,$"레벨 성장  /  {rarity}★",27,42);
            Label(_content,$"Lv.{p.Level}  ·  현재 EXP {p.CurrentExp:N0}  ·  다음 레벨 {EconomyService.RequiredExpForLevel(p.Level):N0} EXP  ·  상한 {cap}",21,42);
            Label(_content,$"공명 {p.ResonanceStage}단계  ·  에테르 크레딧 {save.AetherCredit:N0}",18,36);
            foreach(int tier in new[]{1,2,3,4})
            {
                int owned=EconomyService.RecordCount(save,tier), exp=EconomyService.RecordExp(tier);
                var card=Card(_content,"Record_"+tier);
                Label(card,$"T{tier} 전술 작전기록 칩",22,34);
                Label(card,$"보유 {owned:N0}  ·  1개당 +{exp:N0} EXP  ·  주입 비용 {exp/5:N0} 크레딧",18,34);
                int selectedTier=tier;
                MakeButton(card,"1개 주입",()=>
                {
                    bool ok=EconomyService.TryInjectRecords(save,_selectedUnit,selectedTier,1,out _,out string reason);
                    if(ok)SaveManager.EnsureInstance().SaveGame(); _notice.text=reason; Render();
                },42,owned>0&&p.Level<cap);
            }
            Label(_content,"공명 돌파는 현재 단계의 레벨 상한을 달성한 뒤 공명 탭에서 진행합니다.",17,42);
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
            Label(_content,"기보·공명 보너스가 반영된 현재 능력치",17,36);
            StatRow("생존 / SURVIVAL", "체력", $"{u.MaxHealth:0.#}", "물리 방어", $"{u.Defense:0.#}", "마법 저항", $"{u.Resistance:P0}");
            StatRow("전투 / COMBAT", "공격력", $"{u.AttackDamage:0.#}", "공격 범위", $"{u.AttackRange} 칸", "지원 계수", $"{u.SupportPower:0.00}");
            StatRow("행동 / ACTION", "이동 AP", $"{u.MoveCostAP:0.#}", "공격 AP", $"{u.AttackCostAP:0.#}", "무게", $"{u.Weight}");
            Label(_content,"성장",25,42);
            Label(_content,"기보에서 개인 TP로 능력치를 해방하고, 중복 획득 시 공명으로 추가 TP와 직군 보너스를 얻습니다.",20,68);
            MakeButton(_content,"기보 성장 확인",()=>{_tab="기보";Render();},48);
            if(preview!=null) Destroy(preview);
        }
        private void StatRow(string category, string a, string av, string b, string bv, string c, string cv)
        {
            Label(_content,category,15,26).color = OutgameStyle.TextMuted;
            var row = Panel(_content,category,Color.clear);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12; layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            StatCell(row,a,av); StatCell(row,b,bv); StatCell(row,c,cv);
        }
        private static void StatCell(Transform parent,string name,string value)
        {
            var cell = Panel(parent,name,OutgameStyle.SurfaceCard);
            cell.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var label = Label(cell,name,17,24);
            Stretch(label.rectTransform,new(.06f,.53f),new(.94f,.94f)); label.color = OutgameStyle.TextMuted;
            var number = Label(cell,value,30,38);
            Stretch(number.rectTransform,new(.06f,.06f),new(.94f,.56f));
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
                var charge = lv.ChargeType != SPChargeType.Auto ? lv.ChargeType : skill.DefaultChargeType;
                string recovery = charge == SPChargeType.OnAttack ? "공격 회복" : charge == SPChargeType.OnHit ? "피격 회복" : "자동 회복";
                Label(card,$"스킬 Lv.{level}   /   AP {lv.Cost:0.#}   /   필요 SP {lv.SPCost}   /   {recovery}",17,34);
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
            var save=SaveManager.EnsureInstance().CurrentData;
            p=EconomyService.Unit(save,_selectedUnit.name);
            Label(_content,$"공명 돌파  {Mathf.Clamp(stage,0,2)} / 2",28,46);
            Label(_content,$"개인 공명 증표 {p.OperatorCertificates} · 크레딧 {save.AetherCredit:N0}",22,48);
            Label(_content,"중복 대원은 증표로 전환됩니다. 증표와 크레딧으로 돌파하세요.",20,55);
            Label(_content,$"개인 기보 TP 보너스 +{EconomyService.ResonanceTP(stage)}",23,42);
            int certificates=stage==0?5:15;long credits=stage==0?(_selectedUnit.Rarity==3?10000:_selectedUnit.Rarity==4?20000:40000):(_selectedUnit.Rarity==3?30000:_selectedUnit.Rarity==4?60000:120000);
            MakeButton(_content,stage>=3?"돌파 완료":$"증표 {certificates} + 크레딧 {credits:N0} · 돌파",()=>
            {
                bool ok=EconomyService.TryUpgradeResonance(save,_selectedUnit.name,_selectedUnit,out string reason);
                if(ok)SaveManager.Instance.SaveGame();
                _notice.text=ok?"공명 돌파 완료":reason;Render();
            },48,stage<2 && p.Level>=EconomyService.GetMaxLevel(Mathf.Clamp(_selectedUnit.Rarity,3,5),stage));
        }        private static string StatName(MetaStatType stat)=>stat switch
        {
            MetaStatType.MaxHealth=>"체력",MetaStatType.AttackDamage=>"공격력",MetaStatType.Defense=>"방어력",MetaStatType.Resistance=>"마법 저항",MetaStatType.MoveRange=>"이동 범위",MetaStatType.AttackRange=>"공격 범위",MetaStatType.MoveCostAP=>"이동 AP",MetaStatType.AttackCostAP=>"공격 AP",MetaStatType.MaxSP=>"최대 SP",MetaStatType.SupportPower=>"지원 계수",_=>"행동 속도"
        };
        private static RectTransform Panel(Transform parent,string name,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=color;return (RectTransform)go.transform;
        }
        private static void Stretch(RectTransform rt,Vector2 min,Vector2 max){rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;}
        private static RectTransform Scroll(Transform parent,string name,Vector2 min,Vector2 max,bool grid=false)
        {
            var viewport=Panel(parent,name,Surface);Stretch(viewport,min,max);viewport.gameObject.AddComponent<RectMask2D>();
            var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
            content.anchorMin=new(0,1);content.anchorMax=Vector2.one;content.pivot=new(.5f,1);content.sizeDelta=Vector2.zero;
            if(grid) content.gameObject.AddComponent<GridLayoutGroup>();
            else { var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new(12,12,10,16);layout.spacing=10;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false; }
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            return content;
        }
        private static RectTransform Card(Transform parent,string name)
        {
            var card=Panel(parent,name,OutgameStyle.SurfaceCard);var layout=card.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new(18,18,14,14);layout.spacing=6;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;return card;
        }
        private static TextMeshProUGUI Label(Transform parent,string text,float size,float height)
        {
            var go=new GameObject("Text",typeof(RectTransform));go.transform.SetParent(parent,false);var tmp=go.AddComponent<TextMeshProUGUI>();
            tmp.font=UiFontProvider.GetTmpFont();tmp.text=text;tmp.fontSize=size;tmp.color=OutgameStyle.TextWhite;tmp.raycastTarget=false;tmp.overflowMode=TextOverflowModes.Ellipsis;go.AddComponent<LayoutElement>().preferredHeight=height;return tmp;
        }
        private static Button MakeButton(Transform parent,string text,UnityEngine.Events.UnityAction action,float height,bool enabled=true)
        {
            var rt=Panel(parent,text,OutgameStyle.SurfaceCard);var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=rt.GetComponent<Image>();button.interactable=enabled;button.onClick.AddListener(action);rt.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
            var label=Label(rt,text,20,height);label.alignment=TextAlignmentOptions.Center;label.color=enabled?Accent:new(.5f,.6f,.65f);Stretch(label.rectTransform,Vector2.zero,Vector2.one);label.margin=new(16,4,16,4);OutgameStyle.ApplyLightButtonStyle(button,label);return button;
        }
        private static void Clear(Transform parent){foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
    }
}
