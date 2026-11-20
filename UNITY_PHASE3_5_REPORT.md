# BRODUN PHASE 3.5 — Dungeon Map 재설계 및 UI 수정

검증일: 2026-10-13. Unity **6000.0.47f1 (2ad1ed33fd3b)**, Git **Paul**.

프로젝트: `C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul`

이번 작업은 PHASE 3의 화면 흐름을 유지하면서 맵 생성과 UI만 확장했다. Battle/보상/이벤트 콘텐츠 구현, Scene/Prefab 추가, Package 변경, commit/push는 하지 않았다.

## 1. 여정 종료 Hover 버그 원인

기존 공통 `StyledButton.DoStateTransition`은 상태 Sprite가 아직 배정되지 않아도 `image.sprite`에 null을 대입했다. `AddComponent`의 OnEnable이 Configure보다 먼저 실행되는 초기화 순서에서 정상 이미지가 지워질 수 있었다. 상태 변경 시 유효 Sprite 보장도 없었고 Hover/Selected에는 높은 밝기 tint가 적용됐다.

이는 코드에서 확인하고 초기화 회귀 테스트로 검증한 결함이다. **수정 전 사용자가 보았던 Hover 화면 자체를 이번 실행에서 재현하지는 않았으므로, 그 현상의 단일 원인으로 확정하지는 않는다.** 수정 후 실제 Editor 마우스 Hover에서는 맵 버튼과 Modal 버튼 모두 흰 사각형으로 바뀌지 않았다.

## 2. 적용한 수정

- 공통 버튼의 Normal/Hover/Pressed/Disabled Sprite를 `Configure`에서 한 번에 배정하고 즉시 스타일을 갱신한다.
- 초기화 도중 상태 Sprite가 없으면 기존 유효 이미지를 지우지 않는다. 상태 이미지 누락 시 Normal을 대체 이미지로 사용한다.
- Sprite override를 정리하고 Hover/Selected는 약한 tint 및 테두리 강조로 제한한다.
- `여정 종료`에는 별도 갈색/주황색 경고 계열 tint를 적용한다. 텍스트는 기존 금빛 대비를 유지한다.
- `[계속하기] [여정 종료]` 확인 Modal, 기본 계속하기 포커스, Modal 외부 입력 차단과 중복 전환 방지를 유지했다.

## 3. HUD 표기 수정

`구역 1 / 층 1` 대신 **`1구역 1층`** 형태를 사용한다. Character/HP/Gold/Deck는 기존 RunState 값을 그대로 표시한다. 세 16:9 해상도에서 HUD와 고정 버튼이 화면 안에 배치되는지 검사했다.

## 4. 기존 Dungeon Map 구조의 문제점

기존 11노드 고정 연결 구조는 Room 종류가 변해도 진행 형태가 동일하고, 미래 경로를 비교할 공간과 진행 길이가 부족했다. 기존 웹 규칙 재현용 생성기는 호환성 때문에 보존하되, **실제 GameFlow의 새 Run에는 새 생성기를 사용**하도록 연결했다.

## 5. 새 Random Graph 생성 방식

`LayeredMapGenerator.Generate`는 다음 두 단계를 분리한다.

1. `GenerateGraph`: Layer 수·폭, 노드, 방향 간선, 별도 정규화 위치 정보를 생성한다.
2. `RoomAssignment.Assign`: 완성된 그래프를 변경하지 않고 방 종류와 조우 종류를 배정한다.

Layer 폭과 분기/합류 간선이 Seed에 따라 달라진다. 단순히 방 아이콘만 무작위로 바꾸지 않는다. 위치의 작은 Y 편차도 별도 결정적 RNG 스트림으로 생성한다.

## 6. Layer 수

기본 설정은 중간 **8~10 Layer**다. Start Layer 0과 최종 Boss Layer를 별도로 둔다. 첫 선택 Layer와 마지막 중간 Layer는 2노드로 제한해 시작 분기와 최종 수렴을 명확히 한다. 설정 검증 범위는 중간 7~12 Layer다.

## 7. Node 수 범위

중간 Layer당 2~4노드, 기본 설정의 전체 이론 범위는 **18~38노드**다. Start와 Boss는 각각 1개다. Start에서 Boss까지 9~11개의 방을 거치며, 몇 번의 클릭만으로 바로 Boss에 도착하지 않는다.

