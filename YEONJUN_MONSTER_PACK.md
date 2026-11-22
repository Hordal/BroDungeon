# YeonJun · 57종 몬스터 및 전투 모션 추가

## 브랜치와 원본

- 원본: `Paul`의 `2756cc7fbc35ea8de6a115b98fd2fc4e3909cbe9`.
- 작업 및 업로드 대상: `YeonJun`.
- `Paul` 브랜치에는 커밋하거나 푸시하지 않는다.
- 기존 `YeonJun`은 `Paul`의 조상이었으므로 기존 이력을 보존한 fast-forward로 가져왔다.
- 원본 프로젝트의 기존 파일은 수정하지 않고 새 폴더와 이 안내서만 추가한다.

## Unity에서 확인

Unity **6000.0.47f1**로 프로젝트를 연다. 기존 프로젝트와 동일한 에디터 버전이며 패키지 업그레이드가 필요하지 않다.

1. 상단 메뉴 **BRODUN > Monsters > Open 57 Monster Motion Demo**를 누른다.
2. 열리는 `Assets/DeckbuilderMonsterCombat/Scenes/MonsterCombatDemo.unity`에서 Play를 누른다.
3. Previous / Next로 57종을 바꾸고 Attack / Skill / Hit / Block / Death / Spawn을 재생한다.
4. Idle은 자동 반복된다. 죽음 모션 후에는 Spawn으로 복원한다.
5. 기존 게임은 `Assets/BRODUN/Scenes/BrodunBoot.unity`에서 평소처럼 실행한다.

몬스터 팩 확인용 씬은 기존 빌드 씬 목록에 추가하지 않았다. 기본 게임의 진입 씬과 빌드 구성을 유지한다.

## 추가 리소스

`Assets/DeckbuilderMonsterCombat/` 아래에 다음이 들어 있다.

| 폴더 | 내용 |
|---|---|
| Sprites | 몬스터 PNG 57개 |
| Animations | 57종 × 7개 = 399개 AnimationClip |
| Controllers | Animator Controller 57개 |
| Prefabs | 이미지·컨트롤러·재생 컴포넌트가 연결된 프리팹 57개 |
| Data | 이름·층·등급·동작 유형·특수기 이름을 담은 monsters.json |
| Runtime | 모션 재생 API, 절차적 효과, 데모 브라우저 |
| Editor | 빌더, 검증 및 BRODUN 메뉴 |
| Tests | 57종의 리소스 연결 검사 및 대표 보스의 실제 모션 재생 검사 |

동작은 대기·공격·특수기·피격·방어·죽음·등장이다. 공격은 12개 유형을 공유하며 개별 몬스터에 맞는 유형·크기·타이밍을 지정했다.

## 기존 게임과 연결할 때

이번 작업은 몬스터·모션 팩의 추가와 별도 확인용 씬 제공이다. 기존 게임에 등장하는 12종의 편성, 적 수치, 전투 규칙과 적 표시 코드는 변경하지 않았다. 새 몬스터 57종이 기본 게임의 랜덤 조우에 자동으로 등장하도록 바꾸지는 않았다.

프리팹의 `MonsterCombatActor`에서 `PlayAttack()`, `PlaySkill()`, `PlayHit()`, `PlayBlock()`, `PlayDeath()`, `PlaySpawn()`을 호출할 수 있다. `onAttackImpact`와 `onSkillImpact`는 연출의 타격 시점 알림이다. 피해 계산과 턴 처리는 게임 쪽에서 연결한다.

기존 게임의 `EnemyView`는 UI Image와 프레임 시퀀스를 사용하고, 이 팩은 SpriteRenderer와 Animator를 사용한다. 따라서 기존 전투 화면을 교체하려면 별도 어댑터와 몬스터 편성·수치 설계가 필요하다. 그 작업은 이번 원본 보존 범위에 포함하지 않는다.

## 모션의 한계

기존 단일 이미지를 이동·회전·변형하고 색·투명도 및 브레스·투사체·파동 효과를 조합한다. 팔·다리·날개가 독립적으로 움직이는 스켈레탈 애니메이션이나 새 자세를 그린 프레임 애니메이션은 아니다.

## 검증

새 팩 검사는 `BRODUN > Monsters > Validate 57 Monster Motion Pack`에서 실행한다. 결과는 Git 공유 대상이 아닌 `Logs/MonsterCombatValidation.json`에 기록된다.

### 실행 결과 · 2026-11-17

- EditMode: **261/261 통과**. 기존 203개와 새 팩 58개다.
- 새 팩 PlayMode: **3/3 통과**. 불·서리·공허 보스 각각 공격·특수기 타격 이벤트, 피격·방어, 죽음 완료, 죽은 상태의 공격 차단, 재등장 및 루트 좌표 보존을 검사했다.
- 전체 PlayMode는 그래픽 장치를 끈 첫 실행에서 20/28 통과했다. 실패한 화면 관련 8개만 Direct3D 11을 켜서 재실행하여 4개가 추가 통과했다. 최종 개별 결과 기준 **24/28 통과**다.
- 남은 기존 게임 검사 4개는 새 팩 폴더를 Assets 밖으로 옮기고 원본 설정을 복원한 비교 실행에서도 **동일하게 실패**했다. 원본 코드 보존 요청에 따라 이 작업에서 수정하지 않았다. 실행 환경은 Unity 6000.0.47f1, Direct3D 11이다.

| 기존 검사 | 관찰된 실패 |
|---|---|
| AttackFramesVfxAndImpactStayPresentationOnly | 카드 효과의 렌더링 정점 수가 0 |
| EveryCardVfxHasRenderableGeometry | earth_strike 효과의 렌더링 정점 수가 0 |
| HoverIsIdempotentAndUsedCardOwnsItsVisualUntilDiscard | 사용 카드의 호버 후 위치가 기대값과 다름 |
| LargeHandUsesPagesAndEveryCardRemainsAccessible | 손패 페이지의 표시 개수 기대 6, 실제 16 |

따라서 전체 프로젝트의 모든 검사가 통과했다고 주장하지 않는다. 새 팩 관련 **61개 검사는 전부 통과**했고, 남은 4개는 팩 없이도 재현되는 기존 게임의 검사 실패다. 비교가 끝난 후 새 폴더를 복원했다.
