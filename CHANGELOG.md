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

### Changed

- 프로젝트 임시 제목을 `멍경찰과 냥도둑`으로 사용
- 판매 NPC를 `너구리 상인`으로 확정
- 핵심 시점을 3D 기울어진 탑다운으로 확정
- 실제 AI와 음성 입력 전에 숫자키 `1`~`4`로 동물 명령을 검증하도록 개발 순서 변경
- 대부분의 최종 모델링을 그레이박스 프로토타입 이후로 이동
- Unity 생성 파일과 테스트 결과를 제외하도록 `.gitignore` 보강
- 사용하지 않는 2D 편집, Collaborate, Rider, Visual Scripting, Multiplayer Center 패키지 제거

### Deprecated

- `Assets/CatCops/`의 기존 자동 생성 프로토타입을 새 MVP 구현 기준에서 제외
- 고정 암시장, 비밀 장터, 까마귀 상인 설정

### Known

- 기존 경찰 모델은 리그와 애니메이션이 없음
- 네트워크 최종 방식과 실제 음성 기술은 미정
- 기본 씬 버튼의 직접 Play Mode 클릭 검증은 아직 수행하지 않음
