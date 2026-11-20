# BRODUN PHASE 3 결과 보고

검증일: 2026-10-13. Hordal/BroDungeon / Paul / Unity 6000.0.47f1.
구현 범위: Boot → Title → CharacterSelect → DungeonMap + 현재 덱.
PHASE 4는 시작하지 않았다. 마우스 중심 핵심 흐름은 구현·검증했으며, OS Escape 키 입력 검증의 한계는 17/20/25번에 명시했다.

## 1. 생성한 Scene

- Assets/BRODUN/Scenes/BrodunBoot.unity
- Assets/BRODUN/Scenes/BrodunCharacterSelect.unity
- Assets/BRODUN/Scenes/BrodunDungeonMap.unity
- Assets/BRODUN/Scenes/BrodunTitle.unity

Boot를 Build Settings 첫 Scene으로 등록했다.
기존 Assets/Scenes/SampleScene.unity의 등록과 enabled=true를 보존했다.
기존 SampleScene / URP2DSceneTemplate의 파일은 수정하지 않았다.
각 화면은 독립 Scene이며, Screen View가 Play 시 데이터로 uGUI 계층을 구성한다.
EditMode에서 빈 검은 Boot 카메라만 보이는 것은 정상이다. Play하면 Title로 이동한다.

## 2. 생성한 Prefab

0개. 이번 UI는 GameScreenView/StyledButton의 재사용 가능한 런타임 구성 방식이다.
노드·연결·덱을 Scene에 고정 복제하지 않는다.
Enemy/전투/손패 Prefab은 만들지 않았다.
향후 수동 디자이너 편집을 위한 UI Prefab 분리는 가능하지만 이번에 존재한다고 주장하지 않는다.

## 3. 생성한 C# Script

총 9개.

- Assets/BRODUN/Scripts/Editor/Phase3ProjectBuilder.cs
- Assets/BRODUN/Scripts/Flow/FlowSession.cs
- Assets/BRODUN/Scripts/Presentation/GameFlow.cs
- Assets/BRODUN/Scripts/Presentation/GameScreenView.cs
- Assets/BRODUN/Scripts/Presentation/MapPathsGraphic.cs
- Assets/BRODUN/Scripts/Presentation/PresentationAssets.cs
- Assets/BRODUN/Scripts/Presentation/StyledButton.cs
- Assets/BRODUN/Tests/Editor/Phase3FlowTests.cs
- Assets/BRODUN/Tests/PlayMode/ScreenFlowTests.cs

FlowSession은 UnityEngine 참조가 없는 Flow assembly다.
GameFlow는 Unity Scene/서비스 수명/Transition을 맡고,
GameScreenView는 화면 구성·데이터 표시·Overlay,
StyledButton은 상태별 Sprite/Scale/Cancel 수신,
MapPathsGraphic은 실제 노드 데이터의 연결선을 그린다.
PresentationAssets는 시각 리소스 묶음이며 게임 규칙을 복사하지 않는다.
Phase3ProjectBuilder는 명시적으로 호출하는 Editor 생성 도구다.

## 4. 생성한 UI Asset

새 그림을 생성한 것이 아니라 실제 기존 파일을 재사용했다.
PNG 22개: 배경 2 + 캐릭터 3 + UI 17.
새 PresentationAssets SO 1개: Assets/BRODUN/Data/Presentation.asset.
폰트 2개: NotoSansCJKkr-Regular.otf, Silkscreen-Regular.ttf.
각 OFL 라이선스와 ASSET_SOURCES.md를 포함한다.
새 asmdef 3개: Brodun.Flow / Brodun.Presentation / Brodun.Tests.PlayMode.
원본 웹 PNG/SVG/캐릭터 스프라이트 시트는 삭제하거나 변경하지 않았다.

## 5. 기존 파일 수정 목록

PHASE 3 기준 3개:

- ProjectSettings/EditorBuildSettings.asset: 새 4 Scene을 앞에 등록, 기존 SampleScene 보존.
- Assets/BRODUN/Scripts/Editor/Brodun.Editor.asmdef: 생성 도구의 Flow/Presentation/InputSystem 참조 추가.
- Assets/BRODUN/Tests/Editor/Brodun.Tests.Editor.asmdef: 새 Flow/UI 검사에 필요한 참조 추가.

