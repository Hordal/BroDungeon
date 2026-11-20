# BRODUN PHASE 4 — Battle Vertical Slice / Reward / Dungeon 복귀

검증일: 2026-10-13. Unity 6000.0.47f1 (2ad1ed33fd3b), Paul.

프로젝트 위치: `C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul`.

PHASE 2의 전투/덱/데미지/상태/Intent/보상 규칙을 호출하는 Unity 화면과 진행 계층을 추가했다. 게임 수치나 기존 랜덤 맵 생성 규칙은 변경하지 않았다. 자동 검사와 실제 Mouse 검사 모두 완료했으며 아래에서 증거 수준을 구분한다.

## 1. 생성 Scene

[BrodunBattle.unity](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scenes/BrodunBattle.unity) 하나를 추가했다. 일반/엘리트/보스가 모두 이 Scene과 공통 전투 코드를 사용한다. 기존 4개 BRODUN Scene과 팀원의 SampleScene/URP 템플릿은 재생성하지 않았다. Build Settings에는 새 Scene 항목만 추가했다.

## 2. 생성 Prefab

- [CardView.prefab](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Prefabs/CardView.prefab): 모든 손패/보상 카드가 공유한다.
- [EnemyView.prefab](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Prefabs/EnemyView.prefab): 모든 적의 이미지/HP/상태/Intent/툴팁을 공유한다.

27종 카드나 적 종류마다 로직 Prefab을 복제하지 않았다.

## 3. 생성 Script

- [BattleContext.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Flow/BattleContext.cs): 지속되는 조우와 입력 상태.
- [BattleController.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/BattleController.cs): 입력 전달, 연출 순서, 보상/복귀 연결.
- [BattleView.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/BattleView.cs): 전투/손패/보상/패배 UI.
- [BattleUi.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/BattleUi.cs): 표시용 UI 생성 도구.
- [CardView.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/CardView.cs): 공통 카드 표시/Hover/Click.
- [EnemyView.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/EnemyView.cs): 적의 숫자와 프레임 애니메이션.
- [IntentHover.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/IntentHover.cs): Intent 툴팁.
- [BattlePresentationAssets.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/BattlePresentationAssets.cs): Sprite/Prefab/EnemyData 참조와 FPS.
- [Phase4ProjectBuilder.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Editor/Phase4ProjectBuilder.cs): 기존 manifest를 읽어 신규 아트만 임포트하고 Scene/Prefab/SO 생성.
- [Phase4Qa.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Editor/Phase4Qa.cs): Editor 전용 재현 Seed 시작 메뉴.
- [BattleSliceTests.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Tests/Editor/BattleSliceTests.cs): 신규 EditMode 검사.
- [BattleSceneTests.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Tests/PlayMode/BattleSceneTests.cs): 신규 실제 Scene/UI 검사와 캡처.

위 C# 파일에는 Unity가 생성한 `.meta`가 함께 있다. 새 ScriptableObject는 `Assets/BRODUN/Resources/BrodunBattle.asset` 하나이며 **게임 규칙 데이터가 아니라 표현 리소스 참조**다.

## 4. 변경 기존 파일

- [FlowSession.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Flow/FlowSession.cs): Battle 화면/Context, 진입 및 보상 후 복귀 가드.
- [GameFlow.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/GameFlow.cs): 실제 BattleScene 로드/재로드/Fade/복귀.
- [GameScreenView.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/GameScreenView.cs): 완료된 현재 방도 완료 아이콘과 `현재 · ✓ 완료`로 표시.
- [CardEffectResolver.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Rules/CardEffectResolver.cs): 조건부 효과가 실제로 발동했는지 보여주는 `ConditionalTriggered` trace만 추가. 조건식/수치/피해 계산은 그대로다.
- [Phase3ProjectBuilder.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Editor/Phase3ProjectBuilder.cs): enum 확장 후에도 기존 4개 화면만 대상으로 하고, 새 BattleScene을 잘못 재생성/등록 해제하지 않도록 범위 제한. 이번 작업에서 이 Builder를 실행해 기존 Scene을 다시 만들지는 않았다.
- [ScreenFlowTests.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Tests/PlayMode/ScreenFlowTests.cs): 기존 Stub 전제를 실제 BattleScene 진입/동일 Context 재로드 검사로 갱신.
- [MapUxTests.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Tests/PlayMode/MapUxTests.cs): 맵 단독 재로드 검사를 분리하고 캡처 출력 경로를 환경 변수로 지정할 수 있게 함.
- [EditorBuildSettings.asset](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/ProjectSettings/EditorBuildSettings.asset): BattleScene 항목 추가. 기존 항목과 enabled 값 유지.

