# 변경 기록

이 프로젝트는 의미 있는 설계, 기능, 문서 변경을 기록한다.

## Unreleased

### Added

- 프로젝트 루트 `AGENTS.md`
- 프로젝트 소개와 목표 구조를 담은 `README.md`
- `docs/00_PROJECT_BRIEF.md`부터 `docs/15_KNOWN_ISSUES.md`까지의 문서 패키지
- 기능, 버그, 코드 리뷰, 리팩터링 프롬프트 템플릿
- Blender 원본과 Unity 반입 자산을 분리하는 `ArtSource/` 방향
- 경찰 모델 리깅 및 `Idle`, `Run`, `ComedyRun` 기술 스파이크 계획
- 현재 Unity 프로젝트 루트의 로컬 Git 저장소와 `main` 브랜치
- Blender, FBX, 오디오와 Unity 패키지를 위한 Git LFS 규칙
- `Assets/_Project/`, `Assets/ThirdParty/`, `ArtSource/Blender/`, `Builds/`, `Submission/` 목표 폴더 구조
- Unity `6000.5.4f1`, URP `17.5.0`, Input System `1.19.0`, Test Framework `1.7.0` 고정 환경
- Windows x86_64 우선 빌드 대상과 네트워크 기술 검증 후보
- `Bootstrap`, `Game`, `Result` 기본 씬과 빌드 순서
- 중앙화된 씬 ID, 로더, 공통 전환 버튼과 에디터 검증 도구
- 경기, 플레이어, 보물, 체포, 동물과 음성용 ScriptableObject 설정
- 필수 설정을 묶는 `DefaultGameConfigSet`, Bootstrap 초기화와 명확한 범위 오류 검증
- Match, Player, Loot, Arrest, Companion, Voice, Network 분류형 런타임 로그
- 개발·제출 빌드별 로그 레벨 설정, 동일 키 중복 억제와 원본 예외 기록
- 분리된 런타임, Edit Mode, Play Mode Assembly Definition과 기본 테스트
- Windows TECH-001 전용 씬, Input System 이동 큐브와 실행 결과 기록
- Blender 5.2 테스트 더미 생성기, 5본 Generic 리그, 단일 재질과 `Idle`·`Walk`
- 이동·충돌과 모델을 분리한 `PlayerRoot/VisualRoot` 검증 프리팹과 TECH-002 Windows 빌드
- Windows 마이크, 받아쓰기 상태, 오류 코드와 키보드 대체 입력을 표시하는 TECH-003 격리 빌드
- Netcode for GameObjects `2.13.0`과 Unity Transport `6.5.0`
- 로컬 직접 IP Host·Client 접속, 두 플레이어 위치 동기화와 종료 처리를 검증하는 NET-001 빌드
- 서버 권한 경찰·도둑 역할 배정, 역할별 시작점과 2명 제한을 검증하는 NET-002 모드
- 56×44m 순환형 회색 상자 마을과 필수 장소 7개
- 주요 장소별 복수 경로, 지붕 3개, 사다리 3개와 쓰레기통 위치 4개
- 경로 폭·물리 장애물 자동 검사와 Windows 캡슐 횡단 검증
- 읽기 전용 현재 상태 계약과 순차 전환만 허용하는 MATCH-001 상태 머신
- 경기 상태 중복·건너뛰기·역방향 전환 방지와 상태 변경 알림
- 본게임 경찰·도둑 역할, 역할별 시작점 해석과 상호작용 권한표
- Game 씬의 파란 경찰·빨간 도둑 임시 역할 표시
- Input System WASD와 CharacterController를 사용하는 공통 플레이어 이동 모터
- 경찰 추적용 3D 원근 탑다운 카메라와 경기 상태 이동 제한
- MAP 자동 횡단 프로브를 명시적 `-mapAutoQuit` 검증 실행으로 격리
- 경찰 이동 코드를 복사하지 않고 같은 모터를 사용하는 도둑 이동
- `-playerRole Police|Thief` 로컬 역할 선택과 선택 역할 카메라 추적
- Space 공통 대시, 지속시간·쿨타임·경기 상태 제한과 상태 조회
- `E` 공통 상호작용, 거리 기반 대상 선택과 역할·경기 상태 권한 검사
- Game 씬 보물·판매·사다리·일반 프로토타입 상호작용 지점과 안내 UI
- 물리·이동·상호작용·네트워크와 렌더 모델을 분리한 `PlayerVisualRoot`
- Blender 모델 교체 후에도 루트 충돌과 이동을 유지하는 VisualRoot 계약 테스트
- `MatchConfig` 기반 3초 READY 카운트다운과 중복 시작 방지
- PLAYING에서만 감소하고 0초에 ENDING으로 전환되는 4분 경기 타이머
- 시간·역할·경기 상태·상호작용 안내를 읽기 전용으로 표시하는 공통 HUD
- 씬 재진입 시 공통 HUD 중복 인스턴스를 제거하는 수명주기 보호
- READY와 경기 시작 직후 경찰·도둑 목표를 한 문장으로 표시하는 역할 안내
- `AVAILABLE`, `RESERVED`, `CARRIED`, `DROPPED`, `HIDDEN`, `SOLD` 보물 상태 머신
- 판매된 보물의 모든 후속 전환을 거부하는 종료 상태 규칙
- 안정적인 ID·표시 이름·희귀도를 가진 `LootDefinition` 데이터 에셋 3종
- `LootConfig` 단일 가격표로 관리하는 일반 200·고급 350·희귀 500 가격
- 도둑·PLAYING·빈손 조건을 모두 검사하는 `LootCarrier` 보물 획득 권한
- 획득 시 `RESERVED → CARRIED` 전환과 보물·소지자 양방향 소유 관계
- Game 씬 보석상 앞 프로토타입 보물의 실제 획득 상호작용
- 플레이어 루트의 `CarryPoint`를 따라오는 보물 `PresentationRoot`
- 운반 중 월드 충돌체 비활성화와 소지자 비활성화·파괴 시 안전한 `DROPPED` 복구
- `Q` 보물 드롭, 전방 지면 탐색과 바닥 여유 높이를 적용한 안전한 월드 배치
- 비활성 경기·바닥 없음·중복 드롭 거부와 드롭 보물 재획득
- `PlayerConfig` 기반 보물 운반 속도 0.9배 페널티와 현재 배율 조회
- 소지 변경 이벤트에 연동된 비누적 페널티 적용, 드롭·플레이어 재활성화 시 원속도 복구
- 너구리 장터의 실제 `LootSaleZone` 트리거와 도둑 전용 `ThiefLootWallet`
- 희귀도 가격표 기반 판매 정산, `SOLD` 처리, 소지·이동 페널티 해제와 승리 검사 요청
- 획득·판매 요청을 식별하는 `LootRequestId`와 성공 요청 재실행 방지
- 지갑의 요청·보물 이중 정산 원장으로 버튼 연타와 네트워크 중복 판매 차단
- 판매 금액·목표 금액·보유 보물·가격·이동 페널티·판매 가능 여부를 읽기 전용으로 표시하는 도둑 HUD
- 도둑 HUD의 빈손·운반·판매 가능·정산 완료·역할 전환 표시를 검증하는 Play Mode 테스트
- 경찰·도둑 거리와 장애물을 검사하고 진입·이탈 이벤트를 발행하는 `ArrestRangeSensor`
- 비활성·잘못된 역할·자기 자신을 체포 대상으로 인정하지 않는 ARREST-001 검증
- `PLAYING` 중 유효한 체포 감지가 유지될 때만 시간을 누적하는 `ArrestProgressController`
- `ArrestConfig`의 1.5초 체포 시간을 0~1 진행도로 변환하고 완료 값에서 중복 누적을 막는 ARREST-002 검증
- 범위·시야 상실, 참가자 비활성화, 경기 상태 변경 시 진행도를 즉시 초기화하는 ARREST-003 중단 규칙
- 실제 진행 중이었을 때만 원인별 `ProgressInterrupted` 이벤트를 한 번 발행하는 중복 방지 검증
- 체포 진행도 완료를 한 번만 확정하고 경찰 승리·결과 연출 요청을 발행하는 `ArrestCompletionController`
- 체포 직후 `ENDING` 전환으로 이동과 상호작용을 함께 중지하는 ARREST-004 통합 검증
- 경찰의 체포 진행·중단·완료와 도둑의 위험 경고를 역할별로 표시하는 `ArrestHudPresenter`
- 게임 상태를 변경하지 않는 읽기 전용 체포 게이지와 역할 전환 표시를 검증하는 ARREST-005 테스트
- 남은 시간·도둑 판매액·체포율·도난 알림·현재 목표를 모은 역할 전용 `PoliceHudPresenter`
- 보물 운반과 판매 완료 알림을 구분하고 도둑 역할에서 숨기는 UI-002 읽기 전용 HUD 검증
- 체포·판매·시간 종료 요청을 같은 평가 시점에 모아 단일 `MatchResult`를 결정하는 `MatchResultEvaluator`
- 동시 조건을 체포, 목표 판매, 시간 종료 순으로 판정하고 이미 확정된 결과를 바꾸지 않는 MATCH-004 중재 규칙

