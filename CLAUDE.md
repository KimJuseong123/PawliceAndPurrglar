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

> **맵에 무언가를 놓았으면 그 자리가 비어 있는지 재본다.** 눈으로 좌표를 찍어
> 놓은 돌 5개 중 4개가 건물 안에 박혀 있었다. 씬은 빌드되고 `Validate MAP-001`은
> 통과하고 로그는 "5 rock pickups placed"라고 찍는다. 플레이해 보기 전까지 아무도
> 모른다. `CheckSpotIsClear`가 이제 겹침을 재고 `NoPickupIsSealedInsideGeometry`가
> 테스트로 막는다. 도로 격자(가로 z = 12/-12/-18/26/39, 세로 x = -24/-18/0/18/24/40)
> 교차점은 구조상 빈 땅이다.

> **`Physics.Raycast`는 기본적으로 트리거를 맞힌다.** 이 맵은 트리거 구체로
> 가득하다 — 줍는 지점, 은닉처, 판매처, 사다리가 전부 트리거다. 그래서 던진 돌이
> 가장 가까운 픽업에서 멈췄고 화면에는 아무 설명도 없었다. 시야·투척처럼 "가로막는
> 것"을 묻는 질문에는 `QueryTriggerInteraction.Ignore`를 명시한다. 캐릭터도
> 빼야 한다 — 플레이어와 동물 모두 `CharacterController`로 움직이고, 동물은 발밑에
> 붙어 다니므로 엄폐물로 취급하면 아무것도 던질 수 없다.

> **Play Mode 테스트에서 `Object.Destroy`는 프레임 끝에야 적용된다.** 한 테스트가
> 남긴 플레이어나 벽이 다음 테스트에서 그대로 살아 있어서, 옆 테스트의 도둑을
> 상대로 판정이 실패한다. 이건 리졸버 버그처럼 보이지만 아니다. 만든 것을
> 추적해서 `[TearDown]`에서 `DestroyImmediate`한다. 그리고 같은 프레임에 만든
> 콜라이더는 물리 씬에 아직 없으므로 `Physics.SyncTransforms()`를 부른다.

> **테스트가 "전부 통과"해도 방금 쓴 코드를 돌린 것이 아닐 수 있다.** 한 어셈블리가
> 컴파일에 실패하면 Unity는 **이전에 컴파일된 어셈블리로 테스트를 돌리고**, 종료 코드
> 0에 "172개 전부 통과"를 찍는다. 실제로는 지운 지 오래인 테스트가 돌고 있었다.
> 결과 XML에서 **이번에 추가·개명한 테스트 이름을 실제로 찾아본다** — 없으면 낡은
> 결과다. 로그의 `error CS` 개수도 함께 센다. 어셈블리가 나뉘어 있어서 EditMode는
> 멀쩡히 돌고 PlayMode만 깨져 있을 수 있다.

> **에디터 스크립트가 채운 `List`는 씬 저장에서 사라진다.** `onClick.AddListener`와
> 같은 종류인데 컴포넌트 목록에서도 일어난다. 센서 신호 호 7개를 `AddBar`로
> 넘겼더니 빌드에서 목록이 비어 있어서 칸수 계산이 아무 일도 하지 않았다
> (`ISSUE-031`). 이 함정으로 플레이테스트를 세 번 낭비했다 — 로비 버튼, 다리
> 애니메이터, 그리고 이것. **프레젠터가 런타임에 스스로 찾게 한다.**

> **애니메이션 문제는 호스트와 클라이언트 양쪽에서 재본다.** 클라이언트에서 두
> 캐릭터 모두 다리가 멈춰 있었는데 (호스트 24°, 클라이언트 2.5°) 편집기 씬
> 테스트는 세션이 없어서 계속 통과했다. 복제된 위치는 `MoveTowards`로 목표에
> 도착한 뒤 다음 패킷까지 멈춰 있으므로, **프레임 이동량으로 잰 속도는 대부분
> 0이다.** 복제로 움직이는 것의 속도는 재지 말고 호스트가 보내는 값을 받는다
> (`CompanionLegAnimator.SetExternalSpeed`). `NetworkMatchProbe`가 역할별 허벅지
> 스윙을 기록한다.

