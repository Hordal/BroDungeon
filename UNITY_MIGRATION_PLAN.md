# BRODUN Unity 이식 분석 및 단계별 개발 계획

분석일: 2026-10-13 KST. 대상: `Hordal/BroDungeon`, `Paul`. 기준 Editor: **Unity 6000.0.47f1**.

## 1. 결론과 이번 작업의 경계

기존 Unity 프로젝트를 유지하면서 웹에서 검증한 규칙을 C#으로 이식하고, 화면과 연출은 Unity 게임으로 재설계한다. 새 Unity 프로젝트 생성, HTML/CSS의 Canvas 1:1 복제, JavaScript/WebView 런타임 실행은 하지 않는다.

현재 Unity 쪽은 URP 2D 기본 프로젝트이고 BRODUN 게임 코드·UI는 아직 없다. 웹에는 1구역 카드 전투 루프가 구현되어 있다. 데이터 계층부터 이식할 수 있으나 **AI Assistant의 최소 Editor 요구사항과 Editor 자동 삭제 코드에 대한 사전 처리가 필요하다.** 패키지를 모두 호환 검증했다고 판단할 수는 없다.

이번 PHASE 1에서 만든 파일은 이 문서 하나뿐이다. Scene/Prefab/C#/에셋/ProjectSettings/Packages/WebPrototype은 변경하지 않았다. Editor 실행, Package Resolve, Unity 컴파일, Play Mode, 빌드는 수행하지 않았다. PHASE 2~6은 아래의 제안이며 이번에 구현한 결과가 아니다. commit/push/PR/merge도 하지 않았다.

### 분석 기준과 근거

- 로컬 HEAD와 fetch 후 `origin/Paul`: `eedba21b4552200874388bdb9f637850b14b3af0`.
- 원격 main 읽기 기준: `6f0111cbd683c3155c6e6657434e496cac8f9d0c`.
- 시작 시 작업 트리는 clean. 원격 Paul에 추가 변경은 없었고 pull/merge는 필요하지 않았다.
- 추적 파일 462개: 기존 Unity 프로젝트 및 루트 파일 50개, WebPrototype 412개.
- 코드/JSON/YAML/PNG 헤더, 로컬 설치 Editor의 내장 패키지 manifest, Unity 공식 문서·레지스트리 메타데이터를 읽었다. 이전 보고서의 완료 표현을 현재 Unity 구현의 증거로 사용하지 않았다.
- 근거의 수준을 구분한다: **파일 확인**, **웹 규칙 테스트**, **공식 최소 버전 확인**, **Unity 실행 검증 미수행**.

## 2. 기존 Unity 프로젝트 분석

| 위치 | 실제 확인 결과 | 보존 및 이식 판단 |
|---|---|---|
| `Assets/Scenes/SampleScene.unity` | Main Camera, Global Light 2D. Orthographic Camera. Canvas 및 PrefabInstance 없음 | 현재 Build 시작 씬. 삭제·교체하지 않는다 |
| `Assets/Settings/Scenes/URP2DSceneTemplate.unity` | Main Camera, Global Light 2D의 템플릿 씬 | 게임 씬이 아니라 템플릿으로 보존 |
| `Assets/Settings/Lit2DSceneTemplate.scenetemplate` | Lit 2D 템플릿과 내장 미리보기 텍스처 | BRODUN 배경으로 오인하지 않는다 |
| `Assets/Settings/UniversalRP.asset` | Renderer2D GUID `424799608f7334c24bf367e4bbfa7f9a` 참조 | 기존 파이프라인 유지 |
| `Assets/Settings/Renderer2D.asset` | 2D Renderer, 별도 RendererFeature 없음 | 새 렌더링 기능/파이프라인 교체 불필요 |
| `Assets/DefaultVolumeProfile.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset` | Volume/SRP 설정 에셋 | 설정용 ScriptableObject. CardData 등 게임 데이터와 구분 |
| `Assets/InputSystem_Actions.inputactions` | Player/UI 액션 맵 | 기존 파일과 GUID 보존. UI 맵 재사용 검토 |
| `Assets/Editor/HubForceResolve.cs` | 유일한 C# 파일. Editor 초기 패키지 Resolve 및 자기 삭제 | 게임 코드가 아니며 실행 부작용 있음 |
| `Packages/` | 직접 의존성 12개, lock 항목 51개 | 현재 선언/lock 차이와 호환성 문제만 기록 |
| `WebPrototype/` | HTML/CSS/JS, 이미지, 테스트, 문서 | 기능 명세와 아트 원본. Unity에서 자동 임포트되지 않는 위치 |

`Assets/`의 실제 파일 유형은 `.asset` 4개, `.unity` 2개, `.cs` 1개, `.inputactions` 1개, `.scenetemplate` 1개, `.meta` 13개다. BRODUN 런타임 C#, Prefab, 게임용 ScriptableObject, Animator Controller, AnimationClip, Canvas/EventSystem, TMP Font Asset은 발견되지 않았다. 다른 팀 브랜치에 있을 수 있는 코드를 없다고 단정하지 않으며 이번 판단은 Paul에 한정한다.

### 설정 확인

- `ProjectSettings/ProjectVersion.txt`: `6000.0.47f1`, revision `2ad1ed33fd3b`. 요청 버전과 일치한다.
- 표준 설치 위치에서 해당 Editor의 `Unity.exe` 존재와 파일 버전 `6000.0.47.2806253`을 확인했다. **실행한 것은 아니다.**
- `EditorBuildSettings.asset`: 활성 씬은 SampleScene 하나. Input Actions GUID 연결이 있다.
- `GraphicsSettings.asset`: UniversalRP GUID `681886c5eb7344803b6206f758bf0b1c` 연결. 해당 `.meta`와 일치한다.
- `QualitySettings.asset`: 현재 index 5(Ultra), 품질별 별도 RenderPipeline 참조는 0. 기본 Graphics 파이프라인을 기준으로 조사했다.
- 기본 화면 1920×1080, Web 기본 960×600. 기존 Canvas Scaler는 없다. 새 게임 UI의 1920×1080 기준 설계와 기존 PlayerSettings 변경은 별개다.
- `activeInputHandler: 2`(Both). UI는 새 Input System 경로로 구성하되 이 설정을 임의로 변경하지 않는다.
- Sorting Layer는 Default만 있다. Background/Character/Effect/WorldUI 등의 추가는 PHASE 5에서 검토·승인된 변경으로 한다.
- productName은 `BroDugeon`. 표기 정리는 향후 별도 변경 항목이며 이번에는 유지했다.

### 기존 Editor 코드의 실행 위험

`HubForceResolve.cs`는 `[InitializeOnLoad]`로 Editor 시작 시 동작한다. `Client.Resolve()`를 호출하고 SessionState를 남긴 뒤 `AssetDatabase.DeleteAsset`로 자기 스크립트를 삭제한다. 폴더가 비면 Editor 폴더도 삭제할 수 있다. Resolve 요청 완료를 Request.Status로 직접 확인하는 구조는 아니다.

따라서 단순히 Editor를 여는 행위도 패키지 잠금 파일과 추적 에셋에 변경을 일으킬 수 있다. 이번에는 실행하지 않았다. PHASE 2의 첫 Editor 실행 전 팀 승인 하에 이 동작을 어떻게 안전하게 처리할지 결정해야 한다. 이번 분석에서 파일 삭제·이름 변경·비활성화는 하지 않았다.

## 3. Unity 및 패키지 호환성 감사

프로젝트의 `Library/PackageCache`는 없다. lock 기록을 모든 패키지가 실제 프로젝트에서 해석·컴파일된 증거로 취급하지 않는다. 다음은 선언값, lock 값, 설치된 47f1 내장 패키지/공식 레지스트리의 최소 Editor 메타데이터 비교다.

