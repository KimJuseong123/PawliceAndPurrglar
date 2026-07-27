# 알려진 문제와 공백

마지막 갱신: 2026-07-26

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

### ISSUE-016 경기 씬에서 클라이언트가 자기 역할을 읽지 못한다

- 종류: 버그
- 상태: RESOLVED
- 심각도: High
- 발견 날짜: 2026-07-27
- 발생 환경: 실제 Windows 빌드 2프로세스, `-netLobby` 프로브
- 확인 절차: `net-match-client-result.json`의 `localRole` 확인
- 예상: 로비에서 배정된 역할이 경기 씬에서도 유지되어 `Thief`로 읽힌다
- 실제: 로비에서는 양쪽이 `Police`/`Thief`로 정확히 합의하지만, 경기 씬으로
  넘어간 뒤 클라이언트의 `localRole`이 `Unassigned`가 된다. 호스트는 `Police`를
  유지한다
- 영향: 클라이언트의 `NetworkInputBridge`가 역할을 몰라 입력을 보내지 않으므로
  **클라이언트 조작이 동작하지 않는다.** 위치 복제 자체는 정상이라 두 화면의
  좌표는 일치한다
- 임시 해결: 없음. 서버 쪽은 정상이므로 호스트 조작은 동작한다
- 관련 작업: NET-003
- 해결 기록:

진단으로 원인을 확정했다. 프로브에 `boardExists`를 분리해 출력하니 클라이언트는
`boardExists: false`였다. 즉 미배정이 아니라 **오브젝트 자체가 사라진 것**이었다.
NGO 씬 관리가 켜진 상태에서 서버가 Single 모드로 씬을 로드하면 NGO는 자기 장부의
씬 소유 관계로 클라이언트 복제본을 정리한다. `DontDestroyOnLoad`는 Unity 씬만
바꾸고 그 장부를 바꾸지 않으며 `DestroyWithScene = false`는 서버 자신의
인스턴스만 보호한다.

해결: **역할을 지속 복제 상태가 아니라 1회 통보로 바꿨다.** 역할은 한 경기 동안
고정된 값이므로 계속 복제할 이유가 없다. 호스트가 경기 씬을 로드하기 직전, 아직
로비에 살아 있는 보드가 `CommitRolesRpc`로 전원에게 경찰 클라이언트 id를 보내고,
각 기계가 자기 id와 비교해 `LocalPlayerRoleSelector.OverrideRole`에 저장한다.
평범한 static이라 씬 전환을 그대로 넘어간다. 보드는 로비와 함께 사라져도 되므로
`DontDestroyOnLoad`와 `DestroyWithScene` 처리를 모두 제거했다.

검증: 클라이언트가 `boardExists: false`인 상태에서도 `localRole: Thief`를 읽고,
입력이 호스트를 거쳐 도둑을 실제로 이동시켰다.

프로브의 `boardExists`·`spawnedLinks` 분리 출력은 남겨뒀다. 다음에 비슷한 증상이
나오면 "없음"과 "미배정"을 바로 갈라낼 수 있다.

### ISSUE-015 사다리 상호작용이 아무 일도 하지 않았다

- 종류: 미구현
- 상태: RESOLVED
- 심각도: Medium
- 발견 날짜: 2026-07-27
- 발생 환경: `Game` 씬 `Prototype Ladder Point`
- 확인 절차: `Paws & Loot > Setup > Diagnose Interaction Targets`
- 예상: `E`를 누르면 지붕으로 올라간다
- 실제: 상호작용 자체는 **정상 연결돼 있었다.** 진단 결과 타입 `Traversal`,
  콜라이더 활성, 양쪽 역할 허용, 상호작용 범위 2m로 모두 정상이었고 `E`를
  누르면 `TryInteract`가 호출되어 `true`를 반환했다. 문제는 대상이
  `PrototypeInteractable`이라 `InteractionCount`만 1 증가시키고 끝난 것이다.
  올라가는 기능(`MAP-003`)이 아예 없었으므로 사용자에게는 고장으로 보였다