> **손으로 만든 메시는 감기 방향을 재본다.** 기절 별 4개가 만든 순간부터 한 번도
> 그려지지 않았다. 삼각형이 반대로 감겨서 백페이스 컬링이 매 프레임 전부 버렸고,
> 그 외 모든 것은 정상이었다 — 활성, 렌더러 켜짐, 노란 머티리얼, 올바른 높이,
> 절두체 안. `IsShowing` 같은 컴포넌트 자기 의견을 검사하는 테스트는 계속
> 통과한다. 앞면 법선과 보는 방향의 내적을 재야 한다
> (`StunStarsVisibilityPlayModeTests` 참고). 평면과 보는 방향이 함께 감기를
> 정하므로 한 곳에서 맞춘 규칙을 다른 곳에 그대로 쓸 수 없다 — 같은 코드로 만든
> 손전등 부채꼴(XZ 평면, 위에서 봄)은 정상이었고 별(XY 평면, 카메라로 돌림)만
> 반대였다.

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


> **임포트한 모델이 씬에서 흰색으로 나오면 머티리얼 리맵이 비어 있는 것이다.**
> FBX는 임포터의 머티리얼 리맵이 채워져야 자기 텍스처를 쓴다. 비어 있어도 **예외도
> 경고도 없고** 씬은 저장되고 검증기는 통과한다 — 흰 덩어리 하나가 늘 뿐이다.
> `Rebuild Map Sandbox`가 이제 `Repair Model Textures`를 먼저 부르지만, 다른 씬을
> 만들 때는 손으로 돌린다 (`ISSUE-058`).
>
> 헷갈리는 점: **`Capture Model Sheet`은 멀쩡하게 나온다.** 컨택트 시트는 모델을
> 그때그때 인스턴스화하고 씬은 저장된 참조를 들고 있어서, 같은 모델이 도구마다
> 다르게 보인다. 씬이 틀렸다는 뜻이지 모델이 틀렸다는 뜻이 아니다.

> **평면도로 색과 재질을 판단하지 않는다.** 평면도는 에디터 프로세스가 에디터 조명으로
> 80m 상공에서 찍는다. "무엇이 어디에 있는가"에는 정확하고 "어떻게 보이는가"에는
> 아니다 — 같은 오후에 두 번 잘못된 결론을 만들었다 (`ISSUE-060`). 창을 밖에서
> 긁는 것도 안 된다. 앞에 있던 다른 창을 두 번 찍었다.
>
> 빌드가 스스로 찍게 한다.
>
> ```bash
> "Builds/Sandbox/Windows/MapSandbox.exe" -sandboxShot Logs/sandbox-ingame.png -shotSeconds 6
> ```

> **바닥에 까는 타일을 구울 때 조명을 켜지 않는다.** 구워 넣고 싶은 것은 그 땅의
> 색이지 조명에 대한 두 번째 의견이 아니다. 정면 직사광은 위를 보는 면을 전부
> N·L = 1에, 즉 밝기 곡선의 끝에 앉힌다. 감면이 남긴 법선의 미세한 흔들림이 그
> 절벽에서 떨어져 아스팔트가 흰 잡음으로 터졌다 — **연석과 차선은 완벽했다.**
> 옆을 보는 면과 별도 지오메트리라서다. 그래서 텍스처 문제처럼 보였다 (`ISSUE-059`).
>
> 타일이 이상하게 나오면 `Logs/baked-raw/`의 보정 전 렌더를 먼저 본다. **렌더가
> 틀렸는지 보정이 틀렸는지**는 고치는 곳이 전혀 다르다.

> **실내 모델의 문이 어느 벽에 있는지는 평면도를 찍어서 정한다.** 게임 스크린샷으로
> 세 번 정했고 세 번 다 틀렸다 — 실내 카메라는 궤도 카메라라 스크린샷 한 장에서
> 북쪽과 동쪽을 구분할 방법이 없다. `Capture Interior Plans`는 위에서 방위를 고정해
> 찍으므로 그림과 상수가 같은 좌표에 있다. 화단과 현관 슬래브가 정면 벽 바깥에 있다.
>
> 넓이 탐침만 믿지 않는다. **구멍이 항상 문은 아니다** — 유리 상점 정면은 탐침이 보는
> 띠를 채우고, 간판 레일이나 낮은 선반 줄은 그 띠를 비운다. `DoorWallForModel`에
> 적어두는 편이 낫다. 모델은 다섯 개이고, 평면도를 보는 사람은 1초에 답한다.

