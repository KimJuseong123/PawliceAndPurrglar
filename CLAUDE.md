# CLAUDE.md

Claude Code 전용 작업 지침서다.

**제품 방향, 범위, 금지 사항은 [AGENTS.md](AGENTS.md)가 기준이다.** 이 문서는
중복하지 않고, 저장소를 실제로 조작할 때 필요한 절차와 함정만 다룬다.

## 1. 세션 시작 시 읽는 순서

```text
1. CLAUDE.md            (이 문서)
2. AGENTS.md            제품 방향과 작업 규칙
3. docs/13_CURRENT_STATE.md   현재 작업 ID와 다음 작업
4. docs/09_TASK_BACKLOG.md    작업 목록과 상태
5. docs/03_GAME_RULES.md      수치와 승패 규칙
6. 현재 작업과 직접 관련된 문서
```

전체를 다시 읽지 않는다. `13_CURRENT_STATE.md`의 `현재 작업`과 `바로 다음 작업`이
지금 무엇을 할지 알려준다.

## 2. 저장소 사실 관계

| 항목 | 값 |
|---|---|
| 엔진 | Unity `6000.5.4f1` (URP `17.5.0`) |
| 저장소 루트 | `C:\Users\SSAFY\CatCops` (Unity 프로젝트 루트와 동일) |
| 런타임 어셈블리 | `PawsAndLoot.Runtime` (루트 네임스페이스 `PawsAndLoot`) |
| 현재 코드 위치 | `Assets/_Project/` |
| 레거시 실험물 | `Assets/CatCops/` — **기준으로 사용하지 않음** |
| 외부 에셋 | `Assets/TopDownEngine/` — 수정하지 않음. `Assets/ThirdParty/`는 비어 있음 |
| 빌드 씬 | `Bootstrap`, `Game`, `Result` 3개만 등록됨 |

프로젝트 이름은 `CatCops`(폴더)와 `PawsAndLoot`(어셈블리·제품명)이 섞여 있다.
새 코드는 `PawsAndLoot` 네임스페이스를 사용한다.

## 3. 가장 중요한 함정: 씬 내용은 에디터 스크립트가 만든다

`Game` 씬의 마을, 상호작용 지점, HUD, 시스템 배선은 `.unity` 파일을 손으로
편집해서 만든 것이 아니라 **에디터 스크립트가 코드로 생성**한다.

```text
Assets/_Project/Editor/GreyboxMapSetup.cs    (약 2,100줄) → Game 씬 전체
Assets/_Project/Editor/BasicSceneSetup.cs    (약 640줄)   → Bootstrap/Game/Result 골격
```

따라서:

- 씬에 오브젝트나 컴포넌트를 추가해야 하면 **해당 Setup 스크립트를 수정한 뒤
  Rebuild 메뉴를 실행**한다. `.unity` YAML을 직접 수정하지 않는다.
- HUD는 프리팹이 없다. `Assets/_Project/UI/`는 비어 있고 모든 HUD는
  `Scripts/UI/*Presenter.cs`와 Setup 스크립트가 런타임/에디터에서 조립한다.
- 씬 수동 연결이 필요하면 Setup 스크립트에 넣을 수 있는지 먼저 검토한다.

## 4. 에디터 메뉴 (`Paws & Loot`)

### Setup

```text
Rebuild Basic Scenes              Bootstrap/Game/Result 재생성
Validate Basic Scenes             씬 계약과 빌드 순서 검사
Ensure Bootstrap Services         Bootstrap 서비스 오브젝트 보장
Rebuild MAP-001 Greybox Village   Game 씬 마을 재생성
Validate MAP-001 Greybox Village  장소·경로·폭·충돌 검사
Create Default Config Assets      Settings/Configs 7개 에셋 생성
Validate Default Config Assets    설정값과 필수 참조 검사
Create Default Log Config         로그 설정 생성
Validate Logging                  로그 레벨과 중복 억제 검사
Create Default Loot Data          Data/Loot 3개 에셋 생성
Validate Default Loot Data        보물 데이터 검사
```

### Technical Validation

```text
Create / Validate / Build Windows  TECH-001  키보드 이동
Create / Validate / Build Windows  TECH-002  Blender 리그 임포트
Create / Validate / Build Windows  TECH-003  Windows 받아쓰기 (BLOCKED)
Create / Validate / Build Windows  NET-001   Host·Client 접속
                   Build Windows   NET-002   역할 배정
                   Build Windows   MAP-001   마을 횡단
```

## 5. 검증 방법

