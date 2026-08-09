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
| 저장소 루트 | `C:\Users\SSAFY\pawlice-and-purrglar` (Unity 프로젝트 루트와 동일) |
| 에디터 스크립트 네임스페이스 | `PawsAndLoot.Editor`. `-executeMethod`에 이 이름을 쓴다 |
| 런타임 어셈블리 | `PawsAndLoot.Runtime` (루트 네임스페이스 `PawsAndLoot`) |
| 현재 코드 위치 | `Assets/_Project/` |
| 외부 유료 에셋 | **없다.** 2026-08-08에 TopDownEngine 잔재와 `Assets/CatCops/` 레거시를 전부 제거했다. `Assets/ThirdParty/`는 비어 있다 |
| 빌드 씬 | `Bootstrap`, `Game`, `Result` 3개만 등록됨 |

제목은 **멍경찰과 냥도둑 / PawliceAndPurrglar**로 확정이다 (2026-08-08).
표시되는 곳은 전부 바꿨다 — 제품명, 에디터 메뉴 루트, 문서.

**바꾸지 않은 것은 식별자다**: 네임스페이스 `PawsAndLoot.*`, 어셈블리
`PawsAndLoot.Runtime`, 저장소 폴더 `pawlice-and-purrglar`, 빌드 산출물
`PawsAndLoot.exe`. 제목이 아니라 이름이고, 제출 직전에 어셈블리 참조와 클론
URL을 함께 움직일 이유가 없다. `-executeMethod`에는 여전히
`PawsAndLoot.Editor.*`를 쓴다.

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

> **`Preserve Aspect`는 rect 안에서 가운데 정렬한다.** rect가 그림보다 넓으면
> 그림이 화면 중앙 쪽으로 밀린다. 팀 아트 rect를 팀 그룹 폭으로 잡았더니 넓은
> 하이파이브 포즈가 안쪽으로 밀려 방 목록과 겹쳤다. **rect를 그 자리에 올 수 있는
> 가장 넓은 스프라이트에 맞추고** 원하는 쪽 가장자리에 앵커한다. 그리고 rect가
> 스프라이트보다 좁으면 폭이 제한 변수가 되어 **포즈를 바꿀 때 캐릭터가 작아진다.**

> **작업자가 준 PNG에 알파가 있다고 가정하지 않는다.** 네 장 중 하나가 24bpp
> RGB로 와서 로비에 검은 사각형으로 떴다. 그리고 네 장의 프레이밍이 서로 달라
> 고정 높이로 배치하니 역할을 고를 때 캐릭터가 줄어들었다. `Import Lobby Pair Art`가
> 알파 유무를 검사하고 넷 다 불투명 경계로 잘라 맞춘다.
>
> 배경을 키잉할 때 **밝기 하나로 자르지 않는다.** 그 파일의 배경은 값 23인데
> 아트의 신발·외곽선은 값 3으로 **배경보다 더 어두웠다.** "이보다 어두우면 배경"
> 규칙은 도둑의 다리를 지우고 배경은 남겼다. 배경은 **띠**로 잡는다.

> **`NetworkManager.Singleton`이 로비가 쓰는 매니저와 같다고 가정하지 않는다.**
> NGO는 세션 시작과 무관하게 **활성화 시점에** `DontDestroyOnLoad`를 부르고
> `Singleton`은 비어 있을 때만 잡는다. 씬에 배치된 매니저는 **씬을 다시 로드할
> 때마다 하나씩 늘어나고**, 낡은 쪽이 계속 싱글턴이다. `NetworkObject`는 소유
> 매니저가 없으면 싱글턴으로 폴백하므로, 새 로비가 스폰하는 모든 것이 리스닝하지
> 않는 낡은 매니저로 간다 — `NetworkManagerOwner is not listening` (`ISSUE-052`).
> 첫 판은 정상이고 두 번째 판만 죽으므로 로비 코드를 의심하게 된다.
>
> 스폰은 `NetworkObject.InstantiateAndSpawn(networkManager)`로 매니저를 명시한다
> (`NetworkManagerOwner` 필드는 `internal`이라 직접 못 넣는다). 그리고 씬을 나갈
> 때 세션을 끝낸다 — 씬 로더만 부르면 세션이 살아서 따라온다.

