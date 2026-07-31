# 16. 사운드 계획

효과음과 배경음악의 목록, 그리고 각 소리가 **어느 코드 이벤트에 붙는지**를 적는다.
소리를 고르기 전에 붙일 자리를 정하는 것이 목적이다 — 자리가 없는 소리는 나중에
"어디서 울려야 하지"를 다시 고민하게 된다.

## 현재 상태 (사실 확인)

| 항목 | 상태 |
|---|---|
| `GameSoundId` | **10종 정의됨** |
| `GameSoundBank` | 슬롯 10개, **클립 0개** |
| `GameSoundService.Request` 호출 | **10종 전부 배선됨** (`GameSoundObserver`) |
| 저장소의 오디오 파일 | 2개 (`ArtSource/Blender/sound_effects/`, 고양이 울음·사이렌) |

즉 **배선은 끝나 있고 빈 것은 클립뿐이다.** `GameSoundObserver`가 명령 수락·거부,
보물 획득, 판매, 체포 시작·완료, 동물 소리, 승패를 전부 이벤트에 붙여 두었다.

(이 문서의 첫 판은 "재생 호출 0곳"이라고 적었다. `GameSoundId` 참조를 찾을 때
`Core/Audio`를 제외해서 배선 파일 자체를 빼고 센 것이다.)

규칙은 `GameSoundId`를 이름으로 올리기만 하고 `AudioSource`를 만지지 않는다. 클립이
없거나 믹서가 음소거여도 경기 판정이 달라지지 않는다 — 이 단방향 규칙은 유지한다.

## A. 이미 정의된 10종 — 클립만 채우면 되는 것

배선은 되어 있으므로 `Assets/_Project/Audio/SFX/`에 파일을 넣고
`Paws & Loot/Setup/Assign Sound Bank Clips`의 매핑에 한 줄 추가하면 바로 울린다.
현재 2/10 (고양이 울음, 사이렌—임시).

| ID | 붙는 이벤트 | freesound 검색어 |
|---|---|---|
| `CommandSucceeded` | 동물 명령 수락 (`CompanionCommandDispatcher`) | ui confirm blip, positive chime |
| `CommandFailed` | 쿨타임·범위 밖 거부 | ui error, negative buzz short |
| `LootAcquired` | `LootStateMachine` → Carried | pickup coin, item grab cloth |
| `LootSold` | `ThiefLootWallet.SaleAmountChanged` | cash register, coins drop |
| `ArrestStarted` | `ArrestProgress.TargetEntered` | handcuff click, whistle short |
| `ArrestCompleted` | `ArrestProgress.ArrestCompleted` | handcuff lock, police whistle |
| `DogBark` | 강아지 명령·추적 | dog bark small, puppy yip |
| `CatMeow` | 고양이 명령 | cat meow (**보유 파일 있음**) |
| `Victory` | `MatchResultArbiter.ResultDecided` (승) | victory jingle short |
| `Defeat` | 같은 이벤트 (패) | fail jingle, sad trombone short |

## B. 이벤트는 있는데 사운드 ID가 없는 것 — ID 추가 필요

코드에 이미 이벤트가 있으므로 붙이는 비용이 낮다. 우선순위 순.

### 추격의 핵심 (P0)

| 새 ID | 이벤트 | 검색어 | 비고 |
|---|---|---|---|
| `ThrowReleased` | `ThrowResolver` 투척 | whoosh short, throw swing | 손을 떠나는 순간 |
| `ThrowHit` | `ThrowFlightTracker.ApplyHit` | impact thud soft, hit body | |
| `ThrowMissed` | 벽·바닥 착탄 | stone hit concrete | 맞았는지 소리로 구분되어야 한다 |
| `Stunned` | `StunState.Stunned(float)` | dizzy stars, cartoon daze | 별 4개 연출과 짝 |
| `SensorTripped` | `FlashlightVisibility.RevealFor` | alarm beep, motion sensor | **경찰에게만** 들려야 한다 |
| `TrapTriggered` | `PlacedTrap.Triggered` | splat sticky, banana slip | 바나나·끈끈이 구분 |
| `ArrestInterrupted` | `ProgressInterrupted` | ui cancel, cloth rustle | 방해 사유 4종 공용 |

