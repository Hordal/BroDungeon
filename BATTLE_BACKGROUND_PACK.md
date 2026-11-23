# BRODUN 테마 선택과 몬스터 배경 연결

회랑·불·서리·공허의 일반전·엘리트, 보스전에 사용하는 배경 8장이다. 몬스터와 배경은 같은 테마 ID를 기준으로 고른다.

## 실행 방법

- `Assets/BRODUN/Scenes/BrodunBoot.unity`에서 시작하고, 캐릭터 선택 후 1구역 테마를 고른다. 1·2구역 보스를 처치하면 방문하지 않은 테마 중 다음 구역을 직접 고른다.
- 8장 미리보기: **BRODUN > Art > Battle Background Pack**.
- 위치: `Assets/BRODUN/Resources/BRODUN/Backgrounds/`.
- 일반 게임은 현재 구역의 테마 ID로 배경과 조우를 선택한다. 일반전과 엘리트는 normal, 보스는 boss 배경을 사용한다.
- 미지원 구역이나 누락된 리소스에는 기존 `BattlePresentationAssets.background`를 사용한다.

| 테마 ID | 테마 | 일반전 및 엘리트 | 보스전 |
|---|---|---|---|
| ruins | 잊혀진 회랑 | 01_ruins_normal.png | 01_ruins_boss.png |
| frost | 서리의 수맥 | 02_waterway_normal.png | 02_waterway_boss.png |
| fire | 소멸의 화로 | 03_foundry_normal.png | 03_foundry_boss.png |
| void | 공허의 성좌 | 04_storm_normal.png | 04_storm_boss.png |

서리와 공허 배경은 기존 Unity GUID를 유지하도록 파일 접두사를 재사용한다. 불·서리·공허는 YeonJun의 테마별 일반 15종·엘리트 3종·보스 1종을 사용하고, 회랑은 가고일·슬라임·질풍 파수꾼·몰락한 거신을 사용한다.

| ThemeId | 이름 | 일반 | 엘리트 | 보스 | 배경 파일 접두사 |
|---|---|---:|---:|---|---|
| ruins | 잊혀진 회랑 | 가고일·이끼 슬라임 2종 | 질풍 파수꾼 1종 | 몰락한 거신 | 01_ruins |
| fire | 소멸의 화로 | 불 계열 15종 | 불 계열 3종 | 왕관을 삼킨 용 아그라스 | 03_foundry |
| frost | 서리의 수맥 | 서리 계열 15종 | 서리 계열 3종 | 영면의 여왕 이스벨 | 02_waterway |
| void | 공허의 성좌 | 공허 계열 15종 | 공허 계열 3종 | 별을 삼킨 교황 녹틸 | 04_storm |

PNG 파일 이름은 기존 Unity GUID와의 호환을 위해 유지했다. 02는 얼어붙은 수로와 장례 성소로, 04는 공허와 일식 성좌로 다시 그렸다. 더 이상 폭풍 가안을 네 번째 선택 테마로 사용하지 않는다.

상세 편성은 `BackgroundPackSource/manifest.json`과 `ThemeEnemyCatalog.cs`에 있다. 테마별 ID 목록을 명시하므로 불을 골랐을 때 서리·공허 몬스터가 섞이지 않는다.

## 데이터와 표시 구조

- `RunState.ThemeId`: 원정의 테마. `CurrentArea`는 진행 단계이며 테마와 분리한다.
- `DungeonThemes`: 테마 ID·표시명·배경 키·등장 몬스터 ID를 함께 정의.
- `EncounterPool.ForTheme`: 테마와 등급을 함께 필터링.
- `GameCatalogData.ToDefinitions()`: 기존 12종 정의를 유지해 웹 규칙·기존 데이터 검증용으로 사용.
- 게임 부팅 시 `DungeonThemes.WithThemeEnemies`가 기존 정의에 57종을 추가한다. 총 정의는 69종이며 선택 테마에 편성된 몬스터는 61종이다.
- 테마 몬스터는 YeonJun 모션 팩의 프리팹을 우선 사용한다. 전용 프리팹이 없으면 `Resources/BRODUN/ThemeMonsters/`의 단일 Sprite를 사용한다.
- 씬·프리팹을 재생성하지 않았고 Unity 에디터·패키지 버전을 변경하지 않았다.

## 현재 전투 규칙의 범위

새 몬스터의 이름과 외형·일반/엘리트/보스 분류를 연결했다. 기본 수치는 해당 등급을 기준으로 사용하며 전투 시 진행 구역에 따른 보정을 적용한다.

원본 monsters.json의 역할 설명에 있는 보스별 기믹, 장례 종, 비늘 개방, 성좌 매듭, 다중 페이즈는 아직 전투 로직으로 구현하지 않았다. 원본 설명은 출처 문서로 보존한다. 신규 57종은 기존 공격·방어·강화 패턴으로 전투하며, 특수 능력을 이미 구현한 것처럼 표시하지 않는다.

## 미리보기

**BRODUN > Art > Battle Background Pack**에서 게임과 동일한 네 테마 ID로 일반/엘리트·보스 배경을 바꿔 본다. 배경은 1672 × 941 PNG이며 몬스터와 UI는 별도로 배치한다. built-in image_gen 제작 프롬프트는 BackgroundPackSource에 보관한다.

## 검증

- .NET에서 실제 Rules·Flow 소스와 기존 순수 규칙 검사 및 ThemeMatchingTests를 실행: **81개 통과, 실패 0**.
- 4테마 × 128 Seed의 모든 전투·보스 노드에서 테마·등급 일치와 결정적 편성을 검사.
- 일반전 보상 → 지도 복귀 → 같은 테마 보스 → 결과 화면의 테마 유지 검사. 이 상태 전환 검사는 적 HP를 1로 설정하는 테스트 fixture이며 밸런스나 실제 완주 검증을 뜻하지 않는다.
- 신규 57종 이미지의 원본 해시·메타데이터·GUID 중복과 배경 8장의 크기·리소스 경로 정적 검사.
- 설치된 Unity 6000.6.2f1 참조 어셈블리로 C# 컴파일. 프로젝트 기준 버전은 6000.0.47f1 그대로다.
- Unity 라이선스가 활성화되지 않아 에디터 Sprite 임포트와 PlayMode 실행·화면 배치 검증은 아직 못 했다. ThemeAssetMatchingTests와 ScreenFlowTests의 테마 버튼 검사를 프로젝트 기준 에디터에서 실행해야 한다.
