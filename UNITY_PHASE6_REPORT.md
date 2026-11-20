# BRODUN PHASE 6 검증 보고서

상태: **[PHASE 6 COMPLETE]**. 기준일 2026-10-14 (Asia/Seoul). 실제 Unity Editor 마우스 클리어/사망 검증과 전체 자동 테스트를 완료했다.

## 1. 전체 Run Flow

Boot → Title → Character Select → 랜덤 Dungeon Map → 연결된 방 선택 → Battle/Room → 보상 또는 방 선택 완료 → Map → Boss Battle → Run Clear. HP 0은 Run Fail로 분기한다. 결과에서 New Run 또는 Title을 선택한다. Scene 전환은 기존 GameFlow Fade/입력 잠금으로 통일했다.

## 2. 추가한 Room Type

새 enum을 임의 증식하지 않았다. 기존 `RoomType.Battle/Puzzle/Event/Boss`와 `EventKind.Shop/Altar/RestSite/None`, `EnemyType.Normal/Elite/Boss`를 그대로 사용한다. 새 논리 화면 Room/RunResult는 기존 generic BrodunDungeonMap Scene을 재사용한다. 새로운 Scene이나 별도 Boss BattleScene은 없다.

## 3. Normal Encounter 구성

EncounterPool은 기존 12 Enemy를 Area/Rank별로 묶는다. Area 1 일반은 ruin_gargoyle/moss_slime, Area 2는 venom_eel/crystal_crawler, Area 3은 ember_gremlin/furnace_sentinel. 현재 실행 가능한 한 Run은 Area 1이다. 첫 전투는 기존 가고일을 유지하고 이후 방은 Seed + Node ID의 안정적인 FNV-1a 해시로 풀에서 선택한다. 카드 드로우/보상 RNG 스트림과 분리했다. 일반 두 적의 기존 수치/패턴은 동일하므로 이번 다양화는 주로 외형/Encounter 정체성의 다양화다.

## 4. Elite Encounter 구성

Area 1 gale_warden, Area 2 frost_seer, Area 3 coil_golem. 현재 Area 1에서 파수꾼을 사용한다. 기존 HP 52 + 기존 층별 보정, 공격 8 → 5×2 → 힘 +2 → 공격 10의 규칙을 유지했다. 기존 랜덤 맵의 엘리트 배치/접속 제한도 변경하지 않았다.

## 5. Boss 구현 내용

Area 1 몰락한 거신(fallen_colossus)의 기존 Sprite/Idle/Attack/Hurt/Death와 데이터 HP 64, 공격 10 → 방어 8 → 공격 6×2 → 힘 +2를 재사용했다. 같은 BattleRuntime/BattleScene을 사용한다. BattleContext에서 Boss 보상 열기를 금지하고 승리 연출 이후 보스 완료 → Run Clear로 전환한다. 보스 완료 이후 일반 Map 복귀도 차단한다. Area 2/3 보스 데이터는 풀에 존재하지만 전체 Area 진행은 이번 범위 밖이다.

## 6. Rest 구현

웹 Config의 EventHeal **8**을 우선 이식했다(새 HP 비율 규칙으로 바꾸지 않음). 최대 HP까지 회복하며 한 번만 선택 가능하다. 같은 방을 Reload해도 ChoiceMade가 RunState.RoomVisits에 남는다. 선택 후 맵으로 버튼을 눌러 완료한다. 그냥 지나가기도 가능하다.

## 7. Shop 구현

Seed/Node 기반의 서로 다른 기존 카드 3종을 제시한다. 웹에는 실제 상점 가격/구매가 없었으므로 PHASE 6 RoomRules의 신규 기본 가격 **30 Gold**를 사용한다. 구매 시 Gold 차감과 Deck 추가, 상품 Purchased 상태를 즉시 적용한다. 부족/구매 완료 버튼은 잠기며 반복 클릭/Scene Reload로 재구매할 수 없다. 여러 상품은 잔액이 허용하는 한 각각 한 번씩 구매 가능하다. 나가기 후 노드가 완료된다.

## 8. Altar / Puzzle / Event 구현

