# 알려진 문제와 공백

마지막 갱신: 2026-07-24

완료된 항목을 삭제하지 않고 해결 상태와 관련 작업을 기록한다.

## 기록 형식

### ISSUE-XXX 제목

- 종류: 버그 / 기술 부채 / 미구현 / 결정 필요
- 상태: OPEN / IN_PROGRESS / RESOLVED / DEFERRED
- 심각도: Critical / High / Medium / Low
- 발견 날짜:
- 발생 환경:
- 재현 또는 확인 절차:
- 예상:
- 실제:
- 영향:
- 임시 해결:
- 관련 작업:
- 해결 기록:

## 현재 항목

### ISSUE-001 레거시 실험물과 새 목표 구조가 공존한다

- 종류: 기술 부채
- 상태: OPEN
- 심각도: High
- 발견 날짜: 2026-07-24
- 발생 환경: 저장소 루트
- 확인 절차: `Assets/CatCops/`와 목표 `Assets/_Project/` 비교
- 예상: 새 프로젝트 자산이 `Assets/_Project/` 아래에 있음
- 실제: 새 `Assets/_Project/` 구조는 생성됐지만 기존 실험 코드와 씬이 `Assets/CatCops/`에 함께 존재
- 영향: 잘못된 코드와 씬을 기준으로 작업할 위험
- 임시 해결: `docs/13_CURRENT_STATE.md`에서 레거시로 명시
- 관련 작업: BASE-001, BASE-002
- 해결 기록:

### ISSUE-002 새 기본 씬이 없었다

- 종류: 미구현
- 상태: RESOLVED
- 심각도: High
- 발견 날짜: 2026-07-24
- 발생 환경: `Assets/_Project/Scenes/`
- 확인 절차: `Bootstrap.unity`, `Game.unity`, `Result.unity` 존재와 빌드 순서 확인
- 예상: 새 MVP 기본 씬과 실행 시작점 존재
- 실제: 세 기본 씬과 공통 전환 구조 생성
- 영향: 해결됨
- 임시 해결: 없음
- 관련 작업: BASE-003
- 해결 기록: 2026-07-24 세 기본 씬, 빌드 순서와 전환 대상 자동 검증 완료

### ISSUE-003 경찰 모델에 리그와 애니메이션이 없다

- 종류: 미구현
- 상태: OPEN
- 심각도: High
- 발견 날짜: 2026-07-24
- 발생 환경: `ArtSource/Police/Police_LowPoly.blend`
- 확인 절차: Blender 오브젝트와 Unity FBX 임포트 확인
- 예상: 테스트 가능한 Avatar와 `Idle`, `Run`, `ComedyRun`
- 실제: 모델과 FBX만 존재
- 영향: 애니메이션 파이프라인 검증 불가
- 임시 해결: 정적 모델을 최종 캐릭터로 취급하지 않음
- 관련 작업: RIG-001~RIG-003, ANIM-001~ANIM-004
- 해결 기록:

### ISSUE-004 Unity 잠금 파일이 남아 있다

- 종류: 환경
- 상태: RESOLVED
- 심각도: Medium
- 발견 날짜: 2026-07-24
- 발생 환경: `Temp/UnityLockfile`
- 확인 절차: Unity 프로세스와 잠금 파일 존재 확인
- 예상: Unity가 종료되면 잠금 파일이 없음
- 실제: 실행 중인 Unity 프로세스 없이 잠금 파일 존재
- 영향: 다음 배치 검증 또는 에디터 실행 전에 상태 확인 필요
- 임시 해결: 검증 전에 프로세스와 잠금 파일을 다시 확인
- 관련 작업: BASE-002
- 해결 기록: 2026-07-24 Unity 프로세스가 없고 잠금 파일이 0바이트인 것을 확인한 뒤 제거했으며, Unity 배치 모드가 정상 종료함

### ISSUE-005 네트워크 방식이 미정이다

- 종류: 결정 필요
- 상태: RESOLVED
- 심각도: Medium
- 발견 날짜: 2026-07-24
- 발생 환경: 설계
- 예상: 제출 MVP의 1대1 실행 방식 확정
- 실제: NGO `2.13.0`과 Unity Transport `6.5.0`으로 로컬 직접 IP 접속, 두 플레이어와 종료 처리를 통과
- 영향: NET-002 역할과 권한 검증을 진행할 수 있음
- 임시 해결: 기술 검증 코드를 Core 규칙과 분리
- 관련 작업: NET-001
- 해결 기록: 2026-07-24 Host·Client 실제 Windows 빌드 결과가 모두 `passed: true`. Relay·Lobby와 최종 권한 구조는 별도 결정으로 남김

