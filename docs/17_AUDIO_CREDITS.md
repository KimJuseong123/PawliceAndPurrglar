# 17. 사운드 출처와 라이선스

받은 파일을 **받는 즉시** 한 줄 추가한다. 나중에 어느 파일이 어느 라이선스였는지
되짚는 것은 사실상 불가능하고, CC-BY의 표기 의무를 놓치면 배포할 수 없다.

이 문서는 사운드의 **파일별 상세**를 담는다. 모델·폰트·엔진까지 포함한 전체 목록과
제출물과 함께 배포되는 고지는 [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)에
있다. 사운드를 추가하면 **양쪽 다** 갱신한다.

## 효과음

**전부 CC0.** freesound에서 CC0 필터로만 받았다. CC0는 표기 의무가 없으므로 URL은
기록하지 않았다 — 다만 같은 소리를 다시 찾거나 더 나은 것으로 교체할 때는 URL이 있는
편이 편하다. 다음에 받는 것은 URL도 함께 적는다.

| 파일 | 이벤트 | 라이선스 | 길이 | 비고 |
|---|---|---:|---:|---|
| `sfx_command_ok.wav` | `CommandSucceeded` | CC0 | 0.20초 | 원본 3.0초를 잘랐다 (아래 참고) |
| `sfx_command_fail.wav` | `CommandFailed` | CC0 | 0.60초 | **볼륨 0.6** (아래 참고) |
| `sfx_loot_pickup.wav` | `LootAcquired` | CC0 | 1.85초 | |
| `sfx_loot_sold.wav` | `LootSold` | CC0 | 1.72초 | |
| `sfx_arrest_start.wav` | `ArrestStarted` | CC0 | 1.52초 | |
| `sfx_arrest_done.mp3` | `ArrestCompleted` | CC0 | — | |
| `sfx_dog_bark.flac` | `DogBark` | CC0 | — | |
| `sfx_cat_meow.wav` | `CatMeow` | CC0 | — | |
| `sfx_victory.wav` | `Victory` | CC0 | 1.81초 | |
| `sfx_defeat.wav` | `Defeat` | CC0 | 2.00초 | |

### 2026-08-09에 들어온 11종 — **출처와 라이선스 미기재**

C-4~C-11에서 받은 것들이다. 위의 열 개와 달리 **어디서 왔는지 기록이 없이**
전달받았다. CC0라고 적지 않은 이유는 그것을 확인한 사람이 없기 때문이다 —
`SUBMIT-005`가 P0이고 공모전 요강이 출처 명시를 요구하므로, **제출 전에 열한 줄을
채워야 한다.**

| 파일 | 이벤트 | 라이선스 | 길이 | 비고 |
|---|---|---:|---:|---|
| `sfx_door_open.wav` | `DoorOpen` | **미확인** | 1.79초 | 문 여닫기·실내 출입 공용 |
| `sfx_inventory_toggle.mp3` | `InventoryToggle` | **미확인** | 0.70초 | |
| `sfx_jump.wav` | `Jump` | **미확인** | 1.72초 | |
| `sfx_purchase_ok.wav` | `PurchaseMade` | **미확인** | 2.20초 | |
| `sfx_countdown_tick.wav` | `CountdownTick` | **미확인** | 2.99초 | 한 박자가 아니라 **카운트다운 전체**다. 3초 카운트 시작에 한 번만 낸다 |
| `sfx_role_assigned_police.wav` | `RoleAssignedPolice` | **미확인** | 0.80초 | |
| `sfx_role_assigned_thief.wav` | `RoleAssignedThief` | **미확인** | 1.87초 | |
| `sfx_raccoon.wav` | `RaccoonChitter` | **미확인** | 0.79초 | |
| `sfx_voice_start.wav` | `VoiceRecordStart` · `PeerJoined` | **미확인** | 0.25초 | 두 이벤트가 같은 녹음을 쓴다 |
| `sfx_voice_stop.wav` | `VoiceRecordStop` · `PeerLeft` | **미확인** | 0.25초 | 같음 |
| `sfx_voice_ready.wav` | `VoiceModelReady` | **미확인** | 0.09초 | |

### 2026-08-09에 들어온 16종 (C-1~C-3) — **출처와 라이선스 미기재**

