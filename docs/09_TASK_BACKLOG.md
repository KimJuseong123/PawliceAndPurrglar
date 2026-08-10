# 작업 백로그

## 상태

- `TODO`
- `READY`
- `IN_PROGRESS`
- `BLOCKED`
- `REVIEW`
- `DONE`

## 우선순위

- `P0`: 현재 단계 완료에 필수
- `P1`: 핵심 경험 향상
- `P2`: 다음 단계 준비
- `P3`: MVP 이후

## 현재 작업 원칙

- 한 번에 한 작업만 `IN_PROGRESS`로 둔다.
- 단계 A 프로토타입이 끝나기 전 음성과 최종 모델 작업을 시작하지 않는다.
- 작업 완료 시 `docs/13_CURRENT_STATE.md`를 갱신한다.
- 결정 변경 시 `docs/14_DECISION_LOG.md`를 갱신한다.

## Epic 0. 문서

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| DOC-001 | P0 | DONE | AGENTS와 README 작성 |
| DOC-002 | P0 | DONE | 프로젝트 문서 패키지 작성 |
| DOC-003 | P1 | TODO | 팀 리뷰 후 미확정 규칙 정리 |
| DOC-004 | P1 | DONE | **공모전 제출 요강을 받아 `docs/18` 0-c에 옮겼다 (2026-08-08).** 다섯 항목 중 하나라도 빠지면 심사 제외. 우선순위를 바꾼 것 둘: 영상이 **30~60초 실플레이 고정**(편집 영상 불가)이고, **AI 에셋 출처 고지가 제외 사유**라 `SUBMIT-004`·`005`가 뒤로 밀 수 없다. `.exe` 직접 제출은 보안상 불가 — WebGL 확정이 옳았다 |

## Epic 1. 프로젝트 기반

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| BASE-001 | P0 | DONE | 목표 저장소 폴더 구조 생성 |
| BASE-002 | P0 | DONE | Unity 버전과 패키지 고정 |
| BASE-003 | P0 | DONE | `Bootstrap`, `Game`, `Result` 기본 씬 구성 |
| BASE-004 | P0 | DONE | 공통 설정 구조 생성 |
| BASE-005 | P0 | DONE | 로깅과 오류 처리 |
| BASE-006 | P1 | DONE | Edit Mode 및 Play Mode 테스트 어셈블리 |

## Epic 1.5. 고위험 기술 검증

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| TECH-001 | P0 | DONE | Windows 목표 플랫폼 빌드와 키보드 이동 검증 |
| TECH-002 | P0 | DONE | Blender 모델, 리그, 애니메이션 연동 검증 |
| CLEAN-001 | P1 | DONE | **TopDown Engine 의존 전면 제거 (2026-08-08).** 실측: 폴더 없음·git 추적 0건·정의 미설정·컨트롤러 클립 GUID 6개 전부 미해결 — **한 번도 해석된 적 없는 참조**였다. 지운 것: `CharacterLocomotion.controller`, 그 GUID를 지키던 테스트, `AnimationClipSourceAudit`(466줄)+테스트, `Audit Placeholder Character Models`, `TryInstantiateCharacter`(MMExplodude 대체 경로), `Assets/CatCops/`(83파일, `_Project`에서 참조 0건), `.gitignore`·README·`CLAUDE.md` 안내. `AnimatorClipGuard`는 우리 방어 코드라 남겼다 |
| CLEAN-002 | P1 | DONE | **Resources 임포트 정리.** `Resources` 아래는 참조와 무관하게 전부 빌드에 들어간다. 손그림 HUD 아이콘 11종이 **1254×1254**(베이크 아이콘은 192×192)였다 → 256 상한·압축·밉맵 해제. 로비 BGM `DecompressOnLoad`+품질 1 → `CompressedInMemory`+0.5 |
| TECH-003 | P1 | 우회됨 | Windows 마이크는 여전히 막혀 있고, 브라우저 녹음(WebGL)으로 우회했다 |

## Epic 2. 카메라와 맵

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| CAMERA-001 | P0 | DONE | 3D 기울어진 탑다운 추적 카메라 (`TopDownFollowCamera`) |
| CAMERA-002 | P1 | TODO | 추격 속도 기반 줌 |
| CAMERA-003 | P1 | DONE | 실내 벽 가림 (`InteriorCutawayView`). **실외 가림은 미구현** |
| MAP-001 | P0 | DONE | 순환형 그레이박스 마을 |
| MAP-002 | P1 | TODO | 주요 장소별 복수 경로 |
| MAP-003 | P2 | DONE | 지붕·사다리 경로 (`LadderTraversal`, 씬 배치 9곳) |

## Epic 3. 경찰 리깅 스파이크

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| RIG-001 | P0 | READY | 기존 경찰 모델 축, 단위, 피벗 점검 |
| RIG-002 | P0 | TODO | 경찰 뼈 계층과 스킨 제작 |
| RIG-003 | P0 | TODO | Humanoid Avatar 검증 |
| ANIM-001 | P0 | TODO | `Idle` 테스트 |
| ANIM-002 | P0 | TODO | `Run` 테스트 |
| ANIM-003 | P0 | TODO | `ComedyRun` 테스트 |
| ANIM-004 | P1 | TODO | 코드 이동과 Animator 전환 |
| ANIM-005 | P2 | TODO | `Throw`, `Hit`, `Slip` 후보 |

## Epic 4. 경기 시스템

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| MATCH-001 | P0 | DONE | 경기 상태 정의 |
| MATCH-002 | P0 | DONE | 준비 카운트다운 |
| MATCH-003 | P0 | DONE | 4분 타이머 |
| MATCH-004 | P0 | DONE | 승리 판정 |
| MATCH-005 | P0 | DONE | 단일 경기 종료 |
| MATCH-006 | P0 | DONE | 결과 화면 |
| MATCH-007 | P0 | DONE | 재시작 초기화 |

## Epic 5. 플레이어

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| PLAYER-001 | P0 | DONE | 경찰 이동 |
| PLAYER-002 | P0 | DONE | 도둑 이동 |
| PLAYER-003 | P1 | DONE | 대시 |
| PLAYER-004 | P0 | DONE | 공통 상호작용 |
| PLAYER-005 | P0 | DONE | 역할 구분 |
| PLAYER-006 | P1 | DONE | 보물 소지 이동 페널티 |

## Epic 6. 보물과 너구리 상인

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| LOOT-001 | P0 | DONE | 보물 상태 정의 |
| LOOT-008 | P0 | DONE | 보물 가격 데이터 |
| LOOT-002 | P0 | DONE | 보물 획득 |
| LOOT-003 | P0 | DONE | 운반 |
| LOOT-004 | P1 | DONE | 드롭 |
| LOOT-005 | P2 | DONE | 보물 숨기기 (`LootHidingSpot`, 씬 배치 4곳) |
| LOOT-006 | P0 | DONE | 보물 판매 |
| LOOT-007 | P0 | DONE | 중복 획득·판매 방지 |
| ITEM-001 | P0 | DONE | **휴대 방식 4단계** — 주머니·한 손·양손·짐짝. 보물마다 이동 속도가 다르다 |
| ITEM-002 | P0 | DONE | **획득 시간** — 크기에 따라 0.4~2.4초. 움직이거나 기절하면 취소되고 진행도는 저장되지 않는다 |
| ITEM-003 | P1 | DONE | **고무닭** — 밟으면 22m 소음. 아무도 잡지 않고 주인이 없다 |
| ITEM-004 | P1 | DONE | **폭죽** — 2.5초 도화선 뒤 저절로 터진다. 설치자도 노출된다 |
| ITEM-005 | P1 | DONE | **소음 게시판** (`NoiseBoard`) — 소리 사건 하나를 양쪽 기계가 공유한다. 깨지는 병·금괴 낙하가 여기 올라간다 |
| ITEM-006 | P1 | DONE | **진열장** — 1.1초 깨기, 30m 소음. 봉인 중에는 내용물이 상호작용에서 빠진다 |
| ITEM-007 | P1 | DONE | **대왕 반지 경보** — 경찰 1.25배 12초, 도둑 4초 노출, 빈 진열대 표시. **2026-08-08: 4초 노출이 한 번도 일어난 적이 없었다** — `LootAlarm`이 경찰에게만 있는 컴포넌트를 도둑에게 물었고 `?.`가 삼켰다. 게다가 복제되지 않아 노출된 본인은 알 수도 없었다. 이제 `NetworkItemCoordinator.RevealRole`로 나가고 화면에도 뜬다 |
| ITEM-008 | P1 | DONE | **냉동 문어** — 던져 맞히면 2.2초 시야 차단. 기절도 몰수도 없다 |
| ITEM-009 | P1 | DONE | **낙하 소음** — 반경을 휴대 방식에서 끌어온다. 주머니는 소리가 없다 |
| MERCHANT-001 | P0 | DONE | 너구리 상인 판매 범위 (범위 진입 시 거래창) |
| MERCHANT-002 | P0 | DONE | 중복 판매 방지 (`LootRequestId` 경유, 판매는 호스트에서만) |
| MERCHANT-003 | P1 | DONE | 판매 피드백 (판매 목록·TOTAL·재화 배지 즉시 갱신) |
| MERCHANT-004 | P2 | TODO | 출현 위치 규칙 |

