# AI 활용 기록

## 기록 원칙

공모전 준비와 개발에서 AI가 설계, 코드, 모델 생성, 분석, 문서 작성에 기여한 주요 작업을 기록한다.
단순 자동완성보다 결과물과 의사결정에 영향을 준 작업을 우선한다.

각 기록에는 AI 결과를 사람이 어떻게 검토했고 무엇을 실제로 반영했는지 포함한다.
실패하거나 폐기한 결과도 중요한 학습이면 삭제하지 않는다.

## 기록 양식

### AI-YYYYMMDD-001

- 날짜:
- 작업자:
- 사용 도구:
- 모델 또는 기능:
- 작업 목적:
- 관련 작업 ID:
- 입력 프롬프트 요약:
- AI 생성 결과:
- 실제 반영 파일:
- 사람이 수정하거나 결정한 내용:
- 검증 방법:
- 최종 상태: 반영 / 일부 반영 / 폐기 / 보류
- 비고:

## 현재 기록

### AI-20260723-001

- 날짜: 2026-07-23
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: 코드 생성, Blender Python, Unity 편집기 자동화
- 작업 목적: 초기 3D 탑다운 프로토타입과 경찰 로우폴리 모델 가능성 검토
- 관련 작업 ID: 레거시 실험
- 입력 프롬프트 요약: 참고 이미지를 바탕으로 Unity 프로토타입과 Blender 로우폴리 모델을 생성
- AI 생성 결과: `Assets/CatCops/` 실험 코드와 씬, 경찰 Blender/FBX, 생성 스크립트
- 실제 반영 파일: `ArtSource/Police/`, `Assets/CatCops/`
- 사람이 수정하거나 결정한 내용: 초기 프로토타입과 모델의 완성도가 목표에 미달한다고 판단하고 프로젝트를 기획 단계부터 재시작
- 검증 방법: Unity 실행 화면과 Blender 렌더를 직접 검토
- 최종 상태: 일부 반영
- 비고: 경찰 모델만 리깅 파이프라인 검증용으로 승계하며 나머지는 새 구현 기준에서 제외

### AI-20260724-001

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: 문서 분석과 작성
- 작업 목적: 프로젝트 방향, MVP 범위, 개발 규칙과 문서 패키지 재정립
- 관련 작업 ID: DOC-001, DOC-002
- 입력 프롬프트 요약: 첨부 초안과 최신 채팅 결정을 우선순위에 따라 통합해 저장소 문서 전체 작성
- AI 생성 결과: `AGENTS.md`, `README.md`, `docs/00`~`docs/15`, `prompts/`, `CHANGELOG.md`
- 실제 반영 파일: 본 기록에 나열된 저장소 문서
- 사람이 수정하거나 결정한 내용: 4분 1대1, 3D 기울어진 탑다운, 숫자키 우선 명령, 까마귀 상인, 경찰 리깅 스파이크 확정
- 검증 방법: 파일 누락, 폐기 용어, 단계와 수치의 문서 간 일관성 검사
- 최종 상태: 반영
- 비고: 실제 게임 구현과 Unity 런타임 검증은 별도 작업

### AI-20260724-002

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: 저장소 분석, 파일 구조 생성, Unity 배치 검증
- 작업 목적: 현재 Unity 프로젝트를 로컬 Git 저장소로 만들고 목표 저장소 경계를 구성
- 관련 작업 ID: BASE-001
- 입력 프롬프트 요약: 기존 작업을 유지한 채 BASE-001의 미완료 구조부터 계속 진행
- AI 생성 결과: Git과 LFS 초기화, `_Project`, `ThirdParty`, `ArtSource/Blender`, `Builds`, `Submission` 구조와 관련 문서 갱신
- 실제 반영 파일: `.gitattributes`, `.gitignore`, Unity 폴더 메타, 저장소 구조 표식, 관련 문서
- 사람이 수정하거나 결정한 내용: `C:\Users\SSAFY\CatCops`를 저장소 루트로 사용
- 검증 방법: 경로 존재, Git ignore, LFS 속성, Unity 배치 임포트와 컴파일 로그 검사
- 최종 상태: 반영
- 비고: 게임 코드, 씬과 기존 외부 에셋은 수정하지 않음

### AI-20260724-003

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: 저장소 분석, 패키지 정리, Unity 배치 검증, 문서 정합성 갱신
- 작업 목적: 팀 개발용 Unity 버전과 패키지 조합을 고정하고 불필요한 의존성을 제거
- 관련 작업 ID: BASE-002
- 입력 프롬프트 요약: Unity 버전, 렌더 파이프라인, 입력, 테스트, 내비게이션, 네트워크 후보와 목표 플랫폼을 결정하고 프로젝트가 열리는지 검증
- AI 생성 결과: 패키지 축소, 개발 환경 표, 네트워크 검증 후보와 Windows x86_64 우선 결정, 너구리 상인 설정 반영
- 실제 반영 파일: `Packages/manifest.json`, `Packages/packages-lock.json`, `AGENTS.md`, `README.md`, 관련 `docs/`
- 사람이 수정하거나 결정한 내용: 판매 NPC를 까마귀에서 너구리로 변경하고 네트워크 패키지는 기술 검증 후 도입하기로 함
- 검증 방법: JSON 파싱, 직접 의존성 검사, Unity `6000.5.4f1` Windows 64비트 배치 임포트와 전체 스크립트 컴파일
- 최종 상태: 반영
- 비고: Netcode for GameObjects와 Unity Multiplayer Services는 후보이며 아직 설치하지 않음