기존 테스트를 삭제/Ignore하지 않았다. 이전 단계의 미커밋 파일과 이번 변경은 작업 시작 시 복사본과 비교해 구분한다.

## 5. BattleContext 구조

Context가 기존 RunState, 선택 RoomNode, BattleRuntime을 참조한다. Room ID/타입/조우, Character/HP/덱/Seed는 그 객체에서 제공된다. 새 Scene이 적이나 덱을 다시 생성하지 않는다. Context는 GameFlow의 FlowSession이 소유하며 Scene 재로드에도 유지된다.

상태는 `Initializing → PlayerTurn → ResolvingPlayerAction / EnemyTurn → Victory / Defeat → Reward → Finished`다. 여러 bool 대신 이 상태로 카드·턴·보상 입력을 제한한다. 단일 적은 `enemy-0` 식별자로 자동 대상으로 지정하며, Context API의 Target ID 검증 지점은 별도로 남겼다.

## 6. Encounter 결정 방식

기존 `BattleRuntime` 생성자가 `IntentResolver.Select`와 `EnemyInstance`를 호출한다. 현재 Area/Room EncounterType으로 기존 EnemyData를 선택한다. Seed가 재현하는 맵/방 정보가 같으면 Encounter도 같다. 적을 다시 랜덤으로 고르지 않는다.

기존 웹의 **일반전 r8은 legacy 적을 선택하는 규칙**도 유지했다. 새 맵에서 r8이 Elite면 Elite가 우선이다. 일반/Elite/Boss 선택, 층별 HP 증가, 덱 Shuffle/보상 Seed 식을 이번 UI에서 재정의하지 않았다.

## 7. Normal Battle 구현

1구역 폐허 가고일과 기존 이끼 슬라임을 지원한다. 기본 일반 HP 38, 공격 패턴 6 → 8 → 방어 5, 비보스의 층별 HP 증가는 기존 값 `(Floor - 1) × 2`다. 카드 기본 피해 5/4, 비용 1, 방어 5도 그대로다.

## 8. Elite Battle 구현

질풍 파수꾼을 같은 Scene/Runtime/View에 연결했다. 기본 HP 52에 기존 층 보정을 적용한다. 패턴은 공격 8 → 5×2 → 힘 +2 → 공격 10이다. 일반과 다른 이미지/이름/HP/Intent를 표시한다.

1구역 보스 몰락한 거신도 기본 공통 전투 연결과 리소스를 준비했다. HP 64, 공격 10/방어 8/6×2/힘 +2 등 기존 패턴을 사용한다. 전용 Boss Phase나 최종 연출은 만들지 않았다. 보스 Encounter 생성은 자동 검사했고 전체 Run의 보스 승리까지 실제 Mouse 플레이했다고 주장하지 않는다.

## 9. Player UI

선택 Character 이름/기존 Portrait, HP/MaxHP/보간 HP Bar, Block, 실제 존재하는 Strength를 표시한다. 없는 Player 상태 아이콘은 만들지 않았다. 피격에는 짧은 Flash와 Portrait에만 약한 수평 흔들림을 사용한다.

## 10. Enemy UI

기존 EnemyInstance의 이름/등급/HP/MaxHP/Block/Strength/Weak/Poison과 현재 Intent를 표시한다. 상태가 0이면 해당 상태 텍스트를 생략한다. HP 수치는 Runtime을 읽고 Bar만 시각적으로 따라간다.