> **오브젝트의 원점이 그 오브젝트의 중심이라고 가정하지 않는다.** 집 모델은 배치될 때
> 실루엣 전체로 재중심되고 그 실루엣에는 앞으로 튀어나온 현관이 들어간다. 그래서 벽이
> 원점보다 1.09m 뒤에 있고, 원점 기준으로 면을 판정하니 **앞면 전체가 방 안쪽으로 분류돼**
> 투과되지 않았다. 뒷면은 완벽했으므로 문 문제처럼 보였다 (`ISSUE-045`).
>
> 그리고 분류를 하면 **"어디에도 속하지 않은 것"의 수를 단정한다.** 면마다 개수가 충분한지만
> 보면 앞면이 25개로 통과한다 — 원래 82개여야 하는데.

> **물리 콜백 안에서 `CharacterController`를 옮기지 않는다.** `OnTriggerEnter`는
> `Move` 안에서 오고, 컨트롤러는 그 호출 끝에 자기가 계산한 위치를 덮어쓴다. 순간이동이
> 조용히 되돌려져서 플레이어가 나가려던 문턱에 남고 21m를 떨어졌다 (`ISSUE-044`).
> `LateUpdate`로 미룬다.
>
> **그리고 맵 위의 고정 오프셋 지점은 땅이 있는지 재본다.** 나가는 지점을 집 앞 5.4m로
> 잡았더니 북쪽 끝 줄 집에서는 그게 경계벽 안이었다. 아래로 레이캐스트해서 **같은 높이의**
> 땅이 나올 때까지 당긴다 — 2m 벽 위에 서는 것도 걸러야 한다.

> **트리거를 넣었으면 그 안에 다른 지점이 들어가 있지 않은지 본다.** 나가는 문 트리거를
> 문틀보다 크게 만들었더니 입장 지점이 그 안에 있었다. 들어가는 순간 나가지고, 그
> 플레이어가 씬에 남아 **관계없는 테스트 9개(너구리·HUD·지붕)까지 오염시켰다**
> (`ISSUE-043`). Play Mode 실패가 여러 곳에서 한꺼번에 터지면 먼저 **한 클래스만 격리
> 실행**해서 오염인지 진짜 실패인지 가른다.
>
> 그리고 충돌로 발동하는 것은 키 입력과 권위 구조가 다르다. 키는 입력 브리지를 타고
> 호스트로 가지만 충돌은 모든 기계에서 동시에 일어난다 — 어느 기계가 결정하는지 명시적으로
> 물어야 한다 (`PlayerInteriorState.HasAuthority`).

> **덮어쓰는 자세를 추가하면 그 뼈를 재는 계측을 같이 고친다.** 점프 자세가 허벅지를
> 54° 돌리는데 걷기는 24°다. 프로브가 최대값을 재고 있었으므로, 걷기가 완전히 죽어도
> `policeThighSwing`이 54로 건강해 보인다 (`ISSUE-042`). 접지 여부로 거르면 부족하다 —
> 착지 후에도 자세가 몇 프레임 남는다. **뼈를 실제로 쓰는 쪽(애니메이터 블렌드)에 묻는다.**
>
> 그리고 `IsAirborne` 같은 상태 플래그는 시뮬레이션을 멈출 때 **내려야** 한다.
> `Move`가 조기 반환하면서 낡은 `true`를 남겼고, 캐릭터가 별 모양 자세로 얼어붙었다.

> **`FindFirstObjectByType`으로 고른 것을 테스트나 프로브의 기준으로 쓰지 않는다.**
> 인스턴스 ID 순서는 맵의 성질이 아니다. 프로브가 "첫 보물" 옆에 도둑을 놓고 있었는데,
> 실내 19개를 씬에 추가하자 ID 순서가 바뀌어 다른 보물이 먼저 나왔고 회귀가 깨졌다
> (`ISSUE-041`). 이름 순서처럼 결정적인 기준으로 고른다.