### AI-20260724-004

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity C# 구현, 에디터 자동화, 배치 검증
- 작업 목적: 프로젝트 실행의 최소 씬 흐름과 중앙화된 씬 전환 구조 생성
- 관련 작업 ID: BASE-003
- 입력 프롬프트 요약: Bootstrap, Game, Result 씬을 만들고 시작 씬, Game 이동, 빌드 씬 목록과 중복되지 않는 씬 이름 관리를 구현
- AI 생성 결과: `GameSceneCatalog`, `GameSceneLoader`, 공통 전환 버튼, 세 기본 씬과 씬 생성·검증 도구
- 실제 반영 파일: `Assets/_Project/Scripts/Core/`, `Assets/_Project/Scripts/UI/`, `Assets/_Project/Editor/`, `Assets/_Project/Scenes/`, `ProjectSettings/EditorBuildSettings.asset`
- 사람이 수정하거나 결정한 내용: MVP 기본 씬은 Bootstrap, Game, Result만 먼저 사용
- 검증 방법: Unity 배치 컴파일, 씬 파일과 필수 오브젝트 검사, 씬별 전환 대상과 빌드 순서 자동 검증
- 최종 상태: 반영
- 비고: 실제 에디터 Play Mode에서 버튼을 직접 클릭하는 수동 검증은 별도 수행 필요

### AI-20260724-005

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity C# 구현, ScriptableObject 에셋 생성, 배치 검증
- 작업 목적: 핵심 게임 수치를 코드와 씬에서 분리하고 시작 시 설정 오류를 명확하게 검출
- 관련 작업 ID: BASE-004
- 입력 프롬프트 요약: 경기, 이동, 대시, 체포, 보물, 동물과 음성 관련 공통 설정 구조 생성
- AI 생성 결과: 여섯 기능별 설정, `GameConfigSet`, 런타임 서비스와 Bootstrap, 에디터 생성·검증 도구
- 실제 반영 파일: `Assets/_Project/Scripts/Core/Configuration/`, `Assets/_Project/Settings/Configs/`, Bootstrap 씬, 관련 문서
- 사람이 수정하거나 결정한 내용: 확정되지 않은 수치는 프로토타입 가설로 유지하고 실제 음성 입력은 비활성화
- 검증 방법: Unity 배치 컴파일, 기본 설정 전체 범위 검사, 의도적인 누락 참조 오류 메시지 검사
- 최종 상태: 반영
- 비고: 최종 밸런스는 플레이테스트 후 조정

### AI-20260724-006

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity C# 구현, 에디터 자동 검증, 정적 코드 검색
- 작업 목적: 게임 시스템별 로그를 일관되게 추적하고 제출 빌드의 로그 노이즈를 제한
- 관련 작업 ID: BASE-005
- 입력 프롬프트 요약: Match, Player, Loot, Arrest, Companion, Voice, Network 로그와 오류 처리 기반 구현
- AI 생성 결과: `GameLogger`, `GameLogConfig`, Bootstrap, 7개 분류, 빌드별 최소 레벨, 중복 억제와 예외 기록 API
- 실제 반영 파일: `Assets/_Project/Scripts/Core/Logging/`, `Assets/_Project/Settings/Logging/`, Bootstrap 씬, 관련 문서
- 사람이 수정하거나 결정한 내용: 개발 기본 레벨은 Debug, 제출 기본 레벨은 Warning으로 구분
- 검증 방법: Unity 배치 컴파일, 7개 분류 출력, 동일 키 중복 억제, 빈 `catch`와 매 프레임 로그 정적 검색
- 최종 상태: 반영
- 비고: 파일 및 원격 로그 수집은 MVP 범위에 포함하지 않음

### AI-20260724-007

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity Assembly Definition 구성, Edit Mode·Play Mode 테스트 작성과 배치 실행
- 작업 목적: 런타임 코드와 테스트 코드를 분리하고 반복 가능한 자동 테스트 기반 생성
- 관련 작업 ID: BASE-006
- 입력 프롬프트 요약: 테스트용 Assembly Definition, 간단한 실행 테스트와 README 실행 방법 작성
- AI 생성 결과: `PawliceAndPurrglar.Runtime`, Edit Mode·Play Mode 테스트 어셈블리와 기본 테스트
- 실제 반영 파일: `Assets/_Project/Scripts/PawliceAndPurrglar.Runtime.asmdef`, `Assets/_Project/Tests/`, README와 관련 문서
- 사람이 수정하거나 결정한 내용: Edit Mode는 Editor 전용, Play Mode는 TestAssemblies로 일반 빌드에서 제외
- 검증 방법: Unity Test Runner 배치 실행, Edit Mode 5/5와 Play Mode 1/1 통과 확인
- 최종 상태: 반영
- 비고: 테스트 0개 발견은 성공으로 처리하지 않음

### AI-20260724-008

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 기술 검증 씬·빌드 자동화, Windows 입력 자동 검증
- 작업 목적: 목표 플랫폼의 씬 로딩, 해상도, 키보드 입력과 실행 빌드 가능 여부 확인
- 관련 작업 ID: TECH-001
- 입력 프롬프트 요약: 실제 Windows 빌드에서 큐브를 키보드로 이동할 수 있는지 검증
- AI 생성 결과: `TechnicalTest` 씬, Input System 이동 큐브, 결과 JSON·스크린샷과 Windows 빌드 도구
- 실제 반영 파일: `Assets/_Project/Scenes/TechnicalTest.unity`, `Assets/_Project/Scripts/TechnicalValidation/`, `TechnicalValidationSetup.cs`
- 사람이 수정하거나 결정한 내용: Windows x86_64를 우선 통과시키고 WebGL 서버·GitHub Pages는 보조 플랫폼 재검토 시 수행
- 검증 방법: WindowsPlayer 실행, W 키 입력, 1280×720과 약 8m 이동 결과 JSON, 화면 캡처 확인
- 최종 상태: 반영
- 비고: 빌드 출력과 LocalLow 결과 파일은 저장소에 커밋하지 않음

