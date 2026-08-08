# 23. 효과음 수집 작업표 (94종)

`docs/16_AUDIO_PLAN.md` C절의 94종과 2026-08-08 freesound CC0 조사를 합친
**받으러 갈 때 손에 들고 가는 표**다. 한 줄에 네 가지가 있다.

| 칸 | 뜻 |
|---|---|
| **ID** | `GameSoundId` enum에 추가할 값 |
| **파일 이름** | `Assets/_Project/Audio/SFX/`에 넣을 이름. **확장자는 무엇이든 된다** |
| **사용처** | 이 소리가 나는 코드 이벤트. 이게 맞아야 소리가 제자리에서 난다 |
| **검색어** | freesound CC0 필터에 넣을 말 |

후보 sound ID와 링크는 `Downloads/freesound-cc0-shortlist.md`에 있다. 이 문서는
**그 목록을 대체하지 않고** 파일 이름과 검색어를 채운다.

---

## 0. 시작하기 전에 — 순서가 있다

### 0-1. 재생 구조가 먼저인 것이 있다

`docs/16` B절이 지적한 대로 지금은 **2D 소스 하나에 `PlayOneShot`뿐**이다. 그래서:

- **C-10 앰비언스 5종**과 `sfx_fuse`(도화선)는 **루프 채널이 없으면 붙일 자리가 없다**
- `sfx_bang_far`는 **3D 위치가 없으면** 가까운 폭발과 구분되지 않는다
- 감옥 11초 루프도 마찬가지다

받는 것 자체는 지금 해도 되지만, **붙는 것은 구조 다음이다.**

### 0-2. 받는 순서

```text
C-1 (구현 끝났는데 소리만 없음)  →  C-3 (보물·경보)  →  C-2 (투척·피격)
→  C-4 (문·실내)  →  C-5 (이동)  →  나머지
```

C-1이 먼저인 이유는 **고무닭과 폭죽이 하는 일이 소리를 내는 것뿐**이라서다. 지금
그 둘은 절반만 존재한다.

### 0-3. 톤을 하나로 묶는 법

94종을 94명에게서 받으면 소리가 콜라주가 된다. **LilMati**(CC0 590개)가
`Cartoon, Stunned 01~05`, `Cartoon, Dizzy 01~06`, `Select, Granted 01~06`,
`Select, Denied 01~03` 같은 **번호 붙은 세트**를 갖고 있다. 기절·UI·판정음을 이
사람 하나로 덮으면 톤이 붙는다.

번호 변형이 있다는 것도 이득이다 — `docs/16` B가 지적한 "피치 랜덤이 없다"를
클립 여러 개를 돌려 쓰는 것으로 대신할 수 있다.

```text
https://freesound.org/search/?f=license%3A%22Creative+Commons+0%22+username%3A%22LilMati%22
```

### 0-4. 검색 URL 만드는 법

아래 표의 검색어를 그대로 넣는다. 공백은 `+`로 바뀐다.

```text
https://freesound.org/search/?q=<검색어>&f=license%3A%22Creative+Commons+0%22
```

**CC0 필터를 반드시 건다.** `docs/16` E절이 CC-BY는 표기하면 가능, CC-BY-NC는 금지로
정해 뒀다. 필터 없이 고르면 나중에 라이선스 때문에 다시 골라야 한다.

---

## 1. 파일 이름 규칙

```text
Assets/_Project/Audio/SFX/sfx_<무엇>_<어떻게>.<확장자>
```

- `sfx_` 접두사 + **소문자** + `_` 구분
- 확장자는 `.wav` `.mp3` `.ogg` `.flac` `.aiff` `.aif` 중 무엇이든 된다.
  `SoundBankSetup.Extensions`가 그 순서로 찾으므로 **이름만 맞으면 된다**
- **교체할 때는 같은 이름으로 덮어쓴다.** 새 이름으로 넣으면 뱅크가 옛 파일을
  계속 가리킨다. 확장자가 달라지는 것은 괜찮다

