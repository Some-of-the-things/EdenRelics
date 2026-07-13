# Vintage Dating Rules — Authoring Template

*For Teodora. A structured way to capture every deterministic dating rule so it drops straight into
the rules engine with no translation. You own the facts and sources; I turn the finished document
into the machine-readable dataset.*

---

## 1. What this is for

The engine's headline feature is catching **contradictions**: *"your '1970s' dress has a post-1988
care label."* To do that, each rule needs to give the engine a **date boundary** it can compare
against the era the seller claims. This template captures each rule in the same shape so that job is
mechanical.

You don't need to think about code. Just fill in the fields below for each rule. Write as many rules
as you can; partial is fine — I can ingest it in batches.

**You are the authority.** Every date and source is yours to verify. The three worked examples below
are there to show the *format*, not to assert facts — treat their dates/sources as illustrative and
confirm them yourself.

---

## 1a. How this fits the engineering brief

The engine works exactly as the brief's §3.2 describes: **each rule contributes one permitted
interval** (`NOT BEFORE x` / `NOT AFTER y`), and the estimate is **what survives the intersection of
all of them**. An empty intersection is a *contradiction* to surface, never to silently reconcile.
So you write rules one at a time; you never have to think about how they combine — that's the engine's
job. A garment with the brand label cut out but a datable care label + zip still dates fine (evidence
set, not label→date). Terms map 1:1 to the brief: my `earliest` = `NOT BEFORE`, `latest` = `NOT AFTER`,
`confidence` = `strength` (HARD/SOFT), `source` = `source_citation`, `status` = active/unverified.

**`unverified` rules never affect output** — they just park in the store so your research can stage.

## 2. The one concept that matters: boundary + direction

Each rule says one of four things about when a garment could have been made. Pick the `boundary_type`
that fits and give the year(s):

| `boundary_type` | Meaning | The engine flags when… |
|---|---|---|
| `earliest` | The feature can't appear *before* this year (e.g. a symbol introduced in 1988). | …the claimed era **ends before** `year_from`. |
| `latest` | The feature can't appear *after* this year (e.g. a mark discontinued in 1952). | …the claimed era **starts after** `year_to`. |
| `range` | The garment sits **between** two years (e.g. a country that only existed 1949–1990). | …the claimed era **doesn't overlap** `year_from`–`year_to`. |
| `era` | A softer "this looks like decade X" signal, no hard boundary. | …used as a gentle nudge only. |

And each rule is either:
- **`hard`** — regulatory / legal / standardised / geopolitical fact. The engine raises a **firm
  contradiction**. Needs a source.
- **`soft`** — a typical/heuristic tell (construction, materials trends). The engine raises a
  **gentle, hedged "worth checking"** note — never stated as certainty.

That hard/soft split is important: it keeps the tool honest and stops it crying wolf.

---

## 3. Fields for each rule

Copy the blank block in §6 for every rule. Fields marked **(required)**.

