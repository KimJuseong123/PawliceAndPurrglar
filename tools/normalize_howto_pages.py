"""로비 게임방법 페이지를 한 규격으로 맞춘다.

작업자가 준 크롭은 세 장의 기준이 서로 다르다. 크기도(984x478 / 1018x518 /
1000x622) 가로세로비도 다르고, 가장자리에는 화살표 버튼의 잘린 조각이, 위쪽에는
로고의 아랫부분이 물려 있다.

그대로 쓰면 두 가지가 깨진다.

1. `preserveAspect`는 rect 안에서 가운데 정렬한다. 비율이 다른 세 장을 한 rect에
   넣으면 좁은 쪽이 축소돼, 페이지를 넘길 때마다 패널 크기가 달라진다.
2. 배경이 불투명하고 로비 배경색과 미세하게 다르다(234,223,212 대 242,229,218).
   얹으면 네모난 색 경계가 보인다.

그래서 배경을 투명으로 빼고, 패널 상자만 남기고, 한 캔버스에 위쪽 기준으로 맞춘다.

배경 제거는 **가장자리에서 시작하는 채우기**로 한다. 밝기 한 값으로 자르면 패널
안쪽의 크림색 채움(244,235,223)이 배경(234,223,212)과 10 남짓밖에 차이가 나지
않아 같이 지워진다. 바깥에서 이어진 영역만 지우면 안쪽은 색이 같아도 남는다.

화살표 조각과 로고 물림은 패널과 **떨어져 있는 별개 덩어리**다. 가장 큰 덩어리만
남기면 따로 좌표를 재지 않고 사라진다.

사용:
    python Tools/normalize_howto_pages.py
"""

from __future__ import annotations

import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "ArtSource" / "Lobby" / "HowTo"
OUTPUT = SOURCE / "normalized"

# 배경으로 볼 색과 허용 오차. 띠로 잡는 이유는 PNG 압축과 안티에일리어싱이
# 평평한 배경에도 몇 단계의 흔들림을 남기기 때문이다.
BACKGROUND = np.array([234, 223, 212])
TOLERANCE = 14

# 패널 상자 둘레에 남기는 여백. 둥근 모서리의 안티에일리어싱이 잘리지 않게 한다.
MARGIN = 2


