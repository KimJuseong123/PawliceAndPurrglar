# 20. 감면(Decimate) 목록

**1·2·4순위는 완료했다** (2026-08-02). 이 PC의 Blender 5.2로
`Tools/decimate_fbx.py`를 헤드리스로 돌렸다. 남은 것은 3순위(실내·미사용 외관)이고,
그건 임포트될 때 하면 된다.

| | 이전 | 이후 |
|---|---:|---:|
| 샌드박스 씬 삼각형 | 3,061,747 | **396,852** |
| 샌드박스 빌드 에셋 | 140 MB | **35 MB** |
| 본 게임 빌드 에셋 | — | **41 MB** |

측정일: 2026-08-02. 모든 수치는 `Pawlice and Purrglar / Setup / Report Model Weights`가
실제 임포트된 메시를 세어 얻은 것이다. 파일 크기 짐작이 아니다.

## 왜 필요한가

생성기(Tripo)가 내보낸 모델은 거의 전부 **약 95만 삼각형**이다. 손으로 만든 것과
같은 실루엣을 스캔 밀도로 표현한 결과다. 화면에서는 3~5만 삼각형과 구분되지 않지만,
빌드 용량과 프레임에는 그대로 실린다.

목표는 **각 모델 3~5만 삼각형**이다. Blender의 Decimate(Collapse) 비율 약 0.04.

---

## 1순위 — 본 게임이 지금 쓰고 있는 것

`Game.unity`가 실제로 배치하는 모델이다. 여기가 가장 급하다.

| 모델 | 이전 → 이후 | Blender 파일 |
|---|---:|---|
| 느낌표 아이콘 | 983,743 → **2,000** | `expression icon 3d/warning 3d model/orange+exclamation+mark+3d+model.fbx` |
| 하트 아이콘 | 958,854 → **2,000** | `expression icon 3d/pink heart 3d model/pink+heart+3d+model.fbx` |
| 전구 아이콘 | 953,166 → **2,000** | `expression icon 3d/light bulb 3d model/light+bulb+3d+model.fbx` |
| 소용돌이 아이콘 | 951,692 → **2,000** | `expression icon 3d/dizzy 3d model/spiral+badge+3d+model.fbx` |

**합계 385만 삼각형.** 동물 머리 위에 뜨는 한 뼘짜리 아이콘 넷이다. 화면에서 차지하는
크기를 생각하면 **각 1,000~3,000 삼각형이면 충분하다** — 비율 0.002. 이 넷이 이
목록 전체에서 투자 대비 효과가 가장 크다.

## 2순위 — 샌드박스 맵이 쓰는 것 (본 게임 이식 예정)

| 모델 | 지금 | Blender 파일 |
|---|---:|---|
| 보석상 | 954,434 → **40,000** | `Buildings/jewelry shop 3d model/jewelry+shop+3d+model.fbx` |
| 분수 광장 | 945,538 → **40,000** | `environment/fountain plaza 3d model/fountain+plaza+3d+model.fbx` |
| 2층집 | 919,926 → **40,000** | `Buildings/two-story house 3d model/two-story+house+3d+model.fbx` |

**합계 282만.** 지금 샌드박스 빌드 에셋의 32MB가 이 셋이다.

## 3순위 — 아직 임포트 안 됐지만 들어올 것

전부 20MB대이므로 **각각 약 95만 삼각형**으로 보면 된다.

### 실내

| 용도 | Blender 파일 |
|---|---|
| 집 실내 1 | `building_inside/house01 interior 3d model/house+interior+3d+model.fbx` |
| 집 실내 2 | `building_inside/house02 interior 3d model/apartment+interior+3d+model.fbx` |
| 집 실내 3 | `building_inside/house03 interior 3d model/house+interior+3d+model.fbx` |
| 서점 실내 | `building_inside/bookstore interior 3d model/library+interior+3d+model.fbx` |
| 슈퍼마켓 실내 | `building_inside/grocery store 3d model/grocery+store+3d+model.fbx` |
| 보석상 실내 | `building_inside/jewelry store interior 3d model/jewelry+store+interior+3d+model.fbx` |
| 경찰서 실내 | `building_inside/police station 3d model/police+station+3d+model.fbx` |

실내는 **플레이어가 가장 가까이서 오래 보는 곳**이다. 다른 것보다 덜 깎아도 된다 —
10만 정도면 충분하고, 굳이 3만까지 내릴 필요는 없다.

### 건물 외관 (아직 안 쓰는 것)

| 모델 | Blender 파일 |
|---|---|
| 서점 외관 | `Buildings/bookstore 3d model/bookstore+3d+model.fbx` |
| 슈퍼마켓 외관 | `Buildings/grocery+store+3d+model/tripo_convert_c2c04816-….fbx` (29.3MB, 가장 무거움) |
| 집 01 | `Buildings/house01 3d model/house+3d+model.fbx` |
| 집 02 | `Buildings/house02 3d model/cozy+cottage+3d+model.fbx` |
| 집 03 | `Buildings/house03 3d model/modern+house+3d+model.fbx` |
| 집 04 | `Buildings/house04 3d model/miniature+house+3d+model.fbx` |

