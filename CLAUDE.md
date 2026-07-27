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

> **에디터 스크립트에서 `onClick.AddListener`를 호출하지 않는다.** 이건
> 비영구(non-persistent) 리스너라 씬을 저장하면 사라진다. 에디터에서는
> 동작하는 것처럼 보이지만 빌드된 게임에서는 버튼이 아무 일도 하지 않는다.
> 실제로 `ISSUE-017`에서 로비 버튼 6개가 전부 이렇게 죽어 있었다.
> 버튼 연결은 프레젠터의 `OnEnable`에서 한다
> (`SceneNavigationButton`, `NetworkLobbyPresenter` 참고).
>
> 저장된 씬에서 확인하는 법: `.unity` 파일의 `m_OnClick:` 밑이
> `m_Calls: []`이면 연결이 없는 것이다.

> **맵 배치를 바꿨으면 `Capture Map Overview`로 평면도를 본다.** 건물이 도로
> 위에 얹혀 있거나 벽 밖으로 나가 있어도 테스트·검증기·플레이 카메라 중 어느
> 것도 잡지 못한다. 실제로 주택 2채가 골목을 막고 경찰서가 벽을 3m 뚫고 나간
> 채로 오래 남아 있었고, 평면도를 찍고 나서야 발견했다.
>
> 이 도구는 그래픽 모드 배치로 도는데 그 부작용으로 `QualitySettings`와
> `GraphicsSettings`가 더러워진다. 도구가 원복하지만, 실행 후
> `git status -- ProjectSettings/`가 비어 있는지 확인한다.

> **TopDownEngine은 저장소에 없다. 이걸 전제로 확인한다.** 라이선스가 재배포를
> 금지해서 제외돼 있고, 이 개발 PC에만 로컬로 있다. 두 가지가 걸린다
> (`ISSUE-019`).
>
> 1. **컴파일**: `Assets/CatCops/`의 레거시 브리지가 TDE를 참조한다.
>    `CATCOPS_TOPDOWNENGINE` 정의로 감싸져 있으니 그 안의 코드를 되살리지 않는다.
> 2. **애니메이션**: `CharacterLocomotion.controller`의 클립 6개가 TDE 파일이다.
>    TDE 없는 환경에서는 참조가 끊기고, `AnimatorClipGuard`가 Animator를 끈다.
>    이 가드를 없애면 캐릭터가 땅에 묻힌다 (힙 0.45m → 0.07m로 주저앉음).
>
> **스킨드 캐릭터의 위치는 `Renderer.bounds`로 재지 않는다.** 루트 본 기준
> 사전 계산 박스라 애니메이션된 실제 포즈를 반영하지 않는다. 주저앉은 캐릭터도
> 정상으로 보고한다. 뼈(`Hip`, `L_Foot`)의 world Y를 재야 한다.
> `NetworkMatchProbe`가 그 값을 기록한다.

> **씬 오브젝트를 런타임에 움직이려면 두 가지를 확인한다.** 둘 다 실패해도
> 예외도 로그도 남지 않아서, 눈으로 볼 때까지 모른다 (`ART-013`에서 뚜껑이
> 안 열린 원인이 정확히 이 두 개였다).
>
> 1. **프리팹 인스턴스는 자식 재부모화가 거부된다.** 모델은
>    `PrefabUtility.InstantiatePrefab`으로 들어오므로 `SetParent`가 그냥
>    무시된다. `PrefabUtility.UnpackPrefabInstance`로 먼저 푼다.
> 2. **`SceneOptimizationPass`가 `BatchingStatic`으로 굽는다.** 구워진
>    렌더러는 transform을 돌려도 화면에서 안 움직인다. 스킨드 메시와
>    `DynamicRootNames`, 그리고 `RaccoonBinGreeter`가 참조하는 transform만
>    제외된다. 새로 움직이는 것을 추가하면 제외 규칙도 함께 넣는다.


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
Capture Map Overview              Game 씬 상공 평면도 → Logs/map-overview.png
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

현재 기준선: Edit Mode 123개, Play Mode 77개 (`NET-010` 시점).
테스트를 추가하면 `13_CURRENT_STATE.md`의 `최근 검증` 표에 실제 수치를 기록한다.

### 런타임 검증 (자체 보고 프로브 패턴)

이 프로젝트는 빌드된 플레이어 자체가 인수 테스트 도구다. 각 프로브는 JSON
결과와 스크린샷을 쓰고 `Application.Quit()`한다.

```text
%USERPROFILE%\AppData\LocalLow\PawsAndLoot\PawsAndLoot\
  tech-001-result.json / tech-002-result.json / tech-003-result.json
  net-001-{host,client}-result.json / map-001-result.json
  net-lobby-{host,client}-result.json
  net-match-{host,client}-result.json
  net-rematch-{host,client}-result.json
```

