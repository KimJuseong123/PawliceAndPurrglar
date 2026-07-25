# 멍경찰과 냥도둑

**Paws & Loot**

경찰은 강아지에게 추적과 경계를 명령하고, 도둑은 고양이에게 정찰과 교란을 명령해 4분 동안 보물을 지키거나 훔치는 1대1 음성 명령 비대칭 추격 게임.

> 현재 제목은 임시 제목이며, 프로젝트 방향을 고정하기 전까지 `멍경찰과 냥도둑`을 사용합니다.

## 발표용 30초 소개

멍경찰과 냥도둑은 경찰과 도둑이 각자의 AI 반려동물에게 직접 음성 명령을 내리며 대결하는 비대칭 추격 게임입니다. 경찰은 강아지의 후각을 이용해 도둑을 추적하고, 도둑은 고양이를 보내 보물을 찾거나 경찰의 시선을 돌립니다. 플레이어는 직접 움직이는 동시에 반려동물에게 명령해야 하므로, 혼자 조작하지만 둘이 협동하는 듯한 재미를 느낄 수 있습니다.

## 게임 개요

| 항목 | 내용 |
|---|---|
| 장르 | 캐주얼 전략 액션 / 비대칭 추격전 |
| 플레이 인원 | 2명, 경찰 1명 대 도둑 1명 |
| 경기 시간 | 4분 |
| 시점 | 원근감 있는 3D 기울어진 탑다운 |
| 엔진 | Unity |
| 주요 제작 도구 | Unity, Blender |
| 현재 단계 | 저장소 및 Unity 프로젝트 기반 정리 |

## 게임의 핵심

경찰과 도둑은 자신의 캐릭터를 직접 조작하면서 동물 파트너에게 별도의 명령을 내립니다.

경찰은 강아지를 이용해 도둑의 흔적을 찾고 주요 보물 구역을 경계합니다.
도둑은 고양이를 보내 보물 위치를 정찰하고 경찰과 강아지의 시선을 돌립니다.

경찰의 목표는 보물을 지키고 도둑을 체포하는 것입니다.
도둑의 목표는 제한 시간 안에 보물을 훔쳐 마을 어딘가에 나타나는 너구리 상인에게 판매하는 것입니다.

이 게임이 지향하는 감정은 진지한 범죄 대결보다 다음에 가깝습니다.

> 말랑하고 허술한 캐릭터들이 작은 3D 마을을 뛰어다니며 벌이는, 한눈에 읽히는 우당탕 추격 코미디

## 시점과 조작감

게임은 정적인 아이소메트릭이나 1인칭이 아니라, 플레이어를 따라가는 원근 투영 탑다운 카메라를 사용합니다.

- 경찰, 도둑, 동물 파트너와 투척물이 한 화면에서 읽혀야 합니다.
- 추격이 빨라지면 카메라가 조금 멀어져 주변 경로를 보여줍니다.
- 건물이 캐릭터를 가리면 지붕이나 벽을 투명하게 처리합니다.
- 투척물은 궤적과 착탄 위치를 알아볼 수 있어야 합니다.
- 피격과 미끄러짐은 짧은 히트 스톱, 카메라 반응, 후속 애니메이션으로 강조합니다.

조작은 즉각적이어야 합니다.
캐릭터의 멍청하고 귀여운 느낌은 입력 지연이 아니라 팔을 벌리고 달리거나, 모자와 눈알이 늦게 흔들리고, 급정지 때 몸이 앞으로 쏠리는 애니메이션으로 표현합니다.

## 동물 파트너 명령

최종적으로는 음성 입력을 동물 파트너의 명령으로 변환합니다.
하지만 핵심 게임 플레이가 검증되기 전에는 실제 음성 인식이나 자연어 AI를 연결하지 않습니다.

프로토타입에서는 모든 명령을 숫자키 `1`~`4`로 실행합니다.

### 경찰 강아지