- 영향: 지붕 경로를 전혀 쓸 수 없었다
- 임시 해결: 없음
- 관련 작업: MAP-003, PLAYER-004
- 해결 기록: 2026-07-27 `LadderTraversal`을 구현해 상점 사다리 3개에 붙였다.
  같은 키로 높이에 따라 올라가거나 내려오고, 이동 중에는
  `CharacterController`를 꺼서 중력과 벽 판정이 이송과 싸우지 않게 했다.
  등반 중 대상이 파괴되면 사다리가 잠기지 않도록 복구 경로를 넣었다.
  양쪽 역할이 모두 쓸 수 있어 지붕이 완전한 안전지대가 되지 않는다.

진단 도구는 남겨뒀다. 앞으로 "상호작용이 안 된다"는 보고가 오면
콜라이더·범위·권한 중 무엇이 문제인지 먼저 수치로 확인한다.

### ISSUE-014 CHAR-001 실행에 필요한 Unity 6000.5.4f1 실행 파일 경로를 현재 환경에서 찾지 못했다

- 종류: 환경
- 상태: RESOLVED (오진)
- 심각도: Medium
- 발견 날짜: 2026-07-26
- 발생 환경: 로컬 개발 PC
- 확인 절차: `ProjectSettings/ProjectVersion.txt`의 요구 버전과 로컬 Unity 설치 경로 비교
- 예상: Unity `6000.5.4f1` 에디터로 CHAR-001 배치 실행과 빌드를 수행
- 실제: 최초 기록은 `2022.3.6f1`만 존재한다고 봤으나 **사실과 달랐다.**
  실제 설치 목록은 `6000.5.4f1` 하나뿐이며 `2022.3.6f1`은 존재하지 않는다
- 영향: 없음. 잘못된 환경 진단이었다
- 임시 해결: 불필요
- 관련 작업: CHAR-001
- 해결 기록: 2026-07-26 다음 경로가 실재하는 것을 확인했고, 같은 실행 파일로
  Edit Mode·Play Mode 테스트, 씬 재생성과 Windows 빌드를 모두 수행했다.

```text
C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe
```

배치 실행 명령은 `CLAUDE.md` 5절에 있다. Unity Hub의 설치 목록은
`ls "C:/Program Files/Unity/Hub/Editor/"`로 확인한다.

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

### ISSUE-013 추격 카메라 시점이 이동 방향에 따라 흔들렸다

- 종류: 버그
- 상태: RESOLVED
- 심각도: High
- 발견 날짜: 2026-07-25
- 발생 환경: `TopDownFollowCamera`, `PlayerMovementMotor`
- 확인 절차: 플레이테스트 빌드에서 방향을 바꿔 이동
- 예상: 시점이 고정되고 이동만 화면 기준으로 처리된다
- 실제: 카메라가 매 프레임 `LookAt(target)`으로 회전했다. 위치는
  `SmoothDamp`로 지연되므로 이동 중에는 카메라에서 플레이어를 향하는 벡터가
  계속 변하고, 그만큼 카메라 yaw가 흔들렸다. `PlayerMovementMotor`가 이 카메라
  Transform을 이동 기준축으로 사용하기 때문에 입력축까지 함께 돌아가
  `이동 → 카메라 회전 → 입력축 회전`의 피드백이 생겼다
- 영향: 방향을 바꿀 때마다 시점이 전환되는 것처럼 느껴져 추격 조작이 어려웠다
- 임시 해결: 없음
- 관련 작업: CAMERA-001, PLAYER-001
- 해결 기록: 2026-07-25 회전을 고정 오프셋에서 유도한 `FixedRotation` 하나로
  고정하고 `LookAt` 호출을 제거했다. 시점 방향은 지도 정남에서 북쪽을
  내려다보는 고정 시야로 확정했다. 목표를 6개 방향으로 움직여도 회전이
  0.01도 이내로 유지되는 Play Mode 테스트 2개를 추가했다