### 새 소리를 추가할 때 손대는 세 곳 — 하나라도 빠지면 조용히 실패한다

| 순서 | 파일 | 하는 일 |
|---|---|---|
| 1 | `Assets/_Project/Scripts/Core/Audio/GameSoundId.cs` | enum에 값 추가 |
| 2 | `Assets/_Project/Editor/SoundBankSetup.cs`의 `Mapping` | `(ID, "파일이름")` 한 줄 |
| 3 | 뱅크 에셋 | `Rebuild MAP-001` 또는 `EnsureAllSoundIds()` |

3번을 빼면 `Validate Sound Bank`가 예외를 던진다. **그게 의도다** — enum에만 있고
뱅크에 없는 ID는 영원히 소리가 안 나는데 로그도 안 남는다.

넣은 뒤 한 번 실행한다:

```text
PawliceAndPurrglar > Setup > Assign Sound Bank Clips
```

콘솔에 `[AUDIO-001] Victory ← sfx_victory (3.00s)`처럼 **ID · 파일 · 길이**가
찍힌다. 길이가 찍히는 이유는 그것이 가장 자주 틀리고 아무도 확인하지 않는 값이기
때문이다.

---

## C-1. 구현이 끝났는데 소리만 없다 (P0, 6종) — **여기부터**

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `NoisePropSquawk` | `sfx_chicken_squawk` | 고무닭을 밟아 터뜨렸을 때 (`PlacedTrap.Triggered`, RubberChicken) | `rubber chicken` · `squeaky toy` · `squeeze toy` |
| `NoisePropFuse` | `sfx_fuse_burn` | 폭죽을 놓은 순간부터 터지기 전까지 지글거리는 심지 (`ToolUseAction.Placed`) | `fuse burning` · `sparkler` · `sizzle fuse` |
| `NoisePropBang` | `sfx_firework_bang` | 심지가 다 타서 폭죽이 터지는 순간 | `firecracker` · `firework pop` (**총성처럼 들리는 것 피한다**) |
| `NoiseHeardFar` | `sfx_bang_far` | 같은 폭발이 **8m보다 먼 곳**에서 들릴 때 (`NoiseBoard.Heard`) | `distant explosion` · `far explosion` |
| `DogAlerted` | `sfx_dog_alert` | 개가 소음에 반응해 경계 자세로 바뀔 때 (`CompanionNoiseAttention`) | `dog whine` · `dog whimper` |
| `CatAlerted` | `sfx_cat_alert` | 고양이가 같은 소음에 반응할 때 | `cat trill` · `cat chirp` |

> **`sfx_fuse_burn`은 길이를 반드시 본다.** 도화선은 2.5초를 **끊기지 않고** 채워야
> 한다. 검색 결과 페이지는 길이를 안 보여주므로 개별 페이지에서 확인한다.
>
> **`sfx_bang_far`는 `sfx_firework_bang`과 다른 녹음이어야 한다.** 같은 파일을
> 볼륨만 줄여 쓰면 거리가 읽히지 않는다. 이것이 클립을 둘로 나눈 이유 전부다.
>
> **`sfx_dog_alert`는 기존 `sfx_dog_bark`와 달라야 한다.** 후자는 명령 수락음이다.

