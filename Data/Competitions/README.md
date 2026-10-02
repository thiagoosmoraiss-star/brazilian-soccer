# Data/Competitions

Competition formats and season calendar (B3). Recalibrate here; no code change needed.

| File | Controls | Valid values |
| --- | --- | --- |
| `competitions.json` | `league`: points (3/1), tiebreak order (GDD §5: wins, goal difference, goals for, head-to-head, cards, draw), card weights (yellow 1, red 3), promoted/relegated per adjacent divisions (**X-39: 4/4**). `cup`: 32 clubs, **8 qualifiers per division (X-40)**, host rule (lower division hosts), neutral final, first-season qualification by squad OVR. `matchRules`: bench size, substitutions, configured duration, AI tactic (mentality/line/pressure) | win > draw points; tiebreakers end with `Draw`; promoted = relegated; cup size a power of two; AI tactic within MVP ranges |
| `calendar.json` | Minimum days between matches of a club (3), league start (first Saturday of April) and weekly interval, cup round dates (first Wednesday of the listed months), transfer windows (January, July — GAME_DESIGN §4), season end | interval ≥ minimum; cup months increasing; one cup date per knockout round |

Season = calendar year. All four divisions play on the same Saturdays; cup rounds are drawn when their date arrives. Run a career headless with `dotnet run --project dotnet/CareerSim -- --seasons 10 --tables`.