### AI-20260724-009

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Blender Python 모델·리그 생성, Unity FBX 임포트와 Windows 빌드 자동 검증
- 작업 목적: 최종 모델 제작 전에 Blender와 Unity 사이의 단위, 축, 리그, 애니메이션과 모델 교체 규격 확인
- 관련 작업 ID: TECH-002
- 입력 프롬프트 요약: 단순 몸체, 3~5본, `Idle`, `Walk`, 재질 1개를 실제 빌드에서 검증하고 모델 교체 후 이동과 충돌을 유지
- AI 생성 결과: Blender 5.2 테스트 더미 생성기, `.blend`, `.fbx`, Generic 리그, Animator, `PlayerRoot/VisualRoot` 프리팹과 검증 장면
- 실제 반영 파일: `ArtSource/Blender/TechnicalValidation/`, `Assets/_Project/Art/TechnicalValidation/`, 검증 프리팹·씬·스크립트와 관련 문서
- 사람이 수정하거나 결정한 내용: 기존 경찰 모델은 변경하지 않고 별도 테스트 더미로 파이프라인만 검증
- 검증 방법: Blender 배치 생성, Unity Windows 빌드, LocalLow JSON 전 항목 통과, 실제 렌더 캡처, Edit Mode 7/7과 Play Mode 1/1
- 최종 상태: 반영
- 비고: 최종 경찰의 Humanoid Avatar와 고급 웨이트는 RIG 작업에서 별도로 검증

### AI-20260724-010

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity Windows 음성 API 기술 검증, 오류·대체 입력 UI와 Windows 빌드
- 작업 목적: 숫자키 우선 개발과 별개로 목표 플랫폼의 마이크·STT 가능성과 실패 복구 위험 확인
- 관련 작업 ID: TECH-003
- 입력 프롬프트 요약: 마이크 입력을 텍스트로 표시하고 거부나 실패 시 키보드로 계속 진행하되 AI 명령은 구현하지 않음
- AI 생성 결과: `VoiceTechnicalTest` 장면, Windows 받아쓰기 프로브, 결과 JSON·스크린샷, 키보드 대체 입력
- 실제 반영 파일: 음성 기술 검증 런타임·에디터 스크립트, 장면, README와 관련 문서
- 사람이 수정하거나 결정한 내용: AI 대화와 명령 분류는 제외하고 플랫폼 위험만 격리 검증
- 검증 방법: WindowsPlayer 빌드와 실행, 마이크 1개, 오류 HRESULT, 대체 입력 응답과 화면 캡처 확인
- 최종 상태: 차단 결과 반영
- 비고: 내장 recognizer가 `0x80004003`으로 생성되지 않아 실제 발화 텍스트는 미검증

### AI-20260724-011

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity NGO 기술 검증, Windows 다중 프로세스 실행과 결과 수집
- 작업 목적: 선택한 네트워크 방식으로 Host와 Client가 접속하고 두 플레이어의 위치와 종료를 확인
- 관련 작업 ID: NET-001
- 입력 프롬프트 요약: 두 실행 파일 또는 에디터 인스턴스가 같은 세션에 접속하는 기술 검증 구현
- AI 생성 결과: NGO 기반 NetworkManager, Unity Transport, 네트워크 플레이어, 검증 장면과 Windows 빌드 도구
- 실제 반영 파일: 네트워크 검증 런타임·에디터 스크립트, 장면, 프리팹, 패키지와 관련 문서
- 사람이 수정하거나 결정한 내용: Lobby와 Relay 없이 로컬 직접 IP로 접속 위험만 검증하고 본게임 코드와 격리
- 검증 방법: WindowsPlayer 두 프로세스 실행, Host·Client JSON, 이동 동기화, 종료 콜백, Edit Mode와 Play Mode 테스트
- 최종 상태: 반영
- 비고: NGO `2.7.0`은 Unity `6000.5`의 Transport API와 컴파일되지 않아 `2.13.0`으로 교체

### AI-20260724-012

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity NGO 역할 동기화, 접속 승인, 다중 프로세스 검증
- 작업 목적: 접속한 두 플레이어에게 경찰과 도둑을 중복 없이 배정하고 각 클라이언트가 자기 역할을 확인
- 관련 작업 ID: NET-002
- 입력 프롬프트 요약: 경찰 1명, 도둑 1명, 역할별 시작 위치와 로컬 역할 인식 검증
- AI 생성 결과: 서버 쓰기 역할 변수, 역할 결정 규칙, 2명 접속 제한, 역할별 위치·색상과 결과 기록
- 실제 반영 파일: 네트워크 검증 플레이어·프로브·에디터 도구, 역할 테스트와 관련 문서
- 사람이 수정하거나 결정한 내용: 기술 검증에서는 Host 경찰·첫 Client 도둑으로 고정하고 실제 역할 선택 UI는 구현하지 않음
- 검증 방법: WindowsPlayer 두 프로세스 역할 결과, 세 번째 접속 거절, 렌더 캡처, Edit Mode와 Play Mode 테스트
- 최종 상태: 반영
- 비고: 기술 관문 A는 TECH-003 음성 텍스트 미통과로 전체 `BLOCKED`

### AI-20260724-013

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 회색 상자 씬 생성, 경로 물리 검사와 Windows 자동 횡단
- 작업 목적: 최종 아트 전에 필수 장소와 순환 동선, 복수 경로, 횡단 시간과 끼임 위험 검증
- 관련 작업 ID: MAP-001
- 입력 프롬프트 요약: 경찰·도둑 시작점과 상점·광장·골목·지붕·사다리·쓰레기통이 있는 회색 상자 맵 제작
- AI 생성 결과: 56×44m Game 씬, 위치·경로 정의, 에디터 재생성 도구와 CharacterController 횡단 프로브
- 실제 반영 파일: `Assets/_Project/Scenes/Game.unity`, 맵 런타임·에디터 코드, 회색 상자 재질과 테스트
- 사람이 수정하거나 결정한 내용: 비밀 장터 요구를 고정 암시장 대신 너구리 거래장터 앵커로 해석하고 중앙 조명과 캡슐 여유 폭을 조정
- 검증 방법: Unity 씬 계약 검사, Windows 빌드, 72m 자동 횡단 JSON·스크린샷, Edit Mode와 Play Mode 테스트
- 최종 상태: 반영
- 비고: 실제 횡단 17.58초, 끼임 0회. 최종 카메라와 플레이어 이동은 후속 작업에서 구현

### AI-20260724-014

