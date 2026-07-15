# Garment auto-measurement — ArUco spike

Goal of the spike: decide whether we can auto-measure a garment from a phone photo
accurately enough to trust, using a printed **ArUco marker** of known size as the scale
reference. If yes, this feeds the tool's measurement step (§4.7); if not, we fall back to
manual measurement entry.

**The software is already built and self-tested** — on a synthetic, perspective-warped
scene it recovers a 300×400 mm rectangle to within ~0.5 mm (`python measure.py --selftest`).
So this test is about **real-world error** (fabric drape, lighting, lens distortion, how
flat the garment actually lies), not about whether the code works.

## How it works
A single square marker gives four points whose real positions we know exactly. From them we
compute a homography that maps image pixels → millimetres **on the marker's plane**, which
corrects camera tilt from one marker. Click two points → distance in cm. The only hard
requirement is that the garment lies flat, in the same plane as the marker.

## Setup (once)
```
cd seller-tool/measurement
py -m venv .venv
.venv/Scripts/python -m pip install -r requirements.txt
.venv/Scripts/python measure.py --selftest      # should print RESULT: PASS
```

## Step 1 — Peter: make the marker
```
.venv/Scripts/python generate_marker.py --size-mm 120       # writes marker.png
```
Print `marker.png` **at 100% / actual size (no "fit to page")** on plain paper, mount it flat
(glue to card so it doesn't curl), and **measure the printed black square with a ruler**. If
it isn't exactly 120 mm, that's fine — just note the real size and pass it later as
`--size-mm <actual>`. This measured size is the whole calibration; everything scales off it.

## Step 2 — Teo: shoot the garments
For each garment:
1. Lay it flat and smooth on a plain, contrasting background. Remove wrinkles.
2. Put the marker **flat, in the same plane** as the garment (on the same tabletop), near it,
   fully inside the frame.
3. Shoot from roughly **straight above**, whole garment **and** the whole marker in frame,
   even lighting, no glare on the marker, in focus.
4. **Hand-measure** two real dimensions (e.g. pit-to-pit, and length) with a tape — this is
   the ground truth to compare against.

Aim for variety: a few different garments, fabrics (crisp cotton vs. drapey), colours, and a
couple of deliberately imperfect shots (slight tilt, slight wrinkle) to probe the limits.

## Step 3 — measure and log the error
```
.venv/Scripts/python measure.py photo.jpg --interactive        # click the two end points
# (if the printed square wasn't 120 mm: add --size-mm <actual>)
```
Click the same two points you hand-measured; the tool prints cm. Record per garment:

| garment | fabric | dimension | tape (cm) | tool (cm) | error (cm) | notes (tilt/drape) |
|---------|--------|-----------|-----------|-----------|------------|--------------------|

## Go / no-go
- **Go** if the tool lands within about **±0.5 cm** of the tape on flat, well-shot garments,
  and degrades gracefully (not wildly) on the imperfect shots.
- **No-go / needs work** if errors are routinely >1 cm even on good shots, or detection is
  flaky — then we either improve the rig (bigger marker, multi-marker board for full
  perspective correction, lens-distortion calibration) or fall back to manual entry.

## Files
- `generate_marker.py` — make the printable marker.
- `measure.py` — detect marker, rectify, measure (`--selftest`, `--interactive`, `--points`).
- `requirements.txt` — numpy + opencv-contrib-python.