기존 PHASE 1.5 수정은 그대로 보존:
Assets/Editor/HubForceResolve.cs, Packages/manifest.json, Packages/packages-lock.json.
위 3개의 Git diff는 이번 단계의 추가 패키지 변경이 아니다.
PHASE 2 Rules C# / 45개 원본 데이터 / JSON 기대값 / 웹 코드는 바꾸지 않았다.
Unity 자동 생성 .vsconfig / PackageManagerSettings.asset / SceneTemplateSettings.json은 제거했다.
ProjectSettings.asset의 자동 define 변경은 기존 {}로 되돌리고 HEAD와 내용 동일성을 확인했다.
기존 Scene/URP/Input Actions/ProjectVersion 수정 없음.

## 6. GameFlow 구조

FlowSession이 한 Run과 PendingSeed/PendingRoom/현재 화면/전환 잠금/덱 표시 상태를 소유한다.
GameFlow 하나만 DontDestroyOnLoad로 유지하며 그 자식 EventSystem/Transition Overlay도 유지된다.
Card/Deck/Dungeon 시스템마다 Singleton을 만들지 않았다.
SceneManager 호출은 GameFlow에 모았다. 버튼에서 Scene 문자열을 직접 로드하지 않는다.
캐릭터 선택 전에는 Run=null, PendingSeed만 유지한다.
이는 PHASE 2 RunState 생성에 CharacterId가 필요하기 때문이며 임시 전사를 선택하지 않는다.
GAME START는 기존 Run을 버리고 Seed를 설정한 뒤 선택 화면으로 간다.

## 7. Scene Transition 구조

Fade Out 0.22초 → LoadSceneAsync → View Bind → Fade In 0.22초.
합계 약 0.44초 + Scene 로드 시간.
전환용 Overlay가 Raycast를 차단하고 FlowSession.IsTransitioning도 중복 호출을 거절한다.
UI의 새 content는 준비 전 interactable=false다.
씬 로드 후 같은 Session/Run을 사용한다.
Transition은 Coroutine/Time.unscaledDeltaTime으로 구현했다. 외부 Tween Package 없음.

## 8. Title 구현

16:9 전체 배경, 금색 픽셀 BRODUN 로고, 한국어 문구, 게임 시작/종료.
작동하지 않는 Continue/Settings는 표시하지 않는 방식을 선택했다.
개발 Navbar/Seed/Gallery/Screenshot Mode/Test 버튼 없음.
Normal/Hover/Pressed/Disabled Sprite와 작은 Scale 변화, 금색/청록 강조를 공유한다.
메뉴 종료는 Player에서는 Application.Quit, Editor에서는 Play Mode 종료다.
오디오/복잡한 Parallax/먼지 효과는 추가하지 않았다.

## 9. CharacterSelect 구현

전사/마법사/사제의 실제 idle_00 PNG를 재사용했다.
이름/HP/시작 행동력/설명/시작 덱 수는 GameCatalogData의 원본 정의를 읽는다.
모두 실제 데이터인 HP 60, Energy 3, 시작 4장. 예시의 HP 80은 적용하지 않았다.
카드 Hover/Selected 테두리와 Scale, 클릭 중복 차단.
선택하면 PHASE 2 RunState 생성자가 HP/영구 시작 덱/Seed 기반 Dungeon을 초기화한다.
UI 전용 능력/추가 패시브/직업별 임의 수치는 없다.
뒤로가기 버튼은 Title로 이동하며 선택 대기 상태를 폐기한다.

## 10. DungeonMap 구현

RunState.Map의 실제 X/Y/Next 데이터를 화면 Anchor와 선으로 변환한다.
UI에서는 Random을 다시 실행하지 않는다.
시작 포함 11노드, 기존 연결과 방 종류/Seed 결과를 유지한다.
일반/엘리트/보스/퍼즐/이벤트의 Shop·Altar·Rest 하위 종류를 표시한다.
Shop/Altar/Rest를 새 RoomType으로 추가하지 않았다.