> **캐릭터를 텔레포트할 때 피벗이 아니라 발을 목표면에 맞춘다.** 문이 플레이어를
> 바닥면 + 0.06m에 놓았는데, transform은 발바닥보다 0.94m 위에 있어서 캡슐 전체가
> 바닥 슬래브 안에 파묻혔다. `CharacterController`는 1m짜리 관통을 아래로 밀어내
> 해소하고, 플레이어는 들어가는 즉시 떨어진다 (`ISSUE-040`). 오프셋은
> `controller.height/2 - center.y`로 캡슐에서 읽는다 — 두 캐릭터가 다른 모델이다.
>
> **그리고 바닥 관통 테스트는 캡슐 밑면(`controller.bounds.min.y`)을 본다.** 피벗 y로
> 재면 통과한다 — 피벗은 0.06에 남아 있고 발만 −0.94로 내려가 있었다. 도착 위치를
> 확인하는 테스트도 `Vector3.Distance`가 아니라 **수평 거리**로 재야 한다. 수직
> 오프셋은 의도된 것이다.

> **프리팹을 풀면 씬이 폭발한다.** 문짝을 경첩에 모으려고 집 8채를
> `UnpackPrefabInstance`로 풀었더니 `Game.unity`가 3.17MB → 9.16MB, GameObject
> 316개 → 3,134개가 됐다. 프리팹 인스턴스는 참조 하나로 기록되는데, 풀면 부품
> 186개가 전부 실체로 적힌다. 씬은 통째로 재생성되므로 **재생성마다 `.git`에 9MB가
> 새로 쌓인다.** 화면에는 아무 차이도 없다 (`ISSUE-038`).
>
> 재부모화가 필요해 보이면 먼저 **좌표 계산으로 대체할 수 있는지** 본다.
> `HouseDoorLeaf`는 부품을 공유 피벗 기준으로 각각 회전시켜서 경첩과 같은 결과를
> 낸다. 계측은 프리팹 소속(`PrefabUtility.IsPartOfPrefabInstance`)으로 한다 — 씬을
> 열면 프리팹이 인스턴스화되므로 부품이 계층에 존재하는 것은 정상이다.

> **회전한 오브젝트의 기준점을 월드 축으로 잡지 않는다.** 문 경첩을
> `bounds.min.x`로 잡았더니 180° 돌린 집에서는 그게 문짝의 반대쪽 끝이라 경첩이
> 반대편에 생기고 문이 안쪽으로 열렸다. 문은 열리므로 로그도 테스트도 아무 말을
> 하지 않는다 (`ISSUE-039`). 기준점·오프셋·회전축을 전부 그 오브젝트의 로컬 공간에서
> 계산하고, **돌린 사례로 테스트한다** — 항등 회전만 시험하면 셋 다 통과한다.

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
Capture Sandbox Overview          샌드박스 상공 평면도 → Logs/sandbox-overview.png
                                  + 배치 목록 Logs/sandbox-placements.txt
                                  좌표 격자를 얹으려면
                                  python Tools/annotate_sandbox_plan.py
                                  **좌표 확인용이다. 색 판단에 쓰지 않는다**
Repair Model Textures             임포터 머티리얼 리맵을 채운다 (흰 모델 고침)
Bake Road Tile Textures           바닥 타일을 위에서 Unlit으로 굽는다
                                  보정 전 렌더는 Logs/baked-raw/
Report House Model Layout         집 모델 부품·치수 보고 (실내를 손대기 전에 먼저 잰다)
Report Interior Faces             실내 네 면의 부품 배정과 **어디에도 안 속한 것** 보고
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

