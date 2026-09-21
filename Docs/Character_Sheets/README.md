# 📁 Checkmate RPG — Character Sheets & Balance Documents Index

이 폴더는 **Checkmate RPG**의 캐릭터 밸런스, 성급 위계, 오퍼레이터 기획 명세서를 영구 보존하는 공식 문서 저장소입니다.

---

## 📌 문서 인덱스 (Documentation Index)

### 1. 👑 마스터 통합 명세서 (Single Source of Truth)
- **[MASTER_CHARACTER_BALANCE_SPECIFICATION.md](MASTER_CHARACTER_BALANCE_SPECIFICATION.md)**
  - 전체 시스템의 단일 기준 마스터 문서.
  - 코어 수치 공식(물리 감산식, 마법 비율식, Splat, 쇄빙), 명일방주 기반 성급(5★, 4★, 3★) 위계, 5성 에이스 4인(엘레나/키아라/베스타/아틀라스), 스탠다드 3성 13인, 플레이어 킹 7개 슈트, 서브클래스 64종, PVE 인카운터 및 QA 체크리스트 총망라.

---

### 2. 📂 세부 기획 문서 (Specialized Sub-Documents)

| 문서 파일명 | 주요 내용 | 용도 및 참고 시점 |
|:---|:---|:---|
| **[Arknights_Style_Balance_Architecture.md](Arknights_Style_Balance_Architecture.md)** | • 명일방주(Arknights) 시스템 1:1 매핑 명세<br>• 성급별 스킬 슬롯 분화 (1스 순환 / 2스 국밥 / 3스 결전기)<br>• 재능(패시브) 위계 및 SP 3대 충전 방식<br>• 육성 재화 경제학 | 명일방주식 캐릭터 스킬 및 시스템 벤치마킹 시 참조 |
| **[Rarity_Balance_Framework.md](Rarity_Balance_Framework.md)** | • 성급(5★ 4★ 3★) 밸런스 프레임워크 개정본<br>• **기초 스탯 격차 10~15% 압축 원칙**<br>• **성급과 난이도의 디커플링 (국밥형 5성 vs 테크니컬 5성 공존)**<br>• 나이트 기물 3종 실측 비교 | 신규 캐릭터 기획 및 성급별 수치 책정 시 참조 |
| **[Standard_3Star_Roster.md](Standard_3Star_Roster.md)** | • 스탠다드 3성 기본 오퍼레이터 13인 명세서<br>• 폰 4인 (레오, 바론, 클로에, 로빈)<br>• 나이트 3인 (카일, 롤랑, 루카스)<br>• 비숍 3인 (셀레나, 노아, 에녹)<br>• 룩 3인 (브록, 이그니스, 테레사) | 초반 보급형 및 3성 가챠 풀 캐릭터 구현 시 참조 |
| **[Character_Balance_Design_Sheet.md](Character_Balance_Design_Sheet.md)** | • 초기 캐릭터 밸런스 디자인 시트<br>• 레벨 1~90 스케일링 수식 모델<br>• 64개 서브클래스 전체 수치 테이블 | 레벨업 성장 곡선 및 전체 클래스 분류 조회 시 참조 |

---

## 🛠️ 개발 원칙 준수 안내
- 본 문서군의 수치와 명세는 `Assets/Scripts/Data/UnitData.cs`, `GddUnitSpecs.cs`, `UnitSubclassType.cs` 및 `Master GDD Part 2, 3, 4`와 100% 동기화되어 있습니다.
- 새로운 캐릭터 추가 시 마스터 문서의 [9. 기획 및 QA 밸런스 검증 체크리스트]를 반드시 준수해야 합니다.