## 8. 연결 규칙

모든 간선은 다음 Layer로만 향한다. Boss를 제외한 모든 노드에는 1~2개 출구가 있다. 먼저 모든 다음 노드의 입구와 현재 노드의 출구를 보장한 후, Lane 순서를 역전하지 않는 추가 분기/합류를 Seed 기반으로 선택한다. 중복 간선과 역방향 간선을 만들지 않는다.

## 9. Boss 수렴 보장 방식

마지막 중간 Layer의 2노드는 모두 단일 Boss로 연결한다. 이전 Layer의 모든 노드에 다음 Layer 출구가 있으므로, Layer 역순으로 보면 모든 노드가 결국 Boss에 도달한다. 테스트에서는 각 노드부터 탐색해 Boss 도달 여부도 직접 검사했다.

## 10. Dead End 방지 방식

모든 다음 Layer 노드를 기존 Layer에서 연결하고, 아직 출구가 없는 노드에는 추가 출구를 연결한다. 따라서 Start에서 도달할 수 없는 고립 노드와 Boss 이전 막다른 길이 없다. 1,024개 Seed에서 전체 도달성, 입구/출구 수, 역방향 연결 부재를 검사했다.

## 11. RoomType 배치 규칙

- 첫 Layer는 일반 전투다.
- 중간 Layer마다 적어도 절반(올림)은 전투다.
- Layer 2에 접근 가능한 상점 기회를 최소 1개 둔다. **모든 선택 경로에 상점이 보장되는 것은 아니다.**
- 첫 두 Layer에는 Elite를 배치하지 않으며, 연결된 연속 Elite를 금지한다.
- 연결된 같은 이벤트(상점/제단/휴식처)의 연속 배치를 피한다.
- Boss 직전은 전투 2개이며 그중 1개는 Elite다. 해당 Elite의 직전 연결 노드가 Elite라면 일반으로 조정한다.
- Shop/Altar/RestSite는 기존 `RoomType.Event + EventKind` 구조를 사용한다. 새 타입이나 실제 이벤트 플레이를 만들지 않았다.

## 12. RoomType 확률 설정 위치

[MapGenerationConfig.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Rules/MapGenerationConfig.cs)의 기본 가중치:

| 일반 전투 | Elite 전투 | Puzzle | Event |
|---:|---:|---:|---:|
| 52 | 12 | 16 | 20 |

Event 세부 후보는 상점/제단/휴식처를 동일 비중으로 뽑는다. 위 값은 **배치 제약을 적용하기 전 추첨 가중치**이며, 최종 맵의 정확한 비율을 뜻하지 않는다. 최소 전투 비율·상점·마지막 Elite 보정이 추가된다. 최종 밸런싱 값은 아니다.

## 13. Seed 재현 방식

기존 PHASE 2의 `SeededRandom`을 사용한다. Graph는 Run Seed, 위치와 Room 배치는 각각 고정 offset을 더한 별도 스트림을 사용한다. `UnityEngine.Random` 전역 상태는 사용하지 않는다.

동일 Seed의 노드 수·위치·연결·RoomType·EnemyType·EventKind가 같은지 검증했다. 32개 서로 다른 Seed의 연결 시그니처 다양성도 검사했다. `GameFlow.StartNewRun`의 선택적 Seed 인자는 재현 테스트용이며 일반 플레이에 Seed 입력 UI를 추가하지 않았다.

## 14. Dungeon Runtime 변경 내용

- `GraphNode`: Id, Layer, Lane, Next, Previous를 표현한다.
- `DungeonGraph.Placements`: 논리 그래프와 분리된 정규화 좌표다.
- `RoomNode`: 기존 필드를 유지하고 Previous, Layer, 새 위치/층/조우 입력을 확장했다.
- `RunState`: 생성 결과와 `MapGeneratorVersion`을 보유한다. 새 production Run은 `layered-v2`다.
- `FlowSession`: 맵 생성 함수를 주입받는다. UI에서 맵을 다시 생성하지 않는다.
- Current/Completed/Available/Locked는 기존 RunState와 연결 정보를 기준으로 `NodeState`가 계산한다. 별도 View 플래그를 중복 저장하지 않는다.

기존 `web-v1` 기본 생성 경로는 PHASE 2 웹 비교 fixture와 이전 호출 호환성을 위해 남겼다. 실제 GameFlow 테스트는 새 production 그래프가 선택되는지 별도로 검사한다.

