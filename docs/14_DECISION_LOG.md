# 결정 기록

## 기록 형식

### DEC-XXX 제목

- 날짜:
- 상태: 제안 / 확정 / 폐기
- 배경:
- 고려한 선택지:
- 결정:
- 이유:
- 장점:
- 단점:
- 영향:
- 재검토 조건:

## 채택된 결정

### DEC-027 호스트 권한과 직접 IP 접속을 채택한다

- 날짜: 2026-07-27
- 상태: 확정
- 배경: `DEC-P01`이 미결인 채로 단계 7을 시작할 수 없었다. 권한 주체와 접속
  방식을 정해야 이동·타이머·보물·체포 동기화를 설계할 수 있다.
- 고려한 선택지: 호스트 권한 + 직접 IP, 호스트 권한 + Relay, 전용 서버
- 결정: 호스트가 서버 권한을 갖고, 접속은 직접 IP로 한다. Relay와 전용 서버는
  보류한다. NGO `2.13.0`과 Unity Transport `6.5.0`을 본게임에 채택한다.
- 이유: `NET-001`과 `NET-002`에서 이미 이 조합으로 접속과 역할 배정을 검증했다.
  계정·비용·운영이 필요 없고 공모전 시연 환경에서 가장 확실하다.
- 장점: 추가 비용 0, 검증된 경로, 권한 주체가 하나로 단순함
- 단점: 같은 네트워크가 아니면 접속할 수 없고, 호스트가 나가면 세션이 끝난다.
  호스트에 유리한 지연 차이가 생길 수 있다.
- 영향: Integration, Match, Loot, Arrest, Lobby UI, Build
- 재검토 조건: 원격 접속이 필요해지거나 호스트 유리가 플레이테스트에서
  확인될 때. 그때 Relay를 먼저 검토한다.

이 결정으로 `DEC-013`의 "네트워크 패키지는 기술 검증 후 채택한다"가 해소됐고
`DEC-P01`은 이 결정으로 대체된다.

### DEC-028 승패는 복제하지 않고 판정의 입력만 복제한다

- 날짜: 2026-07-27
- 상태: 확정
- 배경: `NET-006`과 `NET-007`은 "두 화면이 같은 승리 판정"을 요구한다. 가장
  직접적인 방법은 호스트가 정한 결과를 그대로 복제해 클라이언트에 심는 것이다.
- 고려한 선택지: (1) 결과를 복제해 클라이언트 판정을 덮어쓴다, (2) 판정의
  입력(지갑 총액, 체포 완료, 남은 시간)만 복제하고 판정은 양쪽이 각자 한다
- 결정: (2)를 택한다. `NetworkPlayerLink`가 `ThiefLootWallet.SoldAmount`와
  `ArrestProgressController`의 진행도·완료·중단을 복제하고, 승패는 여전히
  각 기계의 `MatchResultArbiter`가 결정한다.
- 이유: `AGENTS.md`가 승패를 `MatchResultArbiter`/`MatchResultEvaluator`만
  결정하도록 못박았다. 네트워크가 결과를 주입하는 경로를 만들면 규칙 계층
  밖에서 승패가 바뀔 수 있는 두 번째 경로가 생긴다. 입력을 복제하면 양쪽
  판정기가 같은 값을 보므로 결론이 구조적으로 같아진다.
- 장점: 규칙 계층의 단일 판정 원칙이 유지된다. 네트워크를 끄면 아무것도
  달라지지 않는다. 중복 판매·중복 체포 방지가 이미 있는 곳에 그대로 남는다.
- 단점: 결론이 같다는 것이 "명령받았기 때문"이 아니라 "입력이 같기 때문"이다.
  복제되지 않은 입력이 판정에 새로 쓰이면 갈라질 수 있다.
- 영향: Match, Loot, Arrest, Integration
- 재검토 조건: 판정 입력이 늘어나 복제 누락을 추적하기 어려워질 때. 그때는
  판정 결과 자체를 복제하는 대신 판정을 서버 전용 컴포넌트로 옮긴다.

실측 근거: `-netScenario full`에서 양쪽이 `Police / ThiefArrested`로 일치했다
(`13_CURRENT_STATE.md`).

### DEC-029 재경기 요청은 RPC가 아니라 NGO 명명 메시지로 보낸다

- 날짜: 2026-07-27
- 상태: 확정
- 배경: 재경기 버튼은 `Result` 씬에서 눌리고 복귀 대상은 `Game` 씬이다.
  씬에 배치된 `NetworkObject`는 씬 전환 때 사라지므로(`ISSUE-016`) 그 위의
  RPC로는 이 요청을 보낼 수 없다.
- 고려한 선택지: (1) 씬 간 유지되는 `NetworkObject`를 하나 만든다,
  (2) `CustomMessagingManager`의 명명 메시지를 쓴다
- 결정: (2). `NetworkRematchCoordinator`가 `NetworkManager` 오브젝트에 붙어
  `"PawliceAndPurrglar.Rematch"` 메시지를 등록하고, 클라이언트의 요청을 받은 호스트만
  씬을 로드한다.
- 이유: `ISSUE-016`에서 씬 간 `NetworkObject` 유지를 시도했다가 NGO의 씬
  관리와 충돌해 클라이언트에서 오브젝트가 사라졌다. 명명 메시지는
  `NetworkManager`에 속하므로 세션이 살아 있는 동안 모든 씬에서 동작한다.
- 장점: 유지해야 할 오브젝트가 없다. 오프라인에서는 핸들러가 없어 기존 로컬
  로드가 그대로 동작한다.