| 패키지 | manifest | lock | 확인한 최소 Editor 또는 내장 근거 | 판단 |
|---|---|---|---|---|
| collab-proxy | 2.6.0 | 2.6.0 | 2021.3.0f1 | 최소 버전 범위 충족, 실행 미검증 |
| feature.2d | 2.0.1 | 2.0.1 | 47f1 내장 패키지 2.0.1 | 2D 기반 유지 |
| ide.rider | 3.0.31 | 3.0.31 | 2019.2.6f1 | 최소 버전 범위 충족 |
| ide.visualstudio | 2.0.22 | 2.0.22 | 2019.4.25f1 | 최소 버전 범위 충족 |
| inputsystem | 1.11.2 | 1.11.2 | 2019.4 | 기존 UI 입력에 사용 가능성 확인, 실행 필요 |
| render-pipelines.universal | **17.0.3** | **17.0.4** | 47f1 내장 URP/Core 모두 17.0.4 | 선언/lock 차이 기록. 오류 확정 아님 |
| test-framework | **1.4.5** | **1.5.1** | 47f1 내장 Test Framework 1.5.1 | 선언/lock 차이 기록. 오류 확정 아님 |
| timeline | 1.8.7 | 1.8.7 | 2019.3 | 현재 기반 유지, 필수 사용 아님 |
| ugui | 2.0.0 | 2.0.0 | 47f1 내장 2.0.0, 최소 2019.2 | 제안 UI 기반 |
| visualscripting | 1.9.5 | 1.9.5 | 2021.3 | 기존 유지, C# 이식에 필수 아님 |
| ai.assistant | **2.20.0-pre.1** | 동일 | **6000.0 + unityRelease 60f1** | **47f1은 선언된 최소 버전 미충족** |
| ai.inference | 2.6.1 | 동일 | 6000.0 | 최소 버전 범위에는 들어감. 전이 의존성/컴파일 미검증 |

위 패키지 ID의 공통 접두사는 `com.unity.`다. 모든 버전은 이번 단계에서 그대로 유지한다.

### 우선 해결 항목