Locked: 어두운 Sprite/아이콘, 버튼 interactable=false.
Available: 정상 프레임, 금색 경로/표시, 약한 Scale pulse.
Hover/keyboard selection: 상태별 밝은 Sprite/Scale.
선택 이후 Current: selected 프레임 + 현재 위치 표시.
Completed: Run.CompletedNodes 기반 체크 아이콘/완료 표시와 지나간 경로.
현재 클릭한 노드는 CurrentNode/ActiveRoom에 들어가지만 Completed에는 넣지 않는다.
PendingRoom 안내를 닫아도 해당 방은 미완료로 남으며 다음 방은 열리지 않는다.
PHASE 4가 결과 처리와 방 완료를 연결할 때까지 의도적으로 진행을 멈춘다.

BattleEntryRequested(PendingRoomContext)와 GameFlow.EnterBattle 진입점을 준비했다.
핸들러가 아직 없으면 안전한 미구현 안내만 표시한다.
실제 BattleRuntime/적 생성/피해/보상은 이 UI에서 실행하지 않는다.

## 11. Deck Viewer 구현

맵 HUD의 현재 덱 버튼으로 Overlay를 연다.
Run.Deck 인스턴스를 읽어 각 카드 이름/비용/속성/전체 장수를 표시한다.
중복 카드 인스턴스도 각각 표시한다.
ScrollRect + VerticalLayoutGroup + RectMask2D로 목록이 길어지는 경우를 준비했다.
닫기 버튼/표준 uGUI Cancel 이벤트를 지원한다.
모달 뒤쪽 클릭은 차단하고 버튼 이벤트도 Overlay 밖에서는 거절한다.
획득/업그레이드/제거/손패 기능은 추가하지 않았다.
Open/Refresh 시 HUD도 Run의 최신 HP/Gold/층/덱 수를 다시 읽는다.

## 12. RunState 연결 방식

GameCatalogData → 독립 GameDefinitions → FlowSession → RunState.
View는 Run을 생성/복제하지 않고 소유한 Flow에 요청한다.
Scene 간 동일 Run 객체가 유지되며 PendingRoom은 Run.ActiveRoom으로 재구성 가능하다.
Map 재로드 테스트로 동일 객체, 카드/골드/CurrentNode/완료 목록 유지 여부를 확인했다.
Title로 나갈 때 확인 창을 거쳐 Run을 명시적으로 버린다. Save/Continue를 가장하지 않는다.

## 13. 사용한 기존 에셋

WebPrototype/Assets/BRODUNArt의 실제 PNG:

- Assets/BRODUN/Art/Backgrounds/area01_forgotten_corridor.png
- Assets/BRODUN/Art/Backgrounds/title_background.png
- Assets/BRODUN/Art/Characters/mage.png
- Assets/BRODUN/Art/Characters/priest.png
- Assets/BRODUN/Art/Characters/warrior.png
- Assets/BRODUN/Art/UI/button_disabled.png
- Assets/BRODUN/Art/UI/button_hover.png
- Assets/BRODUN/Art/UI/button_normal.png
- Assets/BRODUN/Art/UI/button_pressed.png
- Assets/BRODUN/Art/UI/event_altar.png
- Assets/BRODUN/Art/UI/event_rest.png
- Assets/BRODUN/Art/UI/event_shop.png
- Assets/BRODUN/Art/UI/icon_battle.png
- Assets/BRODUN/Art/UI/icon_boss.png
- Assets/BRODUN/Art/UI/icon_event.png
- Assets/BRODUN/Art/UI/icon_puzzle.png
- Assets/BRODUN/Art/UI/map_choice_hover.png
- Assets/BRODUN/Art/UI/map_choice_locked.png
- Assets/BRODUN/Art/UI/map_choice_normal.png
- Assets/BRODUN/Art/UI/map_choice_selected.png
- Assets/BRODUN/Art/UI/node_complete.png
- Assets/BRODUN/Art/UI/node_current.png

원본 출처와 폰트 원본 URL은 Art/ASSET_SOURCES.md에 있다.
캐릭터 PNG 이름은 Unity용으로 바꿨지만 실제 원본은 각 Frames/<character>/idle_00.png다.
기존 InputSystem_Actions의 UI Point/Click/ScrollWheel/Navigate/Submit/Cancel을 복제해 사용한다.
기존 Player action map은 이동 기능으로 연결하지 않았다.