- 단점: 타입 안전한 RPC가 아니라 문자열 키와 직접 직렬화다.
- 영향: Integration, UI, Core
- 재검토 조건: 씬 간 요청이 여러 종류로 늘어날 때. 그때는 세션 전용 명령
  채널로 묶는다.

실측 근거: `-netScenario rematch`에서 클라이언트만 눌렀고 호스트의
`hostReceivedRequests`가 1, 양쪽 `activeScene`이 `Game`이었다.

### DEC-026 경기 결과는 임시 씬 세션으로 전달한다

- 날짜: 2026-07-25
- 상태: 확정
- 배경: Game 씬의 확정 결과를 Result 씬에서 표시하되 UI가 승패를 다시 계산하면 안 된다.
- 고려한 선택지: 전역 정적 세션, DontDestroyOnLoad 오브젝트, PlayerPrefs 영구 저장
- 결정: 첫 `MatchResult`만 보관하는 `MatchResultSession`을 씬 전환 경계에서 사용하고 새 Game 씬 시작 시 초기화한다.
- 이유: 한 경기 결과는 영구 저장 대상이 아니며 현재 로컬 MVP에는 네트워크 영속 객체가 필요하지 않다.
- 장점: 결과 규칙과 UI 분리, 간단한 씬 간 전달, 테스트 가능한 명시적 초기화
- 단점: 프로세스 재시작과 도메인 재로드를 넘어 결과를 유지하지 않는다.
- 영향: Match, Result UI, Scene Flow, Rematch, Tests
- 재검토 조건: 네트워크 경기 세션 또는 경기 기록 저장 기능을 도입할 때

### DEC-025 동시 승패 판정은 체포, 목표 판매, 시간 종료 순으로 처리한다

- 날짜: 2026-07-25
- 상태: 확정
- 배경: 체포 완료, 목표 금액 판매와 타이머 만료가 같은 프레임에 발생할 수 있다.
- 고려한 선택지: 이벤트 수신 순서, 판매 우선, 체포 우선의 고정 평가
- 결정: 요청을 한 평가 시점에 모으고 `체포 > 목표 판매 > 시간 종료` 순으로 한 번만 결과를 결정한다.
- 이유: 체포 완료의 즉시성을 유지하면서 판매와 시간 종료가 겹칠 때는 실제 누적 금액으로 일관되게 판정할 수 있다.
- 장점: 컴포넌트 실행 순서와 UI에 의존하지 않는 결정적 결과
- 단점: 체포와 목표 판매가 완전히 동시에 일어나면 경찰에 유리하다.
- 영향: Match, Arrest, Loot, Timer, Tests
- 재검토 조건: 네트워크 권한과 서버 타임스탬프 정책을 확정하거나 플레이테스트에서 동시 판정 불공정이 확인될 때

### DEC-001 제한 명령형 동물 AI를 사용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 실시간 대전에서 자유 대화 AI의 지연과 예측 불가능성을 줄여야 한다.
- 고려한 선택지: 자유 대화 AI, 제한 명령 AI, 완전 스크립트 연출
- 결정: 제한된 `CompanionCommandId`와 상태 기반 동물 AI를 사용한다.
- 이유: 명령 결과가 빠르고 테스트 가능해야 한다.
- 장점: 안정성, 밸런스, 실패 복구
- 단점: 자유로운 대화 표현이 제한됨
- 영향: Companions, Input, Voice, UI
- 재검토 조건: 핵심 명령이 완성되고 자유 대화가 게임 재미에 명확히 기여한다는 근거가 생길 때

### DEC-002 숫자키 명령을 먼저 구현한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 음성 입력 전에 명령 자체의 재미와 상태 흐름을 검증해야 한다.
- 고려한 선택지: 음성 우선, 키보드 우선, 동시 구현
- 결정: `1`~`4` 입력으로 명령을 완성한 뒤 같은 명령 ID에 음성을 연결한다.
- 이유: AI와 STT 문제를 분리해 테스트할 수 있다.
- 장점: 빠른 프로토타입, 안정적인 시연, 접근성
- 단점: 초기 빌드에서 음성 차별점이 보이지 않음
- 영향: Input, Companions, Voice, UI, Pipeline
- 재검토 조건: 핵심 명령과 게임 루프가 플레이테스트를 통과할 때

### DEC-003 1대1, 4분 경기를 사용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 비대칭 역할과 동물 명령을 짧은 세션에서 검증한다.
- 고려한 선택지: 1대1, 2대2, 싱글플레이
- 결정: 경찰 1명 대 도둑 1명, 경기 시간 240초
- 이유: 역할과 정보전이 가장 단순하게 읽힌다.
- 장점: 설명과 반복 플레이가 쉬움
- 단점: 한 역할의 밸런스 문제가 크게 드러남
- 영향: Match, Gameplay, Networking, UI
- 재검토 조건: 반복 플레이에서 4분이 지나치게 길거나 짧다고 확인될 때

### DEC-004 3D 기울어진 탑다운을 핵심 시점으로 사용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 1인칭 타격감과 전체 추격 상황 가시성 사이에서 선택이 필요했다.
- 고려한 선택지: 1인칭, 3인칭 추적, 정적 아이소메트릭, 동적 기울어진 탑다운
- 결정: 원근 투영 3D 기울어진 탑다운
- 이유: 두 플레이어와 동물, 투척물, 경로를 한 화면에서 보여줄 수 있다.
- 장점: 귀여운 애니메이션과 비대칭 정보 표현
- 단점: 직접 타격감은 별도 카메라와 피드백으로 강화해야 함
- 영향: Camera, Map, Art, UI, Animation
- 재검토 조건: 그레이박스에서 캐릭터와 추격이 너무 작게 느껴질 때