> **배치 실행의 로그는 종료 통보를 받은 뒤에만 판독한다.** 실행 중에
> `grep -c "error CS"`를 하면 0이 나오고 그게 "통과"로 읽힌다. 실제로는 오류가
> 있었고, 두 번 속았다 (`ISSUE-051`).

> **배치 실행 결과가 코드 변경을 반영하지 않으면 로그에서 `error CS`부터 찾는다.**
> 어느 한 어셈블리라도 컴파일에 실패하면 Unity는 **직전에 성공한 어셈블리로
> `-executeMethod`를 그대로 실행한다.** 깨진 것은 테스트 어셈블리였는데 증상은
> 에디터 도구에 나타났고, 도구는 멀쩡한 성공 로그를 남겼다. 추출기 좌표를 두 번
> 고치고 두 번 다 산출물이 그대로여서 좌표를 의심했다 (`ISSUE-051`).
> **성공 로그는 낡은 코드가 남긴 것일 수 있다.**

> **라벨을 "일단 만들어 두고 숨기지" 않는다.** 결과 화면의 승자·사유·골드·시간
> 라벨이 `fontSize 1` + 완전 투명 + `SetActive(false)`로 만들어져 있었다. 컴포넌트는
> 존재하므로 "라벨이 있는가"를 보는 검사는 전부 통과하고, 화면의 숫자는 목업 그림에
> 박힌 고정값이었다 — **어떤 경기를 해도 같은 결과가 나왔다** (`ISSUE-050`).
> 활성 여부·글자 크기·알파를 함께 단정한다 (`NoLabelIsHiddenOrUnreadable`).

> **완성된 화면 이미지를 UI 배경으로 깔지 않는다.** 로비가 목업 PNG 한 장을
> `preserveAspect`로 깔고 그 위에 **캡션이 빈 문자열이고 완전 투명인 버튼**을 픽셀
> 좌표로 고정한 것이었다. 목업의 4:3이 아닌 화면비에서는 그림만 레터박스로 줄고
> 버튼은 제자리에 남아 서로 어긋난다. 버튼이 죽은 게 아니라 **다른 자리에 있었다**
> (`ISSUE-046`). 에디터에서도 빌드에서도 정상으로 보이고 로그도 남지 않는다.
> 창 크기를 바꿔야만 드러나므로 `Capture Lobby Layout`으로 네 해상도를 찍어 본다.
>
> 좌우의 검은 여백은 로비 밑에 깔린 `Scene UI` 캔버스였다. 캔버스를 하나 더 얹기
> 전에 **밑에 무엇이 있는지** 본다.

> **TMP는 사각형이 한 줄보다 낮으면 `Ellipsis`에서 아무것도 그리지 않는다.**
> 말줄임을 하는 게 아니라 통째로 사라진다. 22pt 캡션에 24px rect, 28pt 상태 문구에
> 40px rect가 그랬다 — 몇 픽셀 차이다 (`ISSUE-047`). 같은 40px에 `Overflow`인 옆
> 라벨은 멀쩡히 보여서 레이아웃 문제로 보인다. **텍스트 rect는 글자 크기의 1.45배
> 이상**으로 잡고, 검사는 "라벨이 있는가"가 아니라 `preferredHeight <= rect.height`로
> 한다.

> **내용에 따라 자라는 요소를 고정 배치 옆에 두지 않는다.** LAN 방 목록을 하단
> 컨트롤의 `VerticalLayoutGroup`에 넣었더니 방이 하나만 발견돼도 주소 패널이 83px
> 올라가 캐릭터를 침범했다 (`ISSUE-048`). **가장 흔한 상태(비어 있음)로만 렌더해
> 보면 통과한다.** 최악의 경우 크기로 검사하고, 자라는 것은 빈 공간으로 띄운다.