- 날짜: 2026-07-24
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: 순수 C# 경기 상태 머신과 NUnit 상태 전환 테스트
- 작업 목적: 경기 생명주기를 한 방향으로 고정하고 다른 시스템이 안전하게 현재 상태를 조회하도록 기반 생성
- 관련 작업 ID: MATCH-001
- 입력 프롬프트 요약: `LOBBY`, `READY`, `PLAYING`, `ENDING`, `RESULT`의 유효 전환과 중복 전환 방지 구현
- AI 생성 결과: `MatchState`, 읽기 인터페이스, 상태 변경 값과 `MatchStateMachine`
- 실제 반영 파일: `Assets/_Project/Scripts/Core/Match/`, `Assets/_Project/Tests/EditMode/MatchStateMachineTests.cs`
- 사람이 수정하거나 결정한 내용: 재시작 전환은 MATCH-007로 남기고 현재는 `RESULT`를 종료 상태로 유지
- 검증 방법: 초기 상태, 순차·중복·건너뛰기·역방향·미정의 상태, 이벤트와 `PLAYING` 권한 단위 테스트
- 최종 상태: 반영
- 비고: Edit Mode 30/30, Play Mode 1/1 통과. 카운트다운과 타이머는 포함하지 않음

### AI-20260725-015

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 역할 데이터, 권한 행렬, 회색 상자 씬 표시와 NUnit 테스트
- 작업 목적: 이동 전에 본게임 경찰·도둑 역할과 시작 위치, 상호작용 가능 범위를 명확히 구분
- 관련 작업 ID: PLAYER-005
- 입력 프롬프트 요약: Police·Thief 정의, 서로 다른 시작 위치, 역할별 상호작용 권한과 임시 색상 표시 구현
- AI 생성 결과: `PlayerRole`, `PlayerRolePermissions`, `PlayerRoleSpawnResolver`, `PlayerRoleIdentity`
- 실제 반영 파일: `Assets/_Project/Scripts/Gameplay/Players/`, Game 씬 생성기, 역할 테스트와 관련 문서
- 사람이 수정하거나 결정한 내용: 기술 검증 역할 타입을 본게임에 재사용하지 않고 역할 권한을 단일 표로 중앙화
- 검증 방법: Windows 빌드·렌더 캡처, 시작점 거리와 권한 행렬, 씬 역할 표시 Edit Mode 테스트
- 최종 상태: 반영
- 비고: 역할 표시는 최종 캐릭터가 아닌 파란색·빨간색 캡슐 마커

### AI-20260725-016

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity Input System, CharacterController 이동과 Play Mode 물리 테스트
- 작업 목적: 경찰이 회색 상자 맵을 프레임 독립적으로 이동하고 충돌·계단·경기 상태 제한과 카메라 추적을 검증
- 관련 작업 ID: PLAYER-001
- 입력 프롬프트 요약: WASD, 벽 충돌, 경사 또는 계단, 카메라 추적, PLAYING 중 이동 구현
- AI 생성 결과: `PlayerMovementMotor`, `PlayerKeyboardInput`, `TopDownFollowCamera`, 최소 `MatchRuntimeState`
- 실제 반영 파일: 플레이어·카메라 런타임 코드, Game 씬 생성기, Play Mode 이동 테스트와 관련 문서
- 사람이 수정하거나 결정한 내용: 키보드 입력과 이동 물리를 분리하고 CharacterController 이동을 양 역할 공통 기반으로 사용
- 검증 방법: Windows 빌드, 벽·계단·프레임 독립성·상태 제한·카메라 Play Mode 테스트
- 최종 상태: 반영
- 비고: MatchRuntimeState의 즉시 PLAYING 시작은 MATCH-002에서 카운트다운으로 교체 예정

### AI-20260725-017

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 공통 이동 조립, 실행 인자 역할 선택과 씬 계약 테스트
- 작업 목적: 경찰 이동 코드를 복사하지 않고 도둑에게 같은 이동·충돌 규칙 적용
- 관련 작업 ID: PLAYER-002
- 입력 프롬프트 요약: 도둑 이동, 역할별 시작 위치, 경찰과 같은 충돌 규칙과 공통 구조 재사용
- AI 생성 결과: `LocalPlayerRoleSelector`, `PlayerRoleControlBinding`, 양 역할 공통 씬 조립
- 실제 반영 파일: 역할 선택 런타임 코드, Game 씬 생성기, 공유 이동 씬 테스트와 관련 문서
- 사람이 수정하거나 결정한 내용: 단일 로컬 빌드에서는 실행 인자로 역할을 선택하고 선택된 역할만 입력·카메라를 소유
- 검증 방법: Windows 빌드, 두 역할 컴포넌트 동일성, 인자 해석, Edit Mode와 Play Mode 테스트
- 최종 상태: 반영
- 비고: 실제 네트워크 소유권 연결은 후속 네트워크 통합 범위

### AI-20260725-018

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity CharacterController 대시 상태와 Play Mode 테스트
- 작업 목적: 공통 플레이어에 짧은 가속, 쿨타임, 충돌과 경기 상태 제한 추가
- 관련 작업 ID: PLAYER-003
- 입력 프롬프트 요약: 대시 쿨타임, 벽 통과 방지, 연속 입력 차단, 경기 종료 후 사용 불가와 상태 조회
- AI 생성 결과: `PlayerMovementMotor` 대시 상태, Space 입력과 쿨타임 조회 API
- 실제 반영 파일: 공통 이동 모터, 키보드 입력, 대시 Play Mode 테스트와 관련 문서
- 사람이 수정하거나 결정한 내용: 대시도 일반 이동과 같은 CharacterController 경로를 사용하고 방향은 시작 시 고정
- 검증 방법: Windows 빌드, 비활성 경기·연속 요청·쿨타임·벽 충돌·종료 취소 테스트
- 최종 상태: 반영
- 비고: HUD 쿨타임 표시는 UI-001에서 연결