## C-3. 보물·진열장·경보 (P0~P1, 11종)

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `LootPickupStart` | `sfx_loot_search` | 보물을 줍기 시작할 때 (`LootPickupProgress.Started`) | `rummaging` · `searching drawer` |
| `LootPickupInterrupted` | `sfx_loot_cancel` | 줍는 도중 중단됐을 때 (`.Interrupted`) | `denied` · `error beep` |
| `GlassBreak` | `sfx_glass_break` | 진열장 유리를 1.1초간 깨부술 때 (`LootDisplayCase.Broken`) | `glass smash` · `glass break` |
| `CaseKeyUnlock` | `sfx_case_unlock` | 진열장을 열쇠로 여는 경로 | `keys jingle` · `lock click` |
| `AlarmSiren` | `sfx_alarm_siren` | 경보 보물을 들어올려 경보 발동 (`LootAlarm.Raised`) | `alarm siren` · `burglar alarm` |
| `DropLight` | `sfx_drop_light` | 지갑·책 등 **가벼운** 보물 낙하 (ITEM-009) | `light object drop` · `book drop` · `wallet drop` |
| `DropMetal` | `sfx_drop_metal` | 금괴·현금서랍 등 **금속** 보물 낙하 | `metal clang drop` · `metal object fall` |
| `DropGlass` | `sfx_drop_glass` | 술병 등 **유리** 보물 낙하 | `glass bottle break` |
| `LootHidden` | `sfx_loot_hide` | 쓰레기통·상자에 보물을 숨길 때 (`LootHidingSpot.LootHidden`) | `cardboard box rustle` |
| `LootRecovered` | `sfx_loot_unhide` | 숨긴 보물을 꺼낼 때 (`.LootRecovered`) | 같음 (다른 변형을 쓴다) |
| `MarketRevealed` | `sfx_market_open` | 암시장 5곳 중 2곳이 열릴 때 (`BlackMarketDraw`) | **아래 "못 찾은 9종" 참고** |

## C-2. 투척과 피격 (P0, 13종)

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `ThrowCharge` | `sfx_throw_charge` | 조준하며 힘을 모으는 동안 (`ThrowChargeController`) | `bow draw` · `stretch tension` |
| `ThrowReleased` | `sfx_throw_release` | 던지는 순간 (`ToolUseAction.Thrown`) | `whoosh short` · `swoosh` |
| `ThrowHitBody` | `sfx_throw_hit_body` | 던진 것이 캐릭터를 맞췄을 때 (`ThrowFlightTracker`) | `dull thud` · `body impact` |
| `ThrowMissGround` | `sfx_throw_hit_ground` | 빗나가 벽·바닥에 떨어졌을 때 | `concrete hit` · `stone impact` |
| `Stunned` | `sfx_stunned` | 기절 진입 — 별 4개와 짝 (`StunState.Stunned`) | `cartoon stunned` · `cartoon dizzy` (**LilMati 세트**) |
| `Blinded` | `sfx_ink_splat` | 냉동 문어에 맞아 시야가 가려질 때 (`BlindedState.Blinded`) | `wet splat` · `splat` |
| `BlindCleared` | `sfx_ink_clear` | 가렸던 시야가 풀릴 때 (`.Cleared`) | `tape peel` · `packing tape` |
| `TrapPlaced` | `sfx_trap_place` | 함정을 바닥에 설치하는 순간 (`ToolUseAction.Placed`) | **아래 "못 찾은 9종" 참고** |
| `TrapSlip` | `sfx_banana_slip` | 바나나를 밟고 미끄러질 때 (`PlacedTrap.Triggered`, Banana) | `cartoon slip` · `slip whistle` |
| `TrapSticky` | `sfx_glue_stick` | 끈끈이에 3초간 붙잡힐 때 (GlueTrap) | `sticky tape` · `cellotape peel` |
| `SensorTripped` | `sfx_sensor_trip` | 센서를 건드려 노출이 시작될 때 | `ping` · `beep up` · `detect` |
| `LureTaken` | `sfx_lure_eat` | 동물이 미끼를 먹을 때 (`CompanionLure.LureStarted`) | `cat eating` · `dog sniffing` |
| `ArrestInterrupted` | `sfx_arrest_cancel` | 체포 중 도둑이 벗어났을 때 (`ArrestProgressController.ProgressInterrupted`) | `denied` · `buzzer short` |