> **맵 배치를 바꿨으면 `Capture Map Overview`로 평면도를 본다.** 건물이 도로
> 위에 얹혀 있거나 벽 밖으로 나가 있어도 테스트·검증기·플레이 카메라 중 어느
> 것도 잡지 못한다. 실제로 주택 2채가 골목을 막고 경찰서가 벽을 3m 뚫고 나간
> 채로 오래 남아 있었고, 평면도를 찍고 나서야 발견했다.
>
> 이 도구는 그래픽 모드 배치로 도는데 그 부작용으로 `QualitySettings`와
> `GraphicsSettings`가 더러워진다. 도구가 원복하지만, 실행 후
> `git status -- ProjectSettings/`가 비어 있는지 확인한다.

> **캐릭터 클립은 0개다. 이걸 전제로 확인한다.** TopDownEngine 의존은 2026-08-08에
> 제거했다 — 그 에셋은 라이선스가 재배포를 금지해 저장소에 없었고 **이 PC에도 설치돼
> 있지 않았다.** 즉 `CharacterLocomotion.controller`가 가리키던 클립 6개는 처음부터
> 하나도 해석되지 않았고, 커밋된 컨트롤러는 빈 구멍 여섯 개였다 (`ISSUE-019` 종결).
>
> 남은 것은 `AnimatorClipGuard` 하나다. 컨트롤러가 없거나 쓸 수 있는 클립이 0개면
> Animator를 끈다. **끄지 않으면 휴머노이드 리타게팅이 캐릭터를 주저앉힌다**
> (힙 0.45m → 0.07m). 이건 TDE와 무관한 규칙이므로 남겨 뒀다.
>
> 클립이 들어올 자리는 `Assets/_Project/Art/Characters/Animations/`이고
> `Rebuild Character Locomotion Animator`가 이름으로 찾아 컨트롤러를 만든다.
> 비어 있으면 컨트롤러를 만들지 않고 그 사실을 로그로 남긴다 (`MODEL-002`).
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


> **로컬 역할은 정적 값이라 테스트 사이로 샌다.** `LocalPlayerRoleSelector`의
> 역할은 씬 로드를 넘어 살아남으므로, 설정하고 정리하지 않으면 **뒤에 도는 다른
> 테스트가 그 역할로 돈다.** 실제로 이것이 너구리 5건과 경찰 HUD 1건을 실패시키고
> 있었고, `[TearDown]`에 `ClearOverriddenRole()`을 넣자 6건이 함께 통과했다
> (`ISSUE-054`). **여러 테스트가 한꺼번에 실패하면 먼저 오염을 의심한다** — 실패가
> 한 클래스에 몰려 있으면 그 클래스의 결함보다 공유 상태가 원인일 확률이 높다.
>
> 그리고 **역할을 단정하는 테스트는 역할을 스스로 설정한다.** 씬이 정한 값을 그대로
> 쓰면서 반대를 단정하면, 뒤집힌 실패가 코드 버그처럼 보인다. 실제로 결과 화면 관점
> 테스트 두 개가 그래서 정확히 반대로 실패했고, 코드는 처음부터 맞았다.

> **스프라이트 없는 `Image`는 "아무것도 없음"이 아니라 흰 사각형이다.** 결과 화면
> 제목을 비우려고 `sprite = null`만 했더니 화면 위쪽에 큰 흰 상자가 떴다. 게다가
> `SetSprite`가 `sprite != null`을 조건에 넣어 null을 조용히 무시하고 있어서, 비우는
> 호출 자체가 아무 일도 하지 않았다. **컴포넌트를 끈다** (`image.enabled = false`).
> `ISSUE-050`(라벨을 `fontSize 1` + 투명으로 "숨긴" 것)과 같은 계열이다.

> **목업 좌표를 원본 파일에서 재면 임포터가 그 크기를 유지하는지 확인한다.** Unity의
> `npotScale` 기본값이 `ToNearest`라서 1672×941 목업이 **2048×1024로 확대**되고,
> `AssetDatabase`로 읽는 커터에는 그 크기가 간다. 원본 기준으로 잰 창이 전혀 다른
> 자리를 자른다. `importer.npotScale = TextureImporterNPOTScale.None`으로 막는다.
>
> 이건 조용히 실패한다 — 잘라낸 그림에 픽셀이 남아 있으면 의도한 것처럼 보인다.
> 걸린 이유는 두 제목의 **바이트 크기가 10배 차이**났기 때문이다. 비슷해야 할 산출물
> 끼리 크기를 비교한다.
>
> 그리고 **컷아웃 창은 픽셀 단위로 맞추지 않고 이웃만 피하도록 넉넉하게 잡는다.**
> 커터가 내용에 맞춰 트림하므로 정확도는 커터가 담당한다. 행별 잉크 비율로 글자
> 경계를 재면 **맨 아랫줄 얇은 획을 놓친다.**