### DEC-005 캐릭터의 멍청함은 애니메이션으로 표현한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 귀엽고 허술한 성격을 조작감 손상 없이 표현해야 한다.
- 고려한 선택지: 입력 지연, 불규칙 이동, 과장 애니메이션
- 결정: 입력은 즉각적으로 유지하고 팔 벌린 달리기, 후속 흔들림, 미끄러짐을 사용한다.
- 이유: 재미와 조작 정확성을 동시에 유지한다.
- 장점: 캐릭터 정체성과 시각적 코미디
- 단점: 애니메이션 제작과 튜닝 비용
- 영향: Animation, Movement, Camera, Audio
- 재검토 조건: 애니메이션이 상태 판독이나 충돌 판정을 방해할 때

### DEC-006 대부분의 모델은 프로토타입 이후 적용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 초기 모델링과 자동 생성 프로토타입의 품질이 핵심 재미 검증보다 앞섰다.
- 고려한 선택지: 최종 모델 우선, 그레이박스 우선, 병렬 제작
- 결정: 그레이박스 핵심 루프 완료 후 Blender 최종 모델을 교체한다.
- 이유: 규칙과 맵이 바뀌어도 아트 재작업을 줄인다.
- 장점: 일정과 범위 관리
- 단점: 초기 발표 화면의 완성도가 낮음
- 영향: Art, Pipeline, Prefabs, Schedule
- 재검토 조건: 외부 발표 일정상 최소 대표 모델이 추가로 필요할 때

### DEC-007 경찰 모델만 리깅 스파이크에 사용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 최종 모델 제작 전에 Blender와 Unity 리깅 파이프라인을 검증해야 한다.
- 고려한 선택지: 모든 캐릭터 리깅, 새 테스트 모델, 기존 경찰 모델
- 결정: 기존 경찰 모델 한 체로 `Idle`, `Run`, `ComedyRun`을 검증한다.
- 이유: 현재 보유 자산으로 가장 작은 기술 검증이 가능하다.
- 장점: 축, 스케일, Avatar 문제를 조기에 발견
- 단점: 모델 자체는 최종 품질이 아님
- 영향: ArtSource, Art, Animation, Prefabs
- 재검토 조건: 기존 모델 구조가 리깅에 부적합하다고 확인될 때

### DEC-008 판매 NPC는 너구리 상인이다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 초기 까마귀 상인 설정을 첨부 콘셉트 이미지와 세계관에 더 잘 맞는 동물로 재검토했다.
- 고려한 선택지: 고정 암시장, 너구리 상인, 까마귀 상인
- 결정: 도둑은 이동형 너구리 상인에게 보물을 판매한다.
- 이유: 쓰레기통과 골목을 오가는 너구리의 이미지가 코믹한 밀거래 연출과 잘 맞는다.
- 장점: 기억하기 쉬운 중립 NPC
- 단점: 출현 규칙과 판매 가시성을 새로 설계해야 함
- 영향: GDD, Map, Gameplay, UI, Art, Audio
- 재검토 조건: 플레이테스트에서 판매 지점을 찾기 어렵거나 역할이 혼란스러울 때

### DEC-011 Unity 개발 환경을 고정한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 팀원과 자동화 환경에서 같은 Unity와 패키지 조합을 사용해야 한다.
- 고려한 선택지: 현재 환경 유지, Unity LTS로 재생성, 패키지 최신화
- 결정: Unity `6000.5.4f1`, URP `17.5.0`, Input System `1.19.0`, Test Framework `1.7.0`, AI Navigation `2.0.13`, Cinemachine `3.1.7`을 고정한다.
- 이유: 현재 프로젝트와 TopDown Engine이 이 조합에서 컴파일되며, 불필요한 재마이그레이션을 피할 수 있다.
- 장점: 재현 가능한 개발 환경과 작은 패키지 표면
- 단점: Unity 또는 외부 에셋 업데이트는 별도 검증이 필요함
- 영향: ProjectSettings, Packages, Build, Input, Navigation, Tests
- 재검토 조건: 치명적 엔진 버그, 목표 플랫폼 빌드 실패 또는 패키지 보안 문제가 확인될 때

### DEC-012 Windows x86_64를 첫 빌드 대상으로 한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 마이크, 네트워크와 3D 렌더링을 먼저 안정적으로 검증할 플랫폼이 필요하다.
- 고려한 선택지: Windows x86_64, WebGL, Android
- 결정: MVP의 첫 실행 빌드는 Windows x86_64로 제작한다.
- 이유: 현재 개발 환경에서 빌드와 입력 검증이 가장 단순하다.
- 장점: 마이크와 멀티플레이 기술 검증이 수월함
- 단점: 링크만으로 실행하는 WebGL 접근성은 초기 단계에서 제공하지 못함
- 영향: Build, Voice, Network, Submission
- 재검토 조건: 공모전 제출 규정이 WebGL을 요구하거나 Windows 배포가 제한될 때