### AI-20260725-019

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 물리 범위 탐색, 역할 권한, Input System과 UI 연결
- 작업 목적: 역할과 경기 상태를 검사하는 공통 상호작용 기반 구현
- 관련 작업 ID: PLAYER-004
- 입력 프롬프트 요약: 최근접 유효 대상, 권한 검사, 경기 상태 검사, 대상 소멸 안전성과 안내 UI
- AI 생성 결과: 상호작용 계약·스캐너·입력·프로토타입 대상·안내 Presenter
- 실제 반영 파일: 플레이어 런타임 코드, PlayerConfig, Game 씬 생성기, Edit/Play Mode 테스트
- 사람이 수정하거나 결정한 내용: 실제 보물·판매 규칙은 후속 LOOT 작업으로 남기고 프로토타입 이벤트만 실행
- 검증 방법: Windows 빌드, 역할별 대상 선택, 비활성 경기, 파괴된 대상과 씬 계약 테스트
- 최종 상태: 반영
- 비고: Edit Mode 45/45, Play Mode 7/7 통과

### AI-20260725-020

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 계층 분리, NGO 컴포넌트와 모델 교체 회귀 테스트
- 작업 목적: Blender 모델을 나중에 교체해도 플레이어 물리와 조작을 유지하는 구조 확정
- 관련 작업 ID: ART-001
- 입력 프롬프트 요약: PlayerRoot와 VisualRoot 분리, 이동의 애니메이션 비의존, 모델 교체 가능
- AI 생성 결과: `PlayerVisualRoot`, 역할별 `VisualRoot/PlaceholderModel`, 교체 검증 테스트
- 실제 반영 파일: 플레이어 런타임 코드, Game 씬 생성기, Edit/Play Mode 테스트
- 사람이 수정하거나 결정한 내용: CharacterController는 동작 특성상 PlayerRoot에 유지하고 시각 모델만 자식으로 분리
- 검증 방법: Windows 빌드, 씬 계층 계약, 모델 교체 전후 충돌 인스턴스와 이동 비교
- 최종 상태: 반영
- 비고: Edit Mode 46/46, Play Mode 8/8 통과

### AI-20260725-021

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 경기 상태, 설정 기반 시간 진행과 Play Mode 테스트
- 작업 목적: 모든 플레이어 준비 이후 경기 시작 전에 입력이 잠기는 카운트다운 구현
- 관련 작업 ID: MATCH-002
- 입력 프롬프트 요약: 3~5초 READY, 이동 불가, 중복 시작 방지, 종료 후 PLAYING 전환
- AI 생성 결과: MatchConfig 준비 시간, MatchRuntimeState 카운트다운과 Tick API
- 실제 반영 파일: 경기 설정·런타임, Game 씬 생성기와 Play Mode 테스트
- 사람이 수정하거나 결정한 내용: MVP 기본 준비 시간은 3초, 현재 자동 준비 완료로 카운트다운 시작
- 검증 방법: Windows 빌드, 중복 요청·경계 시간·상태 전환 Play Mode 테스트
- 최종 상태: 반영
- 비고: Edit Mode 46/46, Play Mode 9/9 통과

### AI-20260725-022

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 경기 시간 상태와 Play Mode 경계 테스트
- 작업 목적: PLAYING 구간에만 진행되는 4분 타이머와 종료 전환 구현
- 관련 작업 ID: MATCH-003
- 입력 프롬프트 요약: PLAYING 중 감소, 0 하한, 종료 정지, 재시작 초기화와 UI 조회
- AI 생성 결과: MatchRuntimeState 남은 시간, 만료 전환과 타이머 초기화 API
- 실제 반영 파일: 경기 런타임과 Play Mode 타이머 테스트
- 사람이 수정하거나 결정한 내용: 타이머 만료는 결과 판정 전 단계인 ENDING까지만 전환
- 검증 방법: Windows 빌드, 상태별 시간 진행·하한·정지·초기화 테스트
- 최종 상태: 반영
- 비고: Edit Mode 46/46, Play Mode 10/10 통과

### AI-20260725-023

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity UGUI Presenter, 시간 서식과 수명주기 테스트
- 작업 목적: 핵심 경기 정보를 규칙 변경 없이 표시하는 공통 HUD 구현
- 관련 작업 ID: UI-001
- 입력 프롬프트 요약: 남은 시간, 역할, 상호작용 안내, 경기 상태와 중복 생성 방지
- AI 생성 결과: `CommonHudPresenter`, Game 씬 HUD 구성과 표시 테스트
- 실제 반영 파일: UI 런타임, Game 씬 생성기, Edit/Play Mode 테스트
- 사람이 수정하거나 결정한 내용: 기존 단독 상호작용 Presenter를 공통 HUD에 통합하고 HUD를 읽기 전용으로 유지
- 검증 방법: Windows 빌드, 시간 서식·상태 반영·규칙 비변경·중복 인스턴스 테스트
- 최종 상태: 반영
- 비고: Edit Mode 46/46, Play Mode 16/16 통과. 숨김 창 자동 캡처는 렌더 프레임 문제로 미완료

### AI-20260725-024

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 역할별 UI Presenter와 상태 시간 테스트
- 작업 목적: 경기 시작 시 각 역할의 승리 목표를 짧게 이해시키는 안내 구현
- 관련 작업 ID: UI-007
- 입력 프롬프트 요약: 경찰·도둑 목표를 3문장 이내로 경기 시작 시 표시
- AI 생성 결과: `RoleObjectivePresenter`, 역할 문구와 Game 씬 목표 패널
- 실제 반영 파일: UI 런타임, Game 씬 생성기, Edit/Play Mode 테스트
- 사람이 수정하거나 결정한 내용: READY 전체와 PLAYING 시작 후 4초 노출, 결과 화면은 MATCH-006으로 유지
- 검증 방법: Windows 빌드, 역할 변경·표시 수명·문장 수·상태 비변경 테스트
- 최종 상태: 반영
- 비고: Edit Mode 47/47, Play Mode 19/19 통과