## Epic 7. 체포와 방해

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| ARREST-001 | P0 | DONE | 체포 범위 감지 |
| ARREST-002 | P0 | DONE | 체포 진행도 |
| ARREST-003 | P0 | DONE | 체포 중단 |
| ARREST-004 | P0 | DONE | 체포 완료 |
| ARREST-005 | P0 | DONE | 체포 UI |
| THROW-001 | P1 | DONE | 소품 줍기와 던지기 (판정·입력·HUD) |
| THROW-005 | P1 | PARTIAL | 돌 줍는 지점 5곳 **임시** 배치 (12초 후 재생성). 최종 배치는 맵 작업 때 |
| THROW-006 | P1 | DONE | 바나나·개껌을 상점 진열대에서 훔친다 (도둑 전용) |
| THROW-007 | P1 | DONE | 네트워크 라우팅 (호스트 기준) — 던지기 RPC, 기절 복제, 덫 명명 메시지 |
| THROW-008 | P2 | DONE | 던지기 연출: 팔 스윙 + 날아가는 돌 + 기절 별. 팔 축은 리그에서 측정 |
| THROW-002 | P1 | DONE | 투척 궤적과 착탄 (`ThrowFlightTracker`) |
| THROW-003 | P1 | DONE | 바나나 미끄러짐 (`PlacedTrap`, 설치자는 안 걸림) |
| THROW-004 | P2 | DONE | 참치캔·개껌으로 **상대 동물 유인** (`CompanionLure`) |
| THROW-009 | P1 | DONE | **경찰 설치물 2종** — 끈끈이(3초 고정), 센서등(2.5초 노출) |
| THROW-010 | P1 | DONE | 설치물 시각 표현 (`PlacedTrapView`). 그전까지 설치물은 전부 보이지 않았다 |
| THROW-014 | P1 | DONE | **당한 쪽 화면 표시 5종.** 배너는 `PlayerStatusBannerView` 하나이고 끈끈이·센서등·**경보 질주(경찰)**·**은신 중(도둑)** 넷을 우선순위대로 하나씩 말한다. 회전은 별도(`SlipSpinView`). 원문: 바나나 = 캐릭터가 한 바퀴 회전 (`SlipSpinView`, 별은 이제 `Impact`에만), 끈끈이·센서등 = 화면 가장자리 배너 + 남은 초 (`PlayerStatusBannerView`). 원인은 `StunCause`로 나누고 복제한다 — 복제하지 않으면 한쪽은 돌고 한쪽은 별이 뜬다. **규칙·시간·몰수는 전부 그대로** |
| THROW-011 | P1 | DONE | **경찰 경제** — 명중 시 몰수(도둑 −50%, 경찰 +20%), 지갑, 슈퍼마켓 구매 |
| THROW-012 | P1 | DONE | 감옥 (체포 → 경찰서 11초 → 도둑 스폰에 재배치). 승리 조건 확정으로 해금 |
| THROW-013 | P2 | TODO | 수갑 소모품화. 승리 조건은 확정됐고 수갑 없이도 성립하므로 선택 사항이 됨 |
| MAP-008 | P1 | DONE | **집 실내** — 문 개폐, 실물 크기 큰 방 8개, 실내 3인칭 시점, 호스트 랜덤 배치 |
| ART-015 | P1 | TODO | **`thief.fbx` 다리 가중치 보강** — 다리가 정점의 19.4%뿐이라 스윙이 화면에서 안 읽힌다 |

`THROW-001`·`THROW-003`은 **기능만** 들어갔다. 판정(`ThrowResolver`,
`PlacedTrap`), 기절(`StunState`), 소지 슬롯(`ToolCarrier`), 입력(`F` 키),
HUD 표시가 동작하고 테스트로 고정돼 있다.

돌 줍는 지점 5곳이 **임시로** 들어갔다. 양쪽 다 주울 수 있고 12초 후 다시
생긴다. 최종 배치와 바나나(`THROW-006`)는 맵 작업 때 함께 정한다.

도둑이 "달리는 것처럼 안 보인다"는 문제는 **코드가 아니라 리그다** (`ART-015`).
두 캐릭터 모두 뼈는 똑같이 24° 스윙하는데, 메시가 그 뼈에 묶여 있는 양이 다르다.

| | 다리 가중치 | 팔 가중치 | 정점 수 |
|---|---:|---:|---:|
| `police.fbx` | **38.8%** | 18.1% | 6,258 |
| `thief.fbx` | **19.4%** | 20.3% | 3,633 |

경찰은 다리가 팔의 두 배라 걸음이 지배하고, 도둑은 다리와 팔이 비슷한 데다 경찰의
절반이다. 그래서 "양팔만 휘적휘적"으로 보인다. Blender에서 가중치를 손봐야 하고,
절차적 애니메이션으로는 덮을 수 없다.

플레이어 몸통 상하 움직임은 **넣었다가 되돌렸다.** 달리는 것을 읽히게 하려고
넣었는데, 다리가 잘 안 움직이는 캐릭터에서는 그게 지배적인 움직임이 되어 "들썩"이
아니라 "진동"으로 읽혔다 ("눈이 아프다"). 경찰은 넣기 전에도 괜찮았다.

경찰 경제가 들어갔다 (`THROW-011`).

| 항목 | 값 |
|---|---:|
| 도둑이 잃는 비율 | 누적액의 50% |
| 경찰이 받는 비율 | 누적액의 20% |
| 경찰 시작 금액 | 120골드 |
| 끈끈이 / 센서등 가격 | 60 / 90골드 |

두 비율이 다른 것이 핵심이다. 같으면 명중이 **이전**이 되어 도둑은 여전히 승리
궤도에 있으면서 자기를 잡을 장비를 대주게 된다. 30%는 경기에서 사라진다.

몰수는 **기절이 실제로 걸렸을 때만** 일어난다. 그래서 기존 재기절 간격이 돈을
빼앗는 빈도의 상한이 된다 — 없으면 돌 하나로 몇 초 만에 도둑을 비운다. 방향은
한쪽뿐이다. 도둑이 경찰 지갑을 털면 경찰 장비가 자기 실패로 자기를 대는 셈이다.

경찰 지갑은 **승패를 결정하지 않는다.** 장비만 산다. 모으는 것으로 이길 수 있으면
경찰은 추격을 멈춘다.

임시로 뿌려둔 경찰 전용 픽업 4곳은 **제거했다.** 공짜로 줍히면 지갑에 쓸 데가
없고, 쓸 데 없는 화폐는 경제가 아니라 화면 구석의 숫자다.

