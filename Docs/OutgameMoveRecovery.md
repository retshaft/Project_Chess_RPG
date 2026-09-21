# 파일 이동 후 아웃게임 진단 — 2026-09-17

상태: PC 보안 정책으로 실제 UI 복구 검증 차단. Play Mode 종료.

## 확인된 원인
- 실행 프로젝트: C:/Users/VR13/Downloads/20260917/Graduation_Project_v01, Unity 6000.3.23f1.
- 최초 Play에서 Missing Script 37건. TitleUI/OutgameUIManager/MainLobbyUI/SaveManager/UnitData/PlayerDeckData의 GUID는 소스와 일치하지만 MonoScript.GetClass()는 null.
- CheckmateRPG 및 NCalc, Unsafe 어셈블리는 현재 로드됨. 이전 로그에는 Unsafe 의존성 누락도 있었음.
- 재임포트 후에도 CS8034, HRESULT 0x800711C7: Windows가 Unity.SourceGenerators.dll, Unity.Properties.SourceGenerator.dll, Unity.UIToolkit.SourceGenerator.dll 로드를 차단.
- Windows CodeIntegrity 이벤트 3077/3033에서도 dotnet.exe의 위 모듈 로드가 signing level/policy에 의해 차단됨을 확인. 정책 ID 0283ac0f-fff1-49ae-ada1-8a933130cad6.
- 따라서 파일 이동 자체나 버튼 참조 누락으로 단정할 수 없으며, 현재 PC의 컴파일 환경 문제가 직접 확인됨.

## 수정
- AutoGenerateTestAssets: 타입 로드 실패를 파일 부재로 취급하지 않도록 실제 파일 존재 여부로 생성 조건 변경. 기존 Play 진입 보호 유지.
- 자동 생성 로그와 m_Script=0/빈 Roster로 확인된 ConscriptUnitData 및 DefaultTestDeck 손상본을 Logs/OutgameRecoveryBackup에 보관하고 HEAD의 정상 데이터를 복원. 다른 기존 변경 보존.
- AssetGenerationRecoveryTests: 자동 생성 실행 전후 두 파일의 바이트가 동일한지 회귀 검증 추가.
- 임시 에디터 진단/테스트 실행 도구는 Assets에서 Tools/OutgameRecovery로 이동해 자동 실행 중지.

## 검증
- Unity 컴파일 오류 없음. 단, Source Generator 차단 경고가 있어 정상 컴파일 환경으로 판정하지 않음.
- EditMode 22/22 통과: AssetGenerationRecoveryTests, OutgameRuleRegressionTests, OutgameProgressionRegressionTests.
- 결과: Logs/outgame-recovery-tests.xml. 스크립트 연결 진단: Logs/outgame-diagnostic.txt.
- 타이틀 → 로비 및 메뉴 왕복 Play 검증은 미완료. 테스트 통과가 씬 동작 복구를 의미하지 않음.

## 필요한 후속 조치
PC 관리자에게 위 Unity 설치 파일이 보안 정책에서 차단된 원인을 확인하고 정상 실행 가능한 공식 Unity 설치 환경을 마련하도록 요청한다. 보안 기능 전체 해제나 우회는 수행하지 않았다.
환경 복구 후 스크립트 재임포트 → OutgameScene 재열기 → Play → START → 로비/메뉴 왕복 및 콘솔 검증 → Play 종료가 필요하다.