## 15. Map UI 변경 내용

기존 배경과 방 아이콘/상태 에셋을 유지했다. 노드 간 가로 간격은 기준 Canvas 310px, 노드는 128×100px다. 배경 위에 어두운 Viewport를 두고 노드와 연결선의 대비를 높였다.

방 이름과 상태 표시의 텍스트 높이를 한국어 폰트에 맞춰 보완했다. 첫 캡처에서 발견한 글자 잘림과 연결선 원점 불일치를 수정했으며, 텍스트 적정 높이와 선 mesh 범위 검사를 추가했다.

## 16. Scroll / Drag 구현 방식

`DungeonMapViewport → MapContent → Nodes + ConnectionLines` 구조다. `RectMask2D`가 Viewport 밖 콘텐츠를 자른다. `DungeonMapScroller`는 기존 uGUI `ScrollRect`를 확장하며 수평 드래그, 경계 제한, 관성 이동과 휠 수평 탐색을 제공한다.

콘텐츠 폭은 `max(ViewportWidth, 320 + BossLayer × 310)`으로 계산한다. 노드가 화면 밖에 있는 것은 정상이며 HUD/고정 버튼은 스크롤하지 않는다.

## 17. 현재 위치 Auto Focus 방식

맵 최초 표시 시 현재 노드가 보이도록 이동한다. Current가 변경되었을 때만 약 0.28초 동안 부드럽게 포커스한다. 사용자가 드래그/휠을 사용하면 자동 포커스를 중단한다. 매 프레임 중심으로 강제 복귀하지 않는다. `[현재 위치]` 버튼으로 언제든 복귀할 수 있다.

## 18. Node 상태 표시 방식

| 상태 | 표시/동작 |
|---|---|
| Current | 선택 프레임, 금빛 외곽, `현재 위치` |
| Available | 밝은 프레임/청록 외곽, `◆ 이동 가능`, 클릭 가능 |
| Completed | 완료 아이콘, `✓ 완료` |
| Locked | 어두운 프레임과 아이콘, 클릭 불가 |
| Boss | 최종 노드 전용 아이콘, 미리 탐색 가능하나 조건 전에는 클릭 불가 |

Current와 Completed가 동시에 해당하면 Current 표시를 우선하고, 다음 방에 이동한 뒤 이전 방에 완료 표시를 보여준다. 방 선택은 Pending Room만 저장한다. 미구현 전투 안내를 닫아도 완료되거나 다음 방이 열리지 않는다.

## 19. Connection Line 개선 내용

논리 데이터의 Next 간선을 그대로 그린다. 단순 장식용 연결은 없다. Lane 순서를 유지하므로 인접 Layer 사이 독립 간선의 교차가 생기지 않는다. Layer를 건너뛰는 긴 간선과 다른 노드를 관통하는 연결을 만들지 않는다.

이동 가능 경로는 금빛, 지나온 경로는 청록색, 미래/잠긴 경로는 회색이다. 강조 선은 더 두껍게 그린다. 선 양끝은 노드 가장자리 앞에서 마감한다. 중앙 pivot의 Graphic 좌표와 왼쪽 pivot의 MapContent 좌표를 올바르게 변환했다.

## 20. 기존 테스트 결과

기존 **EditMode 153개**와 **PlayMode 6개**를 보존했다. 테스트를 삭제하거나 Ignore하지 않았다.

기존 해상도 캡처 테스트의 공간 검사만 변경했다. 길어진 맵의 노드에는 화면 경계 대신 MapContent 내부 배치를 검사하고, HUD/고정 버튼의 화면 내부 검사는 그대로 유지한다. 화면 밖 노드를 억지로 화면 안에 넣는 방식으로 테스트를 통과시키지 않았다.

## 21. 신규 테스트 결과

EditMode 15개를 추가해 **168/168 통과**했다. 4개 테스트에서 Seed 0~1023 전체를 검사한다. 동일 Seed, 다른 Seed, 방 생성 제약, 도달성, 연결 제한, UI 폭, 상태 전환, 이전 생성기 호환성이 포함된다.