위와 같다. 어디서 왔는지 기록 없이 전달받았으므로 CC0라고 적지 않았다.

| 파일 | 이벤트 | 라이선스 | 길이 | 비고 |
|---|---|---:|---:|---|
| `sfx_chicken_squawk.flac` | `NoisePropSquawk` | **미확인** | 2.00초 | |
| `sfx_fuse_burn.wav` | `NoisePropFuse` | **미확인** | 8.10초 | **도화선은 2.5초다. 5.6초가 남아 폭발 뒤까지 탄다** |
| `sfx_firework_bang.wav` | `NoisePropBang` | **미확인** | 7.06초 | **받은 파일이 아니라 만든 파일이다** (아래 참고) |
| `sfx_bang_far.wav` | `NoiseHeardFar` | **미확인** | 5.00초 | 8m 밖에서 들리는 같은 폭발 |
| `sfx_throw_charge.ogg` | `ThrowCharge` | **미확인** | 2.59초 | 완충이 1.0초다 |
| `sfx_throw_release.wav` | `ThrowReleased` | **미확인** | 2.08초 | |
| `sfx_throw_hit_body.wav` | `ThrowHitBody` | **미확인** | 0.33초 | |
| `sfx_stunned.wav` | `Stunned` | **미확인** | 2.56초 | 돌 기절이 1.2초다. **돌 명중에만 울린다** |
| `sfx_ink_splat.wav` | `Blinded` | **미확인** | 0.46초 | |
| `sfx_trap_place.wav` | `TrapPlaced` | **미확인** | 3.08초 | |
| `sfx_banana_slip.wav` | `TrapSlip` | **미확인** | 2.28초 | |
| `sfx_sensor_trip.wav` | `SensorTripped` | **미확인** | 3.40초 | 노출이 2.5초다 |
| `sfx_lure_eat.wav` | `LureTaken` | **미확인** | 5.00초 | 미끼가 4초다 |
| `sfx_loot_search.wav` | `LootPickupStart` | **미확인** | 2.28초 | |
| `sfx_glass_break.mp3` | `GlassBreak` | **미확인** | 0.84초 | |
| `sfx_case_unlock.wav` | `CaseKeyUnlock` | **미확인** | 14.23초 | **여는 데 2.8초다. 11.4초가 남는다 — 목록에서 가장 심하다** |

#### 이어 붙인 것 — `sfx_firework_bang`

폭죽은 `sfx_firework_bang_1.mp3`(2.06초)와 `sfx_firework_bang_2.wav`(5.00초) 두 개로
왔고 **반드시 순서대로** 나야 한다. 지금 오디오 계층은 `PlayOneShot` 하나뿐이라 같은
프레임에 두 번 부르면 겹쳐 나오고, 첫 번째 길이만큼 뒤에 두 번째를 예약하면 그 간격을
누군가 계속 맞춰야 한다.

그래서 **파일을 합쳐 순서를 파일의 성질로 만들었다.** ID 하나, 클립 하나, 맞출 타이밍
없음. `Merge Firework Bang Clips`(`FireworkBangMerge`)가 48kHz 스테레오로 리샘플해
잇고 이음매에 5ms 크로스페이드를 넣는다 — 두 녹음을 그냥 붙이면 파형에 계단이 생기고
그건 듣는 사람이 매번 알아채는 딸깍 소리다.

원본 두 개는 `Assets/_Project/ArtSource/Audio/`에 남겨 다시 만들 수 있게 했다.
`Audio/SFX/`에 두지 않은 이유는 그 폴더의 모든 파일이 뱅크가 가리키는 소리로
기대되는데, 폭죽의 3분의 2는 그것이 아니기 때문이다.

#### 쓰지 않은 것 — `sfx_electric_stunned.wav`

`sfx_stunned.wav`가 같이 왔고 그쪽을 썼다. 여분은 **저장소에 넣지 않았다** — 아무도
가리키지 않는 파일은 빌드 용량만 차지한다. 이쪽이 낫다고 판단되면 `sfx_stunned.wav`를
**같은 이름으로 덮어쓴다.** 새 이름으로 넣으면 뱅크가 옛 파일을 계속 가리킨다.

