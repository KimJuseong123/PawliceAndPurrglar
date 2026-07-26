# 현재 상태

마지막 갱신: 2026-07-26

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

판정: 현재 프레임은 목표에 여유가 있으나 **렌더러 1,604개는 과다하다.**
평균이 중간값의 1.5배인 것은 초기 로드 구간의 스파이크이며, 정상 구간은
VSync 상한에 붙어 있다. `ART-012` 머티리얼 통합과 정적 배칭을 적용할 근거로
이 수치를 기준선으로 삼는다.

## 현재 작업

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

- **7단계 핵심 게임 네트워크화** (`NET-003`~`NET-010`, `ARREST-006`)
- **11단계 음성 명령** (`VOICE-001`~`VOICE-006`)

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

1. CHAR-001을 Unity `6000.5.4f1`에서 실제 실행
2. 신규 4종 캐릭터의 애니메이션 클립 유무와 기본 재생 확인
3. `ISSUE-011`: 도둑 판매 승리 경로 확보
4. 관문 B 플레이테스트

## 차단 요소

- 도둑 판매 승리 경로 부재로 관문 B가 `BLOCKED` (`ISSUE-011`)
- 로컬 1인 조작만 가능해 실제 경찰 대 도둑 추격 미검증
- 네트워크 서비스와 최종 권한 구조 미정
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

기능 완료, 단계 변경, 경로 변경 시 이 문서를 함께 갱신한다.
