# 20. 감면(Decimate) 목록

측정일: 2026-08-02. 모든 수치는 `Paws & Loot / Setup / Report Model Weights`가
실제 임포트된 메시를 세어 얻은 것이다. 파일 크기 짐작이 아니다.

## 왜 필요한가

생성기(Tripo)가 내보낸 모델은 거의 전부 **약 95만 삼각형**이다. 손으로 만든 것과
같은 실루엣을 스캔 밀도로 표현한 결과다. 화면에서는 3~5만 삼각형과 구분되지 않지만,
빌드 용량과 프레임에는 그대로 실린다.

목표는 **각 모델 3~5만 삼각형**이다. Blender의 Decimate(Collapse) 비율 약 0.04.

---

## 1순위 — 본 게임이 지금 쓰고 있는 것

`Game.unity`가 실제로 배치하는 모델이다. 여기가 가장 급하다.

| 모델 | 지금 | Blender 파일 |
|---|---:|---|
| 느낌표 아이콘 | 983,743 | `expression icon 3d/warning 3d model/orange+exclamation+mark+3d+model.fbx` |
| 하트 아이콘 | 959,014 | `expression icon 3d/pink heart 3d model/pink+heart+3d+model.fbx` |
| 전구 아이콘 | 953,378 | `expression icon 3d/light bulb 3d model/light+bulb+3d+model.fbx` |
| 소용돌이 아이콘 | 951,894 | `expression icon 3d/dizzy 3d model/spiral+badge+3d+model.fbx` |

**합계 385만 삼각형.** 동물 머리 위에 뜨는 한 뼘짜리 아이콘 넷이다. 화면에서 차지하는
크기를 생각하면 **각 1,000~3,000 삼각형이면 충분하다** — 비율 0.002. 이 넷이 이
목록 전체에서 투자 대비 효과가 가장 크다.

## 2순위 — 샌드박스 맵이 쓰는 것 (본 게임 이식 예정)

| 모델 | 지금 | Blender 파일 |
|---|---:|---|
| 보석상 | 954,546 | `Buildings/jewelry shop 3d model/jewelry+shop+3d+model.fbx` |
| 분수 광장 | 945,576 | `environment/fountain plaza 3d model/fountain+plaza+3d+model.fbx` |
| 2층집 | 920,026 | `Buildings/two-story house 3d model/two-story+house+3d+model.fbx` |

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
| 가로등 | 96,594 | `environment/street lamp 3d model/street+lamp+3d+model.fbx` |
| 나무 (로우폴리) | 89,780 | `environment/low poly tree 3d model/low+poly+tree+3d+model.fbx` |
| 나무 (양식화) | 93,725 | `environment/stylized tree 3d model/stylized+tree+3d+model.fbx` |
| 외벽 돌담 | 46,984 | `environment/outer wall 3d model/stone+wall+3d+model.fbx` |
| 소화전 | 46,514 | `environment/fire hydrant 3d model/fire+hydrant+3d+model.fbx` |

이미 10만 이하라 급하지 않다. 다만 **가로등과 나무는 개수가 많다** — 지금 12개지만
맵이 커지면 늘어난다. 각 5,000으로 내리면 좋다.

---

## 하지 않아도 되는 것

### 도로 조각 5종 — **필요 없다**

각 95만 삼각형이지만 **빌드에 들어가지 않는다.** 위에서 한 번 구워 512px 그림으로
쓰고, 화면에 있는 것은 삼각형 2개짜리 평면이다. `Report Model Weights`가 이 다섯을
`unused`로 보고하는 것이 그 증거다.

| 모델 | 상태 |
|---|---|
| `road segment 01~05` | 굽는 원본으로만 쓰임. 빌드 제외 |
| `curved road segment` | 같음 |
| `grass tile` | 같음 |

**모델을 고치면 다시 구워야 한다** (`Bake Road Tile Textures`). 감면할 필요는 없다.

### 이미 가벼운 것

경찰서(15,588), 서점(17,428), 슈퍼마켓(9,912), 1층집(7,648), 캐릭터 전부(5,000 이하).
손댈 이유가 없다.

---

## 숲과 호수

**ArtSource에 없다.** 만들어 넣으실 계획이면, 나무처럼 여러 번 반복될 것이므로
처음부터 **5,000 삼각형 이하**로 만드시는 편이 나중에 깎는 것보다 낫다.

호수는 평면에 가까우므로 도로와 같은 방법(위에서 구워 평면에 입히기)이 그대로
통한다. 필요하면 그렇게 처리할 수 있다.

---

## 작업 순서 제안

1. **아이콘 4개** — 385만 → 1만 미만. 반나절이면 되고 효과가 가장 크다
2. **보석상 · 분수 광장 · 2층집** — 282만 → 15만. 샌드박스 빌드 32MB → 2MB
3. 실내 7개 (들여올 때)
4. 가로등 · 나무

1과 2만 해도 빌드 에셋이 **80MB → 25MB 내외**가 된다. WebGL에서 편한 크기다.