> **임포트 모델로 갈아탈 때 그 모델의 파트 이름에 의존하는 코드를 함께 옮긴다.**
> `HouseInteriorSetup`은 `IN_House1F_Wall*`이라는 파트를 찾아 반높이 칸막이를
> 세우는데, 새로 들어온 실내 모델은 그런 파트가 없는 단일 메시였다. 이름이 안
> 맞아도 **예외도 경고도 없다** — 세울 것을 못 찾았을 뿐이라 조용히 0개가 되고,
> 씬은 저장되고 생성기는 성공 로그를 남긴다 (`ISSUE-054`). 세운 개수를 찍고 0이면
> 실패로 만든다.

> **다른 브랜치를 병합했으면 병합 전에 그쪽 기준선을 실측한다.** `origin/main`을
> 받아 병합했더니 Play Mode 16건이 실패했는데, 그중 **13건은 main에 이미 있던
> 것**이었다 — 별도 worktree에 main을 꺼내 돌려 보고 나서야 제 몫이 3건임을
> 알았다. 기준선 없이 고치기 시작하면 무엇을 고쳤는지 말할 수 없다.
> 그리고 상대 브랜치의 `13_CURRENT_STATE.md` 수치를 믿지 않는다 — 그 기록 이후에
> 커밋이 더 있었고, 정작 커밋 메시지에 "실패 17개에서 12개로"라고 적혀 있었다.

> **`-testFilter`로 0개가 발견되면 종료 코드 0이 나온다.** 클래스 이름을 쉼표로
> 이어 넘겼더니 아무것도 안 돌고 통과처럼 보였다. 실행할 때마다 결과 XML의
> `total`을 본다.

> **배경 위에 무언가를 세웠으면 배경의 어느 지점에 서 있는지 잰다.** 밤 광장 판을
> 깔고 달을 지키려 위쪽만 조금 잘랐더니, 광장이 전부 화면 아래로 나가 두 팀이
> 하늘에 떠 있었다 (`ISSUE-053`). 요소마다 크기·위치·스프라이트가 다 맞았으므로
> **계약 테스트 전부가 통과했다** — 틀린 것은 두 요소 사이의 관계뿐이었다. 원화의
> 지평선·바닥 경계를 fraction으로 재고 발 위치와의 관계를 테스트로 고정한다
> (`CharactersStandOnTheSquareAndNotInTheSky`).
>
> 그리고 **화면비가 다른 판을 덮어 배치할 때 무엇을 버릴지 먼저 산수로 정한다.**
> 16:9는 4:3의 75%만 보여주므로, 원화의 상단 특징(달 0.072)과 하단 특징(웅덩이
> 0.72)을 동시에 담을 수 있는지는 계산으로 결정된다. 이 판은 담을 수 없었다.
>
> **레이아웃 그룹 자식에 배경을 깔지 않는다.** 그룹이 자식의 앵커를 덮어쓰므로,
> 채우기로 만든 띠가 배경이 아니라 **행의 한 칸**으로 배치된다. 예외도 로그도 없다.

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

> **빌드에서 프로젝트 루트를 `Application.dataPath`의 부모로 잡지 않는다.** 에디터에서는
> 그게 프로젝트 루트지만 **빌드에서는 exe가 있는 폴더**다. `LocalAI/`를 그렇게 찾다가
> Windows 빌드의 음성이 통째로 죽어 있었다 — 게이트웨이 실행 파일이 존재한 적이 없으므로
> 포트 16개를 훑고 `GATEWAY_NOT_READY`로 끝나고, 흔적은 시작 시 `LogWarning` 한 줄이다.
> 그래서 증상이 마이크 문제로 보인다 (`ISSUE-069`). 위로 올라가며 표식 파일을 찾고,
> **찾은 경로를 한 번 로그로 남긴다.** 모델이 1GB를 넘으면 빌드마다 복사하지 않는다.

