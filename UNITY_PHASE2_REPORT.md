# BRODUN PHASE 2 완료 보고

검증일: 2026-10-13. 대상: Hordal/BroDungeon, Paul.
기준 웹 commit: eedba21b4552200874388bdb9f637850b14b3af0.
Unity: 6000.0.47f1 (2ad1ed33fd3b). PHASE 3는 시작하지 않았다.

## 1. 새 폴더 구조

```
Assets/BRODUN/
  Data/
    WebRulesSnapshot.json
    Generated/
      Cards/          (27)
      Characters/     (3)
      Enemies/        (12)
      BattleConfig.asset
      Reward.asset
      GameCatalog.asset
  Scripts/
    Rules/            순수 C# 정의, 상태, 전투/덱/맵/보상 규칙
    Data/             ScriptableObject + JSON import DTO
    Editor/           명시적으로 실행하는 데이터 Asset 생성 도구
  Tests/Editor/       EditMode 테스트 및 실제 웹 실행 기대값
Tools/Brodun/         빌드 시점 웹 데이터/기대값 추출·비교 도구
```

Rules assembly는 noEngineReferences=true로 UnityEngine 참조를 금지했다.
Data → Rules, Editor → Data/Rules, Tests → Editor/Data/Rules의 단방향 참조다.
기존 Assets/Editor와 팀원 Scene/설정 구조에 새 게임 코드를 섞지 않았다.

## 2. 새 C# 파일 전체 목록

24개. 각 경로는 저장소 루트 기준이다.

- Assets/BRODUN/Scripts/Data/BattleConfigData.cs
- Assets/BRODUN/Scripts/Data/CardData.cs
- Assets/BRODUN/Scripts/Data/CharacterData.cs
- Assets/BRODUN/Scripts/Data/EnemyData.cs
- Assets/BRODUN/Scripts/Data/GameCatalogData.cs
- Assets/BRODUN/Scripts/Data/RewardData.cs
- Assets/BRODUN/Scripts/Data/WebSnapshot.cs
- Assets/BRODUN/Scripts/Editor/DataAssetBuilder.cs
- Assets/BRODUN/Scripts/Rules/AssemblyInfo.cs
- Assets/BRODUN/Scripts/Rules/BattleRuntime.cs
- Assets/BRODUN/Scripts/Rules/CardEffectResolver.cs
- Assets/BRODUN/Scripts/Rules/DamageCalculator.cs
- Assets/BRODUN/Scripts/Rules/DeckRuntime.cs
- Assets/BRODUN/Scripts/Rules/Definitions.cs
- Assets/BRODUN/Scripts/Rules/DungeonRuntime.cs
- Assets/BRODUN/Scripts/Rules/EnemyInstance.cs
- Assets/BRODUN/Scripts/Rules/RewardRuntime.cs
- Assets/BRODUN/Scripts/Rules/RunState.cs
- Assets/BRODUN/Scripts/Rules/SeededRandom.cs
- Assets/BRODUN/Tests/Editor/CoreRulesTests.cs
- Assets/BRODUN/Tests/Editor/ProgressionTests.cs
- Assets/BRODUN/Tests/Editor/ProjectEnvironmentTests.cs
- Assets/BRODUN/Tests/Editor/TestSupport.cs
- Assets/BRODUN/Tests/Editor/WebParityTests.cs

Definitions.cs에는 enum, CardEffect/CardDefinition/CharacterDefinition/EnemyDefinition,
IntentData/BattleConfig/RewardDefinition/ElementDefinition/GameDefinitions가 있다.
RunState.cs에는 CardInstance/RunState/RoomResult,
EnemyInstance.cs에는 EnemyInstance/EnemyActionResult/IntentResolver,
DamageCalculator.cs에는 DamageResult/DamageCalculator/StatusState/ElementRules,
RewardRuntime.cs에는 PendingReward/RewardResolver가 있다.
CardEffectResolver는 공통 피해, 조건부 피해, 상태, 자원 핸들러를 분리한다.

## 3. ScriptableObject 타입

CardData, CharacterData, EnemyData, BattleConfigData, RewardData, GameCatalogData 6종.
실제 플레이 중 변하는 HP/덱/방어/힘/독/약화/Intent/보상 완료 상태는 저장하지 않는다.
ToDefinitions()는 독립적인 읽기 전용 원본 정의를 만들고, 새 Run/Battle은 새 런타임 인스턴스를 만든다.
카드/캐릭터 Sprite 필드는 준비만 했고 null이다. 적은 기존 asset_id를 보존했다.
Initialize는 Editor builder에서 원본 데이터를 채우는 용도이며 전투 규칙에서는 호출하지 않는다.

## 4. 실제 생성한 데이터 Asset 수

45개 = 카드 27 + 캐릭터 3 + 적 12 + BattleConfig 1 + Reward 1 + GameCatalog 1.
Unity AssetDatabase 검색과 실제 파일 수를 확인했다.
JSON만 준비하거나 빈 SO 클래스로 끝낸 작업이 아니다.
Editor 메뉴 BRODUN/Data/Rebuild web-rule assets로 명시적으로 재생성한다.
같은 경로의 기존 SO는 GUID를 유지하며 갱신하고, 타입이 다른 기존 파일은 덮어쓰지 않는다.
자동 InitializeOnLoad 생성이나 Scene 변경은 없다.