승리 조건이 확정됐다 (`DEC-032`). 경찰은 **체포 3회**, 도둑은 판매 1,000골드,
시간 만료는 경찰 승리다. 체포 1회는 경기를 끝내지 않고 **경찰서 11초 구금 후
도둑 스폰에 재배치**한다 (`THROW-012` 완료).

체포 1회 즉시 승리는 4분짜리 경기를 30초에 끝낼 수 있었고 도둑에게 복구 수단이
없었다. 석방 지점을 경찰서 문 앞이 아니라 도둑 스폰으로 둔 것은, 방금 잡은 경찰
옆에 돌려놓으면 남은 두 번을 그대로 넘겨주기 때문이다.

`THROW-013`(수갑 소모품)은 이제 **선택 사항**이다. 승리 조건이 수갑 없이도
성립한다.

동물 표정 아이콘이 들어갔다 (`ART-016`). 결과 20종과 상태 머신을 **얼굴 넷으로 좁혔다.**
추격 중에 묻는 질문은 "됐나"이지 "리졸버가 어느 분기를 탔나"가 아니고, 둘째를 답하려는
아이콘은 아무것도 답하지 못한다.

| 아이콘 | 언제 |
|---|---|
| 느낌표 | `TrailFound`, `BarkRevealedThief`, `ScoutReported` |
| 전구 | 상태가 `MoveToTarget`/`ExecuteCommand`로 — 명령을 이해하고 착수 |
| 하트 | `Completed`, `SearchStarted`, `StealDelivered`, `HideStored` 등 |
| 뱃지 | `TrailMissing`, `BarkFoundNobody`, `StealNoLoot`, 명령 거부 |

**뱃지가 가장 값어치 있다.** 흔적이 없는데 강아지가 뛰어가는 모습은 흔적을 찾은 모습과
화면상 완전히 동일하다. 지금 동물이 하는 것 중 가장 오해를 부르는 장면이고, 이 아이콘
하나가 그것을 가른다.

전구를 결과가 아니라 상태 변화에 붙인 것은 어떤 명령이 결과까지 몇 초 걸리기 때문이다.
그동안 아무 표시가 없으면 못 알아들은 것처럼 보인다.

동물 유인 소품 두 종이 들어갔다 (`THROW-004`). **양쪽이 서로의 동물을 건드릴 수 있는
유일한 수단**이고, 그전까지 동료는 따돌릴 수는 있어도 방해할 수는 없었다 — 게임의 동물
절반에 대응 수단이 하나도 없었다.

| 소품 | 주인 | 부르는 동물 | 값 |
|---|---|---|---:|
| 참치캔 | 경찰 (상점) | 고양이 | 40골드 |
| 개껌 | 도둑 (진열대 절도) | 강아지 | — |

기절이 아니라 **이동**이다. 동물은 무력화되지 않고 가고 싶은 곳으로 걸어간다. 얼어붙은
개보다 읽기 쉽고 우습고, 던진 쪽에도 비용이 있다 — 잘못 던진 개껌은 강아지를 자기 쪽으로
끌어온다. 명령 수행 중에도 덮어쓰므로 상대가 내린 지시를 망칠 수 있고, 그게 이 소품의
값어치다. 4초 뒤 원래 하던 것으로 돌아간다.

바나나는 `ThrowableKind`에 있었지만 **얻을 방법이 없었다** (`THROW-006`). 이제 슈퍼마켓
진열대에서 도둑만 가져간다. 길바닥의 돌은 누구 것도 아니지만 가게 안 진열대는 경찰이
집어가는 물건이 아니다.

승리 조건은 규칙만 들어갔고 **화면에는 아직 안 보인다** (`UI-009`). 지금은 몇 번
잡혔는지 알 수 없고, 감옥에 갇힌 도둑은 자기가 왜 못 움직이는지 알 수 없다. 규칙이
동작하는 것과 플레이어가 그 규칙을 아는 것은 다른 문제다.

클라이언트에서 보이려면 복제가 필요하다. 승패 자체는 호스트가 정해 복제하므로
판정에는 영향이 없지만, 체포 횟수와 구금 잔여 시간은 지금 호스트에만 있다.

경찰 설치물 2종이 들어갔다 (`THROW-009`). 둘 다 **친숙한 물건**이다.

| 물건 | 효과 | 반경 | 근거 |
|---|---|---:|---|
| 끈끈이 | 3초 고정 | 0.85m | 쥐덫 대신. 만화 마을에 곰덫은 유일하게 잔인한 물건이 된다 |
| 센서등 | 2.5초 노출 | 3.2m | 아파트 복도 센서등. 밤에는 경보보다 빛이 값지다 |

효과는 `TrapEffect` 두 종(`Hold`, `Reveal`)으로 모델링했다. 물건마다 분기하지
않으므로 물건을 추가해도 판정 코드가 늘지 않는다. **센서등은 속도를 전혀 늦추지
않는다** — 도둑은 계속 뛰고, 다만 드러난 채로 뛴다. 늦추기까지 하면 끈끈이의 더
좋은 버전이 될 뿐이다.

노출은 손전등 부채꼴을 **완전히 무시한다** (거리도). 방향 마커를 띄우는 대신
도둑을 실제로 보이게 한 것은, 밤에 안 보이는 것에 의존하는 쪽에게 보인다는 것
자체가 가장 강한 사건이기 때문이다. 마커는 같은 정보를 더 나쁘게 전달한다.

**설치물은 그전까지 전부 보이지 않았다** (`THROW-010`). 코디네이터가 트리거만
있는 빈 오브젝트를 만들고 있었다. 설치 가능한 물건을 아직 아무도 얻을 수 없어서
드러나지 않았을 뿐이고, 처음 놓는 바나나는 투명했을 것이다. 보이지 않는 덫은
덫이 아니라 불운이다.

획득은 임시로 경찰 전용 지점 4곳이다 (14초 후 재생성). 상점은 4묶음이다.

조준은 커서다. 좌클릭이 커서 방향으로 던지고 `F`도 남는다. 커서 광선을 발밑
수평면과 교차시켜 방향을 구하며, 이 계산만 요청하는 기계에서 하고 나머지는 전부
호스트가 정한다 — 커서는 남의 화면에만 있으므로 호스트가 알 수 없는 유일한 값이다.

야간 밝기와 손전등 경계는 1차 실기 피드백이다. 달빛을 0.62로 올렸고 도둑 화면만
추가로 밝다. 부채꼴은 바닥에 양쪽 모두에게 그리고, 조명·판정·표시가
`FlashlightCone` 하나를 읽는다. 세 값이 따로 있어서 조명 23°와 판정 30°가 이미
어긋나 있었다.

`ThrowablePickup`은 `Generic` 타입이다. `Loot`으로 두면 `PlayerRolePermissions`가
도둑 전용으로 막아 **경찰이 돌을 주울 수 없다.** 역할 제한은 픽업 자체의
`roleRestricted`가 담당하며, 상점 바나나가 도둑 전용이 되는 것도 그 경로다.
`PlayerInteractionSceneTests`가 모든 픽업을 양쪽 역할로 검사한다.

`THROW-007`로 판정이 호스트로 모였다. 던지기는 RPC로 호스트가 맞았는지 정하고,
기절은 `NetworkVariable`로 복제되며, 설치된 덫은 명명 메시지로 양쪽이 같은
자리에 그리고 호스트만 발동시킨다. 투사체마다 `NetworkObject`를 만들지 않았다
(`ISSUE-016`).

**밤과 시야 제한이 들어갔다.** 달빛 조명, 경찰 손전등 원뿔, 그리고 부채꼴 밖의
도둑을 렌더링하지 않는 `FlashlightVisibility`다. 세상을 어둡게 만드는 방식이
아니라 **도둑만 숨기는** 방식이다 — 고정 카메라가 지형을 계속 보여줘야 하고,
얻는 효과는 거의 같으면서 비용이 훨씬 낮다.

