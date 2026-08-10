"""로비 게임방법 페이지를 한 규격으로 맞추고, 닫기 버튼의 자리를 잰다.

작업자가 준 그림은 **패널만** 그린 것이 아니라 모달이 떠 있는 화면을 잘라낸
것이다. 그래서 장마다 크기와 가로세로비가 다르고, 가장자리에는 화살표 버튼과
로비 캐릭터의 잘린 조각이 들어 있으며, 패널 바깥은 어두운 회색으로 차 있다.

그 회색은 임의의 색이 아니다. 로비 배경(242,229,218)에 검정을 알파 0.558로
덮은 값(107,101,95)과 정확히 같다 — 즉 이 그림들은 **딤 처리된 모달**의
목업이다. 그래서 바깥은 칠하는 것이 아니라 **빼야** 한다. 칠해서 넣으면 그
사각형이 로비의 캐릭터를 가린다.

예전 판은 배경을 투명으로 빼지 않고 로비 배경색으로 칠했다. 그때는 패널 안쪽
채움과 바깥 배경의 차이가 10~15밖에 되지 않아 가장자리에서 시작한 채우기가
패널 안쪽까지 먹어 들어갔기 때문이다(한 장이 734조각으로 부서졌다). 지금은
바깥이 어두운 회색이고 패널은 크림색이라 그 문제가 없다.

패널의 바깥선은 **줄마다 세어서** 고른다. 연결 성분으로 고르려고 했는데 되지
않았다 — 화살표가 테두리에 닿을 만큼 가까워서 패널과 한 덩어리로 붙고,
안티에일리어싱을 지우려고 열기 연산을 7픽셀까지 키워도 떨어지지 않는다. 반면
줄 단위로 보면 패널은 폭의 거의 전부를 채우고 화살표는 70픽셀 남짓이라 어느
줄에서도 절반을 넘지 못한다.

잘라낸 뒤에 칠할 곳을 성분으로 고르고 구멍을 채운다. 채우지 않으면 2페이지의
쓰레기통 사진처럼 바깥 회색과 같은 색이 섞인 곳이 패널 한가운데에 구멍으로
뚫린다.

닫기(X)는 그림 안에 그려져 있다. 지울 수도 있었지만 그러면 같은 모양을 코드로
다시 그려야 하므로, 대신 **어디에 그려져 있는지를 재서** 그 자리를 비율로
적어 둔다. 빌더가 그 비율로 투명한 클릭 영역을 올린다. 좌표를 눈으로 찍으면
`ISSUE-046`이고, 재서 적으면 그림이 바뀔 때 이 스크립트가 다시 잰다.

사용:
    python Tools/normalize_howto_pages.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "ArtSource" / "Lobby" / "HowTo"
OUTPUT = SOURCE / "normalized"

# 패널 바깥의 색. 로비 배경 F2E5DA 위에 검정 알파 0.558이다. 여기서 재서 쓰고,
# 이 상수는 잰 값이 예상과 맞는지 확인하는 데만 쓴다.
EXPECTED_BACKDROP = np.array([107, 101, 95])

# 배경으로 볼 허용 오차. WebP 압축과 안티에일리어싱이 평평한 면에도 몇 단계의
# 흔들림을 남긴다.
TOLERANCE = 22

# 화살표 자국을 지울 구간. 세로 가운데이고, 위아래 둥근 모서리는 건드리지
# 않는다 — 모서리는 원래 다른 모양이라 곧은 띠로 덮으면 그쪽이 망가진다.
NOTCH_BAND = (0.28, 0.72)
NOTCH_STRIP = 16
CLEAN_ROW = 0.18

# 닫기 버튼을 찾을 범위: 패널 오른쪽 위. 테두리를 성분으로 잡지 않도록 안쪽으로
# 물러나서 본다.
CLOSE_WINDOW_LEFT = 0.78
CLOSE_WINDOW_BOTTOM = 0.32
CLOSE_WINDOW_INSET = 16

# 네 장이 잰 닫기 위치가 이보다 더 벌어지면 멈춘다. 한 장만 어긋난 그림을
# 평균으로 뭉개면 그 페이지에서만 클릭이 빗나가고, 화면에는 아무 표시도 없다.
CLOSE_SPREAD_LIMIT = 0.012


def backdrop_of(rgb: np.ndarray) -> np.ndarray:
    """네 모서리에서 바깥 색을 고른다."""
    height, width = rgb.shape[:2]
    corners = np.array(
        [
            rgb[1, 1],
            rgb[1, width - 2],
            rgb[height - 2, 1],
            rgb[height - 2, width - 2],
        ]
    )
    return np.median(corners, axis=0)


def panel_bounds(is_content: np.ndarray) -> tuple[int, int, int, int]:
    """패널의 바깥선.

    연결 성분으로는 고를 수 없다. 화살표가 패널 테두리에 닿을 만큼 가까워서
    한 덩어리로 붙고, 안티에일리어싱을 지우려고 열기 연산을 7픽셀까지 키워도
    떨어지지 않는다.

    대신 **줄마다 세어서** 고른다. 패널은 폭이나 높이의 거의 전부를 채우고,
    화살표와 캐릭터 조각은 70픽셀 남짓이라 어느 줄에서도 절반을 넘지 못한다.
    둥근 모서리가 있는 줄도 반지름이 24픽셀 정도여서 여유가 크다.
    """
    columns = is_content.sum(axis=0)
    rows = is_content.sum(axis=1)
    xs = np.where(columns > columns.max() * 0.5)[0]
    ys = np.where(rows > rows.max() * 0.5)[0]
    if xs.size == 0 or ys.size == 0:
        raise ValueError("패널로 볼 만큼 채워진 줄이 없다.")

    return int(xs[0]), int(xs[-1]) + 1, int(ys[0]), int(ys[-1]) + 1


def panel_mask(is_content: np.ndarray) -> np.ndarray:
    """잘라낸 패널 안에서 실제로 칠할 픽셀.

    가운데를 품은 연결 성분에 구멍 채우기를 건다. 채우지 않으면 2페이지의
    쓰레기통 사진처럼 바깥 회색과 같은 색이 섞인 곳이 패널 한가운데에 구멍으로
    뚫린다. 남는 투명은 둥근 모서리 네 곳뿐이다.
    """
    labels, count = ndimage.label(is_content)
    if count == 0:
        raise ValueError("배경이 아닌 픽셀이 하나도 없다.")

    height, width = labels.shape
    centre = labels[height // 2, width // 2]
    if centre == 0:
        raise ValueError("잘라낸 그림 한가운데가 배경이다.")

    return ndimage.binary_fill_holes(labels == centre)


def repair_arrow_notches(panel: np.ndarray) -> None:
    """화살표가 패널 테두리 위로 번진 자국을 지운다.

    바깥선까지 잘라내도 화살표의 둥근 끝이 테두리에 얹혀 있어서, 세로 가운데
    구간에서만 테두리 색이 흐려진다. 네 장 모두 패널 높이의 0.38~0.66 언저리에
    있고 어긋난 정도는 최대 45단계다.

    칠해 없애지 않고 **화살표가 없는 높이의 띠를 세로로 복사한다.** 테두리가
    단색이 아니라 안쪽으로 갈수록 밝아지는 몇 픽셀짜리 그러데이션이라, 한 색으로
    덮으면 그 자리만 납작해져서 오히려 눈에 띈다.
    """
    height = panel.shape[0]
    clean = panel[int(height * CLEAN_ROW)].copy()
    top = int(height * NOTCH_BAND[0])
    bottom = int(height * NOTCH_BAND[1])

    panel[top:bottom, :NOTCH_STRIP] = clean[:NOTCH_STRIP]
    panel[top:bottom, -NOTCH_STRIP:] = clean[-NOTCH_STRIP:]


def close_button(panel: np.ndarray) -> tuple[int, int, int, int]:
    """패널 안에서 닫기 버튼의 사각형.

    버튼의 X 획은 크림색 판 위에 있는 가장 큰 어두운 덩어리다. 창의 가장자리에
    닿은 성분은 버리는데, 그것은 패널 테두리이지 버튼이 아니기 때문이다.
    """
    height, width = panel.shape[:2]
    x0 = int(width * CLOSE_WINDOW_LEFT)
    y0 = CLOSE_WINDOW_INSET
    window = panel[y0 : int(height * CLOSE_WINDOW_BOTTOM), x0 : width - CLOSE_WINDOW_INSET]

    labels, count = ndimage.label(window.mean(axis=2) < 125)
    best = None
    for index in range(1, count + 1):
        rows, cols = np.where(labels == index)
        touches_edge = (
            rows.min() == 0
            or cols.min() == 0
            or rows.max() == window.shape[0] - 1
            or cols.max() == window.shape[1] - 1
        )
        if touches_edge:
            continue
        if best is None or rows.size > best[0]:
            best = (rows.size, cols.min() + x0, cols.max() + x0, rows.min() + y0, rows.max() + y0)

    if best is None:
        raise ValueError("패널 오른쪽 위에서 닫기 버튼을 찾지 못했다.")

    _, left, right, top, bottom = best
    return int(left), int(right), int(top), int(bottom)


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGB")


def main() -> int:
    sources = sorted(
        [p for p in SOURCE.iterdir() if p.suffix.lower() in (".png", ".webp")],
        key=lambda p: p.name,
    )
    if not sources:
        print(f"'{SOURCE}'에 page*.png / page*.webp가 없다.", file=sys.stderr)
        return 1

    print(f"{len(sources)}장을 맞춘다.")

    cut: list[tuple[Image.Image, tuple[int, int, int, int]]] = []
    for path in sources:
        image = load(path)
        rgb = np.array(image).astype(int)

        drift = np.abs(backdrop_of(rgb) - EXPECTED_BACKDROP).max()
        if drift > TOLERANCE:
            print(
                f"  경고 {path.name}: 바깥 색이 {backdrop_of(rgb).astype(int).tolist()}로"
                f" 예상({EXPECTED_BACKDROP.tolist()})과 {int(drift)}만큼 다르다."
                " 딤 알파가 바뀌었으면 LobbyCanvasBuilder의 Scrim도 함께 고친다.",
                file=sys.stderr,
            )

        is_content = ~(np.abs(rgb - backdrop_of(rgb)) <= TOLERANCE).all(axis=2)
        left, right, top, bottom = panel_bounds(is_content)
        cropped = rgb[top:bottom, left:right]
        mask = panel_mask(is_content[top:bottom, left:right])

        panel = np.zeros((*mask.shape, 4), dtype=np.uint8)
        panel[..., :3] = cropped
        panel[..., 3] = np.where(mask, 255, 0)
        repair_arrow_notches(panel)

        box = close_button(panel[..., :3].astype(int))
        cut.append((Image.fromarray(panel, "RGBA"), box))

        print(
            f"  {path.name}: {image.width}x{image.height}"
            f" -> {panel.shape[1]}x{panel.shape[0]}"
            f"  (닫기 {box[1] - box[0] + 1}x{box[3] - box[2] + 1})"
        )

    width = max(page.width for page, _ in cut)
    height = max(page.height for page, _ in cut)
    print(f"공통 캔버스 {width}x{height}")

    centres: list[tuple[float, float]] = []
    sizes: list[tuple[float, float]] = []

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for stale in OUTPUT.glob("*.png"):
        stale.unlink()

    for index, (page, box) in enumerate(cut, start=1):
        canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        # 가로는 가운데, 세로는 **위쪽 기준**. 제목이 페이지마다 같은 높이에
        # 오게 하려는 것이고, 가운데로 맞추면 내용이 짧은 페이지에서 제목이
        # 내려앉아 넘길 때마다 흔들린다. 닫기 버튼도 제목 줄에 있으므로 이
        # 정렬이 곧 버튼이 한자리에 있다는 뜻이다.
        offset = (width - page.width) // 2
        canvas.paste(page, (offset, 0))
        target = OUTPUT / f"page{index}.png"
        canvas.save(target)

        left, right, top, bottom = box
        centres.append(
            (
                (left + right + 1) / 2 / width + offset / width,
                (top + bottom + 1) / 2 / height,
            )
        )
        sizes.append(((right - left + 1) / width, (bottom - top + 1) / height))
        print(f"  -> {target.relative_to(ROOT)}  {canvas.width}x{canvas.height}")

    spread = max(
        max(abs(a - b) for a, b in zip(axis, axis[1:])) if len(axis) > 1 else 0.0
        for axis in (
            [c[0] for c in centres],
            [c[1] for c in centres],
        )
    )
    if spread > CLOSE_SPREAD_LIMIT:
        print(
            f"닫기 버튼이 장마다 {spread:.4f}만큼 어긋나 있다"
            f" (허용 {CLOSE_SPREAD_LIMIT}). 한 자리에 클릭 영역을 둘 수 없다.",
            file=sys.stderr,
        )
        return 1

    hotspots = {
        "canvasWidth": width,
        "canvasHeight": height,
        "closeCenterX": round(sum(c[0] for c in centres) / len(centres), 5),
        "closeCenterY": round(sum(c[1] for c in centres) / len(centres), 5),
        "closeWidth": round(sum(s[0] for s in sizes) / len(sizes), 5),
        "closeHeight": round(sum(s[1] for s in sizes) / len(sizes), 5),
    }
    (OUTPUT / "hotspots.json").write_text(
        json.dumps(hotspots, indent=2) + "\n", encoding="utf-8"
    )
    print(f"닫기 버튼 {hotspots} (장마다 최대 {spread:.4f} 차이)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