## 5. 카드 27종 이식 결과

이름/설명/비용/속성/효과 순서/수치/히트 수/조건/소멸을 실제 JS에서 추출했다.
기본 피해와 방어는 ordered Effects로부터 얻는다. 임의 밸런스 변경은 없다.
CardType은 공격/방어/스킬의 표시용 파생 분류다.
웹에 rarity가 없어 Unspecified이며 보상 확률이나 필터에 사용하지 않는다.

| ID | 이름 | 비용 | 속성 | 실제 효과 |
| --- | --- | ---: | --- | --- |
| earth_strike | 지반 강타 | 1 | 땅 | 피해 5. |
| stone_guard | 바위 방벽 | 1 | 바위 | 방어도 5. |
| wind_cut | 돌풍 베기 | 1 | 바람 | 피해 4. |
| fire_bolt | 화염탄 | 1 | 불 | 피해 5. |
| ice_guard | 빙결 장막 | 1 | 얼음 | 방어도 5. |
| lightning_strike | 낙뢰 | 1 | 번개 | 피해 5. |
| water_wave | 정화의 파동 | 1 | 물 | 피해 4. HP 1 회복. |
| earth_blessing | 대지의 가호 | 1 | 땅 | 방어도 5. |
| wind_judgment | 심판의 바람 | 1 | 바람 | 피해 5. |
| earth_crush | 대지 분쇄 | 2 | 땅 | 피해 13. |
| stone_retaliation | 반석의 반격 | 1 | 바위 | 피해 4. 방어도 보유 시 추가 피해 5. |
| wind_read | 바람 읽기 | 0 | 바람 | 1장 드로우. 이번 전투에서 소멸. |
| water_stream | 수맥의 흐름 | 1 | 물 | 피해 3. 1장 드로우. |
| water_rest | 치유의 물결 | 0 | 물 | HP 3 회복. 방어도 2. 이번 전투에서 소멸. |
| ice_shard | 얼음 파편 | 1 | 얼음 | 피해 5. 약화 2턴(공격 -3). |
| ice_link | 빙결 연계 | 1 | 얼음 | 피해 4. 적의 직전 속성이 물 시 추가 피해 7. |
| poison_touch | 독의 손길 | 1 | 독 | 피해 2. 독 3(적 턴 피해 후 1 감소). |
| poison_sting | 맹독 찌르기 | 1 | 독 | 피해 5. 적이 독 상태 시 추가 피해 5. |
| poison_mist | 독안개 | 1 | 독 | 독 3(적 턴 피해 후 1 감소). 약화 1턴(공격 -3). |
| fire_inferno | 열화 폭격 | 2 | 불 | 피해 14. |
| fire_strength | 화로의 힘 | 1 | 불 | 이번 전투 힘 +2. 방어도 3. |
| lightning_chain | 연쇄 번개 | 1 | 번개 | 피해 3 × 2회(타격마다 힘 적용). |
| lightning_charge | 전하 축적 | 0 | 번개 | 행동력 +1(최대 6). 이번 전투에서 소멸. |
| explosion_blast | 폭발 일격 | 2 | 폭발 | 피해 15. |
| explosion_link | 점화 연계 | 1 | 폭발 | 피해 5. 적의 직전 속성이 불 시 추가 피해 8. |
| explosion_barrier | 충격 방벽 | 1 | 폭발 | 방어도 7. 피해 2. |
| stone_fortress | 돌의 요새 | 2 | 바위 | 방어도 12. |

## 6. 캐릭터 3종 이식 결과

모두 최대 HP 60, 시작 HP 60, 시작 행동력 3, Gold 0, 시작 덱 4장이다.
전사: earth_strike / stone_guard / wind_cut / wind_read.
마법사: fire_bolt / ice_guard / lightning_strike / lightning_charge.
사제: water_wave / earth_blessing / wind_judgment / water_rest.
웹에 없는 직업 패시브나 별도 직업별 피해 보정은 추가하지 않았다.

## 7. 속성 9종 이식 결과

Earth 땅 / Stone 바위 / Wind 바람 / Water 물 / Ice 얼음 /
Poison 독 / Fire 불 / Lightning 번개 / Explosion 폭발.
None은 속성이 아직 없는 상태를 뜻하며 10번째 게임 속성이 아니다.
enum뿐 아니라 카탈로그의 ID/한국어 이름 데이터도 보존했다.
속성이 자동으로 상태이상을 부여하거나 상성 배율을 만들지는 않는다.
각 카드의 명시적 효과와 직전 공격/상태 카드의 적 속성 표식에만 관여한다.

## 8. Deck Runtime 구조

