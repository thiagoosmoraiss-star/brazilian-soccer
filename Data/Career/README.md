# Data/Career

`development.json` — players in the career (B4, baseline v1, X-42). Recalibrate here; no code change needed.

| Section | Controls | Valid values |
| --- | --- | --- |
| `weeksPerYear`, `ageCurve` | OVR points per year by age band (GAME_DESIGN §9: 16–21 fast, 22–26 slow, 27–30 stable, 31–33 light decline, 34+ steep); applied weekly | Ordered by `maxAge` |
| `potentialGapForFullGrowth` | Growth scales down as OVR approaches the hidden potential (never above it) | > 0 |
| `minutes` | Growth factor without minutes and the minutes share that counts as "full" | 0–1 |
| `staff` | Training centre / assistant coach factors — neutral (1.0) until B7 | > 0 |
| `decline` | Physical attributes lose first; chance for the other role attributes | 0–1 |
| `annual` | December adjustment: bonus for a good season (average rating, minimum appearances) | — |
| `condition` | Energy recovery per day; morale change chances after win/loss/draw; form = average of the last N ratings | — |
| `injuries` | Light/medium: club matches missed; severe: days out (GAME_DESIGN §9: 1–2 dates, 3–6 dates, 2–6 months) | Valid ranges |
| `suspensions` | 3 yellows = 1 match; second yellow = 1; straight red weights for 1, 2, 3 matches; per competition; reset each season | — |
| `retirement` | Retirement chance by age (last entry must be 1.0) | Ages increasing |
| `youth` | 3 youths per club per year (X-42), age, OVR offset from the squad mean, potential above OVR; squad floor (18, 2 goalkeepers) until the market (B5) | — |

Inspect a career with `dotnet run --project dotnet/CareerSim -- --seasons 10 --stats` (top-22 OVR per division, squad size, age, players above potential).
