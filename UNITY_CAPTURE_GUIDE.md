# BRODUN 과제용 GIF 촬영 준비

## 1. 촬영용 추가 기능

Editor 메뉴 `BRODUN → Capture → Prepare character selection (seed 7)`를 추가했다. 게임 화면에는 개발 버튼/Seed/디버그 UI를 추가하지 않았다. 자동 플레이나 GIF 생성 기능은 아니다. 사람이 마우스로 플레이하고 녹화하기 위한 준비 기능이다.

## 2. Capture Mode 구현

Editor 전용 메뉴가 원본 카탈로그에서 별도 메모리 GameDefinitions를 만든다. 1구역 신규 일반 적의 BaseHP만 이 촬영용 복사본에서 14로 지정한다. 카드/캐릭터/Config/Reward/다른 적은 원본의 불변 정의를 공유한다. 원본 EnemyData/Scene/Prefab/PNG/Package를 덮어쓰지 않는다. Player 빌드에는 메뉴와 Editor 전용 진입 API가 포함되지 않는다.

## 3. ON / OFF와 실행 방법

1. Unity **6000.0.47f1**에서 이 프로젝트를 연다.
2. `Assets/BRODUN/Scenes/BrodunBoot.unity`를 열고 Play한다.
3. Title에서 위 Capture 메뉴를 실행한다. 캐릭터 선택 화면으로 이동한다.
4. Game View 해상도 `Full HD (1920x1080)` / 16:9를 확인한다. Game View에 포인터를 두고 Shift+Space로 확대하면 Editor 패널을 녹화에서 제외하기 쉽다. 이 작업은 녹화 전에 한다.
5. 캐릭터 선택 화면부터 녹화를 시작한다. 아래 시나리오대로 마우스로 플레이한다.

OFF: Play Mode를 종료하면 모든 촬영용 메모리 상태가 사라진다. 같은 Play Mode에서도 Title로 돌아가 일반 `GAME START`를 누르면 원본 카탈로그로 새 Run을 생성한다. 촬영 Run 자체를 진행 중에 일반 밸런스로 바꾸지는 않는다. 다음 촬영은 Title에서 메뉴를 다시 실행한다. 메뉴는 Title/전환 종료 상태에서만 동작한다.

## 4. 촬영용 Seed

- **7**. 기존 랜덤 그래프 생성기를 그대로 사용해 동일한 그래프를 재현한다.

## 5. 촬영용 Character

- **전사**를 직접 클릭. 자동 선택하지 않아 캐릭터 선택 장면이 보인다.

## 6. 촬영용 Encounter

- 시작 노드에서 연결된 아래쪽 첫 일반전 **r2 / 폐허 가고일 / 14 HP**. 엘리트나 다른 방을 선택하지 않는다.
- 일반 적의 공격은 원래 **6 → 8 → 방어 5** 패턴이다. 플레이어 HP 60, 행동력 3, 기본 공격 5/4, 보상 Gold 50 모두 기존 값이다.
- 촬영용 복사본은 해당 Run의 1구역 신규 일반 적에 적용되므로 다른 일반방도 짧아질 수 있다. 이 시나리오에서는 r2 한 방만 촬영한다.

## 7. 예상 촬영 시간

목표 **약 25~30초**, 2턴 전투. 아래는 사람의 연습된 클릭 기준 예상 시간이며 실제 영상 길이를 측정한 결과가 아니다.

## 8. 추천 촬영 순서

| 구간 | 마우스 조작 / 보일 내용 |
| --- | --- |
| 0~3초 | 전사 카드에 Hover 후 선택 |
| 3~6초 | 맵의 아래쪽 첫 금빛 일반 전투방 r2 클릭 |
| 6~9초 | 가고일 HP 14/14, ENERGY 3/3, 공격 6 Intent를 잠깐 보여주고 공격 카드 Hover |
| 9~14초 | `지반 강타`(5) → `돌풍 베기`(4), 적 HP 14 → 9 → 5 / Energy 3 → 2 → 1 |
| 14~18초 | END TURN 클릭. 방어 카드는 사용하지 않는다. 적 공격 6 / 플레이어 HP 60 → 54 / 다음 Intent 8 / Energy 회복 |
| 18~22초 | 2턴 `지반 강타`(5)로 처치. Death → Victory → Reward |
| 22~25초 | 보상 후보를 잠깐 보여주고 `골드 +50` 선택. +50 GOLD / HUD 증가 |
| 25~30초 | 맵 복귀 후 HP 54/60, Gold 50, 완료된 현재 노드와 다음 금빛 연결 노드를 보여주고 종료 |

카드 위치는 매 턴 바뀌므로 이름을 보고 누른다. `바람 읽기`는 불필요하며 위 시나리오에서는 사용하지 않는다. 1턴에 적을 죽이면 적 공격 장면이 빠지므로 반드시 2장 공격 후 End Turn한다. 보상은 Gold가 숫자 변화를 가장 짧게 보여준다. 카드 보상도 선택 가능하나 덱 보기까지 넣으면 영상이 길어진다.

Fade 0.22초씩, 기존 카드/적/Death/보상 피드백 시간은 변경하지 않았다. 급하게 연타하면 연출 중 입력 잠금 때문에 무시될 수 있으므로 연출 후 다음 조작을 한다. GIF에는 Game View만 담고 Console/Editor 메뉴/이 문서는 담지 않는다. OBS를 사용한다면 Game View 영역을 16:9로 잡고 1280×720 출력도 가능하다. 설치/설정 변경/실제 GIF 녹화는 이번 작업에서 수행하지 않는다.