DrawPile / Hand / DiscardPile / ExhaustPile을 별도로 유지한다.
시작: 영구 Run 덱을 복사 → 전투 Seed로 셔플 → 4장 드로우.
사용: 손패에서 해당 instance_id를 한 번 제거 → 소멸 효과 유무에 따라 Exhaust/Discard로 이동 →
카드 효과 순서대로 실행. 따라서 드로우 카드가 방금 버린 자신을 다시 뽑는 웹 동작도 유지한다.
덱이 비면 Discard를 같은 RNG 스트림으로 다시 셔플한다.
턴 종료 시 남은 손패를 버리고 다음 턴에 4장 뽑는다.
소멸은 이번 전투에만 적용되며 Run 영구 덱에서는 제거하지 않는다.
중복 카드 종류는 허용하지만 각 카드 인스턴스 ID는 다르다.

## 9. Damage 계산 순서

카드 유효성/비용 검사 → 비용 차감 → 덱에서 사용 처리 → 직전 적 속성 캡처 →
Effects 배열 순서대로 처리 → 공격/독/약화 카드일 때 적 속성 표식 갱신 → 승리 판정.
일반 피해 각 Hit: max(0, 기본 피해 + 플레이어 힘) → 적 방어 소비 → 남은 피해를 현재 HP로 제한.
조건부 추가 피해: 조건 판정 → 힘 없이 해당 amount를 별도 Hit로 적용.
적 공격: max(0, Intent 기본값 + 적 힘 - 약화 중이면 3) → 각 Hit마다 플레이어 방어 소비 → HP 감소.
독: 적 턴 시작에 HP 직접 감소. 방어를 무시한다. 적이 죽으면 적 행동을 실행하지 않는다.

## 10. Weak / Strength

힘은 전투 동안 누적되고 일반 공격의 각 타격에 더해진다.
조건부 추가 피해에는 힘을 더하지 않는다.
약화는 퍼센트가 아니라 적 공격 각 타격 -3, 최소 0이다.
약화 중첩은 더하기가 아닌 남은 턴의 max(old,new)다.
공격뿐 아니라 방어/버프를 포함한 적 행동이 끝날 때 1 감소한다.
현재 웹에는 플레이어에게 약화를 거는 적 행동이 없으므로 추가하지 않았다.

## 11. 상태이상 구조

StatusState는 Strength/Weak/Poison을 관리한다.
독은 누적, 적 턴 시작에 현재 독만큼 HP 피해 후 독 1 감소.
방어는 피해에 소비되며 플레이어는 적 턴 종료 후 0,
적은 자기 행동 직전에 기존 방어를 0으로 만들고 방어 행동이면 새 방어를 얻는다.
플레이어 힘/방어와 적 상태는 BattleRuntime/EnemyInstance의 전투 한정 상태다.
전투가 바뀌면 새 인스턴스이므로 초기화되지만 Run HP/골드/카드는 유지된다.
범용 상태이상 프레임워크나 웹에 없는 지속 효과는 만들지 않았다.

## 12. 속성 연계 구조

ElementRules는 Block > 0, Enemy Poison > 0, 직전 Water, 직전 Fire 조건만 판정한다.
ice_link: 직전 Water이면 +7.
explosion_link: 직전 Fire이면 +8.
stone_retaliation: 플레이어 방어가 있으면 +5.
poison_sting: 적이 독 상태이면 +5.
같은 카드가 새로 표식을 붙이기 전의 적 속성으로 판정한다.
방어/회복/힘/행동력/드로우만 있는 지원 카드는 기존 표식을 덮어쓰지 않는다.
새 원소 상성이나 임의 콤보는 없다.

## 13. Intent 구조

IntentData는 타입/피해 또는 방어·힘 수치/히트 수/패턴 인덱스를 갖는 불변 객체다.
EnemyInstance.CurrentIntent는 기본 패턴, BattleRuntime.CurrentIntent는 현재 상태를 반영한 예고값이다.
턴 종료 시 예고를 스냅샷으로 고정 → 독 → 생존하면 같은 스냅샷 실행 →
약화 감소/패턴 인덱스 증가 → 다음 플레이어 턴/다음 Intent 결정.
실행 시 재랜덤/공격력 재계산을 하지 않는다.
예고의 방어 전 피해와 ExpectedHPDamage(block)를 구분한다.
약화/힘 변화는 플레이어 턴의 다음 Preview 호출에 반영된다.

일반: 공격 6 → 공격 8 → 방어 5.
엘리트: 공격 8 → 공격 5×2 → 힘 +2 → 공격 10.
보스: 공격 10 → 방어 8 → 공격 6×2 → 힘 +2.
패턴은 순환한다. 힘은 누적된다. 적 행동에는 RNG를 사용하지 않는다.

적 데이터는 신규 9종과 기존 3종 모두 포함한다.
일반 HP=38+(구역-1)×4+(층-1)×2,
엘리트 HP=52+(구역-1)×6+(층-1)×2,
보스 HP=64+(구역-1)×8, 보스에는 층 보정이 없다.
r8은 기존 일반 적, r9는 엘리트로 선택한다.
일반/엘리트/보스 모두 같은 BattleRuntime을 공유한다.

## 14. Reward 구조