## 11. Card Hand 구조

기존 DeckRuntime.Hand에 해당하는 CardView를 하단 수평 손패로 만든다. 카드 수에 따라 중심과 간격을 조정한다. 뽑을 카드/버림/소멸 pile 수를 함께 표시한다. 초기 Draw는 기존 설정 **4장**이며 시작 덱 4장이 전부 표시된다. 테스트용 추가 Draw를 제품 규칙 변경과 혼동하지 않는다.

## 12. Card Hover 구조

고정된 카드 Hit Rect와 별도 Visual 자식을 사용한다. 마우스 진입 시 Visual이 약 42 기준 px 올라오고 1.10배 확대되며, 부모 카드 순서를 앞으로 보낸다. Hit Rect 자체를 이동하지 않아 Hover 진입/이탈 진동을 피한다. 해제하면 원위치/순서로 부드럽게 돌아간다.

카드 이름/비용/속성/공격·방어·기술 분류/기본 피해·방어/설명을 기존 CardDefinition에서 읽는다. UI에 카드별 수치를 다시 적지 않는다.

## 13. Energy 처리

크게 `ENERGY 현재 / 턴 기준`을 표시하고 비용 차감/회복을 즉시 반영한다. 초기/턴 시작 3, 하드 상한 6은 기존 설정 그대로다. 자원 카드로 턴 기준 3을 초과할 수 있으며 UI 숫자도 그대로 표시한다. Energy가 부족한 카드는 흐리게 보이고 Click해도 Runtime을 실행하지 않는다.

## 14. Turn Flow

`BattleContext.EndTurn → 기존 BattleRuntime.EndTurn`을 호출한다. 기존 남은 Hand 버림, Poison tick, 실행 Intent snapshot, Block/HP/Weak/Strength, 턴 종료/다음 Draw/Energy 갱신 순서를 그대로 사용한다. Runtime의 턴 처리는 동기적이고, Controller는 그 결과 주변의 연출과 입력 잠금을 담당한다.

## 15. Intent Flow

표시/툴팁은 `BattleRuntime.CurrentIntent`와 기존 `IntentData.ExpectedHPDamage`를 읽는다. 별도 데미지 예측식이나 Intent 재추첨은 없다. 턴 종료 시 실행된 예고를 연출 중 유지하고, 연출 종료 후 새 Runtime Intent로 갱신한다. Attack/다중 Attack/Block/Buff를 지원한다.

## 16. Damage / Block UI 연결

Damage/Block 계산은 기존 DamageCalculator/CardEffectResolver/IntentResolver에서만 수행한다. 숫자는 실제 CardResult/EnemyActionResult의 HPDamage/Blocked를 사용한다. 방어 후 0 피해인 상황과 다중 공격도 같은 규칙을 거친다. 실제 HP는 즉시 변경하고 HP Bar 보간/떠오르는 숫자만 표현한다.

## 17. Status UI

실제 Enemy Strength/Weak/Poison과 Player Strength를 표시한다. Poison 감소와 Weak 턴 감소를 View에서 다시 계산하지 않는다. 없는 디버프/유물/버프 시스템을 추가하지 않았다.

## 18. Element Combo Feedback

기존 조건부 효과 Handler가 실제 조건을 만족했을 때 남기는 bool trace로 `COMBO / 카드 이름`을 표시한다. View가 조건이나 추가 피해량을 다시 계산하지 않는다. 물 → 얼음 연계의 4+7 피해와 방어 조건부 공격을 실제 UI 경로 테스트에 포함했다.

## 19. Monster Animation

기존 원본에서 1구역 가고일/파수꾼/거신/슬라임의 Idle/Attack/Hurt/Death **4×4×4 = 64 PNG 프레임**을 추가 임포트했다. 기존 아트는 삭제/교체하지 않았다.

