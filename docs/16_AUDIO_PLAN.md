# 16. 사운드 계획

효과음과 배경음악의 목록, 그리고 각 소리가 **어느 코드 이벤트에 붙는지**를 적는다.
소리를 고르기 전에 붙일 자리를 정하는 것이 목적이다 — 자리가 없는 소리는 나중에
"어디서 울려야 하지"를 다시 고민하게 된다.

## 현재 상태 (2026-08-06 실측)

| 항목 | 상태 |
|---|---|
| `GameSoundId` | **10종 정의됨** |
| `GameSoundBank` 엔트리 | 10개, **클립 10/10 할당 완료** |
| `GameSoundService.Request` 호출 | 10종 전부 배선됨 (`GameSoundObserver`) |
| `Assets/_Project/Audio/SFX/` | 파일 10개 (전부 CC0) |
| `musicTrack` | **비어 있음.** 저장소에 BGM 파일 0개, `Audio/Music/` 폴더 없음 |

효과음 10종은 끝났다. 남은 것은 **아래 C의 94종과 BGM 6트랙**이다.

> 이 문서의 이전 판은 "클립 0개, 저장소 오디오 파일 2개"라고 적고 있었다. `AUDIO-001`
> 2차 작업으로 10개가 다 들어온 뒤에도 갱신되지 않은 것이다. 그리고 그보다 앞선 판은
> "재생 호출 0곳"이라고 적었다 — `GameSoundId` 참조를 찾을 때 `Core/Audio`를 제외해서
> 배선 파일 자체를 빼고 센 것이다. **상태 표는 셀 때마다 실측한다.**

규칙은 `GameSoundId`를 이름으로 올리기만 하고 `AudioSource`를 만지지 않는다. 클립이
없거나 믹서가 음소거여도 경기 판정이 달라지지 않는다 — 이 단방향 규칙은 유지한다.

## A. 완료된 10종

파일은 [`Assets/_Project/Audio/SFX/`](../Assets/_Project/Audio/SFX)에 있고, 파일명↔ID
매핑은 [`SoundBankSetup.cs`](../Assets/_Project/Editor/SoundBankSetup.cs)의 `Mapping`
배열에 적혀 있다. 라이선스와 길이는 [17_AUDIO_CREDITS.md](17_AUDIO_CREDITS.md).

| ID | 파일 | 붙는 이벤트 |
|---|---|---|
| `CommandSucceeded` | `sfx_command_ok.wav` | `CompanionCommandDispatcher.CommandAccepted` |
| `CommandFailed` | `sfx_command_fail.wav` | `.CommandRejected` |
| `LootAcquired` | `sfx_loot_pickup.wav` | `LootCarrier.HeldLootChanged` |
| `LootSold` | `sfx_loot_sold.wav` | `ThiefLootWallet.SaleAmountChanged` |
| `ArrestStarted` | `sfx_arrest_start.wav` | `ArrestProgressController.IsProgressing` 상승 에지 |
| `ArrestCompleted` | `sfx_arrest_done.mp3` | `ArrestCompletionController.ArrestCompleted` |
| `DogBark` | `sfx_dog_bark.flac` | 강아지 명령 수락 |
| `CatMeow` | `sfx_cat_meow.wav` | 고양이 명령 수락, `DistractionBoard.DistractionStarted` |
| `Victory` | `sfx_victory.wav` | `MatchEndController.MatchEndingStarted` (승) |
| `Defeat` | `sfx_defeat.wav` | 같은 이벤트 (패) |

## B. 클립을 받기 전에 고쳐야 하는 것 — 재생 구조의 한계

**아래 C의 상당수는 클립을 구해도 울리지 않는다.** `GameSoundService`가 하는 일은
공유 `AudioSource` 하나에 `PlayOneShot` 하는 것뿐이다. 목록을 채우기 전에 이것을
먼저 본다 — 클립을 다 받아 놓고 붙일 데가 없는 것을 발견하는 것이 최악이다.