> **`/health`가 `ready`라고 해서 그 장치로 실제 작업이 되는 것은 아니다.**
> `WhisperModel(device="cuda")`는 `cublas64_12.dll`이 없어도 **생성에 성공한다** —
> ctranslate2가 첫 encode에서야 DLL을 연다. 그래서 헬스체크는 `cuda, ready: true`를
> 보고하고 요청은 **100% 500**이었고, 적재 시점 폴백은 예외를 볼 기회가 없었다.
> 게이트웨이를 의심할 근거가 헬스체크뿐이면 통과로 읽힌다. 폴백은 **실제로 쓰는
> 지점**에도 둔다.

> **`FlashlightVisibility`는 *보는 쪽*에 붙어 있다.** 도둑을 드러내려면 **경찰의**
> 컴포넌트를 꺼야 한다. 이 규칙이 두 곳에 쓰여 있었고 한 곳이 거꾸로였다 —
> `LootAlarm`이 도둑에게 자기 `FlashlightVisibility`를 물었는데 도둑에게는 없으므로
> `?.`가 통째로 삼켰고, **경보의 4초 노출은 만들어진 날부터 한 번도 일어나지
> 않았다.** 사이렌과 경찰 질주는 정상이라 동작하는 경보로 보였다. 드러내는 것은
> `FlashlightVisibility.RevealRole` 하나로만 한다.
>
> 그리고 **드러남은 복제해야 한다.** 호스트에서 결정되고 노출되는 사람은 반대쪽
> 기계에 있다 — 호스트만 아는 노출은 아무도 모르는 노출이다.

> **매 프레임 `FindObjectsByType`을 부르지 않는다.** 경기 중 한 번 생기고 다시
> 만들어지지 않는 오브젝트를 보려고 초당 120번 씬을 훑는 코드는 WebGL에서 그대로
> 비용이 된다. 캐시하고, **못 찾았을 때만** 낮은 빈도로 다시 훑는다. 캐시를 채울 때
> **첫 일치에서 멈추지 않는다** — 절반만 채워진 잔여 오브젝트가 먼저 걸리면 루프가
> 거기서 끝나고, 그 뒤로는 아무것도 보고하지 않는다.

> **입력을 `FindObjectsByType`으로 전부 켜지 않는다.** `V`가 모든
> `VoiceCommandInput`을 시작해서 경찰용과 도둑용이 같은 프레임에 같은 마이크를 열었다.
> 윈도우는 장치를 한 클라이언트에게 주므로 한쪽은 **표본이 전부 0인 클립**을 받는다 —
> 무음이 간헐적으로 나오고, 한쪽의 `Microphone.End`가 다른 쪽 녹음을 끊는다. 역할마다
> 붙는 컴포넌트는 **자기가 이 기계의 역할인지 스스로 묻게 한다**
> (`CanCaptureLocally`, `CompanionCommandKeyboardInput.CanReadLocalInput`).

> **쿨타임을 결과보다 먼저 시작하지 않는다.** 음성 쿨타임이 말이 끝난 순간 걸려서,
> 실패한 시도가 키를 30초 잠갔다. 플레이어는 눌렀는데 아무 일이 없고, 다시 눌러도
> 아무 일이 없다. **성공만 비용을 쓴다.**

> **PyInstaller로 게이트웨이를 다시 굽기 전에 conda DLL 경로를 PATH에 넣는다.**
> `LocalAI/.venv`는 miniforge 위에 얹혀 있고 `_ctypes`는 `ffi-8.dll`을
> `<base>\Library\bin`에서 찾는다. PyInstaller는 PATH만 보고, 없으면 **경고로 넘기고
> 종료 코드 0**을 준다. 나온 exe는 첫 import에서 죽는다. `build-service.ps1`이 이제
> base prefix의 DLL 경로를 스스로 넣고 스테이징에 빌드한 뒤 `_ctypes.pyd`를 확인하고서야
> 교체한다 — 예전 스크립트는 PyInstaller를 부르기 **전에** 동작하는 게이트웨이를 지웠고,
> `LocalAI/runtime/`은 gitignore라 되돌릴 것이 없었다.