제단은 한 번 선택: HP −6 / Gold +15, 또는 Gold −10 / HP +8, 또는 거절. HP 제물은 현재 HP >6일 때만 가능해 제단이 즉사 선택이 되지 않는다. 기존 EventGold **15**, EventHeal **8**을 재사용하며 신규 비용만 RoomRules에 모았다.

퍼즐/일반 Event는 최소 선택형 탐색: Seed 결과에 따라 Gold +15 또는 HP −6, 아니면 지나가기. 정식 퍼즐 미니게임은 아니다. 함정이 치명적이면 방 완료/보상 없이 Run Fail로 간다. 상점·휴식·제단은 기존 EventRooms PNG를 새 Unity Sprite 참조로 가져왔다.

## 9. RunState 통합 결과

기존 Character/HP/MaxHP/Gold/Deck/CurrentNode/CompletedNodes/Seed/Area/Floor를 동일 RunState에서 사용한다. 새 RoomVisits는 Node ID → RoomVisit로 선택/상품/피드백 상태를 보관한다. RoomRuntime은 규칙, FlowSession은 화면/Run 소유권, GameFlow는 Scene/Fade, RoomView/RunResultView는 표시를 담당한다. RunSummary는 결과 시점의 불변 값 스냅샷이다. Unity Scene reference를 도메인 상태에 넣지 않았다. 완전한 저장 시스템을 구현한 것은 아니다.

## 10. HP Persistence 검증

규칙 테스트에서 전투/상점/휴식/제단/다음 전투 HP 유지와 최대 HP 제한을 검사했다. 실제 마우스 Run에서 일반전 종료 HP 59/60이 상점/퍼즐/다음 엘리트전 시작까지 유지됐다. 첫 엘리트 종료 55/60, 두 번째 엘리트 종료 56/60이 각각 Map과 다음 방에 그대로 반영됐다. 보스 종료 53/60이 결과 화면에도 표시됐다.

## 11. Gold Persistence 검증

전투 +50 → 상점 −30 → 제단 −10 등 정확한 잔액과 부족 결제 차단, 중복 획득 차단을 자동 검사했다. Gold 음수/overflow 조건도 가능한 입력 경계에서 방지한다. 마우스 Run에서는 일반전 Gold +50 후 얼음 파편 구매로 50→20을 확인했고, 후속 모든 방과 Run Clear까지 20이 유지됐다. 두 번째 상점의 세 상품은 모두 골드 부족/30으로 잠겼다.

## 12. Deck Persistence 검증

전투 보상/상점 구매는 기존 RunState.AddCard를 사용한다. 다음 전투의 Hand+DrawPile에 구매한 Instance ID/Card ID가 포함되고 사용 가능한지 자동 검사했다. 마우스로 상점의 얼음 파편(덱 4→5), 첫 엘리트 보상 점화 연계(5→6), 두 번째 엘리트 보상 폭발 일격(6→7)을 획득했다. 모두 다음 전투에서 손패로 나타났고 실제 사용했다. 기존 중복 카드 허용 규칙은 그대로다.

## 13. Node State Persistence 검증

현재/완료/Available/Locked와 Boss 표시는 기존 PHASE 3.5 맵을 유지한다. 비전투 방은 선택만으로 완료되지 않고 명시적 맵 복귀 시 완료한다. Battle은 여전히 보상 확정 후 완료한다. 연결되지 않거나 완료한 방에 다시 들어갈 수 없다.

## 14. Run Clear

Boss Victory 후 일반 Reward 없이 RunResult로 간다. 캐릭터, 최종 HP, Gold, 덱 크기, 도달 구역/층, 완료 방 수를 표시한다. Boss 완료/결과 전환의 반복 호출은 거부한다.

## 15. Run Fail

Player HP 0이면 기존 BattleRuntime이 Failed를 판정하고, 입력 잠금/피격 표시 후 결과 화면으로 간다. Reward/Node Completion/추가 카드 사용은 금지한다. 함정 사망도 같은 결과 흐름이다.

## 16. New Run Reset