### 이동 (P1)

| 새 ID | 이벤트 | 검색어 | 비고 |
|---|---|---|---|
| `FootstepStone` | `PlayerMovementMotor` 보행 주기 | footstep concrete single | 거리 |
| `FootstepWood` | 같은 위치, 실내 | footstep wood single | 실내 바닥 |
| `Jump` | `PlayerMovementMotor.TryJump` | jump grunt cartoon | |
| `Land` | `IsAirborne` 해제 | land thud light | |
| `Dash` | `TryStartDash` | dash whoosh, quick swish | 쿨타임 5초라 자주 안 울린다 |
| `LadderStep` | `LadderTraversal.ClimbStarted` | ladder climb metal | |

### 문과 실내 (P1)

| 새 ID | 이벤트 | 검색어 |
|---|---|---|
| `DoorOpen` | `HouseDoorLeaf.Swing` | door open wooden creak |
| `DoorClose` | 열림 유지 시간 종료 | door close soft |
| `EnterInterior` | `PlayerInteriorState` 진입 | room tone shift, muffle transition |
| `ExitInterior` | 퇴장 | 같은 것의 역방향 |
| `ValuablePocketed` | `InteriorValuablePickup` | jewel pickup, small chime |
| `FurnitureSearch` | 옷장·서랍 상호작용 (예정) | drawer open wooden |

### 경제와 시계 (P2)

| 새 ID | 이벤트 | 검색어 |
|---|---|---|
| `PurchaseMade` | `PoliceWallet.AmountChanged` 감소 | cash register small |
| `PurchaseDenied` | 잔액 부족 | ui error soft |
| `CountdownTick` | `MatchState.Countdown` | countdown beep |
| `MatchStart` | Countdown → Playing | whistle start, bell |
| `TimeWarning` | 남은 30초 | clock ticking tense |
| `TimerExpired` | `TimerExpired` | buzzer end |

### UI·로비 (P2)

| 새 ID | 이벤트 | 검색어 |
|---|---|---|
| `UiClick` | 버튼 (`SceneNavigationButton`) | ui click soft |
| `PeerJoined` | `NetworkSessionController` 접속 | ui join blip |
| `PeerLeft` | `NetworkDisconnectHandler` | ui leave blip |
| `RematchRequested` | `NetworkRematchCoordinator` | ui ready chime |
| `MerchantGreeting` | `RaccoonBinGreeter` | raccoon chitter, critter squeak |

## C. 앞으로 추가할 기능에 어울리는 것

백로그에 있고 아직 구현 전인 것들. 기능이 들어올 때 함께 정하면 된다.

| 기능 | 필요한 소리 | 검색어 |
|---|---|---|
| 음성 명령 (STT) | 녹음 시작·종료, 인식 실패 | mic open beep, radio squelch |
| 바나나 (`THROW-006`) | 미끄러짐, 껍질 착지 | banana slip cartoon |
| 감옥 (`THROW-012`) | 철문, 열쇠 | jail door slam, key jingle |
| 수갑 소모품 (`THROW-013`) | 채우는 소리 | handcuff ratchet |
| 루프탑 이동 | 지붕 발소리 (기와/금속) | footstep roof tile |
| 은신처 | 숨는 소리, 심장박동 | cloth hide, heartbeat loop |
| 너구리 변수 | 쓰레기통 뒤짐 | trash can rummage |
| 손전등 | 켜기·끄기 | flashlight click |

## D. 배경음악 (Suno)

경기가 4분이므로 긴 곡은 필요 없다. **루프**로 만들고, 상태 전환에서 크로스페이드한다.