> **연출은 이미 있고 소리만 없는 것들이다.** `sfx_stunned`는 별 4개(`StunStarsView`),
> `sfx_banana_slip`은 캐릭터 회전(`SlipSpinView`), `sfx_ink_splat`은 먹물
> 오버레이(`InkBlindOverlayView`), `sfx_glue_stick`·`sfx_sensor_trip`은 화면 배너
> (`PlayerStatusBannerView`)와 짝이다. **소리가 붙는 순간이 그 연출이 뜨는 순간과
> 같아야 한다.**

## C-4. 문과 실내 (P1, 11종)

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `DoorOpen` | `sfx_door_open` | 집 문을 여는 스윙 (`HouseDoorLeaf`) | `wooden door creak open` |
| `DoorClose` | `sfx_door_close` | 열림 유지 시간이 끝나 자동으로 닫힐 때 | `door close` · `metal door close` |
| `DoorLocked` | `sfx_door_locked` | 잠긴 문에 진입이 거부될 때 (`HouseDoorway` 거부) | `door locked` · `handle rattle` |
| `EnterInterior` | `sfx_enter_interior` | 실내로 들어갈 때 (`PlayerInteriorState.InteriorChanged`) | `room tone` · `indoor ambience` |
| `ExitInterior` | `sfx_exit_interior` | 실내에서 나갈 때 (같은 이벤트, 반대 방향) | 같음 (역방향 페이드) |
| `ValuablePocketed` | `sfx_valuable_pocket` | 실내 소품을 주울 때 (`InteriorValuablePickup.Taken`) | `coin pickup` · `plingy coin` |
| `SearchStart` | `sfx_search_start` | 서랍·통을 뒤지기 시작할 때 (`SearchableContainer`) | `opening drawer` |
| `SearchCompleted` | `sfx_search_done` | 뒤지기가 끝났을 때 (`ContainerTransfer.SearchCompleted`) | `drawer open close` |
| `TakeAll` | `sfx_take_all` | "모두 가져가기" 버튼 (`TakeAllPressed`) | **아래 "못 찾은 9종" 참고** |
| `InventoryToggle` | `sfx_inventory_toggle` | 인벤토리를 열고 닫을 때 (`InventoryTogglePressed`) | `bag zipper` · `backpack zip` |
| `EscapeHatchUse` | `sfx_escape_hatch` | 실내 탈출 창문/해치 (`InteriorEscapeHatch`) | `window slide` |

## C-5. 이동 (P1, 12종)

> **먼저 정할 것**: 경찰과 도둑이 **같은 발소리를 내도 되는가.** 답이 "아니오"면
> 발소리 클립 수가 두 배가 된다. `docs/16`이 이 질문을 열어 뒀다.

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `FootstepStone` | `sfx_step_stone` | 도로(`env_road_*`) 위 보행 주기 (`PlayerMovementMotor`) | `concrete footstep` |
| `FootstepGrass` | `sfx_step_grass` | 잔디(`env_grass_tile`) 위 보행 | `grass footstep` |
| `FootstepWood` | `sfx_step_wood` | 실내(`interior_*`) 나무 바닥 보행 | `wood floor footstep` |
| `FootstepRoof` | `sfx_step_roof` | 지붕/옥상 위 보행 | **`sfx_step_stone`으로 대체 가능한지 먼저 판단** |
| `Jump` | `sfx_jump` | 점프 입력 (`PlayerMovementMotor.TryJump()`) | `jump` · `jump grunt` |
| `Land` | `sfx_land` | 공중 상태가 풀리며 착지 (`IsAirborne` 해제) | `land thud` · `landing` |
| `Dash` | `sfx_dash` | 대시 발동 (`TryStartDash()`) | `whoosh` (**`sfx_throw_release`와 다른 것으로**) |
| `LadderMount` | `sfx_ladder_mount` | 사다리를 잡고 오르기 시작 (`LadderTraversal.ClimbStarted`) | `ladder climb` |
| `LadderStep` | `sfx_ladder_step` | 오르는 도중 반복되는 주기음 | `climbing ladder` · `wooden ladder step` |
| `LadderDismount` | `sfx_ladder_dismount` | 사다리에서 내려올 때 (`.ClimbFinished`) | **아래 "못 찾은 9종" 참고** |
| `CarryStrain` | `sfx_carry_strain` | 무거운 보물(짐짝 단계)을 들고 이동할 때 | `heavy breathing` · `strain grunt` |
| `CompanionPaws` | `sfx_paws` | 개·고양이의 걷는 발소리 | **아래 "못 찾은 9종" 참고** |