### AI-20260725-025

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: 순수 C# 상태 머신과 NUnit 전환 행렬 테스트
- 작업 목적: 보물의 전체 생명주기와 판매 종료 상태를 규칙으로 고정
- 관련 작업 ID: LOOT-001
- 입력 프롬프트 요약: 6개 상태, 유효 전환만 허용, SOLD 재사용 금지
- AI 생성 결과: `LootState`, `LootStateMachine`, 상태 변경 값과 테스트
- 실제 반영 파일: Gameplay/Loot 런타임과 Edit Mode 테스트
- 사람이 수정하거나 결정한 내용: 전환 집합은 docs/03_GAME_RULES.md와 정확히 일치시킴
- 검증 방법: 허용·건너뛰기·중복·SOLD 종료 상태와 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 61/61, Play Mode 19/19 통과

### AI-20260725-026

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity ScriptableObject 데이터와 AssetDatabase 검증
- 작업 목적: 보물 종류와 가격을 이름·UI가 아닌 데이터로 관리
- 관련 작업 ID: LOOT-008
- 입력 프롬프트 요약: 일반 200, 고급 350, 희귀 500과 데이터 기반 가격 조회
- AI 생성 결과: `LootDefinition`, 기본 정의 3종과 생성·검증 도구
- 실제 반영 파일: Gameplay/Loot, Assets/_Project/Data/Loot, Edit Mode 테스트
- 사람이 수정하거나 결정한 내용: stable ID와 표시 이름은 가격 계산에서 제외하고 rarity만 LootConfig에 전달
- 검증 방법: 기본 에셋 수·ID·희귀도별 가격과 이름 비의존 테스트, Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 63/63, Play Mode 19/19 통과

### AI-20260725-027

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity C# 상호작용·소유권 구현과 Play Mode 테스트
- 작업 목적: 도둑만 경기 중 보물을 하나 획득할 수 있는 핵심 규칙 구현
- 관련 작업 ID: LOOT-002
- 입력 프롬프트 요약: 역할·경기 상태·단일 소지·중복 획득 제한과 CARRIED 전환
- AI 생성 결과: `LootCarrier`, `LootItem`, Game 씬 연결과 획득 테스트
- 실제 반영 파일: Gameplay/Loot 런타임, Game 씬 생성기·씬, Edit/Play Mode 테스트
- 사람이 수정하거나 결정한 내용: 소유권은 Transform이 아닌 상호 참조와 상태로 관리하고 시각 부착은 LOOT-003으로 분리
- 검증 방법: 역할·경기 상태·단일 소지·중복 요청 테스트와 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 63/63, Play Mode 20/20 통과

### AI-20260725-028

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity Transform 계층·컴포넌트 수명주기 구현과 Play Mode 테스트
- 작업 목적: 보물 외형이 소지자를 따라오되 상태와 모델 계층을 분리
- 관련 작업 ID: LOOT-003
- 입력 프롬프트 요약: 시각 추적, 명확한 소유권, 소지자 파괴·비활성화 안전 처리
- AI 생성 결과: 플레이어 `CarryPoint`, 보물 `PresentationRoot`, 충돌체 전환과 소지자 상실 복구
- 실제 반영 파일: Gameplay/Loot 런타임, Game 씬 생성기·씬, Edit/Play Mode 테스트
- 사람이 수정하거나 결정한 내용: 보물 전체가 아니라 외형 루트만 부착하고 소지자 상실은 `DROPPED`로 복구
- 검증 방법: 위치 추적·충돌체·상태·비활성화·파괴 테스트와 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 63/63, Play Mode 23/23 통과

### AI-20260725-029

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 물리 레이캐스트·입력·상태 전환 구현과 Play Mode 테스트
- 작업 목적: 소지 보물을 안전하게 내려놓고 다시 획득하는 루프 구현
- 관련 작업 ID: LOOT-004
- 입력 프롬프트 요약: 수동 드롭, 지형 아래 낙하 방지, 재획득과 중복 요청 무시
- AI 생성 결과: `LootDropInput`, `LootGroundPlacement`, 드롭 API와 테스트
- 실제 반영 파일: Gameplay/Loot, 역할 입력 바인딩, Game 씬 생성기·씬, Play Mode 테스트
- 사람이 수정하거나 결정한 내용: `Q`를 임시 드롭 키로 지정하고 바닥을 찾지 못하면 소지를 유지
- 검증 방법: 지면 배치·재획득·비활성 경기·바닥 없음·중복 요청과 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 63/63, Play Mode 26/26 통과

### AI-20260725-030

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 설정·이벤트 기반 이동 배율 구현과 Play Mode 테스트
- 작업 목적: 보물을 운반하는 도둑에게 중복되지 않는 10% 이동 페널티 적용
- 관련 작업 ID: PLAYER-006
- 입력 프롬프트 요약: 소지 시작 적용, 드롭·판매 해제, 중복 방지와 재경기 초기화
- AI 생성 결과: PlayerConfig 배율, `LootCarryMovementPenalty`, 공통 이동 모터 배율 API와 테스트
- 실제 반영 파일: PlayerConfig, Gameplay/Players, Game 씬 생성기·씬, Edit/Play Mode 테스트
- 사람이 수정하거나 결정한 내용: 일반 이동과 대시에 같은 0.9배를 적용하고 판매 해제는 LOOT-006에서 통합 검증
- 검증 방법: 기본·운반 속도, 중복 적용, 드롭과 수명주기 복구, Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 65/65, Play Mode 28/28 통과

### AI-20260725-031

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 상호작용 구역·거래 상태·이벤트 구현과 Play Mode 테스트
- 작업 목적: 너구리 장터에서 보물을 판매해 도둑 금액을 올리는 핵심 루프 완성
- 관련 작업 ID: LOOT-006
- 입력 프롬프트 요약: 역할·소지·구역 검사, 가격 정산, SOLD, 소지 해제와 승리 검사 요청
- AI 생성 결과: `LootSaleZone`, `ThiefLootWallet`, 판매 트랜잭션과 씬·테스트 연결
- 실제 반영 파일: Gameplay/Loot, Game 씬 생성기·씬, Edit/Play Mode 테스트
- 사람이 수정하거나 결정한 내용: 판매 NPC는 너구리 장터로 유지하고 승리 확정 대신 검사 요청 이벤트만 발행
- 검증 방법: 경찰·빈손·구역·경기 상태, 200골드 정산, SOLD·페널티 해제·이벤트와 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 65/65, Play Mode 30/30 통과