현재 기준선: Edit Mode 215개, Play Mode 183개 (2026-08-03).
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
"Builds/Playtest/Windows/PawsAndLoot.exe" -batchmode -nographics -netLobby host   -netScenario full -netMatchSeconds 60
"Builds/Playtest/Windows/PawsAndLoot.exe" -batchmode -nographics -netLobby client -netScenario full -netMatchSeconds 60
```

`-netScenario` 3종:

| 값 | 검증 대상 | 권장 `-netMatchSeconds` |
|---|---|---|
| `full` | NET-005·006·007. 획득 → 판매 연타 → **체포 3회** → 승자 비교 | 60 |
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

승리에 체포가 3회 필요하고 그 사이에 감옥 11초가 있으므로 `full`은 **60초**가
필요하다. 16초는 체포 1회 시절의 값이고, 그대로 두면 승자가 나오지 않아
`passed=false`가 고정된다 — 회귀가 실패하는 것이 아니라 **꺼진다.**

경찰은 도둑이 감옥에 있는 동안에는 배치하지 않는다. 감옥 안의 도둑 위에 서 있으면
출소하는 순간 즉시 잡히는데, 그건 게임이 하는 일이 아니다. 결과의
`peakArrestCount`와 `jailSpells`가 3회까지 갔는지 보여준다 — 없으면 실패가
"승자 없음"까지만 말하고 체포가 깨진 것인지 감옥이 안 풀린 것인지 구분되지 않는다.

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

## 8. 모델을 새로 넣을 때

**삼각형이 많으면 넣기 전에 Blender에서 감면한다.** 넣고 나서 정리하는 것이 아니라
임포트 절차의 일부다.

이 프로젝트의 모델은 거의 전부 Tripo 생성물이고 **약 95만 삼각형**으로 나온다. 손으로
만든 3~5만짜리와 화면에서 구분되지 않는데 빌드 용량과 프레임에는 그대로 실린다. 표정
아이콘 넷이 385만 삼각형을 차지하고 있던 것을 뒤늦게 발견했고, 그때는 이미 본 게임 씬에
들어가 있었다. WebGL 제출이 목표이므로 용량이 곧 첫 로딩 시간이다.

```bash
blender --background --python Tools/decimate_fbx.py -- <in.fbx> <out.fbx> <목표 삼각형>
```

| 종류 | 목표 삼각형 |
|---|---:|
| 아이콘 | 2,000 |
| 소품·가로등·나무 | 5,000 |
| 건물 외관 | 40,000 |
| 실내 | 100,000 |

실내는 플레이어가 가장 가까이서 오래 보므로 덜 깎는다.

> **바꿔 넣기 전에 옆에서 찍어 확인한다.** 텍스처는 벽에 있으므로 위에서만 보면 UV가
> 찢어진 것을 놓친다. `Capture Model Sheet`의 카메라 각도를 잠시 눕히면 된다.
>
> **교체는 제자리 덮어쓰기다.** 새 파일로 넣으면 GUID가 새로 생기고 씬의 모든 참조가
> 끊긴다.

**예외: 위에서만 보이고 반복해서 깔리는 것은 감면하지 않고 굽는다.** 도로와 잔디가
그렇다. `Bake Road Tile Textures`가 위에서 찍은 512px 그림을 삼각형 2개짜리 평면에
입히므로, 원본이 95만이어도 빌드에 들어가지 않는다. 모델을 고치면 다시 구워야 한다.

측정은 `Report Model Weights`. 파일 크기로 짐작하지 않는다. 현황은
`docs/20_DECIMATION_LIST.md`.

## 9. 프로토타입 단계 대체 수단

- 동물 명령: 숫자키 `1`~`4`. 실제 STT·자연어 분류·LLM 호출은 구현하지 않는다.
- 3D 모델: 그레이박스 도형. 최종 모델은 별도로 제작 중이며
  `PlayerVisualRoot.ReplaceVisual`이 교체 지점이다.
- 이 두 가지를 "AI 음성 기능 완료" 또는 "최종 아트 적용"으로 표현하지 않는다.

## 10. 작업 완료 시 갱신할 문서

한 작업을 끝내면 다음을 같은 변경에 포함한다.

| 문서 | 갱신 내용 |
|---|---|
| `docs/13_CURRENT_STATE.md` | 현재 작업 상태, 바로 다음 작업, `최근 검증` 표 |
| `docs/09_TASK_BACKLOG.md` | 작업 상태 `TODO → DONE` |
| `CHANGELOG.md` | `Unreleased`의 Added/Changed |
| `docs/14_DECISION_LOG.md` | 설계 결정을 새로 했을 때만 |
| `docs/15_KNOWN_ISSUES.md` | 문제를 발견·해결했을 때만 |
| `docs/03_GAME_RULES.md` | 밸런스 수치를 바꿨을 때 (코드만 바꾸지 않는다) |

## 11. 보고 형식

작업 완료 보고는 `AGENTS.md` 10절의 6개 항목을 사용한다.

```text
변경 요약 / 변경 파일 / 검증 / Unity 에디터 작업 / 남은 위험 / 문서 변경
```

정적 검사와 Unity 런타임 검증을 구분해서 적는다. 실행하지 않은 테스트를
통과했다고 쓰지 않는다.