## C-6. 경찰 장비와 감옥 (P1~P2, 8종)

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `FlashlightOn` | `sfx_flashlight_on` | 손전등을 켤 때 (`PoliceFlashlight`) | `flashlight click` |
| `FlashlightOff` | `sfx_flashlight_off` | 손전등을 끌 때 | `flashlight click off` |
| `NightVisionShift` | `sfx_nightvision` | 야간투시 필터 전환 (`NightVisionFill`) | `electric hum rise` |
| `PurchaseMade` | `sfx_purchase_ok` | 보급소 구매 성공 (`PoliceSupplyCounter`) | `cash register` |
| `PurchaseDenied` | `sfx_purchase_fail` | 잔액 부족·범위 밖 거부 | `denied` · `error` |
| `JailDoorClose` | `sfx_jail_close` | 체포되어 감옥에 갇힐 때 (`ThiefJailState.Jailed`) | `jail door close` · `cell door` |
| `JailRelease` | `sfx_jail_open` | 감옥 11초가 지나 풀려날 때 (`.Released`) | `prison cell door open` |
| `HandcuffRatchet` | `sfx_handcuff` | 체포 순간 수갑 (`THROW-013`, 아직 TODO) | `handcuffs` · `cuffs locking` |

> 감옥 11초를 채우는 **루프는 여기 없다.** `docs/16` B의 루프 채널이 먼저다.

## C-7. 경기 흐름 (P2, 5종)

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `CountdownTick` | `sfx_countdown_tick` | 시작 전 카운트다운 매 초 | `countdown beep` |
| `MatchStart` | `sfx_match_start` | 카운트다운이 끝나고 경기 시작 | `referee whistle` |
| `TimeWarning` | `sfx_time_warning` | 남은 시간 30초 | `clock tick loop` |
| `TimerExpired` | `sfx_time_up` | 제한 시간 종료 (`MatchRuntimeState.TimerExpired`) | `buzzer` |
| `ArrestTally` | `sfx_arrest_tally` | 체포 횟수가 오를 때마다 (1/3·2/3·3/3) | `select granted` (**LilMati 01→03→06으로 음정 상승**) |

## C-8. UI·로비·네트워크 (P2, 9종)

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `UiClick` | `sfx_ui_click` | 버튼을 눌렀을 때 (`SceneNavigationButton`) | `ui click` · `button click` |
| `UiHover` | `sfx_ui_hover` | 버튼에 마우스를 올렸을 때 | `ui hover` · `menu click soft` |
| `UiBack` | `sfx_ui_back` | Esc 등으로 뒤로가기 | `menu back` · `cancel` |
| `RoleAssigned` | `sfx_role_assigned` | 로비에서 역할이 정해질 때 (`NetworkRoleBoard`) | `access granted` |
| `PeerJoined` | `sfx_peer_join` | 상대가 세션에 접속 (`NetworkSessionController.ModeChanged`) | `beep up` · `connect` |
| `PeerLeft` | `sfx_peer_left` | 상대가 세션을 나감 (`NetworkDisconnectHandler`) | `beep down` · `disconnect` |
| `RoomFound` | `sfx_room_found` | LAN에서 방을 발견 | `ping` · `notification ping` |
| `RematchRequested` | `sfx_rematch` | 결과 화면에서 재경기 요청 (`NetworkRematchCoordinator`) | `select granted` |
| `ResultReveal` | `sfx_result_reveal` | 결과 화면 진입 | `swoosh swirl` · `transition whoosh` |