시야 제한은 **표시만** 바꾼다. 안 보이는 도둑도 호스트에서는 똑같이 움직이고
보물을 들고 체포당한다. 경찰 화면에서만 동작하도록 로컬 역할로 막아 두었다.
1.75m 체포 거리보다 넓은 3m 안에서는 항상 보인다 — 잡을 수 있는 거리에서
안 보이면 허공을 붙잡는 셈이 된다.

## Epic 8. 동물 파트너

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| COMP-001 | P0 | DONE | 공통 AI 동료 상태 구조 (7개 상태) |
| COMP-002 | P0 | DONE | 소유자 따라가기와 리쉬 복구 |
| COMP-003 | P0 | DONE | 명령 요청 데이터 구조 |
| COMP-004 | P0 | DONE | 명령 전달기 (입력과 AI 분리) |
| COMP-005 | P0 | DONE | 명령 유효성 검사 |
| COMP-006 | P0 | DONE | 경로 실패 복구 |
| COMP-007 | P0 | DONE | 숫자키 명령의 실제 효과 (`COMP-008` 해소, `DOG-003`·`CAT-004`로 대체) |
| COMP-008 | P0 | DONE | 동료 벽 충돌 (`CharacterController`) |

### 단계 9. 강아지와 고양이 최소 명령

처음부터 8개를 만들지 않는다. 대표 명령 하나씩으로 재미를 먼저 검증한다.

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| DOG-001 | P0 | DONE | 강아지 소유와 따라가기 |
| CAT-001 | P0 | DONE | 고양이 소유와 따라가기 |
| DOG-002 | P0 | DONE | 강아지 명령 수신 (경찰 진영만) |
| CAT-002 | P0 | DONE | 고양이 명령 수신 (도둑 진영만) |
| DOG-007 | P1 | DONE | 강아지 명령 쿨타임 |
| CAT-007 | P1 | DONE | 고양이 명령 쿨타임 |
| DOG-008 | P1 | DONE | 강아지 경로 실패 복구 설정 |
| CAT-008 | P1 | DONE | 고양이 경로 실패 복구 설정 |
| UI-004 | P0 | DONE | 명령 버튼 (경찰 추적 / 도둑 교란) |
| VOICE-007 | P0 | DONE | 버튼·숫자키가 `CommandRequest` 생성 |
| DOG-003 | P0 | DONE | 추적 (흔적 기반, 실제 위치를 완벽히 알지 않음) |
| DOG-009 | P1 | DONE | 흔적을 발자국으로 표시 (추적 중, 경찰 화면만) |
| CAT-004 | P0 | DONE | 교란 (정보만, 실제 위치 불변, 지속시간 제한, 중복 방지) |
| UI-005 | P0 | DONE | 명령 쿨타임 표시 |
| UI-006 | P0 | DONE | 명령 결과 피드백 |

### 단계 10. 나머지 AI 명령

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| DOG-004 | P1 | DONE | 수색 (지정 지역 이동) |
| DOG-005 | P1 | DONE | 경계 (지정 위치 유지) |
| DOG-006 | P1 | DONE | 짖기 (근접 시 도둑 노출, 아니면 실패 보고) |
| CAT-003 | P1 | DONE | 정찰 (주변 보물·경찰 보고, 상태 변경 없음) |
| CAT-009 | P1 | DONE | 정찰 결과를 방향 마커로 표시 (도둑 화면만) |
| CAT-005 | P1 | DONE | 절도 (보물로 이동, 판매는 수행하지 않음) |
| LOOT-005 | P1 | DONE | 보물 숨기기 공통 기능 (지정 은신처 2곳) |
| CAT-006 | P1 | DONE | 숨기 (가까운 은신처로 이동) |
| CAT-010 | P1 | DONE | 물기 (경찰에게 달려가 1.2초 기절, 고양이 전용) |
| COMP-007 | P1 | DONE | 귀여운 대기 행동 (승패·쿨타임 영향 없음) |

`DOG-004`와 `DOG-005`는 현재 "지정 지점으로 이동하고 보고"까지만 한다. 원형
탐색 경로나 경계 중 침입 감지 같은 고유 로직은 없으므로 이를 "수색·경계 완성"
으로 표현하지 않는다.

`CAT-005` 절도는 고양이가 보물 위치까지 이동하는 것까지만이다. 실제 집어
오기와 소유자에게 전달은 미구현이며, 판매는 의도적으로 구현하지 않는다.