| 키 | 임시 명령 ID | 역할 |
|---|---|---|
| `1` | `TRACK` | 도둑의 흔적 또는 위치를 추적 |
| `2` | `SEARCH` | 주변을 수색 |
| `3` | `GUARD` | 지정한 구역을 경계 |
| `4` | `BARK` | 도둑을 압박하거나 위치를 드러냄 |

### 도둑 고양이

| 키 | 임시 명령 ID | 역할 |
|---|---|---|
| `1` | `SCOUT` | 보물이나 안전한 경로를 정찰 |
| `2` | `DISTRACT` | 경찰 또는 강아지의 주의를 분산 |
| `3` | `STEAL` | 가까운 목표물에 절도 행동 수행 |
| `4` | `HIDE` | 은신 지점으로 이동하거나 대기 |

명령 이름과 세부 효과는 플레이테스트 후 변경될 수 있습니다.
키보드와 향후 음성 입력은 동일한 명령 ID를 사용해야 합니다.

## 아트 제작 방식

초기 프로토타입은 캐릭터, 동물, 집, 소품을 단순한 도형과 임시 머티리얼로 제작합니다.
이 단계에서는 이동, 추격, 명령, 보물, 체포와 같은 핵심 흐름을 우선 검증합니다.

대부분의 최종 모델은 프로토타입 이후 Blender에서 별도로 제작하고 FBX로 Unity에 반입합니다.
Blender 원본과 Unity 런타임 에셋을 분리해 관리합니다.

- Blender 원본: `ArtSource/Blender/`
- Unity 모델: `Assets/_Project/Art/Models/`
- Unity 캐릭터 프리팹: `Assets/_Project/Prefabs/Characters/`

현재 예외는 경찰 모델 한 체입니다.
기존 경찰 모델을 먼저 사용해 다음 항목만 검증합니다.

- Blender와 Unity의 축, 단위와 피벗
- Humanoid 리깅 및 리타게팅 가능 여부
- Animator 전환
- 이동 코드와 애니메이션의 분리
- 탑다운 시점에서의 실루엣
- `Idle`, `Run`, `ComedyRun` 애니메이션

현재 경찰 모델 원본:

```text
ArtSource/Police/Police_LowPoly.blend
```

현재 Unity 반입본:

```text
Assets/CatCops/Models/Police_LowPoly.fbx
```

새 저장소 구조를 적용할 때 검증된 경찰 모델만 `Assets/_Project/` 아래로 이전합니다.
기존 `Assets/CatCops/` 프로토타입의 나머지 코드와 에셋은 새 설계의 기준으로 사용하지 않습니다.

## 프로토타입 개발 순서

1. 저장소 구조와 기획 문서를 확정합니다.
2. 단순 도형으로 마을, 이동, 카메라를 구현합니다.
3. 경찰과 도둑의 추격 및 체포 흐름을 구현합니다.
4. 보물 획득, 운반, 너구리 상인 판매 흐름을 구현합니다.
5. 강아지와 고양이 명령을 숫자키 `1`~`4`로 구현합니다.
6. 4분 타이머와 승패 판정을 연결합니다.
7. 경찰 모델로 리깅과 `Idle`, `Run`, `ComedyRun`을 검증합니다.
8. 플레이테스트로 핵심 재미와 시인성을 확인합니다.
9. 검증 후 실제 음성 입력, 멀티플레이, 최종 모델과 연출을 단계적으로 적용합니다.

## 현재 상태

프로젝트 기반과 고위험 기술 검증을 거쳐 56×44m 회색 상자 마을을 구성했습니다.
현재는 경기 상태와 플레이어 규칙을 순서대로 연결하는 단계입니다.

### 고정 개발 환경

