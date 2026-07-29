# 현재 상태

마지막 갱신: 2026-07-28

이 문서는 작업 시작 시 가장 먼저 확인하는 현재 저장소 상태다.

## 현재 목표

그레이박스 마을에서 경찰의 체포 감지·진행·중단·완료와 역할별 UI를 검증한다.

## 현재 단계

```text
단계 5: 체포 시스템
```

## 확정된 제품 방향

- 임시 제목: 멍경찰과 냥도둑
- 영문명: Paws & Loot
- 경찰 1명 대 도둑 1명
- 경기 시간 4분
- 원근감 있는 3D 기울어진 탑다운
- 경찰과 강아지, 도둑과 고양이
- 도둑의 판매 NPC는 너구리 상인
- 동물 명령은 숫자키 `1`~`4`로 먼저 구현
- 실제 음성 입력과 자연어 분류는 핵심 루프 검증 후 적용
- 대부분의 모델은 그레이박스 이후 Blender에서 제작
- 기존 경찰 모델만 리깅과 애니메이션 테스트에 사용

## 저장소에서 확인된 상태

- Unity 버전: `6000.5.4f1`
- URP: `17.5.0`
- Input System: `1.19.0`, Player Settings `Both`
- Unity Test Framework: `1.7.0`
- AI Navigation: `2.0.13`
- Cinemachine: `3.1.7`
- 첫 빌드 대상: Windows x86_64
- 네트워크 기술 검증: Netcode for GameObjects `2.13.0`, Unity Transport `6.5.0`
- `AGENTS.md` 존재
- `README.md` 존재
- 프로젝트 문서 패키지 작성
- `C:\Users\SSAFY\CatCops`를 로컬 Git 저장소 루트로 초기화
- 기본 브랜치 `main`과 로컬 Git LFS 설정
- 프로젝트 전용 Unity 자산 경로 생성:

```text
Assets/_Project/
```

- 새 외부 에셋 경로 생성:

```text
Assets/ThirdParty/
```

- Blender 원본과 제출 자료 경로 생성:

```text
ArtSource/Blender/
Submission/
```

- 기존 `Assets/TopDownEngine/`은 이동하지 않고 별도 외부 에셋 루트로 유지
- 기존 실험 씬 존재:

```text
Assets/CatCops/Scenes/CatCopsPrototype.unity
```

- 새 기본 씬 존재:

```text
Assets/_Project/Scenes/Bootstrap.unity
Assets/_Project/Scenes/Game.unity
Assets/_Project/Scenes/Result.unity
```

- 빌드 씬 순서: `Bootstrap`, `Game`, `Result`
- 공통 씬 ID와 로더: `GameSceneCatalog`, `GameSceneLoader`
- 공통 설정 에셋과 런타임 접근:

```text
Assets/_Project/Settings/Configs/
Assets/_Project/Scripts/Core/Configuration/
```

- `Bootstrap`의 `GameConfigBootstrap`이 여섯 설정 에셋을 시작 시 검증
- 설정 누락과 범위 오류는 필드명을 포함한 `GameConfigurationException`으로 중단
- 분류형 런타임 로그와 설정:

```text
Assets/_Project/Scripts/Core/Logging/
Assets/_Project/Settings/Logging/DefaultGameLogConfig.asset
```

- 개발 로그는 `Debug` 이상, 제출 로그는 `Warning` 이상
- 동일 `Once` 키는 실행 세션에서 한 번만 출력
- `GameLogger.Exception`이 원본 예외와 스택 추적을 보존
- 런타임과 테스트 Assembly Definition:

```text
Assets/_Project/Scripts/PawsAndLoot.Runtime.asmdef
Assets/_Project/Tests/EditMode/PawsAndLoot.Tests.EditMode.asmdef
Assets/_Project/Tests/PlayMode/PawsAndLoot.Tests.PlayMode.asmdef
```

- Windows TECH-001 검증 씬과 빌드:

```text
Assets/_Project/Scenes/TechnicalTest.unity
Builds/TechnicalValidation/Windows/PawsAndLootTech.exe
```

- 실제 WindowsPlayer에서 1280×720, Input System, W 키 이동 약 8m 확인
- WebGL 로컬 서버와 GitHub Pages 검증은 Windows 우선 단계에서 미실행

- Blender TECH-002 테스트 원본과 Unity 반입 파일:

```text
ArtSource/Blender/TechnicalValidation/TechRig.blend
Assets/_Project/Art/TechnicalValidation/TechRig.fbx
```

- Blender 5.2에서 2.31m 테스트 더미, Generic 5본, 단일 파란 재질, `Idle`·`Walk` 생성
- `PlayerRoot`의 이동·Collider와 교체 가능한 `VisualRoot`를 분리한 검증 프리팹 생성
- 실제 WindowsPlayer에서 5본, 재질 1개, 두 클립, 축·스케일, 루트 모션 비활성화 확인

- Windows TECH-003 음성 검증 씬과 빌드:

```text
Assets/_Project/Scenes/VoiceTechnicalTest.unity
Builds/TechnicalValidation/Windows/PawsAndLootVoiceTech.exe
```

- 실제 WindowsPlayer에서 마이크 1개와 키보드 대체 입력 응답 확인
- Unity 내장 `DictationRecognizer` 생성은 `0x80004003`으로 실패
- 실제 발화 텍스트 출력은 미검증이며 TECH-003은 `BLOCKED`

- NET-001 로컬 접속 검증 씬과 빌드:

```text
Assets/_Project/Scenes/NetworkTechnicalTest.unity
Builds/TechnicalValidation/Windows/PawsAndLootNetworkTech.exe
```

- NGO `2.13.0`, Unity Transport `6.5.0`으로 `127.0.0.1` Host·Client 접속
- 실제 WindowsPlayer 두 개에서 플레이어 2명, 소유자·위치 분리와 이동 동기화 확인
- Client 종료를 Host가 감지했으며 Host·Client 결과 모두 `passed: true`
- NET-002에서 Host `Police`, Client `Thief`를 서버 권한으로 배정
- 양쪽 모두 경찰 1명·도둑 1명, 역할 중복 없음, 로컬 역할 인식과 시작점 분리 통과
- 세 번째 접속을 거절하고 Host에서 거절 1회 확인

- MAP-001 회색 상자 마을:

```text
Assets/_Project/Scenes/Game.unity
Assets/_Project/Scripts/Gameplay/Map/
```

- 56×44m, 필수 장소 7개, 경로 9개, 지붕 3개, 사다리 3개, 쓰레기통 위치 4개
- WindowsPlayer 72m 자동 횡단 17.58초, 끼임 0회

- MATCH-001 경기 상태:

```text
Assets/_Project/Scripts/Core/Match/
```

- `LOBBY -> READY -> PLAYING -> ENDING -> RESULT` 순차 전환만 허용
- 중복·건너뛰기·역방향·정의되지 않은 상태 전환 거부
- `IMatchStateReader`로 현재 상태와 경기 활성 여부 조회

- PLAYER-005 본게임 역할:

```text
Assets/_Project/Scripts/Gameplay/Players/
```

- `Police`, `Thief` 역할과 역할별 맵 시작점 분리
- 경찰 체포, 도둑 보물·판매 권한을 중앙 권한표로 검사
- Game 씬의 파란 경찰·빨간 도둑 임시 표시 확인

- PLAYER-001 경찰 이동:

```text
Assets/_Project/Scripts/Gameplay/Players/PlayerMovementMotor.cs
Assets/_Project/Scripts/Gameplay/Players/PlayerKeyboardInput.cs
Assets/_Project/Scripts/Gameplay/Camera/TopDownFollowCamera.cs
```

- WASD, CharacterController 벽 충돌·계단, 프레임 독립 이동
- `IMatchStateReader.IsGameplayActive`가 참일 때만 수평 이동
- 경찰을 추적하는 원근 탑다운 카메라

- PLAYER-002 도둑 이동:

```text
Assets/_Project/Scripts/Gameplay/Players/LocalPlayerRoleSelector.cs
Assets/_Project/Scripts/Gameplay/Players/PlayerRoleControlBinding.cs
```

- 경찰·도둑 모두 같은 `PlayerMovementMotor`와 CharacterController 사용
- 기본 경찰, `-playerRole Thief` 실행 시 도둑 입력과 카메라 선택

- PLAYER-003 공통 대시:

- Space 입력, `PlayerConfig`의 대시 속도·지속시간·쿨타임 사용
- 일반 이동과 동일한 CharacterController 충돌
- 경기 종료 취소, 연속 입력 차단과 쿨타임 상태 조회

- 작업자 제공 프롭 원본과 Unity 반입 FBX 19종:

```text
ArtSource/Blender/Props/
Assets/_Project/Art/Props/
```

- 아이템 8종, 기능 오브젝트 6종, 투척물 4종, 캔따개 1종
- 캐릭터 콘셉트 참고 이미지(3D 모델 아님):

```text
ArtSource/Reference/Characters/
```

- 캐릭터 모델은 아직 없어 외부 TopDown Engine `MMExplodude` 만화풍 메시를
  역할 색으로 임시 대체. 머리 본을 2.2배로 키우고 신장 1.7m로 정규화했으며
  대기 애니메이션을 연결
- 외부 에셋 전체에 캐릭터 FBX는 7개뿐이고 2등신 캐릭터는 없음. 실제 2등신은
  `MODEL-002` 자체 제작이 필요
- 건물은 계속 회색 상자

- 기존 경찰 Blender 원본 존재:

```text
ArtSource/Police/Police_LowPoly.blend
```

- 기존 경찰 FBX 존재:

```text
Assets/CatCops/Models/Police_LowPoly.fbx
```

- 신규 캐릭터 원본 FBX 4종 추가:

```text
ArtSource/Blender/Characters/police+officer+3d+model/
ArtSource/Blender/Characters/theif+3d+model/
ArtSource/Blender/Animals/dog/
ArtSource/Blender/Animals/cat/
```

- `Assets/CatCops/Models/*.fbx`는 현재 Git LFS 포인터 상태이며, 새 CHAR-001 검증은
  위 `ArtSource/Blender/...` 원본을 `Assets/_Project/Art/Characters/`로 복사해
  임포트하는 방식으로 진행

- 신규 캐릭터 검증용 에디터 스크립트와 런타임 프리뷰 추가:

```text
Assets/_Project/Editor/CharacterTechnicalValidationSetup.cs
Assets/_Project/Scripts/TechnicalValidation/CharacterTechnicalPreview.cs
Assets/_Project/Scripts/TechnicalValidation/CharacterTechnicalValidationReporter.cs
```

- CHAR-001 검증은 다음 흐름을 자동화:
  - 신규 경찰, 도둑, 강아지, 고양이 FBX를 `Assets/_Project/Art/Characters/`로 동기화
  - `CharacterTechnicalTest.unity` 장면 생성
  - 캐릭터 4종을 한 씬에 배치
  - 애니메이션 클립이 있으면 기본 클립 재생, `Idle`/`Walk`가 함께 있으면 이동 연동
  - 결과 JSON과 스크린샷 저장

## 아직 존재하지 않는 목표 결과

- 새 구조의 경찰 프리팹
- 경찰 Armature와 Avatar
- `Idle`, `Run`, `ComedyRun`
- 너구리 상인의 출현·이동·판매 피드백
- 숫자키 동물 명령 상태 머신
- 도둑의 판매 승리를 가능하게 하는 보물 배치 또는 목표 금액
- 신규 4종 캐릭터 FBX의 실제 애니메이션 클립 유무와 개수에 대한 런타임 검증 결과

## 레거시 실험물 처리

`Assets/CatCops/`의 기존 코드와 씬은 초기 방향 탐색을 위한 실험물이다.
새 MVP의 구조나 규칙 기준으로 사용하지 않는다.

승계 대상:

- 경찰 Blender 원본과 FBX
- 렌더 파이프라인 문제 해결 경험
- 외부 TopDown Engine 자산

승계하지 않는 대상:

- 기존 자동 생성 마을
- 기존 임시 UI
- 기존 단순 이동과 동물 로직
- 기존 수치와 승패 흐름

기존 파일을 삭제하거나 이동하는 작업은 별도 요청과 백업 후 진행한다.

## 현재 알려진 환경 상태

- 실행 중인 Unity 프로세스는 확인되지 않았다.
- 현재 작업 환경에서는 Unity `6000.5.4f1` 실행 파일 경로를 확인하지 못했다.
  `C:\Program Files\Unity\Hub\Editor\2022.3.6f1\Editor\Unity.exe`만 확인되어
  CHAR-001 배치 실행은 아직 미검증이다.
- 실행 중인 프로세스 없이 남아 있던 `Temp/UnityLockfile`을 제거했다.
- Unity `6000.5.4f1` 배치 모드에서 새 폴더 임포트와 `.meta` 생성을 검증했다.
- 불필요한 2D 편집, Collaborate, Rider, Visual Scripting, Multiplayer Center 패키지를 제거했다.
- Windows 64비트 대상 패키지 재해석과 전체 스크립트 컴파일을 오류 없이 완료했다.
- 기존 TopDown Engine 셰이더의 fallback 관련 비차단 경고가 남아 있다.
- 기본 씬 생성, 빌드 순서와 전환 대상은 에디터 자동 검증을 완료했다.
- 실제 Play Mode에서 버튼을 직접 클릭하는 수동 검증은 아직 수행하지 않았다.

- COMP-001~006 동료 공통 구조:

```text
Assets/_Project/Scripts/Companions/
Assets/_Project/Scripts/Input/CompanionCommandKeyboardInput.cs
Assets/_Project/Scripts/Animation/PlayerLocomotionAnimator.cs
```

- 입력에서 AI까지 경로가 하나뿐:
  `입력 → CompanionCommandRequest → 검사기 → 전달기 → 에이전트`
- 신규 캐릭터 5종·건물 5종을 Unity로 반입하고 경찰·도둑을 실제 모델로 교체
- 경찰·도둑 FBX는 Humanoid 아바타 생성에 성공해 외부 휴머노이드 클립을
  리타게팅한 `Idle`·`Run` 컨트롤러를 사용
- 신규 캐릭터 FBX 자체에는 애니메이션 클립이 없어 동물은 여전히 정지 상태

## ART-002 규격 검사 결과

`Paws & Loot > Setup > Validate Authored Models`가 확정 모델 5종을 검사해
**14건의 규격 위반**을 보고했다. 경고는 빌드를 막지 않는다.

| 모델 | 위반 |
|---|---|
| 경찰 | 애니메이션 클립 없음 |
| 도둑 | 애니메이션 클립 없음 |
| 강아지 | 삼각형 **15,764** (예산 4,000), 원점 `y=-0.183`, 신장 1.26m → 0.59배 축소, 클립 없음 |
| 고양이 | 삼각형 **18,669** (예산 4,000), 원점 `y=-0.149`, 신장 1.18m → 0.38배 축소, 클립 없음 |
| 너구리 | 삼각형 **4,957** (예산 4,000), 원점 `y=-0.154`, 신장 1.14m → 0.61배 축소, 클립 없음 |

경찰과 도둑은 기하 규격을 통과했다. 동물 3종은 **삼각형이 예산의 1.2~4.7배**이고
원점이 발바닥이 아니라 15~18cm 아래에 있어 코드가 매번 보정하고 있다.

가장 시급한 것은 **애니메이션 클립 부재**다. 5종 전부 클립이 0개이므로 현재
이동은 절차적 애니메이션으로 대체돼 있다. `MODEL-002`의 실제 산출물은 클립
6종이다.

## ART-012 최적화 결과

씬 재생성 마지막 단계에서 자동 실행되므로 생성 내용과 어긋나지 않는다.