FPS/loop는 기존 manifest에서 읽는다: Idle 6 FPS loop, Attack/Hurt 10 FPS non-loop, Death 7 FPS non-loop. Walk는 현재 연출에 필요하지 않아 임포트/재생하지 않는다. 키보드 이동 시스템은 추가하지 않았다.

프레임별 기존 pivotX/pivotY를 Sprite Importer와 Image Rect에 적용하고, 같은 적의 모든 모션은 고정 pixel scale을 사용한다. 매 프레임을 동일한 높이로 강제 확대하는 방식으로 발 위치가 흔들리지 않게 했다. 구현은 작은 Sprite 프레임 재생기이며 새 애니메이션 패키지나 Animator Controller는 필요하지 않다.

## 20. Enemy Death 처리

Runtime Won이면 새 카드/턴 입력을 막고 Death를 끝까지 재생한 뒤 Victory/Reward로 간다. Death 마지막 프레임을 유지하며 Idle로 돌아가지 않는다. Reward 상태에서 Scene을 재로드해도 이미 죽은 적은 Death 마지막 프레임으로 복원한다.

## 21. Player Death 처리

Runtime Lost/RunStatus.Failed이면 입력을 잠그고 패배 Overlay와 `[타이틀로]`를 제공한다. 방을 Complete하지 않고 보상을 생성/선택할 수 없게 한다. 완성형 GameOver Scene이나 저장 기능은 만들지 않았다.

## 22. Victory 구조

전투 Runtime의 Won → 입력 잠금 → Death → 짧은 Victory 표시 → Reward Overlay다. BattleRuntime이 만든 PendingReward를 유지하며, 승리만으로 Map으로 돌아가지 않는다.

## 23. Reward UI

같은 공통 Card Prefab으로 서로 다른 후보 3장과 이름/비용/속성/설명을 표시한다. 카드 1장, Gold, Skip 중 하나만 선택한다. 후보 생성/중복 검증은 기존 RewardResolver만 사용한다.

## 24. Gold Reward

기존 보상 50 Gold를 적용한다. 선택 즉시 RunState/HUD가 증가하고 `+50 GOLD`를 표시한다. 별도 UI 숫자를 재화처럼 관리하지 않는다.

## 25. Card Reward

기존 후보 중 선택한 Card ID를 RewardResolver.Resolve에 전달한다. 새 CardInstance가 영구 Run Deck에 추가된다. Map의 덱 보기와 다음 전투 Draw에서 확인한다. 카드 Upgrade나 별도 희귀도 생성 규칙은 추가하지 않았다.

## 26. Skip

Gold/카드 없이 기존 RewardResolver의 Skip으로 확정한다. 지급 0이어도 ResolvedReward/RoomResult를 기록하고 방 완료와 복귀를 진행한다.

## 27. Reward 중복 방지

입력 상태 Reward → Finished가 첫 선택에만 바뀐다. 기존 reward.Resolved/Run.ResolvedRewards/ActiveRoom 검증을 유지한다. Overlay 입력도 즉시 잠근다. 버튼 연타/확정 전 Scene 재로드/다시 열린 Overlay에서 중복 지급되지 않는지 검사한다.

## 28. Dungeon 복귀

보상 확정과 짧은 획득 피드백 이후 기존 GameFlow Fade로 DungeonMap을 로드한다. BattleContext를 정리하되 같은 RunState를 유지한다. Scene 재로드가 새 Run/전투/보상을 만드는 경로가 되지 않도록 한다.

## 29. Node Complete 처리

기존 RewardResolver가 보상 확정 뒤 DungeonRuntime.Complete를 호출한다. Node Click이나 Victory 시점에는 완료되지 않는다. 완료된 현재 방은 선택 프레임과 완료 아이콘/`현재 · ✓ 완료` 표시를 함께 사용한다.

## 30. Next Node 활성화

기존 DAG의 현재 방 Next만 Available이다. 다른 Branch/이전 Node/Boss를 임의로 열지 않는다. 보상 확정 전 ActiveRoom이 존재하는 동안 다음 방은 잠긴다. Boss 완료 시 기존 RunStatus.Complete 처리로 진행이 끝난다.