### DEC-013 네트워크 패키지는 기술 검증 후 채택한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 네트워크 구현을 핵심 규칙에 결합하기 전에 접속과 권한 모델을 검증해야 한다.
- 고려한 선택지: Netcode for GameObjects, Photon Fusion, Mirror
- 결정: Netcode for GameObjects와 Unity Multiplayer Services를 우선 후보로 두되 아직 설치하지 않는다.
- 이유: 패키지 도입 전에 Host, Client, 역할 배정, 위치 동기화와 종료 처리를 작은 스파이크로 확인한다.
- 장점: 불필요한 의존성과 구조 재작업을 줄임
- 단점: 본격 멀티플레이 일정이 기술 검증 이후로 밀림
- 영향: Network, Integration, Match, Build
- 재검토 조건: 기술 검증 실패, 비용 또는 운영 조건이 공모전 범위와 맞지 않을 때

### DEC-014 MVP 기본 씬을 Bootstrap, Game, Result로 나눈다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 실행 시작, 경기와 결과 화면의 책임을 최소 단위로 분리해야 한다.
- 고려한 선택지: 단일 Main 씬, Bootstrap과 Game, Bootstrap과 Game과 Result
- 결정: `Bootstrap`, `Game`, `Result` 세 씬을 사용하고 `Bootstrap`을 빌드 첫 씬으로 둔다.
- 이유: 현재 MVP에 필요한 실행 흐름만 만들면서 경기 재시작과 결과 화면을 분리할 수 있다.
- 장점: 책임 분리, 빌드 시작점 고정, 이후 씬 확장 용이
- 단점: 씬 간 상태 전달 구조는 별도로 설계해야 함
- 영향: Scenes, Core, UI, Build
- 재검토 조건: 로비 또는 별도 메인 메뉴가 MVP에 반드시 필요해질 때

### DEC-015 핵심 게임 수치를 ScriptableObject 설정으로 관리한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 경기와 이동, 보물, 체포, 동물 명령 수치가 기능 코드와 씬에 흩어지는 것을 막아야 한다.
- 고려한 선택지: 코드 상수, 씬 컴포넌트 직렬화, 기능별 ScriptableObject와 설정 묶음
- 결정: 기능별 여섯 ScriptableObject를 `DefaultGameConfigSet`으로 묶고 Bootstrap에서 시작 시 검증한다.
- 이유: Inspector에서 값을 확인하면서도 런타임 시스템은 동일한 검증된 설정을 조회할 수 있다.
- 장점: 밸런스 조정 위치가 명확하고 누락과 잘못된 범위를 빠르게 발견한다.
- 단점: 새로운 필수 설정을 추가할 때 설정 묶음과 기본 에셋 생성기를 함께 갱신해야 한다.
- 영향: Config, Bootstrap, Match, Player, Loot, Arrest, Companion, Voice
- 재검토 조건: 원격 설정 또는 서버 권한 설정이 필요해져 로컬 ScriptableObject만으로 부족할 때

### DEC-016 분류형 로그와 빌드별 최소 레벨을 사용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 경기 시스템이 늘어나기 전에 상태 전환과 오류의 소유 영역을 일관되게 추적해야 한다.
- 고려한 선택지: 직접 `Debug.Log` 호출, 외부 로깅 패키지, 작은 프로젝트 전용 로거
- 결정: 7개 게임 분류와 4개 레벨을 가진 `GameLogger`를 사용하고 빌드 종류별 최소 레벨은 ScriptableObject로 관리한다.
- 이유: 현재 MVP에 필요한 검색성과 제출 빌드 노이즈 제어를 외부 의존성 없이 제공한다.
- 장점: 로그 형식 통일, 분류 검색, 중복 억제, 예외 스택 보존
- 단점: 파일 로그와 원격 수집은 아직 제공하지 않는다.
- 영향: Bootstrap, Match, Player, Loot, Arrest, Companion, Voice, Network
- 재검토 조건: 원격 진단, 파일 보존 또는 구조화된 분석 파이프라인이 실제로 필요할 때

### DEC-017 TECH-001은 Windows x86_64 빌드로 통과한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 첫 목표 플랫폼에서 씬, 렌더링, 해상도와 키보드 입력이 실제 빌드에서도 동작하는지 확인해야 한다.
- 고려한 선택지: Windows x86_64, WebGL, 두 플랫폼 동시 검증
- 결정: Windows x86_64 개발 빌드에서 TECH-001을 먼저 통과하고 WebGL 검증은 보조 플랫폼 재검토 시 수행한다.
- 이유: 현재 첫 빌드 대상과 향후 마이크·네트워크 검증 환경이 Windows로 정해져 있다.
- 장점: 실제 목표 환경의 기술 위험을 먼저 제거하고 WebGL 전용 작업을 미룰 수 있다.
- 단점: 로컬 서버와 GitHub Pages 배포 가능 여부는 아직 검증되지 않았다.
- 영향: Build, Input, Scenes, Submission
- 재검토 조건: 공모전이 WebGL을 요구하거나 링크 실행 배포가 필요해질 때

### DEC-018 최종 캐릭터 전에 별도 Generic 테스트 리그로 반입 규격을 고정한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 기존 경찰 모델을 바로 수정하면 모델 구조 문제와 Blender-Unity 파이프라인 문제를 구분하기 어렵다.
- 고려한 선택지: 기존 경찰 즉시 리깅, Unity 도형만 사용, 별도 최소 Blender 테스트 리그
- 결정: 2m급 단순 더미, 5본, 단일 재질, `Idle`·`Walk`를 가진 Generic 리그로 반입과 교체 구조를 먼저 검증한다.
- 이유: 축, 단위, 스킨, 애니메이션 take 이름과 프리팹 구조를 최종 모델 작업 전에 독립적으로 확인할 수 있다.
- 장점: 기존 경찰 원본을 보존하고 모델 교체 시 이동과 충돌 판정을 유지한다.
- 단점: Humanoid Avatar와 최종 경찰의 복잡한 웨이트는 별도 RIG 작업에서 다시 검증해야 한다.
- 영향: ArtSource, Art, Animation, Prefabs, TechnicalValidation
- 재검토 조건: 기존 경찰 모델 리깅을 시작하거나 Humanoid 리타게팅이 필요할 때

