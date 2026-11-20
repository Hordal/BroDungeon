# PHASE 1.5 Unity 호환성 및 패키지 정리 결과

2026-10-13 KST. 저장소 `Hordal/BroDungeon`, 브랜치 `Paul`, HEAD `eedba21b4552200874388bdb9f637850b14b3af0`. Editor는 **6000.0.47f1 (2ad1ed33fd3b)**로 유지했다.

## 1. AI Assistant 실제 사용 여부

사용자는 Assistant를 사용하지 않으며 Codex로 개발한다고 명시했다. 프로젝트 Assets/Editor 코드와 Packages/ProjectSettings의 검색에서 Assistant는 패키지 선언/lock 외에 사용되지 않았다. 제거 전 lock 51항목의 역방향 의존성을 조사한 결과 Assistant를 의존하는 다른 패키지는 0개였다. 로컬 내장 패키지와 실제 Resolve 후 PackageCache의 package.json/asmdef/C# 검색에서도 Assistant 패키지 ID/Unity.AI.Assistant 참조는 발견되지 않았다. 단순 텍스트 조사와 의존성 그래프를 실제 컴파일·등록 패키지 확인으로 보완했다.

AI Inference는 Assistant와 별개다. 이번 작업에서는 기존 2.6.1을 제거하거나 변경하지 않았다.

## 2. AI Assistant 제거 여부와 이유

`com.unity.ai.assistant 2.20.0-pre.1`을 manifest와 lock에서 제거했다. 사용하지 않고 코드/역방향 패키지 의존성이 없으며, 공식 메타데이터의 최소 6000.0.60f1이 고정 Editor 47f1보다 높기 때문이다. Editor 업그레이드는 하지 않았다.

lock에서 Assistant만 참조하던 `com.unity.modules.unitywebrequest`의 도달 불가능한 항목도 정리했다. 이는 Editor의 네트워크 기능이나 설치 파일을 삭제한 것이 아니라 더 이상 사용하지 않는 lock 그래프 항목의 제거다.

## 3. 변경된 Package 파일

### Packages/manifest.json

- Assistant 직접 의존성 제거.
- **기존 Mono.Cecil 1.11.5를 명시적으로 고정**: 첫 Resolve에서 Assistant의 1.11.5 제약이 사라져 Unity가 1.11.4를 자동 선택했다. 다른 패키지 버전을 변경하지 않는 요구사항을 지키기 위해 원래 이미 사용하던 1.11.5를 직접 선언하고 재검증했다. 새 버전/새 패키지를 도입한 것이 아니다.
- 다른 기존 직접 의존성의 버전 문자열은 모두 유지했다.

### Packages/packages-lock.json

- Assistant와 도달 불가능해진 unitywebrequest 항목 제거.
- Mathematics는 의존 경로 변경에 따라 depth 1→2, Mono.Cecil은 직접 고정에 따라 depth 1→0.
- 최종 등록/잠금 항목 49개. 남아 있는 패키지들의 **작업 전 대비 버전 변경 0개**.

| 항목 | manifest 유지값 | 실제 등록/lock | 처리 |
|---|---|---|---|
| URP | 17.0.3 | 17.0.4 | 기존 값 그대로, 47f1 내장 버전 일치 |
| Test Framework | 1.4.5 | 1.5.1 | 기존 값 그대로, 47f1 내장 버전 일치 |
| Input System | 1.11.2 | 1.11.2 | 유지 |
| uGUI | 2.0.0 | 2.0.0 | 유지 |
| Pixel Perfect | feature.2d 전이 의존성 | 5.0.3 | 유지, 초기 API 마이그레이션 사항은 아래 기록 |
| AI Inference | 2.6.1 | 2.6.1 | 유지 |
| Mono.Cecil | 1.11.5 명시 고정 | 1.11.5 | 원래 버전 유지 |

## 4. HubForceResolve.cs의 기존 역할

Git 이력상 `6f0111c` 초기 설정 커밋에서 추가된 Editor 코드다. 이름과 주석/구현은 Unity Hub의 일회성 패키지 복구 목적을 가리키지만, 당시 어떤 문제 때문에 작성했는지에 대한 별도 이슈 기록은 없다.

전체 동작:

1. `[InitializeOnLoad]` 정적 생성자가 EditorApplication.delayCall에 Tick을 등록한다.
2. 컴파일/임포트 중이면 다음 delayCall로 미룬다.
3. 세션 상태가 비어 있으면 `Client.Resolve()`를 호출하고 세션 상태를 resolved로 바꾼다.
4. 이후 Tick에서는 자기 파일을 `AssetDatabase.DeleteAsset`로 삭제한다. 해당 .meta도 함께 삭제될 수 있다.
5. Editor 폴더가 비면 폴더와 폴더 .meta도 삭제될 수 있다.