| 한계 | 무엇이 막히는가 | 필요한 것 |
|---|---|---|
| **3D 위치가 없다.** 2D 소스 하나뿐 | 22m 밖의 폭죽과 발밑의 폭죽이 같은 음량으로 들린다. `NoiseHeardFar`를 별도 클립으로 두는 설계 자체가 성립하지 않는다 | 위치를 받는 오버로드(`Request(id, Vector3)`)와 소스 풀 |
| **루프 채널이 없다.** `musicSource`만 `loop = true` | 도화선 지글거림, 발소리, 분수, 앰비언스 | 시작·정지가 있는 루프 채널 |
| **피치 랜덤이 없다** | 발소리가 같은 파형 반복이라 기계처럼 들린다 | `pitch` 흔들기 (±5% 정도) |
| **역할별 청취가 없다** | `SensorTripped`는 경찰에게만 들려야 하는데 구분할 자리가 없다 | 요청 시 대상 역할 지정 |
| **0.08초 중복 억제가 전역이다** | 발소리처럼 의도적으로 빠른 반복과, 프레임마다 올라오는 사고를 같은 규칙으로 다룬다 | ID별 억제 시간 |

그리고 **`GameSoundId`에 값을 추가하면 뱅크 에셋을 다시 만들어야 한다.**
`Validate Sound Bank`가 엔트리 없는 ID에서 예외를 던지므로, enum만 늘리고 에셋을
그대로 두면 검증이 깨진다. `GameSoundBank.EnsureAllSoundIds()`를 호출하는 경로를
함께 손댄다.

## C. 추가할 효과음 94종

붙는 자리는 코드에서 확인한 이벤트·컴포넌트만 적었다. 관련 모델을 함께 적은 것은
소리를 고를 때 **무엇이 화면에 보이는지**가 톤을 정하기 때문이다 — 같은 "충격음"이
돌과 냉동 문어에서 전혀 다른 소리여야 한다.

### C-1. 구현이 끝났는데 소리만 없는 것 (P0, 6종)

고무닭(`ITEM-003`)과 폭죽(`ITEM-004`)은 **하는 일이 소리를 내는 것뿐**인데 그 소리가
없다. 지금은 노란 링만 퍼지므로 이 두 소품은 절반만 존재한다.

| ID 후보 | 붙는 자리 | 모델 | 길이 | 검색어 | 주의 |
|---|---|---|---:|---|---|
| `NoisePropSquawk` | `PlacedTrap.Triggered` (RubberChicken) | `throwable_rubber_chicken` | 0.4~0.8초 | `rubber chicken squeak`, `squeaky toy` | 한 번 삑 하고 끝난다. 길게 우는 것은 어디서 났는지 헷갈린다 |
| `NoisePropFuse` | `ToolUseAction.Placed` (Firework) | `throwable_firework` | 2.5초+ | `fuse burning`, `sparkler hiss` | **끊기지 않고 2.5초를 채운다** (`FireworkFuseSeconds`). 짧으면 침묵이 생겨 이미 터진 줄 안다 |
| `NoisePropBang` | 도화선 만료 | 같음 | 1.0~2.0초 | `firecracker pop`, `bottle rocket burst` | 총성처럼 들리는 것은 피한다. 코믹한 톤 |
| `NoiseHeardFar` | `NoiseBoard.Heard`, 8m 초과 | — | 1.0~2.0초 | `distant firework`, `muffled explosion` | **같은 소재의 다른 녹음**이어야 거리가 읽힌다 |
| `DogAlerted` | `CompanionNoiseAttention` | `dog.fbx` | 0.3~0.6초 | `dog perk up` | `DogBark`와 달라야 한다 |
| `CatAlerted` | 같음 | `cat.fbx` | 0.3~0.6초 | `cat chirp` | `CatMeow`와 달라야 한다 |

#### 거리에 따라 다른 클립을 쓰는 이유