PlayMode 6개를 추가해 최종 전체 **12/12 통과**했다. 맵 유지, ScrollRect 드래그/포커스, 버튼 상태, Modal 입력, 해상도 캡처, 실제 Input System UI 경유 드래그 검사다. 후자는 가상 마우스를 프레임별로 입력하고 배치 테스트 중에만 포커스 정책을 보완한 뒤 finally에서 원래 설정으로 복구한다. 제품의 입력 설정을 변경하지 않는다.

최종 전체 PlayMode 결과는 아래 증거 파일을 기준으로 한다.

- [EditMode 결과](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/EditMode-final.xml)
- [PlayMode 전체 결과](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/PlayMode-final-all.xml)
- [Input System 드래그 단독 결과](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/Drag-input.xml)

## 22. Compile Error / Warning

최종 컴파일 검사에서는 BRODUN C# 오류/경고가 없었다. 실제 interactive Editor Console도 Log/Warning/Error **0/0/0**이었다. Missing Script/Serialization 오류는 관찰되지 않았다.

Editor 시작 로그에는 기존 Licensing Client 서명 검증 Code 10, access token 관련 진단이 있다. 이후 entitlement 해석과 Editor 로드가 성공했다. 이를 게임 Console 오류 0개와 혼동하지 않는다.

검증 과정에서는 첫 PlayMode 실행의 IL 후처리 대기를 중단하고 재실행했다. 추가 QA 코드의 GetMesh API 호출 오류 1건은 수정했다. 이전 실패/진단 기록은 검증 폴더에 보존하고 최종 XML과 구분했다.

## 23. PlayMode 검증 결과

computer-use 스킬로 Unity 6000.0.47f1의 실제 Editor를 열고 마우스로 다음을 확인했다.

- Boot → Title → CharacterSelect → DungeonMap.
- 여정 종료 버튼과 확인 Modal의 실제 Mouse Hover: 흰색 면으로 변하지 않음.
- Modal의 계속하기: Run 유지.
- 휠 탐색으로 미래 경로/Boss 확인, Locked Boss 클릭 차단.
- 현재 위치 버튼으로 시작 위치 복귀.
- 현재 덱 보기/닫기 및 4장 시작 덱 표시.
- Available 전투방 선택 → Current/Pending Room/미구현 안내, 클릭만으로 완료되지 않음.
- `1구역 1층` HUD와 Console 0/0/0.

**검증 한계:** native 자동 마우스 도구의 drag 호출에서는 화면 이동을 확인하지 못했다. 이를 실제 손 드래그 통과로 기록하지 않았다. ScrollRect 직접 이벤트와 Input System → UI raycast → ScrollRect를 경유한 프레임별 드래그 테스트는 통과했다. 사용자의 실제 마우스로 길게 누르고 좌우로 끄는 최종 체감 검사는 남아 있다.

## 24. 해상도 검증 결과

1920×1080, 1600×900, 1280×720에서 Seed 17/29의 Start/Middle/Boss/LeaveHover/LeaveModalHover를 실제 Unity Game View로 캡처했다. Screen 크기 일치, 고정 버튼의 화면 내부 배치, 버튼 Sprite, 방 이름의 글자 영역, 연결 mesh의 콘텐츠 범위를 검사했다.

대표 Start/Middle/Boss/Modal 캡처를 열어 시각적으로도 확인했다. 가로 콘텐츠는 정상적으로 잘리며 고정 HUD와 하단 버튼은 화면 안에 남는다. 다른 화면비/초광폭/모바일 터치는 이번 검증 범위가 아니다.

## 25. 생성한 스크린샷

새 맵 캡처 **30장**: 2 Seed × 3 해상도 × 5 상태. 별도로 기존 화면 회귀 캡처도 보존한다. 이미지와 테스트 로그는 Git 저장소 밖 `work/phase35`에 둔다.

- [Seed A 시작](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/screenshots/Start_Seed17_1920x1080.png)
- [Seed B 시작](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/screenshots/Start_Seed29_1920x1080.png)
- [중간 경로](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/screenshots/Middle_Seed17_1600x900.png)
- [Boss 위치](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/screenshots/Boss_Seed29_1280x720.png)
- [여정 종료 Hover](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/screenshots/LeaveHover_Seed17_1920x1080.png)
- [Modal Hover](C:/Users/user/Documents/Codex/2026-09-22/dl/work/phase35/screenshots/LeaveModalHover_Seed17_1600x900.png)

이 PNG의 Hover는 PlayMode 테스트가 상태 이벤트를 전달해 캡처한 것이다. 실제 native Mouse Hover는 별도로 Editor에서 확인했다.

