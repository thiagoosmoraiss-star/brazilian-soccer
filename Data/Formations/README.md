# Data/Formations

`formations.json`: MVP formations (TECHNICAL_SPEC §9 / MVP_SCOPE: 4-4-2, 4-3-3, 4-2-3-1). Each has 11 slots in order: `position` (GOL…ATA), `role` (GK, CB, FB, DM, CM, AM, W, ST) and a normalized base position (`x` 0 = own goal line … 1 = opponent goal line; `y` 0 = left … 1 = right). Validation: unique ids, exactly 11 slots, exactly one GK slot, x/y within 0–1. The slot order is the order of `MatchTeamSetup.Starters`.