| 항목 | 전 | 후 |
|---|---:|---:|
| 고유 머티리얼 | 137개 | **121개** |
| 재바인딩한 슬롯 | — | 191개 |
| static 표시 렌더러 | 0개 | **1,589개** |
| 그림자 끈 렌더러 | 0개 | **1,262개** |
| 전체 할당 메모리 | 176.2MB | **171.9MB** |

**프레임 개선은 관측하지 못했다.** 최초 측정과 직후 측정 모두 중간값이
8.333ms로 VSync 상한에 붙어 있어 비교가 불가능했다. 그래서 프로브가
`vSyncCount = 0`으로 상한을 풀도록 고쳤다.

상한을 푼 실측(ART-012 적용 후, 3,178프레임):

| 항목 | 값 |
|---|---:|
| 평균 프레임 | 3.15ms (317.7fps) |
| 중간값 프레임 | **2.01ms (약 498fps)** |
| 상위 1% 최악 프레임 | 4.56ms |

즉 현재 씬은 이 하드웨어에서 목표 대비 큰 여유가 있다. 다만 **ART-012 이전의
상한 없는 수치가 없어 개선 폭 자체는 측정하지 못했다.** 머티리얼 16개 감소와
메모리 4.3MB 감소는 확인했고, 정적 배칭 효과는 드로우 콜로만 확인할 수 있어
검증 대상에서 제외했다.

## ART-005 성능 측정 결과

`-perfProbe` 인자로 실제 WindowsPlayer에서 8초간 807프레임 측정
(RTX 4050 Laptop, Direct3D12, 1600×900).

| 항목 | 값 |
|---|---:|
| 평균 프레임 | 12.39ms (80.7fps) |
| 중간값 프레임 | 8.33ms (120fps, VSync 상한) |
| 상위 1% 최악 프레임 | 8.34ms |
| 활성 렌더러 | 1,604개 |
| 스킨드 메시 | 5개 |
| 한 메시 최대 본 | 41개 |
| 전체 본 | 199개 |
| 고유 머티리얼 | 133개 |
| 라이트 | 2개 |
| 텍스처 메모리 | 16.77MB |
| 전체 할당 메모리 | 176.21MB |
| 드로우 콜 | 미측정 |

드로우 콜은 Unity가 `UnityStats`로만 노출하며 이는 에디터 전용이므로 빌드된
플레이어에서 정직하게 보고할 수 없다. 렌더러 1,604개와 머티리얼 133개가 그
추정의 근거다.

### WebGL 빌드 용량

`Paws & Loot > Build > Measure WebGL Build Size`로 실제 빌드해 측정했다.
WebGL은 목표 플랫폼이 아니며(`ISSUE-008`) 측정 목적으로만 빌드한다.

| 항목 | 값 |
|---|---:|
| 전체 | 34.05MB |
| 브라우저가 받는 페이로드 | **33.90MB** |
| `WebGL.data.gz` (에셋) | 19.78MB |
| `WebGL.wasm.gz` (코드) | 14.02MB |
| `WebGL.framework.js.gz` | 0.08MB |

판정: 첫 로딩에 33.9MB는 **웹 배포 기준으로 무겁다.** 절반 이상이 코드
(`wasm` 14MB)인데, 이는 외부 TopDown Engine 전체가 여전히 프로젝트에 임포트돼
있어 IL2CPP가 함께 컴파일하기 때문이다. 웹을 실제 목표로 채택하면 미사용 외부
에셋 제거가 가장 큰 절감 수단이다. 에셋 19.8MB는 건물 텍스처와 캐릭터
베이스컬러가 대부분이다.

판정: 현재 프레임은 목표에 여유가 있으나 **렌더러 1,604개는 과다하다.**
평균이 중간값의 1.5배인 것은 초기 로드 구간의 스파이크이며, 정상 구간은
VSync 상한에 붙어 있다. `ART-012` 머티리얼 통합과 정적 배칭을 적용할 근거로
이 수치를 기준선으로 삼는다.

## NET-003·NET-004 검증 결과

실제 Windows 빌드 두 프로세스로 경기 씬까지 진입해 측정했다.

| 항목 | 호스트 | 클라이언트 | 판정 |
|---|---|---|---|
| `matchState` | `Playing` | `Playing` | 일치 |
| `remainingSeconds` | 233.05 | 233.067 | **0.017초 차이** |
| `matchRemoteControlled` | false | **true** | 호스트만 시뮬레이션 |
| `playerLinks` | 2 | 2 | 양쪽 스폰 |
| 경찰 좌표 | -24.00 1.08 0.00 | 동일 | 복제 정상 |
| 도둑 좌표 | 24.00 1.08 -18.00 | 동일 | 복제 정상 |
| `localRole` | Police | **Thief** | 일치 |

**NET-004는 완료다.** 두 화면의 남은 시간이 0.02초 이내로 일치하고,
클라이언트는 자기 타이머를 돌리지 않으며(`matchRemoteControlled: true`)
승패도 호스트만 결정한다.

**NET-003도 완료다.** `ISSUE-016`을 해결한 뒤 두 캐릭터가 실제로 이동했고
양쪽 좌표가 0.04m 이내로 일치했다.

| 대상 | 호스트 | 클라이언트 | 차이 |
|---|---|---|---:|
| 경찰 | `-26.97 1.08 -7.29` | `-26.97 1.08 -7.25` | 0.04m |
| 도둑 | `16.46 1.08 -20.09` | `16.46 1.08 -20.05` | 0.04m |

도둑은 클라이언트가 보낸 입력으로 움직였다. 즉 `클라이언트 입력 → 호스트
시뮬레이션 → 좌표 복제`가 실제로 동작한다. 클라이언트의 링크는
`remoteDrivenLinks: 2`로 모터가 꺼져 있고, 각 기계는 자기 역할에만 입력을
보내므로 상대를 조작할 경로가 없다.

### 구조

```text
클라이언트 입력 → RPC → 호스트가 모터로 시뮬레이션 → 좌표 복제 → 클라이언트 표시
```

호스트만 모터를 돌리므로 클라이언트가 임의 위치로 갈 수 없다. 세션 중에는 모든
로컬 키보드 입력이 꺼지므로 한 기계가 상대 역할을 조작할 경로가 아예 없다.
좌표는 스냅이 아니라 보간해 적용하고 4m를 넘으면 즉시 맞춰 늦은 패킷이 순간이동
대신 짧은 추격으로 보이게 한다.

씬 관리를 NGO로 넘겼다. 경기 씬의 플레이어·보물·체포 오브젝트가 씬에 배치된
`NetworkObject`이므로 서버가 씬을 로드해야 클라이언트가 같은 오브젝트를 찾는다.
오프라인에서는 핸들러가 거절하고 기존 로더가 그대로 동작한다.

## 직접 IP 로비 검증 결과

`DEC-027` 호스트 권한 + 직접 IP를 채택하고 로비를 만들었다. 실제 Windows 빌드
두 프로세스로 검증했다.

| 항목 | 호스트 | 클라이언트 |
|---|---|---|
| `sessionMode` | `Host` | `Client` |
| `connectedPlayers` | 2 | 서버 전용 값이라 0 |
| `rolesAssigned` | **true** | **true** |
| `localRole` | **Police** | **Thief** |
| `policeClientId` | 0 | 0 |
| `passed` | **true** | **true** |

`policeClientId`가 양쪽에서 0으로 일치한다. 즉 두 화면이 같은 역할 배정을
읽는다. 내 IP는 `192.168.35.197`로 정확히 표시됐다.

### 이 과정에서 고친 것 세 가지

1. `Awake()`에서 `StartHost`/`StartClient`를 호출해 NetworkManager 초기화 전에
   실행되어 `NullReferenceException`이 났다. 시작을 첫 `Update`로 미뤘다.