## 14. Placeholder 목록

시각 Asset의 가짜 참조나 회색 기본 Button placeholder는 없다.
전투방/보스: 미구현 안내 + PendingRoomContext만 준비.
퍼즐/Shop/Altar/Rest: 콘텐츠 미구현 안내.
표시를 위해 실제 기능이 있다고 가장하거나 방을 자동 완료하지 않는다.
음악/효과음은 없으며 재생을 가장하지 않는다.

## 15. UI 시스템 선택

uGUI. Text/Image/Button/CanvasGroup/CanvasScaler/ScrollRect와 InputSystemUIInputModule 사용.

## 16. 선택 이유

현재 프로젝트에 UI 체계가 없었고 uGUI/Input System이 이미 설치되어 있다.
Sprite 중심 UI, Overlay, Hover/Scale, 향후 카드 애니메이션 연결을 새 패키지 없이 구현할 수 있다.
CanvasScaler Scale With Screen Size, 1920×1080, match=0.5.
큰 레이아웃은 비율 Anchor, 노드는 데이터 좌표를 비율 Anchor로 사용한다.
테스트한 16:9에서는 검은 웹형 중앙 영역/좌우 여백을 게임 화면 안에 만들지 않았다.
픽셀 PNG는 Point, Mipmap off, Uncompressed. 필요한 22개에만 적용했다.
한국어 폰트를 포함해 특정 PC의 Windows 폰트에 의존하지 않는다.
SIL OFL 1.1 문서와 원본 링크는 Fonts/ 및 ASSET_SOURCES.md에서 확인할 수 있다.

## 17. 실제 Play Mode 테스트 결과

batch만 실행한 것이 아니다.
Unity 6000.0.47f1 Editor 창을 열고 Boot Scene을 직접 로드한 뒤 Play를 눌렀다.
실제 마우스로 GAME START → 마법사 선택 → DungeonMap까지 진행했다.
현재 덱 열기/닫기, 잠긴 보스 클릭 거절, Available r1 선택,
미구현 안내, 현재 위치 변경/미완료 상태 유지도 실제 창에서 확인했다.
검사 후 Play Mode를 종료하고 Editor를 닫았다. Scene 저장은 하지 않았다.
최종 HUD/Cancel 보완은 이후 자동 PlayMode로 다시 검사했다.

Escape 실키 입력은 그 Editor 조작에서 닫힘을 확인하지 못했다.
이후 가상 키 이벤트/키 상태 진단도 장치 상태 자체가 적용되지 않아,
그 실패를 게임 로직 오류라고 단정하거나 키보드 성공으로 바꿔 보고하지 않는다.
최종 구현은 표준 ICancelHandler로 바꾸고 배경 클릭 후 선택도 유지하도록 했다.
최종 테스트는 Escape 바인딩 및 실제 uGUI Cancel 수신 → Deck Close까지 확인한다.
OS/실기 키보드 → Game View 입력 전체 경로는 아직 별도 재확인이 필요하다.

## 18. 해상도 테스트 결과

| 해상도 | Title | CharacterSelect | DungeonMap | Deck |
| --- | --- | --- | --- | --- |
| 1920×1080 | 통과/캡처 | 통과/캡처 | 통과/캡처 | 통과/캡처 |
| 1600×900 | 통과/캡처 | 통과/캡처 | 통과/캡처 | 통과/캡처 |
| 1280×720 | 통과/캡처 | 통과/캡처 | 통과/캡처 | 통과/캡처 |

실제 Unity PlayModeWindow.SetCustomRenderingResolution으로 Game View 크기를 설정했다.
Screen.width/height를 assert했고, 모든 활성 버튼의 화면 내 경계도 검사했다.
ScreenCapture.CaptureScreenshot으로 게임 렌더 결과 12장을 저장했다.
그림을 합성한 UI 목업이나 HTML 캡처가 아니다.
PNG를 직접 열어 글자/캐릭터/버튼/노드/목록을 확인했다.
초기 마법사 이미지와 이름의 간격 및 얇은 가변 폰트 가독성을 보완했다.
16:9 밖의 화면, 초광폭, 모바일, 다국어 긴 문장, 최종 픽셀 배율 정수화는 미검증이다.