| 트랙 | 길이 | 성격 | 프롬프트 방향 |
|---|---|---|---|
| 로비 | 60초 루프 | 가볍고 기대감 | playful cartoon caper, light jazz, upbeat, loopable |
| 경기 기본 | 90초 루프 | 긴장 낮음, 반복 견딤 | sneaky cartoon chase, walking bass, muted trumpet |
| 추격 고조 | 60초 루프 | 경찰이 도둑을 볼 때 | frantic cartoon chase, fast drums, brass stabs |
| 실내 | 60초 루프 | 조용, 답답함 | tense quiet, muffled, minimal percussion |
| 결과 (승) | 15초 | 짧게 | triumphant cartoon fanfare short |
| 결과 (패) | 15초 | 짧게 | comic defeat, deflating |

전환 지점은 이미 있다 — `MatchStateChanged`(로비/카운트다운/경기/결과),
`PlayerInteriorState.InteriorChanged`(실내), `FlashlightVisibility.IsRevealed`(고조).

## E. 라이선스 — 확인이 필요한 부분

### freesound.org

**라이선스가 파일마다 다르다.** 검색 시 반드시 필터를 걸어야 한다.

| 라이선스 | 쓸 수 있나 | 조건 |
|---|---|---|
| **CC0** | 예, 제약 없음 | 없음 — **이걸 우선한다** |
| **CC-BY 4.0** | 예 | **출처 표기 필수**. 크레딧 파일에 작성자·파일명·URL |
| **CC-BY-NC** | ⚠️ 비상업만 | 공모전 수상·배포가 상업으로 해석될 수 있다. **피하는 것이 안전** |
| Sampling+ (구) | ⚠️ | 조건이 모호하다. 피한다 |

권장: **CC0로 필터링**해서 모으고, 꼭 필요한 것만 CC-BY로 받는다. 받는 즉시
`docs/17_AUDIO_CREDITS.md`에 한 줄씩 적는다 — 나중에 어느 파일이 어느 라이선스였는지
되짚는 것은 사실상 불가능하다.

### Suno AI

**요금제에 따라 다르다.**

- **무료(Basic)**: 생성물은 **비상업 용도만**. 공모전 제출은 위험하다.
- **유료(Pro / Premier)**: 유료 구독 기간에 생성한 것은 상업적 사용이 허용된다.
  단 구독을 해지해도 이미 만든 것의 권리는 유지되는지 **약관에서 직접 확인**해야 한다.

추가로 알아둘 것: 순수 AI 생성물은 한국·미국 모두 **저작권 보호를 받지 못한다**는 것이
현재 판단이다. 사용에는 문제가 없지만 "우리가 저작권을 가진다"고 주장할 수는 없다.
공모전 규정에 **AI 생성물 사용 가능 여부**와 **제출물 권리 귀속** 조항이 있는지 먼저
읽어야 한다 — 이건 기술이 아니라 규정 문제라서 확인 없이는 답할 수 없다.

### 결론

- 효과음 freesound: **좋은 선택.** CC0 우선, CC-BY는 크레딧, CC-BY-NC는 피한다.
- 배경음악 Suno: **유료 구독으로 만들면 괜찮다.** 무료로 만든 것을 제출물에 쓰는 것은
  피한다. 공모전 규정의 AI 조항을 먼저 확인한다.

## 작업 순서 제안

1. `docs/17_AUDIO_CREDITS.md`를 먼저 만든다 (빈 표). 파일을 받을 때마다 한 줄 추가.
2. A의 10종을 채우고 재생 호출을 붙인다 — 구조는 이미 있으므로 이것만으로 게임에 소리가
   생긴다.
3. B의 P0(추격 핵심) 7종을 ID와 함께 추가한다. 여기까지가 "소리로 상황을 안다"의 최소선.
4. 발소리는 그 다음이다. 좋아지긴 하지만 없어도 정보가 빠지지 않는다.
5. BGM은 마지막. 효과음이 자리를 잡은 뒤에 음량 균형을 맞추는 것이 순서다.