2. 호스트만 런타임에 `ConnectionApproval`을 켜면 NGO가 설정 해시 불일치로
   클라이언트를 끊는다. 씬 설정에서 양쪽 동일하게 켜고 콜백만 호스트에 붙였다.
3. 역할 보드를 씬에 배치했더니 클라이언트가
   `NetworkPrefab could not be found`로 스폰에 실패하고 끊겼다.
   `EnableSceneManagement = false`에서는 in-scene NetworkObject도 프리팹 목록
   에서 찾는다. 프리팹으로 등록해 호스트가 스폰하도록 바꿨다.

`ConnectedClientsIds`는 NGO에서 서버 전용이라 클라이언트에서 항상 비어 있다.
클라이언트의 준비 판정은 접속 여부와 복제된 역할 보드로만 한다.

## NET-005~NET-010 검증 결과

실제 Windows 빌드 두 프로세스, `-netScenario` 3종으로 측정했다.
결과 파일은 `net-match-{host,client}-result.json`,
`net-rematch-{host,client}-result.json`이다.

### `-netScenario full` (NET-005·006·007)

| 항목 | 호스트 | 클라이언트 | 판정 |
|---|---|---|---|
| `lootState` | `Sold` | `Sold` | 일치 |
| `sawLootCarried` | true | true | 일치 |
| `lootCarrierRole` | 1 (Thief) | 1 (Thief) | 일치 |
| `soldAmount` | 200 | 200 | 일치 |
| `interactRequests` | 0 | **11,174** | 클라이언트만 연타 |
| `creditedSales` | **1** | 0 (호스트 몫) | 중복 판매 없음 |
| `arrestSeconds` | 1.5 | 1.5 | 일치 |
| `arrestCompleted` | true | true | 일치 |
| `decidedWinner` | Police | Police | 일치 |
| `decidedReason` | ThiefArrested | ThiefArrested | 일치 |
| `remainingSeconds` | 230.535 | 230.545 | 0.010초 차이 |
| `peakRemoteDrivenLinks` | 0 | 2 | 호스트만 시뮬레이션 |
| `passed` | **true** | **true** | |

`creditedSales`가 클라이언트에서 0인 것은 정상이다. 중복 판매 방지는 실제로
판매가 일어나는 호스트에만 있고, 클라이언트 지갑은 총액만 반영한다.

### `-netScenario rematch` (NET-008)

| 항목 | 호스트 | 클라이언트 |
|---|---|---|
| `reachedResult` | true | true |
| `hostReceivedRequests` | **1** | 0 |
| `returnedToGame` | **true** | **true** |
| `activeScene` | `Game` | `Game` |

클라이언트만 재경기를 눌렀고 호스트가 요청 1건을 받아 양쪽을 함께 Game으로
되돌렸다. 호스트는 스스로 누르지 않으므로 복귀의 원인이 클라이언트 요청임이
분리된다.

### `-netScenario disconnect` (NET-009)

| 항목 | 호스트 | 클라이언트 |
|---|---|---|
| `disconnectHandledCount` | **1** | 0 (먼저 나간 쪽) |
| `disconnectReason` | 상대가 접속을 종료했습니다. | |

정확히 1회다. 2회 이상이면 "오류 메시지가 반복" 증상이 된다.

### 구조

```text
클라이언트 E/Q/1~4 → RPC → 호스트가 스캐너·carrier·dispatcher 실행
호스트 보물 상태·소유자·판매액·체포 진행도 → NetworkVariable → 클라이언트 표시
```

승패는 복제하지 않는다. 복제하는 것은 판정의 **입력**(지갑 총액, 체포 완료)
이고, 판정 자체는 양쪽의 `MatchResultArbiter`가 한다. `AGENTS.md`가 승패를
`MatchResultArbiter`/`MatchResultEvaluator`만 결정하도록 못박았기 때문에
네트워크가 결과를 덮어쓰는 경로를 만들지 않았다 (`DEC-028`).

재경기는 RPC가 아니라 NGO 명명 메시지(`CustomMessagingManager`)를 쓴다.
버튼은 결과 씬에서 눌리고 복귀는 경기 씬이라, 씬에 배치된 `NetworkObject`는
그 사이에 사라진다 (`ISSUE-016`과 같은 원인).

## 현재 작업

- 작업 ID: `THROW-001`~`007`, `DOG-009`, `CAT-009` 상호작용 확장
- 상태: `DONE` (돌 배치는 임시)
- 결과: 던지기·설치·기절이 들어갔고 판정은 호스트만 한다. 밤 조명과 경찰 손전등,
  그리고 부채꼴 밖의 도둑을 렌더링하지 않는 시야 제한을 넣었다.
  이미 돌아가면서 보이지 않던 두 기능을 드러냈다 — 강아지 추적의 흔적을 발자국으로,
  고양이 정찰 결과를 방향 마커로 표시한다.
  Edit Mode 147개, Play Mode 98개 통과.
- 남은 것: 돌·바나나 최종 배치(`THROW-005/006`), 관문 B 2인 실기 플레이테스트
- 1차 실기 피드백 반영: 야간이 너무 어두웠고, 마우스가 하는 일이 없었고, 기절과
  손전등 경계가 화면에서 읽히지 않았다. 넷 다 고쳤다. 밝기 수치는 여전히 추정이며
  가로등(`ART-*`)이 들어오면 다시 잡는다

## 이전 작업

- 작업 ID: `ISSUE-011` 도둑 승리 경로, `ART-014` 절차적 보행
- 상태: `DONE`
- 결과: 보물 6개(1,200골드)를 맵 전역에 배치해 도둑의 판매 승리 경로를 열었다.
  관문 B의 차단 요소 두 개가 모두 사라졌다 (`NET-003`도 이미 완료 상태였는데
  문서만 낡아 있었다).
  사지 스윙 축을 리그마다 측정하도록 바꿔 강아지가 다리를 옆으로 벌리던 문제를
  고쳤고, 몸통 상하 운동을 발 디딤에 동기화했다. 사람용 2족 보행을 추가해
  클립이 없는 환경에서도 걷는다.
  Edit Mode 147개, Play Mode 90개 통과.
- 남은 것: 관문 B 2인 실기 플레이테스트, `NET-010` 남은 2개 시나리오

## 이전 작업

- 작업 ID: `ART-013` 너구리 상인 인사 연출과 캐릭터 크기 조정
- 상태: `DONE`
- 결과: 상인이 쓰레기통에 숨어 있다가 다가가면 뚜껑을 열고 올라와 손을 흔든다.
  크기는 강아지 1.36m, 고양이 1.20m, 너구리 1.35m, 쓰레기통 2.08m.
  뚜껑이 열리지 않던 원인 **두 가지**를 찾아 고쳤다. 프리팹 인스턴스는 자식
  재부모화가 거부되는데 `SetParent`가 조용히 무시되고, 최적화 패스가 뚜껑을
  정적 배칭으로 구워 버린다. 둘 다 로그도 예외도 남기지 않는다.
  Edit Mode 141개, Play Mode 85개 통과.
- 남은 것: 통 안쪽 바닥판이 없어 뚜껑이 열리는 순간 지면이 비칠 수 있다

## 이전 작업

- 작업 ID: `NET-011` 로비 실사용 수정, `MAP-006` 맵 확장, 카메라 축소
- 상태: `DONE`
- 결과: **빌드된 로비의 버튼 6개가 전부 죽어 있어 사람이 하는 멀티플레이가
  불가능했다** (`ISSUE-017`). 에디터 시점 `onClick.AddListener`가 씬 저장 시
  버려진 것이다. 연결을 `OnEnable`로 옮기고, 프로브에 `-netJoinMode ui|room`을
  추가해 실제 버튼을 누르는 경로를 검증에 넣었다.
  같은 네트워크 방 목록(LAN 브로드캐스트)을 추가해 IP를 몰라도 참가할 수 있다.
  맵을 80×68m로 확장하고 확장 구역을 규칙적인 격자로 깔아 집 17채를 6열 5행으로
  넣었다. 카메라는 처음 대비 약 29% 확대했다.
  상공 렌더링 도구(`Capture Map Overview`)를 만들어 배치를 눈으로 확인했고,
  그 과정에서 **주택 2채가 북쪽 골목을 막고 있고 경찰서가 서쪽 벽을 3m 뚫고
  나가 있는** 기존 결함을 찾아 함께 고쳤다. 둘 다 플레이 카메라로는 보이지
  않는 종류다. Edit Mode 139개, Play Mode 77개 통과.