저장소 밖 작업 폴더: work/phase3/screenshots/
Title / CharacterSelect / DungeonMap / Deck 뒤에 _1920x1080.png, _1600x900.png, _1280x720.png가 붙는다.

## 19. EditMode Test 결과

최종 EditMode-release.xml: 153 / 통과 153 / 실패 0 / 스킵 0.
기존 PHASE 2 143개가 그대로 통과했다. 신규 Flow/등록 검사 10사례 추가:

- Boot/Title/GameStart 및 중복 전환 잠금
- 3직업 데이터/시작 덱/HP/Seed 연결
- Available/Locked 및 방 선택 후 미완료
- Completed 상태는 실제 Runtime 완료에서만 파생
- 덱 열기·닫기/선택 잠금/원본 유지
- 맵 재로드와 PendingRoom 재구성/Run 유지
- 여정 폐기/새 Run 초기화
- Scene 등록, Boot 직렬화 의존성, 필수 Sprite/font/Input 참조

Node exporter --check도 성공했다. 웹 규칙/기대값은 변하지 않았다.

## 20. PlayMode Test 결과

최종 PlayMode-release.xml: 6 / 통과 6 / 실패 0 / 스킵 0. Editor exit 0.

- BootTitleCharacterSelectionAndRealSceneLoad
- AllThreeCharactersUseCatalogStatsAndDeck
- LockedNodesAndBattleEntryRemainUncompletedAcrossReload
- DeckOverlayOpensClosesAndUsesCurrentDeck
- BackAndExplicitAbandonment
- GameViewResolutionScreenshotsAndLayoutBounds

실제 Scene 로드 + uGUI pointerClickHandler/CancelHandler로 UI 수신부를 검증한다.
Cancel 검사는 하드웨어 키보드 입력 전체를 검증한 것과 다르다.
직접 게임 컨트롤러만 호출하고 UI 버튼이 작동했다고 주장하지 않는다.
원시 OS 입력 경로는 17번의 수동 확인 범위와 한계를 따른다.

중간 실패/수정 이력도 보관했다:

- 생성 도구 OpenScene의 확장자 누락: .unity 추가.
- Editor 테스트 InputSystem assembly 참조 누락: asmdef 참조 보완.
- Boot Presentation SO 참조 누락: NewScene의 unused asset unload 후 저장된 Asset을 다시 읽어 연결하고 assert 추가.
- Escape 장치 이벤트/상태 주입 진단 실패: 검증 불가능한 하드웨어 성공 주장 없이 표준 uGUI 수신부 테스트와 분리.

최종 근거 파일은 work/phase3의 EditMode-release.xml, PlayMode-release.xml,
Editor-editmode-release.log, Editor-playmode-release.log이다.
실제 Editor 수동 확인 로그는 Editor-interactive.log이다.
초기 실패/중간 성공 로그도 삭제하지 않았다.

## 21. Compile Error / Warning

최종 실제 Unity 실행: Compile Error 0, C# Warning 0.
버전은 6000.0.47f1. 두 최종 테스트 프로세스 종료 코드 0.
처음 개발 중 발생한 오류와 최종 상태를 구분한다.

## 22. Console Error / Warning

최종 게임 테스트에서 NullReference/Scene Load/게임 예외/Serialization 경고 없음.
수동 마우스 흐름 로그에도 게임 예외/컴파일 오류 없음.
기존 native Licensing Client signature/token/ULF 진단은 시작 로그에 남아 있으나,
이후 entitlement 확인 후 Editor/테스트가 정상 실행됐다.
원시 로그의 모든 Error 문자열이 0이라는 주장은 하지 않는다.
인증/OS 보안/Editor 업그레이드 설정은 건드리지 않았다.
Package Resolve/List 49개 성공도 최종 EditMode 검사에서 다시 확인했다.

## 23. Missing Script / Sprite

