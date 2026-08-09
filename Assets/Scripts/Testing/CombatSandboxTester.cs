using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.UI;
using CheckmateRPG.Core;
using System.Collections;

namespace CheckmateRPG.Testing
{
    public class CombatSandboxTester : MonoBehaviour
    {
        [Header("References")]
        public Transform playerUnit; // 현재 선택된 오퍼레이터
        public Transform[] squadUnits; // 0: 키아라, 1: 엘레나, 2: 베스타
        public Transform bossUnit;
        public SkillCutInUI skillCutIn;
        public CheckmateCinematicUI checkmateCinematic;
        public KingSynchroUI kingSynchro;
        
        private HealthComponent _bossHealth;
        private StatusEffectComponent _bossStatus;
        private SPComponent _playerSP;
        private int _bleedStacks = 0;
        private int _selectedIndex = 0;

        private readonly string[] _operatorTitles = {
            "⚔️ 키아라 [나이트 / 검객 Swordmaster]",
            "🏹 엘레나 [비숍 / 저격수 Sniper]",
            "💥 베스타 [룩 / 공성병기 Siege Weapon]"
        };

        private void Start()
        {
            if (bossUnit != null)
            {
                _bossHealth = bossUnit.GetComponent<HealthComponent>();
                _bossStatus = bossUnit.GetComponent<StatusEffectComponent>();
            }
            SelectUnit(0);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) SwitchNextUnit();
            if (Input.GetKeyDown(KeyCode.A)) PerformActionA();
            if (Input.GetKeyDown(KeyCode.S)) PerformActionS();
            if (Input.GetKeyDown(KeyCode.D)) PerformActionD();
            if (Input.GetKeyDown(KeyCode.F)) SimulateCheckmate();
            if (Input.GetKeyDown(KeyCode.R)) ResetSandbox();
        }

        public void SelectUnit(int index)
        {
            if (squadUnits != null && squadUnits.Length > 0)
            {
                _selectedIndex = (index + squadUnits.Length) % squadUnits.Length;
                playerUnit = squadUnits[_selectedIndex];
                if (playerUnit != null)
                {
                    _playerSP = playerUnit.GetComponent<SPComponent>();
                }
                Debug.Log($"[CombatSandboxTester] 스쿼드 제어 오퍼레이터 변경: {_operatorTitles[_selectedIndex]}");
            }
        }

