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
- AI 생성 결과: `PawsAndLoot.Runtime`, Edit Mode·Play Mode 테스트 어셈블리와 기본 테스트
- 실제 반영 파일: `Assets/_Project/Scripts/PawsAndLoot.Runtime.asmdef`, `Assets/_Project/Tests/`, README와 관련 문서
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

## 기록 시 주의사항

- 비밀 키, 개인정보, 전체 음성 데이터는 기록하지 않는다.
- 외부 저작물을 그대로 생성 결과로 저장하지 않는다.
- AI 생성 코드를 검토와 테스트 없이 완료로 표시하지 않는다.
- 프롬프트 전문이 길면 `prompts/logs/`에 별도 보관하고 이 문서에는 요약과 경로만 적는다.