### DEC-019 숫자키 우선 원칙을 유지하면서 Windows STT 기술 검증은 지금 분리 실행한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 숫자키는 명령 로직을 먼저 만들기 위한 대체 입력이지만 마이크 권한, 한국어 인식과 실패 복구 위험은 확인하지 못한다.
- 고려한 선택지: 핵심 루프 이후까지 STT 전체 보류, 지금 AI 음성 명령까지 구현, Windows 받아쓰기만 격리 검증
- 결정: `TECH-003` 전용 장면에서 마이크 입력을 텍스트로 표시하고 게임 명령과 AI 대화에는 연결하지 않는다.
- 이유: 공모전의 핵심 소개인 음성 명령이 기술적으로 불가능한 상황을 본 개발 전에 발견해야 한다.
- 장점: 명령 로직 일정에 영향을 주지 않으면서 플랫폼 위험과 대체 입력을 확인한다.
- 단점: Windows Legacy API 결과만으로 최종 STT 서비스와 한국어 품질을 확정할 수 없다.
- 영향: TechnicalValidation, Voice 문서, Windows 빌드
- 재검토 조건: 핵심 명령이 완성되어 실제 STT 서비스, 비용과 한국어 정확도를 선택할 때

### DEC-020 NET-001 기술 검증에 NGO 2.13.0을 사용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: Unity `6000.5.4f1`에서 두 Windows 클라이언트 접속과 소유권 기반 위치 동기화를 검증해야 한다.
- 고려한 선택지: NGO `2.7.0`, NGO `2.13.0`, Photon Fusion, Mirror
- 결정: NET-001과 NET-002 격리 검증에는 NGO `2.13.0`과 Unity Transport `6.5.0`을 사용한다.
- 이유: `2.7.0`은 현재 Transport API의 `EntityId` 변경과 컴파일되지 않았고 `2.13.0`은 실제 빌드와 두 프로세스 검증을 통과했다.
- 장점: Unity 6 환경과 맞는 공식 패키지, NetworkVariable과 소유권 검증 가능
- 단점: Lobby, Relay, 비용, 운영 방식과 본게임 권한 구조는 아직 확정되지 않음
- 영향: Packages, TechnicalValidation, Network, Build
- 재검토 조건: NET-002 권한 검증 실패, 배포 환경에서 직접 IP를 사용할 수 없거나 운영 서비스 요구가 확정될 때

### DEC-021 NET-002 역할은 서버 권한으로 배정한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 두 클라이언트가 같은 역할을 선택하거나 서로 다른 역할 상태를 보는 문제를 기술 검증해야 한다.
- 고려한 선택지: 각 Client의 로컬 결정, Host 고정 역할, 서버 권한 역할 변수
- 결정: 기술 검증에서 Host를 경찰, 첫 원격 Client를 도둑으로 배정하고 서버만 역할 값을 변경한다.
- 이유: 중복 역할을 구조적으로 막고 양쪽 클라이언트가 같은 역할 상태를 관찰할 수 있다.
- 장점: 경찰 1명·도둑 1명 보장, 역할별 시작 위치와 권한 검사 기반 제공
- 단점: 역할 선택과 교환 UI가 없고 Host가 항상 경찰임
- 영향: TechnicalValidation, Network, Player Role
- 재검토 조건: 실제 로비와 역할 선택 흐름을 구현할 때

### DEC-022 단축키 핵심 프로토타입은 음성 기술 관문과 분리해 진행한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: TECH-003은 현재 PC의 Windows 받아쓰기 미지원으로 차단됐지만 단계 A의 검증 질문은 숫자키 동물 명령과 추격의 재미다.
- 고려한 선택지: STT 해결 전 전체 중단, 음성 기능을 임시 제거, 단계 A와 제출 MVP 관문 분리
- 결정: 실제 STT는 제출 MVP의 차단 요소로 유지하고 그레이박스·경기 규칙·숫자키 명령 개발은 단계 A로 진행한다.
- 이유: 음성 플랫폼 문제와 핵심 게임 재미 검증을 분리하면서 음성 차별점을 완료로 오인하지 않기 위해서다.
- 장점: 핵심 루프 개발을 진행하면서 TECH-003 위험을 문서상 유지
- 단점: 초기 프로토타입은 최종 한 줄 소개의 음성 경험을 제공하지 못함
- 영향: Pipeline, Map, Match, Companions, Voice, Submission
- 재검토 조건: 단계 A 핵심 루프가 통과하거나 외부 STT 후보를 선정할 때