결과의 New Run은 Run/Room/Battle/PendingRoom/Summary/Deck overlay를 비우고 새 Seed로 Character Select에 간다. 캐릭터 선택 시 새 RunState를 생성한다. Title 복귀도 이전 Run과 PendingSeed를 비운다. Editor Capture Preview에서 새 게임으로 갈 때는 원본 Catalog 정의로 복원하는 기존 분리를 유지한다.

## 17. Resolution 검증

새 Shop/Rest/Altar/Puzzle/RunClear/RunFail 화면을 1280×720, 1600×900, 1920×1080, 2560×1440에서 캡처/버튼·카드·HUD 경계 검사해 모두 통과했다. 총 **24 PNG**다. Shop 720p, Rest 1080p, Altar 720p, Puzzle 900p, Clear 720p, Fail 1080p를 직접 열어 가독성과 겹침도 확인했다. 모든 캡처의 모든 픽셀을 수동 검토했다고 주장하지 않는다.

- [Shop 720p](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase6/screenshots/Shop_1280x720.png)
- [Run Clear 720p](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase6/screenshots/RunClear_1280x720.png)
- [Run Fail 1080p](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase6/screenshots/RunFail_1920x1080.png)

## 18. 실제 Mouse Full Run 검증

**통과.** Unity 6000.0.47f1의 실제 Game View(1920×1080, 16:9)를 네이티브 마우스로 조작했다. 사용자 직접 플레이와 입력이 겹친 이전 시도는 중단했고, 사용자의 검증 재개 지시 후 새 Run으로 처음부터 다시 검증했다.

- Title → 재현용 일반 Run 시작 → 사제 선택 → 랜덤 Map → r1 일반전(가고일 HP 38) → Gold +50 → r3 상점(얼음 파편 구매) → r8 퍼즐 통과 → r11 퍼즐 통과 → r14 엘리트(HP 60) → 점화 연계 보상 → r17 퍼즐 통과 → r20 상점(잔액 부족, 구매 없이 나가기) → r23 엘리트(HP 66) → 폭발 일격 보상 → boss(거신 HP 64) → Run Clear.
- Seed **101**. Editor QA 메뉴는 원본 랜덤 생성기의 재현용 Seed만 선택하고 자동 플레이/HP/Deck/Enemy 수치 변경은 하지 않는다. Capture Preview는 사용하지 않았다.
- 일반전 5턴, 엘리트 각각 6턴, 보스 5턴에 실제 카드로 승리했다. 보스 승리 후 일반 Reward 또는 Map 재진입 없이 결과 화면으로 이동했다.
- 최종 결과: **사제, 1구역 9층, HP 53/60, Gold 20, 덱 7장, 완료 방 9개**.
- 카드 클릭/사용 연출, 적 Hurt/Death, 턴 종료, Intent 갱신, Reward 카드 선택, Map 완료/Available 연결, Room 버튼 상태를 확인했다. 얼음 파편으로 엘리트 Intent 5×2→2×2, 보스 6×2→3×2가 즉시 바뀌었다. 보스의 약화된 3×2 공격에서 무방어 HP 58→52로 실제 피해 6이 일치했다.
- 비전투 방 두 종류(Shop/Puzzle)를 실제 Run에서 방문했다. Rest/Altar의 실제 uGUI 선택/결과는 별도 자동 Scene 통합 테스트로 검증했으며, 네이티브 마우스로 모든 방 종류를 방문했다고 주장하지 않는다.
- 자동 PlayMode fixture, 순수 모델 시뮬레이션 안내 로그, 이번 네이티브 마우스 Run은 서로 다른 검증이다.

## 19. Death Flow 검증

**통과.** Run Clear에서 New Run 클릭 → Character Select → 전사를 선택했다. 새 랜덤 맵에서 HP 60/60, Gold 0, 전사 시작 덱 4장, 입구 Current, 완료 경로 없음으로 초기화됐다. 첫 일반전에 들어가 카드/방어를 전혀 쓰지 않고 마우스로 End Turn만 눌렀다. 적의 6→8→방어 5 패턴을 반복해서 받았고 13번째 턴 종료 시 HP 4→0으로 사망했다.