Resolve는 패키지 그래프/lock/cache/컴파일에 영향을 줄 수 있다. 명시적인 삭제 대상은 자기 C# 및 빈 Editor 폴더이며 씬/게임 에셋을 직접 삭제하는 코드는 없다. 그러나 Resolve 요청의 완료 상태를 직접 추적하지 않고 자기 삭제가 컴파일/도메인 리로드를 유발하므로 공유 프로젝트의 반복 시작 코드로 남겨 둘 필요는 없다.

## 5. HubForceResolve.cs에 적용한 처리

삭제 대신 기존 코드 전체를 `#if BRODUN_ENABLE_LEGACY_HUB_FORCE_RESOLVE`로 감싸 기본 비활성화했다. 해당 심볼은 프로젝트에 정의되어 있지 않다. 원본 코드와 파일 경로, 기존 .meta/GUID `0e8e035cfbf7e57419872f71b4c7eda8`는 보존했다.

따라서 평소 Editor 시작에는 자동 Resolve/자기 삭제가 실행되지 않는다. 필요하면 팀 검토 후 원본 동작을 확인할 수 있다. 이번에는 해당 심볼을 켜거나 강제 Resolve 코드를 실행하지 않았다. 일반 Package Manager의 정상적인 시작 Resolve는 그대로 허용했다.

## 6. Unity 6000.0.47f1 Editor 실행 여부

실제 설치된 **47f1 Unity.exe를 batch mode로 실행**했다. 버전 업그레이드/다운그레이드, ProjectVersion 변경은 없었다. 두 번째 상세 검사와 마지막 재실행은 Direct3D 11/NVIDIA GPU에서 진행했다.

씬/패키지/Renderer를 확인하는 임시 Editor 전용 검증 코드를 사용했고, 검증 후 C#과 자동 생성 .meta를 제거했다. 이 코드는 게임 기능이 아니며 최종 프로젝트에 남아 있지 않다. 실제 게임 Play Mode/Player 빌드/수동 화면 품질 검사는 이번 범위에서 수행하지 않았다.

| 실행 | 내용 | 종료 결과 |
|---|---|---|
| 1차 | 최초 Library/패키지 임포트, 씬·Renderer 검사 | 0, 검사 성공. API Updater 처리 이력 있음 |
| 2차 | Mono.Cecil 1.11.5 유지 상태, D3D11, 상세 재검사 | 0, 검사 성공 |
| 추가 시도 | 임시 코드 제거 후 batchmode에 openfile 옵션 사용 | 옵션 비호환으로 실패. 프로젝트 컴파일 오류와 구분 |
| 최종 재실행 | 잘못된 openfile 옵션 제거, 임시 코드 없는 상태 | **0**, 정상 시작/컴파일/종료 |

추가 시도의 실패 원문은 `The "openfile" cmdline option needs user interaction`이다. 이 옵션 조합을 그대로 재사용하지 않는다. 기존 씬 로드 검증은 앞선 EditorSceneManager.OpenScene 검사로 수행했다.

## 7. Package Resolve 결과

성공. 시작 시 정상 Resolve 이후 실제 등록 패키지 목록 요청도 성공했다. Assistant는 등록되지 않았고 최종 49개가 확인되었다. 최종 재실행은 해석된 패키지 캐시를 정상 복원했다. 필수 패키지 누락/버전 충돌로 중단되지 않았다.

URP/Test Framework의 선언/lock 차이는 실제 Editor의 내장 패키지 선택과 일치하여 그대로 유지했다. 게임과 무관한 패키지 전체 업데이트는 하지 않았다.

## 8. Compile Error 개수

**최종 재검사 0개**, C# 컴파일 경고도 0개다.

첫 임포트에서는 Pixel Perfect 5.0.3 변환기의 구형 `UnityEngine.Experimental.Rendering.Universal.PixelPerfectCamera` 참조 때문에 고유 위치 기준 CS0234 8개가 기록됐다. Unity API Updater가 PackageCache의 해당 참조를 현재 `UnityEngine.Rendering.Universal`로 자동 마이그레이션했고 재컴파일이 성공했다. 패키지 버전 변경이나 프로젝트 게임 C# 수동 수정으로 해결한 것이 아니다. 2차/최종 로그에는 해당 오류가 없다.

초기 오류 기록을 삭제하거나 처음부터 오류가 없었다고 보고하지 않는다. 새로운 PC/캐시 없는 최초 임포트에서도 API 마이그레이션 과정은 재확인해야 한다.

## 9. Console Error 및 Warning 요약

- 2차 씬/Renderer 검사 중 Unity 로그 콜백으로 수집한 Error/Exception/Assert/Warning: **0개**.
- 2차/최종 C# compiler Error/Warning: **0/0**.
- 2차/최종 로그의 Serialization 경고/오류, Missing Script, Shader Unsupported 경고: 발견되지 않음.
- 초기 임포트에는 위 CS0234/API Updater 진단과 Terrain Lit Base Pass 관련 Shader Unsupported 경고가 기록됐다. 이후 D3D11 검사에서 재발하지 않았다.
- 모든 시작 로그에는 Licensing Client 서명 검증/토큰·ULF 갱신 관련 오류 문구가 있었다. 이후 entitlement 해석 성공이 기록되고 정상 종료했다. 따라서 **시작 로그 전체에 오류 문구가 전혀 없었다고 주장하지 않는다.** 라이선스/로그인 상태는 이 PC의 환경 진단이며 변경하거나 우회하지 않았다.
- 추가 실행의 openfile 옵션 실패와 크래시 기록도 별도 보존했다. 마지막 옵션 수정 재실행은 종료 코드0이다.