## 9. 실제 마우스 검증

computer-use 스킬을 사용해 Unity Editor에서 직접 마우스로 전체 흐름을 검증했다. 전사 선택 → 맵 → 아래쪽 r2 → Card Hover → 지반 강타/돌풍 베기 → End Turn → 적 공격 → 지반 강타 → Death/Victory/Reward → Gold → 맵 복귀를 확인했다.

실제 수치: Enemy HP **14 → 9 → 5 → 0**, Energy **3 → 2 → 1 → 다음 턴 3**, Player HP **60 → 54**, Intent **공격 6 → 공격 8**, Gold **0 → 50**. 복귀 맵에서 `현재 · ✓ 완료`와 다음 Available 노드가 동시에 표시됐다. 버튼/전환 실패, Hover 흔들림, UI 겹침, 중복 보상은 이 플레이에서 관찰되지 않았다. 중복 보상의 반복 입력 방지는 기존 자동 검사에서도 통과했다. 1920×1080 Game View 설정을 직접 확인했으며 1280×720/1600×900/1920×1080 기존 UI 회귀 검사도 이번에 다시 통과했다.

검증은 장면별 확인을 위해 중간에 멈춘 수동 플레이다. 25~30초짜리 실시간 녹화, GIF 파일 생성, OBS 출력/압축 품질 검증은 수행하지 않았다. 검증 후 Play Mode와 검증용 Editor를 정상 종료해 Capture는 OFF다.

## 10. Compile Error / Warning

C# Error **0**, Warning **0**. 이번 최종 EditMode/PlayMode/실제 Editor 로그 기준이다.

## 11. Console Error / Warning

실제 수동 플레이 후 Console **Error 0 / Warning 0 / Info 1**. Info는 Capture ON 안내이며 Console을 Clear하지 않았다. 시작 로그의 기존 Licensing Client Code 10/토큰 진단은 별도로 남아 있지만 이후 Editor 로드/Play/정상 종료가 성공했다. 시작 로그의 모든 진단이 0이라는 뜻은 아니다.

## 12. 기존 EditMode / PlayMode Test 결과

- EditMode **190/190**: 기존 PHASE 4 188개 + Capture 분리/2턴 승리 검증 2개.
- PlayMode **20/20**: 기존 PHASE 4 19개 + Capture Run 종료 후 일반 새 게임 적 HP **14 → 38** 복원 검증 1개.
- 모두 Failed 0, Skip 0, Unity 실행 exit code 0. 기존 테스트 삭제/Ignore 없음.
- [EditMode XML](C:/Users/user/Documents/Codex/2026-09-22/dl/work/capture/EditMode.xml), [PlayMode XML](C:/Users/user/Documents/Codex/2026-09-22/dl/work/capture/PlayMode.xml), [실제 Editor 로그](C:/Users/user/Documents/Codex/2026-09-22/dl/work/capture/Interactive.log).

## 13. 이번 변경 파일

- 수정: `Assets/BRODUN/Scripts/Presentation/GameFlow.cs` — Editor 한정 촬영 Run 진입과 일반 새 게임 원본 복원.
- 추가: `Assets/BRODUN/Scripts/Editor/CapturePreparation.cs` 및 Unity `.meta`.
- 추가: `Assets/BRODUN/Tests/Editor/CapturePreparationTests.cs` 및 Unity `.meta` — 원본 불변/2턴 스크립트 검사.
- 수정: `Assets/BRODUN/Tests/PlayMode/ScreenFlowTests.cs` — Capture OFF 후 실제 일반 전투 HP 원복 검사 추가.
- 추가: 이 문서 `UNITY_CAPTURE_GUIDE.md`.

## 14. 실제 게임 규칙 변경 여부

실제 게임 규칙·밸런스·원본 SO 변경 없음. Scene/Prefab/에셋/Package 변경 없음. 촬영 Run의 별도 메모리 EnemyDefinition만 사용한다. 원본/캐릭터/카드/패턴/보상 불변과 OFF 복원을 테스트했다. 촬영용 낮은 HP는 실제 최종 밸런스가 아니므로 제출 설명에도 촬영용 Run임을 명시하는 것이 좋다.

## 15. Unity 버전

**6000.0.47f1 (2ad1ed33fd3b)** 유지. Game View Full HD (1920×1080), 16:9 확인. 새 Package 설치/업그레이드 없음.

## 16. Git 상태

Repository `https://github.com/Hordal/BroDungeon.git`, **Paul**. 로컬 HEAD/원격 Paul 모두 `eedba21b4552200874388bdb9f637850b14b3af0`, 원격 main `6f0111cbd683c3155c6e6657434e496cac8f9d0c`로 기존과 동일하다. stage/commit/push/merge/PR/force push 없음. 이전 단계의 미커밋 작업을 보존했다. `git diff --check` 오류 없음.

검증 Editor가 자동 생성한 SceneTemplateSettings.json만 제거했다. 기존 팀원 파일이 아니며 Editor가 재생성 가능한 설정이다. ProjectSettings.asset는 기존 PHASE 4 종료 시와 동일하고 Package 두 파일의 SHA256도 동일하다. 테스트 XML/로그는 저장소 밖 `work/capture`에 보관한다.

**main 브랜치는 수정하지 않았다. PHASE 5는 시작하지 않았다. 촬영 준비만 완료했으며 실제 GIF는 아직 만들지 않았다.**