## Epic 9. UI

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| UI-001 | P0 | DONE | 공통 HUD |
| UI-002 | P0 | DONE | 경찰 HUD |
| UI-003 | P0 | DONE | 도둑 HUD |
| UI-004 | P0 | DONE | 명령 슬롯과 쿨타임 (`CompanionCommandHudPresenter`) |
| UI-005 | P0 | DONE | 명령 성공과 실패 (같은 프레젠터) |
| UI-006 | P0 | DONE | 체포 게이지 (`ARREST-005`에 통합) |
| UI-007 | P0 | DONE | 역할별 목표 안내 |
| UI-008 | P1 | DONE | 첫 경기 안내 (`FirstPlayGuidePresenter`). 동물 명령만 안내하므로 조작 전체 안내는 별건 |
| UI-009 | P1 | DONE | **체포 횟수 `X/3`과 구금 카운트다운.** 둘 다 클라이언트로 복제한다 — 그전에는 그쪽 화면이 계속 0/3이었다 |
| UI-010 | P0 | DONE | 로비 화면 재구현 (`LobbyCanvas.prefab`, `ISSUE-046`) |
| UI-011 | P1 | DONE | 결과 화면 재구현 (`ResultCanvas.prefab`, `ISSUE-050`) |
| UI-012 | P2 | TODO | `사용 아이템`·`발각 횟수` 카운터. 없어서 결과 카드를 3개로 줄였다 |
| UI-013 | P0 | DONE | 역할 선택 캐릭터 아트 4장 적용 (하이파이브 포즈 교체) |
| UI-014 | P1 | REVERTED | 밤 마을 광장 로비 배경. 적용·검증까지 통과했으나 작업자가 단색 복귀를 요청 |
| UI-016 | P0 | DONE | 결과 화면을 보는 사람 기준 승리/패배로. 목업 4장(승자 × 보는 사람) |
| UI-017 | P1 | DONE | 인벤토리·수색 UI 아이콘 확대(슬롯의 80%, 비율 유지) |
| UI-018 | P0 | DONE | **가방 화면 재구현.** 칸이 맵의 보물 전체가 아니라 실제 소지품을 그리고, 우하단 숫자가 가격이 아니라 개수다. 헤더 재화 배지·NEW 배지 추가 |
| UI-019 | P1 | DONE | **총 재화 상시 표시.** 가방을 열지 않아도 화면 우상단에 보인다 (`CurrencyBadgeView`) |
| UI-020 | P1 | DONE | **마우스 드래그로 칸 이동.** 보물 칸끼리 교환·합치기, 소품 퀵슬롯끼리 교환(호스트 경유). 서로 다른 저장소 사이는 화면에 이유를 띄우고 거절 |
| UI-021 | P0 | DONE | **라쿤 암시장 거래창** (`MerchantTradePresenter`). 소지품 그리드 · 판매 목록 · TOTAL · SELL/SELL ALL/CANCEL |
| UI-022 | P1 | DONE | **경찰의 암시장 구매 창.** 같은 창을 방향만 반대로 쓴다 — 왼쪽 퀵슬롯 4칸, 오른쪽 너구리의 물건과 줄별 `BUY`. 가격·판정은 `PoliceSupplyCounter` 재사용 |
| UI-023 | P1 | DONE | **가방 칸 이름 툴팁** (`InventoryTooltipView`). 마우스를 올린 칸의 이름을 옆에 띄우고, 1~4번 사용 아이템은 효과도 한 줄로 적는다. 문장은 `ThrowableCatalog.GetEffectSummary`가 상수로 조립하므로 밸런스를 바꾸면 문구도 함께 움직인다 |
| UI-024 | P1 | DONE | **게임방법을 로비에 상시 띄우지 않고 버튼으로 연다.** 제목 옆 `게임 방법` 버튼이 4쪽짜리 모달을 띄우고, 화살표로 넘기고 X나 `Esc`로 닫는다. 딤 시트가 뒤의 로비 조작을 가린다 (`LobbyHowToOverlay`, `LobbyHowToLauncher`). 페이지 그림 안에 그려진 X 자리는 `Tools/normalize_howto_pages.py`가 **재서** `hotspots.json`에 적고 빌더가 그 비율로 클릭 영역을 올린다 |
| LOOT-009 | P0 | DONE | Loot Table·시드 추첨·수색 컨테이너·이동 로직 (1단계) |
| LOOT-010 | P0 | TODO | **컨테이너를 실내에 배치.** 평면도로 가구 위치를 재야 한다 — 지금은 씬에 컨테이너가 0개 |
| LOOT-011 | P1 | DONE | **표시한 자리가 있는 실내 열세 곳이 전부 물건을 낸다.** 방마다 자기 오브젝트를 갖고(첫 방은 원본, 나머지는 복제) `자리 − 3`을 겹치지 않는 종류로 따로 뽑는다. 경찰에게는 남은 개수를 화면에 적는다 (`InteriorLootTallyPresenter`) |
| LOOT-014 | P0 | TODO | **소품 다섯 종을 실내 랜덤 스폰으로 옮긴다.** 바나나·개껌·폭죽·고무닭·냉동문어는 지금 잔디밭에 2.2m 간격으로 늘어선 줄 하나가 **유일한 획득처**다(씬의 수색 컨테이너는 0개, 상점은 끈끈이·센서등·참치캔만 판다). 상점에 안이 없던 시절의 임시 배치다. 슈퍼마켓·1층집·2층집의 **그 건물의 뽑기 품목에 소품을 넣어** 보물과 같은 추첨에서 함께 나오게 한다(작업자 결정). 즉 자리는 여전히 `자리 − 3`만 채워지고, 채워진 자리 하나하나가 보물일 수도 소품일 수도 있다. `LootSpotDraw`가 지금은 `LootItem`만 다루므로 **소품(`ThrowablePickup`)도 같은 뽑기에 넣을 수 있게 일반화**하는 것이 이 작업의 실제 몸통이다. 소품 픽업을 만드는 코드는 `GreyboxMapSetup.CreateShopShelfPickups` 안에 인라인으로 있으니 실내에서도 부를 수 있게 먼저 빼낸다. 배분: 슈퍼마켓 = 바나나·개껌·냉동문어(냉동식품), 1층집 = 바나나·개껌·고무닭, 2층집 = 폭죽·고무닭·바나나. 들어가면 `CreateShopShelfPickups`의 줄은 지운다 |
| LOOT-013 | P1 | DROPPED | 실내 물건 종류를 자리 수보다 늘리기. **필요 없다고 결론냈다 (2026-08-08, 작업자 결정)** — 경찰은 물건을 못 줍고, 두 화면이 합의해야 하는 것은 "훔쳐서 없음 / 아직 있음"뿐이다. 종류가 매판 같아도 그 구분은 성립한다 |
| LOOT-012 | P1 | DONE | **모든 추첨이 호스트가 굴린 정수 하나에서 시작한다** (`MatchDrawSeed`, `NetworkMatchMirror._matchSeed`). 백로그에 적힌 증상(컨테이너 id 해시)은 `SearchableContainer` 얘기였고 **그건 씬에도 코드에도 인스턴스가 없다** — 실제로 도는 것은 `LootSpotDraw`이고, 그쪽은 `UnityEngine.Random`으로 **호스트만** 뽑고 있었다. 위치는 `NetworkLootLink`가 복제하지만 그것은 **연출 트랜스폼**이라 클라이언트의 `LootItem` 자체는 저장된 자리에 남아 있었다. 이제 양쪽이 같은 시드로 같은 추첨을 돌린다. 컨테이너 시드도 `string.GetHashCode()`(프로세스마다 무작위)에서 FNV-1a로 바꿨다. 2프로세스 실측: **양쪽 모두 13개 방 전부 추첨** |

## Epic 10. 멀티플레이

NET-001과 NET-002는 본격 멀티플레이가 아니라 패키지와 권한 구조를 결정하기
위한 격리된 기술 검증이다. 이후 게임 기능 동기화는 검증 결과 채택 전
`BLOCKED`다.

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| NET-001 | P0 | DONE | Host·Client 접속, 플레이어 생성, 위치 확인과 종료 처리 |
| NET-002 | P0 | DONE | 경찰·도둑 역할 배정과 시작 위치 |
| ARREST-006 | P1 | DONE | 체포 중복 판정 방지 (네트워크 무관, 규칙 계층) |
| NET-003 | P0 | DONE | 이동 동기화 (양쪽 좌표 0.04m 이내 일치 실측) |
| NET-004 | P0 | DONE | 경기 상태와 타이머 동기화 (양쪽 233.05초 일치 실측) |
| NET-005 | P0 | DONE | 보물 소유권 동기화 (양쪽 `Sold`·소유자 Thief 일치 실측) |
| NET-006 | P0 | DONE | 판매와 점수 동기화 (연타 11,174회에 판매 1회, 양쪽 200G) |
| NET-007 | P0 | DONE | 체포 판정 동기화 (양쪽 진행도 1.5초·완료 일치 실측) |
| NET-008 | P1 | DONE | 재경기 동기화 (클라이언트 요청 1건으로 양쪽 Game 복귀) |
| NET-009 | P1 | DONE | 연결 종료 처리 (상대 이탈을 정확히 1회 처리) |
| NET-010 | P1 | DONE | 네트워크 회귀 프로브 5종. 6개 시나리오 전부 실측 |
| NET-011 | P0 | DONE | 로비 실사용 수정 (`ISSUE-017`)과 같은 네트워크 방 목록 |
| MAP-006 | P1 | DONE | 맵 북 2줄·동 2줄 확장 (80×68m), 신규 집 12채 |
| ART-013 | P2 | DONE | 너구리 상인 인사 연출, 캐릭터·소품 크기 조정 |

`ARREST-006`은 네트워크 없이도 성립하는 멱등성 속성이므로 먼저 완료했다.
`MatchResultArbiter`가 여러 요청을 한 번만 판정하고, `ArrestCompletionController`
가 여러 호출에도 한 번만 승리를 요청하며, `MatchResultSession`이 첫 결과만
보관하는 것을 테스트로 고정했다. `NET-007`이 중복 요청을 그대로 전달해도
안전하다는 근거다.

`NET-005`~`009`는 두 프로세스 실측으로 완료했다. 값 비교는
`13_CURRENT_STATE.md`의 `최근 검증` 표에 있다.

`NET-010`은 6개 시나리오를 전부 실측했다.

| NET-010 시나리오 | 상태 | 근거 |
|---|---|---|
| 경찰 승리 | PASS | `-netScenario full`, 양쪽 `Police / ThiefArrested` |
| 도둑 승리 | PASS | `-netScenario steal`, 양쪽 `Thief / SaleTargetReached`, 지갑 1040/1000 |
| 재경기 | PASS | `-netScenario rematch`, 클라이언트 요청으로 양쪽 Game 복귀 |
| 보물 버튼 연타 | PASS | 클라이언트 요청 11,174회에 `creditedSales: 1` |
| 판매와 체포 동시 | PASS | `-netScenario clash`, 체포 1회가 진행 중인 채로 판매가 이겼고 양쪽 승자 일치 |
| 한쪽 종료 | PASS | `-netScenario disconnect`, 호스트가 정확히 1회 처리 |

