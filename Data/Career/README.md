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

## `market.json`

Transfer market (B5, baseline v1, X-43): value, reference wage, negotiation, contracts, squad size band and the market AI's budget proxy. Recalibrate here; no code change needed.

| Section | Controls | Valid values |
| --- | --- | --- |
| `value` | Market value by OVR (curve), age (curve), potential above OVR, remaining contract years (curve), form (curve), division scale | Curves non-empty, strictly increasing X |
| `wage` | Reference season wage by OVR (curve), club reputation (curve), division scale | Curves non-empty |
| `interest` | Weights (reputation, division, wage, minutes) and the acceptance threshold for the player's side of a negotiation | 0–1 |
| `seller` | How much more than market value an important player, or one on a long contract, costs to buy; `financialWeight` stays neutral until the economy (B6) | — |
| `negotiation` | Up to `maxRounds` rounds; the club raises its offer and the wage each round | `maxRounds` > 0 |
| `contracts` | New-signing and renewal length ranges; a squad's top `renewalImportanceRank` by OVR are renewed when their contract ends this year, the rest expire | Ranges valid |
| `squad` | Size band the market AI targets (MVP_SCOPE/TEST_PLAN: 20–26) | `min` ≥ 11, `max` ≥ `min` |
| `budget` | Transfer-fund and wage-bill shares of the club `Budget`, a **proxy** until the economy (B6) replaces it with a real ledger | 0–1 |
| `ai` | Signings/releases per club per window; proposals to the managed club per window (X-43) | > 0 |
| `youthForeignSale` | Chance per window that a high-potential youth under `maxAge` is sold to a fictional foreign club (GAME_DESIGN §10) | 0–1 chance |

`CareerState.ManagedClubId` (X-43): when set, the market never signs, sells or trims that club; AI interest in its players becomes a `TransferProposal` instead (no UI to act on them yet — Track C). Left `null` (every headless test and tool), every club is AI-run.

## `economy.json`

Club economy (B6, baseline v1, X-44): TV, gate, sponsorship, maintenance, prizes and the negative-cash thresholds. Recalibrate here; no code change needed.

| Section | Controls | Valid values |
| --- | --- | --- |
| `tv` | Fictional R$ per match played, by division | Non-empty |
| `ticketing` | Share of the fan base attending a home match, ticket price by division, bonus share on a win | 0–1 for the shares |
| `sponsorship` | The single MVP sponsor, fictional R$ per season by division, spread monthly | Non-empty |
| `maintenance` | Fictional R$ per season by `Stadium.Level` (1–10), spread monthly | Non-empty |
| `prizes` | Paid at season end: league champion (`base × leagueChampionMultiplier[division]`, GAME_DESIGN §11 ratios D=1/C=3/B=8/A=40), cup winner/runner-up (`base × cupWinnerMultiplier`/`cupRunnerUpMultiplier`, Copa=25) | `base` > 0 |
| `cashAlert` | Consecutive negative months before `Club.CashAlert` (1) and before `Club.TransferLockout` (3) — the market (B5) stops signing for a locked-out club | `alertMonths` ≤ `transferLockoutMonths` |

Every revenue and expense goes through `Game.Career.Season.LedgerBook.Post` (including the market's transfer fees), so `Club.Balance` is always exactly the sum of the season's ledger plus every earlier season's archived net (`SeasonSummary.SeasonNetByClub`). `Club.Budget` (the market's funding figure) is re-synced from `Balance` at every month end and at season end: `max(0, Balance)`. The 6-month confidence drop from GAME_DESIGN §11 needs the board/confidence system (B7, pending) and is not implemented yet (see `DECISIONS.md`).
