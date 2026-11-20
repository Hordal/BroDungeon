# [PHASE 5 COMPLETE]

검증일: 2026-10-14 (Asia/Seoul). 자동 검사와 실제 Mouse 검사의 증거 수준을 구분한다.

## 1. 구현한 기능

기존 PHASE 4의 전투/보상/맵 복귀 루프 위에 정보 패널, 손패 페이지, 카드 타입 색상/가독성, 사용 Fade, Hit별 숫자, 턴/Victory 배너, 보상 Fade와 획득 확인 줄을 추가했다. 대형 신규 게임 시스템/새 게임 데이터는 만들지 않았다.

## 2. 개선한 Battle UI

플레이어 상태/적 상태/행동력/하단 Hand를 어두운 금속 테두리 패널로 통일했다. 배경은 기존 1구역 이미지를 유지하고 veil만 약간 밝게 했다. 새 PNG는 생성하지 않았다. HP 숫자는 즉시 읽고 Bar만 짧게 따라가는 기존 방식을 유지했다.

## 3. Card UI / Hover / 사용 연출

공통 CardView Prefab을 유지한다. 이름 25, 설명 최대 20 기준 px, 공격/방어/기술 색상을 구분한다. 고정 Hit Root와 별도 Visual Hover(+42px, 1.10배)를 유지하며 중복 PointerEnter를 무시한다. 사용 중에는 CardView가 Visual 소유권을 가져 Hover Update가 사용 연출을 덮어쓰지 않는다. 0.24초 이동/축소/Fade 후 기존 Discard/Exhaust 처리 결과를 보여준다.

손패 1~5장은 기존 크기를 유지하고 6장은 0.82 scale로 배열한다. 7장 이상은 6장씩 페이지를 나눠 ◀/▶로 선택한다. 카드 수 때문에 텍스트를 계속 축소하거나 카드를 화면 밖에 방치하지 않는다. 페이지 버튼은 카드 아래 안전 여백에 둔다. 여러 장 Draw 검증은 테스트 fixture일 뿐 일반 Draw 규칙은 그대로다.

## 4. Damage / Hit Feedback

떠오르는 숫자에 검은 Outline, 짧은 크기 변화/상승/Fade와 자동 제거를 적용했다. 기존 DamageCalculator가 반환한 DamageResult를 CardResult/EnemyActionResult의 읽기 전용 Hit 기록에 담는다. UI는 합계를 Hit 수로 나누거나 피해를 다시 계산하지 않는다. 방어 흡수/HP 피해를 Hit별로 0.13~0.16초 간격 표시한다. 기존 Player portrait Flash/미세 Shake와 Enemy Hurt tint를 사용하며 카메라 전체 Shake는 없다.

## 5. Enemy Animation 적용 내용

기존 가고일/파수꾼/거신/슬라임 Sprite 참조와 프레임/pivot/FPS를 유지했다. Idle → Attack/Hurt → Idle, Death 마지막 프레임 유지도 그대로다. 비공격 Intent(Block/Buff)에는 공격 Animation을 잘못 재생하지 않고 짧은 행동 대기를 표시한다. Walk/키보드 이동/새 Placeholder 몬스터는 추가하지 않았다.

## 6. Intent UI 개선

적 근처 프레임과 큰 숫자/색상으로 공격(주황), 방어(청록), 버프(금색)를 구분한다. 이름/HP/상태 패널과 Tooltip 배경을 정리했다. 실제 Runtime.CurrentIntent와 기존 ExpectedHPDamage를 읽으며, 현재 예고를 실행한 뒤 다음 Intent를 보여주는 규칙은 변경하지 않았다.

## 7. Turn / Victory 연출