소음이 **22m를 간다** (`ThrowableCatalog.NoiseRadiusMeters`, 손전등의 17m보다 일부러
멀다). 그 범위 안이면 전부 같은 음량으로 들리는 것이 규칙이고 의도한 것이다 —
멀수록 작아지면 "들리긴 하는데 아무것도 할 수 없는 소리"가 되고 소품의 경계선을
아무도 배울 수 없다.

대신 **가까이(≤8m)와 멀리(>8m)에 다른 클립**을 쓰면 거리감이 산다. 음량을 줄이는
것과 다른 녹음을 쓰는 것은 귀에 전혀 다르게 들린다. 단 이건 B의 3D 위치 문제가
풀려야 판정할 수 있다.

#### 폭죽에는 소리가 두 번 필요하다

도화선과 터지는 소리가 **다른 사건**이다. 도화선은 놓은 사람 근처에서만 들리고
(설치자가 자기 위치를 노출하는 값), 터지는 소리는 22m를 간다. 하나로 합치면 폭죽을
설치하는 위험이 화면에도 소리에도 나타나지 않는다.

### C-2. 투척과 피격 (P0, 13종)

| ID 후보 | 붙는 자리 | 모델 | 검색어 |
|---|---|---|---|
| `ThrowCharge` | `ThrowChargeController` | — | `bow draw soft`, `charge up short` |
| `ThrowReleased` | `ToolUseAction.Thrown` | — | `whoosh short`, `throw swing` |
| `ThrowHitBody` | `ThrowFlightTracker` 피격 | — | `impact thud soft` |
| `ThrowMissGround` | 벽·바닥 착탄 | `throwable_rock` | `stone hit concrete` |
| `Stunned` | `StunState.Stunned` | 기절 별 4개 연출과 짝 | `dizzy stars`, `cartoon daze` |
| `Blinded` | `BlindedState.Blinded` (`ITEM-008`) | `throwable_octopus` | `wet slap splat` |
| `BlindCleared` | `BlindedState.Cleared` | 같음 | `cloth peel off` |
| `TrapPlaced` | `ToolUseAction.Placed` | — | `set down soft` |
| `TrapSlip` | `PlacedTrap.Triggered` (Banana) | `throwable_banana` | `banana slip cartoon` |
| `TrapSticky` | 같음 (GlueTrap, 3초 고정) | 아트 없음 | `splat sticky`, `tape peel` |
| `SensorTripped` | `FlashlightVisibility` 노출 시작 | 아트 없음 | `motion sensor beep` |
| `LureTaken` | `CompanionLure.LureStarted` | `throwable_tuna_can`, `throwable_bone` | `animal eating`, `sniff` |
| `ArrestInterrupted` | `ArrestProgressController.ProgressInterrupted` | — | `ui cancel`, `cloth rustle` |

`SensorTripped`는 **경찰에게만** 들려야 한다. 도둑이 자기가 센서를 밟은 것을 소리로
알면 센서등이 도둑에게 정보를 주는 소품이 되어 버린다. B의 역할별 청취가 필요하다.
`TrapSlip`과 `TrapSticky`는 구분되어야 한다 — 미끄러진 것(1초)과 붙잡힌 것(3초)은
플레이어가 다르게 대응해야 하는 상황이다.

### C-3. 보물·진열장·경보 (P0~P1, 11종)

`ITEM-002`·`006`·`007`·`009`가 전부 DONE인데 소리가 하나도 없다.