## 31. RunState 유지 결과

자동 검사에서 동일 Run 객체, Character/Seed/HP/Gold/Deck/획득 카드/현재 방/Completed/보상 처리 기록을 검사한다. Map 재로드 시 현재 노드 근처로 PHASE 3.5의 기존 Focus가 동작한다. 처음 위치로 무조건 돌아가지 않는다.

## 32. EditMode 테스트

기존 **168개 보존**, 신규 **20개**. 최종 **188/188 통과**.

[최종 XML](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/EditMode-final.xml)

일반/Elite/Boss 조우 재현, Context/Hand/Energy, 입력 잠금, 실제 Intent/Block, Victory/Gold/Card/Skip/중복/Next, Defeat, 카드 종류/소멸, 조건부 trace, Prefab/Scene 참조 및 Missing Script를 포함한다. 기존 웹 규칙 비교와 1,024 Seed 랜덤 맵 검사도 계속 통과한다.

## 33. PlayMode 테스트

기존 **12개 보존**, 신규 **7개**, 최종 **19/19 통과**. [최종 XML](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/PlayMode-final-pass.xml).

새 검사: 실제 Scene/Card/Intent/Gold 루프, Card Reward 재로드/획득 카드 재사용, Skip/Defeat, 연결 경로의 일반→일반→Elite 연속 승리, 다양한 카드 UI, 물→얼음/행동력 부족, 세 해상도 캡처/경계.

전투 자동 전략은 테스트에서 실제 카드/턴 입력을 전달할 뿐 HP/공격력/승패를 고치지 않는다. 카드 종류 검사의 추가 카드/추가 Draw는 **테스트 fixture만** 준비하는 것이며 일반 플레이에 지급되지 않는다. 실패한 이전 QA 실행은 최종 결과와 구분해 폴더에 보존한다.

## 34. 실제 Mouse 테스트

computer-use 스킬로 Unity 6000.0.47f1 Editor를 직접 조작해 별도로 검사했다. 자동 테스트의 pointer 이벤트 전달과 아래 실제 Mouse 입력 결과를 구분한다.

전사, Seed 7의 연결 경로를 능력치/HP/승패 조작 없이 연속 플레이했다.

| 방 | 실제 결과 | 보상 및 복귀 상태 |
| --- | --- | --- |
| r2 일반 | 5턴 승리, HP 60 → 55 | `화로의 힘` 선택, 덱 4 → 5, 완료된 현재 방/다음 연결 방 확인 |
| r4 일반 | 3턴 승리, HP 55 → 47 | 획득한 `화로의 힘` 드로우·사용, Gold 0 → 50, 맵에서도 유지 |
| r8 엘리트 | 4턴 승리, HP 47 → 35 | Skip 선택, Gold 50/덱 5 유지, 일반 2방과 엘리트 완료 표시, 현재 방 근처 Focus/다음 연결 방 활성화 |

Card Hover의 상승/확대/복귀, Card Click, End Turn, Intent Hover 툴팁, Reward Card Hover/선택을 확인했다. 0 Cost `바람 읽기`의 Draw/Exhaust, 행동력 3 → 0/다음 턴 3 회복, 행동력 부족 카드 클릭 차단, 적 Hurt/Attack/Death와 보상 전환도 확인했다. 첫 일반전에서 공격 6에 방어 5를 사용하자 HP 피해 1, 엘리트의 공격 5×2에 방어 3을 사용하자 HP 피해 7이었다. 다음 Intent는 실제 패턴대로 갱신됐으며 엘리트 힘 +2 뒤 공격 12도 표시됐다.

맵의 현재 덱 보기에서 획득 카드 포함 5장을 직접 확인했다. Gold 선택 직후 +50 GOLD와 HUD 증가, Skip 직후 건너뛰기 피드백을 확인했다. 세 전투 모두 보상 처리 후에만 맵으로 복귀했다. 마지막 상태의 [네이티브 Editor 캡처](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/NativeMouse_EliteReturn.png)를 보관했다. 검증 후 Play Mode와 검증용 Editor를 정상 종료했다.