### DEC-023 MAP-001은 중앙 교차로와 외곽 순환로를 함께 사용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 탑다운 추격에서 전체 상황은 읽히되 한 경로만 반복되지 않는 회색 상자 동선이 필요하다.
- 고려한 선택지: 단일 중앙 교차로, 외곽 순환로만 사용, 중앙 교차로와 외곽 순환로 결합
- 결정: 56×44m 안에 중앙 동서·남북 축과 북·남 외곽 순환로를 두고 주요 장소 쌍마다 경로 2개 이상을 명시한다.
- 이유: 짧은 추격과 긴 우회가 함께 생기며 경찰과 도둑 양쪽에 예측과 역선택 여지를 주기 위해서다.
- 장점: 막다른 길을 줄이고 횡단 시간과 경로 폭을 자동 검증할 수 있음
- 단점: 현재 건물과 지붕은 회색 상자이며 최종 가시성 검증이 남음
- 영향: Map, Camera, Player, Companions
- 재검토 조건: 실제 플레이어 이동과 카메라 적용 후 동선이 지나치게 단순하거나 횡단 시간이 목표와 맞지 않을 때

### DEC-024 입력과 공통 이동 물리를 분리한다

- 날짜: 2026-07-25
- 상태: 확정
- 배경: 경찰과 도둑이 같은 이동 규칙을 쓰되 키보드·네트워크 입력과 물리 구현이 서로 얽히지 않아야 한다.
- 고려한 선택지: 역할별 이동 코드, 입력과 이동이 합쳐진 단일 컴포넌트, 공통 모터와 입력 어댑터 분리
- 결정: `PlayerKeyboardInput`은 입력만 읽고 두 역할 모두 `PlayerMovementMotor`와 CharacterController를 사용한다.
- 이유: PLAYER-002에서 코드 복사 없이 역할을 추가하고 향후 네트워크 입력도 같은 모터로 전달하기 위해서다.
- 장점: 역할별 중복 제거, 물리 테스트 용이, 입력 소스 교체 가능
- 단점: 씬 조립 시 입력 어댑터와 모터 참조를 모두 연결해야 함
- 영향: Player, Input, Network, Tests
- 재검토 조건: 네트워크 권한 구조가 공통 모터 계약으로 표현되지 않을 때

### DEC-009 Blender 원본과 Unity 반입물을 분리한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: `.blend` 직접 참조는 환경 의존성과 임포트 불안정을 만든다.
- 고려한 선택지: Assets 안에 Blend 보관, 별도 ArtSource, 외부 저장소
- 결정: 원본은 `ArtSource/Blender/`, FBX는 `Assets/_Project/Art/`에 둔다.
- 이유: 제작 파일과 런타임 자산의 책임을 구분한다.
- 장점: 안정적인 임포트와 변경 추적
- 단점: 내보내기 절차가 추가됨
- 영향: Repository, Art, Pipeline
- 재검토 조건: 팀 아트 파이프라인이 별도 DCC 저장소를 사용할 때

### DEC-010 현재 Unity 프로젝트 폴더를 저장소 루트로 사용한다

- 날짜: 2026-07-24
- 상태: 확정
- 배경: 별도 폴더로 옮긴 뒤 버전 관리를 시작할지, 현재 프로젝트에서 바로 시작할지 결정해야 했다.
- 고려한 선택지: 새 상위 폴더로 이동, 현재 폴더에서 로컬 Git 시작
- 결정: `C:\Users\SSAFY\CatCops`를 저장소 루트로 사용하고 나중에 Git 원격 저장소를 연결한다.
- 이유: Unity 프로젝트 경계를 유지하면서 현재 변경부터 이력을 남길 수 있다.
- 장점: 이동 작업이 없고 Unity 상대 경로와 `.meta`를 그대로 유지한다.
- 단점: 외부 유료 에셋과 대용량 바이너리의 원격 공유 정책을 별도로 정해야 한다.
- 영향: Repository, ArtSource, Assets, Builds, Submission
- 재검토 조건: 저장소 분리 또는 별도 아트 저장소가 필요해질 때

## 보류된 결정

### DEC-P01 네트워크 서비스와 최종 권한 구조

- 상태: 제안 전
- 필요한 근거: NET-002 결과, Relay·Lobby 필요 여부, 서버 운영 범위, 공모전 시연 환경

### DEC-P02 실제 음성 인식 기술

- 상태: 제안 전
- 필요한 근거: 목표 플랫폼, 한국어 정확도, 지연, 비용, 오프라인 여부, 개인정보 처리

### DEC-P03 목표 금액과 세부 밸런스

- 상태: 프로토타입 가설
- 필요한 근거: 최소 5회 이상 플레이테스트 데이터
# DEC-VOICE-001: WebGL voice service and Host authority

- Status: Accepted
- Date: 2026-07-29
- WebGL browser capture uses `MediaRecorder` for at most five seconds.
- A same-repository Node.js TypeScript Fastify service handles STT and bounded
  Intent candidates. OpenAI credentials remain server-only environment values.
- The NGO Host remains the final authority for Pet Cognition, random results,
  target validation, and action execution. This supplements the existing
  network authority decision rather than introducing a second game server.
- Session capability tokens are used for voice API access. MVP storage is
  in-memory and single-replica; account authentication and Redis are out of
  scope.
- This decision supersedes the old voice placeholder that only considered
  Windows DictationRecognizer. The Windows technical probe remains isolated.

### DEC-VOICE-002: Local WebGL one-player launch path

- Status: Accepted
- Date: 2026-07-30
- Decision: Use `play-webgl.bat` to build and serve the existing Bootstrap,
  Game, and Result scenes through a local Node static server on port `8080`.
- Decision: Start the existing Fastify voice service on port `3000` as an
  optional companion process; no Unity or npm dependency is added for static
  file serving.
- Decision: Offline one-player WebGL sessions receive a temporary Host
  capability from `/api/game/sessions`. The game remains offline and uses the
  same local authoritative simulation; network matches keep NGO Host authority.