| ID 후보 | 붙는 자리 | 모델 | 검색어 |
|---|---|---|---|
| `LootPickupStart` | `LootPickupProgress.Started` | 보물 22종 | `cloth grab`, `rummage short` |
| `LootPickupInterrupted` | `LootPickupProgress.Interrupted` | — | `ui cancel soft` |
| `GlassBreak` | `LootDisplayCase.Broken` (1.1초 깨기) | `object_shop_display` | `display case break`, `glass smash` |
| `CaseKeyUnlock` | 열쇠로 여는 경로 | `item_case_key` | `key jingle`, `lock click` |
| `AlarmSiren` | `LootAlarm.Raised` (30m) | `loot_diamond_ring` | `alarm siren short` |
| `DropLight` | `ITEM-009` 낙하, 가벼운 것 | `loot_wallet`, `loot_book` | `light object drop` |
| `DropMetal` | 같음, 금속 | `loot_gold_bar`, `loot_cash_drawer` | `heavy metal drop concrete` |
| `DropGlass` | 같음, 유리 | `loot_liquor_bottle` | `glass bottle smash` |
| `LootHidden` | `LootHidingSpot.LootHidden` | `object_trash_can`, `object_wooden_crate`, `object_cardboard_box` | `cardboard rustle`, `lid close` |
| `LootRecovered` | `.LootRecovered` | 같음 | 위의 역방향 |
| `MarketRevealed` | `BlackMarketDraw` (5곳 중 2곳만 열림) | `object_secret_market_stall` | `curtain open`, `low whistle` |

**주머니 휴대는 소리가 없어야 한다** (`ITEM-009` 규칙 — 반경을 휴대 방식에서 끌어오고
주머니는 0이다). 낙하음을 무게 무관하게 하나로 두면 이 규칙이 소리에서 사라진다.

### C-4. 문과 실내 (P1, 11종)

실내 모델 8개(`interior_*.fbx`)가 들어왔는데 실내 전용 소리가 0개다.

| ID 후보 | 붙는 자리 | 모델 | 검색어 |
|---|---|---|---|
| `DoorOpen` | `HouseDoorLeaf` 스윙 | `building_house_1f/2f` | `door open wooden creak` |
| `DoorClose` | 열림 유지 시간 종료 | 같음 | `door close soft` |
| `DoorLocked` | `HouseDoorway` 거부 | 같음 | `door locked rattle` |
| `EnterInterior` | `PlayerInteriorState.InteriorChanged` | `interior_*` 8종 | `room tone shift`, `muffle transition` |
| `ExitInterior` | 같음 | 같음 | 위의 역방향 |
| `ValuablePocketed` | `InteriorValuablePickup.Taken` | — | `jewel pickup`, `small chime` |
| `SearchStart` | `SearchableContainer` 상호작용 | `trashcan_lid` | `drawer open wooden` |
| `SearchCompleted` | `ContainerTransfer.SearchCompleted` | 같음 | `drawer close`, `found chime` |
| `TakeAll` | `TakeAllPressed` | — | `multiple items grab` |
| `InventoryToggle` | `InventoryTogglePressed` | — | `bag open soft` |
| `EscapeHatchUse` | `InteriorEscapeHatch` | — | `window slide`, `hatch open` |

### C-5. 이동 (P1, 12종)

| ID 후보 | 붙는 자리 | 모델 | 검색어 |
|---|---|---|---|
| `FootstepStone` | `PlayerMovementMotor` 보행 주기 | `env_road_*` | `footstep concrete single` |
| `FootstepGrass` | 같음, 잔디 | `env_grass_tile` | `footstep grass single` |
| `FootstepWood` | 같음, 실내 | `interior_*` | `footstep wood single` |
| `FootstepRoof` | 루프탑 | — | `footstep roof tile` |
| `Jump` | `PlayerMovementMotor.TryJump()` | — | `jump grunt cartoon` |
| `Land` | `IsAirborne` 해제 | — | `land thud light` |
| `Dash` | `TryStartDash()` | — | `dash whoosh` |
| `LadderMount` | `LadderTraversal.ClimbStarted` | `object_ladder` | `ladder grab metal` |
| `LadderStep` | 등반 중 주기 | 같음 | `ladder climb metal` |
| `LadderDismount` | `.ClimbFinished` | 같음 | `step off ledge` |
| `CarryStrain` | `SetLootCarryPenalty` 짐짝 단계 | `loot_watermelon`, `loot_cash_drawer` | `heavy breathing effort` |
| `CompanionPaws` | 동물 보행 | `dog.fbx`, `cat.fbx` | `small animal steps` |

