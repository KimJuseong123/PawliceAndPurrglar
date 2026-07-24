# 기술 아키텍처

## 1. 목표

- 게임 규칙과 Unity 표현을 분리한다.
- 키보드와 향후 음성이 같은 명령 경로를 사용한다.
- 네트워크 패키지가 바뀌어도 핵심 규칙을 유지한다.
- 외부 에셋을 프로젝트 코드와 격리한다.
- 작은 단위로 테스트할 수 있게 구성한다.
- 밸런스 값을 한 위치에서 관리한다.

## 2. 목표 폴더 구조

```text
Assets/_Project/
├─ Art/
├─ Audio/
├─ Data/
├─ Materials/
├─ Prefabs/
├─ Scenes/
├─ Scripts/
│  ├─ Core/
│  ├─ Gameplay/
│  ├─ Companions/
│  ├─ Input/
│  ├─ Animation/
│  ├─ UI/
│  └─ Integration/
├─ Settings/
├─ Tests/
└─ UI/
```

외부 에셋은 `Assets/ThirdParty/` 또는 기존 외부 에셋 전용 루트에 둔다.
Blender 원본은 `ArtSource/Blender/`, Unity 반입 FBX는 `Assets/_Project/Art/`에 둔다.

## 3. 계층

### Core

책임:

- 경기 상태
- 타이머
- 승패 판정
- 공통 ID와 인터페이스
- 규칙 이벤트

후보 타입:

- `MatchState`
- `MatchRules`
- `MatchResult`
- `MatchResultEvaluator`
- `GameClock`

### Gameplay

책임:

- 플레이어 이동과 대시
- 상호작용
- 보물 상태와 운반
- 너구리 상인 판매
- 체포
- 투척과 상태 이상

후보 타입:

- `PlayerMovement`
- `PlayerInteraction`
- `LootItem`
- `LootCarrier`
- `RaccoonMerchantSaleZone`
- `ArrestProgress`
- `ThrowableItem`
- `StatusEffectController`

### Companions

책임:

- 동물 상태 머신
- 명령 검증과 실행
- 대상 선택
- 이동과 경로 실패 복구
- 소유자 복귀

후보 타입:

- `CompanionAgent`
- `CompanionStateMachine`
- `CompanionCommandId`
- `CompanionCommandRequest`
- `CompanionCommandValidator`
- `DogCommandExecutor`
- `CatCommandExecutor`

### Input

책임:

- 플레이어 이동 입력
- 키보드 명령 입력
- UI 버튼 입력
- 향후 음성 입력 어댑터

입력 계층은 명령 ID만 만들고 동물 상태를 직접 변경하지 않는다.

후보 인터페이스:

- `IPlayerInputSource`
- `ICompanionCommandSource`
- `ICompanionCommandDispatcher`

### Animation

책임:

- 이동 상태를 Animator 파라미터로 표현
- `Idle`, `Run`, `ComedyRun` 전환
- 투척, 피격, 미끄러짐 표현
- 시각 이벤트와 게임 규칙 이벤트의 연결

애니메이션 이벤트가 승패, 체포, 판매를 확정하지 않는다.

### UI

책임:

- 타이머와 목표 금액
- 보물 소지 상태
- 체포 진행도
- 명령 상태와 쿨타임
- 향후 음성 인식 결과
- 결과 화면

UI는 이벤트를 구독해 표시하며 UI 텍스트가 게임 값을 소유하지 않는다.

### Integration

책임:

- TopDown Engine 등 외부 에셋 어댑터
- 네트워크 패키지 어댑터
- 향후 STT 서비스 어댑터
- 플랫폼별 권한과 빌드 연결

외부 패키지 타입을 Core 규칙에 직접 노출하지 않는다.

## 4. 주요 이벤트

- `MatchStarted`
- `MatchTimeChanged`
- `LootPickedUp`
- `LootDropped`
- `LootSold`
- `RaccoonMerchantBecameAvailable`
- `ArrestStarted`
- `ArrestCancelled`
- `ArrestCompleted`
- `ThrowableHit`
- `StatusEffectApplied`
- `CommandAccepted`
- `CommandRejected`
- `CompanionStateChanged`
- `VoiceRecognized`
- `MatchEnded`

이벤트 이름은 구현 전에 코드 스타일에 맞춰 확정한다.

## 5. 설정 데이터

BASE-004에서 다음 ScriptableObject를 구현했다.

- `MatchConfig`: 경기 시간, 목표 금액
- `PlayerConfig`: 이동과 대시 속도, 대시 지속 시간과 쿨타임
- `LootConfig`: 희귀도별 보물 가격
- `ArrestConfig`: 체포 거리와 완료 시간
- `CompanionConfig`: 동물 이동 속도와 명령 쿨타임
- `VoiceConfig`: 음성 기능 활성화 여부, 키보드 대체 입력, 최대 발화 시간