### ISSUE-012 플레이어와 보물의 시각 루트가 월드 원점에 남아 있었다

- 종류: 버그
- 상태: RESOLVED
- 심각도: High
- 발견 날짜: 2026-07-25
- 발생 환경: `Assets/_Project/Editor/GreyboxMapSetup.cs`
- 확인 절차: `VisualRoot`와 `PresentationRoot`의 월드 좌표 확인
- 예상: 시각 모델이 플레이어와 보물 콜라이더를 따라간다
- 실제: `CreateChild`가 `Transform.SetParent(parent)`를 world position 유지
  모드로 호출해, 새로 만든 오브젝트가 월드 원점에 그대로 남았다. 자식 위치를
  따로 지정하지 않는 `VisualRoot`와 `PresentationRoot`는 부모가 스폰 지점이나
  보석상 앞에 있어도 시각 모델을 맵 중앙(0,0,0)에 그렸다
- 영향: 임시 캡슐과 보물 큐브가 실제 위치가 아닌 맵 중앙에 표시됐다.
  콜라이더·이동·체포·판매 판정은 루트 좌표를 쓰므로 게임 규칙에는 영향이 없어
  자동 테스트로 드러나지 않았다
- 임시 해결: 없음
- 관련 작업: MAP-001, PLAYER-005, LOOT-002, ART-004
- 해결 기록: 2026-07-25 `VisualRoot`와 `PresentationRoot`의 `localPosition`과
  `localRotation`을 명시적으로 초기화했다. 모델 적용 후 두 캐릭터의 발이
  `y=-0.01m`로 지면에 닿는 것을 재빌드 로그로 확인했다

`CreateChild` 자체는 16개 호출 지점이 공유하며 대부분의 자식이 월드 좌표를
직접 지정하므로, 검증된 MAP-001 동선에 회귀를 주지 않기 위해 헬퍼 기본 동작은
바꾸지 않았다. 남은 그룹 노드는 위치를 쓰지 않아 현재 문제가 없지만, 새로
`CreateChild`로 시각 요소를 붙일 때는 지역 좌표를 반드시 초기화한다.

### ISSUE-011 현재 Game 씬에서 도둑이 판매로 승리할 수 없다

- 종류: 미구현
- 상태: OPEN
- 심각도: High
- 발견 날짜: 2026-07-25
- 발생 환경: `Assets/_Project/Scenes/Game.unity`, `MatchConfig.targetSaleAmount`
- 확인 절차: Game 씬의 `LootItem` 수와 `LootConfig` 가격, `TargetSaleAmount` 비교
- 예상: 도둑이 보물을 판매해 목표 금액에 도달하는 승리 경로가 존재
- 실제: Game 씬에 보물 1개(`common-trinket`, 200골드)와 판매처 1곳만 있고
  목표 금액은 1,000골드다. `SOLD`는 종료 상태라 재판매도 불가능하므로 도둑의
  누적 판매 금액은 최대 200골드에 머문다
- 영향: 관문 B의 `판매 또는 체포` 중 판매 승리 분기를 플레이테스트할 수 없다.
  경찰은 체포와 시간 종료 두 경로로 승리하지만 도둑은 승리 경로가 없다
- 임시 해결: 없음. 체포와 시간 종료 경로만 검증 가능
- 관련 작업: LOOT-006, MATCH-004, 관문 B
- 해결 기록:

미확정 결정이므로 코드만 바꾸지 않는다. 보물 배치 수를 늘릴지, 목표 금액
가설값을 낮출지 결정한 뒤 `docs/03_GAME_RULES.md`와
`docs/14_DECISION_LOG.md`를 함께 갱신한다.

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