def flood_background(rgb: np.ndarray) -> np.ndarray:
    """가장자리에서 이어진 배경 픽셀을 True로 돌려준다."""
    height, width = rgb.shape[:2]
    near = (np.abs(rgb.astype(int) - BACKGROUND) <= TOLERANCE).all(axis=2)

    seen = np.zeros((height, width), dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    for x in range(width):
        for y in (0, height - 1):
            if near[y, x] and not seen[y, x]:
                seen[y, x] = True
                queue.append((y, x))
    for y in range(height):
        for x in (0, width - 1):
            if near[y, x] and not seen[y, x]:
                seen[y, x] = True
                queue.append((y, x))

    while queue:
        y, x = queue.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < height and 0 <= nx < width:
                if near[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    queue.append((ny, nx))

    return seen


def largest_component(mask: np.ndarray) -> np.ndarray:
    """남은 불투명 픽셀 중 가장 큰 연결 덩어리만 돌려준다."""
    height, width = mask.shape
    label = np.zeros((height, width), dtype=np.int32)
    best_id, best_size = 0, 0
    current = 0

    for sy in range(height):
        for sx in range(width):
            if not mask[sy, sx] or label[sy, sx]:
                continue
            current += 1
            size = 0
            queue = deque([(sy, sx)])
            label[sy, sx] = current
            while queue:
                y, x = queue.popleft()
                size += 1
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < width:
                        if mask[ny, nx] and not label[ny, nx]:
                            label[ny, nx] = current
                            queue.append((ny, nx))
            if size > best_size:
                best_id, best_size = current, size

    return label == best_id


def panel_bounds(opaque: np.ndarray) -> tuple[int, int, int, int]:
    """패널 상자의 경계. 조각이 없는 주사선에서만 잰다.

    가장 큰 덩어리로는 안 된다. 화살표 조각은 패널 테두리에 닿아 있고 로고
    물림도 안티에일리어싱으로 이어져 있어서, 셋이 한 덩어리로 잡힌다.

    대신 방해물이 **어디에 있는지**를 쓴다. 화살표는 좌우 가장자리의 세로
    가운데에 있고 로고는 위쪽 가로 가운데에 있다. 그래서 좌우 경계는 위아래
    끝 근처의 가로줄에서, 상하 경계는 좌우 끝 근처의 세로줄에서 재면 서로를
    피한다.
    """
    height, width = opaque.shape

    def span(line: np.ndarray) -> tuple[int, int] | None:
        hit = np.where(line)[0]
        return (int(hit.min()), int(hit.max())) if hit.size else None

    # 좌우: 화살표가 닿지 않는 높이의 가로줄들.
    lefts, rights = [], []
    for fraction in (0.10, 0.14, 0.86, 0.90):
        found = span(opaque[int(height * fraction)])
        if found:
            lefts.append(found[0])
            rights.append(found[1])

    # 상하: 로고가 닿지 않는 가로 위치의 세로줄들.
    tops, bottoms = [], []
    for fraction in (0.10, 0.16, 0.84, 0.90):
        found = span(opaque[:, int(width * fraction)])
        if found:
            tops.append(found[0])
            bottoms.append(found[1])

    # 중앙값 대신 가장 안쪽 값. 한 줄이라도 조각을 스치면 바깥으로 벌어지는데,
    # 벌어진 쪽을 채택하면 잘라내려던 것이 그대로 남는다.
    return (
        max(lefts) if lefts else 0,
        min(rights) if rights else width - 1,
        max(tops) if tops else 0,
        min(bottoms) if bottoms else height - 1,
    )


def extract(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    rgb = np.array(image)[..., :3]

    background = flood_background(rgb)
    opaque = ~background

    left_edge, right_edge, top_edge, bottom_edge = panel_bounds(opaque)

    top = max(0, top_edge - MARGIN)
    bottom = min(image.height, bottom_edge + 1 + MARGIN)
    left = max(0, left_edge - MARGIN)
    right = min(image.width, right_edge + 1 + MARGIN)

    # 잘라낸 창 바깥은 물론이고, 창 안에서도 배경이었던 곳은 투명하게 둔다.
    panel = np.zeros_like(opaque)
    panel[top:bottom, left:right] = opaque[top:bottom, left:right]

    out = np.array(image)
    # 패널 덩어리 밖은 전부 투명. 가장자리 조각과 로고 물림이 여기서 떨어진다.
    out[..., 3] = np.where(panel, 255, 0)
    cropped = Image.fromarray(out[top:bottom, left:right], "RGBA")

    print(
        f"  {path.name}: {image.width}x{image.height}"
        f" -> 패널 {cropped.width}x{cropped.height}"
        f" (버린 여백 상 {top}, 하 {image.height - bottom},"
        f" 좌 {left}, 우 {image.width - right})"
    )
    return cropped


def main() -> int:
    sources = sorted(SOURCE.glob("page*.png"))
    if not sources:
        print(f"'{SOURCE}'에 page*.png가 없다.", file=sys.stderr)
        return 1

    print(f"{len(sources)}장에서 패널을 뽑는다.")
    panels = [extract(path) for path in sources]

    # 한 캔버스. 넓이는 가장 넓은 것, 높이는 가장 높은 것.
    width = max(panel.width for panel in panels)
    height = max(panel.height for panel in panels)
    print(f"공통 캔버스 {width}x{height}")

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for index, panel in enumerate(panels, start=1):
        canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        # 가로는 가운데, 세로는 **위쪽 기준**. 제목이 페이지마다 같은 높이에
        # 오게 하려는 것이고, 가운데 정렬하면 내용이 짧은 페이지에서 제목이
        # 내려앉아 넘길 때마다 흔들린다.
        canvas.paste(panel, ((width - panel.width) // 2, 0), panel)
        target = OUTPUT / f"page{index}.png"
        canvas.save(target)
        print(f"  -> {target.relative_to(ROOT)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