새 기능의 런타임 검증이 필요하면 이 패턴을 따른다. 명시적 인자
(`-mapAutoQuit`, `-netMode`, `-playerRole` 등)로만 활성화해서 일반 실행을
방해하지 않는다.

### 두 프로세스 네트워크 검증 (`NET-003`~`NET-010`)

빌드를 두 번 띄우고 결과 JSON을 비교한다. 호스트를 1초 먼저 띄운다.

```bash
"Builds/Playtest/Windows/PawsAndLoot.exe" -batchmode -nographics -netLobby host   -netScenario full -netMatchSeconds 16
"Builds/Playtest/Windows/PawsAndLoot.exe" -batchmode -nographics -netLobby client -netScenario full -netMatchSeconds 16
```

`-netScenario` 3종:

| 값 | 검증 대상 | 권장 `-netMatchSeconds` |
|---|---|---|
| `full` | NET-005·006·007. 획득 → 판매 연타 → 체포 → 승자 비교 | 16 |
| `rematch` | NET-008. 클라이언트만 재경기를 눌러 양쪽 복귀 확인 | 40 |
| `disconnect` | NET-009. 클라이언트가 먼저 나가고 호스트 처리 1회 확인 | 20 |

`-netJoinMode`로 **세션을 어떻게 시작할지**를 정한다. `-netMatchSeconds`를
빼면 로비 단계에서 판정하고 끝낸다.

| 값 | 동작 |
|---|---|
| `api` (기본) | 세션 API 직접 호출. **UI를 전혀 거치지 않는다** |
| `ui` | 실제 `호스트`·`접속` 버튼을 누른다 |
| `room` | 호스트는 버튼, 클라이언트는 발견된 방 항목을 누른다 |

`api`만 돌리면 `ISSUE-017`처럼 UI가 죽어 있어도 전부 통과한다. 로비를
건드렸으면 `ui`나 `room`으로 한 번은 돌린다.

`full`은 호스트가 캐릭터를 보물·판매처·상대 옆으로 **배치**한다. 이동 경로는
`MAP-001`이 담당하고 여기서 검증하는 것은 요청이 호스트에 도달하는지와 결과가
클라이언트로 돌아오는지다.

프로브는 경기가 `Playing`에서 벗어나면 즉시 기록한다. 승패가 정해지면 경기 씬이
언로드되어 프로브가 사라지기 때문이다. 같은 이유로 스폰 수와 원격 제어 여부는
경기 중에 래치한 값을 쓴다. 종료 시점에 읽으면 전부 0으로 나온다.

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

## 7. 네트워크 계층 구조

`Assets/_Project/Scripts/Integration/Network/`가 세션 전체를 담당한다. 규칙
계층은 이 폴더를 참조하지 않는다.

| 파일 | 역할 |
|---|---|
| `NetworkSessionController` | 호스트/접속 시작, 2인 제한, 역할 보드 스폰 |
| `NetworkRoleBoard` | 역할 배정. 씬 전환 전에 **1회 통보**로 넘긴다 |
| `NetworkSceneCoordinator` | 세션 중 씬 로드를 서버만 실행 |
| `NetworkMatchMirror` | 경기 상태·타이머 복제 (NET-004) |
| `NetworkPlayerLink` | 플레이어 1명. 이동 입력 RPC, 행동 RPC, 지갑·체포 복제 |
| `NetworkLootLink` | 보물 1개. 상태·위치·소유자 복제 (NET-005) |
| `NetworkInputBridge` | 로컬 키를 자기 역할의 링크로만 전송 |
| `NetworkRematchCoordinator` | 재경기 명명 메시지 (NET-008) |
| `NetworkDisconnectHandler` | 상대 이탈 시 1회 정리 (NET-009) |

세 가지 함정을 기억한다.

1. **씬에 배치된 `NetworkObject`는 씬 전환을 넘기지 못한다** (`ISSUE-016`).
   씬을 넘겨야 하는 값은 1회 RPC + 로컬 정적 값으로, 씬을 넘겨야 하는 요청은
   `CustomMessagingManager` 명명 메시지로 보낸다.
2. **`ConnectedClientsIds`는 서버 전용이다.** 클라이언트에서 항상 비어 있다.
3. **`NetworkConfig`는 양쪽이 같아야 한다.** 런타임에 한쪽만 플래그를 바꾸면
   해시 불일치로 접속이 끊긴다. 씬 설정에서 양쪽 동일하게 켠다.

`Assets/_Project/Scripts/Integration/`의 외부 에셋 어댑터 자리는 아직 비어
있다. `Assets/_Project/UI/`도 비어 있고 HUD는 전부 코드로 조립한다.

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