| 항목 | 버전 또는 결정 |
|---|---|
| Unity | `6000.5.4f1` |
| 렌더 파이프라인 | URP `17.5.0` |
| 입력 | Input System `1.19.0`, Player Settings `Both` |
| 테스트 | Unity Test Framework `1.7.0` |
| 내비게이션 | AI Navigation `2.0.13` |
| 카메라 | Cinemachine `3.1.7` |
| 첫 빌드 대상 | Windows x86_64 |

Unity Hub에서 저장소 루트를 열면 `Packages/manifest.json`과 `Packages/packages-lock.json`을 기준으로 패키지가 복원됩니다.
네트워크는 Netcode for GameObjects와 Unity Multiplayer Services를 우선 후보로 두지만 기술 검증 전에는 설치하지 않습니다.

핵심 게임 수치는 다음 Inspector 에셋에서 관리합니다.

```text
Assets/_Project/Settings/Configs/
```

`Paws & Loot > Setup > Create Default Config Assets`는 기본 설정을 생성하고
Bootstrap 씬에 연결합니다. `Validate Default Config Assets`는 저장된 값과 필수
참조를 검사합니다. 현재 목표 금액, 이동, 대시, 체포, 보물 가격과 동물 수치는
플레이테스트 전 프로토타입 가설입니다.

런타임 로그 설정은 다음 에셋에서 확인합니다.

```text
Assets/_Project/Settings/Logging/DefaultGameLogConfig.asset
```

Editor와 Development Build는 `Debug` 이상, 일반 제출 빌드는 `Warning` 이상을
기본 출력합니다. 런타임 코드는 직접 `Debug.Log`를 호출하지 않고
`GameLogger`의 Match, Player, Loot, Arrest, Companion, Voice, Network 분류를
사용합니다.

현재 확정된 사항:

- 임시 제목은 `멍경찰과 냥도둑`
- 1대1, 4분 비대칭 추격전
- 3D 기울어진 탑다운 시점
- 경찰과 강아지, 도둑과 고양이의 역할 구조
- 판매 NPC는 너구리 상인
- AI 및 음성 기능은 숫자키 명령으로 먼저 검증
- 최종 모델은 그레이박스 프로토타입 이후 적용
- 기존 경찰 모델만 리깅 및 애니메이션 테스트에 사용
- 경기 상태는 `LOBBY → READY → PLAYING → ENDING → RESULT` 순서만 허용
- 본게임 역할은 `Police`, `Thief`로 구분하며 맵의 서로 다른 시작점을 사용
- 경찰은 `WASD`로 이동하며 `CharacterController` 충돌과 원근 탑다운 카메라를 사용

아직 확정되지 않은 사항:

- 네트워크 패키지의 최종 채택과 서버 구조
- Windows 이후 WebGL 지원 여부
- 명령별 정확한 효과와 쿨타임
- 보물 종류와 목표 금액
- 체포 방식과 세부 밸런스
- 최종 맵 크기와 건물 수
- 실제 음성 인식 및 자연어 분류 방식

세부 상태는 이후 `docs/13_CURRENT_STATE.md`에서 관리합니다.

## 목표 저장소 구조

Blender 원본을 Unity 에셋과 분리하기 위해 제안 구조에 `ArtSource/`를 추가합니다.

