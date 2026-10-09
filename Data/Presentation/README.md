# Data/Presentation

Presentation tunables of the match scene (A7b, X-59). Nothing here changes the simulation; it is read by the Unity side
only (`GameDataLoader.LoadMatchView`) and validated on load like every other data file.

## match_view.json

| Field | Controls | Valid values |
| --- | --- | --- |
| `camera.pitchDegrees` | Broadcast camera elevation (GAME_DESIGN §16: ~30–45°) | 0–90 |
| `camera.farDistance` / `nearDistance` | Camera distance with the ball in midfield / near a goal or at a set piece (zoom dinâmico) | far ≥ near > 0 |
| `camera.zoomInGoalDistance` / `zoomOutGoalDistance` | Ball distance to the nearest goal line (m) where the camera is fully in / fully out | out > in ≥ 0 |
| `camera.farSideExtraPitchDegrees` | Extra elevation with the ball on the far touchline | ≥ 0 (total < 90) |
| `camera.followSeconds` / `zoomSeconds` | Time constants of the follow damping and of the zoom (s) | > 0 |
| `camera.lookaheadSeconds` / `maxLookahead` | The camera looks ahead of the ball along its velocity (s), at most this far (m) | ≥ 0 |
| `camera.fieldOfView` | Vertical field of view (degrees) | 0–180 |
| `radar.widthFraction` / `opacity` / `dotSize` | Radar width (fraction of the screen), translucency, dot size (reference px) | 0–1 / 0–1 / > 0 |
| `animation.strideMeters` | Placeholder run cycle: one leg cycle per this many meters | > 0 |
| `animation.legSwingDegrees` / `sprintLegSwingDegrees` | Leg swing running / sprinting | ≥ 0 |
| `animation.armSwingFraction` | Arm swing as a fraction of the leg swing | ≥ 0 |
| `animation.moveSpeedThreshold` | Below this speed (m/s) the player stands | ≥ 0 |
| `animation.kickSeconds` / `kickSwingDegrees` | Kick (pass/shot) leg swing duration and amplitude | > 0 / ≥ 0 |
| `hud.bannerSeconds` | How long an event banner (goal, half-time, restart) stays up | > 0 |