| Field | | What to write |
|---|---|---|
| `id` | **(req)** | A stable code, `CATEGORY-###` (e.g. `CARE-001`). Just keep them unique. |
| `category` | **(req)** | One of the families in §5. |
| `title` | **(req)** | Short human name. |
| `tell` | **(req)** | The observable feature — what you actually see on the garment. |
| `how_to_detect` | **(req)** | Precisely where to look and what exactly indicates it, so it's unambiguous. A photo example is welcome. |
| `boundary_type` | **(req)** | `earliest` / `latest` / `range` / `era` (see §2). |
| `year_from` | cond. | For `earliest` and `range`. |
| `year_to` | cond. | For `latest` and `range`. |
| `confidence` | **(req)** | `hard` or `soft` (see §2). |
| `source` | **(req for `hard`)** | The authority — regulation, standard, brand history, reference. This also feeds the public dating guides, so a citable source is gold. |
| `applies_to` | opt. | Garment scope if limited (e.g. "nightwear", "knitwear"). Default: all. |
| `flag_message` | opt. | The wording the seller sees, in the Eden voice. If you leave it blank I'll generate one; but your voice is better. |
| `caveats` | opt. | Exceptions that should stop a false alarm — reproductions, later re-tagging, regional variants, overlaps. |
| `transition_lag_months` | opt. | For date-of-change rules only (per brief §3.7): how long an *outdated* feature could linger (label stock used up, warehouse time). The engine applies this tolerance on the **trailing** edge; the **leading** edge is always 0 (a feature can't appear before it existed). Default ~12 if unsure; leave blank if not a change-of-date rule. |
| `status` | **(req)** | `unverified` while you're researching; `verified` once you've confirmed it against a source. **Only `verified` rules ever affect output.** |

**You don't need to handle rules interacting.** If a garment trips several rules, the engine takes
the most restrictive boundary and treats rule-vs-rule conflicts as their own flag. One rule, one row.

---

## 4. Three worked examples (format only — verify the facts yourself)

```yaml
- id: MARK-001
  category: Utility & regulatory marks
  title: CC41 utility ("double eleven") mark
  tell: The "CC41" utility mark on the label.
  how_to_detect: Woven/printed "CC41" (two stylised circles + "41"), usually near the composition or brand label.
  boundary_type: range
  year_from: 1941
  year_to: 1952
  confidence: hard
  source: UK Board of Trade Utility Clothing Scheme, 1941–1952.  # confirm exact end date + a citable ref
  flag_message: "This piece carries the CC41 utility mark — it dates to the 1941–1952 utility scheme."
  caveats: Presence dates the piece; ABSENCE tells you nothing. Watch for reproduction/costume pieces.
  status: unverified

- id: ORIGIN-001
  category: Country-of-origin phrasing
  title: "West Germany" country of origin
  tell: Label reads "Made in West Germany" / "West Germany".
  how_to_detect: Country-of-origin wording on the maker/composition label.
  boundary_type: range
  year_from: 1949
  year_to: 1990
  confidence: hard
  source: The state styled "West Germany" existed 1949–1990 (reunification Oct 1990).  # confirm + cite
  flag_message: "The 'West Germany' label dates this to 1949–1990."
  caveats: "W. Germany"/"West-Germany" variants count too. Old label stock could linger briefly after 1990.
  status: unverified

- id: FASTEN-001
  category: Fastenings
  title: Nylon coil zip
  tell: A moulded nylon/plastic coil zip rather than individual metal teeth.
  how_to_detect: Continuous nylon coil teeth, not separate metal teeth.
  boundary_type: earliest
  year_from: 1960   # approximate — this is a soft tell
  confidence: soft
  source: Nylon coil zips became common from the 1960s (trend, not regulation).  # refine
  flag_message: "The nylon zip suggests 1960s or later — worth double-checking an earlier date."
  caveats: Metal zips continued alongside nylon; a replaced/repaired zip misleads. Soft signal only.
  status: unverified
```

---

## 5. Category checklist (to work through so nothing's missed)

Tick these families off as you cover them — most vintage tells fall into one:

- [ ] **Care & washing labels** — symbols vs written instructions; which symbol system/version; wash-temperature systems.
- [ ] **Fibre-content / composition labels** — mandatory-labelling wording; synthetic trade names (Crimplene, Terylene, Bri-Nylon, Courtelle, etc.).
- [ ] **Country-of-origin phrasing** — "Empire Made", "British Made", "West Germany", "Czechoslovakia", "USSR", "British Hong Kong" vs "Made in Hong Kong", "Made in England" typography.
- [ ] **Utility & regulatory marks** — CC41 utility mark; kitemarks; other regulatory marks.
- [ ] **Brand-label typography / logos by era** — St Michael / M&S label eras; other brands' label/logo changes.
- [ ] **Fastenings** — metal vs nylon/plastic zips; zip makers (Lightning, Talon, YKK…); Velcro; hook-and-eye styles.
- [ ] **Sizing systems** — imperial vs metric; UK size-number system changes; bust-measurement vs modern numbering.
- [ ] **Barcodes / retail codes** — EAN/retail barcodes on labels (a later-era signal).
- [ ] **Registered numbers** — RN / CA / WPL numbers (mainly US-made, but sold in the UK).
- [ ] **Union labels** — ILGWU etc. (US brands, datable label variants).
- [ ] **Construction / fabric tells (mostly `soft`)** — seam finishes, overlocking, selvedge, print styles.

Add any family I've missed.

---

## 6. Blank template — copy one per rule

```yaml
- id:
  category:
  title:
  tell:
  how_to_detect:
  boundary_type:      # earliest | latest | range | era
  year_from:
  year_to:
  confidence:         # hard | soft   (= brief's HARD | SOFT strength)
  source:
  applies_to:
  flag_message:
  caveats:
  transition_lag_months:   # trailing-edge tolerance for change-of-date rules; leave blank otherwise
  status:             # unverified | verified   (only verified rules affect output)
```

**Prefer a spreadsheet?** The same fields work as columns — one row per rule. Use whatever's faster
for you (this doc, a Google Sheet, whatever); I can ingest either. The only things I really need are:
consistent **boundary_type + years**, the **hard/soft** call, and a **source** for the hard ones.

---

## 7. Handoff

Write as many `verified` rules as you can, in any order. When there's a batch worth wiring up, I turn
this into the machine-readable dataset the engine runs on — and the same rules + sources become the
material for the public dating guides. You never touch code; you own the vintage knowledge.