## 이전 작업

- 작업 ID: 단계 7 핵심 게임 네트워크화 (`NET-005`~`NET-010`)
- 작업: 보물 소유권, 판매·점수, 체포 판정, 재경기, 연결 종료, 회귀 프로브
- 상태: `NET-005`~`009` `DONE`, `NET-010` `PARTIAL` (6개 중 4개 실측)
- 결과: 상호작용·투기·동료 명령 입력이 모두 호스트로 라우팅되고, 보물 상태·
  소유자, 판매 총액, 체포 진행도·완료가 호스트에서 복제된다. 클라이언트의
  연타 11,174회에도 판매는 1회만 기록됐고 양쪽이 같은 승자
  (`Police / ThiefArrested`)를 표시했다. 재경기는 클라이언트 요청 1건으로
  양쪽이 함께 Game으로 돌아갔고, 상대 이탈은 정확히 1회 처리됐다.
  Edit Mode 123개, Play Mode 77개 통과.
- 미측정: 도둑 승리와 판매·체포 동시 발생. 둘 다 `ISSUE-011` 때문에
  현재 콘텐츠(보물 1개 200G, 목표 1000G)로는 재현할 수 없다.

## 이전 작업

- 작업 ID: 단계 13 사운드와 연출
- 작업: `AUDIO-001`~`003`, `UX-001`~`003`, `MAP-003` 사다리 등반,
  `ISSUE-015` 사다리 상호작용 수정
- 상태: `DONE` (배선), 사운드 클립은 작업자 몫
- 결과: 사운드 이벤트 10종을 정의하고 규칙에서 사운드로만 흐르는 단방향
  옵저버를 연결했다. `GameSoundBank`에 10개 항목이 생겼고 **클립은 전부
  비어 있다.** 첫 플레이 안내, 아이콘 병행 표기, 쿨타임 막대와 동료 현재 행동
  표시를 추가했다. 사다리 3개가 실제로 올라가고 내려온다.
  Edit Mode 118개, Play Mode 68개 통과.

## 이전 작업

- 작업 ID: 단계 12 Blender 최종 모델 적용
- 작업: `MODEL-001` 규격 확정, `ART-002` 검사 도구, `ART-003` Animator,
  `ART-004`·`ART-006`~`010` 모델 적용, `MAP-002` 충돌, `CAT-005` 실제 절도,
  동물 보행 개선
- 상태: `DONE` (저장소 몫)
- 남은 작업자 몫: `MODEL-002` 애니메이션 클립 6종, `MODEL-007` 건물 모델
- 미착수: `MAP-003` 지붕·사다리 루트, `MAP-004` 루트 밸런스, `MAP-005` 스폰
  확정, `ART-011` 시각 효과

## 보류 중인 단계

사용자 지시로 보류했다. 요청 전까지 착수하지 않는다.

- **11단계 음성 명령** (`VOICE-001`~`VOICE-006`)

7단계 네트워크화는 2026-07-27에 보류를 해제하고 `NET-003`~`NET-010`,
`ARREST-006`을 진행했다.

## 이전 작업

- 작업 ID: 단계 9 강아지와 고양이 최소 명령
- 작업: `DOG-001`·`CAT-001`·`DOG-002`·`CAT-002`·`DOG-007`·`CAT-007`·
  `DOG-008`·`CAT-008`·`UI-004`·`VOICE-007`·`DOG-003`·`CAT-004`·
  `UI-005`·`UI-006`
- 상태: `DONE`
- 결과: 동료에 `CharacterController`를 붙여 벽을 통과하지 않게 했고, 추적은
  흔적 기반으로 실제 위치를 완벽히 알지 않으며, 교란은 정보만 만들고 도둑
  위치를 바꾸지 않는다. 명령 버튼·쿨타임·결과 피드백 HUD를 추가했다.
  Edit Mode 118개, Play Mode 62개 통과.

- 이전 작업 ID: `CHAR-001`
- 상태: `SUPERSEDED`
- 결과: 검증 장면 스크립트는 남아 있으나, 신규 캐릭터는 CHAR-001 전용 장면이
  아니라 Game 씬에 직접 적용해 검증했다. Unity `6000.5.4f1` 경로를 찾지
  못했다는 기록은 오진이었다 (`ISSUE-014`).

## 바로 다음 작업

1. **관문 B 2인 실기 플레이테스트.** 차단 요소가 모두 해소됐다
2. `NET-010` 남은 2개 시나리오 (도둑 승리, 판매·체포 동시 발생)
3. `MAP-004` 루트 밸런스 (플레이테스트 결과 반영)
4. `MODEL-002` 애니메이션 클립 6종 (작업자 몫, 현재 클립 0개)
5. 낡은 백로그 상태 정리 (`MAP-002`·`LOOT-005`·`UI-004` 등 완료됐는데 TODO)

## 차단 요소

- (해소) 도둑 판매 승리 경로는 보물 6개 배치로 열렸다 (`ISSUE-011`).
  `NET-010`의 남은 두 시나리오는 이제 측정 가능하며 아직 실행하지 않았다
- 최종 접속 방식 미정: 현재는 직접 IP만이며 Relay·전용 서버는 보류 (`DEC-027`)
- 현재 개발 PC에서 Unity 내장 Windows 받아쓰기 생성 실패
- 실제 음성 입력 기술 미정
- Unity `6000.5.4f1` 실행 파일 경로 미확인으로 CHAR-001 자동 실행 미검증

TECH-003은 공모전 제출 MVP의 차단 요소로 유지한다. 단계 A의 단축키 핵심
프로토타입은 음성 통합과 분리해 진행하며 실제 STT를 구현한 것으로 표현하지 않는다.

## 기술 검증 관문 A

| 항목 | 상태 |
|---|---|
| Unity Windows 빌드 | PASS |
| Blender 테스트 모델 임포트 | PASS |
| 음성 텍스트 출력 | BLOCKED |
| 두 플레이어 접속 | PASS |
| 역할 배정 | PASS |
| 전체 관문 | BLOCKED |

## 최근 검증