GUI Console 창을 사람이 열어 확인한 검사는 아니다. 실제 Unity Editor의 로그/컴파일 결과 및 Editor API 검사 결과다.

## 10. 기존 Scene 정상 로드 여부

두 씬 모두 실제 Editor API로 열고 로드 상태와 전체 하위 GameObject의 Missing Script를 검사했다. SaveScene은 호출하지 않았다.

| 기존 Scene | 로드 | Root GameObject | Camera | Missing Script |
|---|---|---:|---:|---:|
| Assets/Scenes/SampleScene.unity | 성공 | 2 | 1 | 0 |
| Assets/Settings/Scenes/URP2DSceneTemplate.unity | 성공 | 2 | 1 | 0 |

Graphics/Quality 설정의 활성 파이프라인은 `UniversalRP`, 실제 생성한 Renderer 타입은 `UnityEngine.Rendering.Universal.Renderer2D`였다. 씬·카메라·조명 에셋은 수정하지 않았다. 최종 화면의 시각 품질/게임 연출 검증은 아니다.

## 11. Unity 버전 유지 여부

Editor 런타임 `Application.unityVersion`, 로그의 엔진 버전, `ProjectSettings/ProjectVersion.txt`가 모두 **6000.0.47f1**이다. ProjectVersion 파일은 Git 기준 변경 없음이다.

## 12. 변경된 전체 파일 목록과 생성물 처리

최종 유지한 수정:

1. `Packages/manifest.json` — Assistant 제거, 기존 Mono.Cecil 버전 유지용 명시 고정.
2. `Packages/packages-lock.json` — 제거된 그래프와 필요한 depth 정리. 남은 버전 변경 없음.
3. `Assets/Editor/HubForceResolve.cs` — 원본 보존/기본 비활성화.
4. `UNITY_PHASE1_5_REPORT.md` — 이번 보고서 신규 작성.

기존 `UNITY_MIGRATION_PLAN.md`는 PHASE 1에서 작성되어 이미 untracked 상태였으며 이번에 변경하지 않았다.

정리한 자동/임시 생성물:

- 임시 검증 C#/.meta와 자동 생성 `.vsconfig`는 제거했다. 기존 팀 파일을 삭제한 것이 아니다.
- AI Inference 2.6.1의 AnalyticsDefineManager가 ProjectSettings에 자동 추가한 `SENTIS_ANALYTICS_ENABLED`는 범위 밖 설정 변경이므로 원래 빈 심볼 상태로 복구했다. 기존 사용자 Analytics 설정은 건드리지 않았다. 해당 패키지가 다음 Editor 시작에 심볼을 재추가할 수 있다는 점은 남아 있다. 이 동작을 영구 변경하려면 별도 범위 검토가 필요하다.
- Editor가 바꾼 ProjectSettings 줄바꿈은 의미가 같음을 검사한 뒤 기존 Windows checkout 형식으로 정리했다. 최종 ProjectSettings의 Git diff는 없다.
- Library/Temp/Logs/UserSettings 및 IDE 빌드 캐시는 Editor가 생성한 로컬 파일이며 기존 .gitignore 범위에 있다. Git에 stage하지 않았다.
- 검사 JSON과 네 개 실행 로그는 저장소 밖 `work/phase15/`에 보관했다. 로그에 개인 환경 정보가 있을 수 있으므로 자동 업로드하지 않는다.

게임 데이터, Scene, Prefab, UI, Battle/RunState 코드는 추가하지 않았다. 기존 웹 코드/에셋/씬/.meta 변경·삭제도 없다. commit/push/force push/PR/main 변경은 수행하지 않았다.

## 13. PHASE 2에 진입해도 안전한 상태인지

**현재 PC의 확인 범위에서는 데이터 계층 개발에 진입할 수 있는 환경 기반을 확보했다.** Assistant의 버전 요구 충돌을 제거했고, 원래 다른 패키지 버전을 유지한 상태에서 Resolve·컴파일·기존 씬 로드·2D Renderer 생성이 통과했다. 자동 자기 삭제도 비활성화되어 있다.

다만 이를 모든 PC/모든 빌드/Play Mode가 검증됐다는 뜻으로 확대하지 않는다. 최초 캐시 생성의 API Updater 과정, 시작 시 라이선스 진단, AI Inference의 Analytics 심볼 자동 갱신은 위와 같이 남겨 두었다. 특히 깨끗한 PC의 첫 임포트는 같은 검증을 다시 수행해야 한다.

PHASE 2 구현은 시작하지 않았다. 사용자의 다음 지시까지 멈춘다.