> **두 기계는 반드시 같은 빌드여야 한다.** `Game.unity`를 재생성하면 in-scene
> `NetworkObject` **130개의 `GlobalObjectIdHash`가 전부 바뀐다**. 다른 커밋으로 만든
> 빌드끼리 붙이면 NGO가 오브젝트마다 이렇게 찍는다:
>
> ```text
> [Netcode] NetworkPrefab hash was not found! In-Scene placed NetworkObject
>           soft synchronization failure for Hash: 2322046117!
> [Netcode] [GlobalObjectIdHash=...] Failed to spawn NetworkObject!
> NullReferenceException
> ```
>
> **어디에도 "빌드가 다르다"는 말이 없다.** 플레이어가 겪는 것은 "안 움직인다"인데,
> 실패한 오브젝트가 `Police Player`이고 거기에 이동 입력이 지나가는
> `NetworkPlayerLink`가 붙어 있어서다. 가방은 로컬 UI라 멀쩡히 동작하므로 **이동
> 버그로 읽힌다.** 아니다.
>
> `NetworkSceneFingerprint`가 접속 시 커밋을 비교해 한 줄로 말한다. 도장은
> `PlaytestBuild`가 빌드할 때 `PlayerSettings.bundleVersion`에 찍고 끝나면 되돌린다
> (저장소를 더럽히지 않는다). **에디터 실행이나 손으로 만든 빌드는 도장이 없어서
> 비교가 안 되고, 그때는 비교 불가라고 말한다.**
>
> **커밋이 같아도 빌드가 다를 수 있다 — 커밋은 빌드가 아니다.** 2026-08-09에 이걸로
> 한 번 당했다. 이 PC에는 재생성된 `Game.unity`가 **커밋되지 않은 채** 있었고
> (`GlobalObjectIdHash` 253줄 변경), 다른 컴퓨터는 같은 커밋을 클론해 옛 씬으로
> 빌드했다. 두 빌드 모두 `1.0+5e5c933`으로 찍혀 **지문이 "같은 빌드"라고 답했다** —
> 정작 그것 하나 잡으라고 만든 검사가 통과를 보고했다.
>
> 이제 `Assets`·`ProjectSettings`·`Packages`에 미커밋 변경이 있으면 도장에
> `-dirty`가 붙는다 (`1.0+5e5c933-dirty`). 범위를 그 셋으로 좁힌 이유는 문서나
> `server/`까지 세면 거의 모든 빌드가 dirty가 되어 **늘 켜진 경고가 되기 때문**이다.
>
> **씬을 재생성했으면 다른 기계에 빌드를 주기 전에 커밋한다.** `.gitignore`는
> 이 경우와 무관하다 — 실제로 검사해 보면 `Assets` 아래 무시되는 파일은 0건이고
> 신선한 클론은 같은 파일 수를 받는다.

> **배치 빌드는 성공해도 `.exe` 날짜가 안 바뀐다.** Mono 빌드라 게임 코드는
> `PawsAndLoot_Data/Managed/*.dll`에 있고 플레이어 실행 파일은 바뀔 이유가 없다.
> 일주일 전 날짜의 exe를 보고 "빌드가 안 됐다"고 판단하면 틀린다 — `Managed/`의
> 날짜와 로그의 `build succeeded`를 본다.

> **`-netScenario full`의 `passed`를 회귀 판정에 쓰지 않는다.** 2026-08-08 기준
> **아무것도 바꾸지 않은 트리에서도 양쪽 `passed=false`**가 나온다 — 판매 500,
> 체포 3회, 승자 합의까지 정상으로 치르고서다 (`GAP-006`). 무엇이 달라졌는지는
> **호스트·클라이언트 로그의 사건 수**로 센다 (`dealt N pieces`, `Arrest completed`,
> `-> Sold`). 그리고 재보기 전에 **변경을 stash하고 기준선을 실측한다** — 이걸
> 안 했으면 남의 실패를 내 것으로 고칠 뻔했다.

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

