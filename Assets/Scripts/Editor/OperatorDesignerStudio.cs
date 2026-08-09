#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CheckmateRPG.Data;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Abilities;
using System.Collections.Generic;
using System.IO;

namespace CheckmateRPG.Editor
{
    public class OperatorDesignerStudio : EditorWindow
    {
        private Vector2 _scrollPos;
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "1. # 기본 정보 (Identity)", "2. # 메커니즘 & 수치 (Stats)", "3. # 스킬 (Skills & Passives)", "4. 에셋 생성 (Bake & Export)" };

        // ─── Basic Info ───
        private string _unitId = "Kiara";
        private string _unitName = "키아라 (Kiara)";
        private ChessPieceType _pieceType = ChessPieceType.Knight;
        private UnitSubclassType _subclass = UnitSubclassType.Swordmaster;
        private ResonanceRoleType _resonanceRole = ResonanceRoleType.Offensive;
        private int _weight = 2; // Middle
        private int _starRating = 5; // ★★★★★
        private Sprite _icon;
        private GameObject _prefab;
        private string _visualDescription = "금발, 네온 그래피티가 그려진 테크웨어, 긴 검(주무기)과 짧은 검(보조무기)";

        // ─── Mechanics & Stats ───
        private float _maxHealth = 700f;
        private float _attackDamage = 155f;
        private float _defense = 32f;
        private float _resistance = 0.05f; // 5%
        private int _speedLevel = 5; // 매우 빠름
        private float _moveCostAP = 24f;
        private float _attackCostAP = 24f;
        private int _moveRange = 3;
        private int _attackRange = 1;
        private int _attackCount = 2; // Swordmaster multi-hit
        private float _attackDamageRatio = 0.65f; // 65% per hit (Total 130%)
        private CombatStyle _combatStyle = CombatStyle.Melee;
        private SPChargeType _defaultChargeType = SPChargeType.Auto;
        private float _maxSP = 120f;
        private float _initSP = 70f;

        // ─── Skills (이하 중 택 1) ───
        [System.Serializable]
        public class ActiveSkillEditorData
        {
            public string skillId;
            public string skillName;
            public string description;
            public int spCost;
            public int initSP;
            public SPChargeType chargeType;
            public int cooldown;
        }

        private List<ActiveSkillEditorData> _selectableSkills = new List<ActiveSkillEditorData>
        {
            new ActiveSkillEditorData { skillId = "Kiara_Active1", skillName = "1. 연참 (공격 회복)", description = "다음 공격의 공격력이 50% 증가하고 다음 공격이 3회 공격한다.", spCost = 5, initSP = 0, chargeType = SPChargeType.OnAttack, cooldown = 1 },
            new ActiveSkillEditorData { skillId = "Kiara_Active2", skillName = "2. 비장검 (자동 회복)", description = "다음 3회의 행동에 소모하는 AP가 4 감소하고, 공격력이 30% 증가한다.", spCost = 84, initSP = 32, chargeType = SPChargeType.Auto, cooldown = 3 },
            new ActiveSkillEditorData { skillId = "Kiara_Active3", skillName = "3. 황우의 극검 - 카오틱 오버드라이브", description = "공격 범위 내 전원 물리 광역 피해 후, 24초 동안 행동 간격 60% 단축, AP 소모 반감, 단타 70% 전환 및 매 타격 출혈 부여.", spCost = 120, initSP = 70, chargeType = SPChargeType.Auto, cooldown = 5 }
        };

        private string _passiveDescription1 = "1. 매 3회 공격 시 마다 대상에게 출혈을 1회 부여한다.";
        private string _passiveDescription2 = "2. 출혈 상태인 적에게 다음 공격이 대상의 방어력을 15% 무시한다. (쿨타임 70초, 출혈 부여 시마다 12초 감소)";