Run Failed 결과는 **전사, 1구역 1층, HP 0/60, Gold 0, 덱 4장, 완료 방 0개**였다. 보상이나 방 완료는 발생하지 않았다. 결과의 Title 버튼으로 시작 화면 복귀도 확인했다. 별도 자동 테스트에서는 Fail 이후 New Run/Title 상태 제거와 사망 후 추가 입력 거절도 확인했다.

## 20. EditMode 결과

최종 재실행 **203/203 통과**, Failed/Skipped 0, exit code 0. 기존 193개 + 신규 RunIntegrationTests 10개를 유지한다. [EditMode-final.xml](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase6/EditMode-final.xml). 기존 웹 golden 규칙 비교와 랜덤 맵 불변식도 통과했다.

## 21. PlayMode 결과

최종 전체 재실행 **23/23 통과**, Failed/Skipped 0, exit code 0. [PlayMode-final.xml](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase6/PlayMode-final.xml). 기존 22개를 유지한다. 기존 Defeat 테스트의 검증 항목은 보존하고, PHASE 6의 RunResult 화면으로 바뀐 이동 기대만 갱신했다. 새 FullRunSceneTests는 실제 uGUI pointer handler, 모든 방, 보스 결과, New Run/Death, 4개 해상도를 검증한다. 테스트 전용 연결 맵 fixture는 생산 랜덤 맵이나 데이터 asset을 덮어쓰지 않는다. HP/공격력을 조작해 승리시키지 않고 실제 카드를 선택하는 테스트 전략으로 정상 전투/엘리트/보스를 이겼다. 이 자동 pointer 검증은 네이티브 마우스 검증과 별개다.

## 22. Compile Error / Warning

최종 EditMode/PlayMode 로그의 **C# Compile Error 0 / Warning 0**. 개발 중 Editor QA 분석기가 internal ActiveRoom setter에 접근한 컴파일 오류는, 실제 Enter/Complete API를 쓰는 방식으로 수정했다. 첫 PlayMode 시도는 컴파일 백엔드 정체로 이번 검증 프로세스만 종료/재시작했다. 이후 전체 실행에서 기존 22개는 통과했지만 신규 통합 테스트가 사망 연출 중 EndTurn을 다시 클릭하려 해 실패했다. 테스트를 약화하거나 게임 잠금을 해제하지 않고, PlayerTurn에서만 반복 클릭하도록 테스트 입력 루프를 수정한 후 전체 23개 재실행이 통과했다. 캐시/Package/팀원 파일은 삭제하지 않았다.

## 23. Console Error / Warning

마우스 Run Clear/Fail/Title 검증 직후 Unity Console을 열어 **Error 0 / Warning 0**을 확인했다(Clear 버튼으로 로그를 지우지 않음). C# 예외/Missing Script/Serialization 오류는 해당 플레이와 최종 로그에서 발견되지 않았다. `MousePlay.log`에는 시작 시 LicensingClient signature Code 10/Access token, EditorUpdateCheck HTTP 404 진단이 있다. 이것은 게임 Console 0건과 구분하며, 라이선스/계정/Package 설정을 임의 변경하지 않았다. 검증 후 Play Mode를 중지하고 검증용 Editor를 정상 종료했다.

## 24. 변경된 주요 파일

기존 C# **11개**: RunState.cs, DungeonRuntime.cs, EnemyInstance.cs, RewardRuntime.cs, AssemblyInfo.cs, FlowSession.cs, BattleContext.cs, GameFlow.cs, GameScreenView.cs, BattleController.cs, BattleSceneTests.cs. AssemblyInfo는 PlayMode 테스트 assembly의 도메인 fixture 접근만 허용한다. 시작 baseline 대비 나머지 원본 BRODUN 파일 442개는 동일하며 기존 파일 누락은 없다.

## 25. 새로 생성한 주요 파일

