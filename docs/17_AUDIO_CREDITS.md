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