- Constraint: OpenAI credentials remain in `server/.env` and keyboard commands
  must remain usable when voice setup or external APIs fail.

### DEC-VOICE-003: Windows local voice runtime

- Status: Accepted
- Date: 2026-08-01
- Windows Standalone uses Unity `Microphone`, 16 kHz mono PCM16 WAV, and a
  bundled loopback FastAPI Gateway at `127.0.0.1`. The Gateway uses local
  faster-whisper and Ollama Qwen3; no cloud API or key is used.
- The existing WebGL/Fastify path remains available and is not replaced.
- Unity validates the structured response and sends only a bounded
  `CompanionCommandRequest` through `CompanionCommandDispatcher`.
- The existing command enum remains authoritative. `TRACK_SCENT` and `CHASE`
  map to `Track`, `STAY` maps to `Stay`, and unsupported `BITE` resolves to
  `NONE` because this branch has no dog bite executor.
- Gateway and Ollama processes are stopped only when Unity started them.
- Models and runtime binaries are installation artifacts, not Git-tracked
  source files.

2026-08-02 addendum:

- Deterministic command fallback runs before Ollama for core dog and cat
  commands. This keeps common voice commands responsive even when the local LLM
  is cold, unavailable, or slow.
- A LocalAI gateway with ready STT but unavailable Ollama is treated as
  degraded but usable by Unity, because fallback commands can still complete
  without an LLM round trip.
- Cat command slot 3 is shown and interpreted as `ROOF` for the current
  playtest. The runtime still reuses the existing third cat command ID to avoid
  broad enum churn, but keyboard, voice context, and HUD labels present it as a
  rooftop-climb command rather than a loot-steal command.
## DEC-032. 경찰 승리는 체포 3회, 도둑은 판매 1,000골드

- Context: 체포 1회가 즉시 승리였다. 4분짜리 경기가 30초 만에 끝날 수 있었고,
  도둑은 한 번의 실수를 복구할 방법이 없었다.
- Decision: 경찰은 체포 3회, 도둑은 누적 판매 1,000골드. 시간 만료는 경찰 승리.
  체포 1회는 경찰서 11초 구금 후 도둑 스폰에 재배치.
- Alternative rejected: 모든 것을 골드로 환산해 제한시간 후 점수를 비교하는 안.
  경찰이 도둑의 돈 절반을 가져가는 구조에서는 **도둑이 가장 부유할 때 잡는 것이
  최적**이 되어 초반에 쫓지 않는 것이 이득이 된다. 추격 게임에서 "쫓지 않는 것이
  최적"은 게임을 망가뜨린다. 또한 `PoliceWallet`이 장비 구매용이라 점수와 겸하면
  손전등을 사는 것이 자기 점수를 깎는 행위가 된다.
- Constraint: 구금 시간은 아무도 플레이하지 않는 시간이므로 4~20초로 제한하고
  기본 11초. 3회 x 20초면 4분의 1분을 구경에 쓰게 된다.
- Consequence: 체포 중복 제거의 책임이 `MatchResultArbiter`에서
  `ArrestCompletionController`로 옮겨갔다. 판정기는 이제 호출마다 세야 하므로,
  한 번의 체포가 두 번 세어지는 것은 래치를 가진 호출자가 막는다. 그 래치는
  타이머가 아니라 **석방 시점**에 풀린다 — 석방이 다시 잡힐 수 있게 되는 시점이다.
- Still open: 클라이언트 HUD의 체포 횟수와 구금 카운트다운은 복제되지 않는다.
  판정 결과는 호스트가 정해 복제하므로 승패에는 영향이 없다.

## DEC-033. 보물 가격을 1/5로 내린다. 목표 금액은 건드리지 않는다

- Context: 판매가가 일반 200 / 고급 350 / 희귀 500이고 목표가 1,000골드였다.
  즉 **희귀 보물 두 개가 곧 승리**다. 도둑은 획득 키를 두 번 누르고 상인까지 한 번
  걸어가면 이겼고, 경찰이 요구받는 체포 3회(사이에 감옥 11초씩)는 일어날 시간
  자체가 없었다. 방 단위로 보면 더 나쁘다 — 가장 싼 집이 1,250골드라 **어느 방
  하나만 털어도 경기가 끝났다.**
- Decision: 등급 간 비율 1 : 1.75 : 2.5를 유지한 채 세 값을 일괄 1/5로 내린다
  (40 / 70 / 100). 실내 귀중품도 함께 20 → 5.
- Alternative rejected: **목표 금액을 5,000으로 올리는 안.** 비율은 똑같이 나오고
  숫자도 더 두툼하다. 채택하지 않은 이유는 목표 1,000이 가격보다 **훨씬 많은 것에
  묶여 있어서**다 — 2프로세스 회귀(`-netScenario full`), HUD 문자열
  (`GOLD 200 / 1000`), 몰수 비율의 기준. 가격은 설정 에셋 한 곳이고, 목표는 그
  전부를 함께 움직인다.
- Alternative rejected: 네 번째 등급을 만들어 최고가만 낮추는 안. `03_GAME_RULES`
  8.6이 이미 거절해 둔 것이고, 문제는 최고가 하나가 아니라 **세 값과 목표의 비율
  전체**였다.
- Constraint: 경찰 경제는 손대지 않는다. 몰수는 누적 판매액의 비율(도둑 50% /
  경찰 20%)이고 목표가 1,000으로 고정이므로, 같은 진행률에서 경찰이 받는 금액은
  가격과 무관하게 같다. 끈끈이 60·센서등 90·시작 120골드가 그대로인 이유다.
