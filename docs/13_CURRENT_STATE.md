# 현재 상태

마지막 갱신: 2026-07-24

이 문서는 작업 시작 시 가장 먼저 확인하는 현재 저장소 상태다.

## 현재 목표

기존 실험 프로토타입을 새 MVP의 기준에서 분리하고, 문서와 목표 구조를 바탕으로 처음부터 그레이박스 프로토타입을 준비한다.

## 현재 단계

```text
단계 3: 회색 상자 맵과 플레이어
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

- 기존 경찰 Blender 원본 존재:

```text
ArtSource/Police/Police_LowPoly.blend
```

- 기존 경찰 FBX 존재:

```text
Assets/CatCops/Models/Police_LowPoly.fbx
```

## 아직 존재하지 않는 목표 결과

- 새 구조의 경찰 프리팹
- 경찰 Armature와 Avatar
- `Idle`, `Run`, `ComedyRun`
- 4분 타이머와 경기 상태 런타임 조립
- 새 보물, 너구리 상인, 체포 시스템
- 숫자키 동물 명령 상태 머신
- 새 결과 화면

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
- 실행 중인 프로세스 없이 남아 있던 `Temp/UnityLockfile`을 제거했다.
- Unity `6000.5.4f1` 배치 모드에서 새 폴더 임포트와 `.meta` 생성을 검증했다.
- 불필요한 2D 편집, Collaborate, Rider, Visual Scripting, Multiplayer Center 패키지를 제거했다.
- Windows 64비트 대상 패키지 재해석과 전체 스크립트 컴파일을 오류 없이 완료했다.
- 기존 TopDown Engine 셰이더의 fallback 관련 비차단 경고가 남아 있다.
- 기본 씬 생성, 빌드 순서와 전환 대상은 에디터 자동 검증을 완료했다.
- 실제 Play Mode에서 버튼을 직접 클릭하는 수동 검증은 아직 수행하지 않았다.

## 현재 작업

- 작업 ID: `ART-001`
- 작업: 캐릭터 VisualRoot 교체 구조
- 상태: `DONE`
- 결과: 모델 교체와 무관한 루트 충돌·이동·상호작용·NetworkObject 구조

## 바로 다음 작업

1. `MATCH-002`: 준비 카운트다운
2. `MATCH-003`: 경기 타이머
3. `UI-001`: 공통 HUD

## 차단 요소

- 네트워크 서비스와 최종 권한 구조 미정
- 현재 개발 PC에서 Unity 내장 Windows 받아쓰기 생성 실패
- 실제 음성 입력 기술 미정

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

기능 완료, 단계 변경, 경로 변경 시 이 문서를 함께 갱신한다.