짧은 PLAYER TURN / ENEMY TURN / VICTORY 배너를 추가했다. Enemy Turn에는 이전 손패를 숨기고 입력을 잠근 상태에서 실제 Attack/Hit 결과를 보여준 뒤 다음 Hand를 표시한다. HP/Energy Runtime은 기존 동기 처리이며 연출이 계산을 지연하지 않는다. Death 완료 → Victory 0.75초 → Reward다. 배너만 새 안내가 이전 배너를 취소하며 게임 상태를 건드리지 않는다.

## 8. Reward UI 개선

전투와 같은 어두운 금속 테두리, 후보 카드 1.08배, 카드와 골드 중 하나만 선택하는 기존 흐름을 유지했다. Gold/Skip 위치를 분리하고 획득 피드백을 후보 카드 아래 별도 확인 줄에 표시한다. 0.18초 Overlay Fade 후 기존 UI를 보여주며 Reward 확정 상태/중복 방지/덱 추가/골드 지급/Skip 규칙을 유지한다.

## 9. Scene Transition 개선

기존 GameFlow Fade Out 0.22초 → Scene Load → Fade In 0.22초와 전환 입력 가드를 재사용한다. GameFlow 코드를 이번에 변경하지 않았다. Reward Overlay에만 짧은 Fade를 추가했다. Node 완료 시점은 여전히 보상 확정 후이며 동일 RunState로 Map에 복귀한다.

## 10. Resolution 검증 결과

1920×1080 / 1600×900 / 1280×720 / 추가 2560×1440에서 실제 Game View 캡처와 카드/Intent/Energy/EndTurn 경계를 검사해 모두 통과했다. 각 해상도 BattleStart/CardHover/EnemyIntent/EnemyHurt/EnemyTurn/VictoryCardReward/GoldReward/DungeonReturn 총 **32 PNG**를 저장했다. 1920 전투, 1280 보상, Tooltip/Gold 캡처를 직접 열어 레이어/글자/배치를 확인했다. 캡처 이후 마지막 Tooltip 위치만 중앙 하단으로 미세 조정했으며, 최종 위치는 아래 실제 Mouse Play에서 다시 컴파일해 확인했다. 따라서 저장된 Intent PNG 일부와 최종 Tooltip 위치는 다르다. 모바일/초광폭은 범위 밖이다.

- [1920 전투](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase5/final_screenshots/BattleStart_1920x1080.png)
- [1280 보상](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase5/final_screenshots/VictoryCardReward_1280x720.png)
- [Intent](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase5/final_screenshots/EnemyIntent_1920x1080.png)
- [Gold](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase5/final_screenshots/GoldReward_1920x1080.png)

## 11. 실제 Mouse Play 검증 결과

computer-use 스킬로 Unity 6000.0.47f1 Editor의 실제 마우스 입력을 사용했다. Capture/QA 메뉴가 아닌 일반 GAME START로 시작했고, 데이터나 HP를 조작하지 않았다. 중단 전 부분 검증과 별개로 마지막 실행에서 다음 루프를 처음부터 끝까지 완료했다.

- Title → 전사 선택 → 랜덤 Dungeon Map → 하단 첫 일반 전투 노드 → 폐허 가고일(38 HP).
- 카드 Hover 확대/복귀, 지반 강타(5), 돌풍 베기(4), 바위 방벽(5) 클릭과 사용 Fade/피해 숫자/Hurt를 확인했다. 행동력은 즉시 3→2→1→0, 다음 턴 3으로 회복했다.
- 공격 Intent 6에 방어 5를 사용하니 HP 60→59, 다음 공격 Intent 8에는 HP 59→56이었다. 적의 방어 Intent 5 실행 후 다음 공격 5는 적 방어에 흡수되고 HP는 그대로였다. 비공격 행동 뒤 다음 공격 Intent도 갱신됐다.
- Enemy Turn 동안 손패가 숨겨지고 입력이 잠겼다가 Player Turn에 다시 표시됐다. 최종 Intent Tooltip은 두 상태 패널 사이에 표시되어 Gold/메시지를 가리지 않았다.
- 5턴째 적 HP 0, Death 프레임과 전투 종료 후 Reward 표시를 확인했다. 보상 후보 Hover 후 **열화 폭격**을 선택했고 획득 확인 문구 후 Fade로 Map에 복귀했다.
- 복귀 Map에서 **HP 55/60, Gold 0, 덱 4→5**를 확인했다. 덱 목록에 열화 폭격(비용 2/불)이 실제 추가됐다. 완료 노드의 체크, 현재 위치, 연결된 다음 Available 노드 2개와 잠긴 나머지 경로가 정상 표시됐다.
- 네이티브 마우스 Elite 전투는 이번 최종 실행에서는 하지 않았다. Normal→Normal→Elite 및 Gold/Skip 경로는 PlayMode 자동 검증 결과이며 실제 마우스 결과로 주장하지 않는다.