RoomRuntime.cs, EncounterPool.cs, RunSummary.cs, RoomPresentationAssets.cs, RoomView.cs, RunResultView.cs, Phase6ProjectBuilder.cs, Phase6Qa.cs, RunIntegrationTests.cs, FullRunSceneTests.cs 및 Unity .meta. 기존 이벤트 배경 3개 사본/메타, BrodunRooms.asset, 이 보고서. 기존 Scene/Prefab/카드·적 데이터 asset은 재생성하지 않았다.

## 26. 기존 Game Rule 변경 여부

카드 수치/캐릭터 HP/Energy/전투 피해·방어·상태·드로우/Intent 실행 순서를 변경하지 않았다. 이번에 허용된 진행 기능인 Encounter 선택, 비전투 방 효과, 보스 Reward 우회, Run 결과/Reset을 추가했다. 기존 web-v1 규칙 fixture는 호환성을 유지한다. 최종 SHA-256 비교에서 시작 시 원본 BRODUN 453개 중 의도한 C# 11개만 달라졌고 나머지 **442개 동일 / 누락 0개**였다. 기존 Scene/Prefab/카드·캐릭터·적 데이터 asset/이미지/.meta를 보존했다. manifest, packages-lock, EditorBuildSettings, ProjectSettings도 PHASE 6 시작 baseline과 동일하다.

## 27. Unity Version

**6000.0.47f1 (2ad1ed33fd3b)** 유지. Package 추가/업데이트하지 않았다.

## 28. Git Branch / Status

**Paul**. stage/commit/push/merge/PR/force push하지 않았다. 로컬 HEAD와 원격 Paul은 모두 `eedba21b4552200874388bdb9f637850b14b3af0`, 원격 main은 작업 전과 같은 `6f0111cbd683c3155c6e6657434e496cac8f9d0c`다. staged 변경 0, `git diff --check` 오류 0이다(LF→CRLF 안내만 있음).

이전 단계의 HubForceResolve/Packages/ProjectSettings 변경과 미추적 BRODUN 전체/Tools/보고서를 그대로 보존했다. Git에서 BRODUN 전체가 이전부터 미추적이므로, 이번 파일 구분은 PHASE 6 시작 baseline과 비교했다. 이번 추가는 BRODUN 파일 29개(메타 포함)와 이 보고서 1개이며 기존 파일 삭제는 0개다. Editor가 자동 생성한 미추적 `ProjectSettings/SceneTemplateSettings.json`만 정상 종료 후 제거하여 이번 환경 변경에서 제외했다. 이 자동 생성 파일은 Editor가 다시 만들 수 있다. Unity 버전/Package/팀원 파일은 변경하지 않았다. **main 브랜치는 수정하지 않았습니다.**

## [POST-PHASE 6 ISSUES]

- Area 2/3 데이터 풀은 준비돼 있으나 전체 구역 이동/그래픽/콘텐츠 연결은 범위 밖이다.
- 일반 2종의 기존 전투 수치/패턴은 동일하다. 이번에는 강제로 차별 밸런싱하지 않았다.
- 정식 퍼즐/상점 유물·카드 제거·강화/제단 확장은 미구현이다.
- 휴식은 웹의 8 HP를 유지하며, 상점 가격/제단 비용/함정은 새 최소 구현값으로 최종 밸런스가 아니다.
- 해상도 검증 중 제단의 유료 회복은 HP가 가득 찬 상태에서도 선택 가능한 것을 확인했다. 현재는 최대 HP까지만 회복하고 Gold는 정상 지불하지만, 향후 가득 찬 HP에서는 비활성화/확인 안내가 더 적절하다. 진행 불가 오류는 아니므로 이번에 규칙을 추가 변경하지 않았다.
- Save/Load/Continue/최종 Audio/설정/최종 QA는 구현하지 않았다.
- 실제 마우스 클리어/사망 Run에서 진행 불가, 버튼 미작동, 보상 중복 또는 상태 초기화 오류는 발견하지 않았다. 다만 이번 Seed 경로의 퍼즐은 같은 최소 선택 UI가 반복된다. 콘텐츠 다양화는 이후 작업으로 남긴다.

PHASE 6 검증·보고를 마쳤다. 다음 PHASE는 시작하지 않고 여기서 멈춘다. commit/push하지 않았다.
