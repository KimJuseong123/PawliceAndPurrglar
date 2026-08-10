"""로비 게임방법 페이지를 한 규격으로 맞춘다.

작업자가 준 크롭은 세 장의 기준이 서로 다르다. 크기도 가로세로비도 다르고,
좌우 가장자리에는 화살표 버튼의 잘린 조각이 들어 있으며, 배경이 불투명한데 그
색이 장마다 조금씩 다르고 로비 배경과도 어긋난다.

그대로 쓰면 세 가지가 깨진다.

1. `preserveAspect`는 rect 안에서 가운데 정렬한다. 비율이 다른 세 장을 한 rect에
   넣으면 좁은 쪽이 축소돼, 넘길 때마다 패널 크기가 달라진다.
2. 배경색이 로비(242,229,218)와 달라서, 얹으면 네모난 색 경계가 보인다.
3. 화살표 조각은 진짜 버튼 옆에 잘린 그림으로 남는다.

**배경을 투명으로 빼지 않는다.** 처음에는 그렇게 했는데, 이 패널은 안쪽 채움이
바깥 배경과 10~15밖에 차이가 나지 않아서 가장자리에서 시작한 채우기가 패널
안쪽까지 먹어 들어간다. 한 페이지는 734조각으로 부서졌다. 대신 배경으로 보이는
색을 **로비 배경색으로 정확히 칠한다.** 로비 위에 얹히면 경계가 사라지고, 패널
안쪽이 함께 칠해지더라도 원래 거의 같은 색이라 눈에 띄지 않는다.

자르는 것은 좌우 화살표뿐이고, 그것도 좌표를 눈으로 찍지 않고 **잰다** — 세로
가운데 높이에서 가장자리부터 훑어, 화살표 덩어리가 끝나고 배경이 시작되는
지점이 패널의 바깥선이다.

사용:
    python Tools/normalize_howto_pages.py
"""

from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "ArtSource" / "Lobby" / "HowTo"
OUTPUT = SOURCE / "normalized"

# 로비의 배경색. `LobbyCanvasBuilder.Backdrop`(F2E5DA)와 같은 값이어야 한다.
LOBBY_BACKDROP = np.array([242, 229, 218], dtype=np.uint8)

# 배경으로 볼 허용 오차. PNG 압축과 안티에일리어싱이 평평한 면에도 몇 단계의
# 흔들림을 남긴다.
TOLERANCE = 16

# 패널 바깥선을 재는 높이들. 화살표(세로 가운데)와 둥근 모서리(위아래 끝)를
# 동시에 피한다.
PANEL_ROWS = (0.25, 0.35, 0.65, 0.75)

# 잰 바깥선에 남기는 여백. 테두리의 안티에일리어싱이 잘리지 않게 한다.
EDGE_MARGIN = 2


def sample_background(rgb: np.ndarray) -> np.ndarray:
    """네 모서리에서 배경색을 고른다.

    밝은 모서리만 센다 — 화살표 조각이 모서리에 걸친 페이지가 있어서, 그 어두운
    값을 배경으로 삼으면 아무것도 걸러지지 않는다.
    """
    height, width = rgb.shape[:2]
    corners = [
        rgb[1, 1],
        rgb[1, width - 2],
        rgb[height - 2, 1],
        rgb[height - 2, width - 2],
    ]
    light = [c for c in corners if c.mean() > 200]
    return np.median(np.array(light if light else corners), axis=0)


def panel_edges(is_background: np.ndarray) -> tuple[int, int]:
    """패널의 왼쪽·오른쪽 바깥선.

    화살표가 **없는 높이**에서 잰다. 화살표는 세로 가운데(대략 0.35~0.65)에
    있고 둥근 모서리는 위아래 끝에 있으므로, 0.25와 0.75는 둘 다 피한다.

    가장자리에서부터 화살표를 찾는 방식은 쓸 수 없다. 화살표가 이미지 끝에
    닿아 있지 않은 페이지가 있어서 — 오른쪽에 배경이 몇 픽셀 남아 있다 —
    "가장자리가 배경이면 화살표가 없다"가 거짓이 된다.
    """
    height, width = is_background.shape
    lefts, rights = [], []

    for fraction in PANEL_ROWS:
        row = is_background[int(height * fraction)]
        content = np.where(~row)[0]
        if content.size:
            lefts.append(int(content.min()))
            rights.append(int(content.max()))

    if not lefts:
        return 0, width

    # 가장 바깥. 한 줄이라도 모서리 곡선에 걸리면 안쪽 값이 나오는데, 그것을
    # 고르면 패널을 잘라 먹는다.
    left = max(0, min(lefts) - EDGE_MARGIN)
    right = min(width, max(rights) + 1 + EDGE_MARGIN)
    return left, right


def repair_arrow_notches(page: np.ndarray) -> None:
    """화살표가 패널 테두리 안쪽으로 밀고 들어온 자국을 지운다.

    바깥선까지 잘라내도 화살표의 둥근 끝이 테두리 위에 조금 얹혀 있어서 작은
    홈처럼 남는다. 그 구간의 테두리는 곧은 세로선이므로, 화살표가 없는 높이의
    좌우 끝 띠를 그 구간에 그대로 세로로 복사하면 원래 선이 복원된다.

    칠해 없애지 않고 복사하는 이유는, 테두리가 단색이 아니라 안쪽으로 갈수록
    밝아지는 몇 픽셀짜리 그러데이션이기 때문이다. 한 색으로 덮으면 그 자리만
    납작해져서 오히려 눈에 띈다.
    """
    height, width = page.shape[:2]
    band_top = int(height * 0.30)
    band_bottom = int(height * 0.70)

    clean_row = page[int(height * 0.22)]
    strip = int(min(48, width * 0.06))

    for y in range(band_top, band_bottom):
        page[y, :strip] = clean_row[:strip]
        page[y, width - strip:] = clean_row[width - strip:]


def normalize(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGB")
    rgb = np.array(image).astype(int)

    background = sample_background(rgb)
    is_background = (np.abs(rgb - background) <= TOLERANCE).all(axis=2)

    left, right = panel_edges(is_background)

    out = np.array(image)
    out[is_background] = LOBBY_BACKDROP
    cropped = out[:, left:right]
    repair_arrow_notches(cropped)

    print(
        f"  {path.name}: {image.width}x{image.height}"
        f" -> {cropped.shape[1]}x{cropped.shape[0]}"
        f" (잘라낸 화살표 좌 {left}px, 우 {image.width - right}px)"
    )
    return Image.fromarray(cropped, "RGB")


def main() -> int:
    sources = sorted(SOURCE.glob("page*.png"))
    if not sources:
        print(f"'{SOURCE}'에 page*.png가 없다.", file=sys.stderr)
        return 1

    print(f"{len(sources)}장을 맞춘다.")
    pages = [normalize(path) for path in sources]

    width = max(page.width for page in pages)
    height = max(page.height for page in pages)
    print(f"공통 캔버스 {width}x{height}")

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for index, page in enumerate(pages, start=1):
        canvas = Image.new("RGB", (width, height), tuple(int(c) for c in LOBBY_BACKDROP))
        # 가로는 가운데, 세로는 **위쪽 기준**. 제목이 페이지마다 같은 높이에
        # 오게 하려는 것이고, 가운데로 맞추면 내용이 짧은 페이지에서 제목이
        # 내려앉아 넘길 때마다 흔들린다.
        canvas.paste(page, ((width - page.width) // 2, 0))
        target = OUTPUT / f"page{index}.png"
        canvas.save(target)
        print(f"  -> {target.relative_to(ROOT)}  {canvas.width}x{canvas.height}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