### Changed

- 체포 완료와 타이머 만료는 직접 `ENDING`으로 전환하지 않고 중앙 승패 판정기에 요청한다.

- 운반 페널티 복원은 씬 초기화 순서와 무관하게 적용되도록 이동 모터의 불필요한 경기 상태 검사를 제거
- 중앙 광장 상호작용 마커를 측정 경로 밖으로 옮기고 자동 측정 중 플레이어 충돌을 격리해 MAP-001 횡단을 복구
- 프로젝트 임시 제목을 `멍경찰과 냥도둑`으로 사용
- 판매 NPC를 `너구리 상인`으로 확정
- 핵심 시점을 3D 기울어진 탑다운으로 확정
- 실제 AI와 음성 입력 전에 숫자키 `1`~`4`로 동물 명령을 검증하도록 개발 순서 변경
- 대부분의 최종 모델링을 그레이박스 프로토타입 이후로 이동
- Unity 생성 파일과 테스트 결과를 제외하도록 `.gitignore` 보강
- 사용하지 않는 2D 편집, Collaborate, Rider, Visual Scripting, Multiplayer Center 패키지 제거
- 남아 있던 `DefaultCompany`와 이전 실험 제품명을 `PawsAndLoot`으로 수정
- Unity `6000.5`와 맞지 않는 NGO `2.7.0` 대신 컴파일과 실행 검증을 통과한 `2.13.0` 채택
- 네트워크 기술 검증 역할을 Host 경찰, 첫 Client 도둑으로 고정하고 추가 접속을 거절
- `Game` 씬을 MAP-001 회색 상자 마을로 교체하고 너구리 거래장터 앵커를 사용
- 백로그 `UI-007`의 잘못된 결과 화면 표기를 역할별 목표 안내로 수정

### Deprecated

- `Assets/CatCops/`의 기존 자동 생성 프로토타입을 새 MVP 구현 기준에서 제외
- 고정 암시장, 비밀 장터, 까마귀 상인 설정

### Known

- 기존 경찰 모델은 리그와 애니메이션이 없음
- 네트워크 서비스·권한 구조와 실제 음성 기술은 미정
- 현재 개발 PC에서 Unity 내장 Windows 받아쓰기가 `0x80004003`으로 생성되지 않음
- 기본 씬 버튼의 직접 Play Mode 클릭 검증은 아직 수행하지 않음
- 숨김 창 Windows 실행에서는 MAP 자동 캡처가 렌더 프레임을 얻지 못해 시간 초과될 수 있음