미측정 2개는 프로브를 더 만들기 전에 `ISSUE-011`(도둑 승리 조건)을 먼저
해결해야 한다. 보물이 1개뿐이면 두 시나리오 모두 재현할 수 없다.

## 단계 7 착수 조건

| 조건 | 상태 |
|---|---|
| NGO를 본게임에 채택 (`DEC-013` 갱신) | **미결** |
| 권한 주체 확정: Host 권한 대 전용 서버 (`DEC-P01`) | **미결** |
| 접속 방식: 직접 IP 대 Relay | **미결** |
| 로컬 규칙 완성 | 대부분 완료, `ISSUE-011` 잔존 |
| 2인 수동 검증 환경 | 필요 (자동 검증 불가 항목 존재) |

`NET-003`의 "순간이동 최소화"와 `NET-004`의 "두 화면에서 같은 남은 시간"은
두 프로세스를 사람이 동시에 관찰해야 확인된다. 자동 프로브로는 일부만 대체
가능하므로, 검증하지 않은 항목을 통과로 기록하지 않는다.

## 기술 검증 관문 A

| 검증 항목 | 상태 | 근거 |
|---|---|---|
| Unity 목표 플랫폼 빌드 | PASS | TECH-001 Windows x86_64 |
| Blender 테스트 모델 임포트 | PASS | TECH-002 Generic 5본, `Idle`·`Walk` |
| 음성 텍스트 출력 | PASS (사람 발화 미확인) | 로컬 게이트웨이가 한국어 5초 WAV를 `강아지 냄새 추적해`로 받아쓰고 `intent: TRACK`까지 냈다 (2026-08-06). Unity 내장 `DictationRecognizer`는 여전히 `0x80004003`이지만 **그 API를 쓰지 않는다** — `Microphone` + 로컬 faster-whisper다 |
| 두 플레이어 접속 | PASS | NET-001 Host·Client 결과 통과 |
| 경찰·도둑 역할 배정 | PASS | NET-002 양쪽 결과 통과 |
| 관문 A 전체 | PASS (조건부) | 받아쓰기 경로가 열렸다. 남은 것은 **사람이 실제 마이크로** 말하는 확인 (`VOICE-010`) |

## Epic 11. 음성

핵심 프로토타입 완료 전 구현하지 않는다.

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| VOICE-001 | P2 | DONE (WebGL) | STT 인터페이스 — 브라우저 녹음 + 서버 경유 |
| VOICE-002 | P2 | DONE (WebGL) | 텍스트 정규화 — 브라우저 녹음 + 서버 경유 |
| VOICE-003 | P2 | DONE (WebGL) | 경찰 명령 분류 — 브라우저 녹음 + 서버 경유 |
| VOICE-004 | P2 | DONE (WebGL) | 도둑 명령 분류 — 브라우저 녹음 + 서버 경유 |
| VOICE-005 | P2 | 확인 필요 | 신뢰도 처리 — 서버 쪽에 있는지 대조 안 됨 |
| VOICE-006 | P2 | DONE (WebGL) | 음성 피드백 UI — 브라우저 녹음 + 서버 경유 |
| VOICE-007 | P2 | DONE (WebGL) | 마이크 거부와 실패 대응 — 브라우저 녹음 + 서버 경유 |
| VOICE-008 | P0 | DONE | **Windows 음성 경로 복구** (`ISSUE-069`). 빌드가 `LocalAI/`를 찾고, STT가 CUDA 실패 시 CPU로 넘어가고, 마이크를 한 번에 하나만 열고, `V`가 누른 만큼 녹음하고, 실패가 쿨타임을 쓰지 않는다 |
| VOICE-009 | P3 | DEFERRED | ~~`cublas64_12.dll` 설치~~ — 로컬 whisper를 계속 쓸 때만 의미가 있다. `VOICE-012`로 클라우드 STT로 가면 GPU는 무관해진다. **클라우드 경로를 먼저 정하고 나서 다시 본다** |
| VOICE-010 | P1 | TODO | **사람이 실제 마이크로 확인.** TTS WAV로 게이트웨이까지는 실측했고 `Microphone` → WAV 구간은 사람이 말해야 확인된다. 2인 동시(경찰 `V`, 도둑 `V`)로 각자 자기 동물만 반응하는지 함께 본다 |
| VOICE-012 | P0 | **DONE** | **전송이 플랫폼이 아니라 설정으로 결정된다** (2026-08-07). `VoiceConfig.Transport`(`Backend` 기본 / `LocalGateway`), `BrowserVoiceCaptureProvider : IVoiceCaptureProvider`, `VoiceCommandInput` 상태 기계에서 `#if UNITY_WEBGL` 제거. **Windows 빌드가 WebGL이 쓸 경로를 그대로 검증한다.** WebGL 이관은 `backendBaseUrl` 교체로 끝난다 |
| VOICE-017 | P0 | **DONE** | **키 없이 도는 스텁 + 전사 주입** (2026-08-07). `OPENAI_API_KEY`가 없으면 자동으로 스텁을 쓰고 시작 시 경고를 남긴다. `transcript` 필드로 마이크·키 없이 명령을 넣는다 (`VOICE_ALLOW_TRANSCRIPT_OVERRIDE`, **기본 꺼짐**, 켜지 않으면 403). 서버 14/14 통과 |
| VOICE-018 | P0 | **DONE** | **`?wait=1` 동기 응답** (2026-08-07). 같은 처리를 돌리고 소켓 리스너가 받았을 **같은 레코드**를 읽어 응답한다 — 경로가 둘이면 답도 둘이 된다 |
| VOICE-019 | P0 | **DONE** | **빈 `allowedIntents`를 deny-all로 읽던 것.** 월드 컨텍스트는 소켓으로 오고 소켓은 WebGL 전용이라, **동기 경로에서는 목록이 항상 비어 있다** — 모든 명령이 조용히 버려졌다. "미지정"으로 고쳤다. 스텁 파이프라인 테스트가 실제 전사에 닿기 전에 잡았다 |
| VOICE-017-old | — | 참고 | **키 없이 도는 스텁 + 개발용 전사 주입.** `StubSpeechToTextClient`(canned 한국어 명령)와 `?transcript=짖어`. 마이크·키 없이 의도→불복종 사슬 전체와 `PetCognitionConfig` 밸런스를 확인할 수 있다. **`VOICE_ALLOW_TRANSCRIPT_OVERRIDE` 기본 꺼짐** — 제출 빌드에 뒷문을 남기지 않는다. 파일 5개(`env.ts`, 스텁, `voice-command-service`, 라우트, 배선) |
| VOICE-018-dup | — | 중복(위 DONE 행 참조) | 서버 `?wait=1` 동기 응답. 지금 답이 WebSocket으로만 오고 그 클라이언트는 jslib라 **WebGL 전용**이다 — Windows가 같은 경로를 끝까지 타려면 필요하다 |
| VOICE-012-old | — | 참고 | **전송을 플랫폼이 아니라 설정으로 고른다.** 지금 `VoiceCommandInput`이 `#if UNITY_WEBGL`로 갈라져 브라우저는 `server/`(Fastify)로, Windows는 로컬 게이트웨이로 간다 — **AI 백엔드가 둘이고 같은 말이 두 곳에서 다르게 해석될 수 있다.** 하나로 모으면 Windows 개발 중에 WebGL과 같은 경로를 테스트한다 |
| VOICE-016 | P0 | DONE | **`짖어`·`숨어`가 음성으로 도달 불가였다.** `FromIntent`에 `Bark`로 가는 인텐트가 없었고 `Hide`도 고양이 허용 목록에 없었다 — 받아쓰기가 완벽해도 `None`이었다. `VoiceCommandReachabilityTests` 4건으로 고정 |
| VOICE-013a | P0 | DONE | **매처를 자모 단위로 재작성** (2026-08-07). 문장 안 스템 탐색 + 초성/종성 공유 알파벳 자모 근사. `지지라고`→`BARK`, `스모`→`HIDE` 복구. 파이썬 13사례 실측 오탐 0 |
| VOICE-013 | P0 | TODO | **받아쓰기 정확도 — 남은 것은 STT 쪽.** `lexiconFor()`를 STT `prompt`로 넘겨 어휘 편향, 그다음 클라우드 STT 전환. `faster-whisper-small`이 "짖으라고"를 "지지라고"로, "숨어"를 "스모"로 받는다. 세 가지를 순서대로: ① 명령 어휘를 `initial_prompt`로 주기(모델 교체 없이 즉시), ② 클라우드 STT로 전환, ③ 그래도 틀린 글자를 LLM이 의도로 되돌리기 |
| VOICE-014 | P0 | **DONE** | **의도 해석은 항상 성공한다** (2026-08-07). 후보가 비거나 `UNKNOWN`이면 결정적 매처가 마지막 말을 한다. 닫힌 명령 목록 + 게임 상태를 주고 LLM이 **반드시 하나를 고르게** 한다 — "못 알아들었어요"는 없다. 받아쓰기가 조금 틀려도 여기서 복구된다 |
| VOICE-015 | P0 | **DONE** (저장소 몫) | **엉뚱한 행동은 게임이 굴린다, LLM이 아니다.** `PetCognitionResolver`가 이미 굴리고 시드는 서버 `commandId` 기반이라 두 기계가 같다. 실기 확인은 `VOICE-010`. 불복종 확률은 `03_GAME_RULES.md`의 밸런스 수치이고 **호스트가 굴려야** 양쪽 화면이 같다. 자리는 이미 있다 (`PetCognitionResolver`) |
| VOICE-011 | P1 | **코드 완료 / 서버 미실행** | **WebGL을 LAN·인터넷에서 테스트하려면 https가 필요하다.** `navigator.mediaDevices`가 비보안 컨텍스트에 아예 없으므로 localhost 외에서는 마이크가 열리지 않는다. nginx가 TLS를 끝내고 음성 API가 같은 오리진의 `/api`로 간다 (`TASK-DEPLOY-004`) |

