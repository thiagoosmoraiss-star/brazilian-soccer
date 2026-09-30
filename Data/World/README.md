# Data/World

World-generation definitions (B1). Read by `GameDataLoader.LoadWorld` and checked by `WorldDefinitionValidator`. All values are **baseline v1** (X-37): recalibrate here, no code change needed.

| File | Controls | Valid values |
| --- | --- | --- |
| `names.json` | First/last name pools and weighted nationalities of generated players | Non-empty, unique entries; 3-letter nationality codes, weight > 0 |
| `cities.json` | Fictional cities (one per club) with a real UF (X-02) | Unique names; 27 valid UFs; at least `divisions × clubsPerDivision` cities |
| `club_templates.json` | Club name patterns (`{city}` placeholder) and the color palette | Every pattern contains `{city}`; colors `#RRGGBB`, unique ids, at least 2 |
| `crest_templates.json` | Crest shapes and generic symbols (crest = shape + symbol + the two club colors) | Non-empty, unique |
| `generation.json` | World size (D-12: 4 divisions × 16), per-division ranges (squad OVR mean, reputation 0–1000, budget in fictional R$, stadium level 1–10, fans), star thresholds, stadium capacity per level, squad slots and role offsets, age ranges, attribute generation, potential bonuses, secondary positions, height, foot, weak foot | Divisions listed from strongest to weakest; ranges min ≤ max; squad covers every position; per-position tables complete |

Club profile: each club draws a strength in [0, 1] that drives its squad OVR mean, reputation, budget, stadium and fans (with `clubProfileNoise`), so rich, big clubs are also the strong ones. Fans are interpolated on a log scale.

Player OVR: the squad targets are re-centred so the squad mean equals the club mean; attributes are generated around each target and corrected with the OVR weights (`Data/Balance/ovr.json`).