발소리는 **양쪽 역할이 같은 소리를 내면 안 되는지 먼저 정한다.** 도둑이 조용해야
한다는 규칙은 `03_GAME_RULES.md`에 없다 — 있으면 클립이 두 배가 되고, 없으면 하나로
쓴다. 이건 밸런스 결정이므로 클립을 받기 전에 답이 있어야 한다.

### C-6. 경찰 장비와 감옥 (P1~P2, 8종)

| ID 후보 | 붙는 자리 | 모델 | 검색어 |
|---|---|---|---|
| `FlashlightOn` | `PoliceFlashlight` | — | `flashlight click on` |
| `FlashlightOff` | 같음 | — | `flashlight click off` |
| `NightVisionShift` | `NightVisionFill` | — | `low hum sweep` |
| `PurchaseMade` | `PoliceSupplyCounter` 성공 | `building_supermarket` | `cash register small` |
| `PurchaseDenied` | 잔액 부족·범위 밖 | 같음 | `ui error soft` |
| `JailDoorClose` | `ThiefJailState.Jailed` | `interior_jail` | `jail door slam` |
| `JailRelease` | `.Released` (11초 후) | 같음 | `jail door open`, `key turn` |
| `HandcuffRatchet` | `THROW-013` (TODO) | — | `handcuff ratchet` |

감옥은 11초다. **그 11초 동안 도둑에게 아무 소리도 없으면 화면이 멈춘 것과 구분되지
않는다.** 닫히는 소리와 열리는 소리만으로는 부족할 수 있고, 그 사이를 채우는 것은
루프(B의 루프 채널)이므로 함께 검토한다.

### C-7. 경기 흐름 (P2, 5종)

| ID 후보 | 붙는 자리 | 검색어 |
|---|---|---|
| `CountdownTick` | `MatchRuntimeState.StateChanged` → Countdown | `countdown beep` |
| `MatchStart` | Countdown → Playing | `whistle start`, `bell` |
| `TimeWarning` | 남은 30초 | `clock ticking tense` |
| `TimerExpired` | `MatchRuntimeState.TimerExpired` | `buzzer end` |
| `ArrestTally` | `ArrestCompletionController.CatchCountChanged` | `tally chime rising` |

승리에 체포가 **3회** 필요하므로 `ArrestTally`는 1/3·2/3·3/3이 올라가는 톤이어야
한다. 같은 소리를 세 번 울리면 몇 번째인지 세야 하고, 그건 화면을 봐야 알 수 있는
정보를 소리가 중복해서 말하는 것뿐이다.

### C-8. UI·로비·네트워크 (P2, 9종)

| ID 후보 | 붙는 자리 | 검색어 |
|---|---|---|
| `UiClick` | `SceneNavigationButton` | `ui click soft` |
| `UiHover` | 같음 | `ui hover tick` |
| `UiBack` | `EscapePressed` | `ui back soft` |
| `RoleAssigned` | `NetworkRoleBoard.RoleAssignmentChanged` | `ui assign chime` |
| `PeerJoined` | `NetworkSessionController.ModeChanged` | `ui join blip` |
| `PeerLeft` | `NetworkDisconnectHandler` | `ui leave blip` |
| `RoomFound` | LAN 방 발견 | `ui discover tick` |
| `RematchRequested` | `NetworkRematchCoordinator` | `ui ready chime` |
| `ResultReveal` | 결과 화면 진입 | `reveal swoosh` |

### C-9. 동물 연출 (P1, 10종)

`ART-016`으로 표정 아이콘 4개가 들어왔는데 소리가 없다.