```text
PawsAndLoot/
├─ AGENTS.md
├─ README.md
├─ CHANGELOG.md
├─ .gitignore
│
├─ docs/
│  ├─ 00_PROJECT_BRIEF.md
│  ├─ 01_GDD.md
│  ├─ 02_MVP_SCOPE.md
│  ├─ 03_GAME_RULES.md
│  ├─ 04_AI_VOICE_SPEC.md
│  ├─ 05_TECH_ARCHITECTURE.md
│  ├─ 06_UI_UX_SPEC.md
│  ├─ 07_ART_AUDIO_GUIDE.md
│  ├─ 08_DEVELOPMENT_PIPELINE.md
│  ├─ 09_TASK_BACKLOG.md
│  ├─ 10_TEST_PLAN.md
│  ├─ 11_AI_USAGE_LOG.md
│  ├─ 12_SUBMISSION_CHECKLIST.md
│  ├─ 13_CURRENT_STATE.md
│  ├─ 14_DECISION_LOG.md
│  └─ 15_KNOWN_ISSUES.md
│
├─ prompts/
│  ├─ TASK_TEMPLATE.md
│  ├─ BUG_FIX_TEMPLATE.md
│  ├─ CODE_REVIEW_TEMPLATE.md
│  ├─ REFACTOR_TEMPLATE.md
│  └─ logs/
│
├─ ArtSource/
│  └─ Blender/
│     ├─ Characters/
│     ├─ Animals/
│     ├─ Buildings/
│     └─ Props/
│
├─ Assets/
│  ├─ _Project/
│  │  ├─ Art/
│  │  ├─ Audio/
│  │  ├─ Data/
│  │  ├─ Materials/
│  │  ├─ Prefabs/
│  │  ├─ Scenes/
│  │  ├─ Scripts/
│  │  ├─ Settings/
│  │  ├─ Tests/
│  │  └─ UI/
│  └─ ThirdParty/
│
├─ Builds/
└─ Submission/
   ├─ GameIntroduction/
   ├─ AIUsageDocument/
   ├─ TeamRoles/
   ├─ Screenshots/
   └─ Video/
```

## 문서 안내

| 문서 | 목적 |
|---|---|
| `docs/00_PROJECT_BRIEF.md` | 프로젝트 요약과 발표용 설명 |
| `docs/01_GDD.md` | 전체 게임 디자인 |
| `docs/02_MVP_SCOPE.md` | MVP 포함 및 제외 범위 |
| `docs/03_GAME_RULES.md` | 승패 규칙과 밸런스 값 |
| `docs/04_AI_VOICE_SPEC.md` | 동물 명령 및 향후 음성 입력 사양 |
| `docs/05_TECH_ARCHITECTURE.md` | 코드와 데이터 흐름 |
| `docs/06_UI_UX_SPEC.md` | 화면과 사용자 경험 |
| `docs/07_ART_AUDIO_GUIDE.md` | 모델, 애니메이션, 이펙트, 사운드 기준 |
| `docs/08_DEVELOPMENT_PIPELINE.md` | 개발 단계와 완료 조건 |
| `docs/09_TASK_BACKLOG.md` | 작업 목록과 우선순위 |
| `docs/10_TEST_PLAN.md` | 자동 및 수동 테스트 계획 |
| `docs/11_AI_USAGE_LOG.md` | 공모전 제출용 AI 활용 기록 |
| `docs/12_SUBMISSION_CHECKLIST.md` | 제출 항목과 최종 점검 |
| `docs/13_CURRENT_STATE.md` | 현재 구현 상태 |
| `docs/14_DECISION_LOG.md` | 채택된 결정과 변경 이유 |
| `docs/15_KNOWN_ISSUES.md` | 알려진 문제와 재현 절차 |

## 실행 방법

기본 씬은 다음 세 개입니다.

```text
Assets/_Project/Scenes/Bootstrap.unity
Assets/_Project/Scenes/Game.unity
Assets/_Project/Scenes/Result.unity
```

빌드 순서는 `Bootstrap`, `Game`, `Result`이며 시작 씬은 `Bootstrap`입니다.
씬 이름과 경로는 `GameSceneCatalog` 한 곳에서 관리합니다.

기본 실행 절차:

1. `ProjectSettings/ProjectVersion.txt`에 적힌 Unity 버전을 설치합니다.
2. Unity Hub에서 저장소 루트를 프로젝트로 엽니다.
3. 패키지 임포트와 스크립트 컴파일이 끝날 때까지 기다립니다.
4. `Assets/_Project/Scenes/Bootstrap.unity`를 엽니다.
5. Play Mode에서 `START GAME`을 눌러 `Game` 씬으로 이동합니다.
6. `SHOW RESULT`를 눌러 `Result` 씬으로 이동합니다.
7. `PLAY AGAIN` 또는 `BACK TO START`로 흐름을 다시 확인합니다.