## C-9. 동물 연출 (P1, 10종)

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `IconAlert` | `sfx_icon_alert` | 동물 머리 위 **발견** 아이콘 | `ping` · `alert blip` |
| `IconThinking` | `sfx_icon_think` | **이해하는 중** 아이콘 | `ui notification soft` |
| `IconHappy` | `sfx_icon_happy` | **성공** 아이콘 | `success chime` |
| `IconConfused` | `sfx_icon_confused` | **거부** 아이콘 | `cartoon confused` · `wtf` |
| `CompanionIdle` | `sfx_companion_idle` | 대기 동작(하품 등) 시작 (`CompanionIdleBehaviour.ActionStarted`) | **아래 "못 찾은 9종" 참고** |
| `CatSteal` | `sfx_cat_steal` | 고양이가 보물을 집을 때 (`CompanionLootCourier.LootPickedUp`) | **아래 "못 찾은 9종" 참고** |
| `CatDeliver` | `sfx_cat_deliver` | 고양이가 보물을 전달할 때 (`.LootDelivered`) | **아래 "못 찾은 9종" 참고** |
| `DogSniff` | `sfx_dog_sniff` | 개에게 추적(Track) 명령 | `dog sniffing` |
| `RaccoonChitter` | `sfx_raccoon` | 너구리 NPC 반응 (`RaccoonBinGreeter`) | `raccoon` |
| `TrashRummage` | `sfx_trash_rummage` | 너구리가 쓰레기통을 뒤질 때 | `trash can rummage` · `metal trash can` |

> **`IconHappy`/`IconConfused`가 기존 `CommandSucceeded`/`CommandFailed`와 겹친다.**
> `docs/16`이 지적한 문제가 여기서 실제로 걸린다 — 아이콘이 뜨는 순간이 명령이
> 수락/거부되는 순간과 같으므로, 그대로 두면 **같은 순간에 소리 두 개**가 난다.
> 둘 중 하나만 소리를 내게 하거나, 아이콘 쪽을 훨씬 작게 깔아야 한다.

## C-10. 환경 앰비언스 (P2, 5종) — **루프 채널이 먼저다**

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `FountainLoop` | `sfx_amb_fountain` | 분수·호수 근처에 있는 동안 | `fountain ambience loop` |
| `NightAmbience` | `sfx_amb_night` | 경기 시작부터 밤 맵 전체 | `crickets night` |
| `StreetLampHum` | `sfx_amb_lamp` | 가로등 근처에 있는 동안 | `electric hum 50hz` |
| `WindTrees` | `sfx_amb_wind` | 숲·나무 근처에 있는 동안 | `wind through leaves` |
| `InteriorRoomTone` | `sfx_amb_room` | 실내에 머무는 동안 | `room tone` |

> 다섯 개 전부 **끊김 없이 루프되는지**를 개별 페이지에서 확인한다. `Loopable`
> 태그가 붙은 것이 안전하다.

## C-11. 음성 (보류, 4종)

| ID | 파일 이름 | 사용처 | 검색어 |
|---|---|---|---|
| `VoiceRecordStart` | `sfx_voice_start` | 마이크 녹음 시작 (`VoiceCommandInput.StateChanged`) | `beep up` |
| `VoiceRecordStop` | `sfx_voice_stop` | 마이크 녹음 정지 | `beep down` |
| `VoiceRecognizeFail` | `sfx_voice_fail` | 음성 인식 실패 (`.ErrorReceived`) | `radio squelch` · `radio sign off` |
| `VoiceModelReady` | `sfx_voice_ready` | 로컬 AI 모델 로딩 완료 | `ready chime` |

---

## 못 찾은 9종 — 검색어를 바꿔 본다

CC0 필터에서 쓸 만한 것이 안 나온 것들이다. **원래 검색어가 물건 이름이라서 안
잡힌 경우가 많다.** 소리를 물건이 아니라 **동작**으로 다시 적으면 나온다.