- Constraint: 실내 귀중품이 함께 내려가야 하는 이유는 총액이 아니라 **성질**이다.
  뒤지기는 운반 페널티도 상인까지의 왕복도 빼앗길 위험도 없으므로, 그 전부를
  치르는 보물보다 확실히 적게 벌어야 한다. 20을 그대로 두면 40짜리 일반 보물의
  절반을 공짜로 버는 셈이 된다.
- Consequence: 한 방이 목표의 25~55%가 됐다. 도둑은 최소 두 상점 또는 네 채를
  털어야 하고, 가방 21칸으로 모아 **최소 한 번은 상인까지 마을을 가로지른다.**
  경찰이 무언가를 할 수 있는 창은 그 왕복이다.
- Consequence: `PlayerInteractionSceneTests`에 **위쪽 한계**를 추가했다. 지금까지는
  "맵 총액 > 목표"라는 아래쪽만 검사했고, 그래서 목표를 즉시 넘길 수 있다는 사실을
  아무도 잡지 못했다. 이제 방 하나가 목표 이상이면 실패한다.
- Still open: 실측은 산수와 테스트까지다. **경기 길이가 실제로 어떻게 변하는지는
  2인 플레이테스트로만 알 수 있다.** 240초가 두 상점 + 한 번의 왕복에 충분한지가
  다음에 확인할 것이다.

### DEC-WEBGL-001: 경기 연결을 Unity Relay로 옮기고 초대코드로 만난다 (2026-08-09)

- Context: 제출물은 **링크 하나로 브라우저에서 도는 WebGL**이어야 하고
  (`SUBMIT-001`), 같은 공유기가 아닌 두 사람이 만날 수 있어야 한다.
- Finding: **브라우저는 듣는 소켓을 열 수 없다.** UDP만의 문제가 아니라 TCP로도
  서버가 될 수 없고, Unity Transport가 그 자리에서 거부한다 —
  `UnityTransport.cs`가 `m_ProtocolType != ProtocolType.RelayUnityTransport`인
  WebGL 서버에 대해 예외를 던진다. 즉 "한 명이 호스트하고 IP를 불러준다"는
  `DEC-027`의 모델은 브라우저로 가는 순간 **성립 자체를 못 한다.**
- Decision: 경기 연결을 **Unity Relay**로 보내고, Relay의 join code 6글자를 그대로
  **초대코드**로 쓴다. EC2는 정적 파일과 음성 API만 맡는다.
- Alternative rejected: **EC2에 Unity 전용 서버 풀 + 방 브로커.** 완전한 자체
  호스팅이고 `-dedicatedServer`도 이미 있었다. 거절한 이유는 셋이다 —
  (1) t3.micro는 1GB이고 Unity 헤드리스 서버는 인스턴스당 수백 MB를 먹는다,
  (2) 이 PC에 Linux Build Support가 없어 새 빌드 경로를 제출 직전에 검증해야
  한다 (`TASK-DEPLOY-003`), (3) **초대코드를 우리가 발급하고 포트에 묶어 관리하는
  코드를 새로 써야 한다** — Relay에서는 그것이 이미 답이다.
- Alternative rejected: Node로 WebSocket 릴레이를 직접 짜고 NGO 커스텀 Transport를
  구현하는 안. 자체 호스팅이면서 리눅스 빌드가 필요 없다. 검증되지 않은 코드가
  네트워크 최하단에 들어가고, 제출 기한이 그 위험을 감당할 만큼 넉넉하지 않다.
- Constraint: **전송 종류는 어디서나 `wss` 하나다.** 브라우저에 다른 선택지가 없고,
  데스크톱만 `dtls`로 두면 플레이테스트가 검증한 전송과 제출본의 전송이 갈린다 —
  이 프로젝트가 UDP를 버릴 때 이미 한 번 없앤 분기다.
- Constraint: **직접 IP 경로는 코드에 남긴다.** `TryStartHost`/`TryJoin`은 인터넷도
  Unity 계정도 없이 도는 유일한 회귀 경로이고 `-netJoinMode api`가 그대로 쓴다.
  사라진 것은 로비의 주소·포트 칸뿐이다.
- Consequence: **열어야 하는 포트가 80·443·22로 줄었다.** 경기 트래픽이 EC2를
  지나가지 않으므로 게임 포트를 열 이유가 없다 (`TASK-DEPLOY-006`).
- Consequence: **새 의존이 하나 생겼다** — Unity Cloud 프로젝트 연결. `cloudProjectId`가
  비어 있으면 방을 열 수 없고, 이건 코드로 우회할 수 없는 설정이다
  (`TASK-DEPLOY-007`). 요청을 보내기 전에 로컬에서 잡아 한 문장으로 말하도록 했다 —
  서버까지 갔다가 인증 오류로 돌아오면 네트워크 문제처럼 보인다.
- Consequence: LAN 방 발견은 브라우저에서 통째로 컴파일에서 빠지고, 데스크톱에서도
  **Relay 방은 광고하지 않는다.** 광고하면 아무것도 듣고 있지 않은 포트를 가리키는
  방이 상대 목록에 뜬다.
- Still open: **실측은 테스트와 빌드까지다.** 다른 네트워크의 두 사람이 실제로
  초대코드로 만나 한 판을 끝내는 것은 EC2 배포 후에만 확인할 수 있다
  (`TASK-DEPLOY-008`).
