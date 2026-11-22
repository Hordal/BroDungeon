# BRODUN 전투 배경 팩

일반전·엘리트와 보스전에 사용할 회화풍 PNG 8장이다. 기존 픽셀 배경과 몬스터 에셋은 보존한다.

## 사용

- 기본 게임: 기존처럼 `Assets/BRODUN/Scenes/BrodunBoot.unity` 실행.
- 8장 미리보기: **BRODUN > Art > Battle Background Pack**.
- 위치: `Assets/BRODUN/Resources/BRODUN/Backgrounds/`.
- 전투 배경은 실제 조우한 적의 `Definition.Area`와 방의 보스 여부로 선택한다. 일반전과 엘리트는 normal, 보스는 boss를 사용한다.
- 미지원 구역이나 누락된 리소스에는 기존 `BattlePresentationAssets.background`를 사용한다.

| 기존 구역 번호 | 테마 | 일반전 및 엘리트 | 보스전 |
|---|---|---|---|
| 1 | 잊혀진 회랑 | 01_ruins_normal.png | 01_ruins_boss.png |
| 2 | 침잠의 수맥 | 02_waterway_normal.png | 02_waterway_boss.png |
| 3 | 소멸의 화로 | 03_foundry_normal.png | 03_foundry_boss.png |
| 4 리소스 시안 | 폭풍의 첨탑 | 04_storm_normal.png | 04_storm_boss.png |

**네 번째 테마는 질풍·번개 몬스터에서 확장한 시안이다.** 확정된 네 번째 구역이나 보스 데이터를 대체한다는 의미가 아니다. 4개 중 3개를 방문하는 새 진행 규칙을 이 변경에서 구현하지 않는다. 기본 게임의 현재 구역·몬스터 편성과 YeonJun의 별도 57종 모션 팩을 변경하지 않는다.

## 아트와 임포트

- 각 1672 × 941 PNG. 몬스터·캐릭터·UI 없이 배경만 포함.
- 단일 합성 배경이며 분리 레이어·타일셋은 아님.
- Sprite Single, Bilinear, Clamp, mipmaps off, non-power-of-two scaling off, max size 2048, compression off.
- built-in image_gen 제작. 프롬프트와 제작 명세는 `BackgroundPackSource/`에 보관한다.
- 참고 방향: 림버스 컴퍼니의 어두운 무대감, 리버스 1999의 회화적 분위기, 슬레이 더 스파이어의 전투 가독성.

## 변경 범위

`BattleBackgrounds`는 리소스 선택만 담당하며 전투 수치·난수·런 상태를 변경하지 않는다. `BattleView`는 선택한 배경과 구역 제목을 표시한다. 씬·프리팹의 기존 직렬화 참조를 다시 만들거나 에디터·패키지 버전을 업그레이드하지 않았다.

## 검증

- BRODUN 런타임·에디터 C# 전체를 설치된 Unity 6000.6.2f1 참조 어셈블리로 외부 컴파일: 오류 0, 경고 0.
- PNG 8장의 원본 해시·1672 × 941 크기·리소스 경로·새 GUID 중복을 정적 검사.
- `BattleBackgroundTests`에 4개 테마의 서로 다른 일반/보스 Sprite 및 임포트 설정, 미지원 구역의 fallback을 검사하는 EditMode 테스트 추가.
- Unity batch 검증은 로컬 Unity 라이선스 미활성화로 실행되지 않았다. 실제 Sprite 임포트와 PlayMode, UI·몬스터 겹침 검증은 프로젝트 기준 Unity **6000.0.47f1**에서 확인해야 한다. 원본 프로젝트 버전은 유지했다.
