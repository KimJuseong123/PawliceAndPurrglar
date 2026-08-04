# 핸드오프 — 도둑 인벤토리와 너구리 거래

이 문서는 `CLAUDE_RACCOON_TRADE_INVENTORY_DND_IMPLEMENTATION.md`를 구현하기 위한
사전 조사 결과다. 새 세션에서 이 파일을 읽고 `/to-spec` → `/to-tickets`로 쪼개
진행한다. 조사 없이 바로 구현에 들어가면 없는 시스템을 있다고 가정하게 된다.

## 왜 별도 세션인가

명세의 완료 조건이 39개이고, 인벤토리 데이터 모델을 새로 만든 위에 드래그를 얹고
그 위에 거래를 얹는다. 그리고 **승리 조건과 체포 경제를 건드린다.** 지친 문맥에서
경제 규칙을 만지면 안 된다.

---

## 1. 작업자가 구두로 확정한 규칙 (명세보다 우선)

명세 문서와 목업보다 이쪽이 최신이다.

1. **한 칸에 한 개.** 스택이 없다. 바나나 2개는 **두 칸**을 쓴다.
2. **이동 페널티 없음.** 물건을 많이 들어도 느려지지 않는다.
3. **체포 시**: 돈의 **30%**를 잃는다. 돈이 없으면 **100원어치 물건**을 잃는다.
4. **고양이 가방으로 물건을 옮길 수 있다.**

### 미해결 — 새 세션에서 먼저 물어야 한다

- **스택 유무가 목업과 어긋난다.** 목업에는 수량 배지가 있다(바나나 `2`, 코인 `5`,
  보석 `5`). 1칸 1개면 그 배지는 존재하지 않는다. 규칙이 맞는지 목업이 맞는지
  확인받아야 하고, 답에 따라 데이터 모델이 달라진다.
- **"100원어치 물건"의 선택 규칙.** 싼 것부터인지 비싼 것부터인지, 100을 넘기면
  넘겨서 뺏는지 못 미치게 뺏는지가 정해져야 한다.
- **30%는 누가 가져가는가.** 지금은 도둑이 잃는 50% 중 경찰이 20%를 받고 30%는
  경기에서 사라진다. 30%로 바꿀 때 경찰 몫이 얼마인지 정해야 한다.

---

## 2. 지금 있는 것

| 것 | 위치 | 비고 |
|---|---|---|
| 도둑 화폐 | `ThiefLootWallet` (`SoldAmount` / `TargetAmount`) | **승리 조건 카운터다.** 1,000골드가 도둑 승리 |
| 경찰 지갑 | `PoliceWallet` | 장비 구매용. 승패와 무관 |
| 너구리 판매 | `LootSaleZone`, `LootCarrier.TrySell` | **들고 있는 보물 하나**를 판다 |
| 보물 소지 | `LootCarrier` | **한 번에 하나** (`HeldLoot`) |
| 소품 슬롯 | `QuickSlotController` | 4칸, 스택 있음 |
| 고양이 가방 | `CatInventoryInteractable` | `QuickSlotController` 4칸 |
| 좌우 2패널 교환 UI | `RoleAwareHudController` + `ISlotContainer` | **재사용할 것** |
| 이동 로직 | `ContainerTransfer.MoveOne/MoveEverything` | 꺼내고→넣고→실패시 되돌리기 |
| 아이템 정의 | `LootDefinition` (SO) | `stableId`·`displayName`·`rarity`·`carryType`·`raisesAlarm` |
| 소품 종류 | `ThrowableKind` (enum) | 9종. **가격 없음** |
| 상호작용 | `IPlayerInteractable` + `IHoldInteractable` + `IInteractionPriority` | 새로 만들지 말 것 |
| 역할 권한표 | `PlayerRolePermissions` | `Loot`=도둑, `Generic`=양쪽 |
| Loot Table | `LootTable`, `LootRoller`, `SearchableContainer` | 이번 세션에 만듦 |

## 3. 없는 것

- **25칸 인벤토리 저장소.** HUD의 5×5는 **뷰**다 — 앞 4칸은 퀵슬롯을 비추고 나머지
  21칸은 `FindVisibleLootItems()`, 즉 **주변 바닥의 보물 목록**이다. 담아 두는 곳이
  아니다 (`RoleAwareHudController.BindInventorySlots`).
- **드래그 앤 드롭.** 프로젝트 전체에 `IDragHandler` 계열 구현이 하나도 없다.
  교환 UI는 슬롯 **클릭**으로만 옮긴다.
- **아이템별 가격.** 가격은 희귀도별로 `LootConfig`에서 온다
  (Common 200 / Uncommon 350 / Rare 500). `LootDefinition.GetPrice(LootConfig)`.
