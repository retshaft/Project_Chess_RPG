# 확정 전투 규칙 반영
- 상태: Review
- 담당: Codex 메인 / 원본 codex/integration / 기준 ef13042 및 기존 카메라 UI 변경 보존
- 허용: Assets/Scripts/Data/UnitData.cs, Progression/DeckManager.cs, UI/DeckBuilderUI.cs, UI/RosterSlotUI.cs, UI/BattleHudView.cs, Core/BattleManager.cs, Core/BattleCameraDirector.cs, Core/AbilityDefinition.cs, Core/ActionRuntimeController.cs, Core/Runtime/Processors/DamageMutationProcessor.cs, Components/SPComponent.cs, Components/SubclassComponent.cs, Testing/EditModeTests/ConfirmedBattleRulesTests.cs 및 meta; Docs/Parallel, Docs/PLAYABILITY_QUESTIONS.md.
- 범위: 기본 편성 기준 상한 20과 기물별 기본 비용, 킹 e1 자동 배치, 배치 중 철회, 의무 징집병 충원, 수동/자동 스킬 구분과 SP 연결, 5성만 개별 스킬 클로즈업.
- 보류: OP 전용 적의 구체 배치/난이도/자원 작전 설계. 사용자 결정에 따라 추후 구체화.
- 추가 허용: Units/UnitBrain.cs의 기존 자동 전투 대상 읽기 속성. 새 전투 판단 로직 대신 기존 대상을 자동 스킬에 전달한다.
- 추가 허용: Core/AbilityExecutionPipeline.cs의 기본 공격형 능력 SP 회복, Data/PlayerDeckData.cs의 편성 비용 일치, Testing/UI/DraggableDeploymentButton.cs의 철회/배치 안내 문구.
- 금지: 기존 변경 폐기, 씬 재생성, 임의 캐릭터 효과/성장 수치 변경, 저장 되돌리기.
- 검증: 컴파일, 전체 EditMode 회귀, 정식 씬 경로 Play. 종료 및 Unity 실행권 반납.