## 개발 원칙

- 핵심 재미를 검증하기 전에 최종 모델과 AI 서비스를 만들지 않습니다.
- 한 번에 하나의 독립 기능을 구현합니다.
- 키보드 명령과 향후 음성 명령은 같은 게임 명령을 사용합니다.
- 외부 에셋과 프로젝트 코드를 섞지 않습니다.
- 구현과 문서가 다르면 같은 작업에서 함께 수정합니다.
- 실행하지 않은 테스트를 통과했다고 기록하지 않습니다.
- AI 도구 사용 내역은 `docs/11_AI_USAGE_LOG.md`에 남깁니다.

세부 작업 규칙은 [AGENTS.md](AGENTS.md)를 따릅니다.

## 테스트 실행

테스트 코드는 런타임 코드와 분리되어 있습니다.

```text
Assets/_Project/Scripts/PawsAndLoot.Runtime.asmdef
Assets/_Project/Tests/EditMode/PawsAndLoot.Tests.EditMode.asmdef
Assets/_Project/Tests/PlayMode/PawsAndLoot.Tests.PlayMode.asmdef
```

Unity 에디터에서는 `Window > General > Test Runner`를 열고 `EditMode` 또는
`PlayMode` 탭에서 `Run All`을 실행합니다.

PowerShell 배치 실행 예시:

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe"
$project = "C:\Users\SSAFY\CatCops"

$editMode = Start-Process $unity -Wait -PassThru -ArgumentList @(
  "-batchmode", "-nographics",
  "-projectPath", $project,
  "-runTests", "-testPlatform", "EditMode",
  "-testResults", "$project\Logs\TestResults\editmode.xml",
  "-logFile", "$project\Logs\editmode-tests.log"
)
if ($editMode.ExitCode -ne 0) { throw "Edit Mode tests failed." }

$playMode = Start-Process $unity -Wait -PassThru -ArgumentList @(
  "-batchmode", "-nographics",
  "-projectPath", $project,
  "-runTests", "-testPlatform", "PlayMode",
  "-testResults", "$project\Logs\TestResults\playmode.xml",
  "-logFile", "$project\Logs\playmode-tests.log"
)
if ($playMode.ExitCode -ne 0) { throw "Play Mode tests failed." }
```

Test Runner의 프로세스 종료 코드와 결과 XML을 모두 확인합니다. 테스트가 0개
발견된 실행은 성공으로 간주하지 않습니다.

## 기술 검증 빌드

Windows TECH-001 검증 씬:

```text
Assets/_Project/Scenes/TechnicalTest.unity
```

Unity 메뉴 `Paws & Loot > Technical Validation > Build Windows TECH-001`로
WASD 이동 큐브가 포함된 Windows x86_64 개발 빌드를 생성합니다.

```text
Builds/TechnicalValidation/Windows/PawsAndLootTech.exe
```

실행 결과는 다음 로컬 경로에 JSON과 스크린샷으로 기록됩니다.

```text
%USERPROFILE%\AppData\LocalLow\PawsAndLoot\PawsAndLoot\
```

Windows가 첫 목표 플랫폼이므로 WebGL 로컬 서버와 GitHub Pages 검증은 현재
수행하지 않습니다. WebGL을 보조 플랫폼으로 다시 채택할 때 TECH-001의 별도
WebGL 검증으로 진행합니다.

### Blender 연동 검증

TECH-002 테스트 모델은 최종 캐릭터가 아니라 단위, 축, 리그, 애니메이션과
모델 교체 구조를 확인하는 전용 더미입니다. Blender 5.2에서 다음 명령으로
원본 `.blend`와 Unity 반입용 `.fbx`를 다시 생성할 수 있습니다.

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" `
  --background `
  --python "C:\Users\SSAFY\CatCops\ArtSource\Blender\TechnicalValidation\create_tech_rig.py"
```