| 날짜 | 범위 | 결과 |
|---|---|---|
| 2026-07-24 | 문서 파일과 현재 경로 조사 | 완료 |
| 2026-07-24 | Unity 버전 확인 | `6000.5.4f1` |
| 2026-07-24 | BASE-001 목표 폴더와 Git 경계 검사 | 완료 |
| 2026-07-24 | Unity 배치 임포트와 컴파일 | 오류 없이 종료 |
| 2026-07-24 | BASE-002 패키지 재해석과 Windows 64비트 대상 컴파일 | 오류 없이 종료 |
| 2026-07-24 | BASE-003 기본 씬 생성과 빌드 순서 검사 | 완료 |
| 2026-07-24 | BASE-003 씬별 전환 대상 자동 검사 | 완료 |
| 2026-07-24 | BASE-004 기본 설정 에셋 생성과 전체 범위 검사 | 완료 |
| 2026-07-24 | BASE-004 누락 설정 참조 오류 메시지 검사 | 완료 |
| 2026-07-24 | BASE-005 7개 로그 분류와 출력 형식 검사 | 완료 |
| 2026-07-24 | BASE-005 동일 키 중복 로그 억제 검사 | 완료 |
| 2026-07-24 | BASE-005 빈 `catch`와 매 프레임 로그 정적 검색 | 발견 없음 |
| 2026-07-24 | BASE-006 Edit Mode 테스트 | 5/5 통과 |
| 2026-07-24 | BASE-006 Play Mode 테스트 | 1/1 통과 |
| 2026-07-24 | TECH-001 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-24 | TECH-001 WindowsPlayer 씬·입력·해상도 | 1280×720, 이동 약 8m, 통과 |
| 2026-07-24 | TECH-001 WebGL 로컬 서버·GitHub Pages | Windows 우선으로 미실행 |
| 2026-07-24 | TECH-002 Blender 5.2 `.blend`·FBX 생성 | 성공 |
| 2026-07-24 | TECH-002 Unity Generic 리그·재질·애니메이션 임포트 | 5본, 1재질, `Idle`·`Walk`, 통과 |
| 2026-07-24 | TECH-002 WindowsPlayer 구조·렌더 검증 | 2.31m, `VisualRoot` 분리, 통과 |
| 2026-07-24 | TECH-002 Edit Mode 테스트 | 7/7 통과 |
| 2026-07-24 | TECH-002 Play Mode 테스트 | 1/1 통과 |
| 2026-07-24 | TECH-003 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-24 | TECH-003 마이크와 실패 복구 | 장치 1개, 키보드 대체 입력 통과 |
| 2026-07-24 | TECH-003 Unity 내장 받아쓰기 | `0x80004003`, 차단 |
| 2026-07-24 | TECH-003 실제 발화 텍스트 | 미실행, recognizer 생성 불가 |
| 2026-07-24 | NET-001 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-24 | NET-001 Host·Client 접속과 플레이어 2명 | 통과 |
| 2026-07-24 | NET-001 소유자·위치 분리와 이동 동기화 | 통과 |
| 2026-07-24 | NET-001 Client 종료 처리 | Host 감지, 통과 |
| 2026-07-24 | NET-001 Edit Mode 테스트 | 9/9 통과 |
| 2026-07-24 | NET-001 Play Mode 테스트 | 1/1 통과 |
| 2026-07-24 | NET-002 Host·Client 역할 배정 | `Police` 1명, `Thief` 1명, 통과 |
| 2026-07-24 | NET-002 역할별 시작점과 로컬 역할 인식 | 양쪽 통과 |
| 2026-07-24 | NET-002 세 번째 접속 제한 | 거절 1회, 통과 |
| 2026-07-24 | NET-002 렌더 캡처 | 파란 경찰·빨간 도둑, HUD 확인 |
| 2026-07-24 | NET-002 Edit Mode 테스트 | 15/15 통과 |
| 2026-07-24 | NET-002 Play Mode 테스트 | 1/1 통과 |
| 2026-07-24 | 기술 검증 관문 A | TECH-003 미통과로 `BLOCKED` |
| 2026-07-24 | MAP-001 회색 상자 마을 | 56×44m, 필수 장소 7개, 경로 9개 |
| 2026-07-24 | MAP-001 Windows 횡단 | 72m, 17.58초, 끼임 0회, 통과 |
| 2026-07-24 | MAP-001 Edit Mode 테스트 | 20/20 통과 |
| 2026-07-24 | MAP-001 Play Mode 테스트 | 1/1 통과 |
| 2026-07-24 | BASE-003 씬 계약 회귀 검사 | 오류 없이 종료 |
| 2026-07-24 | MATCH-001 상태 전환 테스트 | Edit Mode 30/30 통과 |
| 2026-07-24 | MATCH-001 Play Mode 회귀 테스트 | 1/1 통과 |
| 2026-07-25 | PLAYER-005 Windows 개발 빌드 | 성공 |
| 2026-07-25 | PLAYER-005 역할 표시 렌더 | 파란 경찰·빨간 도둑 확인 |
| 2026-07-25 | PLAYER-005 Edit Mode 테스트 | 40/40 통과 |
| 2026-07-25 | PLAYER-001 Windows 개발 빌드 | 성공 |
| 2026-07-25 | PLAYER-001 이동 Play Mode 테스트 | 4/4 통과 |
| 2026-07-25 | PLAYER-001 Edit Mode 회귀 테스트 | 40/40 통과 |
| 2026-07-25 | PLAYER-002 Windows 개발 빌드 | 성공 |
| 2026-07-25 | PLAYER-002 Edit Mode 테스트 | 44/44 통과 |
| 2026-07-25 | PLAYER-002 Play Mode 회귀 테스트 | 4/4 통과 |
| 2026-07-25 | PLAYER-003 Windows 개발 빌드 | 성공 |
| 2026-07-25 | PLAYER-003 Play Mode 테스트 | 5/5 통과 |
| 2026-07-25 | PLAYER-004 Windows 개발 빌드 | 성공 |
| 2026-07-25 | PLAYER-004 Edit Mode 테스트 | 45/45 통과 |
| 2026-07-25 | PLAYER-004 Play Mode 테스트 | 7/7 통과 |
| 2026-07-25 | ART-001 Windows 개발 빌드 | 성공 |
| 2026-07-25 | ART-001 Edit Mode 테스트 | 46/46 통과 |
| 2026-07-25 | ART-001 Play Mode 테스트 | 8/8 통과 |
| 2026-07-25 | MATCH-002 Windows 개발 빌드 | 성공 |
| 2026-07-25 | MATCH-002 Edit Mode 테스트 | 46/46 통과 |
| 2026-07-25 | MATCH-002 Play Mode 테스트 | 9/9 통과 |
| 2026-07-25 | MATCH-003 Windows 개발 빌드 | 성공 |
| 2026-07-25 | MATCH-003 Edit Mode 테스트 | 46/46 통과 |
| 2026-07-25 | MATCH-003 Play Mode 테스트 | 10/10 통과 |
| 2026-07-25 | UI-001 Windows 개발 빌드 | 성공 |
| 2026-07-25 | UI-001 Edit Mode 테스트 | 46/46 통과 |
| 2026-07-25 | UI-001 Play Mode 테스트 | 16/16 통과 |
| 2026-07-25 | UI-001 숨김 창 자동 캡처 | 렌더 프레임 미생성으로 시간 초과, Player 예외 없음 |
| 2026-07-25 | UI-007 Windows 개발 빌드 | 성공 |
| 2026-07-25 | UI-007 Edit Mode 테스트 | 47/47 통과 |
| 2026-07-25 | UI-007 Play Mode 테스트 | 19/19 통과 |
| 2026-07-25 | LOOT-001 Windows 개발 빌드 | 성공 |
| 2026-07-25 | LOOT-001 Edit Mode 테스트 | 61/61 통과 |
| 2026-07-25 | LOOT-001 Play Mode 테스트 | 19/19 통과 |
| 2026-07-25 | LOOT-008 Windows 개발 빌드 | 성공 |
| 2026-07-25 | LOOT-008 Edit Mode 테스트 | 63/63 통과 |
| 2026-07-25 | LOOT-008 Play Mode 테스트 | 19/19 통과 |
| 2026-07-25 | LOOT-002 Windows 개발 빌드 | 성공 |
| 2026-07-25 | LOOT-002 Edit Mode 테스트 | 63/63 통과 |
| 2026-07-25 | LOOT-002 Play Mode 테스트 | 20/20 통과 |
| 2026-07-25 | LOOT-003 Windows 개발 빌드 | 성공 |
| 2026-07-25 | LOOT-003 Edit Mode 테스트 | 63/63 통과 |
| 2026-07-25 | LOOT-003 Play Mode 테스트 | 23/23 통과 |
| 2026-07-25 | LOOT-004 Windows 개발 빌드 | 성공 |
| 2026-07-25 | LOOT-004 Edit Mode 테스트 | 63/63 통과 |
| 2026-07-25 | LOOT-004 Play Mode 테스트 | 26/26 통과 |
| 2026-07-25 | PLAYER-006 Windows 개발 빌드 | 성공 |
| 2026-07-25 | PLAYER-006 Edit Mode 테스트 | 65/65 통과 |
| 2026-07-25 | PLAYER-006 Play Mode 테스트 | 28/28 통과 |
| 2026-07-25 | LOOT-006 Windows 개발 빌드 | 성공 |
| 2026-07-25 | LOOT-006 Edit Mode 테스트 | 65/65 통과 |
| 2026-07-25 | LOOT-006 Play Mode 테스트 | 30/30 통과 |
| 2026-07-25 | LOOT-007 Windows 개발 빌드 | 성공 |
| 2026-07-25 | LOOT-007 Edit Mode 테스트 | 65/65 통과 |
| 2026-07-25 | LOOT-007 Play Mode 테스트 | 33/33 통과 |
| 2026-07-25 | UI-003 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | UI-003 Edit Mode 테스트 | 66/66 통과 |
| 2026-07-25 | UI-003 Play Mode 테스트 | 35/35 통과 |
| 2026-07-25 | UI-003 도둑 역할 WindowsPlayer 자동 캡처 | 1920×1080 HUD 배치 및 런타임 예외 없음 |
| 2026-07-25 | MAP-001 횡단 회귀 검증 | 광장 마커 이동·플레이어 충돌 격리 후 자동 횡단 통과 |
| 2026-07-25 | ARREST-001 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | ARREST-001 Edit Mode 테스트 | 66/66 통과 |
| 2026-07-25 | ARREST-001 Play Mode 테스트 | 37/37 통과 |
| 2026-07-25 | ARREST-002 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | ARREST-002 Edit Mode 테스트 | 66/66 통과 |
| 2026-07-25 | ARREST-002 Play Mode 테스트 | 39/39 통과 |
| 2026-07-25 | ARREST-003 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | ARREST-003 Edit Mode 테스트 | 66/66 통과 |
| 2026-07-25 | ARREST-003 Play Mode 테스트 | 42/42 통과 |
| 2026-07-25 | ARREST-004 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | ARREST-004 Edit Mode 테스트 | 66/66 통과 |
| 2026-07-25 | ARREST-004 Play Mode 테스트 | 43/43 통과 |
| 2026-07-25 | ARREST-005 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | ARREST-005 Edit Mode 테스트 | 66/66 통과 |
| 2026-07-25 | ARREST-005 Play Mode 테스트 | 44/44 통과 |
| 2026-07-25 | ARREST-005 숨김 WindowsPlayer 자동 캡처 | 캡처 실패, 시각 근거에서 제외 |
| 2026-07-25 | UI-002 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | UI-002 Edit Mode 테스트 | 66/66 통과 |
| 2026-07-25 | UI-002 Play Mode 테스트 | 45/45 통과 |
| 2026-07-25 | MATCH-004 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | MATCH-004 Edit Mode 테스트 | 72/72 통과 |
| 2026-07-25 | MATCH-004 Play Mode 테스트 | 46/46 통과 |
| 2026-07-25 | MATCH-005 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | MATCH-005 Edit Mode 테스트 | 72/72 통과 |
| 2026-07-25 | MATCH-005 Play Mode 테스트 | 47/47 통과 |
| 2026-07-25 | MATCH-006 Windows x86_64 개발 빌드 | 성공 |
| 2026-07-25 | MATCH-006 Edit Mode 테스트 | 78/78 통과 |
| 2026-07-25 | MATCH-006 Play Mode 테스트 | 48/48 통과 |
| 2026-07-25 | MATCH-006 표시 항목 코드 확인 | 승리 진영·이유·판매액·남은 시간·REMATCH·MAIN MENU 6개 모두 존재 |
| 2026-07-25 | MATCH-007 Edit Mode 회귀 테스트 | 78/78 통과 |
| 2026-07-25 | MATCH-007 Play Mode 테스트 | 49/49 통과 (재경기 테스트 1개 추가) |
| 2026-07-25 | MATCH-007 재경기 7개 완료 조건 | 타이머·점수·보물·위치·체포·중복 HUD·2차 종료 통과 |
| 2026-07-25 | 플레이테스트 Windows x86_64 개발 빌드 | 성공, 3개 씬 포함 |
| 2026-07-25 | 플레이테스트 빌드 실행 | 창모드 1600×900 기동, Player.log 예외 0건 |
| 2026-07-25 | 관문 B 도둑 판매 승리 경로 | 미충족, `ISSUE-011` |
| 2026-07-25 | 제공 프롭 `.blend` 19종 Blender 5.2 FBX 변환 | 19/19 성공 |
| 2026-07-25 | 임시 모델 씬 적용 | 보물 1·좌판 1·쓰레기통 4·사다리 3·캐릭터 2, 폴백 프리미티브 0개 |
| 2026-07-25 | 임시 캐릭터 지면 접지 | 두 역할 모두 높이 1.75m, 발 `y=-0.01m` |
| 2026-07-25 | `ISSUE-012` 시각 루트 좌표 수정 후 Edit Mode | 78/78 통과 |
| 2026-07-25 | `ISSUE-012` 시각 루트 좌표 수정 후 Play Mode | 49/49 통과 |
| 2026-07-25 | 임시 모델 포함 플레이테스트 빌드와 실행 | 성공, Player.log 예외 0건 |
| 2026-07-25 | `ISSUE-013` 카메라 고정 시점 Play Mode 테스트 | 51/51 통과 (카메라 2개 추가) |
| 2026-07-25 | 고정 시점 포함 플레이테스트 빌드와 실행 | 성공, Player.log 예외 0건 |
| 2026-07-25 | 외부 에셋 캐릭터 후보 7종 비율 측정 | 2등신 없음, 최소 3.7등신 |
| 2026-07-25 | 만화풍 임시 캐릭터 적용 | 두 역할 1.70m, 발 `y=0.00m`, 머리 본 2.20배 |
| 2026-07-25 | 만화풍 캐릭터 Edit Mode·Play Mode | 78/78, 51/51 통과 |
| 2026-07-25 | 만화풍 캐릭터 빌드와 실행 | 성공, Player.log 예외·Animator 경고 0건 |
| 2026-07-26 | 신규 모델 11종 비율·리그·클립 측정 | 캐릭터 리그 35~41본, 클립 0개, 약 1유닛 정규화 |
| 2026-07-26 | 건물 `.blend` 5종 FBX 변환 | 5/5 성공 |
| 2026-07-26 | 신규 모델 씬 적용 | 14종 존재, 폴백 0개, 플레이어 1.70m 접지 |
| 2026-07-26 | 경찰·도둑 Humanoid 아바타 생성 | 양쪽 `isValid` 및 `isHuman` 참 |
| 2026-07-26 | 달리기 애니메이터 구성 | `Idle`·`Run` 리타게팅 컨트롤러 생성 |
| 2026-07-26 | COMP-001~006 Edit Mode | **118/118 통과** (동료 26개 추가) |
| 2026-07-26 | COMP-001~006 Play Mode | **57/57 통과** (동료 6개 추가) |
| 2026-07-26 | 동료 씬 배선 확인 | 에이전트 2·전달기 1·브리지 1·입력 2·이동 애니 2 |
| 2026-07-26 | 원격 병합 충돌 검사 | 로컬·원격 동일, 충돌 0건 |
| 2026-07-26 | 동료 포함 빌드와 실행 | 성공, Player.log 예외 0건 |
| 2026-07-27 | NET-005~010 Edit Mode | **123/123 통과** (`ResetTo` 2개, 재경기 브리지 3개 추가) |
| 2026-07-27 | NET-005~010 Play Mode | **77/77 통과** (원격 반영 5개 추가) |
| 2026-07-27 | 씬 재생성 (Bootstrap·Game) | 보물 링크 1개, 플레이어 링크 2개 배선 |
| 2026-07-27 | NET-005 보물 소유권 2프로세스 | 양쪽 `Sold`·소유자 Thief 일치 |
| 2026-07-27 | NET-006 판매 연타 2프로세스 | 요청 11,174회에 `creditedSales: 1`, 양쪽 200G |
| 2026-07-27 | NET-007 체포 진행도 2프로세스 | 양쪽 1.5초·완료 일치 |
| 2026-07-27 | NET-006/007 승자 일치 | 양쪽 `Police / ThiefArrested` |
| 2026-07-27 | NET-008 재경기 2프로세스 | 클라이언트 요청 1건, 양쪽 `Game` 복귀 |
| 2026-07-27 | NET-009 상대 이탈 2프로세스 | 호스트 처리 1회, 반복 없음 |
| 2026-07-27 | NET-010 회귀 시나리오 | 6개 중 4개 PASS, 2개 `ISSUE-011` 대기 |
| 2026-07-27 | 저장된 씬의 버튼 연결 검사 | `m_OnClick` 6개 전부 `m_Calls: []` → `ISSUE-017` |
| 2026-07-27 | 로비 버튼 2프로세스 (`-netJoinMode ui`) | 양쪽 `passed`, `pressedControl` 호스트·접속 버튼 |
| 2026-07-27 | 방 목록 2프로세스 (`-netJoinMode room`) | 클라이언트가 `Room Slot 0`으로 `192.168.31.35:7979` 참가 |
| 2026-07-27 | MAP-006 확장 후 MAP-001 검증 | 통과 (return code 0) |
| 2026-07-27 | MAP-006 확장 후 기본 씬 검증 | 통과 (return code 0) |
| 2026-07-27 | MAP-006 확장 후 NET-005~007 재확인 | 양쪽 `passed: true`, 승자 일치 |
| 2026-07-27 | 확장·카메라·로비 후 Edit Mode | **139/139 통과** (LAN 프로토콜 16개 추가) |
| 2026-07-27 | 확장·카메라·로비 후 Play Mode | **77/77 통과** |
| 2026-07-27 | 상공 평면도 렌더 (80×68m, 960×816px) | 격자 규칙성 확인, 결함 2건 발견 |
| 2026-07-27 | 격자 재배치 후 MAP-001 검증 | 통과 (exit 0) |
| 2026-07-27 | 격자 재배치 후 기본 씬 검증 | 통과 (exit 0) |
| 2026-07-27 | 격자 재배치 후 NET-005~007 | 양쪽 `passed: true`, 승자 일치 |
| 2026-07-27 | 격자 재배치 후 Edit/Play Mode | **139/139**, **77/77 통과** |
| 2026-07-27 | 두 창 실행 (창 모드) | 예외 0건, `inactive controller` 0건 |
| 2026-07-27 | 너구리 리그 팔 각도 실측 | 왼팔 -105°가 손을 어깨 위 +0.22, 오른팔은 -0.11로 아래 |
| 2026-07-27 | 뚜껑 경첩 배선 확인 | 뚜껑 메시가 경첩 아래, `batchingStatic=False` |
| 2026-07-27 | 라쿤 은폐·노출 높이 검산 | 숨김 최상단 1.87m < 테두리 2.08m, 노출 어깨 2.32m |
| 2026-07-27 | ART-013 후 MAP-001 검증 | 통과 (exit 0) |
| 2026-07-27 | ART-013 후 Edit/Play Mode | **141/141**, **85/85 통과** |
| 2026-07-28 | 리그별 스윙 축 실측 | 강아지 X축 앞뒤 0.004 대 옆 0.072 → Z축 채택 |
| 2026-07-28 | 빌드 런타임 사지 수집 확인 | 4종 각 4개, 로그로 실측 |
| 2026-07-28 | 보물 6개 배치 후 MAP-001 검증 | 통과 (경로 막힘 없음) |
| 2026-07-28 | 보물 네트워크 링크 | 6개 자동 연결 |
| 2026-07-28 | ISSUE-011 후 Edit/Play Mode | **147/147**, **90/90 통과** |
| 2026-07-28 | 기절·던지기·덫 판정 테스트 | 8개 추가, 전부 통과 |
| 2026-07-28 | 돌 픽업 5곳 임시 배치 | 양쪽 역할 획득 가능 검사 포함 |
| 2026-07-28 | 라우팅·시야·2묶음 후 2프로세스 | 예외 0건, 승자 일치 |
| 2026-07-28 | 상호작용 확장 후 Edit/Play Mode | **147/147**, **98/98 통과** |
| 2026-07-28 | 조준·연출·야간 후 Edit/Play Mode | **152/152**, **102/102 통과** |
| 2026-07-28 | 평면도 렌더 휘도 측정 | 평균 49.3/255, 검정 뭉침 0% |
| 2026-07-28 | 조준·연출 후 2프로세스 | 예외 0건, 승자 일치 |
| 2026-07-28 | 트리거·배치 수정 후 Edit/Play Mode | **153/153**, **106/106 통과** |
| 2026-07-28 | 2프로세스 던지기 (클라이언트→호스트) | 명중, 기절 1.2초, 양쪽 관측 일치 |
| 2026-07-28 | 소지 복제 후 Edit/Play Mode | **153/153**, **107/107 통과** |
| 2026-07-28 | 2프로세스 돌 획득 | 양쪽 `sawPickedUpRock`·`sawRockTaken` true |
| 2026-07-28 | 별 감기 수정 후 Edit/Play Mode | **153/153**, **109/109 통과** |
| 2026-07-28 | 별 가시성 측정 (실제 씬) | 렌더러 4개·절두체 안·앞면 카메라 향함 |
| 2026-07-28 | 3묶음 후 Edit/Play Mode | **153/153**, **111/111 통과** |
| 2026-07-28 | 3묶음 후 2프로세스 | 예외 0건, 승자 일치, 획득·기절 양쪽 일치 |
| 2026-07-28 | 버퍼·알림·몸통 후 Edit/Play Mode | **157/157**, **113/113 통과** |
| 2026-07-28 | 2프로세스 덫 설치 | 양쪽 `peakTrapCount` 1, 오버플로 0건 |
| 2026-07-28 | 4묶음 후 Edit/Play Mode | **157/157**, **117/117 통과** |
| 2026-07-28 | 2프로세스 경찰 경제 | 양쪽 지갑 120 → 30, 구매 성공 |
| 2026-07-28 | 비행·걷기·레이더 후 Edit/Play Mode | **157/157**, **120/120 통과** |
| 2026-07-28 | 2프로세스 허벅지 스윙 | 수정 전 호스트 24° / 클라 2.5° → 후 양쪽 24° |
| 2026-07-28 | 센서 표시·몸통 되돌림 후 Edit/Play Mode | **157/157**, **121/121 통과** |
| 2026-07-28 | 리그 가중치 측정 | 경찰 다리 38.8% vs 도둑 19.4% (`ART-015`) |
| 2026-07-29 | 수직 움직임 측정 (이동 구간) | 양쪽 0.08m·방향전환 0회·모델 0m |
| 2026-07-29 | 팔 진폭·센서 호 후 Edit/Play Mode | **157/157**, **121/121 통과** |
| 2026-07-29 | 떨림·칸수·은닉 후 Edit/Play Mode | **157/157**, **121/121 통과** |
| 2026-07-29 | 시야·집 크기 후 Edit/Play Mode | **158/158**, **122/122 통과** |
| 2026-07-29 | 집 19채 경계 검사 | 맵 밖 0채, 같은 모델 깊이 편차 0 |
| 2026-07-29 | 실내 후 Edit/Play Mode | **158/158**, **124/124 통과** |
| 2026-07-29 | 2프로세스 실내 배치 | 호스트가 16개 배치·통보, 예외 0건 |
| 2026-07-29 | 실내 획득 후 Edit/Play Mode | **158/158**, **125/125 통과** |
| 2026-07-29 | 실내 3버그 수정 후 Edit/Play Mode | **158/158**, **127/127 통과** |
| 2026-07-29 | 문 6회 왕복 | 매회 바닥 위 유지 |

기능 완료, 단계 변경, 경로 변경 시 이 문서를 함께 갱신한다.