## 4순위 — 있으면 좋지만 급하지 않은 것

| 모델 | 지금 | Blender 파일 |
|---|---:|---|
| 가로등 | 96,594 → **5,000** | `environment/street lamp 3d model/street+lamp+3d+model.fbx` |
| 나무 (로우폴리) | 89,780 → **5,000** | `environment/low poly tree 3d model/low+poly+tree+3d+model.fbx` |
| 나무 (양식화) | 93,728 → **5,000** | `environment/stylized tree 3d model/stylized+tree+3d+model.fbx` |
| 외벽 돌담 | 46,980 → **3,000** | `environment/outer wall 3d model/stone+wall+3d+model.fbx` |
| 소화전 | 46,476 → **3,000** | `environment/fire hydrant 3d model/fire+hydrant+3d+model.fbx` |

이미 10만 이하라 급하지 않다. 다만 **가로등과 나무는 개수가 많다** — 지금 12개지만
맵이 커지면 늘어난다. 각 5,000으로 내리면 좋다.

---

## 하지 않아도 되는 것

### 도로 조각 — 빌드에는 안 들어가지만, 감면은 한다 (2026-08-03 수정)

각 95만 삼각형이고 **빌드에는 들어가지 않는다.** 위에서 한 번 구워 512px 그림으로
쓰고, 화면에 있는 것은 삼각형 2개짜리 평면이다. `Report Model Weights`가 이들을
`unused`로 보고하는 것이 그 증거다.

그래도 감면한다. **저장소에는 들어가기 때문이다** — 조각 하나가 25MB이고, 팀원이
받아야 하는 것도 그것이다. 4만으로 깎으면 1.3MB가 되고 구워진 그림은 같다.

| 모델 | 이전 → 이후 | Blender 파일 |
|---|---:|---|
| 십자 (02) | 963,022 → **40,000** | `environment/road segment 02 3d model/calibration+target+3d+model.fbx` |
| ㄱ자 (03) | 938,816 → **40,000** | `environment/_superseded/board+game+tile+3d+model.fbx` |
| T자 (05) | 976,278 → **40,000** | `environment/road segment 05 3d model/road+segment++05+3d+model.fbx` |
| 막다른 길 (06) | 987,484 → **40,000** | `environment/road segment 06 3d model/3d+road+tile+model.fbx` |

> **굽는 원본을 감면하려면 굽는 것이 Unlit이어야 한다.** 예전 굽기는 정면 직사광을
> 썼고, 그러면 위를 보는 면이 전부 밝기 곡선 끝에 앉는다. 감면이 남긴 법선의 미세한
> 흔들림이 그 절벽에서 떨어져 아스팔트가 흰 잡음으로 터졌다 (`ISSUE-059`).
> 지금은 조명을 끄고 알베도만 찍으므로 4만짜리와 95만짜리가 같은 그림으로 나온다.

**모델을 고치면 다시 구워야 한다** (`Bake Road Tile Textures`).

`road segment 03`으로 받은 압축 파일에는 **ㄱ자가 아니라 횡단보도가 들어 있었다**
(`crosswalk+tile+3d+model.fbx`). 이전 ㄱ자를 `_superseded/`에서 되살려 쓰고 있다.

### 이미 가벼운 것

경찰서(15,588), 서점(17,428), 슈퍼마켓(9,912), 1층집(7,648), 캐릭터 전부(5,000 이하).
손댈 이유가 없다.

---

## 숲과 호수 — 들어왔다 (2026-08-03)

한 구획을 통째로 채우는 세트 피스라 반복되지 않는다. 건물 외관과 같은 4만으로 잡았다.

| 모델 | 이전 → 이후 | Blender 파일 |
|---|---:|---|
| 숲 | 951,012 → **40,000** | `environment/forest 3d model/forest+clearing+3d+model.fbx` |
| 호수 공원 | 944,082 → **40,000** | `environment/lake garden 3d model/isometric+garden+3d+model.fbx` |
| 분수 정원 (광장) | 958,444 → **40,000** | `environment/fountain garden 3d model/fountain+garden+3d+model.fbx` |
| 감옥 (아직 미배치) | 950,518 → **100,000** | `environment/prison cell 3d model/prison+cell+3d+model.fbx` |

감옥은 실내라 10만으로 깎아 `ArtSource/Decimated/env_prison_cell.fbx`에 두었다.
아직 어느 씬에도 넣지 않았다 — 감옥 작업을 할 때 그대로 임포트하면 된다.

---

## 어떻게 돌렸나