- **`Sellable` / `MaxStack` / `Category`** 필드.
- **거래 UI**, 수량 팝업, 확인 팝업.
- **원자적 `TradeService`**, 서버 검증.
- **영속 저장.** 경기 단위 게임이고 세이브가 없다. 명세 §13은 이 프로젝트에
  해당하지 않는다.

## 4. 지우면 안 되는 것

**도둑 체력 UI는 존재하지 않는다.** 명세와 작업자 목록의 "도둑 체력 UI 완전 제거"는
오해에서 나왔다. 화면의 `THIEF 0 / 1000`은 체력이 아니라 **판매 누적액 / 목표액**
이고, 도둑의 승리 조건 표시다. 지우면 도둑이 자기가 이기고 있는지 볼 수 없다.
이 프로젝트에 HP는 없고 체포는 진행도 기반이다.

## 5. 경제에 미치는 영향 — 가장 위험한 부분

지금 판매는 "보물 하나 들고 너구리에게 간다"다. 명세는 "가방에 모아 한꺼번에
판다"다. 바꾸면 다음이 함께 흔들린다.

- **1,000골드 목표.** 지금은 보물 2~5개면 이긴다. 여러 개를 모아 한 번에 팔면
  도달 속도가 달라진다.
- **체포 몰수.** 30% + 물건 손실로 바뀌면 `MatchResultEvaluator`와
  `docs/03_GAME_RULES.md`를 함께 고쳐야 한다. `CLAUDE.md`가 "코드만 바꾸지 않는다"고
  적어 둔 항목이다.
- **`LootCarryType`.** 이동 페널티를 없애면 휴대 방식 4단계(주머니·한 손·양손·짐짝)의
  존재 이유가 사라진다. 낙하 소음이 그 값을 쓰고 있으니 함께 봐야 한다.

## 6. 티켓 후보 (의존 순서)

1. **인벤토리 저장소** — 25칸, 1칸 1개. `ISlotContainer`를 구현해 교환 UI와 이동
   로직을 그대로 쓴다. 뷰(`BindInventorySlots`)를 저장소 기준으로 바꾼다.
2. **아이템 판매 데이터** — `Sellable`·`SellPrice`를 어디에 둘지. `LootDefinition`
   확장이냐 새 SO냐. 희귀도 가격과의 관계.
3. **드래그 앤 드롭** — `EventSystem` 배선, 드래그 고스트 레이어, 드롭 판정.
   `GameplayInputRouter`의 마우스 입력과 공존 방식.
4. **너구리 거래 UI** — 좌 인벤토리 / 우 판매 목록. `SearchableContainer`가 쓰는
   `IHoldInteractable` 패턴을 따른다.
5. **원자적 판매** — 검증 → 총액 → 차감 → 화폐 증가 → 퀵슬롯 정리. 호스트 권한.
6. **체포 몰수 규칙 변경** — 30% + 100원어치 물건. `03_GAME_RULES.md` 동시 개정.

1과 2가 나머지 전부를 막는다. 3은 1 없이는 옮길 대상이 없다.

## 7. 이번 세션에서 이미 한 것 (중복 금지)

- `LootTable` / `LootRoller` / `SearchableContainer` / `ISlotContainer` /
  `ContainerTransfer` — Loot Table 4개 에셋, `Validate Loot Setup` 포함
- 교환 UI를 고양이 전용에서 임의 컨테이너로 일반화
- 결과 화면을 **보는 사람 기준** 승리/패배로 (목업 4장 = 승자 × 보는 사람)
- 아이콘 크기를 슬롯의 88%로, `preserveAspect` 켬
- 결과 화면 배지 중복 제거 (밴드 그림에 이미 올바른 배지가 있다)

## 8. 남아 있는 미해결 버그 (작업자 보고)

- **감옥에 갔다 오면 고양이를 찾을 수 없다.** 미조사.
- **모든 UI가 경찰 화면에서 진행된다.** 역할 전달 경로(`OverrideRole` → 씬 셀렉터
  → HUD `ActiveRole`)는 코드상 정상. **두 프로세스를 각각 다른 `-logFile`로 띄워야**
  어느 기계가 무엇을 커밋했는지 구분된다.
- **경찰이 던지기를 볼 수 없다.** `ThrowTrajectoryPreview`가 씬에서 **어느
  플레이어에게도 붙지 않는다** — 그러면 둘 다 못 보는 것이라 증상과 안 맞는다.
  퀵슬롯이 비어 보이는 것인지, 조준선이 없는 것인지 확인이 필요하다.
- Play Mode 실패 7건 (`ISSUE-054`). 실내 4건은 단일 메시 모델 문제.