Editor 재현 메뉴: Play Mode의 Title에서 `BRODUN → QA → PHASE 4 - Start seed 7`. 이는 기존 StartNewRun(7)만 호출한다. 능력치/덱/HP/보상/완료 상태를 변경하지 않는다. 일반→일반→Elite 경로는 **r2 → r4 → r8**이다.

## 35. 해상도 테스트

1920×1080 / 1600×900 / 1280×720의 실제 Unity Game View를 검사한다. 카드 Root/Intent/EndTurn/Energy의 화면 내부 경계를 검사하고 캡처를 열어 HP/손패/보상 배치를 확인한다. 초광폭/모바일 화면비는 이번 검증 범위 밖이다.

## 36. Screenshot 목록

출력 폴더: `C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots`.

- [Battle Start](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/BattleStart_1920x1080.png)
- [Card Hover](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/CardHover_1920x1080.png)
- [Enemy Intent Tooltip](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/EnemyIntent_1280x720.png)
- [Enemy Hurt](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/EnemyHurt_1920x1080.png)
- [Enemy Turn](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/EnemyTurn_1920x1080.png)
- [Victory / Card Reward](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/VictoryCardReward_1920x1080.png)
- [Gold Reward](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/GoldReward_1920x1080.png)
- [Dungeon 복귀](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/screenshots/DungeonReturn_1920x1080.png)

위 8개 상태를 3개 해상도로 캡처한 **24장**과 실제 Mouse 엘리트 완료/맵 복귀의 네이티브 Editor 캡처 **1장**이다. Victory와 Card Reward는 같은 캡처에 표시된다. 별도 순간 Victory 단독 PNG나 네이티브 마우스 녹화 영상은 만들지 않았다. 기존 화면/맵 회귀 캡처는 저장소 밖 별도 하위 폴더에 둔다.

## 37. Compile Error / Warning

최종 C# Compile Error **0**, C# Warning **0**. 최종 EditMode/PlayMode 로그에서 컴파일 오류/경고가 없고 두 실행 모두 exit code 0이다. 개발 중 TextureImporter의 pivot 설정 API 호출 오류는 TextureImporterSettings 방식으로 수정했다. 배치 검증 한 차례는 이전 compiler lock 대기로 재시작했다. 패키지나 Editor 업그레이드로 우회하지 않았다.

## 38. Console Error / Warning

실제 Mouse 플레이를 완료한 Editor Console에서 **Error 0 / Warning 0 / Info 1**을 확인했다. Info는 QA Seed 7 시작 안내다. Console을 Clear해서 수치를 지우지 않았다. [실제 Editor 로그](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase4/Editor-interactive.log)에도 게임 Runtime Exception/C# 컴파일 오류가 없다.

단, Editor 시작 로그에는 기존 Licensing Client의 서명 검증 Code 10/토큰 갱신 실패 진단이 있다. 이는 Console의 게임 오류 수치와 구분하며, 이후 Editor 로드·Play Mode·세 전투 플레이·정상 종료를 실제 확인했다. 따라서 모든 종류의 로그 진단이 0이라고 주장하지 않는다.

## 39. Missing Script

새 BattleScene과 두 공통 Prefab은 EditMode 검사에서 Missing Script **0**이다. 모든 신규 MonoBehaviour는 독립 C# 파일로 관리하고 Sprite/SO/Prefab 참조가 존재하는지 검사한다. 기존 팀원 Scene을 삭제하거나 다른 Scene으로 교체하지 않았다.

## 40. Placeholder 목록

- 카드 전용 27종 일러스트 대신 기존 공격/제단 아이콘을 사용하는 공통 Artwork placeholder.
- 기술 카드에는 기존 card_back 이미지, 공격/방어에는 기존 프레임을 사용한다. 새 최종 카드 아트가 아니다.
- 전투 배경은 기존 1구역 배경 재사용.
- Player는 기존 정지 Portrait에 Flash/미세 Shake. 새로운 Player 애니메이션을 만들었다고 주장하지 않는다.
- 효과음은 실제 파일이 없어 연결하지 않았다. Controller의 card/attack/hit/death/victory/defeat/reward 이벤트 Hook만 준비했다.