| ID 후보 | 붙는 자리 | 모델 | 검색어 |
|---|---|---|---|
| `IconAlert` | 발견 표시 | `icon_alert` | `alert ping short` |
| `IconThinking` | 이해 중 | `icon_thinking` | `thinking blip` |
| `IconHappy` | 성공 | `icon_happy` | `happy chirp` |
| `IconConfused` | 거부 | `icon_confused` | `confused warble` |
| `CompanionIdle` | `CompanionIdleBehaviour.ActionStarted` | `dog`, `cat` | `animal yawn`, `stretch` |
| `CatSteal` | `CompanionLootCourier.LootPickedUp` | `cat.fbx` | `cat grab cloth` |
| `CatDeliver` | `.LootDelivered` | 같음 | `drop item soft` |
| `DogSniff` | Track 명령 | `dog.fbx` | `dog sniffing` |
| `RaccoonChitter` | `RaccoonBinGreeter` | `raccoon.fbx` | `raccoon chitter`, `critter squeak` |
| `TrashRummage` | 같음 | `object_trash_can` | `trash can rummage` |

`IconHappy`/`IconConfused`는 `CommandSucceeded`/`CommandFailed`와 **겹칠 위험이
있다.** 명령 수락과 동물이 이해했다는 표시가 거의 동시에 오므로, 둘 다 울리면 두 번
삑 하는 것으로만 들린다. 아이콘 소리를 넣을 때 기존 두 개를 줄이거나 없애는 것까지
같이 결정한다.

### C-10. 환경 앰비언스 (P2, 5종)

전부 루프이므로 B의 루프 채널이 먼저다.

| ID 후보 | 붙는 자리 | 모델 | 검색어 |
|---|---|---|---|
| `FountainLoop` | 분수·호수 근처 | `env_fountain_plaza`, `env_fountain_garden`, `env_lake_garden` | `fountain water loop` |
| `NightAmbience` | 경기 시작 | 밤 맵 | `night crickets loop` |
| `StreetLampHum` | 가로등 근처 | `env_street_lamp` | `lamp electrical hum` |
| `WindTrees` | 숲·나무 근처 | `env_tree`, `env_tree_round`, `env_forest` | `wind leaves loop` |
| `InteriorRoomTone` | 실내 체류 | `interior_*` | `room tone quiet loop` |

### C-11. 음성 (보류, 4종)

STT가 보류이므로 마지막이다. 더킹 훅(`GameSoundService.SetVoiceCaptureActive`)은 이미
있다.

| ID 후보 | 붙는 자리 | 검색어 |
|---|---|---|
| `VoiceRecordStart` | `VoiceCommandInput.StateChanged` | `mic open beep` |
| `VoiceRecordStop` | 같음 | `mic close beep` |
| `VoiceRecognizeFail` | `.ErrorReceived` | `radio squelch` |
| `VoiceModelReady` | `LocalAiProcessManager.StateChanged` | `soft ready chime` |

## D. 배경음악 (Suno)

**저장소에 파일이 하나도 없고 `musicTrack`이 비어 있다.** 경기가 4분이므로 긴 곡은
필요 없다. **루프**로 만들고 상태 전환에서 크로스페이드한다.

| 트랙 | 길이 | 성격 | 프롬프트 방향 |
|---|---|---|---|
| 로비 | 60초 루프 | 가볍고 기대감 | playful cartoon caper, light jazz, upbeat, loopable |
| 경기 기본 | 90초 루프 | 긴장 낮음, 반복 견딤 | sneaky cartoon chase, walking bass, muted trumpet |
| 추격 고조 | 60초 루프 | 경찰이 도둑을 볼 때 | frantic cartoon chase, fast drums, brass stabs |
| 실내 | 60초 루프 | 조용, 답답함 | tense quiet, muffled, minimal percussion |
| 결과 (승) | 15초 | 짧게 | triumphant cartoon fanfare short |
| 결과 (패) | 15초 | 짧게 | comic defeat, deflating |

전환 지점은 이미 있다 — `MatchStateChanged`(로비/카운트다운/경기/결과),
`PlayerInteriorState.InteriorChanged`(실내), `FlashlightVisibility`(고조). 다만
`GameSoundService`는 `musicSource` 하나에 클립 하나를 물릴 뿐이므로 **크로스페이드
자체가 없다.** 트랙 6개를 받기 전에 두 번째 음악 소스와 페이드가 필요하다.

## E. 라이선스

### freesound.org

**라이선스가 파일마다 다르다.** 검색 시 반드시 필터를 걸어야 한다.