```bash
blender --background --python Tools/decimate_fbx.py -- <in.fbx> <out.fbx> <목표 삼각형>
```

비율이 아니라 **목표 삼각형 수**를 받는다. Decimate는 비율을 받지만 모델마다 밀도가
제각각이라, 고정 비율은 아이콘을 뭉개면서 건물은 무겁게 남긴다. 비율은 목표에 닿는
값으로 계산한다.

UV는 `use_collapse_triangulate`로 지킨다. 이 메시들은 구워진 아틀라스 하나만 들고
있어서, 텍스처 좌표를 잃으면 모델이 회색이 된다. 결과를 옆에서 찍어 확인했고 창문·문·
지붕 타일까지 그대로 남았다.

결과는 `ArtSource/Decimated/`에 남아 있다. 원본은 건드리지 않았다.

**교체는 제자리 덮어쓰기로 했다.** 새 파일로 넣으면 GUID가 새로 생겨서 씬의 모든
참조가 끊긴다.

## 남은 것

3순위(실내 7개, 아직 안 쓰는 건물 외관 6개)뿐이다. 임포트할 때 같은 명령을 돌리면
된다. 실내는 목표를 10만으로 잡는다 — 플레이어가 가장 가까이서 오래 보는 곳이다.

```bash
blender --background --python Tools/decimate_fbx.py --   "ArtSource/Blender/building_inside/house01 interior 3d model/house+interior+3d+model.fbx"   "ArtSource/Decimated/interior_house01.fbx" 100000
```

## 상호작용 소품 (2026-08-04)

원본은 모두 46,000~50,000 삼각형으로 들어왔고 소품 예산 5,000으로 감면했다.
감면 후 삼각형 수는 Blender로 실측해 5,000임을 확인했다.

| 대상 | 원본 | 감면 전 | 감면 후 |
|---|---|---:|---:|
| 참치캔 | `cat interact 3d/fish can 3d model` | 48,664 | 5,000 |
| 바나나 | `interactive item 3d/banana 3d model` | 48,446 | 5,000 |
| 고무닭 | `dog interact 3d/yellow chicken 3d model` | 47,408 | 5,000 |
| 냉동 문어 | `interactive item 3d/ice octopus 3d model` | 49,512 | 5,000 |
| 폭죽 | `market items 3d/fireworks set 3d model` | 46,444 | 5,000 |

문서의 폴더 이름과 내용물이 어긋나는 사례가 있다. `interactive item 3d/can 3d model`
안에 들어 있는 것은 **기름통**이고, 참치캔은 `cat interact 3d/fish can 3d model`에
있다 (`17_게임_아이템_사용처_정리.md` 8.6절의 이름 충돌 항목과 같은 종류다).

## 상점 전리품 12종 (2026-08-04)

전부 5,000 삼각형으로 감면했고 Blender로 실측해 확인했다.

| 매장 | 물건 | 원본 폴더 | 스템 |
|---|---|---|---|
| 슈퍼마켓 | 빵 | `market items 3d/bread 3d model` | `loot_bread` |
| 슈퍼마켓 | 고급 양주병 | `market items 3d/vodka 3d model` | `loot_liquor_bottle` |
| 슈퍼마켓 | 계산대 돈통 | `market items 3d/cash drawer 3d model` | `loot_cash_drawer` |
| 슈퍼마켓 | 한우 선물세트 | `market items 3d/box of steaks 3d model` | `loot_beef_gift_set` |
| 서점 | 일반 책 | `bookstore item 3d/book 3d model` | `loot_book` |
| 서점 | 캐릭터 피규어 | `bookstore item 3d/cartoon knight 3d model` | `loot_figure_knight` |
| 서점 | 만년필 | `bookstore item 3d/fountain pen 3d model` | `loot_fountain_pen` |
| 서점 | 노트북 | `bookstore item 3d/laptop 3d model` | `loot_laptop` |
| 주얼리샵 | 루비 | `jewely items 3d/red gemstone 3d model` | `loot_ruby` |
| 주얼리샵 | 황금 손목시계 | `jewely items 3d/golden watch 3d model` | `loot_gold_watch` |
| 주얼리샵 | 금괴 | `jewely items 3d/gold bar 3d model` | `loot_gold_bar` |
| 주얼리샵 | 다이아몬드 반지 | `jewely items 3d/diamond ring 3d model` | `loot_diamond_ring` |

**사과는 없다.** 문서 1차 목록이 요구하지만 모델이 없어서, 같은 표의 **빵**이
저가·주머니 자리를 대신한다.

`vodka 3d model` 폴더 안의 파일명은 `potion+bottle+3d+model.fbx`다. 원화를
확인했고 실제로 양주 디캔터이며, `dog interact 3d`의 물약병과 md5가 다르다 —
Tripo가 붙인 파일명일 뿐이므로 폴더를 기준으로 삼는다.