## 41. 아직 미구현 기능

2·3구역 완성, Boss 전용 Phase/최종 연출, 실제 Shop/Altar/Rest/Puzzle 콘텐츠, Save/Continue, 최종 Audio/VFX/밸런스, 완전한 Multi Enemy/카드 Upgrade는 미구현이다.

비전투 방은 기존 PHASE 3.5의 Pending/미구현 안내를 유지한다. 이를 공짜 완료나 가짜 보상으로 처리하지 않았다. 따라서 이번 검증의 반복 플레이 경로는 전투방 경로이며, 모든 무작위 경로에서 전체 던전을 끝낼 수 있다고 주장하지 않는다.

## 42. Unity 버전

**6000.0.47f1 (2ad1ed33fd3b)** 고정. ProjectVersion 변경이나 Editor 업그레이드는 하지 않았다. 최종 ProjectVersion.txt와 실제 Editor 제목 모두 확인했다.

## 43. Package 변경 여부

이번 PHASE에서 `manifest.json`/`packages-lock.json`을 변경하지 않았다. 작업 시작 복사본과 두 파일의 SHA256이 일치한다. 기존 uGUI/Input System/URP만 활용했다. 기존 PHASE 1.5의 Package 변경을 이번 변경으로 다시 분류하지 않는다.

## 44. Git 상태

Repository: `https://github.com/Hordal/BroDungeon.git`, branch **Paul**.

작업 시작 HEAD/원격 Paul: `eedba21b4552200874388bdb9f637850b14b3af0`.

작업 시작 원격 main: `6f0111cbd683c3155c6e6657434e496cac8f9d0c`.

최종 로컬 HEAD와 원격 Paul은 시작과 동일한 `eedba21b4552200874388bdb9f637850b14b3af0`이다. 원격 main도 `6f0111cbd683c3155c6e6657434e496cac8f9d0c`로 동일하다. branch Paul, origin URL을 다시 확인했다. stage/commit/push/PR/merge/force push는 수행하지 않았다.

이전 단계의 dirty/untracked 파일을 보존했다. 시작 복사본에 존재하던 BRODUN 원본 `.png/.unity/.asset/.meta` **216개**를 SHA256으로 비교한 결과 변경 0/누락 0이다. 기존 ProjectSettings.asset도 시작 복사본과 동일하다. 의도한 Build Settings 차이는 새 BattleScene 등록뿐이다. `git diff --check`는 오류 없이 통과했고, Git의 LF→CRLF 안내는 공백 오류/컴파일 경고와 구분했다.

Editor가 이번 실행 중 자동 생성한 `ProjectSettings/SceneTemplateSettings.json`만 제거해 불필요한 프로젝트 설정 변경을 남기지 않았다. 기존 팀원 파일이 아니며 Editor가 재생성할 수 있는 설정이다. 이 파일 외에 기존 프로젝트 파일을 삭제하지 않았다. 테스트 로그/XML/검증 캡처/시작 복사본은 저장소 밖 `work/phase4`에 보관했다.

**main 브랜치는 수정하지 않았다.**

## 45. PHASE 5 진입 가능 여부

이번 범위의 PHASE 4 완료 기준을 충족했고 PHASE 5의 UI/VFX/타격감 개선을 시작할 수 있는 상태다. EditMode 188/188, PlayMode 19/19, 실제 Mouse 일반전 2회/엘리트전 1회와 카드·골드·Skip 보상/맵 복귀를 확인했다. 전체 무작위 경로 완주, 비전투 방 콘텐츠, 보스 전체 승리까지 검증 완료라는 의미는 아니다.

PHASE 5 구현은 시작하지 않았다. commit/push도 하지 않았으며 사용자의 다음 지시를 기다린다.