### AI-20260725-032

- 날짜: 2026-07-25
- 작업자: 프로젝트 담당자
- 사용 도구: OpenAI Codex
- 모델 또는 기능: 멱등 요청 ID·정산 원장 구현과 Play Mode 경계 테스트
- 작업 목적: 입력 연타와 네트워크 중복 메시지가 보물·판매 상태를 두 번 반영하지 못하게 함
- 관련 작업 ID: LOOT-007
- 입력 프롬프트 요약: 중복 획득·판매, 네트워크 재전송, 경기 종료 직전 판매와 SOLD 재판매 차단
- AI 생성 결과: `LootRequestId`, 플레이어 성공 요청 이력, 지갑 요청·보물 이중 원장과 테스트
- 실제 반영 파일: Gameplay/Loot 런타임과 Play Mode 테스트
- 사람이 수정하거나 결정한 내용: 요청 ID는 플레이어 범위에서 고유하게 사용하고 실패 요청은 상태를 변경하지 않음
- 검증 방법: 드롭 후 과거 획득 재전송, 판매 연타·동일 메시지·SOLD 재판매·ENDING 경계와 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 65/65, Play Mode 33/33 통과

### AI-20260725-033

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity uGUI Presenter 구현, 씬 조립, Play Mode 테스트와 WindowsPlayer 화면 검증
- 사용 목적: 도둑의 보물 판매 진행 상황과 현재 행동 가능 여부를 읽기 전용 HUD로 표시
- 관련 작업 ID: UI-003
- 입력 프롬프트 요약: 판매 금액, 목표 금액, 보유 보물, 가격, 이동 페널티와 판매 가능 여부 표시
- AI 생성 결과: `ThiefHudPresenter`, Game 씬 우측 HUD, 상태별 Play Mode 테스트
- 실제 반영 파일: UI 런타임, Game 씬 생성기와 씬, Edit/Play Mode 테스트, 관련 문서
- 사람이 수정하거나 결정한 내용: HUD는 게임 규칙을 변경하지 않고 기존 지갑·소지·이동·상호작용 상태만 읽도록 제한
- 검증 방법: 빈손·운반·판매 구역·판매 완료·경찰 역할 전환 테스트, Windows 빌드와 1920×1080 자동 캡처
- 최종 상태: 반영
- 비고: Edit Mode 66/66, Play Mode 35/35 통과. 화면 검증 중 발견한 이동 모터 초기화 순서 예외, 광장 마커와 대기 플레이어의 자동 횡단 방해도 수정

### AI-20260725-034

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity Physics 거리·시야 감지와 Play Mode 테스트
- 사용 목적: 경찰이 유효한 도둑을 체포할 수 있는 거리와 장애물 조건을 검증
- 관련 작업 ID: ARREST-001
- 입력 프롬프트 요약: 거리 검사, 장애물 검사, 유효 대상, 진입·이탈 이벤트와 자기 자신 방지
- AI 생성 결과: `ArrestRangeSensor`, Game 씬 경찰 연결, 물리·이벤트 테스트
- 실제 반영 파일: Gameplay/Arrest, Game 씬 생성기와 씬, Edit/Play Mode 테스트, 관련 문서
- 사람이 수정하거나 결정한 내용: 센서는 감지만 소유하고 경기 상태와 진행도는 후속 작업으로 분리
- 검증 방법: 거리 밖·안, 장애물 생성, 비활성 도둑, 자기 참조 거부와 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 66/66, Play Mode 37/37 통과

### AI-20260725-035

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 경기 상태 기반 체포 시간 누적과 Play Mode 테스트
- 사용 목적: 유효한 감지가 유지되는 동안 1.5초 체포 진행도를 계산
- 관련 작업 ID: ARREST-002
- 입력 프롬프트 요약: 프레임 독립 진행, 0~1 진행도, 중복 진행 방지, 경기 중 작동
- AI 생성 결과: `ArrestProgressController`, Game 씬 연결, 진행 조건과 상한 테스트
- 실제 반영 파일: Gameplay/Arrest, Game 씬 생성기와 씬, Edit/Play Mode 테스트, 관련 문서
- 사람이 수정하거나 결정한 내용: 완료 이벤트와 승리 요청은 ARREST-004로 분리
- 검증 방법: 비경기·미감지·음수 시간·부분 진행·완료 상한과 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 66/66, Play Mode 39/39 통과

### AI-20260725-036

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 체포 중단 상태와 이벤트 테스트
- 사용 목적: 체포 조건이 깨질 때 누적 진행도를 안전하게 초기화
- 관련 작업 ID: ARREST-003
- 입력 프롬프트 요약: 범위 이탈, 장애물, 참가자 비활성, 경기 상태 변경과 초기화
- AI 생성 결과: `ArrestInterruptionReason`, 중단 이벤트와 초기화 로직, 조건별 테스트
- 실제 반영 파일: Gameplay/Arrest, Play Mode 테스트, 관련 문서
- 사람이 수정하거나 결정한 내용: 범위와 장애물은 센서 관점에서 동일한 대상 감지 상실로 분류
- 검증 방법: 범위 이동·장애물 생성·오브젝트 비활성·경기 상태 변경과 반복 평가
- 최종 상태: 반영
- 비고: Edit Mode 66/66, Play Mode 42/42 통과

### AI-20260725-037

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 체포 완료와 경기 상태 통합 테스트
- 사용 목적: 체포 완료를 한 번 확정하고 경찰 승리와 결과 연출을 요청
- 관련 작업 ID: ARREST-004
- 입력 프롬프트 요약: 단일 완료 이벤트, 경찰 승리 요청, 이동·상호작용 중지, 결과 연출 시작
- AI 생성 결과: `ArrestCompletionController`, 완료 잠금, Game 씬 연결과 통합 테스트
- 실제 반영 파일: Gameplay/Arrest, Game 씬 생성기와 씬, Edit/Play Mode 테스트, 관련 문서
- 사람이 수정하거나 결정한 내용: 최종 승자 데이터와 Result 씬은 MATCH 작업으로 남기고 요청 이벤트까지만 구현
- 검증 방법: 실제 MatchRuntimeState 전환, 반복 완료, 이동 모터와 상호작용 스캐너 확인
- 최종 상태: 반영
- 비고: Edit Mode 66/66, Play Mode 43/43 통과