기존 두 Scene 로드 검사: Missing Script 0.
신규 화면 실제 Play 시 모든 Transform의 Component null 검사: Missing Script 0.
필수 Presentation Sprite 전부 존재, 실제 View background/portrait/room icon 참조 검사 통과.
한국어/로고 폰트와 Input Actions도 실제 로딩 참조를 확인했다.

## 24. 웹과 UI 차이

웹의 작은 중앙 캔버스/개발 Navbar 대신 전체 화면 던전 배경.
금색 픽셀 로고/청록 Hover/프레임형 캐릭터 선택과 HUD를 도입했다.
맵은 실제 데이터 기반 선과 상태별 노드, 게임형 덱 버튼/Overlay로 재배치했다.
Seed 입력/갤러리/개발 네비게이션을 게임 화면에 노출하지 않는다.
기능/데이터를 계승했지만 HTML/CSS의 배치나 DOM을 Unity로 복제하지 않았다.

## 25. 아직 구현하지 않은 기능 / 검증 한계

BattleScene, 손패, 적/Intent/전투 HP UI, RewardScene, 공격/피격/사망 애니메이션,
VFX/오디오/전투 결과 처리, 실제 Shop/Altar/Rest/Puzzle, Save/Continue, Settings 없음.
방을 선택한 뒤 실제 콘텐츠를 끝낼 수 없는 것은 이번 범위의 명시적인 제한이다.
마우스 핵심 흐름은 실제 Editor에서 확인했다.
최종 OS Escape 키 전체 경로, standalone Player build, 전체 키보드 탐색/게임패드는 미검증.
가상 키 상태 주입 진단 실패를 숨기지 않으며, 키 이벤트 전달 문제의 최종 원인을 확정했다고 주장하지 않는다.
최종 UI Cancel 수신부/바인딩 검사만 통과했다고 구분한다.
PHASE 4 진입 전에 게임 창 포커스를 포함한 키보드 실기 QA를 추가하는 것이 좋다.
Scene/View UI는 런타임 구성 방식이라 EditMode Canvas를 수동 드래그해서 편집하는 Prefab은 아직 없다.

## 26. Unity Editor 버전

ProjectVersion.txt: 6000.0.47f1 / 2ad1ed33fd3b.
실제 실행 창과 Application.unityVersion 검사도 일치.
다른 Unity 버전으로 열거나 재저장하지 않았다.

## 27. Package 변경 여부

PHASE 3의 추가/삭제/버전 변경 0.
uGUI 2.0.0, Input System 1.11.2 및 PHASE 1.5 resolved 버전 유지.
URP manifest/lock 차이와 Test Framework manifest/lock 차이를 임의 정렬하지 않았다.
AI Assistant 재추가, 외부 Tween/폰트/SVG Package 설치 없음.
폰트는 일반 Asset 파일이며 Unity Package가 아니다.

## 28. Git 상태

브랜치 Paul. origin=https://github.com/Hordal/BroDungeon.git.
HEAD/원격 Paul은 eedba21b4552200874388bdb9f637850b14b3af0 그대로.
원격 main 작업 전/후 6f0111cbd683c3155c6e6657434e496cac8f9d0c 그대로.
commit/push/merge/force push 없음. 새 작업은 unstaged/untracked다.
기존 팀원 tracked 파일 삭제 없음.
PHASE 3 신규 파일 98개(이 보고서 포함, Unity meta 포함). PHASE 2 신규 파일과 구분한 목록은 부록 A.
tracked 변경은 이전 PHASE 1.5의 3개 + 이번 EditorBuildSettings.asset 1개.
PHASE 2에서 만든 미추적 asmdef 2개도 이번 참조 변경을 위 5번에 별도 명시했다.
main 브랜치는 수정하지 않았다.

## 29. PHASE 4 진입 가능 여부

마우스 기반 Boot → Title → 캐릭터 → 맵/덱과 데이터 연결 기반은 준비됐다.
BattleEntryRequested/PendingRoomContext를 다음 단계에서 BattleScene 연결점으로 사용할 수 있다.
153 EditMode + 6 PlayMode 통과, 네 Scene과 실제 시각 리소스가 존재한다.
단 OS Escape/전체 키보드/standalone 실기 검증 한계는 남아 있다.
이를 제외한 현재 마우스 기반 PHASE 3 목표는 완료했으며, PHASE 4는 지시 전까지 시작하지 않는다.