## 26. 변경된 파일 목록

이번 작업 시작 시의 파일 복사본과 비교했다. PHASE 1.5~3부터 이미 있던 미커밋 파일을 이번 작업이 새로 만든 파일로 분류하지 않았다.

수정한 8개 파일:

- [FlowSession.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Flow/FlowSession.cs)
- [GameFlow.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/GameFlow.cs)
- [GameScreenView.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/GameScreenView.cs)
- [MapPathsGraphic.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/MapPathsGraphic.cs)
- [StyledButton.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/StyledButton.cs)
- [DungeonRuntime.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Rules/DungeonRuntime.cs)
- [RunState.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Rules/RunState.cs)
- [ScreenFlowTests.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Tests/PlayMode/ScreenFlowTests.cs)

추가한 5개 C# 파일 및 각 `.meta`:

- [DungeonMapScroller.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Presentation/DungeonMapScroller.cs)
- [MapGenerationConfig.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Rules/MapGenerationConfig.cs)
- [LayeredMapGenerator.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Scripts/Rules/LayeredMapGenerator.cs)
- [RandomDungeonTests.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Tests/Editor/RandomDungeonTests.cs)
- [MapUxTests.cs](C:/Users/user/Documents/Codex/2026-09-22/dl/outputs/BroDungeon_Paul/Assets/BRODUN/Tests/PlayMode/MapUxTests.cs)

추가 문서: 이 보고서 `UNITY_PHASE3_5_REPORT.md`. 기존 Scene/Prefab/Art/Generated SO/Package의 내용과 GUID는 이번 작업에서 변경하지 않았다. 테스트 결과/캡처/시작 시 백업은 저장소 밖에 보관한다.

Editor가 자동 변경한 `ProjectSettings.asset`의 Standalone define은 작업 시작 시 값으로 복구했다. 이번 실행에서 자동 생성된 `ProjectSettings/SceneTemplateSettings.json`만 제거했다. 이는 Editor가 다시 생성할 수 있는 기본 설정이며 기존 팀원 Scene이나 에셋 삭제가 아니다.

## 27. Unity 버전

`ProjectVersion.txt` 및 실제 실행 로그 모두 **6000.0.47f1 (2ad1ed33fd3b)**이다. 업그레이드하지 않았다. `manifest.json`과 `packages-lock.json`은 이번 작업 시작 시 복사본과 동일하다.

## 28. Git 상태

Repository: `https://github.com/Hordal/BroDungeon.git`, 작업 브랜치 **Paul**.

HEAD 및 원격 Paul: `eedba21b4552200874388bdb9f637850b14b3af0`.

원격 main: `6f0111cbd683c3155c6e6657434e496cac8f9d0c` — 작업 시작 시 값과 동일하다.

stage/commit/push/merge/force push를 수행하지 않았다. 기존 미커밋 변경 및 untracked 파일을 보존했다. `.vsconfig`, `PackageManagerSettings.asset` 등 시작 시 존재하던 파일도 임의 삭제하지 않는다.

## 29. PHASE 4 진입 가능 여부

새 production 맵, 방 선택 제한, Pending Room, RunState 유지, UI와 기존 흐름의 자동 검증은 준비됐다. **다만 실제 마우스 손 드래그의 체감 검증은 남아 있으므로 PHASE 3.5의 모든 수동 검증이 완료됐다고 주장하지 않는다.**

PHASE 4 전 권장 마지막 확인: `BrodunBoot`를 열어 Play → 게임 시작 → 캐릭터 선택 → 맵의 빈 영역을 길게 누른 채 좌우 드래그하고 현재 위치 버튼으로 돌아오기.

PHASE 4에서는 기존 `BattleEntryRequested`에 단일 BattleScene을 연결하고, 실제 방 결과/보상 처리 뒤에만 Complete하도록 연결해야 한다. 길어진 Layer/Floor와 기존 웹의 고정 방 ID 기반 적 선택/난이도 규칙은 Battle 연결 전에 비교해야 한다. 전투·보상·상점·퍼즐의 실제 플레이, 다음 구역 진행, 저장/불러오기, 최종 밸런싱은 이번에 구현하지 않았다.

**PHASE 4는 시작하지 않았다. 다음 지시를 기다린다. main 브랜치는 수정하지 않았다.**