#### 아직 녹음이 없는 3종

`sfx_dog_alert`, `sfx_cat_alert`, `sfx_glue_stick`. **코드는 완성돼 있고** 파일만 넣으면
동작한다. `Assign Sound Bank Clips`이 돌 때마다 세 줄로 경고하는 것이 남은 목록이다.
두 알림음은 명령 수락음(`sfx_dog_bark`·`sfx_cat_meow`)과 **달라야 한다** — 동물이 소리를
알아챈 것과 명령을 받은 것이 같게 들리면 안 된다.

---

`VoiceRecognizeFail`은 새 파일이 아니라 기존 `sfx_command_fail.wav`를 그대로 쓴다.
`ValuablePocketed`(실내 소품)도 마찬가지로 기존 `sfx_loot_pickup.wav`이고,
고양이가 물건을 건네줄 때는 기존 `sfx_cat_meow.wav`다.

### 확장자를 고친 이력

받은 파일은 전부 `.mp3`였지만 실제로 MP3인 것은 `sfx_arrest_done` 하나뿐이었다. 나머지
여덟 개는 WAV, `sfx_dog_bark`는 FLAC이다. Unity는 확장자로 임포터를 고르므로 실제 포맷에
맞춰 이름을 바꿨다. 뱅크 매핑은 확장자 없이 이름만 적고 코드가 찾는다.

### 잘라낸 것

`sfx_command_ok`는 원본이 3.0초였다. 동물 명령은 몇 초마다 눌리는 동작이라 자기 소리와
겹쳐서, 앞 0.2초만 남기고 마지막 25ms를 페이드아웃했다. 잘린 파형을 그냥 끊으면 딸깍
소리가 나고, 그건 듣는 사람이 매번 알아채는 유일한 잡음이다.

ffmpeg이 없고 scipy는 24비트 WAV를 쓰지 못해서 바이트 단위로 잘랐다 — 헤더가 1초당
바이트 수를 알려주므로 data 청크를 자르고 크기 필드만 고치면 된다.

### 볼륨을 낮춘 것

`CommandFailed`를 1.0에서 **0.6**으로 낮췄다. 이 소리는 쿨타임 중에 명령을 다시 누를
때마다 나는데, 그건 플레이어가 가장 자주 하는 실수라 경기 내내 가장 많이 들리는
소리가 된다. 실기에서 거슬린다는 지적이 나왔다.

볼륨은 `GameSoundBank.asset`의 엔트리별 값이고 `PlayOneShot(clip, GetVolume(id))`으로
실제 재생에 쓰인다. `EnsureAllSoundIds`가 엔트리를 다시 만들 때도 기존 값을 읽어
보존하므로 씬을 재생성해도 살아남는다.

### 지운 것

`sfx_alarm_siren.mp3`는 지웠다. `sfx_arrest_done`이 들어와 쓰이지 않게 됐고, 저장소에
있던 두 파일 중 유일하게 출처와 라이선스가 기록돼 있지 않았다. 라이선스를 모르는 파일을
남겨 둘 이유가 없다.

## 배경음악

| 트랙 | 용도 | 생성 도구 | 요금제 | 상업 사용 |
|---|---|---|---|---|
| — | — | — | — | — |

Suno로 만들 경우 **어느 요금제에서 생성했는지**를 적는다. 무료(Basic) 생성물은 비상업
용도로 제한되므로, 제출물에 쓸 것은 유료 구독 기간에 만든 것이어야 한다.

## 받을 때 규칙

1. **CC0를 우선한다.** freesound 검색에서 라이선스 필터를 먼저 건다.
2. CC-BY는 가능하지만 이 표에 작성자·URL을 적는다.
3. **CC-BY-NC는 받지 않는다.** 공모전 수상·배포가 상업으로 해석될 수 있다.
4. 파일명은 `sfx_<무엇>.mp3`로 바꿔 넣는다. 원본 파일명은 이 표의 URL로 추적한다.
5. 넣는 위치는 `Assets/_Project/Audio/SFX/`이고, `Assign Sound Bank Clips` 메뉴의
   매핑에 한 줄 추가한다 — 인스펙터에서 손으로 꽂으면 재현되지 않는다.