### ISSUE-010 NGO 2.7.0이 Unity 6000.5의 Transport API와 컴파일되지 않는다

- 종류: 버그
- 상태: RESOLVED
- 심각도: High
- 발견 날짜: 2026-07-24
- 발생 환경: Unity `6000.5.4f1`, NGO `2.7.0`, Unity Transport `6.5.0`
- 확인 절차: NGO `2.7.0` 설치 후 프로젝트 컴파일
- 예상: 네트워크 기술 검증 코드 컴파일
- 실제: Transport의 obsolete `EntityId` API 사용이 오류로 승격되어 패키지 자체가 컴파일되지 않음
- 영향: NET-001 빌드 불가
- 임시 해결: 없음
- 관련 작업: NET-001
- 해결 기록: 패키지 레지스트리의 호환 최신 버전 NGO `2.13.0`으로 갱신한 뒤 컴파일, Windows 빌드와 두 프로세스 접속 통과

### ISSUE-006 실제 음성 기술이 미정이다

- 종류: 결정 필요
- 상태: DEFERRED
- 심각도: Medium
- 발견 날짜: 2026-07-24
- 발생 환경: 설계
- 예상: STT, 권한, 비용과 실패 복구 계획 확정
- 실제: Windows x86_64를 첫 빌드 대상으로 정했지만 음성 기술은 선택 전
- 영향: 음성 통합 범위 미정
- 임시 해결: 숫자키와 버튼 명령을 먼저 완성
- 관련 작업: VOICE-001~VOICE-007
- 해결 기록: 2026-07-24 첫 빌드 대상을 Windows x86_64로 확정

### ISSUE-007 프로토타입 밸런스 값이 검증되지 않았다

- 종류: 결정 필요
- 상태: OPEN
- 심각도: Low
- 발견 날짜: 2026-07-24
- 발생 환경: `docs/03_GAME_RULES.md`, `Assets/_Project/Settings/Configs/`
- 예상: 플레이테스트 근거가 있는 목표 금액, 체포 시간, 쿨타임
- 실제: 초기 가설값을 ScriptableObject로 중앙화했지만 플레이테스트 근거는 아직 없음
- 영향: 한 진영이 과도하게 유리할 수 있음
- 임시 해결: 확정값과 가설값을 문서에서 구분하고 기능 코드는 검증된 `GameConfigSet`만 사용
- 관련 작업: BASE-004, 단계 5 밸런스 검증
- 해결 기록: 2026-07-24 초기 가설값을 설정 에셋으로 중앙화하고 누락 및 범위 검증 추가

### ISSUE-008 WebGL 배포 경로가 검증되지 않았다

- 종류: 미구현
- 상태: DEFERRED
- 심각도: Low
- 발견 날짜: 2026-07-24
- 발생 환경: WebGL, 로컬 HTTP 서버, GitHub Pages
- 확인 절차: WebGL 빌드, 로컬 서버 실행과 GitHub Pages 배포
- 예상: WebGL을 목표로 채택할 경우 브라우저에서 씬과 입력이 정상 동작
- 실제: TECH-001은 Windows x86_64에서 통과했으며 WebGL은 현재 목표가 아님
- 영향: 링크만으로 실행하는 배포는 아직 제공하지 못함
- 임시 해결: Windows 실행 파일을 첫 시연 대상으로 사용
- 관련 작업: TECH-001, SUBMIT-001
- 해결 기록: WebGL을 보조 플랫폼으로 채택할 때 재개

### ISSUE-009 Unity 내장 Windows 받아쓰기가 현재 개발 PC에서 생성되지 않는다

- 종류: 결정 필요
- 상태: BLOCKED
- 심각도: High
- 발견 날짜: 2026-07-24
- 발생 환경: Unity `6000.5.4f1`, WindowsPlayer, Windows 빌드 `26200`, 한국어 사용자 언어
- 확인 절차: `PawsAndLootVoiceTech.exe` 실행 후 `DictationRecognizer` 생성
- 예상: 마이크 입력을 받아 인식된 텍스트를 화면에 표시
- 실제: 마이크 장치는 1개지만 생성 시 `0x80004003: Speech recognition is not supported on this machine.`
- 영향: Unity 내장 API로는 TECH-003 실제 발화 완료 조건을 충족하지 못함
- 임시 해결: 숫자키와 버튼 대체 입력을 유지하며 게임 명령 개발과 음성 검증을 분리
- 관련 작업: TECH-003, VOICE-001~VOICE-007
- 해결 기록: 오류와 키보드 대체 입력은 WindowsPlayer에서 검증했다. 관리자 권한이 없어 Windows Speech capability 설치 상태는 확정하지 못했으며, 음성 언어 팩 PC 재검증 또는 외부 STT 후보 선정이 필요하다.