## 실행 / 유지보수

1. Unity 6000.0.47f1로 저장소 프로젝트를 연다.
2. Assets/BRODUN/Scenes/BrodunBoot.unity를 열고 Play한다.
3. GAME START → 캐릭터 클릭 → 맵 → 현재 덱 / 연결된 첫 방 선택을 확인한다.
4. 전투/다른 콘텐츠는 미구현 안내에서 멈춘다. 자동 클리어나 테스트용 진행 버튼은 없다.
5. 화면 생성 도구는 BRODUN/PHASE 3/Build screens and register scenes.
   생성 소유의 네 BRODUN Scene을 다시 만들므로 그 Scene을 수동 수정했다면 먼저 diff/백업을 확인한다.
   기존 팀원 Scene은 보존한다. 이 도구를 실행할 때마다 게임 규칙 SO를 재생성하지 않는다.
6. Scene View에서 UI가 보이지 않아도 Play에서 생성되는 정상 구조다. Hierarchy에서 실행 중 UI를 검사할 수 있다.
7. Native Keyboard/Editor 포커스 QA는 이 보고서의 미검증 항목을 참고한다.

## 부록 A. PHASE 3 신규 파일 전체

```
Assets/BRODUN/Art.meta
Assets/BRODUN/Art/ASSET_SOURCES.md
Assets/BRODUN/Art/ASSET_SOURCES.md.meta
Assets/BRODUN/Art/Backgrounds.meta
Assets/BRODUN/Art/Backgrounds/area01_forgotten_corridor.png
Assets/BRODUN/Art/Backgrounds/area01_forgotten_corridor.png.meta
Assets/BRODUN/Art/Backgrounds/title_background.png
Assets/BRODUN/Art/Backgrounds/title_background.png.meta
Assets/BRODUN/Art/Characters.meta
Assets/BRODUN/Art/Characters/mage.png
Assets/BRODUN/Art/Characters/mage.png.meta
Assets/BRODUN/Art/Characters/priest.png
Assets/BRODUN/Art/Characters/priest.png.meta
Assets/BRODUN/Art/Characters/warrior.png
Assets/BRODUN/Art/Characters/warrior.png.meta
Assets/BRODUN/Art/Fonts.meta
Assets/BRODUN/Art/Fonts/NotoSansCJKkr-Regular.otf
Assets/BRODUN/Art/Fonts/NotoSansCJKkr-Regular.otf.meta
Assets/BRODUN/Art/Fonts/OFL.txt
Assets/BRODUN/Art/Fonts/OFL.txt.meta
Assets/BRODUN/Art/Fonts/OFL_Silkscreen.txt
Assets/BRODUN/Art/Fonts/OFL_Silkscreen.txt.meta
Assets/BRODUN/Art/Fonts/Silkscreen-Regular.ttf
Assets/BRODUN/Art/Fonts/Silkscreen-Regular.ttf.meta
Assets/BRODUN/Art/UI.meta
Assets/BRODUN/Art/UI/button_disabled.png
Assets/BRODUN/Art/UI/button_disabled.png.meta
Assets/BRODUN/Art/UI/button_hover.png
Assets/BRODUN/Art/UI/button_hover.png.meta
Assets/BRODUN/Art/UI/button_normal.png
Assets/BRODUN/Art/UI/button_normal.png.meta
Assets/BRODUN/Art/UI/button_pressed.png
Assets/BRODUN/Art/UI/button_pressed.png.meta
Assets/BRODUN/Art/UI/event_altar.png
Assets/BRODUN/Art/UI/event_altar.png.meta
Assets/BRODUN/Art/UI/event_rest.png
Assets/BRODUN/Art/UI/event_rest.png.meta
Assets/BRODUN/Art/UI/event_shop.png
Assets/BRODUN/Art/UI/event_shop.png.meta
Assets/BRODUN/Art/UI/icon_battle.png
Assets/BRODUN/Art/UI/icon_battle.png.meta
Assets/BRODUN/Art/UI/icon_boss.png
Assets/BRODUN/Art/UI/icon_boss.png.meta
Assets/BRODUN/Art/UI/icon_event.png
Assets/BRODUN/Art/UI/icon_event.png.meta
Assets/BRODUN/Art/UI/icon_puzzle.png
Assets/BRODUN/Art/UI/icon_puzzle.png.meta
Assets/BRODUN/Art/UI/map_choice_hover.png
Assets/BRODUN/Art/UI/map_choice_hover.png.meta
Assets/BRODUN/Art/UI/map_choice_locked.png
Assets/BRODUN/Art/UI/map_choice_locked.png.meta
Assets/BRODUN/Art/UI/map_choice_normal.png
Assets/BRODUN/Art/UI/map_choice_normal.png.meta
Assets/BRODUN/Art/UI/map_choice_selected.png
Assets/BRODUN/Art/UI/map_choice_selected.png.meta
Assets/BRODUN/Art/UI/node_complete.png
Assets/BRODUN/Art/UI/node_complete.png.meta
Assets/BRODUN/Art/UI/node_current.png
Assets/BRODUN/Art/UI/node_current.png.meta
Assets/BRODUN/Data/Presentation.asset
Assets/BRODUN/Data/Presentation.asset.meta
Assets/BRODUN/Scenes.meta
Assets/BRODUN/Scenes/BrodunBoot.unity
Assets/BRODUN/Scenes/BrodunBoot.unity.meta
Assets/BRODUN/Scenes/BrodunCharacterSelect.unity
Assets/BRODUN/Scenes/BrodunCharacterSelect.unity.meta
Assets/BRODUN/Scenes/BrodunDungeonMap.unity
Assets/BRODUN/Scenes/BrodunDungeonMap.unity.meta
Assets/BRODUN/Scenes/BrodunTitle.unity
Assets/BRODUN/Scenes/BrodunTitle.unity.meta
Assets/BRODUN/Scripts/Editor/Phase3ProjectBuilder.cs
Assets/BRODUN/Scripts/Editor/Phase3ProjectBuilder.cs.meta
Assets/BRODUN/Scripts/Flow.meta
Assets/BRODUN/Scripts/Flow/Brodun.Flow.asmdef
Assets/BRODUN/Scripts/Flow/Brodun.Flow.asmdef.meta
Assets/BRODUN/Scripts/Flow/FlowSession.cs
Assets/BRODUN/Scripts/Flow/FlowSession.cs.meta
Assets/BRODUN/Scripts/Presentation.meta
Assets/BRODUN/Scripts/Presentation/Brodun.Presentation.asmdef
Assets/BRODUN/Scripts/Presentation/Brodun.Presentation.asmdef.meta
Assets/BRODUN/Scripts/Presentation/GameFlow.cs
Assets/BRODUN/Scripts/Presentation/GameFlow.cs.meta
Assets/BRODUN/Scripts/Presentation/GameScreenView.cs
Assets/BRODUN/Scripts/Presentation/GameScreenView.cs.meta
Assets/BRODUN/Scripts/Presentation/MapPathsGraphic.cs
Assets/BRODUN/Scripts/Presentation/MapPathsGraphic.cs.meta
Assets/BRODUN/Scripts/Presentation/PresentationAssets.cs
Assets/BRODUN/Scripts/Presentation/PresentationAssets.cs.meta
Assets/BRODUN/Scripts/Presentation/StyledButton.cs
Assets/BRODUN/Scripts/Presentation/StyledButton.cs.meta
Assets/BRODUN/Tests/Editor/Phase3FlowTests.cs
Assets/BRODUN/Tests/Editor/Phase3FlowTests.cs.meta
Assets/BRODUN/Tests/PlayMode.meta
Assets/BRODUN/Tests/PlayMode/Brodun.Tests.PlayMode.asmdef
Assets/BRODUN/Tests/PlayMode/Brodun.Tests.PlayMode.asmdef.meta
Assets/BRODUN/Tests/PlayMode/ScreenFlowTests.cs
Assets/BRODUN/Tests/PlayMode/ScreenFlowTests.cs.meta
UNITY_PHASE3_REPORT.md
```