Gold 50 / 후보 카드 1장 / Skip 중 하나.
후보는 전체 27종에서 해당 캐릭터의 시작 카드 4종만 제외하고 셔플한 서로 다른 3종.
구역/직업/희귀도 제한이나 이미 보유한 카드 제외는 없다.
승리 시 PendingReward를 만들되 즉시 방을 완료하거나 보상을 자동 지급하지 않는다.
RunId:RoomId, reward.Resolved, Run.ResolvedRewards, ActiveRoom 일치를 검사한다.
잘못된 카드/보상 타입은 상태 변경 전에 거절한다.
확정하면 골드 또는 카드를 즉시 적용 → RoomResult 기록 → 방 완료/ActiveRoom 해제.
같은 객체뿐 아니라 같은 ID로 다시 만든 보상도 중복 확정할 수 없다.

## 15. RunState 구조

선택 캐릭터, 현재/최대 HP, Gold, 영구 CardInstance 덱, 획득 instance ID,
Seed, RunId, 상태, 현재 구역/층/노드, ActiveRoom,
완료 노드, 보상 확정 ID, 노드 그래프, 방 결과를 보유한다.
전투는 같은 RunState를 참조하므로 보상과 다음 전투에 값이 이어진다.
MonoBehaviour/singleton/Scene 의존성이 없으며 PHASE 3의 상위 흐름에서 인스턴스를 유지하면 된다.
SaveData, 디스크 저장, 로드 시스템은 구현하지 않았다.
새 RunId는 GUID 기반으로 만들고 테스트에는 고정 ID를 주입할 수 있다.

## 16. Dungeon runtime 구조

시작 포함 11노드, 실제 방 10개: 전투 5(엘리트 포함), 퍼즐 2, 이벤트 2, 보스 1.
시작 → r1 → r2/r3/r4 → r5/r6/r7 → r8/r9 → boss.
실제 링크는 웹과 같은 교차 연결이며 고정이다. 한 경로에서는 5개 방을 방문한다.
Seed로 중간 6개 방 종류를 셔플하며 이벤트 종류는 shop/altar/rest_site 중 선택한다.
현재 위치/연결 Next/완료 목록/Available/ActiveRoom으로 상태를 구분한다.
ActiveRoom 또는 종료된 Run이 있으면 다음 방을 선택하지 못한다.
보스 완료도 보상 확정 후에만 Run Complete가 된다.
맵 UI, 이벤트 콘텐츠, 퍼즐 로직, 구역 2·3 자동 진행은 이번에 추가하지 않았다.

## 17. Seed / RNG

IRandomSource + SeededRandom. 웹 Mulberry32의 uint32 overflow/unsigned shift/곱셈을 재현한다.
Fisher–Yates의 반복 방향과 난수 소비 순서도 같다.
맵: Seed.
이벤트 종류: Seed+5801.
전투 덱: Seed+완료 방 수×211+방 column.
보상: Seed+노드 배열 인덱스×719.
UnityEngine.Random이나 전역 RNG를 사용하지 않는다.
Seed 0/17/29/4294967295의 맵/시작 손패/보상 후보와 전체 전투 추적을 비교했다.

## 18. 작성한 테스트 목록

CoreRulesTests 32사례:
캐릭터 3종 초기 상태, Run/원본 독립성, 골드/회복 검증,
비용 1회 차감 및 잘못된 사용 거절, 드로우/버림/재셔플/인스턴스 보존,
전투 한정 소멸과 사용 후 드로우 순서, 기본 피해/힘/약화,
방어·오버킬 제한, 조건부 피해의 힘 제외, 다중 타격 힘,
약화 max/비공격 행동 감소, 독의 방어 무시/감소/적 행동 전 처치,
지원 카드 표식 보존, 불/폭발/독 조건, 행동력 상한/턴 회복,
예고·실제 피해·다음 예고 일치, 고정 다중 공격/방어 소비,
적 방어 초기화/힘 유지, 적 구역·층·등급 수치 7사례,
패배 후 입력/맵 차단, 일반·기존·엘리트·보스 선택.

ProgressionTests 9사례:
고정 맵의 방 수/연결 제한, Gold/Card/Skip 각 보상 확정 및 중복 거절,
잘못된 보상 원자성/다른 방 보상 거절, 후보 중복·시작 덱 제외·기보유 허용,
획득 카드의 이후 전투 사용과 Run 유지, 승리 후 보상 대기, 보스 보상 후 완료.

WebParityTests 98사례:
실제 SO 원본 데이터 1, 카드 27×직전 표식 3=81,
Seed 기반 맵/손패/보상 4, 캐릭터 3×Seed 4의 전체 전투 기록 12.

ProjectEnvironmentTests 4사례:
실제 Package Resolve/List 및 버전 확인 1,
기존 Scene 로드/Missing Script/Serialization 검사 2, URP 2D Renderer 검사 1.

## 19. 테스트 통과/실패 결과

최종 Unity EditMode: 총 143 / 통과 143 / 실패 0 / 스킵 0, Editor 종료 코드 0.
최초 규칙 검사 139/139 통과.
환경 검사 추가 후 142/143: 테스트의 빈 초기 Scene 상태 복원 호출에서 1건 실패.
게임 규칙 실패가 아니며, additive 검사 후 저장하지 않고 정리하도록 테스트를 수정했다.
최종 재실행에서 143/143 통과했다. 앞선 실패 XML/로그도 삭제하지 않고 남겼다.