        [MenuItem("CheckmateRPG/Operator Designer Studio", false, 1)]
        [MenuItem("Tools/Operator Designer Studio", false, 1)]
        public static void ShowWindow()
        {
            var win = GetWindow<OperatorDesignerStudio>("Operator Studio");
            win.minSize = new Vector2(550, 700);
            win.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(30));
            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawIdentityTab(); break;
                case 1: DrawStatsTab(); break;
                case 2: DrawSkillsTab(); break;
                case 3: DrawBakeTab(); break;
            }

            EditorGUILayout.EndScrollView();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("⚔️ Checkmate RPG : 서브컬처 오퍼레이터 디자인 스튜디오", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Load Sample: Kiara.md", EditorStyles.toolbarButton))
            {
                LoadKiaraTemplate();
            }
            if (GUILayout.Button("Load: Elena.md (저격수)", EditorStyles.toolbarButton))
            {
                LoadElenaTemplate();
            }
            if (GUILayout.Button("Load: Vesta.md (공성병기)", EditorStyles.toolbarButton))
            {
                LoadVestaTemplate();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void DrawIdentityTab()
        {
            EditorGUILayout.LabelField("📌 기본 정보 (Basic Identity)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Docs/Characters/<Character>.md 문서의 '# 기본 정보' 항목과 매핑되는 설정입니다.", MessageType.Info);

            _unitId = EditorGUILayout.TextField("에셋 영문 ID (File Name)", _unitId);
            _unitName = EditorGUILayout.TextField("오퍼레이터 이름", _unitName);
            _pieceType = (ChessPieceType)EditorGUILayout.EnumPopup("체스 기물 타입", _pieceType);
            _subclass = (UnitSubclassType)EditorGUILayout.EnumPopup("직군 (Subclass)", _subclass);
            _resonanceRole = (ResonanceRoleType)EditorGUILayout.EnumPopup("공명 프리셋 역할군", _resonanceRole);
            _weight = EditorGUILayout.IntSlider("체급 (Weight, 0~4)", _weight, 0, 4);
            _starRating = EditorGUILayout.IntSlider("별 등급 (Star Grade)", _starRating, 1, 5);

            string stars = new string('★', _starRating);
            EditorGUILayout.LabelField("등급 표기", stars, EditorStyles.boldLabel);

            _icon = (Sprite)EditorGUILayout.ObjectField("초상화 아이콘 (Icon)", _icon, typeof(Sprite), false);
            _prefab = (GameObject)EditorGUILayout.ObjectField("인게임 프리팹 (Prefab)", _prefab, typeof(GameObject), false);

            EditorGUILayout.LabelField("비주얼 및 테크웨어 설정 (Visual Notes)");
            _visualDescription = EditorGUILayout.TextArea(_visualDescription, GUILayout.Height(60));
        }

        private void DrawStatsTab()
        {
            EditorGUILayout.LabelField("📊 핵심 메커니즘 및 수치 (Mechanics & Stats)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("체스 기물의 기본 이동 방식과 검객 등 특수 직군의 다타 속성을 조정합니다.", MessageType.Info);

            EditorGUILayout.LabelField("■ 체스 행동 & 비용", EditorStyles.boldLabel);
            _moveCostAP = EditorGUILayout.FloatField("이동 AP 소모량 (Move Cost AP)", _moveCostAP);
            _attackCostAP = EditorGUILayout.FloatField("공격 AP 소모량 (Attack Cost AP)", _attackCostAP);
            _speedLevel = EditorGUILayout.IntSlider("행동 속도 등급 (Speed Level 0~6)", _speedLevel, 0, 6);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("■ 공격 로직 & 배율", EditorStyles.boldLabel);
            _combatStyle = (CombatStyle)EditorGUILayout.EnumPopup("전투 스타일 (Melee/Range)", _combatStyle);
            _attackCount = EditorGUILayout.IntSlider("행동당 연속 타격수 (Attack Count)", _attackCount, 1, 10);
            _attackDamageRatio = EditorGUILayout.Slider("타격당 피해 배율 (Ratio per Hit)", _attackDamageRatio, 0.1f, 3.0f);
            EditorGUILayout.LabelField("총합 예상 피해 배율:", $"{_attackCount * _attackDamageRatio * 100:F0}%");
            _attackRange = EditorGUILayout.IntField("공격 사거리 (Attack Range)", _attackRange);
            _moveRange = EditorGUILayout.IntField("이동 사거리 (Move Range)", _moveRange);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("■ Lv.1 기준 스테이터스", EditorStyles.boldLabel);
            _maxHealth = EditorGUILayout.FloatField("체력 (Max HP)", _maxHealth);
            _attackDamage = EditorGUILayout.FloatField("공격력 (Attack Damage)", _attackDamage);
            _defense = EditorGUILayout.FloatField("방어력 (Defense)", _defense);
            _resistance = EditorGUILayout.Slider("저항력 (Resistance, 0~1)", _resistance, 0f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("■ SP 리소스 설정 (초기값)", EditorStyles.boldLabel);
            _defaultChargeType = (SPChargeType)EditorGUILayout.EnumPopup("기본 SP 회복 방식", _defaultChargeType);
            _maxSP = EditorGUILayout.FloatField("대표 최대 SP", _maxSP);
            _initSP = EditorGUILayout.FloatField("대표 초기 SP", _initSP);
        }

        private void DrawSkillsTab()
        {
            EditorGUILayout.LabelField("⚔️ 스킬 및 패시브 설계 (Skills & Passives)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("서브컬처 오퍼레이터 특유의 '이하 중 택 1' 액티브 스킬 구성 및 카운터 패시브를 설정합니다.", MessageType.Info);

            EditorGUILayout.LabelField("■ 패시브 스킬 (Passive Skills)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("패시브 1 (타수 트리거 등)");
            _passiveDescription1 = EditorGUILayout.TextArea(_passiveDescription1, GUILayout.Height(40));
            EditorGUILayout.LabelField("패시브 2 (상태이상 연동 등)");
            _passiveDescription2 = EditorGUILayout.TextArea(_passiveDescription2, GUILayout.Height(40));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("■ 액티브 스킬 - [이하 중 택 1] 목록", EditorStyles.boldLabel);

            for (int i = 0; i < _selectableSkills.Count; i++)
            {
                var skill = _selectableSkills[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"👉 액티브 스킬 #{i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("❌ Remove", GUILayout.Width(70)))
                {
                    _selectableSkills.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                skill.skillId = EditorGUILayout.TextField("스킬 ID", skill.skillId);
                skill.skillName = EditorGUILayout.TextField("스킬명 (UI & 컷인용)", skill.skillName);
                skill.spCost = EditorGUILayout.IntField("요구 SP (Max SP)", skill.spCost);
                skill.initSP = EditorGUILayout.IntField("초기 SP (Init SP)", skill.initSP);
                skill.chargeType = (SPChargeType)EditorGUILayout.EnumPopup("SP 회복 방식", skill.chargeType);
                skill.cooldown = EditorGUILayout.IntField("재사용 쿨타임 (턴/초)", skill.cooldown);
                EditorGUILayout.LabelField("스킬 설명 및 폼 전환 효과:");
                skill.description = EditorGUILayout.TextArea(skill.description, GUILayout.Height(40));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("➕ 액티브 스킬 추가 (Add Active Skill)", GUILayout.Height(28)))
            {
                _selectableSkills.Add(new ActiveSkillEditorData
                {
                    skillId = $"{_unitId}_Active{_selectableSkills.Count + 1}",
                    skillName = $"신규 스킬 #{_selectableSkills.Count + 1}",
                    description = "스킬 효과를 입력하세요.",
                    spCost = 50,
                    initSP = 10,
                    chargeType = SPChargeType.Auto
                });
            }
        }

        private void DrawBakeTab()
        {
            EditorGUILayout.LabelField("⚡ 원클릭 에셋 생성 및 동기화 (Bake Assets)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("설정한 정보에 맞추어 UnitData와 AbilityDefinition 에셋을 Resources 폴더 내에 일괄 생성 및 자동 링크 바인딩합니다.", MessageType.Warning);

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.3f);
            if (GUILayout.Button("🔥 Save & Generate Operator Assets (에셋 즉시 Bake)", GUILayout.Height(45)))
            {
                BakeCurrentOperator();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("생성될 에셋 경로 미리보기:", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"• Unit Data: Assets/Resources/Units/{_unitId}.asset");
            foreach (var s in _selectableSkills)
            {
                EditorGUILayout.LabelField($"• Ability: Assets/Resources/Abilities/{s.skillId}.asset");
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("💡 Tip: 기획자가 의도한 수치를 수술실(Studio)에서 즉각 셋업하고, 테스트 샌드박스에서 검증해보세요!", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void LoadKiaraTemplate()
        {
            _unitId = "Kiara";
            _unitName = "키아라";
            _pieceType = ChessPieceType.Knight;
            _subclass = UnitSubclassType.Swordmaster;
            _resonanceRole = ResonanceRoleType.Offensive;
            _weight = 2;
            _starRating = 5;
            _visualDescription = "금발, 네온 그래피티가 그려진 테크웨어, 긴 검(주무기)과 짧은 검(보조무기)";

            _maxHealth = 700f;
            _attackDamage = 155f;
            _defense = 32f;
            _resistance = 0.05f;
            _speedLevel = 5;
            _moveCostAP = 24f;
            _attackCostAP = 24f;
            _attackCount = 2;
            _attackDamageRatio = 0.65f;
            _combatStyle = CombatStyle.Melee;
            _defaultChargeType = SPChargeType.Auto;
            _maxSP = 120f;
            _initSP = 70f;

            _selectableSkills.Clear();
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Kiara_Active1", skillName = "황혼의 연격 (1스킬)", description = "다음 공격의 공격력이 50% 증가하고 다음 공격이 3회 공격한다.", spCost = 5, initSP = 0, chargeType = SPChargeType.OnAttack, cooldown = 1 });
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Kiara_Active2", skillName = "비장검 (2스킬)", description = "다음 3회의 행동에 소모하는 AP가 4 감소하고, 공격력이 30% 증가한다.", spCost = 84, initSP = 32, chargeType = SPChargeType.Auto, cooldown = 3 });
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Kiara_Active3", skillName = "황우의 극검 - 카오틱 오버드라이브", description = "공격 범위 내 전원 물리 광역 피해 후, 24초간 행동 간격 60% 단축, AP 반감, 단타 전환 및 매 타격 출혈 부여.", spCost = 120, initSP = 70, chargeType = SPChargeType.Auto, cooldown = 5 });

            _passiveDescription1 = "1. 매 3회 공격 시 마다 대상에게 출혈을 1회 부여한다.";
            _passiveDescription2 = "2. 출혈 상태인 적에게 다음 공격이 대상의 방어력을 15% 무시한다. (쿨타임 70초, 출혈 부여 시마다 12초 단축)";
            Repaint();
            Debug.Log("[OperatorDesignerStudio] Loaded Kiara template from Kiara.md specs!");
        }

        public void BakeCurrentOperator()
        {
            string unitDir = "Assets/Resources/Units";
            string abilityDir = "Assets/Resources/Abilities";
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(unitDir)) AssetDatabase.CreateFolder("Assets/Resources", "Units");
            if (!AssetDatabase.IsValidFolder(abilityDir)) AssetDatabase.CreateFolder("Assets/Resources", "Abilities");

            List<AbilityDefinition> createdAbilities = new List<AbilityDefinition>();

            // 1. Bake Abilities
            foreach (var s in _selectableSkills)
            {
                string path = $"{abilityDir}/{s.skillId}.asset";
                var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                if (ability == null)
                {
                    ability = ScriptableObject.CreateInstance<AbilityDefinition>();
                    AssetDatabase.CreateAsset(ability, path);
                }

                ability.AbilityId = s.skillId;
                ability.SkillName = s.skillName;
                ability.Description = s.description;
                ability.DefaultChargeType = s.chargeType;

                if (ability.Levels == null) ability.Levels = new List<AbilityLevelData>();
                if (ability.Levels.Count == 0) ability.Levels.Add(new AbilityLevelData());

                var lvl = ability.Levels[0];
                lvl.SPCost = s.spCost;
                lvl.InitSP = s.initSP;
                lvl.ChargeType = s.chargeType;
                lvl.Cooldown = s.cooldown;

                EditorUtility.SetDirty(ability);
                createdAbilities.Add(ability);
            }

            // 2. Bake UnitData
            string unitPath = $"{unitDir}/{_unitId}.asset";
            var unit = AssetDatabase.LoadAssetAtPath<UnitData>(unitPath);
            if (unit == null)
            {
                unit = ScriptableObject.CreateInstance<UnitData>();
                AssetDatabase.CreateAsset(unit, unitPath);
            }

            unit.UnitName = _unitName;
            unit.PieceType = _pieceType;
            unit.Subclass = _subclass;
            unit.ResonanceRole = _resonanceRole;
            unit.Weight = _weight;
            unit.Icon = _icon;
            unit.Prefab = _prefab;
            unit.MaxHealth = _maxHealth;
            unit.AttackDamage = _attackDamage;
            unit.Defense = _defense;
            unit.Resistance = _resistance;
            unit.SpeedLevel = _speedLevel;
            unit.MoveCostAP = _moveCostAP;
            unit.AttackCostAP = _attackCostAP;
            unit.AttackCount = _attackCount;
            unit.AttackDamageRatio = _attackDamageRatio;
            unit.Style = _combatStyle;
            unit.ChargeType = _defaultChargeType;
            unit.MaxSP = _maxSP;
            unit.InitSP = _initSP;
            unit.MoveRange = _moveRange;
            unit.AttackRange = _attackRange;
            unit.SyncDefaultChessMetadata();
            unit.UpdateMoveSpeed();

            unit.SelectableActiveSkills = new List<AbilityDefinition>(createdAbilities);
            if (createdAbilities.Count > 0)
            {
                unit.SelectedSkillIndex = createdAbilities.Count - 1; // Default to 3rd active skill (hyper active) if available, or 0
                if (unit.Abilities == null) unit.Abilities = new List<AbilityDefinition>();
                unit.Abilities.Clear();
                unit.Abilities.AddRange(createdAbilities);
            }

            EditorUtility.SetDirty(unit);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OperatorDesignerStudio] Successfully baked Operator '{unit.UnitName}' with {createdAbilities.Count} selectable active skills!");
        }

        private void LoadElenaTemplate()
        {
            _unitId = "Elena";
            _unitName = "엘레나";
            _pieceType = ChessPieceType.Bishop;
            _subclass = UnitSubclassType.Sniper;
            _resonanceRole = ResonanceRoleType.Offensive;
            _weight = 2;
            _starRating = 5;
            _visualDescription = "은발, 미래주의 사이버 기어와 방음 헤드폰, 원거리 암살을 위한 대형 실탄 대물 레일건 장착";

            _maxHealth = 520f;
            _attackDamage = 240f;
            _defense = 15f;
            _resistance = 0.05f;
            _speedLevel = 3;
            _moveCostAP = 24f;
            _attackCostAP = 24f;
            _attackCount = 1;
            _attackDamageRatio = 1.3f;
            _combatStyle = CombatStyle.Range;
            _defaultChargeType = SPChargeType.Auto;
            _maxSP = 100f;
            _initSP = 60f;
            _moveRange = 7;
            _attackRange = 7;

            _selectableSkills.Clear();
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Elena_Active1", skillName = "2칸 관통탄 (1스킬)", description = "다음 사격이 2칸 관통탄으로 발사되며, 공격력이 70% 증가한 물리 저격 피해를 입힌다.", spCost = 6, initSP = 0, chargeType = SPChargeType.OnAttack, cooldown = 1 });
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Elena_Active2", skillName = "무제한 시야 (2스킬)", description = "15초 동안 사거리가 대각선 끝까지 확장되며, 사격 및 행동 쿨타임이 25% 단축된다.", spCost = 70, initSP = 40, chargeType = SPChargeType.Auto, cooldown = 4 });
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Elena_Active3", skillName = "발키리의 심판 - 아스트럴 스나이프", description = "공작된 스나이핑 시야로 상공 컷인 후, 적 킹이나 최대 체력 적에게 공격력 350% 장거리 암살 피해 및 방어력 40% 무력화.", spCost = 100, initSP = 60, chargeType = SPChargeType.Auto, cooldown = 6 });

            _passiveDescription1 = "1. 3칸 이상 떨어진 적 타격 시 치명타율 25% 상승, 대상 이동속도 3초간 20% 저하.";
            _passiveDescription2 = "2. 제자리에서 6초 이상 대기 시 '위장(은신)' 상태가 되며 다음 타격 피해량 20% 상승.";
            Repaint();
            Debug.Log("[OperatorDesignerStudio] Loaded Elena template (Bishop / Sniper)!");
        }

        private void LoadVestaTemplate()
        {
            _unitId = "Vesta";
            _unitName = "베스타";
            _pieceType = ChessPieceType.Rook;
            _subclass = UnitSubclassType.SiegeWeapon;
            _resonanceRole = ResonanceRoleType.Defensive;
            _weight = 3;
            _starRating = 5;
            _visualDescription = "붉은 머리카락, 육중한 메카닉 엑소 슈트, 등 뒤에 하드폰트 마운트된 쌍불신 대장갑 공성 캐논";

            _maxHealth = 1100f;
            _attackDamage = 210f;
            _defense = 55f;
            _resistance = 0.1f;
            _speedLevel = 2;
            _moveCostAP = 36f;
            _attackCostAP = 36f;
            _attackCount = 1;
            _attackDamageRatio = 1.0f;
            _combatStyle = CombatStyle.Range;
            _defaultChargeType = SPChargeType.Auto;
            _maxSP = 110f;
            _initSP = 65f;
            _moveRange = 7;
            _attackRange = 7;

            _selectableSkills.Clear();
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Vesta_Active1", skillName = "중장갑 분사 (1스킬)", description = "다음 공성 포격이 중장갑을 훼손하여 3칸 밀치기 및 방어력 30% 파기.", spCost = 8, initSP = 0, chargeType = SPChargeType.OnAttack, cooldown = 1 });
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Vesta_Active2", skillName = "앵커 포격 태세 (2스킬)", description = "18초간 이동 불능 상태가 되는 대신 매 포격이 십자(+) 광역 폭발 타격으로 변경되며 공격력 50% 증가.", spCost = 80, initSP = 50, chargeType = SPChargeType.Auto, cooldown = 5 });
            _selectableSkills.Add(new ActiveSkillEditorData { skillId = "Vesta_Active3", skillName = "지평선 붕괴 - 궤도 차익성 포격", description = "쌍불신 공성 캐논을 전방 융합하여 사선 컷인 후, 직선 기물 경로 상의 전원에게 공격력 280% + 대상 방어력 200% 대장갑 파성 피해!", spCost = 110, initSP = 65, chargeType = SPChargeType.Auto, cooldown = 7 });

            _passiveDescription1 = "1. 적 '룩' 기물 또는 보스 공격 시 최종 피해량 35% 증폭 및 SP 상승 2초간 억제.";
            _passiveDescription2 = "2. 배치 후 30초 동안 받는 피해 20% 감쇄, 체급 3(Heavy) 밀치기 상시 면역.";
            Repaint();
            Debug.Log("[OperatorDesignerStudio] Loaded Vesta template (Rook / Siege Weapon)!");
        }

        [MenuItem("CheckmateRPG/Bake Sample Operators/Bake Kiara (⚔️ 검객 Knight)", false, 1)]
        [MenuItem("Tools/Bake Sample Operator Kiara", false, 2)]
        public static void BakeKiaraFromMenu()
        {
            var temp = CreateInstance<OperatorDesignerStudio>();
            temp.LoadKiaraTemplate();
            temp.BakeCurrentOperator();
            DestroyImmediate(temp);
        }

        [MenuItem("CheckmateRPG/Bake Sample Operators/Bake Elena (🏹 저격수 Bishop)", false, 2)]
        public static void BakeElenaFromMenu()
        {
            var temp = CreateInstance<OperatorDesignerStudio>();
            temp.LoadElenaTemplate();
            temp.BakeCurrentOperator();
            DestroyImmediate(temp);
        }

        [MenuItem("CheckmateRPG/Bake Sample Operators/Bake Vesta (💥 공성병기 Rook)", false, 3)]
        public static void BakeVestaFromMenu()
        {
            var temp = CreateInstance<OperatorDesignerStudio>();
            temp.LoadVestaTemplate();
            temp.BakeCurrentOperator();
            DestroyImmediate(temp);
        }

        [MenuItem("CheckmateRPG/Bake Sample Operators/🌟 Bake ALL Sample Operators (Kiara, Elena, Vesta)", false, 0)]
        public static void BakeAllFromMenu()
        {
            var temp = CreateInstance<OperatorDesignerStudio>();
            temp.LoadKiaraTemplate();
            temp.BakeCurrentOperator();

            temp.LoadElenaTemplate();
            temp.BakeCurrentOperator();

            temp.LoadVestaTemplate();
            temp.BakeCurrentOperator();

            DestroyImmediate(temp);
            Debug.Log("[OperatorDesignerStudio] Successfully baked ALL 3 Sample Operators (Kiara, Elena, Vesta)!");
        }
    }
}
#endif