Unity 메뉴 `Paws & Loot > Technical Validation > Build Windows TECH-002`는
다음을 자동으로 검사하고 Windows 개발 빌드를 생성합니다.

- 미터 단위, 발 중앙 원점, Unity 축과 회전
- Generic 리그 5본과 단일 스킨 메시
- Blender에서 가져온 파란 재질 1개
- `Idle`, `Walk` 애니메이션
- `PlayerRoot`의 이동 및 Collider와 교체 가능한 `VisualRoot` 분리
- 루트 모션 비활성화

```text
Assets/_Project/Scenes/BlenderTechnicalTest.unity
Assets/_Project/Prefabs/TechnicalValidation/TechRigPlayer.prefab
Builds/TechnicalValidation/Windows/PawsAndLootBlenderTech.exe
```

실행 결과는 같은 LocalLow 폴더의 `tech-002-result.json`과
`tech-002-screenshot.png`에 기록됩니다.

### Windows 음성 텍스트 검증

TECH-003은 AI 대화나 동물 명령을 구현하지 않고 Windows 마이크 입력을 텍스트로
받을 수 있는지만 확인하는 격리된 장면입니다.

```text
Assets/_Project/Scenes/VoiceTechnicalTest.unity
Builds/TechnicalValidation/Windows/PawsAndLootVoiceTech.exe
```

Unity 메뉴 `Paws & Loot > Technical Validation > Build Windows TECH-003`으로
빌드합니다. 실행 후 `Start listening`을 누르고 짧은 한국어 문장을 말합니다.
음성을 사용할 수 없을 때는 `Space` 또는 `Continue with keyboard`로 계속할 수
있어야 합니다.

2026-07-24 현재 개발 PC의 실제 WindowsPlayer에서는 마이크 1개를 확인했지만
Unity 내장 `DictationRecognizer` 생성이 다음 오류로 실패했습니다.

```text
0x80004003: Speech recognition is not supported on this machine.
```

따라서 내장 Windows 받아쓰기는 채택하지 않았고 TECH-003은 차단 상태입니다.
Windows 음성 언어 팩이 준비된 PC에서 재검증하거나 외부 STT 후보를 별도
선정해야 합니다. Unity 6의 이 API는 Windows 전용 Legacy API이며 운영체제의
음성 개인정보 설정도 필요합니다.

- 공식 API: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Windows.Speech.DictationRecognizer.html
- 로컬 결과: `tech-003-result.json`, `tech-003-screenshot.png`

### 로컬 네트워크 접속 검증

NET-001은 Netcode for GameObjects `2.13.0`과 Unity Transport `6.5.0`으로
직접 IP Host·Client 접속을 확인하는 격리된 장면입니다. Lobby, Relay와 본게임
규칙 동기화는 포함하지 않습니다.

```text
Assets/_Project/Scenes/NetworkTechnicalTest.unity
Builds/TechnicalValidation/Windows/PawsAndLootNetworkTech.exe
```

Unity 메뉴 `Paws & Loot > Technical Validation > Build Windows NET-001`로
빌드한 뒤 PowerShell에서 두 프로세스를 실행합니다.

```powershell
$exe = "C:\Users\SSAFY\CatCops\Builds\TechnicalValidation\Windows\PawsAndLootNetworkTech.exe"
$hostProcess = Start-Process $exe -PassThru -ArgumentList @(
  "-netMode", "host", "-netInstance", "host",
  "-netAddress", "127.0.0.1", "-netPort", "7979",
  "-netQuitAfter", "12"
)
Start-Sleep -Seconds 2
$clientProcess = Start-Process $exe -PassThru -ArgumentList @(
  "-netMode", "client", "-netInstance", "client",
  "-netAddress", "127.0.0.1", "-netPort", "7979",
  "-netDisconnectAfter", "8", "-netQuitAfter", "10"
)
```

