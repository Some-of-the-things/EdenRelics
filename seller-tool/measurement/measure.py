"""
Garment measurement from a flat-lay photo + a printed ArUco marker of known size.

How it works (the point of the spike):
  A single square marker gives four coplanar points whose real-world positions we know
  exactly (a size_mm square). From those we compute a HOMOGRAPHY that maps image pixels
  to real millimetres on the marker's plane. That rectifies perspective from ONE marker,
  so a phone shot that isn't perfectly overhead still measures correctly — provided the
  garment lies flat in the same plane as the marker. Any two points you click are then
  converted to mm and the distance is reported in cm.

Assumptions / limits (what the physical test is really probing):
  - The garment is flat and coplanar with the marker (drape, curl, thick seams add error).
  - The marker's printed size is exactly --size-mm (verify with a ruler; see generate_marker.py).

Usage:
  python measure.py --selftest                 # synthetic accuracy check, no photo needed
  python measure.py photo.jpg --interactive    # click two points -> distance in cm
  python measure.py photo.jpg --points 120,340 980,352   # headless: measure one segment
"""
import argparse
import sys
import cv2
import numpy as np

DICT = cv2.aruco.DICT_4X4_50


def get_detector():
    dictionary = cv2.aruco.getPredefinedDictionary(DICT)
    params = cv2.aruco.DetectorParameters()
    # Sub-pixel corner refinement — matters for scale accuracy.
    params.cornerRefinementMethod = cv2.aruco.CORNER_REFINE_SUBPIX
    return cv2.aruco.ArucoDetector(dictionary, params)


def detect_markers(gray, detector):
    """Return {id: (4,2) float32 corner array} in the marker's own corner order."""
    corners, ids, _ = detector.detectMarkers(gray)
    out = {}
    if ids is not None:
        for c, i in zip(corners, ids.flatten()):
            out[int(i)] = c.reshape(4, 2).astype(np.float32)
    return out


def image_to_metric_homography(marker_px, size_mm):
    """Homography mapping image pixels -> mm, with the marker as a size_mm square.

    detectMarkers returns corners as top-left, top-right, bottom-right, bottom-left in
    the marker's frame, so the metric target square uses the same order. Euclidean
    distances are invariant to the frame's origin/rotation, so measurements are correct
    regardless of how the marker is turned in the photo.
    """
    dst = np.array([[0, 0], [size_mm, 0], [size_mm, size_mm], [0, size_mm]], dtype=np.float32)
    H, _ = cv2.findHomography(marker_px, dst)
    return H


def to_mm(H, pts_px):
    pts = np.asarray(pts_px, dtype=np.float32).reshape(-1, 1, 2)
    return cv2.perspectiveTransform(pts, H).reshape(-1, 2)


def distance_mm(H, p1_px, p2_px):
    a, b = to_mm(H, [p1_px, p2_px])
    return float(np.hypot(*(a - b)))


def _solve(gray, detector, ref_id, size_mm):
    markers = detect_markers(gray, detector)
    if not markers:
        raise SystemExit("No ArUco markers detected. Check focus/lighting and that the whole marker is in frame.")
    if ref_id not in markers:
        raise SystemExit(f"Reference marker id {ref_id} not found (saw ids {sorted(markers)}). Pass --ref-id.")
    return image_to_metric_homography(markers[ref_id], size_mm), markers


# ---------------- Self-test: synthetic warped scene with a known-size rectangle ----------------

def _render_scene(size_mm=120.0, ppm=3.0):
    """A flat 'tabletop': the marker plus a rectangle of known real size (300 x 400 mm)."""
    dictionary = cv2.aruco.getPredefinedDictionary(DICT)
    draw = getattr(cv2.aruco, "generateImageMarker", None) or cv2.aruco.drawMarker
    W_mm, H_mm = 560, 760
    canvas = np.full((round(H_mm * ppm), round(W_mm * ppm)), 255, dtype=np.uint8)

    marker = draw(dictionary, 0, round(size_mm * ppm))
    ox, oy = round(40 * ppm), round(40 * ppm)
    canvas[oy:oy + marker.shape[0], ox:ox + marker.shape[1]] = marker

    # Known rectangle: 300 mm x 400 mm, its metric corners are ground truth.
    x0, y0, w, h = 190.0, 130.0, 300.0, 400.0
    rect_mm = np.array([[x0, y0], [x0 + w, y0], [x0 + w, y0 + h], [x0, y0 + h]], dtype=np.float32)
    cv2.polylines(canvas, [np.int32(rect_mm * ppm)], True, 0, max(1, round(ppm)))
    return canvas, ppm, rect_mm, (w, h)


def _warp(canvas):
    """Deterministic perspective warp — simulates a phone shot that isn't dead-overhead."""
    H_img, W_img = canvas.shape[:2]
    src = np.array([[0, 0], [W_img, 0], [W_img, H_img], [0, H_img]], dtype=np.float32)
    # Push corners in by fixed fractions (no RNG -> reproducible).
    dst = np.array([[0.10 * W_img, 0.06 * H_img],
                    [0.94 * W_img, 0.02 * H_img],
                    [0.98 * W_img, 0.97 * H_img],
                    [0.03 * W_img, 0.92 * H_img]], dtype=np.float32)
    Wg = cv2.getPerspectiveTransform(src, dst)
    warped = cv2.warpPerspective(canvas, Wg, (W_img, H_img), borderValue=255)
    return warped, Wg