`DefaultGameConfigSet`이 여섯 에셋의 필수 참조를 묶고 `Bootstrap`의
`GameConfigBootstrap`이 시작 시 전체 범위를 검증한 뒤 `GameConfigService`에 등록한다.
참조 누락이나 0 이하 수치, 역전된 속도와 가격은 필드명을 포함한
`GameConfigurationException`으로 즉시 실패한다.

기본 에셋 경로:

```text
Assets/_Project/Settings/Configs/
```

향후 기능이 실제로 구현될 때만 `CameraConfig`, `ThrowableConfig`, 명령별 설정과
`AnimationConfig`를 추가한다. 경기 시간, 목표 금액, 이동 속도, 체포 시간,
명령 쿨타임, 감지 거리와 보물 가격을 씬과 코드에 중복 작성하지 않는다.

## 6. 명령 데이터 흐름

```text
Keyboard / UI / Future Voice
-> ICompanionCommandSource
-> CompanionCommandRequest
-> CompanionCommandValidator
-> CompanionCommandExecutor
-> CompanionStateMachine
-> Gameplay Events
-> UI / Animation / Audio
```

## 7. 네트워크 경계

네트워크 패키지는 아직 미정이다.
그 전까지 다음을 구분한다.

- 로컬 입력
- 로컬 카메라와 화면 연출
- 권한 있는 경기 상태
- 동기화할 상태

권한 있는 상태 후보:

- 경기 타이머와 결과
- 보물 소유와 상태
- 판매 금액
- 체포 진행과 완료
- 명령 접수와 동물 핵심 상태

로컬 표현 후보:

- 카메라 흔들림
- UI 애니메이션
- 비결정적 파티클
- 사운드 변형

## 8. 모델 및 애니메이션 파이프라인

```text
ArtSource/Blender
-> FBX Export
-> Assets/_Project/Art
-> Model Import Settings
-> Character Prefab
-> Animator Controller
-> Gameplay Scene
```

경찰 모델 스파이크에서는 다음을 기록한다.

- 단위와 축
- 원점과 피벗
- Humanoid Avatar 유효성
- 리타게팅 결과
- 루트 모션 사용 여부
- 탑다운 시점 실루엣
- 임포트 경고

## 9. 씬

기본 씬:

```text
Assets/_Project/Scenes/Bootstrap.unity
Assets/_Project/Scenes/Game.unity
Assets/_Project/Scenes/Result.unity
```

`Bootstrap`은 시작 지점, `Game`은 경기 조립, `Result`는 결과 표시를 담당한다.
씬 이름과 경로는 `GameSceneCatalog`에서 관리하고 전환은 `GameSceneLoader`를 통한다.
빌드 씬 순서는 `Bootstrap`, `Game`, `Result`로 고정한다.

씬은 조립과 참조를 담당하고 게임 규칙을 직접 소유하지 않는다.

## 10. 테스트

### Edit Mode

- 경기 상태 전환
- 승패 판정
- 보물 상태 전환
- 명령 검증
- 입력 소스별 동일 명령 ID

### Play Mode

- 이동과 카메라
- 체포 범위
- 보물 부착과 드롭
- 동물 이동과 복귀
- Animator 전환
- 재시작 초기화

## 11. 오류 처리

동물 경로 실패:

- 제한 횟수 재탐색
- 목표 무효화
- 소유자 근처 안전 복귀
- 무한 반복 방지

향후 음성 서비스 실패:

- 키보드와 버튼 유지
- 상태와 실패 이유 표시
- 경기 중단 금지
- 오류 로그 기록

모델 임포트 실패:

- 기존 검증 모델 유지
- FBX 축, 스케일, Avatar 오류 기록
- 프리팹 참조를 깨뜨리는 자동 교체 금지

## 12. 고정 개발 환경

| 항목 | 결정 |
|---|---|
| Unity | `6000.5.4f1` |
| 렌더 파이프라인 | URP `17.5.0` |
| 입력 | Input System `1.19.0`, Player Settings는 `Both` |
| 테스트 | Unity Test Framework `1.7.0` |
| 카메라 | Cinemachine `3.1.7` |
| 내비게이션 | AI Navigation `2.0.13` 유지, 동물 경로 탐색에 사용 예정 |
| 목표 플랫폼 | Windows x86_64 우선 |

새 프로젝트 코드는 Input System을 사용한다.
`Both` 설정은 기존 TopDown Engine의 Legacy Input 호환을 위한 임시 경계이며, 새 입력 코드를 Legacy Input으로 작성한다는 의미가 아니다.

네트워크는 Netcode for GameObjects와 Unity Multiplayer Services 조합을 우선 후보로 둔다.
이 단계에서는 설치하지 않으며, 기술 검증에서 Host, Client, 역할 배정, 위치 동기화, 연결 종료를 확인한 뒤 최종 채택한다.
WebGL은 마이크와 네트워크 제약을 별도로 검증한 뒤 보조 빌드 대상으로 재검토한다.