로그: [MousePlay-final.log](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase5/MousePlay-final.log). 검증 완료 후 Play Mode를 종료하고 이번 검증용 Editor를 닫았다.

## 12. EditMode 결과

기존 Capture 단계의 190개 + 신규 Hit trace 3개, 최종 소스 **193/193 통과**, Failed/Skip 0, exit code 0. [XML](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase5/EditMode-verified.xml). 기존 143개 웹 규칙 비교, 랜덤 맵/Context/Reward 검사도 보존됐다.

## 13. PlayMode 결과

기존 20개를 삭제하지 않았다. Hover idempotency/사용 Visual 소유권 검사, 16장 손패 페이지 접근 검사를 추가했다. 기존 해상도 검사를 네 번째 해상도로 확장했고 캡처 경로를 환경 변수로 분리했다. 최종 **22/22 통과**, Failed/Skip 0, exit code 0. [XML](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase5/PlayMode-verified.xml). Normal→Normal→Elite 연속 승리/Card·Gold·Skip/Defeat/중복 지급/Run 유지 등 기존 검사도 통과했다.

## 14. Compile Error / Warning

개발 중 BattleBanner의 animation 이름이 Unity Component.animation과 겹친 경고는 bannerRoutine으로 수정했다. 최종 C# Compile Error **0 / Warning 0**. EditMode 재확인 한 번은 Unity 컴파일 백엔드가 정체돼 이번 검증 프로세스만 종료 후 재실행했고 193/193 통과했다. 한 번은 GUI Editor가 완전히 종료되기 전에 검증 실행을 시작해 `HandleProjectAlreadyOpenInAnotherInstance` 시작 실패가 났다. 이전 프로세스 종료를 확인한 후 재실행했고 22/22 통과했다. 프로젝트/캐시를 삭제하거나 Package를 바꿔 우회하지 않았다. 실패/정체 로그와 최종 통과 결과를 구분한다.

## 15. Console Error / Warning

최종 실제 Mouse Play 후 Console 창을 열어 **Log 0 / Warning 0 / Error 0**을 확인했다. Console Clear로 지우지 않았다. 최종 실행 로그에도 C# 오류/경고, NullReferenceException/MissingReferenceException 등 게임 예외가 없었다.

단, Editor 시작 로그에는 기존 Licensing Client 서명 Code 10/캐시 토큰 및 ULF 갱신 실패 진단, EditorUpdateCheck URL 404가 있었다. 이후 entitlement가 정상 확인되어 Editor와 Play는 실행됐다. 앞선 별도 실행의 CancellationTokenSource Dispose 내부 진단도 최종 게임 Console 오류와 구분한다. Unity Hub/라이선스/업데이트 서비스 진단까지 모두 0이라고 주장하지 않는다.

## 16. 변경된 주요 파일

`Assets/BRODUN/` 아래:

- `Scripts/Presentation/BattleUi.cs`: 공통 패널 primitive.
- `Scripts/Presentation/BattleView.cs`: 패널/HUD/손패/배너/보상 표시 연결.
- `Scripts/Presentation/BattleController.cs`: 기존 Runtime 결과를 Hit별 연출, 사용/턴/Victory 순서에 전달.
- `Scripts/Presentation/CardView.cs`: Bind 스타일, Hover 입력 가드, 사용 Visual 소유권.
- `Scripts/Presentation/EnemyView.cs`: 이름/HP/Intent/Tooltip 패널과 Hurt tint.
- `Scripts/Rules/CardEffectResolver.cs`: 계산 결과 Hit trace만 기록.
- `Scripts/Rules/EnemyInstance.cs`: 실행 결과 Hit trace만 기록.
- `Tests/PlayMode/BattleSceneTests.cs`: 기존 검사 보존 + 2개 신규 검사/2560×1440/캡처 경로.

## 17. 새로 생성한 주요 파일

`Assets/BRODUN/` 아래 C# 및 각각 Unity `.meta`:

- `Scripts/Presentation/HandPresentation.cs` — 페이지/배치/입력 표시.
- `Scripts/Presentation/BattleBanner.cs` — 순수 연출 배너.
- `Scripts/Presentation/DamageNumber.cs` — 순수 숫자 연출.
- `Tests/Editor/PresentationTraceTests.cs` — 카드/적 다중 Hit 결과와 비공격 trace 검사.

추가 문서: 이 파일 `UNITY_PHASE5_REPORT.md`. Scene/Prefab/SO/PNG/Animation asset/Package는 새로 생성하거나 재생성하지 않았다.

## 18. 기존 Game Rule 변경 여부

카드/캐릭터/적 데이터, 피해/방어/상태 계산, Draw/Energy, Intent/Seed/보상/RunState/던전 완료 규칙은 변경하지 않았다. Rules 계층의 두 파일에는 이미 계산된 DamageResult 기록만 추가했다. 룰 변경과 연출 trace 추가를 구분한다. 기존 Capture 메뉴와 촬영용 데이터 분리도 유지한다.

## 19. Unity Version

**6000.0.47f1 (2ad1ed33fd3b)** 고정. Editor/ProjectVersion/Package 버전을 임의 변경하지 않았다.

## 20. Git Branch / Status

**Paul**, `https://github.com/Hordal/BroDungeon.git`. 시작과 종료 HEAD/원격 Paul 모두 `eedba21b4552200874388bdb9f637850b14b3af0`, 원격 main `6f0111cbd683c3155c6e6657434e496cac8f9d0c`로 동일하다. stage/commit/push/merge/PR/force push를 하지 않았다. main 브랜치는 수정하지 않았다.

PHASE 5 시작 시 이미 존재하던 미커밋/미추적 작업을 보존했다. 시작 복사본 대비 BRODUN 기존 파일 437개가 동일하고 수정은 16번의 8개 C#뿐이다. 누락된 기존 파일은 없다. 신규 C# 4개와 각 .meta, 이 보고서만 추가했다. 기존 PNG/데이터 asset/Scene/Prefab/.meta 379개도 변경하지 않았다. Packages manifest/lock, ProjectSettings.asset, EditorBuildSettings.asset 해시가 시작 복사본과 동일하다. Editor가 이번 검증 중 자동 생성한 SceneTemplateSettings.json만 종료 후 제거했다(기존 파일 아님; 필요 시 Unity가 재생성 가능). 기존 .vsconfig/PackageManagerSettings와 이전 단계 수정은 그대로다.

## 범위 밖 / 다음 단계

최종 전용 카드 일러스트/Audio/VFX/밸런스, 3 Area 완주, 보스 전용 Phase, Save/Continue, Shop/Rest/Altar/Puzzle 전체, 모바일/초광폭/전체 최종 QA는 미완성이다. 현재 카드 Artwork는 기존 아이콘 placeholder이고 Player는 정지 portrait 기반이다. 이번 단계의 대형 손패 대응은 fan/scroll이 아닌 페이지 방식이다.

PHASE 6은 시작하지 않았다. 여기서 작업을 멈추고 사용자 지시를 기다린다.