빌드 시점 Node exporter --check도 종료 코드 0.
현재 JS를 실제 실행한 결과와 Unity 쪽 snapshot/golden JSON이 일치한다.
PHASE 1의 41,876개 웹 테스트 전체를 이번에 재실행하거나 Unity에 복제했다고 주장하지 않는다.
이번 대표 비교 테스트로 검증한 범위와 전체 가능 입력에 대한 수학적 동치 증명은 다르다.

검증 원본은 저장소 밖 작업 폴더 work/phase2에 보관했다:
Editor-data-build.log, Editor-runtime-build.log,
EditMode-results.xml, Editor-tests.log,
EditMode-final-results.xml, Editor-final-tests.log,
EditMode-verified-results.xml, Editor-verified-tests.log.
최종 근거는 이름에 verified가 있는 XML/로그다.
Play Mode, 실제 UI 조작, 시각/애니메이션 검증은 미실행이며 이번 범위가 아니다.

## 20. Compile Error / Warning

Unity 6000.0.47f1 실제 실행: C# Compile Error 0, C# Compile Warning 0.
최종 규칙/Scene 검사에 게임 Console Error/Exception 및 Serialization 경고 없음.
기존 Scene 각각 root 2, Missing Script 0, 기존 UniversalRenderPipelineAsset/Renderer2DData 확인.
Package Resolve/List 성공, 49패키지.

다만 native 시작 로그에는 기존 Licensing Client의 signature Code 10 /
token·ULF 관련 오류 진단 5줄이 있다. 이후 Successfully resolved entitlement details로 정상 진행했다.
이 메시지를 숨기거나 인증 설정을 변경하지 않았다.
따라서 전체 Editor 원시 로그의 모든 Error 문자열이 0개라는 뜻은 아니다.

## 21. 웹과 결과가 달라진 부분

검증한 카드/덱/피해/상태/Intent/보상/Seed/맵 규칙 결과의 차이는 발견되지 않았다.
비규칙 구현 차이:
- 웹의 타이머/화면/busy 상태 대신 동기식 순수 전투 연산. UI·연출 지연은 구현하지 않음.
- timestamp 기반 RunId 대신 GUID(테스트에서는 고정 ID 주입).
- 웹의 null 표식은 ElementType.None.
- 웹의 이름 없는 카드 타입/희귀도는 표시용 파생 CardType/Unspecified.
- 잘못된 음수 회복 입력은 거절하고 int Gold overflow를 사전 방지.
- MaxEnergy는 캐릭터 정의에서 가져오며 현재 3종 모두 웹처럼 3.

## 22. 차이의 이유

동기식 연산은 테스트와 향후 BattleScene 연출 연결을 분리하기 위한 선택이다.
비동기 연출이 없는 동안 중복 클릭 타이밍을 재현할 필요가 없지만,
PHASE 4에서는 애니메이션 실행 중 입력을 잠그는 상위 흐름이 별도로 필요하다.
GUID는 같은 시간에 생성한 Run의 ID 충돌을 피하며 보상 RNG에는 사용하지 않는다.
enum/타입/잘못된 외부 입력 검증은 C#의 명시적인 모델링을 위한 것으로 정상 웹 게임 입력의 수치 결과를 바꾸지 않는다.
여러 전투 인스턴스를 같은 방에 동시에 만들어 사용하는 것을 허용하는 UI는 만들어선 안 된다.
PHASE 3~4 상위 흐름은 현재 Run에 하나의 활성 BattleRuntime만 유지해야 한다.

## 23. 변경된 기존 파일

이번 PHASE 2로 남긴 기존 추적 파일 변경은 0개.
기존 Scene/Prefab/입력/URP/ProjectVersion/WebPrototype은 수정하지 않았다.
이미 있던 PHASE 1.5 변경 3개는 그대로 보존했다:
Assets/Editor/HubForceResolve.cs, Packages/manifest.json, Packages/packages-lock.json.
이 파일의 diff를 PHASE 2의 새 수정으로 집계하지 않았다.
Unity가 자동 생성한 .vsconfig는 제거했고,
자동 변경한 ProjectSettings의 빈 Standalone define 항목은 원래 scriptingDefineSymbols: {}로 되돌렸다.
ProjectSettings 3파일은 HEAD와 내용 동일성을 확인하고 기존 CRLF 형식을 유지했다.
임시 제거한 .vsconfig는 Editor 실행으로 재생성되는 IDE 파일이며 팀원 원본 파일 삭제가 아니다.

## 24. 새 파일

이번 신규 파일 총 164개:
C# 24, SO Asset 45, asmdef 4, JSON 2, Unity meta 87(루트 폴더 meta 포함),
웹 참조 exporter 1, 이 보고서 1.
상세 경로는 부록 A에 모두 기재했다.
기존의 UNITY_MIGRATION_PLAN.md와 UNITY_PHASE1_5_REPORT.md는 이전 단계의 미추적 문서다.

## 25. Unity 버전

ProjectVersion.txt: 6000.0.47f1 / revision 2ad1ed33fd3b.
실제 테스트의 Application.unityVersion도 6000.0.47f1.
업그레이드하거나 다른 버전으로 재저장하지 않았다.

