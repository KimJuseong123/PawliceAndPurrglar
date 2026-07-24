# 현재 상태

마지막 갱신: 2026-07-24

이 문서는 작업 시작 시 가장 먼저 확인하는 현재 저장소 상태다.

## 현재 목표

기존 실험 프로토타입을 새 MVP의 기준에서 분리하고, 문서와 목표 구조를 바탕으로 처음부터 그레이박스 프로토타입을 준비한다.

## 현재 단계

```text
단계 1: 저장소와 Unity 기반 정리
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
- 네트워크 우선 후보: Netcode for GameObjects와 Unity Multiplayer Services, 미설치
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

- 기존 경찰 Blender 원본 존재:

```text
ArtSource/Police/Police_LowPoly.blend
```

- 기존 경찰 FBX 존재:

```text
Assets/CatCops/Models/Police_LowPoly.fbx
```

## 아직 존재하지 않는 목표 결과

- `Assets/_Project/Scenes/Main.unity`
- 새 구조의 경찰 프리팹
- 경찰 Armature와 Avatar
- `Idle`, `Run`, `ComedyRun`
- 새 그레이박스 맵
- 새 경기 상태와 4분 타이머
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
- 새 메인 씬과 Play Mode는 아직 검증하지 않았다.

## 현재 작업

- 작업 ID: BASE-002
- 작업: Unity 버전과 패키지 고정
- 상태: 완료
- 완료 결과: Unity와 핵심 패키지 버전, Windows x86_64 우선 대상, 네트워크 후보를 고정하고 패키지 재해석과 컴파일 검증 완료

## 바로 다음 작업

1. `BASE-003`: Bootstrap, Game, Result 기본 씬 구성
2. `BASE-004`: 공통 설정 구조 생성
3. `BASE-005`: 로깅과 오류 처리
4. `BASE-006`: 테스트 구조 생성

## 차단 요소

- 네트워크 패키지 최종 채택과 권한 구조 미정
- 실제 음성 입력 기술 미정

두 항목은 현재 단계 작업을 차단하지 않는다.

## 최근 검증

| 날짜 | 범위 | 결과 |
|---|---|---|
| 2026-07-24 | 문서 파일과 현재 경로 조사 | 완료 |
| 2026-07-24 | Unity 버전 확인 | `6000.5.4f1` |
| 2026-07-24 | BASE-001 목표 폴더와 Git 경계 검사 | 완료 |
| 2026-07-24 | Unity 배치 임포트와 컴파일 | 오류 없이 종료 |
| 2026-07-24 | BASE-002 패키지 재해석과 Windows 64비트 대상 컴파일 | 오류 없이 종료 |
| 2026-07-24 | 새 메인 씬 확인 | 존재하지 않음 |
| 2026-07-24 | Unity Play Mode | 실행하지 않음 |

기능 완료, 단계 변경, 경로 변경 시 이 문서를 함께 갱신한다.
