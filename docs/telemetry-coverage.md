# SimHub telemetry coverage & gaps (for Race Notifier variables)

This documents what SimHub exposes **natively** in its normalized telemetry layer
(`GameReaderCommon.StatusDataBase`, surfaced to plugins via `DataUpdate(..., ref GameData data)`
as `data.NewData`), and what it **misses** — so we know the ceiling for "{variable}" message
substitution when we restrict ourselves to native SimHub data.

Reflected from the installed `GameReaderCommon.dll` on 2026-06-06: **257 normalized properties.**

## What SimHub normalizes (native, cross-game)

These are portable across every game SimHub supports. Categories (not exhaustive):

- **Flags:** `Flag_Green`, `Flag_Yellow`, `Flag_Blue`, `Flag_White`, `Flag_Black`, `Flag_Orange`,
  `Flag_Checkered` (each `Int32` 0/1) and **`Flag_Name`** (`String`, combined current-flag label).
- **Position / progress:** `Position`, `PlayerLeaderboardPosition`, `CurrentLap`, `CompletedLaps`,
  `TotalLaps`, `RemainingLaps`, `CurrentSectorIndex`, `TrackPositionPercent`.
- **Timing:** `CurrentLapTime`, `LastLapTime`, `BestLapTime`, `AllTimeBest`, sector times
  (`Sector1Time`…), `DeltaToSessionBest`, `DeltaToAllTimeBest`, `SessionTimeLeft`, `SessionTypeName`.
- **Fuel / consumption:** `Fuel`, `FuelPercent`, `MaxFuel`, `EstimatedFuelRemaingLaps`,
  `InstantConsumption_*`, `FuelUnit`.
- **Car state:** `SpeedKmh`/`SpeedMph`/`SpeedLocal`, `Rpms`, `Gear`, `Throttle`/`Brake`/`Clutch`,
  `IsInPit`, `IsInPitLane`, `PitLimiterOn`, `TCActive`/`TCLevel`, `ABSActive`/`ABSLevel`,
  `DRSAvailable`/`DRSEnabled`, `EngineMap`, `BrakeBias`, `Turbo`/`ERS`/`PushToPass`.
- **Tyres / brakes:** per-corner pressure, temperature (inner/middle/outer), wear, dirt, plus
  avg/min/max rollups; brake temps per corner.
- **Opponents:** `Opponents` list, `OpponentsAhead/BehindOnTrack`, `OpponentsCount`,
  `BestLapOpponent`, class-aware variants. (Gaps to specific cars are computed from this list.)
- **Spotter:** `SpotterCarLeft/Right` (+ angle/distance).
- **Track / session:** `TrackName`, `TrackId`, `TrackLength`, `TrackConfig`, `SessionOdo`,
  `IsGameReplay`, `Spectating`.
- **Physics / motion:** orientation (pitch/roll/yaw + velocities), acceleration (surge/sway/heave),
  `GlobalAccelerationG`.
- **Environment:** `AirTemperature`, `RoadTemperature` (that's essentially it — see gaps).

## What SimHub MISSES in the normalized layer

These are commonly wanted but **not** in the cross-game `StatusDataBase`. Some exist only inside a
specific game's raw data (see next section); others aren't exposed at all.

### Flags (most relevant to this feature)
- **No Red flag.** There is no `Flag_Red`. A session red flag does not appear in the normalized flags
  and is at best reflected loosely in `Flag_Name` for some games.
- **No waving vs. standing yellow**, and **no full-course-yellow / caution / pace** distinction. Only a
  single yellow boolean.
- **No per-sector / local yellow** state (which sector is yellow).
- **Meatball vs. penalty black are conflated.** `Flag_Orange` is the meatball (mechanical) flag;
  a disqualifying/penalty black is `Flag_Black` — but iRacing also has *furled* (warning) black,
  drive-through/stop-go context, and "repair required," none of which the normalized layer distinguishes.
- **No "one to green," "10/5 to go," green-held, start-ready/set/go** race-control states.
- `Flag_Name` is a coarse single label — good for a human-readable "current flag," not for logic that
  needs the exact race-control state.

### Other notable gaps
- **Weather:** only air & road temperature. No rain/precipitation, track wetness %, wind, humidity,
  air pressure, grip/rubber level, or forecast.
- **Tyre compound / tyre set:** not exposed (you get temps/pressures/wear, not which compound).
- **Incidents:** no incident count / "x" count or incident limit (iRacing's IncidentCount).
- **Penalties:** no drive-through / stop-go / time-penalty state.
- **Gaps in seconds** to the car directly ahead/behind are not a top-level scalar — they must be derived
  from the `Opponents` list.
- **Driver metadata:** no license / iRating / Safety Rating / team / car number in telemetry (that's
  session info, separate from `StatusDataBase`).
- **Damage semantics:** only generic `CarDamage1..5` + rollups; no per-component (engine/suspension/aero) meaning.
- **Session state machine:** `SessionTypeName` + flags only; no clean gridding/pace/racing/cooldown phase.
- **Pit service selections** (fuel added, tyres changed) aren't exposed; only `LastPitStopDuration`.

## How SimHub fills some gaps (and why we're not using it)

SimHub exposes each game's **raw SDK data** under `DataCorePlugin.GameRawData.*` (e.g. iRacing's full
`irsdk` telemetry, including the `SessionFlags` bitfield with ~25 flag states, `PlayerCarMyIncidentCount`,
weather, etc.). That can fill many gaps **but**:
- It is **game-specific** — `GameRawData` shapes differ per sim, so a message variable built on it would
  only work for one game and break for others.
- It bypasses the normalized layer, which is exactly the "native, portable" guarantee we want.

**Decision for Race Notifier:** variables resolve only from the **normalized** `StatusDataBase`. That
keeps `{flag}` working across every SimHub-supported game. The trade-off is the coarseness above — most
importantly, **no Red flag** and no waving/penalty distinctions. If those are ever needed, they'd require
opting into game-specific raw data per game (a separate, larger feature).

## Architectural constraint: telemetry is local to the sim PC

SimHub reads each game's telemetry from **local shared memory** on the machine running the sim. A SimHub
plugin therefore only sees telemetry for games running **on the same PC as SimHub**. There is **no native
SimHub mechanism to relay live telemetry into a plugin running on a different PC** (the mobile/remote
dashboards stream *rendered properties* to a viewer, not raw telemetry into another plugin's `DataUpdate`).

**Implication for testing `{flag}` with real data:** run iRacing **+** SimHub **+** the Race Notifier DLL
on the same machine (the sim rig). To observe from this dev box, watch that rig's
`...\SimHub\Logs\SimHub.txt` over the network share, or surface `Flag_Name` on a SimHub dashboard. We do
**not** need (and can't natively build) a cross-PC telemetry relay for this — the substitution engine is
unit-tested locally, and the live flag check happens where the sim runs.
