# BRODUN 웹 프로토타입 — Paul 브랜치

기존 Unity 프로젝트와 독립적인 HTML/CSS/JavaScript 프로토타입이다. 저장소 루트의 `Assets`, `Packages`, `ProjectSettings`와 기존 팀원 파일은 수정하지 않았다. 웹 에셋도 이 폴더 안의 `Assets/BRODUNArt`에만 있다. Unity 에디터에서 이 폴더를 옮기거나 자동 임포트할 필요가 없다.

## 실행

- 이 폴더의 `index.html`을 Chrome 또는 Edge에서 연다. 런타임에 npm 설치나 빌드가 필요 없다.
- 또는 저장소 루트에서 `python -m http.server 8000 --directory WebPrototype` 실행 후 [로컬 게임](http://localhost:8000/)을 연다. 이 방법에는 Python이 필요하다.
- 게임 시작 → 전사/마법사/사제 선택 → 던전 맵 → 연결된 방 → 카드 전투 → 보상 → 덱 성장 → 다음 방.
- 현재 덱 보기, 큰 행동력 표시, 적 Intent, 골드/HP 유지, Addon 몬스터 애니메이션을 포함한다.
- 일반 화면에서는 Seed를 숨긴다. HTTP 실행 시 `?debug=1&seed=17`을 URL에 붙이면 재현 모드로 실행한다.
- 새로고침 후 이어하기, 2·3구역 연속 진행, 완성형 상점/제단/휴식처, Unity 본 구현은 아직 없다.

이 반영은 Paul 브랜치의 소스 백업이며 GitHub Pages/저장소 설정을 변경하지 않는다. 기존 독립 웹 사이트는 [여기](https://paul2341268.github.io/brodun-sprint1-preview/)에 있고, 이 팀 저장소가 별도로 배포됐다는 뜻은 아니다.

## 폴더

- `index.html`, `styles.css`: 화면과 스타일.
- `js/`: GameFlow/RunState/Dungeon/Battle/Deck/Reward/CardData/EnemyData/밸런스/렌더링.
- `manifest.js`, `manifest.json`, `addon-manifest.js`: 실제 에셋 경로·프레임·FPS·피벗 데이터.
- `Assets/BRODUNArt/`: 기존 173개 + Addon 213개 파일. 신규 몬스터 9종 × 20프레임 = 180장.
- `tests/`: 규칙·밸런스·실제 브라우저 클릭 회귀 테스트. 임시 결과와 의존성은 Git에 올리지 않는다.
- [Addon 분석](ASSET_ADDON_ANALYSIS.md), [변경 보고서](SPRINT5_ADDON_REPORT.md), [이전 Sprint 5 기록](SPRINT5_REPORT.md), [기존 에셋 안내](README_사용안내.md).

## 테스트

이 폴더를 작업 디렉터리로 사용한다.

```text
node tests/model.cjs
node tests/balance.cjs
node tests/browser.cjs
```

규칙·밸런스 테스트는 Node.js 기본 모듈만 사용한다. 브라우저 테스트는 Node.js에서 `playwright` 모듈을 찾을 수 있어야 하며 기본 브라우저는 설치된 Microsoft Edge다. Chrome은 `BRODUN_BROWSER_CHANNEL=chrome`, Playwright Chromium은 `BRODUN_BROWSER_CHANNEL=chromium`으로 지정할 수 있다. 테스트 결과는 기본 `test-output/`에 생성되고 무시된다. 외부 결과 경로는 `BRODUN_QA_OUTPUT`으로 지정 가능하다.

현재 게임 검증은 Git 과거 이력에 의존하지 않는다. 과거 검은 화면 재현/변경 전 밸런스 비교는 원래 웹 저장소의 커밋이 있을 때만 추가로 실행한다. 없으면 해당 과거 비교만 SKIP하고 현재 게임 테스트를 계속한다. 선택적으로 `BRODUN_BASELINE_REPO`를 지정하되 개인 PC 경로를 파일에 하드코딩하지 않는다.

이식 시 `js/art.js`에 초기화 가드를 추가했다. 로컬 파일에서 defer 스크립트 로딩 사이에 첫 애니메이션 프레임이 먼저 실행돼도 GameFlow가 준비되기 전에는 그리지 않고 다음 프레임에서 다시 확인한다. 전투 규칙과 에셋은 변경하지 않았다.

원본 ZIP, 압축 해제 작업 폴더, 캡처, 로그, node_modules, 개인 환경 설정, 과거 웹 저장소의 `.git`은 포함하지 않았다.
