"""
Generate a printable ArUco fiducial marker for the garment-measurement spike.

The marker is the scale reference: measure.py recovers real-world millimetres from a
photo by detecting this marker and using its KNOWN printed side length. So the one
thing that must be true is that the printed square is exactly the size you tell
measure.py — print at 100% (no "fit to page") and verify the printed square with a
ruler. If your printer scales it, just measure the real printed size and pass that as
--size-mm to measure.py; the method self-calibrates from whatever the true size is.

Usage:
    python generate_marker.py                      # id 0, 120 mm, marker.png
    python generate_marker.py --size-mm 100 --id 3 --out mymarker.png
"""
import argparse
import cv2
import numpy as np

DICT = cv2.aruco.DICT_4X4_50


def generate(marker_id: int, size_mm: float, dpi: int, out: str) -> None:
    dictionary = cv2.aruco.getPredefinedDictionary(DICT)
    side_px = max(1, round(size_mm / 25.4 * dpi))

    # OpenCV 4.7+/5.0 renamed drawMarker -> generateImageMarker.
    draw = getattr(cv2.aruco, "generateImageMarker", None) or cv2.aruco.drawMarker
    marker = draw(dictionary, marker_id, side_px)

    # Quiet zone: ArUco needs a white border around the marker to detect it reliably.
    quiet = round(side_px * 0.18)
    canvas_px = side_px + 2 * quiet
    caption_px = round(dpi * 0.5)  # ~half-inch caption strip
    img = np.full((canvas_px + caption_px, canvas_px), 255, dtype=np.uint8)
    img[quiet:quiet + side_px, quiet:quiet + side_px] = marker

    # Corner ticks so you can measure the true printed side edge-to-edge.
    for (cx, cy) in [(quiet, quiet), (quiet + side_px, quiet),
                     (quiet, quiet + side_px), (quiet + side_px, quiet + side_px)]:
        cv2.drawMarker(img, (cx, cy), 0, cv2.MARKER_CROSS, round(dpi * 0.12), max(1, round(dpi / 150)))

    caption = f"ArUco 4X4_50  id={marker_id}   this square = {size_mm:g} mm"
    cv2.putText(img, caption, (quiet, canvas_px + round(caption_px * 0.45)),
                cv2.FONT_HERSHEY_SIMPLEX, dpi / 900, 0, max(1, round(dpi / 300)), cv2.LINE_AA)
    cv2.putText(img, "Print at 100% (no scaling), then verify the square with a ruler.",
                (quiet, canvas_px + round(caption_px * 0.8)),
                cv2.FONT_HERSHEY_SIMPLEX, dpi / 1300, 0, max(1, round(dpi / 400)), cv2.LINE_AA)

    cv2.imwrite(out, img)
    print(f"wrote {out}: {img.shape[1]}x{img.shape[0]} px @ {dpi} dpi "
          f"(marker square {size_mm:g} mm, id {marker_id})")


def main() -> None:
    ap = argparse.ArgumentParser(description="Generate a printable ArUco scale marker.")
    ap.add_argument("--id", type=int, default=0, help="marker id (default 0)")
    ap.add_argument("--size-mm", type=float, default=120.0, help="printed marker side in mm (default 120)")
    ap.add_argument("--dpi", type=int, default=600, help="render dpi (default 600)")
    ap.add_argument("--out", default="marker.png", help="output PNG path")
    args = ap.parse_args()
    generate(args.id, args.size_mm, args.dpi, args.out)


if __name__ == "__main__":
    main()