### AI-20260725-038

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity uGUI 체포 상태 Presenter와 Play Mode 테스트
- 사용 목적: 경찰 체포 게이지와 도둑 위험 경고를 역할별로 표시
- 관련 작업 ID: ARREST-005
- 입력 프롬프트 요약: 진행 게이지, 체포 중단·완료, 도둑 위험 경고
- AI 생성 결과: `ArrestHudPresenter`, Game 씬 UI 조립, 역할별 상태 테스트
- 실제 반영 파일: Scripts/UI, Game 씬 생성기와 씬, Edit/Play Mode 테스트, 관련 문서
- 사람이 수정하거나 결정한 내용: UI는 규칙을 바꾸지 않고 체포 시스템을 읽기만 하며 UI-006을 이 작업에 통합
- 검증 방법: 경찰·도둑 역할 전환, 절반 진행, 범위 이탈, 완료와 반복 Refresh
- 최종 상태: 반영
- 비고: Edit Mode 66/66, Play Mode 44/44, Windows 빌드 통과. 숨김 창 화면 캡처는 실패하여 근거에서 제외

### AI-20260725-039

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity uGUI 경찰 역할 HUD와 상태 통합 테스트
- 사용 목적: 경찰에게 경기와 도둑의 핵심 상태를 한 패널로 제공
- 관련 작업 ID: UI-002
- 입력 프롬프트 요약: 남은 시간, 판매액, 체포 진행도, 도난 알림, 현재 목표
- AI 생성 결과: `PoliceHudPresenter`, Game 씬 역할 패널, 상태별 Play Mode 테스트
- 실제 반영 파일: Scripts/UI, Game 씬 생성기와 씬, Edit/Play Mode 테스트, UI 문서
- 사람이 수정하거나 결정한 내용: 동물 상태와 명령 UI는 Companion 작업으로 남기고 현재 구현 데이터만 표시
- 검증 방법: 10초 타이머 진행, 보물 획득·판매, 체포 절반 진행, 역할 전환, 반복 Refresh
- 최종 상태: 반영
- 비고: Edit Mode 66/66, Play Mode 45/45, Windows 빌드 통과

### AI-20260725-040

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 중앙 승패 판정기와 순수 규칙 단위 테스트
- 사용 목적: 체포·판매·시간 종료 조건을 한 번만 일관되게 판정
- 관련 작업 ID: MATCH-004
- 입력 프롬프트 요약: 경찰·도둑 승리 조건, 동시 판정 기준, UI 분리, 단위 테스트
- AI 생성 결과: `MatchResult`, `MatchResultArbiter`, `MatchResultEvaluator`, 타이머 만료 이벤트와 씬 연결
- 실제 반영 파일: Core/Match, Arrest 완료, Game 씬 생성기와 씬, Edit/Play Mode 테스트, 경기 규칙 문서
- 사람이 수정하거나 결정한 내용: 같은 평가 시점 우선순위를 체포, 목표 판매, 시간 종료로 확정
- 검증 방법: 세 승리 조건, 동시 요청, 중복 요청, 타이머 이벤트와 Windows 빌드
- 최종 상태: 반영
- 비고: Edit Mode 72/72, Play Mode 46/46 통과

### AI-20260725-041

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 경기 종료 컨트롤러와 통합 테스트
- 사용 목적: 확정된 승패를 한 번만 경기 종료 상태로 반영하고 플레이 입력을 정지
- 관련 작업 ID: MATCH-005
- 입력 프롬프트 요약: ENDING 전환, 입력·타이머·보물·체포 중지, 중복 종료 방지
- AI 생성 결과: `MatchEndController`, 역할 입력 일괄 차단, 체포 종료 처리와 Game 씬 연결
- 실제 반영 파일: Core/Match, Players, Arrest, Game 씬 생성기와 씬, Edit/Play Mode 테스트, 관련 문서
- 사람이 수정하거나 결정한 내용: 승패 판정과 종료 처리를 분리하고 확정 결과만 종료 컨트롤러가 수신
- 검증 방법: 전체 Edit/Play Mode 테스트와 Windows x86_64 개발 빌드
- 최종 상태: 반영
- 비고: Edit Mode 72/72, Play Mode 47/47 통과

### AI-20260725-042

- 날짜: 2026-07-25
- 작성자 / 프로젝트 담당자:
- 사용 도구: OpenAI Codex
- 모델 또는 기능: Unity 결과 씬 전환과 읽기 전용 결과 Presenter
- 사용 목적: 한 경기의 확정 결과를 별도 Result 씬에서 일관되게 표시
- 관련 작업 ID: MATCH-006
- 입력 프롬프트 요약: 승리 진영·이유·판매액·남은 시간, 재경기·메인 메뉴 버튼
- AI 생성 결과: `MatchResultSession`, `MatchResultFlowController`, `ResultScreenPresenter`, Result 씬 UI
- 실제 반영 파일: Core/Match, UI, Game·Result 씬 생성기와 씬, Edit/Play Mode 테스트, UI 문서
- 사람이 수정하거나 결정한 내용: 임시 결과 버튼을 제거하고 정상 종료 후 자동으로 Result 씬 전환
- 검증 방법: 세 결과 문구 단위 검증, 실제 Game 종료 통합 테스트, Windows x86_64 빌드
- 최종 상태: 반영
- 비고: Edit Mode 78/78, Play Mode 48/48 통과

## 기록 시 주의사항

- 비밀 키, 개인정보, 전체 음성 데이터는 기록하지 않는다.
- 외부 저작물을 그대로 생성 결과로 저장하지 않는다.
- AI 생성 코드를 검토와 테스트 없이 완료로 표시하지 않는다.
- 프롬프트 전문이 길면 `prompts/logs/`에 별도 보관하고 이 문서에는 요약과 경로만 적는다.
