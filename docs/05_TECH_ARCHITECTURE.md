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

구현 타입:

- `MatchState`
- `IMatchStateReader`
- `MatchStateChanged`
- `MatchStateMachine`

후속 후보 타입:

- `MatchRules`
- `MatchResult`
- `MatchResultEvaluator`
- `GameClock`

`MatchStateMachine`은 `LOBBY`에서 시작하고 아래 순서만 허용하는 순수 C#
객체다.

```text
LOBBY -> READY -> PLAYING -> ENDING -> RESULT
```

같은 상태, 역방향, 건너뛰기와 정의되지 않은 상태 전환은 상태와 이벤트를
변경하지 않고 `false`를 반환한다. 다른 시스템은 `IMatchStateReader`를 통해
현재 상태와 `PLAYING` 여부만 읽는다. 네트워크, UI, 타이머는 이 객체를 직접
소유하지 않으며 후속 조립 계층에서 연결한다.

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

역할 경계:

```text
PlayerRoleIdentity
├─ PlayerRole
├─ PlayerRolePermissions
└─ PlayerRoleSpawnResolver -> GreyboxMapDefinition
```

본게임 역할은 기술 검증 전용 `TechnicalPlayerRole`을 재사용하지 않는다.
`Police`와 `Thief`는 맵의 `PoliceSpawn`, `ThiefSpawn`을 각각 사용한다.
상호작용 구현은 역할을 직접 비교하지 않고 `PlayerRolePermissions`의 다음
권한표를 조회한다.

| 상호작용 | Police | Thief |
|---|---:|---:|
| 일반 | 허용 | 허용 |
| 사다리 등 이동 경로 | 허용 | 허용 |
| 보물 획득 | 거부 | 허용 |
| 판매 | 거부 | 허용 |
| 체포 | 허용 | 거부 |

이동 입력과 물리는 분리한다.

```text
PlayerKeyboardInput
-> PlayerMovementMotor
   ├─ PlayerConfig
   ├─ IMatchStateReader
   └─ CharacterController
```

`PlayerKeyboardInput`은 Input System의 WASD 벡터만 생성한다.
`PlayerMovementMotor`는 카메라 기준 월드 방향, 설정 속도, 중력과 충돌을
적용하며 `PLAYING`이 아닐 때 수평 이동을 만들지 않는다. 경찰과 도둑은 같은
모터를 사용하고 역할별 차이는 설정과 로컬 제어 권한으로만 둔다.

`TopDownFollowCamera`는 원근 투영을 유지하고 목표 위치를 `LateUpdate`에서
추적한다. 맵 검증용 `GreyboxTraversalProbe`는 `-mapAutoQuit` 인자가 있을 때만
동작해 일반 Game 실행의 플레이어와 충돌하지 않는다.

`LocalPlayerRoleSelector`는 역할별 `PlayerRoleControlBinding`을 보유하고
선택된 한 역할의 키보드 입력만 활성화한다. 기본 역할은 경찰이며 개발 빌드에서
`-playerRole Thief`로 도둑 조작을 검증할 수 있다. 이 선택기는 네트워크 역할
권한을 대신하지 않으며, 실제 멀티플레이 연결 시 로컬 소유권 어댑터로 교체한다.

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

NET-001 기술 검증에는 Netcode for GameObjects `2.13.0`과 Unity Transport
`6.5.0`을 사용한다. 로컬 직접 IP Host·Client 접속, 플레이어 생성, 소유자별
위치 동기화와 연결 종료는 실제 Windows 빌드에서 통과했다.

이 선택은 기술 스파이크 기준이며 Relay, Lobby, 서버 운영과 최종 권한 구조는
확정하지 않았다. 본게임 코드에서는 다음 경계를 유지한다.

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

NET-001 검증 코드는 `TechnicalValidation`에 격리하며 Core 규칙에서
`NetworkManager`, `NetworkVariable` 등 NGO 타입을 직접 참조하지 않는다.

NET-002 기술 검증에서는 서버만 역할 `NetworkVariable`을 쓸 수 있다. Host는
경찰, 승인된 첫 원격 Client는 도둑이며 접속 승인 단계에서 최대 2명으로
제한한다. 역할별 시작 위치와 시각 표현은 동기화된 역할을 읽어 적용한다.
이 규칙은 기술 검증용이며 실제 로비의 역할 선택 방식은 별도 설계한다.

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

### MAP-001 회색 상자 경계

`GreyboxMapDefinition`은 `Game` 씬의 위치 앵커, 명시적 경로, 지붕, 사다리와
쓰레기통 참조를 보유한다. 맵 크기나 경로가 경기 규칙을 직접 변경하지 않으며,
이동 구현은 이후 이 정의를 조회만 한다.

```text
GreyboxMapDefinition
├─ GreyboxLocationReference
├─ GreyboxRouteReference
├─ Rooftops / Ladders / TrashBins
└─ GreyboxObstacle
```

에디터 생성기 `GreyboxMapSetup`은 56×44m 마을과 재질을 결정적으로 다시 만들고
저장 직후 다음 계약을 검사한다.

- 필수 장소 7개가 정확히 한 번씩 존재
- 지정된 주요 장소 쌍마다 독립 경로 2개 이상
- 모든 경로의 선언 폭이 2.4m 이상
- 0.9m 지름, 2m 높이 캡슐이 경로상 `GreyboxObstacle`과 겹치지 않음
- 지붕 3개, 사다리 3개, 쓰레기통 위치 4개 이상

`GreyboxTraversalProbe`는 빌드에서 `PlayerConfig.MoveSpeed`로 실제
`CharacterController`를 이동시켜 횡단 시간과 끼임을 JSON으로 기록한다.
최종 플레이어 이동, 카메라, 상호작용을 대신하는 코드는 아니다.

## 10. 테스트

어셈블리 경계:

```text
Assets/_Project/Scripts/PawsAndLoot.Runtime.asmdef
Assets/_Project/Tests/EditMode/PawsAndLoot.Tests.EditMode.asmdef
Assets/_Project/Tests/PlayMode/PawsAndLoot.Tests.PlayMode.asmdef
```

테스트 어셈블리는 `PawsAndLoot.Runtime`을 참조하며 런타임 어셈블리는 테스트
어셈블리를 참조하지 않는다. Edit Mode 테스트는 Editor에서만 컴파일하고,
Play Mode 테스트는 일반 빌드에서 `TestAssemblies`로 제외한다.

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

런타임 로그는 `GameLogger`만 사용하고 다음 분류를 붙인다.

- `Match`
- `Player`
- `Loot`
- `Arrest`
- `Companion`
- `Voice`
- `Network`

로그 형식은 `[PawsAndLoot][Category][Level] message`다.
`DefaultGameLogConfig`은 Editor와 Development Build에서 `Debug` 이상,
일반 제출 빌드에서 `Warning` 이상을 출력한다. 개발 최소 레벨은 제출 최소
레벨보다 반드시 상세해야 하며, 오류 레벨은 설정으로 완전히 숨길 수 없다.

상태 전환은 한 번만 기록하고 `Update`, `FixedUpdate`, `LateUpdate`에서
매 프레임 로그를 남기지 않는다. 반복될 수 있는 경고는 안정적인 키와
`DebugOnce`, `InfoOnce`, `WarningOnce`, `ErrorOnce`를 사용한다.
예외를 처리할 때는 원본 예외를 `GameLogger.Exception`에 전달해 스택 추적을
보존하며 빈 `catch`로 실패를 숨기지 않는다.

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