def selftest(size_mm=120.0):
    canvas, ppm, rect_mm, (true_w, true_h) = _render_scene(size_mm)
    warped, Wg = _warp(canvas)

    detector = get_detector()
    H, markers = _solve(warped, detector, 0, size_mm)

    # Where the rectangle's true corners land in the warped photo (a perfect "click").
    rect_render_px = (rect_mm * ppm).reshape(-1, 1, 2)
    rect_warped_px = cv2.perspectiveTransform(rect_render_px, Wg).reshape(-1, 2)

    rec = to_mm(H, rect_warped_px)
    meas_w = (np.hypot(*(rec[1] - rec[0])) + np.hypot(*(rec[2] - rec[3]))) / 2
    meas_h = (np.hypot(*(rec[3] - rec[0])) + np.hypot(*(rec[2] - rec[1]))) / 2

    ew, eh = meas_w - true_w, meas_h - true_h
    tol_mm = 2.0
    print("Self-test - synthetic warped flat-lay:")
    print(f"  marker detected: id 0 of {sorted(markers)}")
    print(f"  width : true {true_w:.1f} mm  measured {meas_w:.2f} mm  error {ew:+.2f} mm ({ew/true_w*100:+.2f}%)")
    print(f"  height: true {true_h:.1f} mm  measured {meas_h:.2f} mm  error {eh:+.2f} mm ({eh/true_h*100:+.2f}%)")
    ok = abs(ew) < tol_mm and abs(eh) < tol_mm
    print(f"  RESULT: {'PASS' if ok else 'FAIL'} (tolerance +/-{tol_mm:g} mm on a perfectly flat plane)")
    return 0 if ok else 1


# ---------------- Interactive measuring ----------------

def interactive(image_path, ref_id, size_mm):
    img = cv2.imread(image_path)
    if img is None:
        raise SystemExit(f"Could not read image: {image_path}")
    detector = get_detector()
    H, markers = _solve(cv2.cvtColor(img, cv2.COLOR_BGR2GRAY), detector, ref_id, size_mm)
    for c in markers.values():
        cv2.polylines(img, [np.int32(c)], True, (0, 180, 0), 2)

    pts, base, last = [], img.copy(), [img.copy()]

    def redraw():
        view = base.copy()
        for p in pts:
            cv2.circle(view, p, 5, (0, 0, 255), -1)
        if len(pts) == 2:
            cv2.line(view, pts[0], pts[1], (0, 0, 255), 2)
            cm = distance_mm(H, pts[0], pts[1]) / 10.0
            cv2.putText(view, f"{cm:.1f} cm", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 0, 255), 2, cv2.LINE_AA)
            print(f"  {cm:.2f} cm")
        last[0] = view
        cv2.imshow("measure - click 2 points | r reset | s save | q quit", view)

    def on_mouse(event, x, y, flags, _):
        if event == cv2.EVENT_LBUTTONDOWN:
            if len(pts) == 2:
                pts.clear()
            pts.append((x, y))
            redraw()

    cv2.namedWindow("measure - click 2 points | r reset | s save | q quit", cv2.WINDOW_NORMAL)
    cv2.setMouseCallback("measure - click 2 points | r reset | s save | q quit", on_mouse)
    redraw()
    print("Click two points to measure. Keys: r=reset  s=save annotated  q=quit")
    while True:
        k = cv2.waitKey(50) & 0xFF
        if k in (ord("q"), 27):
            break
        if k == ord("r"):
            pts.clear(); redraw()
        if k == ord("s"):
            cv2.imwrite("measured.png", last[0])
            print("  saved measured.png")
    cv2.destroyAllWindows()


def main():
    ap = argparse.ArgumentParser(description="Measure garment dimensions from a photo with an ArUco scale marker.")
    ap.add_argument("image", nargs="?", help="photo with the marker and garment")
    ap.add_argument("--selftest", action="store_true", help="run the synthetic accuracy check and exit")
    ap.add_argument("--interactive", action="store_true", help="click two points to measure")
    ap.add_argument("--points", nargs=2, metavar=("X1,Y1", "X2,Y2"), help="measure one segment headlessly")
    ap.add_argument("--ref-id", type=int, default=0, help="reference marker id (default 0)")
    ap.add_argument("--size-mm", type=float, default=120.0, help="printed marker side in mm (default 120)")
    args = ap.parse_args()

    if args.selftest:
        sys.exit(selftest(args.size_mm))
    if not args.image:
        ap.error("give an image, or use --selftest")
    if args.points:
        img = cv2.imread(args.image)
        if img is None:
            raise SystemExit(f"Could not read image: {args.image}")
        H, _ = _solve(cv2.cvtColor(img, cv2.COLOR_BGR2GRAY), get_detector(), args.ref_id, args.size_mm)
        p1 = tuple(map(float, args.points[0].split(",")))
        p2 = tuple(map(float, args.points[1].split(",")))
        print(f"{distance_mm(H, p1, p2) / 10.0:.2f} cm")
    else:
        interactive(args.image, args.ref_id, args.size_mm)


if __name__ == "__main__":
    main()