### 테스트 (배치 모드)

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.4f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Users/SSAFY/CatCops" -runTests -testPlatform EditMode -testResults "C:/Users/SSAFY/CatCops/Logs/TestResults/editmode.xml" -logFile "C:/Users/SSAFY/CatCops/Logs/editmode-tests.log"
```

`-testPlatform PlayMode`로 바꿔 Play Mode도 실행한다. 확인 사항:

- 프로세스 종료 코드
- 결과 XML의 실제 테스트 수와 실패 목록
- **테스트 0개 발견은 성공이 아니다**

현재 기준선: Edit Mode 78개, Play Mode 48개 (`MATCH-006` 시점).
테스트를 추가하면 `13_CURRENT_STATE.md`의 `최근 검증` 표에 실제 수치를 기록한다.

### 런타임 검증 (자체 보고 프로브 패턴)

이 프로젝트는 빌드된 플레이어 자체가 인수 테스트 도구다. 각 프로브는 JSON
결과와 스크린샷을 쓰고 `Application.Quit()`한다.

```text
%USERPROFILE%\AppData\LocalLow\PawsAndLoot\PawsAndLoot\
  tech-001-result.json / tech-002-result.json / tech-003-result.json
  net-001-{host,client}-result.json / map-001-result.json
```

새 기능의 런타임 검증이 필요하면 이 패턴을 따른다. 명시적 인자
(`-mapAutoQuit`, `-netMode`, `-playerRole` 등)로만 활성화해서 일반 실행을
방해하지 않는다.

### Unity 실행 전 확인

배치 모드나 빌드를 실행하기 전에:

1. Unity 에디터 프로세스가 열려 있는지 확인한다.
2. `Temp/UnityLockfile`이 남아 있는지 확인한다 (과거 `ISSUE-004`).
3. 프로세스 없이 잠금 파일만 있으면 제거 사유를 보고한 뒤 진행한다.

## 6. 코드 규칙

계층 경계와 금지 사항은 `AGENTS.md` 6절이 기준이다. 실무 요약:

- `Debug.Log`를 직접 호출하지 않는다. `GameLogger`의 7개 분류
  (Match/Player/Loot/Arrest/Companion/Voice/Network)를 사용한다.
- 수치를 하드코딩하지 않는다. `Settings/Configs/`의 ScriptableObject를
  `GameConfigService`로 조회한다.
- 승패는 `MatchResultArbiter`/`MatchResultEvaluator`만 결정한다.
  UI와 애니메이션은 읽기만 한다.
- 보물·경기 상태 전환은 전용 상태 머신(`LootStateMachine`,
  `MatchStateMachine`)을 통과해야 한다.
- 중복 실행 방지가 필요한 요청은 `LootRequestId` 같은 명시적 ID를 사용한다.
- Unity 에셋을 추가·이동할 때 `.meta` 파일을 함께 유지한다.

## 7. 아직 비어 있는 영역

다음 폴더는 존재하지만 코드가 없다. 다음 큰 작업 대상이다.

```text
Assets/_Project/Scripts/Companions/   강아지·고양이 상태 머신 (COMP/DOG/CAT)
Assets/_Project/Scripts/Input/        명령 입력 어댑터 (현재 입력은 Gameplay/Players에 흩어져 있음)
Assets/_Project/Scripts/Animation/    애니메이터 파라미터 계층
Assets/_Project/Scripts/Integration/  외부 에셋·네트워크 어댑터
```

`CompanionConfig.cs`와 `CompanionConfig.asset`은 이미 있다. 설정만 선행 생성된
상태이며 런타임 코드는 없다.

## 8. 프로토타입 단계 대체 수단

- 동물 명령: 숫자키 `1`~`4`. 실제 STT·자연어 분류·LLM 호출은 구현하지 않는다.
- 3D 모델: 그레이박스 도형. 최종 모델은 별도로 제작 중이며
  `PlayerVisualRoot.ReplaceVisual`이 교체 지점이다.
- 이 두 가지를 "AI 음성 기능 완료" 또는 "최종 아트 적용"으로 표현하지 않는다.

## 9. 작업 완료 시 갱신할 문서

한 작업을 끝내면 다음을 같은 변경에 포함한다.

| 문서 | 갱신 내용 |
|---|---|
| `docs/13_CURRENT_STATE.md` | 현재 작업 상태, 바로 다음 작업, `최근 검증` 표 |
| `docs/09_TASK_BACKLOG.md` | 작업 상태 `TODO → DONE` |
| `CHANGELOG.md` | `Unreleased`의 Added/Changed |
| `docs/14_DECISION_LOG.md` | 설계 결정을 새로 했을 때만 |
| `docs/15_KNOWN_ISSUES.md` | 문제를 발견·해결했을 때만 |
| `docs/03_GAME_RULES.md` | 밸런스 수치를 바꿨을 때 (코드만 바꾸지 않는다) |

## 10. 보고 형식

작업 완료 보고는 `AGENTS.md` 10절의 6개 항목을 사용한다.

```text
변경 요약 / 변경 파일 / 검증 / Unity 에디터 작업 / 남은 위험 / 문서 변경
```

정적 검사와 Unity 런타임 검증을 구분해서 적는다. 실행하지 않은 테스트를
통과했다고 쓰지 않는다.