## Epic 12. Blender 최종 모델 제작과 적용

Blender 모델 자체는 작업자가 제작한다. 이 저장소의 작업은 Unity 임포트,
프리팹, Animator, 최적화다.

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| MODEL-001 | P1 | DONE | 캐릭터 제작 규격 확정 → `docs/16_MODEL_SPEC.md` |
| MODEL-002 | P1 | TODO | 대표 캐릭터 한 세트 제작 — **작업자 몫** (애니메이션 클립 6종) |
| ART-002 | P1 | DONE | 임포트 규격 검사 도구 (`Validate Authored Models`) |
| ART-003 | P1 | DONE | Animator 6개 상태와 파라미터 분리 |
| ART-004 | P1 | DONE | 대표 모델 게임 적용 (Collider·속도·체포 거리 유지) |
| ART-005 | P1 | DONE | 성능 측정 + WebGL 용량 33.9MB (드로우 콜만 미측정) |
| MODEL-003 | P2 | DONE | 경찰 모델 — **확정** (메시·텍스처, 클립 없음) |
| MODEL-004 | P2 | DONE | 도둑 모델 — **확정** (메시·텍스처, 클립 없음) |
| MODEL-005 | P2 | DONE | 강아지 모델 — **확정** (메시·텍스처, 클립 없음) |
| MODEL-006 | P2 | DONE | 고양이 모델 — **확정** (메시·텍스처, 클립 없음) |
| MODEL-007 | P2 | TODO | 건물과 환경 모델 — **작업자 몫**, 교체 가능성 있음 |
| ART-006 | P2 | DONE | 경찰 모델 적용 |
| ART-007 | P2 | DONE | 도둑 모델 적용 |
| ART-008 | P2 | DONE | 강아지 모델 적용 |
| ART-009 | P2 | DONE | 고양이 모델 적용 |
| ART-010 | P2 | DONE | 환경 모델 적용 (건물 5종·쓰레기통·너구리) |
| MAP-002 | P1 | DONE | 최종 충돌 구조 (전부 Box, 시각 메시 미사용) |
| MAP-003 | P2 | DONE | 지붕과 사다리 루트 (올라가기·내려가기, 양쪽 역할 사용) |

### 단계 13. 사운드와 연출

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| AUDIO-001 | P1 | DONE | 사운드 이벤트 10종 정의 (`GameSoundId`) |
| AUDIO-002 | P1 | DONE | 규칙이 사운드 결과에 의존하지 않는 단방향 연결 |
| AUDIO-003 | P1 | DONE | 배경음과 음성 캡처 시 더킹 (클립 미제작) |
| UX-001 | P1 | DONE | 첫 플레이 안내 (한 줄, 45초 후 자동 종료) |
| UX-002 | P1 | DONE | 아이콘·텍스트 병행 표기 |
| UX-003 | P1 | DONE | 쿨타임 막대·동료 현재 행동·실패 이유 표시 |

**사운드 클립은 하나도 없다.** `GameSoundBank`에 10개 항목이 생성돼 있고 전부
클립이 비어 있다. 이벤트 배선과 믹싱 구조만 완성된 상태이므로 이를 "사운드
완성"으로 표현하지 않는다. 클립 제작은 작업자 몫이다.

`AUDIO-003`의 음성 더킹은 훅만 만들어 뒀다. 음성 입력(11단계)이 보류 중이므로
실제 검증은 그때 한다.
| MAP-004 | P2 | TODO | 맵 루트 밸런스 측정 |
| MAP-005 | P2 | DONE | 보물 6개 맵 전역 배치, 스폰 유지 (`ISSUE-011` 해소) |
| ART-011 | P2 | DONE | 시각 효과 (투척 궤적·기절 별·설치물 표시) |
| ART-016 | P1 | DONE | **동물 표정 아이콘 4종** — 발견·이해·성공·거부를 머리 위에 표시 |
| ART-012 | P2 | DONE | 머티리얼 통합·정적 배칭·그림자 정리 (아틀라스·LOD 미실시) |

`MODEL-002`~`007`의 모델 제작 자체는 작업자 몫이다. 이 저장소가 담당하는 것은
임포트 규격 검사, 프리팹, Animator, 최적화다.

**확정된 6종**(경찰·강아지·도둑·고양이·쓰레기통·너구리)은 메시와 텍스처가
확정이며 교체하지 않는다. 다만 **애니메이션 클립이 하나도 없어** 현재 이동은
절차적 애니메이션으로 대체돼 있다. `MODEL-002`의 실제 산출물은 클립 6종
(`Idle`·`Walk`·`Run`·`Command`·`Win`·`Lose`)이다.

집과 건물은 교체 가능성이 남아 있어 모델에 직접 의존하지 않는다.

`MAP-002` 최종 충돌 구조와 `MAP-003` 지붕·사다리 루트는 Epic 2에서 관리한다.

### ART-000 임시 모델 적용 (완료)

프로토타입 테스트를 진행하기 위한 임시 조치이며 최종 아트가 아니다.

- 작업자가 제공한 프롭 `.blend` 19종을 FBX로 반입해 보물, 너구리 장터,
  쓰레기통, 사다리에 적용
- 캐릭터 모델은 아직 없으므로 외부 TopDown Engine의 `LoftTie`와
  `LoftSuspenders` 메시를 역할 색 머티리얼로 임시 대체
- 건물은 계속 회색 상자를 사용