        public void SwitchNextUnit()
        {
            SelectUnit(_selectedIndex + 1);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 360, 440), "🎮 다중 오퍼레이터 전술 샌드박스", GUI.skin.window);
            GUILayout.Space(22);
            
            GUI.backgroundColor = new Color(0.25f, 0.8f, 1f);
            string title = _selectedIndex < _operatorTitles.Length ? _operatorTitles[_selectedIndex] : "선택 안 됨";
            if (GUILayout.Button($"🔄 [TAB] 제어 유닛 스위치:\n{title}", GUILayout.Height(42)))
            {
                SwitchNextUnit();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(8);

            if (_selectedIndex == 0) // 키아라 (검객)
            {
                if (GUILayout.Button("⚔️ [A] 검객 2연타 평타 공격 (총 130% 피해)", GUILayout.Height(36))) PerformActionA();
                if (GUILayout.Button("🩸 [S] 출혈 상태이상 부여 (3타 적중 패시브)", GUILayout.Height(36))) PerformActionS();
                if (GUILayout.Button("⚡ [D] 3스킬 '카오틱 오버드라이브' EX 컷인", GUILayout.Height(36))) PerformActionD();
            }
            else if (_selectedIndex == 1) // 엘레나 (저격수)
            {
                if (GUILayout.Button("🎯 [A] 원거리 물리 저격 암살 타격 (+30% 피해)", GUILayout.Height(36))) PerformActionA();
                if (GUILayout.Button("👻 [S] 2칸 사각지대 확인 & '위장(은신)' 발동", GUILayout.Height(36))) PerformActionS();
                if (GUILayout.Button("💫 [D] 3스킬 '아스트럴 스나이프' EX 컷인", GUILayout.Height(36))) PerformActionD();
            }
            else if (_selectedIndex == 2) // 베스타 (공성병기)
            {
                if (GUILayout.Button("💥 [A] 2칸 이상 대장갑 파쇄 공성 타격 (고정피해)", GUILayout.Height(36))) PerformActionA();
                if (GUILayout.Button("🛡️ [S] 앵커 고정 십자 포격 태세 & 밀치기 면역", GUILayout.Height(36))) PerformActionS();
                if (GUILayout.Button("🔥 [D] 3스킬 '지평선 붕괴 - 궤도 차익성' 컷인", GUILayout.Height(36))) PerformActionD();
            }

            GUILayout.Space(5);
            if (GUILayout.Button("👑 [F] 적 수호장 킹 처치 - CHECKMATE 종결", GUILayout.Height(36)))
            {
                SimulateCheckmate();
            }
            GUILayout.Space(8);
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("🔄 [R] 유닛 상태 및 보스 HP 일괄 리셋", GUILayout.Height(30)))
            {
                ResetSandbox();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);
            string info = _selectedIndex == 0 ? $"보스 출혈 중첩: {_bleedStacks} 스택" : (_selectedIndex == 1 ? "엘레나 사거리: 대각선 7칸 (2칸 이내 사각지대)" : "베스타 상태: 체급 Heavy (밀치기 상시 면역)");
            GUILayout.Label($"📊 기물 전술 상태: {info}", GUI.skin.box);
            GUILayout.EndArea();
        }

        private void PerformActionA()
        {
            if (_selectedIndex == 0) SimulateKiaraAttack();
            else if (_selectedIndex == 1) SimulateElenaSnipe();
            else if (_selectedIndex == 2) SimulateVestaSiege();
        }

        private void PerformActionS()
        {
            if (_selectedIndex == 0) SimulateBleedTrigger();
            else if (_selectedIndex == 1) SimulateElenaPassive();
            else if (_selectedIndex == 2) SimulateVestaPassive();
        }

        private void PerformActionD()
        {
            if (_selectedIndex == 0) SimulateKiaraEx();
            else if (_selectedIndex == 1) SimulateElenaEx();
            else if (_selectedIndex == 2) SimulateVestaEx();
        }

        // ==========================================
        // 키아라 (검객) 로직
        // ==========================================
        public void SimulateKiaraAttack()
        {
            if (bossUnit == null) return;
            float baseDamage = 155f * 0.65f;
            StartCoroutine(MultiHitRoutine(baseDamage, 2));
            AddPlayerSP(25f);
        }

        private IEnumerator MultiHitRoutine(float hitDamage, int hits)
        {
            for (int i = 0; i < hits; i++)
            {
                if (_bossHealth != null) _bossHealth.TakeDamage(hitDamage);
                bool isCrit = i == hits - 1;
                if (FloatingTextManager.Instance != null)
                {
                    FloatingTextManager.Instance.SpawnDamage(bossUnit.position + Vector3.up * 1.5f, hitDamage * (isCrit ? 1.5f : 1.0f), isCrit);
                }
                yield return new WaitForSeconds(0.15f);
            }
        }

        public void SimulateBleedTrigger()
        {
            if (bossUnit == null) return;
            _bleedStacks++;
            if (_bossStatus != null) _bossStatus.ApplyStatusEffect(StatusEffectType.Bleed, 12f, 1);
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.SpawnStatusText(bossUnit.position + Vector3.up * 1.8f, $"출혈 x{_bleedStacks}", new Color(1f, 0.2f, 0.3f));
                FloatingTextManager.Instance.SpawnDamage(bossUnit.position + Vector3.up * 1.2f, 45f * _bleedStacks, false, true);
            }
        }

        public void SimulateKiaraEx()
        {
            SetPlayerSPFull(120f);
            ShowExCutIn("Units/Kiara", "키아라", "황우의 극검 - 카오틱 오버드라이브");
            if (FloatingTextManager.Instance != null && playerUnit != null)
            {
                FloatingTextManager.Instance.SpawnStatusText(playerUnit.position + Vector3.up * 2f, "OVERDRIVE ON", new Color(0f, 1f, 1f));
            }
        }

        // ==========================================
        // 엘레나 (비숍 - 저격수) 로직
        // ==========================================
        public void SimulateElenaSnipe()
        {
            if (bossUnit == null) return;
            float snipeDmg = 240f * 1.3f; // 원거리 물리 암살 30% 증폭
            if (_bossHealth != null) _bossHealth.TakeDamage(snipeDmg);
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.SpawnSnipeDamage(bossUnit.position + Vector3.up * 1.6f, snipeDmg, false);
            }
            AddPlayerSP(30f);
            Debug.Log($"[Elena] 3칸 이상 대각선 원거리 타격 적중! {snipeDmg} 물리 암살 피해!");
        }

        public void SimulateElenaPassive()
        {
            if (playerUnit == null) return;
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.SpawnStatusText(playerUnit.position + Vector3.up * 1.8f, "위장 (은신)", new Color(0.6f, 0.9f, 1f));
            }
            Debug.Log("[Elena] 6초 저격 대기 완료. 위장(은신) 상태 진입 및 다음 타격 피해량 +20%!");
        }

        public void SimulateElenaEx()
        {
            SetPlayerSPFull(100f);
            ShowExCutIn("Units/Elena", "엘레나", "발키리의 심판 - 아스트럴 스나이프");
            if (bossUnit != null && FloatingTextManager.Instance != null)
            {
                float astralDmg = 240f * 3.5f;
                _bossHealth?.TakeDamage(astralDmg);
                FloatingTextManager.Instance.SpawnSnipeDamage(bossUnit.position + Vector3.up * 1.8f, astralDmg, true);
                FloatingTextManager.Instance.SpawnStatusText(bossUnit.position + Vector3.up * 1.3f, "방어력 -40% 붕괴", new Color(0.2f, 0.8f, 0.9f));
            }
        }

        // ==========================================
        // 베스타 (룩 - 공성병기) 로직
        // ==========================================
        public void SimulateVestaSiege()
        {
            if (bossUnit == null) return;
            float siegeDmg = 210f * 1.35f; // 대장갑 35% 증폭
            if (_bossHealth != null) _bossHealth.TakeDamage(siegeDmg);
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.SpawnSiegeDamage(bossUnit.position + Vector3.up * 1.6f, siegeDmg, false);
                FloatingTextManager.Instance.SpawnStatusText(bossUnit.position + Vector3.up * 2.1f, "40% 고정피해 변환", new Color(1f, 0.5f, 0.1f));
            }
            AddPlayerSP(35f);
            Debug.Log($"[Vesta] 2칸 이상 공성 포격 적중! 적 보스 고방어력 대상 40% 고정 피해 관통!");
        }

        public void SimulateVestaPassive()
        {
            if (playerUnit == null) return;
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.SpawnStatusText(playerUnit.position + Vector3.up * 1.8f, "앵커 고정 십자포격", new Color(1f, 0.6f, 0.2f));
            }
            Debug.Log("[Vesta] 앵커 다운. 18초간 이동 불능 및 매 타격 십자(+) 광역 폭발 피해 전환!");
        }

        public void SimulateVestaEx()
        {
            SetPlayerSPFull(110f);
            ShowExCutIn("Units/Vesta", "베스타", "지평선 붕괴 - 궤도 차익성 포격");
            if (bossUnit != null && FloatingTextManager.Instance != null)
            {
                float orbitDmg = 210f * 2.8f + (40f * 2.0f);
                _bossHealth?.TakeDamage(orbitDmg);
                FloatingTextManager.Instance.SpawnSiegeDamage(bossUnit.position + Vector3.up * 1.8f, orbitDmg, true);
            }
        }

        // ==========================================
        // 공통 헬퍼 메서드
        // ==========================================
        private void AddPlayerSP(float amount)
        {
            if (_playerSP != null) _playerSP.SetCurrentSP(_playerSP.CurrentSP + amount);
        }

        private void SetPlayerSPFull(float max)
        {
            if (_playerSP != null) _playerSP.SetCurrentSP(max);
        }

        private void ShowExCutIn(string resourcePath, string unitName, string skillName)
        {
            if (skillCutIn != null)
            {
                Sprite portrait = null;
                var data = Resources.Load<Data.UnitData>(resourcePath);
                if (data != null) portrait = data.Icon;
                skillCutIn.ShowCutIn(unitName, skillName, portrait);
            }
        }

        public void SimulateCheckmate()
        {
            if (_bossHealth != null) _bossHealth.ApplyTrueDamage(_bossHealth.CurrentHealth);
            if (checkmateCinematic != null)
            {
                checkmateCinematic.PlayCheckmate(() => {
                    Debug.Log("Checkmate sequence concluded! Entering victory screen.");
                });
            }
        }

        public void ResetSandbox()
        {
            _bleedStacks = 0;
            if (_bossHealth != null) _bossHealth.Heal(_bossHealth.MaxHealth);
            if (_bossStatus != null) _bossStatus.RemoveStatusEffect(StatusEffectType.Bleed);
            
            foreach (var unit in squadUnits)
            {
                if (unit != null)
                {
                    var sp = unit.GetComponent<SPComponent>();
                    if (sp != null) sp.ResetSP(60f);
                }
            }
            Debug.Log("[CombatSandboxTester] 샌드박스 전술 유닛 상태 및 보스 HP 일괄 리셋 완료.");
        }
    }
}