| 라이선스 | 쓸 수 있나 | 조건 |
|---|---|---|
| **CC0** | 예, 제약 없음 | 없음 — **이걸 우선한다** |
| **CC-BY 4.0** | 예 | **출처 표기 필수**. 크레딧 파일에 작성자·파일명·URL |
| **CC-BY-NC** | ⚠️ 비상업만 | 공모전 수상·배포가 상업으로 해석될 수 있다. **피하는 것이 안전** |
| Sampling+ (구) | ⚠️ | 조건이 모호하다. 피한다 |

권장: **CC0로 필터링**해서 모으고, 꼭 필요한 것만 CC-BY로 받는다. 받는 즉시
[17_AUDIO_CREDITS.md](17_AUDIO_CREDITS.md)와
[THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) **양쪽에** 한 줄씩 적는다 —
나중에 어느 파일이 어느 라이선스였는지 되짚는 것은 사실상 불가능하다.

기존 10개는 전부 CC0이지만 **URL을 기록하지 않았다.** 다음에 받는 것은 URL도 적는다
— 더 나은 것으로 교체하거나 같은 소리를 다시 찾을 때 필요하다.

### 받은 파일의 확장자를 믿지 않는다

기존 10개는 전부 `.mp3`로 받았는데 실제로 MP3인 것은 하나뿐이었다. 여덟 개는 WAV,
하나는 FLAC이다. Unity는 **확장자로 임포터를 고르므로** 잘못된 확장자는 조용히
실패한다. 받은 뒤 실제 포맷을 확인해서 이름을 바꾼다. 뱅크 매핑은 확장자 없이
이름만 적고 코드가 찾는다 (`SoundBankSetup.Extensions`).

### Suno AI

**요금제에 따라 다르다.**

- **무료(Basic)**: 생성물은 **비상업 용도만**. 공모전 제출은 위험하다.
- **유료(Pro / Premier)**: 유료 구독 기간에 생성한 것은 상업적 사용이 허용된다.
  단 구독을 해지해도 이미 만든 것의 권리는 유지되는지 **약관에서 직접 확인**해야 한다.

추가로 알아둘 것: 순수 AI 생성물은 한국·미국 모두 **저작권 보호를 받지 못한다**는 것이
현재 판단이다. 사용에는 문제가 없지만 "우리가 저작권을 가진다"고 주장할 수는 없다.
공모전 규정에 **AI 생성물 사용 가능 여부**와 **제출물 권리 귀속** 조항이 있는지 먼저
읽어야 한다 — 이건 기술이 아니라 규정 문제라서 확인 없이는 답할 수 없다.

## 작업 순서

1. **B의 재생 구조를 먼저 손댄다.** 3D 위치와 루프 채널이 없으면 C-1의 도화선과
   `NoiseHeardFar`부터 붙을 자리가 없다. 클립을 다 받아 놓고 붙일 데가 없는 것을
   발견하는 것이 최악의 순서다.
2. **C-1의 6종.** 이미 구현이 끝났고 소리만 비어 있다 — 고무닭과 폭죽은 소리를 내는
   것 외에 하는 일이 없다.
3. **C-3의 진열장·경보 4종** (`GlassBreak`, `CaseKeyUnlock`, `AlarmSiren`,
   `LootPickupStart`). `ITEM-006`·`007`도 같은 처지다.
4. **C-2의 13종.** 여기까지 **23종**이 "소리로 상황을 안다"의 최소선이다.
5. C-9의 동물 연출, C-4의 문·실내. 아이콘 4종을 넣을 때 기존
   `CommandSucceeded`/`CommandFailed`와 겹치는지 함께 판단한다.
6. 발소리(C-5)는 그 다음이다. 좋아지긴 하지만 없어도 정보가 빠지지 않는다.
7. BGM은 마지막. 효과음이 자리를 잡은 뒤에 음량 균형을 맞추는 것이 순서다.
   크로스페이드가 없다는 것을 먼저 해결한다.