## 4. 에디터 메뉴 (`PawliceAndPurrglar`)

### Setup

```text
Rebuild Basic Scenes              Bootstrap/Game/Result 재생성
Validate Basic Scenes             씬 계약과 빌드 순서 검사
Ensure Bootstrap Services         Bootstrap 서비스 오브젝트 보장
Rebuild MAP-001 Greybox Village   Game 씬 마을 재생성
Validate MAP-001 Greybox Village  장소·경로·폭·충돌 검사
Capture Map Overview              Game 씬 상공 평면도 → Logs/map-overview.png
Rebuild Bootstrap Lobby           Bootstrap 로비 재생성 (프리팹 인스턴스 배치)

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

### UI

```text
Rebuild Lobby (Art, Prefab, Scene)  아래 셋을 순서대로 실행. 로비를 손댔으면 이것만 쓴다
Extract Lobby Art From Mockup       목업에서 로고·캐릭터 4종 컷아웃 → UI/Lobby/Elements/
Generate Lobby Chrome Sprites       버튼·패널·입력칸·발광·아이콘을 코드로 그려 굽는다
Rebuild Lobby Canvas Prefab         LobbyCanvas.prefab 재생성
Capture Lobby Layout                로비를 4개 해상도로 렌더 → Logs/lobby-*.png

Import Lobby Pair Art               ArtSource/Lobby의 팀 페어 PNG 4장 → UI/Lobby/Sprites/
                                    알파 없으면 배경 키잉, 넷 다 불투명 경계로 잘라 크기 통일
Rebuild Result (Art, Prefab, Scene) 결과 화면 전체 재생성. 결과를 손댔으면 이것만 쓴다
Extract Result Art From Mockups     승패 목업에서 타이틀·VS 밴드·아이콘 컷아웃
Rebuild Result Canvas Prefab        ResultCanvas.prefab 재생성
Capture Result Layout               결과를 4개 해상도로 렌더 → Logs/result-*.png

Create Role-Aware HUD Prefabs       HUD 프리팹과 던파 비트비트 TMP 폰트 에셋 생성
Sync HUD Canvas To Resources        HUD 프리팹을 Resources로 복사
```

UI 스프라이트는 순서 의존이다 — 컷아웃과 크롬이 없으면 프리팹 빌더가 파일
없음으로 멈춘다. `Rebuild ... (Art, Prefab, Scene)`가 그 순서를 보장한다.

컷아웃 코어는 `MockupCutter`, 빌더 헬퍼는 `UiBuildKit`에 공용으로 있다. 두 화면
중 하나만 고치면 다른 하나도 같이 재생성한다.

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
"C:/Program Files/Unity/Hub/Editor/6000.5.4f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Users/SSAFY/pawlice-and-purrglar" -runTests -testPlatform EditMode -testResults "C:/Users/SSAFY/pawlice-and-purrglar/Logs/TestResults/editmode.xml" -logFile "C:/Users/SSAFY/pawlice-and-purrglar/Logs/editmode-tests.log"
```

`-testPlatform PlayMode`로 바꿔 Play Mode도 실행한다. 확인 사항:

- 프로세스 종료 코드
- 결과 XML의 실제 테스트 수와 실패 목록
- **테스트 0개 발견은 성공이 아니다**

현재 기준선: **Edit Mode 299개 전부 통과, Play Mode 213개 중 211 통과 + 1 실패 +
1 스킵 (2026-08-08).** Play Mode의 실패 1건은
`CompanionExpressionPlayModeTests.ShowingAFacePutsExactlyOneIconOnScreen`이며
음성 스택이 아니라 **애니메이션 클립 부재**(`MODEL-002` 미완)에 딸린 것이다.
테스트를 추가하면 `13_CURRENT_STATE.md`의 `최근 검증` 표에 실제 수치를 기록한다.

### 런타임 검증 (자체 보고 프로브 패턴)

이 프로젝트는 빌드된 플레이어 자체가 인수 테스트 도구다. 각 프로브는 JSON
결과와 스크린샷을 쓰고 `Application.Quit()`한다.

```text
%USERPROFILE%\AppData\LocalLow\PawliceAndPurrglar\PawliceAndPurrglar\
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