## 26. Package 변경 여부

PHASE 2 추가 변경 없음. PHASE 1.5 상태를 유지했다.
AI Assistant 재추가 없음, HubForceResolve 비활성 상태 유지.
URP manifest 17.0.3 / resolved 17.0.4,
Test Framework manifest 1.4.5 / resolved 1.5.1,
Mono Cecil 1.11.5 유지. 임의 버전 정렬/업그레이드 없음.

## 27. Git 상태

작업 브랜치 Paul, origin=https://github.com/Hordal/BroDungeon.git.
로컬 HEAD 및 원격 Paul은 eedba21b4552200874388bdb9f637850b14b3af0 그대로.
원격 main은 작업 전/후 모두 6f0111cbd683c3155c6e6657434e496cac8f9d0c.
commit/push/merge/branch 변경은 하지 않았다. 새 작업은 unstaged/untracked다.
기존 tracked 파일 삭제 없음. 기존 PHASE 1.5 수정 보존.
main 브랜치는 수정하지 않았다.

## 28. PHASE 3 진입 가능 여부

PHASE 2 데이터·순수 규칙 기반 기준으로 진입 가능하다.
45개 실제 SO 데이터와 143개 EditMode 테스트가 준비되어 있다.
다음 단계에서는 승인 후에만 상위 게임 흐름이 GameCatalogData → GameDefinitions →
RunState → DungeonRuntime/BattleRuntime/RewardResolver를 연결하도록 구현한다.
Scene/UI/Prefab/입력 잠금/전환 수명 관리와 시각 리소스 연결은 다음 단계의 별도 검증 대상이다.
에셋 대량 Import, Animator, 오디오, Save, 실제 UI/Play Mode 테스트는 아직 구현/검증하지 않았다.
이 보고로 멈추며 PHASE 3 지시를 기다린다.

## 재현 절차

1. Unity 6000.0.47f1만 사용해 프로젝트를 연다.
2. 데이터 변경을 의도한 경우에만 node Tools/Brodun/export-web-rules.cjs --write를 실행하고 JSON diff를 리뷰한다.
3. BRODUN/Data/Rebuild web-rule assets를 명시적으로 실행해 SO를 생성/갱신한다.
4. 평상시에는 node Tools/Brodun/export-web-rules.cjs --check로 현재 웹과 독립 참조 데이터의 일치만 검사한다.
5. Unity Test Runner EditMode에서 Brodun.Tests를 실행한다. 현재 총 143개.
6. CLI는 -batchmode -runTests -testPlatform EditMode -projectPath <project> -testResults <outside-project XML> -logFile <outside-project log>.
   -runTests와 함께 -quit를 붙여 테스트 시작 전에 종료하지 않는다.
7. 타이머·UI·Input·UnityEngine.Random은 이 순수 규칙 검증에 필요하지 않다.

## 부록 A. PHASE 2 신규 파일 전체 경로