## Epic 12.5. 제출

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| ART-001 | P2 | DONE | 캐릭터 VisualRoot 교체 구조 |
| AUDIO-001 | P2 | DONE | 효과음 10종 배선·클립 완료 (CC0) |
| AUDIO-005 | P2 | DONE | `23_SFX_ACQUISITION_SHEET` **C-1~C-3 종결**. 19 ID 추가(총 43, 40개 클립 있음), 나머지 삭제. 폭죽 2파일은 `FireworkBangMerge`가 하나로 이어 붙여 순서를 파일의 성질로 만들었다. **남은 3종**(`sfx_dog_alert`·`sfx_cat_alert`·`sfx_glue_stick`)은 코드 완성, 파일 대기 |
| AUDIO-004 | P2 | DONE | `23_SFX_ACQUISITION_SHEET` **C-4~C-11 종결**. 11개 추가(총 24 ID, 전부 클립 있음), 나머지 60행은 필요 없음으로 삭제. 로비·결과 씬에서 소리가 나지 않던 구조적 결함도 함께 고쳤다 (뱅크를 `Resources`로 옮기고 서비스 없는 씬은 영속 소스로 폴백) |
| SUBMIT-001 | **P0** | TODO | **WebGL 빌드 + 서버 배포 + https.** 링크 클릭만으로 브라우저에서 플레이돼야 하고 심사 종료까지 살아 있어야 한다. `.exe` 제출은 받지 않는다. 소스 전체를 같은 저장소에 커밋 기록과 함께 둔다(비공개면 `dl_gameai_reviewer@nhn.com` 초대) |
| SUBMIT-002 | **P0** | TODO | **30~60초 실플레이 영상**, YouTube(공개 또는 링크 공유). **AI 조작·합성·타인 영상 도용 불가 — 실제 플레이 화면 그대로.** 즉 컷 편집한 홍보 영상이 아니다 |
| SUBMIT-003 | **P0** | TODO | 게임 소개 PDF — 제목·한 줄 소개·목표·조작·종료 조건·실행 방법·**플레이 링크**·**영상 링크**. 링크 둘이 들어가므로 `SUBMIT-001`·`002` 뒤다 |
| SUBMIT-004 | **P0** | TODO | AI 활용 기술 PDF — 구조 설명, 주요 프롬프트·지시, **외부 에셋/오픈소스 출처**. 요강이 도구·활용 내역 기재를 **의무**로 두므로 선택이 아니다. Tripo·Suno·freesound CC0는 **적기만 하면 문제되지 않는다** |
| SUBMIT-005 | **P0** | WIP | 라이선스 정리 — `THIRD_PARTY_NOTICES.md`에 모델·효과음·폰트·엔진 기록 완료. 효과음은 **37개 전부 CC0**(freesound)로 확인해 `docs/17`과 고지 양쪽에 적었다. **남은 것: BGM 미정, Tripo 증빙 4종 보관** |

## 프로토타입 관문 B: 첫 번째 수직 슬라이스

다음 시나리오가 완성돼야 한다.

```text
경기 시작
→ 경찰과 도둑 이동
→ 도둑이 보물 획득
→ 도둑이 판매처로 이동
→ 경찰이 도둑을 추격
→ 판매 또는 체포
→ 결과 화면
→ 재경기
```

강아지, 고양이, 음성, 최종 모델 없이 통과해야 한다. 이 단계에서 먼저
플레이테스트하며, 기본 추격전이 재미없으면 맵과 규칙을 수정한다.

| 검증 항목 | 상태 | 근거 |
|---|---|---|
| 경기 시작과 READY 카운트다운 | PASS | MATCH-002 |
| 경찰·도둑 이동 | PASS | PLAYER-001, PLAYER-002 |
| 보물 획득과 운반 | PASS | LOOT-002, LOOT-003 |
| 판매 | PASS | LOOT-006 |
| 체포 | PASS | ARREST-001~005 |
| 결과 화면 | PASS | MATCH-006 |
| 재경기 | PASS | MATCH-007 |
| 도둑 판매 승리 경로 | PASS | 보물 6개 1,200골드 대 목표 1,000골드 (`ISSUE-011` 해소) |
| 경찰 대 도둑 실제 추격 | PASS | `NET-003`~`NET-009` 완료, 2프로세스 실측 |
| 관문 B 전체 | READY | 차단 요소 해소. 2인 실기 플레이테스트만 남음 |

## 맵 이식 후속 (`TASK-PORT`) — 완료, 삭제

샌드박스 맵을 본게임에 이식하는 후속 작업 10건은 **모두 끝났다** (2026-08-06 확인).
남아 있던 `TASK-PORT-010`(`Game.unity` 6.5MB 상한 초과)도 씬이 3.68MB로 줄어
해소됐다 — `HouseLayoutSceneTests`가 초록이고 Edit Mode 294건 전부 통과한다.

**놓은 자리가 비어 있는지 반드시 재본다.** 눈으로 찍은 좌표 다섯 중 넷이 건물 안에
박혀 있던 적이 있고, 씬은 빌드되고 검증기는 통과했다. 이 교훈은 남긴다.

## 바로 다음 작업

1. `ISSUE-011` 도둑 판매 승리 경로 확보 (보물 수 또는 목표 금액 결정)
2. 관문 B 플레이테스트
3. `COMP-001` 공통 명령 요청과 검증
4. `COMP-002` 숫자키 `1`~`4` 입력 어댑터
# Voice implementation status

The previously blocked voice tasks are unblocked for the WebGL/Fastify path.
The first implementation slice includes browser capture, upload validation,
STT, absolute-command matching, structured Intent candidates, Host cognition,
NGO result events, and fallback keyboard input. Streaming STT, fine-tuning,
persistent analytics, and multi-instance server deployment remain later work.

## 2026-08-02 follow-up status

- DONE: HUD timer restored under the police catch board.
- DONE: Core HUD, inventory, and voice panels use more transparent backplates.
- DONE: Voice feed displays both raw transcript and interpreted animal command.
- DONE: Thief-to-cat `E` interaction opens a two-panel exchange UI.
- DONE: Player inventory surface is represented as a 5x5 square-slot grid.
- DONE: Quick slots reject non-interaction catalog entries.
- DONE: LocalAI deterministic command fallback runs before Ollama, reducing
  common dog/cat command interpretation latency.
- TODO: Manual playtest pass for the cat exchange UI with real item pickup
  order and live microphone input on the target machine.

## 배포 (`TASK-DEPLOY`)

`docs/21_REMOTE_PLAY_AND_DEPLOY.md` 4절이 기준이다.

| ID | 상태 | 내용 |
|---|---|---|
| `TASK-DEPLOY-001` | DONE | 전송을 WebSocket으로 바꾸고 호스트명 접속을 허용한다 (2026-08-05) |
| `TASK-DEPLOY-002` | DONE | `StartServer()` 전용 서버 모드와 `-dedicatedServer` 인자 (2026-08-05). **Relay 채택으로 미사용** |
| `TASK-DEPLOY-003` | **취소** | 리눅스 헤드리스 빌드. **불필요해졌다** — 경기 연결이 Unity Relay를 지나가므로 EC2에 게임 서버가 없다 |
| `TASK-DEPLOY-004` | **코드 완료 / 서버 미실행** | nginx + Certbot + DuckDNS. `deploy/pawlice.nginx.conf`·`setup-ec2.sh` 커밋됨. **EC2에서 아직 실행하지 않았다** |
| `TASK-DEPLOY-005` | **DONE** | `VoiceBackendAddress`가 `Application.absoluteURL`의 오리진을 쓴다 (2026-08-09). 경기 주소는 Relay가 정하므로 입력칸 자체가 없어졌다 |
| `TASK-DEPLOY-006` | **코드 완료 / 서버 미실행** | 열어야 하는 것은 **80·443·22 뿐**이다. 게임 포트는 열지 않는다 — Relay가 처리한다 |
| `TASK-DEPLOY-007` | **TODO (사람만 할 수 있음)** | **Unity Cloud 프로젝트 연결.** `cloudProjectId`가 비어 있으면 방 만들기가 즉시 거절된다. `docs/21` 2절 A |
| `TASK-DEPLOY-008` | TODO | EC2에서 `setup-ec2.sh` 실행 → `.env` → `upload.ps1` → 다른 네트워크 2인 실측 |