| ID | 파일 이름 | 안 나온 검색어 | 대신 써 볼 검색어 | 최후 수단 |
|---|---|---|---|---|
| `TrapPlaced` | `sfx_trap_place` | `set down soft` | `cloth drop` · `soft thump` · `place object table` | `sfx_loot_hide`를 짧게 잘라 재사용 |
| `MarketRevealed` | `sfx_market_open` | `curtain open` | `fabric swoosh` · `tarp rustle` · `awning` | `sfx_result_reveal`(swoosh) 재사용 |
| `TakeAll` | `sfx_take_all` | — | `multiple coins` · `items collect` · `sweep pickup` | `sfx_valuable_pocket`을 3연타 |
| `FootstepRoof` | `sfx_step_roof` | — | `metal roof step` · `tin roof` · `sheet metal walk` | **`sfx_step_stone`으로 대체 판단이 먼저** |
| `LadderDismount` | `sfx_ladder_dismount` | — | `jump down land` · `drop to ground` | `sfx_land` 재사용 |
| `CompanionPaws` | `sfx_paws` | — | `dog paws floor` · `claws on wood` · `animal walking` | 동물 발소리를 아예 빼는 것도 선택지 |
| `CompanionIdle` | `sfx_companion_idle` | `animal yawn` | `dog yawn` · `cat yawn` · `animal sigh` | 뺀다 |
| `CatSteal` | `sfx_cat_steal` | — | `cat pickup` → **없다.** `small cloth grab` · `fabric grab` | `sfx_loot_hide` 재사용 |
| `CatDeliver` | `sfx_cat_deliver` | — | `small item drop soft` · `set down light` | `sfx_drop_light` 재사용 |

### 후보가 약한 것 4종

| ID | 문제 | 대안 검색어 |
|---|---|---|
| `ThrowCharge` | CC0에 3건뿐 | `rubber stretch` · `elastic pull` · `tension creak` |
| `CatAlerted` | 사실상 1건 | `cat meow short` · `cat mrrp` · `kitten chirp` |
| `DropLight` | 후보가 빈약 | `book drop table` · `paper drop` · `light thud` |
| `DropMetal` | 후보가 빈약 | `metal clang` · `coins drop` · `metal bar drop` |

**CC-BY까지 허용할지는 결정이 필요하다.** `docs/16` E절이 CC-BY는 표기하면 가능,
CC-BY-NC는 금지로 적어 뒀다. 허용하면 위 9종의 선택지가 크게 넓어진다.

---

## 받은 뒤에 할 것 — 다섯 가지

1. **실제 포맷을 확인하고 확장자를 맞춘다.** freesound는 업로드 원본을 준다.
   `.wav`로 받아도 안이 다를 수 있다 — **기존 10개가 전부 그랬다.**
2. `Assets/_Project/Audio/SFX/`에 넣고 `SoundBankSetup.Mapping`에 한 줄 추가한다.
   **인스펙터에서 손으로 꽂으면 재현되지 않는다.**
3. **`.gitattributes`에 `*.flac`·`*.aiff`가 없다.** freesound에는 FLAC이 흔하고,
   추가하지 않으면 LFS를 우회해 저장소 본문에 쌓인다.
4. `GameSoundId`에 값을 추가했으면 뱅크 에셋을 다시 만든다 (`EnsureAllSoundIds`).
   enum만 늘리면 `Validate Sound Bank`가 깨진다.
5. `docs/17_AUDIO_CREDITS.md`와 `THIRD_PARTY_NOTICES.md` **양쪽에 URL까지** 적는다.
   CC0는 표기 의무가 없지만 **공모전 요강이 출처 명시를 요구한다** — 그리고 그것이
   제외 사유다 (`docs/18` 0-c).

> **소리를 듣기 전에 확정하지 않는다.** 이 표의 검색어와 후보는 제목·태그·업로더로
> 고른 것이다. "만화 톤이어야 한다"는 조건은 제목으로 판단할 수 없다.