```
Assets/BRODUN.meta
Assets/BRODUN/Data.meta
Assets/BRODUN/Data/Generated.meta
Assets/BRODUN/Data/Generated/BattleConfig.asset
Assets/BRODUN/Data/Generated/BattleConfig.asset.meta
Assets/BRODUN/Data/Generated/Cards.meta
Assets/BRODUN/Data/Generated/Cards/earth_blessing.asset
Assets/BRODUN/Data/Generated/Cards/earth_blessing.asset.meta
Assets/BRODUN/Data/Generated/Cards/earth_crush.asset
Assets/BRODUN/Data/Generated/Cards/earth_crush.asset.meta
Assets/BRODUN/Data/Generated/Cards/earth_strike.asset
Assets/BRODUN/Data/Generated/Cards/earth_strike.asset.meta
Assets/BRODUN/Data/Generated/Cards/explosion_barrier.asset
Assets/BRODUN/Data/Generated/Cards/explosion_barrier.asset.meta
Assets/BRODUN/Data/Generated/Cards/explosion_blast.asset
Assets/BRODUN/Data/Generated/Cards/explosion_blast.asset.meta
Assets/BRODUN/Data/Generated/Cards/explosion_link.asset
Assets/BRODUN/Data/Generated/Cards/explosion_link.asset.meta
Assets/BRODUN/Data/Generated/Cards/fire_bolt.asset
Assets/BRODUN/Data/Generated/Cards/fire_bolt.asset.meta
Assets/BRODUN/Data/Generated/Cards/fire_inferno.asset
Assets/BRODUN/Data/Generated/Cards/fire_inferno.asset.meta
Assets/BRODUN/Data/Generated/Cards/fire_strength.asset
Assets/BRODUN/Data/Generated/Cards/fire_strength.asset.meta
Assets/BRODUN/Data/Generated/Cards/ice_guard.asset
Assets/BRODUN/Data/Generated/Cards/ice_guard.asset.meta
Assets/BRODUN/Data/Generated/Cards/ice_link.asset
Assets/BRODUN/Data/Generated/Cards/ice_link.asset.meta
Assets/BRODUN/Data/Generated/Cards/ice_shard.asset
Assets/BRODUN/Data/Generated/Cards/ice_shard.asset.meta
Assets/BRODUN/Data/Generated/Cards/lightning_chain.asset
Assets/BRODUN/Data/Generated/Cards/lightning_chain.asset.meta
Assets/BRODUN/Data/Generated/Cards/lightning_charge.asset
Assets/BRODUN/Data/Generated/Cards/lightning_charge.asset.meta
Assets/BRODUN/Data/Generated/Cards/lightning_strike.asset
Assets/BRODUN/Data/Generated/Cards/lightning_strike.asset.meta
Assets/BRODUN/Data/Generated/Cards/poison_mist.asset
Assets/BRODUN/Data/Generated/Cards/poison_mist.asset.meta
Assets/BRODUN/Data/Generated/Cards/poison_sting.asset
Assets/BRODUN/Data/Generated/Cards/poison_sting.asset.meta
Assets/BRODUN/Data/Generated/Cards/poison_touch.asset
Assets/BRODUN/Data/Generated/Cards/poison_touch.asset.meta
Assets/BRODUN/Data/Generated/Cards/stone_fortress.asset
Assets/BRODUN/Data/Generated/Cards/stone_fortress.asset.meta
Assets/BRODUN/Data/Generated/Cards/stone_guard.asset
Assets/BRODUN/Data/Generated/Cards/stone_guard.asset.meta
Assets/BRODUN/Data/Generated/Cards/stone_retaliation.asset
Assets/BRODUN/Data/Generated/Cards/stone_retaliation.asset.meta
Assets/BRODUN/Data/Generated/Cards/water_rest.asset
Assets/BRODUN/Data/Generated/Cards/water_rest.asset.meta
Assets/BRODUN/Data/Generated/Cards/water_stream.asset
Assets/BRODUN/Data/Generated/Cards/water_stream.asset.meta
Assets/BRODUN/Data/Generated/Cards/water_wave.asset
Assets/BRODUN/Data/Generated/Cards/water_wave.asset.meta
Assets/BRODUN/Data/Generated/Cards/wind_cut.asset
Assets/BRODUN/Data/Generated/Cards/wind_cut.asset.meta
Assets/BRODUN/Data/Generated/Cards/wind_judgment.asset
Assets/BRODUN/Data/Generated/Cards/wind_judgment.asset.meta
Assets/BRODUN/Data/Generated/Cards/wind_read.asset
Assets/BRODUN/Data/Generated/Cards/wind_read.asset.meta
Assets/BRODUN/Data/Generated/Characters.meta
Assets/BRODUN/Data/Generated/Characters/mage.asset
Assets/BRODUN/Data/Generated/Characters/mage.asset.meta
Assets/BRODUN/Data/Generated/Characters/priest.asset
Assets/BRODUN/Data/Generated/Characters/priest.asset.meta
Assets/BRODUN/Data/Generated/Characters/warrior.asset
Assets/BRODUN/Data/Generated/Characters/warrior.asset.meta
Assets/BRODUN/Data/Generated/Enemies.meta
Assets/BRODUN/Data/Generated/Enemies/abyssal_leviathan.asset
Assets/BRODUN/Data/Generated/Enemies/abyssal_leviathan.asset.meta
Assets/BRODUN/Data/Generated/Enemies/coil_golem.asset
Assets/BRODUN/Data/Generated/Enemies/coil_golem.asset.meta
Assets/BRODUN/Data/Generated/Enemies/crystal_crawler.asset
Assets/BRODUN/Data/Generated/Enemies/crystal_crawler.asset.meta
Assets/BRODUN/Data/Generated/Enemies/ember_gremlin.asset
Assets/BRODUN/Data/Generated/Enemies/ember_gremlin.asset.meta
Assets/BRODUN/Data/Generated/Enemies/fallen_colossus.asset
Assets/BRODUN/Data/Generated/Enemies/fallen_colossus.asset.meta
Assets/BRODUN/Data/Generated/Enemies/foundry_tyrant.asset
Assets/BRODUN/Data/Generated/Enemies/foundry_tyrant.asset.meta
Assets/BRODUN/Data/Generated/Enemies/frost_seer.asset
Assets/BRODUN/Data/Generated/Enemies/frost_seer.asset.meta
Assets/BRODUN/Data/Generated/Enemies/furnace_sentinel.asset
Assets/BRODUN/Data/Generated/Enemies/furnace_sentinel.asset.meta
Assets/BRODUN/Data/Generated/Enemies/gale_warden.asset
Assets/BRODUN/Data/Generated/Enemies/gale_warden.asset.meta
Assets/BRODUN/Data/Generated/Enemies/moss_slime.asset
Assets/BRODUN/Data/Generated/Enemies/moss_slime.asset.meta
Assets/BRODUN/Data/Generated/Enemies/ruin_gargoyle.asset
Assets/BRODUN/Data/Generated/Enemies/ruin_gargoyle.asset.meta
Assets/BRODUN/Data/Generated/Enemies/venom_eel.asset
Assets/BRODUN/Data/Generated/Enemies/venom_eel.asset.meta
Assets/BRODUN/Data/Generated/GameCatalog.asset
Assets/BRODUN/Data/Generated/GameCatalog.asset.meta
Assets/BRODUN/Data/Generated/Reward.asset
Assets/BRODUN/Data/Generated/Reward.asset.meta
Assets/BRODUN/Data/WebRulesSnapshot.json
Assets/BRODUN/Data/WebRulesSnapshot.json.meta
Assets/BRODUN/Scripts.meta
Assets/BRODUN/Scripts/Data.meta
Assets/BRODUN/Scripts/Data/BattleConfigData.cs
Assets/BRODUN/Scripts/Data/BattleConfigData.cs.meta
Assets/BRODUN/Scripts/Data/Brodun.Data.asmdef
Assets/BRODUN/Scripts/Data/Brodun.Data.asmdef.meta
Assets/BRODUN/Scripts/Data/CardData.cs
Assets/BRODUN/Scripts/Data/CardData.cs.meta
Assets/BRODUN/Scripts/Data/CharacterData.cs
Assets/BRODUN/Scripts/Data/CharacterData.cs.meta
Assets/BRODUN/Scripts/Data/EnemyData.cs
Assets/BRODUN/Scripts/Data/EnemyData.cs.meta
Assets/BRODUN/Scripts/Data/GameCatalogData.cs
Assets/BRODUN/Scripts/Data/GameCatalogData.cs.meta
Assets/BRODUN/Scripts/Data/RewardData.cs
Assets/BRODUN/Scripts/Data/RewardData.cs.meta
Assets/BRODUN/Scripts/Data/WebSnapshot.cs
Assets/BRODUN/Scripts/Data/WebSnapshot.cs.meta
Assets/BRODUN/Scripts/Editor.meta
Assets/BRODUN/Scripts/Editor/Brodun.Editor.asmdef
Assets/BRODUN/Scripts/Editor/Brodun.Editor.asmdef.meta
Assets/BRODUN/Scripts/Editor/DataAssetBuilder.cs
Assets/BRODUN/Scripts/Editor/DataAssetBuilder.cs.meta
Assets/BRODUN/Scripts/Rules.meta
Assets/BRODUN/Scripts/Rules/AssemblyInfo.cs
Assets/BRODUN/Scripts/Rules/AssemblyInfo.cs.meta
Assets/BRODUN/Scripts/Rules/BattleRuntime.cs
Assets/BRODUN/Scripts/Rules/BattleRuntime.cs.meta
Assets/BRODUN/Scripts/Rules/Brodun.Rules.asmdef
Assets/BRODUN/Scripts/Rules/Brodun.Rules.asmdef.meta
Assets/BRODUN/Scripts/Rules/CardEffectResolver.cs
Assets/BRODUN/Scripts/Rules/CardEffectResolver.cs.meta
Assets/BRODUN/Scripts/Rules/DamageCalculator.cs
Assets/BRODUN/Scripts/Rules/DamageCalculator.cs.meta
Assets/BRODUN/Scripts/Rules/DeckRuntime.cs
Assets/BRODUN/Scripts/Rules/DeckRuntime.cs.meta
Assets/BRODUN/Scripts/Rules/Definitions.cs
Assets/BRODUN/Scripts/Rules/Definitions.cs.meta
Assets/BRODUN/Scripts/Rules/DungeonRuntime.cs
Assets/BRODUN/Scripts/Rules/DungeonRuntime.cs.meta
Assets/BRODUN/Scripts/Rules/EnemyInstance.cs
Assets/BRODUN/Scripts/Rules/EnemyInstance.cs.meta
Assets/BRODUN/Scripts/Rules/RewardRuntime.cs
Assets/BRODUN/Scripts/Rules/RewardRuntime.cs.meta
Assets/BRODUN/Scripts/Rules/RunState.cs
Assets/BRODUN/Scripts/Rules/RunState.cs.meta
Assets/BRODUN/Scripts/Rules/SeededRandom.cs
Assets/BRODUN/Scripts/Rules/SeededRandom.cs.meta
Assets/BRODUN/Tests.meta
Assets/BRODUN/Tests/Editor.meta
Assets/BRODUN/Tests/Editor/Brodun.Tests.Editor.asmdef
Assets/BRODUN/Tests/Editor/Brodun.Tests.Editor.asmdef.meta
Assets/BRODUN/Tests/Editor/CoreRulesTests.cs
Assets/BRODUN/Tests/Editor/CoreRulesTests.cs.meta
Assets/BRODUN/Tests/Editor/ProgressionTests.cs
Assets/BRODUN/Tests/Editor/ProgressionTests.cs.meta
Assets/BRODUN/Tests/Editor/ProjectEnvironmentTests.cs
Assets/BRODUN/Tests/Editor/ProjectEnvironmentTests.cs.meta
Assets/BRODUN/Tests/Editor/TestSupport.cs
Assets/BRODUN/Tests/Editor/TestSupport.cs.meta
Assets/BRODUN/Tests/Editor/WebGoldenResults.json
Assets/BRODUN/Tests/Editor/WebGoldenResults.json.meta
Assets/BRODUN/Tests/Editor/WebParityTests.cs
Assets/BRODUN/Tests/Editor/WebParityTests.cs.meta
Tools/Brodun/export-web-rules.cjs
UNITY_PHASE2_REPORT.md
```