1. **AI Assistant:** 공식 레지스트리 `versions["2.20.0-pre.1"]`는 `unity: "6000.0"`, `unityRelease: "60f1"`을 명시한다. Unity 패키지 manifest 규칙에 따라 최소 6000.0.60f1 요구로 해석되며 현재 47f1보다 높다. Editor 업그레이드로 해결하지 않는다. PHASE 2 시작 전에 팀 승인으로 게임에 필수 아닌 해당 Editor 패키지 제거 또는 47f1 호환 버전 선택을 검토한다. 대체 버전은 당시 메타데이터를 재검증하고 확정한다. 오늘 삭제·다운그레이드하지 않았다. [공식 패키지 메타데이터](https://packages.unity.com/com.unity.ai.assistant), [unity와 unityRelease 의미](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-manifestPkg.html).
2. **URP/Test Framework 차이:** 잠금 값은 실제 설치된 47f1의 내장 값과 같다. 숫자만 보고 lock를 수동 편집하거나 전체 업데이트하지 않는다. 승인된 첫 Resolve에서 실제 결과와 diff를 확인한다.
3. **Editor 자동 Resolve/자기 삭제:** 앞 절의 팀 코드 보호 방안을 먼저 합의한다.
4. **UI 리소스:** uGUI 2.0.0 내장 경로에서 `Runtime/TMP`와 `TMP Essential Resources.unitypackage`를 확인했다. 별도 TextMeshPro 패키지가 manifest에 없다는 이유만으로 새 패키지를 설치할 필요는 없다. 프로젝트에는 필수 리소스/한글 Font Asset이 아직 없다. PHASE 3에서 필요한 필수 리소스와 사용권 확인된 한글 폰트만 준비한다. [uGUI 2.0 TMP 문서](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html).

### 전이 의존성과 추가 패키지

lock에는 Pixel Perfect 5.0.3, 2D Animation 10.1.4, Sprite 1.0.0, PSD Importer 9.0.3, SpriteShape 10.0.7, Tilemap Extras 4.1.0, Aseprite 1.1.9 등이 있다. Core/ShaderGraph는 17.0.4다. AI Inference 경로의 App UI 1.3.6, Collections 2.5.1, Burst 1.8.21, Newtonsoft JSON 3.2.1 등도 있다. 존재 기록과 실제 Resolve 성공을 구분한다.

Pixel Perfect 5.0.3은 공식 메타데이터상 최소 2021.1이며 lock에 이미 있다. 신규 설치 후보가 아니라 기존 의존성 활용 후보이다. UI까지 강제로 픽셀 그리드에 묶지 않는다. [Pixel Perfect 5.0 문서](https://docs.unity3d.com/Packages/com.unity.2d.pixel-perfect@5.0/manual/index.html).

현재 설계에 DOTween/Cinemachine/Addressables/새 UI 프레임워크는 필수가 아니다. 기본 Coroutine·Animator·uGUI·Input System으로 먼저 구현한다. UI 입력은 `InputSystemUIInputModule`과 기존 UI 액션 맵의 연결을 검토하고, 중복 EventSystem/입력 모듈을 만들지 않는다. [Input System 1.11 UI 문서](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/UISupport.html).

최종 호환 판정은 **미완료**다. 최소 버전 감사에서 Assistant 문제를 발견했으며, 나머지 패키지의 최소 버전 충족/내장 제공 확인은 컴파일·직렬화·빌드 검증을 대신하지 않는다.

## 4. 웹의 실제 모듈과 구현 범위

| 코드 | 실제 책임 | Unity 이식 방향 |
|---|---|---|
| `WebPrototype/js/cards.js` | 27종 카드, 9속성, 3캐릭터 시작 덱, 효과 기반 설명 | CardData/CharacterData와 효과 정의 |
| `js/balance.js` | HP/행동력/보상/적 등급 패턴/연출 시간 | BattleConfig/RewardData. 수치 변경 없이 분리 |
| `js/run-state.js` | RunState, uint32 기반 난수, 셔플, 카드 추가/골드/회복 | 순수 런타임 모델과 SeededRng |
| `js/dungeon.js` | 고정 DAG, 방 유형 셔플, 진입 검증/완료 | DungeonController/노드 데이터 |
| `js/deck.js` | 드로우/손패/버림/소멸 이동 | Deck 런타임 객체 |
| `js/enemies.js` | 구역/12종 적 데이터, Intent 결정·수정·실행 | EnemyData/EnemyState/EnemyIntent |
| `js/battle.js` | 카드 효과, 턴 순서, 전투 잠금, 승패 | BattleRules/BattleController |
| `js/reward.js` | 후보 3장, Gold/Card/Skip, 중복 차단 | RewardController/PendingReward |
| `js/game.js` | 화면 전환, Run 소유, 이전 타이머 무효화 | GameFlow/Scene 전환과 취소 가능한 연출 |
| `js/ui.js` | DOM/HUD/덱/맵/보상/임시 퍼즐·이벤트 | Unity View/Presenter로 재작성 |
| `js/art.js` + 두 manifest | 프레임/FPS/피벗, Canvas FX, 로딩 | Sprite/Animator/EnemyView/FX |
| `tests/` | 규칙·고정 정책 밸런스·브라우저 회귀 | C# EditMode/PlayMode 검증의 근거 |

위 표에서 `js/` 상대 경로는 모두 `WebPrototype/` 아래다.

### 현재 구현과 미구현의 구분

- Title → 3캐릭터 선택 → Map → Battle → Reward → Map 및 보스 보상 후 Run 완료가 있다. HP 0이면 실패/재시작한다.
- 본편 진행 구역은 1구역뿐이다. 2·3구역 배경/적 데이터/갤러리가 있다고 연속 진행이 구현된 것은 아니다.
- 적은 전투당 1개체다. 일반/엘리트/보스가 공통 Battle 구조를 사용한다. 다중 적·자유 타깃 선택은 아직 없다.
- 카드 artwork는 속성 아이콘을 재사용한다. 카드별 고유 일러스트, 희귀도, 명시적 CardType 필드는 없다. UI가 방어 효과와 공격 유무로 방어 카드 틀을 추론한다.
- `upgrade_level`은 인스턴스에 있지만 강화 로직은 없다. `spendGold`는 있지만 실제 상점 구매/제거는 없다.
- 퍼즐은 땅→물→불 버튼 순서의 임시 콘텐츠다. 성공 시 공통 보상, Skip 시 보상 없음이다.
- 이벤트의 shop/altar/rest_site는 배경/표시 하위 유형이다. 모두 임시 회복 +8 또는 Gold +15 선택을 공유한다. 거래·제단 위험·카드 강화는 없다.
- 저장/이어하기, Settings, Exit, 실제 오디오, 보스 전용 페이즈, 치명타, 온라인은 없다.
- 큰 행동력, HP 바/숫자, Intent/툴팁/방어 후 예상 피해, 카드 hover, 피해 숫자와 단순 FX는 웹에 있다. Unity UI/연출이 존재하는 것은 아니다.

## 5. 그대로 계승할 규칙

### 5.1 초기 상태와 수치

- 세 캐릭터 모두 최대/현재 HP 60, Gold **0**, 시작 카드 4개. Gold 50은 초기 재화가 아니라 보상액이다.
- 기본 턴 행동력 3, 임시 획득 상한 6. 다음 턴에는 3으로 돌아온다. 4/3 표시는 임시 행동력이며 최대 HP와 혼동하지 않는다.
- 손패 목표 4장. 실제 덱/소멸 상태 때문에 4장보다 적게 뽑힐 수 있다.
- 첫 일반 HP 38. HP 공식: 등급 기본 HP + `(구역-1) × 구역 보정` + 보스가 아닐 때 `(층-1) × 2`.

| 등급 | 기본 HP | 구역당 HP 보정 | 순환 행동 |
|---|---:|---:|---|
| 일반 | 38 | 4 | 공격6 → 공격8 → 방어5 |
| 엘리트 | 52 | 6 | 공격8 → 공격5×2 → 힘+2 → 공격10 |
| 보스 | 64 | 8 | 공격10 → 방어8 → 공격6×2 → 힘+2 |

1구역 r8 일반은 HP42의 기존 이끼 슬라임, r9 엘리트는 HP56의 질풍 파수꾼, 보스는 HP64의 몰락한 거신이다. 기본 패턴 숫자에 힘/약화가 반영되므로 힘 증가 뒤 공격10은 12가 된다. Unity UI 재설계를 이유로 이 값을 바꾸지 않는다.

### 5.2 카드 기준 데이터

| ID | 이름 | 비용 | 속성 | 순서대로 실행할 효과 |
|---|---|---:|---|---|
| earth_strike | 지반 강타 | 1 | 땅 | 피해5 |
| stone_guard | 바위 방벽 | 1 | 바위 | 방어5 |
| wind_cut | 돌풍 베기 | 1 | 바람 | 피해4 |
| fire_bolt | 화염탄 | 1 | 불 | 피해5 |
| ice_guard | 빙결 장막 | 1 | 얼음 | 방어5 |
| lightning_strike | 낙뢰 | 1 | 번개 | 피해5 |
| water_wave | 정화의 파동 | 1 | 물 | 피해4, 회복1 |
| earth_blessing | 대지의 가호 | 1 | 땅 | 방어5 |
| wind_judgment | 심판의 바람 | 1 | 바람 | 피해5 |
| earth_crush | 대지 분쇄 | 2 | 땅 | 피해13 |
| stone_retaliation | 반석의 반격 | 1 | 바위 | 피해4, 플레이어 방어도 보유 시 추가5 |
| wind_read | 바람 읽기 | 0 | 바람 | 드로우1, 소멸 |
| water_stream | 수맥의 흐름 | 1 | 물 | 피해3, 드로우1 |
| water_rest | 치유의 물결 | 0 | 물 | 회복3, 방어2, 소멸 |
| ice_shard | 얼음 파편 | 1 | 얼음 | 피해5, 약화2턴 |
| ice_link | 빙결 연계 | 1 | 얼음 | 피해4, 적의 직전 속성이 물이면 추가7 |
| poison_touch | 독의 손길 | 1 | 독 | 피해2, 독3 |
| poison_sting | 맹독 찌르기 | 1 | 독 | 피해5, 적에게 독이 있으면 추가5 |
| poison_mist | 독안개 | 1 | 독 | 독3, 약화1턴 |
| fire_inferno | 열화 폭격 | 2 | 불 | 피해14 |
| fire_strength | 화로의 힘 | 1 | 불 | 플레이어 힘+2, 방어3 |
| lightning_chain | 연쇄 번개 | 1 | 번개 | 피해3×2 |
| lightning_charge | 전하 축적 | 0 | 번개 | 행동력+1, 소멸 |
| explosion_blast | 폭발 일격 | 2 | 폭발 | 피해15 |
| explosion_link | 점화 연계 | 1 | 폭발 | 피해5, 적의 직전 속성이 불이면 추가8 |
| explosion_barrier | 충격 방벽 | 1 | 폭발 | 방어7, 피해2 |
| stone_fortress | 돌의 요새 | 2 | 바위 | 방어12 |

전사 시작: earth_strike/stone_guard/wind_cut/wind_read. 마법사: fire_bolt/ice_guard/lightning_strike/lightning_charge. 사제: water_wave/earth_blessing/wind_judgment/water_rest. 위 수치는 이식 기준이며 최종 밸런스 확정이 아니다.

### 5.3 계산 및 턴 순서의 세부 조건

1. 카드 사용은 플레이어 턴·전투 active·busy 아님·손패 인스턴스 존재·비용 충분일 때만 허용한다. 명령 접수 시 비용을 한 번 차감하고 해당 인스턴스를 버림/소멸로 옮긴다.
2. 효과 배열의 순서를 보존한다. 기본 피해의 각 타격에는 플레이어 힘이 더해진다. **조건부 추가 피해에는 현재 웹 기준으로 힘이 추가되지 않는다.** 각 타격은 적 방어를 먼저 소모하고 남은 HP를 넘게 감소시키지 않는다.
3. 조건부 속성 연계는 카드 사용 전 적의 속성을 캡처해 판정한다. damage/poison/weak 효과가 있는 카드만 이후 적의 속성을 갱신한다. 방어/회복/버프 카드만 사용해서 속성 태그가 바뀌면 안 된다.
4. 회복은 최대 HP까지, 약화는 기존/신규 지속시간 중 큰 값, 독은 누적, 힘은 전투 내 누적이다. 약화는 퍼센트가 아니라 **각 적 공격 -3**이다.
5. 턴 종료 시 현재 Intent의 힘/약화가 반영된 값을 고정하고 손패를 버린 뒤 독을 처리한다. 독은 적 방어를 무시하여 현재 독 수치만큼 HP 피해를 주고 1 감소한다. 독으로 적이 죽으면 적 행동 없이 승리한다.
6. 적 공격은 `max(0, 기본 공격 + 적 힘 - 약화 적용 시 3)`을 타격 수만큼 실행한다. 방어는 타격마다 남은 방어도에서 차감한다. 예고 공격력과 방어 후 HP 피해는 다른 값이다.
7. 적의 기존 방어도는 적이 다음 행동을 실행할 때 초기화된다. 새 방어 행동으로 얻은 방어는 그 다음 플레이어 턴에 유효하다.
8. 적은 고정 순환 패턴을 사용한다. 행동 뒤 약화 지속시간을 1 줄이고 패턴 인덱스를 진행한다. 방어/힘 행동에서도 약화 시간이 줄어든다. 예고를 보여준 뒤 다시 랜덤 선택하지 않는다.
9. 플레이어 사망을 판정한 뒤 생존 시 방어를 0으로 초기화하고 행동력3/다음 손패/다음 Intent를 준비한다.
10. 전투 승리 처리는 한 번만 한다. 보상 처리가 끝나기 전에는 해당 방을 완료하거나 맵으로 진행하지 않는다. 보스도 보상을 처리한 뒤 Run 완료가 된다.
11. 웹의 330/800/400/900/650ms는 카드 타격/카드 종료/적 타격/적 종료/승리 대기 연출 시간이다. Unity에서 연출 시간은 조정 가능하지만 효과 적용 횟수·순서는 바꾸지 않는다. Animation Event 누락으로 규칙 처리가 사라지지 않는 구조가 필요하다.

### 5.4 덱·보상·Run 유지

- 영구 덱의 카드 ID와 인스턴스 ID를 분리한다. 전투 piles(draw/hand/discard/exhaust)는 새로 만들고 전투 간 영구 덱을 보존한다.
- 드로우가 비면 버림을 셔플한다. 소멸은 전투 한정이며 다음 전투에는 영구 덱에서 다시 들어온다. 버림/소멸 이동 중 동일 인스턴스를 복제하지 않는다.
- 보상은 Gold +50 / 카드 후보 3장 중 1장 / Skip 중 하나다. 카드 후보 화면으로 들어가는 것만으로 카드를 지급하지 않는다.
- 후보는 해당 캐릭터의 시작 카드 ID만 제외한 공통 풀에서 고른다. 구역/직업/희귀도 필터는 없다. **후보 3장의 ID는 서로 다르지만 이미 보유한 보상 카드와 같은 카드 종류를 다시 획득할 수 있다.** 이를 임의로 금지하지 않는다.
- 보상 ID는 Run+방에 묶이고 resolved flag, resolved_reward_ids, active_room_id 검사로 중복 지급을 막는다. Unity에서는 검증→적용→방 완료를 하나의 원자적 처리로 유지한다.
- HP/Gold/덱/획득 카드/현재 위치/완료 방/Run 상태는 Scene을 바꿔도 유지한다. 새 게임/재시작은 명시적으로 초기화한다. 페이지 새로고침에 해당하는 앱 재시작 후 복구는 미구현이다.

### 5.5 맵과 난수

시작 포함 11노드이고 콘텐츠 방은 10개(전투5/퍼즐2/이벤트2/보스1)다. r9 엘리트도 전투5에 포함된다. 이 비율은 **맵 전체의 노드 수**이며 한 경로에서 10방을 모두 방문한다는 의미가 아니다. 실제 경로는 r1→중간 노드2개→r8/r9→보스의 5개 콘텐츠 방이다.

연결은 고정: start→r1, r1→r2/r3/r4, r2→r5/r6, r3→r5/r6/r7, r4→r6/r7, r5→r8, r6→r8/r9, r7→r9, r8/r9→boss. Seed는 중간6노드 방 종류를 섞는다. 상점/제단/휴식처는 이벤트 하위 유형으로 표시하고 독립 노드를 추가하여 기존 비율을 바꾸지 않는다.

현재 위치에서 연결된 미완료 방만 이동 가능하다. 콘텐츠 진행 중에는 다음 방으로 진입할 수 없다. 보스는 마지막 노드이며 완료/실패한 Run에서는 새 이동이 없다. 현재/완료 상태는 겹칠 수 있다.

| 난수 용도 | 실제 Seed 입력 |
|---|---|
| 중간 방 종류 | seed |
| 이벤트 하위 유형 | seed + 5801 |
| 전투 덱 셔플/재셔플 | seed + 완료 방 수×211 + room.column |
| 카드 후보 | seed + map 노드 인덱스×719 |
| 적 행동 | 난수 없음. 고정 패턴 |

C# 이식 시 JS uint32 변환·Math.imul/bit shift의 의미를 `unchecked` 32비트 연산으로 맞추고 Fisher–Yates 셔플 순서를 보존한다. UnityEngine.Random의 전역 상태로 대체하여 재현성을 깨뜨리지 않는다.

전사, 첫 방 r1 기준 이번 분석에서 얻은 비교 fixture:

| Seed | 첫 손패 순서 | 첫 카드 보상 후보 |
|---:|---|---|
| 0 | wind_read, stone_guard, earth_strike, wind_cut | poison_mist, explosion_link, water_rest |
| 17 | earth_strike, wind_cut, wind_read, stone_guard | lightning_charge, earth_blessing, water_stream |
| 29 | stone_guard, earth_strike, wind_cut, wind_read | lightning_charge, ice_link, poison_sting |
| 4294967295 | wind_read, wind_cut, earth_strike, stone_guard | poison_sting, wind_judgment, fire_bolt |

## 6. Migration Audit A B C

| 분류 | 유지하거나 재구성할 내용 | 범위 |
|---|---|---|
| A 규칙 계승 | 위 카드 수치/효과 순서, 행동력, 덱, 상태이상, Intent, 보상 중복 차단, 맵 연결, Run 유지 | C#으로 다시 구현하되 의미 유지 |
| B Unity 재구현 | GameFlow, Scene 수명, UI/HUD, Animator/FX, Prefab, ScriptableObject, SeededRng, 런타임 데이터, 테스트 | 웹 객체/DOM/timer를 복사하지 않음 |
| C 일반 게임에서 제외 | 상단 개발 Navigation, 에셋 갤러리, 촬영 버튼/F 단축키, Debug URL/Seed 입력, 브라우저 note/링크/페이지 레이아웃 | 웹 원본에서 삭제하지 않고 Unity 플레이 화면에 이식하지 않음 |

현재 덱 확인 기능은 C가 아니다. 개발 Navigation에 있는 **표현 방식**을 제거하고 실제 게임의 덱 아이콘/모달로 유지한다. 새 게임/재시작도 기능은 유지하되 타이틀/메뉴에 배치한다. Seed 확인·에셋 검수는 필요하면 Editor 도구/Development Build 전용 DebugPanel로 옮긴다.

## 7. 재사용 가능한 에셋

실제 위치는 `WebPrototype/Assets/BRODUNArt/`다. 총 386파일: PNG365, SVG20, JSON1. 별도 `WebPrototype/manifest.json`, `manifest.js`, `addon-manifest.js`가 원본 프레임/FPS/피벗 정보를 제공한다. 두 manifest의 이미지 참조와 316개 분리 이미지 크기를 검사했고 누락/크기 불일치는 0이었다.

| 분류 | 실제 파일 수/내용 | Unity 재사용 판단 |
|---|---|---|
| Backgrounds | PNG4: 타이틀+3구역, 1672×941 | 배경 Sprite/UI Image. 레이어/타일맵 원본은 아님 |
| Frames | PNG316: 주인공3+기존 적3의 120프레임, 신규 적9의 180프레임, 아이콘16 | 개별 Sprite 권장. 애니메이션 이미지와 아이콘 구분 |
| Sprites | PNG15: 기존6+신규9 시트 | 원본/검수용. 동일 그림을 다시 슬라이싱해 중복 임포트할 필요 없음 |
| EventRooms | PNG3: shop/altar/rest_site | 배경 준비. 해당 게임 기능 구현을 뜻하지 않음 |
| UI | PNG27+SVG20=47 | 버튼4상태, 카드 틀, HP, 노드, 선택 틀, 방/골드 아이콘 |
| animation_manifest.json | JSON1 | 신규 프레임/FPS/피벗 메타데이터 |

| 구역 | 일반 / 엘리트 / 보스 | 각 종 실제 프레임 |
|---|---|---:|
| 1 | ruin_gargoyle / gale_warden / fallen_colossus | 5동작×4=20 |
| 2 | venom_eel / frost_seer / abyssal_leviathan | 각20 |
| 3 | ember_gremlin / coil_golem / foundry_tyrant | 각20 |

새 몬스터9종 합계180장. 기존 이끼 슬라임 등 3종도 남겨 둔다. 첫 1구역 슬라이스는 주인공3종과 가고일/슬라임/파수꾼/거신의 적4종을 우선 검증한다.

| 동작 | FPS | Loop | Unity에서 요구하는 종료 |
|---|---:|---|---|
| Idle | 6 | 예 | 기본 대기 |
| Walk | 8 | 예 | 등장/접근 연출 전용, WASD 추가 금지 |
| Attack | 10 | 아니오 | 공격 후 Idle |
| Hurt | 10 | 아니오 | 피격 후 Idle |
| Death | 7 | 아니오 | 마지막 프레임 유지, Idle 복귀 금지 |

### 임포트 및 품질 전략

- 원본 웹 에셋을 이동/삭제하지 않는다. 향후 승인된 단계에서 필요한 PNG와 메타데이터를 새 `Assets/BRODUN/Art/` 하위에 선별 복사하고 Unity가 생성한 `.meta`를 함께 관리한다. 기존 `Assets/Settings`와 GUID는 보존한다.
- Sprite/Point/Compression None/MipMap Off를 기본 후보로 삼되 기존 TextureImporter를 덮어쓰는 전역 Postprocessor는 만들지 않는다. UI와 캐릭터/배경 프리셋을 구분한다.
- 프레임은 가변 크기다. 개별 PNG에는 sheet의 left/top 절단을 다시 적용하지 않는다. `pivotX`, `pivotY`를 읽고 동일 PPU/발 앵커로 등록한다. pivotY=0을 임의로 중앙 피벗으로 바꾸지 않는다.
- PPU 128은 기존 안내의 후보값이며 아직 Unity에 적용된 설정이 아니다. 일관된 PPU와 배우별 시각 스케일을 검증하고 UI/월드 비율을 Unity 화면에 맞춰 재설계한다.
- 360×560 세로 map_choice 틀은 원형 맵 노드에 늘려 붙이지 않는다. 필요하면 상세 선택 카드에 종횡비를 유지하여 쓰고 기본 맵은 아이콘/연결선/위치 마커로 구성한다.
- SVG는 편집 원본이다. SVG 직접 임포트를 위해 추가 패키지를 설치하지 않고 이미 있는 PNG를 우선 사용한다.
- 엄격한 32/64px 도트 규격이 아닌 생성형 픽셀 스타일이고 프레임 변형이 있다. Point만으로 최종 픽셀 정합성을 보장할 수 없다. 정수 배율/Pixel Perfect와 실제 해상도·움직임을 확인한다.
- 별도 오디오/폰트 파일은 현재 아트 폴더에 없다. 전용 카드 일러스트, 모든 Intent/상태 아이콘, 레이어별 타이틀 배경도 부족하다. 없는 자료를 있다고 가정하거나 PHASE 1에서 새로 만들지 않는다.
- 예전 안내서의 BRODUN 자동 임포터 C#은 현재 팀 저장소에 없다. 나중에 원본 ZIP의 보조 도구를 가져온다면 두 매니페스트와 적용 폴더/피벗 정책을 통합 검토한다. 지금 존재하는 임포터 충돌로 잘못 보고하지 않는다.

## 8. 웹과 Unity의 기능 차이

| 영역 | 웹 현재 | Unity 현재 | 목표 |
|---|---|---|---|
| 시작/선택 | 동작, 3캐릭터 | 없음 | 정식 Title/선택 UI |
| Map | 고정 DAG·시드 방 종류·진입 검증 | 없음 | 규칙 유지, 전체 화면 노드 맵 |
| Battle | 1적, 3등급 공통 | 없음 | 공통 BattleScene/Controller |
| Card/Deck | 27정의, 4 piles, 3시작 덱 | 없음 | SO 원본+인스턴스/Deck 분리 |
| Intent | 패턴 저장, 힘/약화 반영, 확정 실행 | 없음 | 머리 위 Intent·툴팁·실행 일치 |
| Reward | Gold/Card/Skip·중복 차단 | 없음 | 실제 카드 선택 UI/골드 피드백 |
| Run | 메모리 중앙 상태 | 없음 | Boot 수명의 런타임 객체 |
| Animation | JS 프레임 재생·Canvas FX | 기본 2D 카메라/조명만 | Sprite/Animator/FX/피격 연출 |
| Event/Puzzle | 임시 콘텐츠 | 없음 | 기존 수준을 명확히 표시한 최소 처리 또는 승인된 범위 조정 |
| Save/Continue | 없음 | 없음 | 이번 슬라이스에서 최종 저장 제외, Continue 비활성 |
| Audio/Fonts | CSS 시스템 폰트, 실제 SFX 없음 | 게임용 폰트/오디오 없음 | 한글 TMP, Audio 연결 지점 준비 |

## 9. 기존 프로젝트를 보존하는 Unity 구조 제안

향후 새 작업은 `Assets/BRODUN/` 아래 Data/Runtime/Presentation/Art/Scenes/Prefabs/Tests/Editor로 격리한다. 지금 이 폴더들을 생성하지 않았다. 기존 SampleScene/템플릿/Renderer/설정/입력 파일은 그대로 둔다. 이후 새 Boot를 Build 시작 씬에 넣는 변경도 PHASE 3에서 명시적으로 검토한다.

### 데이터와 런타임

| 제안 타입 | 책임 | 저장하면 안 되는 것/주의 |
|---|---|---|
| CardData | ID/이름/비용/속성/효과 배열/아트/표현용 종류 | 손패 위치, 현재 비용 등 런타임 상태 |
| CharacterData | 캐릭터 ID/시작 덱/HP/행동력/시각 참조 | 현재 HP/Gold |
| EnemyData | ID/등급/구역/패턴/외형 | 현재 HP/약화/Intent 인덱스 |
| BattleConfig/RewardData | 현재 밸런스/보상/연출 설정 | UI의 하드코딩 수치 |
| Room/Area 정의 | 기존 연결·방 유형·이벤트 하위 유형 | 현재 방문/완료 여부 |
| CardInstance | InstanceId/CardId/upgrade_level | SO 원본 변형 금지 |
| RunState | SchemaVersion/RunId/Seed/선택 캐릭터/HP/MaxHP/Gold/영구 덱/획득 인스턴스/구역·층·현재 노드/완료 노드/active room/보상 처리 ID/방 결과/Run 상태 | Scene GameObject/View/UnityEngine.Random 전역 상태 |
| BattleState/EnemyState | piles/행동력/방어/힘/상태이상/턴/전투 단계/저장한 Intent/실행 스냅샷 | 다음 방 진입 시 이전 전투 상태 재사용 금지 |
| PendingReward/EnemyIntent | 확정 후보와 처리 여부/확정 행동과 타격 수 | UI에서 후보·행동 재추첨 금지 |

ScriptableObject는 정의용으로 읽기만 한다. Unity 직렬화용 리스트/DTO와 런타임 Dictionary/Set을 구분한다. 최종 저장은 미구현 범위이며 schema_version은 확장 준비일 뿐 Save 기능의 증거가 아니다. 새 rarity/upgrade/card targeting 규칙을 UI 제작 중 임의로 추가하지 않는다.

### 제어와 표시

- Boot의 단일 Composition Root가 GameFlow와 현재 Run을 소유한다. 모든 클래스에 Singleton/Manager를 만들지 않는다.
- 순수 C# 규칙: RNG/Deck/Damage/Intent/Reward 검증. Unity MonoBehaviour 제어: Scene 이동과 전투 연출 스케줄. View: 입력을 명령으로 전달하고 상태를 표시한다.
- GameFlow의 진입/보상 미해결 가드를 유지한다. 전환마다 취소 토큰/세대 번호로 이전 연출을 무효화하여 새 Run에 오래된 피해가 적용되지 않게 한다.
- 한 BattleScene/공통 Enemy Prefab에 EnemyData/BattleConfig를 주입한다. 일반/엘리트/보스별 별도 전투 시스템은 만들지 않는다.
- Enemy Prefab은 Root의 EnemyView/상태 연결, Visual 자식의 SpriteRenderer/Animator, HP/Intent/Status용 World UI 앵커로 나눈다. 비주얼 접근 이동이 논리 타깃/루트 수명과 섞이지 않게 한다.
- 카드 명령은 유효성 검사→한 번 접수→한 번 효과 적용→연출 종료/잠금 해제 순서를 가진다. 효과 적용 지점은 제어 계층이 보장하고 단순 Animator 상태 존재 여부에 의존하지 않는다.
- Audio는 UI Hover/Click, Card Play, Hit, Enemy Attack, Victory, Reward의 연결 지점만 준비한다. 실제 클립이 없으면 무음으로 안전하게 동작하며 임의의 SFX를 생성하지 않는다.

### Scene과 화면 상태

Boot/Title/CharacterSelect/DungeonMap/Battle/Reward의 6씬을 기본 제안으로 한다. Defeat/RunComplete/Deck 확인은 View 상태/오버레이로 먼저 처리하고 새 씬을 과도하게 추가하지 않는다. Event/Puzzle은 기존 임시 규칙을 유지하는 간단한 콘텐츠 View로 시작할 수 있으며 상점 등 전용 시스템은 후속 범위다.

Boot 객체와 EventSystem은 중복 생성되지 않아야 한다. Scene 직접 실행 시 조용히 다른 Run을 새로 만드는 대신 Bootstrap 경로/개발 전용 진입을 명확히 한다. Scene Load 실패 시 입력 잠금과 화면 덮개를 복구하고 오류를 보고한다.

## 10. UI UX 재설계 방향

uGUI 2.0.0 + TMP + 기존 Input System을 우선 제안한다. 기존 UI 코드는 없어 UI Toolkit과 충돌할 구조도 없으며, 이미 있는 uGUI를 이용하면 카드 Transform/손패 부채꼴/월드 HP·Intent와 연결하기 쉽다. 이는 설계 선택이며 이번에 UI를 만든 것은 아니다.

- 전체 16:9, Canvas Scaler 1920×1080 기준. 1280×720/1920×1080/2560×1440과 16:10을 검증하고 HUD/카드/툴팁의 겹침을 확인한다.
- 다크 던전·석재/금속·금색 강조·청록 보조를 유지한다. 기본 Unity Button 외형이나 웹 상단 툴바를 그대로 쓰지 않는다.
- Title: 게임 시작, 동작 가능한 Settings/Exit, 저장이 없을 때 비활성 Continue. 없는 이어하기를 활성 버튼으로 제공하지 않는다. 평면 배경이라는 제약 안에서 미세한 불빛/먼지/안개 연출을 추가 검토한다.
- 선택: 각 캐릭터의 실제 HP60/행동력3/시작 덱 특징과 애니메이션을 표시한다. 선택 강조와 확정 피드백을 분리한다.
- Map: 아이콘/경로선/현재 마커 중심. Available/Locked/Selected/Completed와 hover를 구분한다. 위치 이동 표시는 현재/진입 중/완료를 혼동하지 않게 한다. 전환은 0.2~0.6초 목표로 입력 중복을 차단한다.
- Battle: 왼쪽 플레이어, 오른쪽 적, 중앙 공격 공간, 하단 부채꼴 손패, 독립 Energy와 턴 종료. HP/행동력/손패 비용/적 HP·Intent/턴/Gold를 1초 안에 식별할 수 있는지 실제 사용자 확인 항목으로 둔다.
- 카드 hover 시 확장·앞으로 정렬하고 카드 수에 따라 간격을 조절한다. 툴팁이 다른 카드 입력을 가리지 않도록 한다. 단일 적 대상부터 구현하되 공격 가능 대상 강조를 제공한다.
- 공격 FX→Hurt/Flash→피해 숫자→HP 변화의 관계를 명확히 한다. HP 후행 바/짧은 약한 카메라 반응은 PHASE 5에서 검증한다. Camera Shake를 위해 새 패키지를 먼저 설치하지 않는다.
- Intent는 피해/방어/힘 및 다중 타격을 구분하고 툴팁에 방어 전 값과 예상 HP 피해를 명시한다. 미구현 적 디버프 Intent를 구현했다고 표시하지 않는다.
- Reward: 카드 자체를 비교하는 후보3장 화면, Skip, Gold 변화/팝업을 제공한다. 골드와 카드가 동시에 지급되지 않도록 입력 처리와 시각 상태를 연결한다.
- 개발용 Seed/갤러리/촬영 기능은 일반 HUD 밖에 둔다. 한글 폰트 fallback/긴 설명/색각 대비와 hover 외 정보 접근도 확인한다.

## 11. PHASE 2부터 6까지 개발 순서 및 완료 기준

각 Phase는 사용자의 별도 진행 지시 후 시작한다. 어느 Phase에서도 별도 요청 없이 commit/push하지 않는다. 이전 Phase의 실패 항목을 다음 Phase의 UI로 가려서 완료 처리하지 않는다.

### PHASE 2 Data Layer

개발 순서:

1. Paul 최신/dirty 상태 재확인. Assistant 최소 버전 문제 및 HubForceResolve 안전 처리에 팀 승인을 받는다. Editor47f1은 고정한다.
2. 승인된 범위에서만 패키지 문제를 처리하고 47f1 최초 Resolve/컴파일 및 변경 diff를 확인한다. 호환 문제를 해결하지 못하면 여기서 중단한다.
3. 새 `Assets/BRODUN/`의 데이터/순수 Runtime/테스트 경계를 만든다. 기존 게임용 코드가 새로 들어왔다면 중복 타입을 만들기 전에 다시 비교한다.
4. Element/효과/적 등급/상태 Enum, CardData/CharacterData/EnemyData/BattleConfig/RewardData와 참조 목록을 작성한다.
5. 기존27카드/3시작 덱/적12종의 정의와 패턴을 입력한다. 첫 슬라이스에 쓰지 않는 구역은 데이터 준비로 표시한다.
6. CardInstance/RunState/BattleState/EnemyState/PendingReward를 정의하고 SO와 분리한다.
7. SeededRng/Shuffle/Deck 및 기본 효과 계산·보상 검증을 순수 C#로 이식한다. 위 fixture와 효과 순서로 비교한다.

완료 기준:

- 47f1 Resolve/컴파일 결과를 실제 기록하고 알려진 패키지 호환 이슈가 남아 있지 않다.
- 데이터 ID 중복/누락, 음수 비용, 잘못된 패턴/시작 덱 참조가 검출된다. 원본 SO는 플레이 중 변경되지 않는다.
- Seed0/17/29/uint.MaxValue의 손패·후보가 fixture와 일치하고, 1,000 Seed의 맵 비율·연결 조건을 C# 테스트로 확인한다.
- 카드 인스턴스 보존, 소멸의 전투 한정 수명, Gold/HP 경계, 조건부 피해/힘/약화/다중 타격·보상 중복 처리가 통과한다.
- 데이터 계층과 테스트만 추가되어 기존 SampleScene이 깨지지 않는다. 이 단계가 완성된 게임 UI나 Play Mode 전투를 의미하지 않는다.

### PHASE 3 Game Flow

개발 순서:

1. 필요한 배경/선택 캐릭터/UI PNG만 전용 폴더로 임포트하고 TMP 필수 리소스·한글 폰트를 준비한다.
2. Boot/Title/CharacterSelect/DungeonMap 씬과 Scene 참조 목록을 새 폴더에 작성한다. SampleScene/템플릿은 유지한다.
3. GameFlow와 Run 소유권, 전환 취소/실패 복구, 단일 EventSystem/UI 입력을 연결한다.
4. 전체 화면 Title과 캐릭터 선택을 구현하고 선택 완료 시 새 Run을 만든다.
5. 고정 DAG와 시드 방 타입, 상태/위치/연결선·덱 확인 UI를 구현한다.
6. 빠른 전환과 중복 클릭 차단을 추가한다. Build 시작 씬 변경은 필요한 항목만 검토한다.

완료 기준:

- Title→CharacterSelect→Map이 실제 Play Mode에서 동작하며 HP60/Gold0/선택 덱/Seed가 일치한다.
- 씬 이동에서 Run이 재생성되지 않고 Boot/EventSystem이 중복되지 않는다. 일반 화면에 Debug Seed/웹 개발 툴바가 없다.
- 잠긴/역방향/완료 노드 클릭이 거부되고, 맵 UI 상태와 실제 진입 가능 상태가 일치한다.
- 아직 Battle이 없는 이 단계에서는 콘텐츠 진입을 명확하게 미구현 상태로 표시한다. 빈 BattleScene으로 이동하거나 가짜 전투 완료를 처리하지 않는다. 선택/맵 흐름까지 독립 실행 가능해야 한다.
- 세 캐릭터, 새 게임 반복, 해상도 변경 시 검은 화면/NullReference/한글 누락이 없다.

### PHASE 4 Battle Vertical Slice

개발 순서:

1. 공통 Battle/Reward 씬과 Player/Enemy/Card/Intent의 기능 중심 Prefab을 만든다. 일반/엘리트/보스를 데이터 주입으로 구분한다.
2. Deck→손패4장, 비용 검증·차감, 10종 효과 유형, 카드 사용 중 잠금과 취소를 연결한다.
3. 적 Intent 결정·표시·수정값 반영·실행 스냅샷·다음 갱신, 독 선처리와 턴/사망 순서를 연결한다.
4. Victory/Defeat/RunComplete를 한 번만 처리하고 PendingReward로 이동한다.
5. Gold/Card3후보/Skip, 덱 목록 갱신·다음 전투 드로우, 방 완료·다음 이동을 연결한다.
6. 1구역 전체 경로가 막히지 않도록 기존 수준의 임시 퍼즐/이벤트 처리만 옮기거나 범위 조정을 먼저 승인받는다. 상점 거래/카드 강화 시스템을 새로 완성하려 하지 않는다.
7. 본편의 가고일/슬라임/질풍 파수꾼/몰락한 거신과 3캐릭터 덱을 공통 구조로 검증한다.

완료 기준:

- Title→선택→Map→Battle→카드/방어→Intent 실행→승리→Reward→Map의 첫 슬라이스를 실제로 완주한다.
- Gold+50, 카드1장, Skip 각각 다른 Run/보상으로 검증한다. 빠른 중복 클릭에도 한 번만 적용된다.
- 힘/약화/방어/다중 타격/독사/소멸과 전체 효과 유형의 웹 대비 C# 결과가 일치한다. 카드 후보에 직업 제한 같은 새 규칙이 섞이지 않는다.
- HP/Gold/덱/획득 카드/현재·완료 노드가 유지된다. 보상 미해결 상태에서 맵 진입이 안 된다.
- HP0/재시작/전투 중 화면 전환에서도 이전 연출이 새 Run을 변경하지 않는다. 보스 보상까지 완료하면 추가 이동이 없다.
- 최소한 Idle/Attack/Hurt/Death와 읽을 수 있는 HP/Intent/Energy가 동작한다. 최종 손패/FX 품질은 다음 Phase에서 다듬는다.

### PHASE 5 Visual Polish

개발 순서:

1. Title/선택/Map/Battle/Reward 전체 화면의 정보 계층과 BRODUN 전용 버튼4상태를 정리한다.
2. 부채꼴 손패·동적 간격·hover 전면 정렬·선택/대상 강조·카드 이동 연출을 연결한다.
3. 가변 크기 Sprite 피벗/PPU/배우 스케일, 공통 Animator/외형 교체, Sorting Layer를 검증한다.
4. 타격 Flash/피해 숫자/HP 후행 바/짧은 FX·카메라 반응을 적용한다. Death는 끝 프레임 유지한다.
5. Map 선택 피드백, 0.2~0.6초 전환, 보상 카드 비교와 Gold 팝업을 완성한다.
6. 한글 폰트·Tooltip·색 대비·해상도 대응을 다듬고 Audio 연결 지점을 준비한다. 없는 SFX는 만들지 않는다.
7. 필요 시 Pixel Perfect/정수 배율을 비교한다. 기존 의존성으로 부족하다는 증거 없이 추가 패키지를 도입하지 않는다.

완료 기준:

- 다섯 주요 화면에 웹 상단 Navigation/중앙 테스트 창/기본 Unity 버튼 외형이 남아 있지 않다.
- 1920×1080/1280×720/2560×1440 및 16:10에서 HUD·카드·Intent·툴팁이 겹치거나 잘리지 않는다.
- 실제 관찰자가 HP/Energy/손패 비용/적 HP·다음 행동/턴/Gold를 빠르게 찾을 수 있는지 확인하고 결과를 기록한다. 자동 테스트만으로 1초 가독성 달성을 주장하지 않는다.
- 모든 사용 몬스터의 Attack/Hurt 후 Idle 복귀, Death 유지, 발 위치 안정, Loop/FPS를 확인한다. Walk는 연출에만 쓰인다.
- 연출을 끄거나 빨리 눌러도 PHASE 4 규칙 결과가 같다. 무음/클립 미지정에서도 오류가 없다.
- 동일 상태의 웹/Unity 화면을 캡처하여 기능 동일성과 표현 개선을 각각 설명한다.

### PHASE 6 QA

개발 순서:

1. 47f1 버전/패키지/참조 무결성을 다시 확인한다.
2. EditMode 규칙과 Seed fixture를 반복 실행하고 PlayMode 전체 흐름을 검증한다.
3. 정상 입력 외에 빠른 중복 클릭, 비용 부족, 카드 중 화면 이동, 로드 실패, 반복 재시작을 검증한다.
4. 해상도/한글/카드 겹침/픽셀 선명도/무음/장시간 플레이와 실제 빌드를 확인한다.
5. 검증 증거, 미완료 항목, 웹 대비 차이를 문서화한다. commit/push/PR은 별도 승인 이후만 한다.

필수 PlayMode 시나리오와 통과 기준:

| 번호 | 시나리오 | 확인 결과의 기준 |
|---:|---|---|
| 1 | Title→선택 | 입력 한 번에 선택 화면, 중복 부팅 없음 |
| 2 | 선택→Map | 캐릭터/HP/덱 초기값, 검은 화면 없음 |
| 3 | Map→Battle | 연결된 방만 진입, 정확한 적/등급 |
| 4 | Draw | 손패/버림/소멸 인스턴스 보존 |
| 5 | 카드 사용 | 유효 명령만 한 번 처리 |
| 6 | Energy | 즉시 비용 차감/다음 턴3/임시 상한6 |
| 7 | Enemy Damage | 효과 순서·방어·힘·조건부 피해 일치 |
| 8 | Hurt | 타격과 피격 모션 연결, 이후 Idle |
| 9 | Intent | 단일/다중/방어/힘, 툴팁/예상 피해 일치 |
| 10 | End Turn | 손패 버림/독 선처리/중복 입력 차단 |
| 11 | Enemy Action | 표시했던 스냅샷 실행, 이후 다음 예고 |
| 12 | Player HP | 방어 흡수 후 HP 감소, HP0 실패 |
| 13 | Victory | 직접 공격/독사 모두 한 번만 승리 |
| 14 | Reward | 미해결 상태 진행 차단, 후보3개 고유 ID |
| 15 | Gold | +50 실제 적용·HUD/팝업, 중복 지급 없음 |
| 16 | Card | 선택 후보만 1장 추가, Skip은 무지급 |
| 17 | Deck | 목록에 즉시 반영, 이후 전투 실제 드로우/사용 |
| 18 | Map 복귀 | 완료 표시/다음 노드 활성, 보스는 Run 완료 |
| 19 | Run 유지 | 선택 캐릭터/HP/Gold/덱/맵 상태 보존 |

최종 호환 완료 기준은 47f1에서 실제 프로젝트 열기/컴파일/PlayMode/빌드가 성공하고 Missing Script/NullReference/MissingReference/직렬화·패키지 참조 문제가 해결된 상태다. Sprite/Animator/SO 참조 누락 0, ProjectVersion 47f1 유지, 기존 팀 파일의 의도치 않은 삭제 0을 확인한다. 오류를 보지 못한 상황과 실제 Console Error 0 확인을 구분한다.

## 12. 위험과 다음 단계 진입 조건

| 우선도 | 위험 | 다음 조치 | 현재 상태 |
|---|---|---|---|
| P0 | Assistant 최소 60f1, 기준47f1 | 팀 승인으로 패키지에 한정한 해결책 확정, Editor 업그레이드 금지 | 문서화만, 미해결 |
| P0 | HubForceResolve 자동 Resolve/자기 삭제 | 팀 코드 안전 처리 합의 후 최초 Editor 실행 | 미실행/원본 보존 |
| P1 | UPM 선언/lock 차이 및 전이 의존성 | 47f1의 실제 Resolve/compile diff 검토 | 내장 버전 일치까지만 확인 |
| P1 | JS/C# RNG·효과 순서 차이 | uint32 fixture/골든 규칙 테스트 | 웹 기준 fixture 확보 |
| P1 | Scene 전환 시 상태/오래된 연출 | Boot 소유 Run, 명령 중복/취소 검사 | 설계 제안 |
| P1 | 한글 TMP 리소스/폰트 부재 | 필수 리소스·폰트 사용권/글리프 확인 | 미임포트 |
| P1 | 가변 PNG 피벗/죽음/루프 | 제한된 임포터·Animator 검증 | 원본 메타데이터 조사 |
| P2 | 이벤트/3구역/저장이 이미 완성됐다고 오해 | 임시/준비/미구현 범위를 UI·보고서에서 분리 | 문서에 명시 |
| P2 | 평면 배경·카드별 아트/오디오 부족 | 기존 자료 우선, 새 제작 범위는 별도 합의 | 자료를 임의 생성하지 않음 |

PHASE 2 진입은 사용자의 지시와 P0 처리 방안의 승인 이후다. 지시만 받고 버전 충돌을 숨긴 채 게임 코드부터 쌓지 않는다. PHASE 3은 데이터/기본 컴파일 통과 뒤, PHASE 4는 시작·선택·맵 안정화 뒤, PHASE 5는 전투 루프 검증 뒤, PHASE 6은 기능·표현을 함께 확인할 수 있을 때 진행한다.

## 13. 이번에 실제 수행한 검증과 미수행 항목

| 항목 | 결과 |
|---|---|
| remote/Paul/HEAD/원격 최신 비교 | 올바른 저장소, Paul, HEAD=origin/Paul |
| ProjectVersion/설치 실행 파일 읽기 | 47f1 일치, 실행 파일 존재 |
| 기존 Scene/C#/Prefab/SO/UI 조사 | 기본 씬2, Editor 코드1, BRODUN 게임 구현 없음 |
| manifest/lock/내장 패키지/공식 최소 버전 비교 | Assistant 최소 버전 문제, URP/Test 내장 일치 확인 |
| 두 에셋 manifest 경로/316 분리 이미지 크기 | 누락0/크기 불일치0 |
| 웹 규칙 테스트 재실행 | `node WebPrototype/tests/model.cjs`: PASS **41,876 assertions**, 1,000 Seed, 카드27, 캐릭터3, 적12, Addon180프레임 |
| Unity Editor/Package Resolve/컴파일/PlayMode/빌드 | **전부 미수행** |
| 오늘 브라우저 플레이/스크린샷 비교/영상 새 촬영 | 미수행. 이전 웹 시연/회귀 결과는 참고 기록일 뿐 Unity 검증 아님 |
| 새 Scene/Prefab/SO/C#/Package/아트 | 없음 |
| 수정/삭제한 기존 파일 | 없음 |
| 이번 산출물 | 이 분석 문서 하나 |
| commit/push/PR/merge/main 수정 | 없음 |

분석용 테스트는 파일을 생성하지 않는 기존 규칙 테스트만 실행했다. 브라우저 테스트의 캡처/결과 폴더나 Unity Library는 새로 만들지 않았다. 이 문서는 아직 Git에 stage/commit하지 않은 로컬 산출물이다.

## 14. 전체 요구사항과 계획의 대응

| 원문 항목 | 대응 계획 |
|---|---|
| 0, 3~6, 37, 43~44 | 기능 계승/표현 재설계, A/B/C, 프로젝트 보존, WebView 금지, 규칙 비교 |
| 1~2, 34~35, 46 | 47f1 고정, 패키지 감사/P0, 제한된 임포트·Pixel Perfect, PHASE 2/6 검증 |
| 7~11, 31~33, 41~42 | PHASE 3 시작·선택·UI 기반, PHASE 5 전체 화면/버튼/가독성/개발 UI·Audio 분리 |
| 12~13, 27~28 | 고정 맵 규칙/세부 유형 보존, PHASE 3 흐름·PHASE 5 이동/전환 |
| 14~23, 36 | PHASE 4 공통 전투/Intent/효과, PHASE 5 손패·타격·HP·Animator·Sorting |
| 24~26 | PHASE 2 정의 SO/인스턴스·Run 분리, Composition Root와 명확한 책임 |
| 29~30 | PHASE 4 원자적 보상/덱 성장, PHASE 5 보상 화면/Gold 피드백 |
| 38~40, 48 | 단계별 gate, 1구역 슬라이스 우선, 최종 콘텐츠·저장·오디오 등 유보 |
| 45, 49~50 | PHASE 6의 19 시나리오·호환성·결과 보고, PHASE 5/6 화면 비교 |
| 47 및 추가 지시 | Paul만 대상, main 보존, 현재 문서만 작성, 승인 전 commit/push/다음 Phase 금지 |

## 15. 공식 확인 자료

아래는 2026-10-13 확인한 1차 자료다. 로컬 설정/설치 패키지의 실제 값이 우선이며 다른 Unity 마이너 버전 문서의 기능을 가져오지 않는다.

- [Unity 6000.0.47f1 릴리스](https://unity.com/releases/editor/whats-new/6000.0.47f1)
- [Unity 6.0 패키지 manifest 속성](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-manifestPkg.html)
- [Assistant 공식 레지스트리](https://packages.unity.com/com.unity.ai.assistant) — 2.20.0-pre.1의 unity/unityRelease 확인.
- [Inference 공식 레지스트리](https://packages.unity.com/com.unity.ai.inference)
- [Input System 공식 레지스트리](https://packages.unity.com/com.unity.inputsystem), [1.11 UI 지원](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/UISupport.html)
- [uGUI 2.0 TMP](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html)
- [Pixel Perfect 공식 레지스트리](https://packages.unity.com/com.unity.2d.pixel-perfect), [5.0 문서](https://docs.unity3d.com/Packages/com.unity.2d.pixel-perfect@5.0/manual/index.html)
- 나머지 registry 직접 의존성은 `https://packages.unity.com/<package-id>`의 해당 버전 항목으로 최소 Editor를 확인했다. builtin URP/Core/uGUI/Test/feature2d/UIElements는 설치된 47f1의 `BuiltInPackages/<package-id>/package.json`을 읽었다.

**PHASE 1 종료. 다음 Phase는 착수하지 않는다. 사용자가 PHASE 2 진행을 지시할 때까지 대기한다.**