결과는 LocalLow 폴더의 `net-001-host-result.json`과
`net-001-client-result.json`에 기록됩니다. 2026-07-24 실제 Windows 빌드에서는
두 플레이어 생성, 서로 다른 소유자와 위치, 이동 동기화, Client 종료 감지가
두 결과 모두 `passed: true`였습니다.

NET-002는 같은 실행 파일에 `-netTask net-002`를 추가합니다.

```powershell
$hostProcess = Start-Process $exe -PassThru -ArgumentList @(
  "-netTask", "net-002",
  "-netMode", "host", "-netInstance", "host",
  "-netAddress", "127.0.0.1", "-netPort", "7980",
  "-netQuitAfter", "12"
)
Start-Sleep -Seconds 2
$clientProcess = Start-Process $exe -PassThru -ArgumentList @(
  "-netTask", "net-002",
  "-netMode", "client", "-netInstance", "client",
  "-netAddress", "127.0.0.1", "-netPort", "7980",
  "-netDisconnectAfter", "8", "-netQuitAfter", "10"
)
```

서버가 Host를 `Police`, 첫 원격 Client를 `Thief`로 배정합니다. 두 역할은 서로
다른 시작점과 색상을 사용하며 세 번째 접속은 거절됩니다. 2026-07-24 실제
Windows 실행에서 양쪽 모두 경찰 1명·도둑 1명, 역할 중복 없음, 로컬 역할 인식과
역할별 시작점 분리를 확인해 `passed: true`였습니다.

### MAP-001 회색 상자 마을

`Game` 씬은 56×44m 순환형 회색 상자 마을입니다. 경찰·도둑 시작점,
슈퍼마켓, 서점, 보석상, 너구리 거래장터, 중앙 광장과 함께 골목, 지붕,
사다리, 쓰레기통 위치를 기본 도형으로 표시합니다. 고정 암시장이나 상인이
아니라 이후 출현 규칙을 검증할 수 있는 거래장터 앵커만 둡니다.

Unity 메뉴에서 맵을 다시 만들거나 검증할 수 있습니다.

```text
Paws & Loot > Setup > Rebuild MAP-001 Greybox Village
Paws & Loot > Setup > Validate MAP-001 Greybox Village
Paws & Loot > Technical Validation > Build Windows MAP-001
```

Windows 개발 빌드:

```text
Builds/TechnicalValidation/Windows/PawsAndLootMapGreybox.exe
```

`-mapAutoQuit` 인자로 실행하면 캐릭터 크기의 캡슐이 경찰 시작점에서 도둑
시작점까지 자동 이동하고 결과를 `map-001-result.json`과
`map-001-screenshot.png`에 기록합니다. 2026-07-24 실제 빌드는 72m 경로를
17.58초에 횡단했고 끼임 0회로 통과했습니다.

일반 실행에서는 MAP 자동 횡단 프로브가 비활성화되고 파란 경찰을 `WASD`로
조작할 수 있습니다. 이동은 경기 상태가 `PLAYING`일 때만 적용됩니다.

도둑 이동을 확인할 때는 같은 실행 파일에 역할 인자를 전달합니다.

```powershell
& ".\Builds\TechnicalValidation\Windows\PawsAndLootMapGreybox.exe" `
  -playerRole Thief
```

경찰과 도둑은 동일한 이동·충돌 코드를 사용하며 선택된 한 역할만 로컬 입력과
카메라를 받습니다.

공통 조작:

```text
WASD  이동
Space 대시
E     가장 가까운 유효 대상과 상호작용
```

대시는 벽을 통과하지 않으며 경기 중이고 쿨타임이 끝났을 때만 시작됩니다.
상호작용 안내는 화면 아래에 표시되며, 경찰과 도둑의 권한 및 현재 경기 상태를
모두 통과한 대상만 선택됩니다.
