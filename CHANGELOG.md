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

### Changed

- 프로젝트 임시 제목을 `멍경찰과 냥도둑`으로 사용
- 판매 NPC를 `너구리 상인`으로 확정
- 핵심 시점을 3D 기울어진 탑다운으로 확정
- 실제 AI와 음성 입력 전에 숫자키 `1`~`4`로 동물 명령을 검증하도록 개발 순서 변경
- 대부분의 최종 모델링을 그레이박스 프로토타입 이후로 이동
- Unity 생성 파일과 테스트 결과를 제외하도록 `.gitignore` 보강
- 사용하지 않는 2D 편집, Collaborate, Rider, Visual Scripting, Multiplayer Center 패키지 제거
- 남아 있던 `DefaultCompany`와 이전 실험 제품명을 `PawsAndLoot`으로 수정
- Unity `6000.5`와 맞지 않는 NGO `2.7.0` 대신 컴파일과 실행 검증을 통과한 `2.13.0` 채택

### Deprecated

- `Assets/CatCops/`의 기존 자동 생성 프로토타입을 새 MVP 구현 기준에서 제외
- 고정 암시장, 비밀 장터, 까마귀 상인 설정

### Known

- 기존 경찰 모델은 리그와 애니메이션이 없음
- 네트워크 서비스·권한 구조와 실제 음성 기술은 미정
- 현재 개발 PC에서 Unity 내장 Windows 받아쓰기가 `0x80004003`으로 생성되지 않음
- 기본 씬 버튼의 직접 Play Mode 클릭 검증은 아직 수행하지 않음
