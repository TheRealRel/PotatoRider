# How riding works in Valheim, and how PotatoRider changes it

A write-up of what we measured while building PotatoRider 1.0.0 on the Odin Horse (Odin Horse 1.9.2, Better Archery 2.0.5). Most of it applies to any saddled creature and any mod that touches mounted movement, the mounted camera or mounted combat. The code is in `src/`; every number below comes from an in-game log, not from reading code alone.

**Where it runs.** When a player gets on, `Sadle.RPC_RequestControl` hands ownership of the creature to the rider (`ZDO.SetOwner`). From then on the creature's AI, movement and rotation run on the rider's own game, and everyone else sees it through the normal transform sync. So everything here is client-side: the server needs nothing, and a player without the mod simply rides the default way. A new rideable creature is different. Its prefab has to exist on the server and on every client, or the server deletes it.

## Steering

**The base game steers with the camera.** `Player.SetControls` passes the raw keys to `Sadle.ApplyControlls(moveDir, lookDir, run, autoRun, block)`. A and D (`moveDir.x`) are ignored. The saddle keeps a speed state and a direction (`m_controlDir`), and the direction is only taken from the camera's look direction while W, Shift or block is held:

| Input | Saddle speed | Notes |
|---|---|---|
| Shift | Run | Wins over everything else |
| W (`moveDir.z > 0.5`) | Walk | Sticky: keeps walking after W is let go |
| S (`moveDir.z < -0.5`) | Stop | |
| Block, nothing else | Turn | Ignored while the saddle is Walk or Run |

`Sadle.UpdateRiding` then calls `MonsterAI.MoveTowards(m_controlDir, run)`, or `LookTowards` for Turn. It's called from the creature's AI update, which runs about 20 times a second, not every physics step.

**PotatoRider's version.** The saddle is always told to go straight ahead, the way the creature faces. W walks, Shift gallops, and letting go sends Stop. A or D without W first sends Stop, and then Turn, because a moving saddle ignores Turn.

**Why it turns the creature itself.** A creature turns in `Character.UpdateRotation` toward its move direction, and its turn rate scales with how far off that direction is:

`rate = turnSpeed × sqrt(min(|angle to target|, 90°) / 90°)`

So a target 25 degrees to the side turns it at about half speed. The first version did this, and the log showed 39 degrees a second against Odin Horse's 75. A target 90 degrees off reaches full speed, but the creature then walks toward the target while its body lags behind, so it crabs sideways. PotatoRider instead does the turn itself, in a prefix on `Character.UpdateRotation`:
- It turns the creature by `input × turnSpeed × dt`, using the creature's own `m_turnSpeed` walking and `m_runTurnSpeed` galloping (Odin Horse: 75 and 120).
- The input eases in and out over a quarter second.
- It turns `m_moveDir` and the saddle's direction with the creature, so it always walks the way it faces.
- It returns the turn rate in radians a second, the value the game itself would return.

**The turn has to happen every physics step.** The second version turned the horse from `Sadle.UpdateRiding`. Because that runs at the AI rate, the log showed the horse turning in 3.75-degree jumps about 20 times a second. `Character.UpdateRotation` runs every physics step (50 a second), which gives steps of 1.5 degrees at 75 degrees a second.

**Turning legs come for free from `turn_speed`.** `UpdateWalking` smooths the rate `UpdateRotation` returns into `m_currentTurnVel` and sends it to the animator as `turn_speed`, beside `forward_speed` (the speed along the body). The Odin Horse's Movement state is a 2D blend of the two:

| Position (turn_speed, forward_speed) | Clip |
|---|---|
| (0, 0) | idle tree |
| (0, 1) | horse_walk |
| (-1, 0) / (1, 0) | horse_walk_L / horse_walk_right |
| (0, 3) | horse_trot |
| (0, 6) | horse_galloping |
| (-2, 6) / (2, 6) | horse_galloping_L / horse_galloping_R |

So turning in place plays the walk-left and walk-right legs once the turn rate is reported. Turning directly from outside reported 0, so the legs stood still. The log after the fix showed walk-left as the main clip on most frames while turning left in place. There are no backwards clips, and `forward_speed` only reads forward speed, so backing up would slide the horse on idle legs. PotatoRider leaves S as Stop.

**Odin Horse's "Speed Modifier" is the mounted walk speed.** Its description calls it a multiplier, but the code writes it straight into `Humanoid.m_speed`. A saddle's Walk moves at `m_speed` (in m/s, times the jog factor), so the default of 1 crawls at 1 m/s. At 6 the horse plays its gallop clip while "walking". At 3 it trots, which is what our server uses.

**Speeding up.** `Character.UpdateWalking` raises a creature's speed by `m_acceleration` m/s every physics step (not scaled by time), slows it twice as fast, then eases half of the change in. Measured on the Odin Horse from standing: full walk in about 0.7 s, full gallop (12 m/s) in about 1.5 s. While ridden and not galloping, PotatoRider sets the step to `m_speed × fixedDeltaTime / seconds`, so W reaches its top speed in "Seconds to full trot" (0.75 by default). Slowing down, whether from a gallop to W or to a stop, eases at that same rate. Shift keeps the horse's own build-up.

## The camera

**It rolls onto its side during mounted attacks.** While attached, `GameCamera.ApplyCameraTilt` takes the camera's up from `player.GetVisual().transform.up`. The camera's direction comes from `player.m_eye`, a child of the player that tips with the rider. Riding attacks lean the rider, so the horizon rolled up to 90 degrees. The fix, applied while attached to anything but a ship:
- re-level the eye with `Player.UpdateEyeRotation` just before `GameCamera.UpdateCamera`;
- use world up in `ApplyCameraTilt`, a transpiler that swaps `GetVisual` for a level object.

**Turning with the horse.** With key steering, the camera keeps its place relative to the horse: each frame, the change in the saddle's facing (`Player.m_attachPoint`) is added to the player's `m_lookYaw`, and the mouse looks around on top of that.

**The horse's facing changes in steps, so the camera eases after it.** The log showed it changing only on frames that had a physics step: about 0.8 degrees, then 0, then 0.8, at 80 fps against 50 physics steps a second. Copying that each frame made the whole view judder while turning. The camera now takes the owed turn with an ease of 0.06 s, which leaves it smooth. Sudden turns over 30 degrees are taken at once.

## Mounted archery

**Odin Horse aims from the saddle.** Its `GetProjectileSpawnPoint` postfix clamps the arrow's direction in the space of `Player.m_attachPoint`: yaw to "Bow Turn Left" and "Bow Turn Right", pitch to "Bow Aim Up Down". The camera wasn't clamped, so past the arc the crosshair pointed where no arrow could go:

| Camera vs the horse | Arrow went | Landed from the crosshair point |
|---|---|---|
| -132 | -110 (the clamp) | 8.55 m at 11 m |
| -123 | -111 | 2.0 m at 8 m |
| -54 to +26 (inside the arc) | within 1 to 3 degrees of the camera | 0.2 to 2.7 m under 25 m |

PotatoRider clamps the camera the same way while a bow is drawn (`Humanoid.IsDrawingBow`), reading Odin Horse's own arc settings live. It started 5 degrees inside the arc, to allow for the arrow leaving from the bow hand. The log showed arrows at the 105-degree stop leaving at exactly 105, so the margin is now 0, and the stop is at 110.

**Better Archery made the rider flicker.** Better Archery's `Player.LateUpdate` postfix (`BetterArchery.Player_LateUpdate_Patch`) sets the spine's world rotation toward the camera every frame while a bow is drawn, and it has no setting to turn that off. Odin Horse's `RestoreAnimatedPose` decides whether the animator wrote the pose this frame by comparing `localRotation` with what it wrote last time (`WrittenLocal`). Better Archery's write breaks that comparison. The player animator runs on the physics clock (`updateMode` Fixed), so on frames without a physics step, Odin Horse treats the twisted pose as freshly animated and twists it again.

Logged bones: the spine was rewritten after the twist in 1,038 of 1,211 frames, and the chest jumped about 31 degrees back and forth. PotatoRider skips Better Archery's spine turn while attached to anything but a ship, and still runs its nocked-arrow update. After the fix: 0 rewrites, and 1 jump over 15 degrees in 2,051 frames. Turning off Better Archery's "Enable Nocked Arrow" also removed a fast flash along the bow's edge. That one hasn't been measured yet.

**The long shots** (45 to 80 m) landed 12 to 17 m short of the crosshair point while leaving within 2 to 5 degrees of it, which looks like arrow drop. Shots on foot at the same range are needed for a comparison before calling it.

## Smaller fixes

**A ridden horse rears to attack on its own.** The AI keeps attacking nearby enemies while someone rides it. A `Humanoid.StartAttack` prefix blocks attacks while the Odin Horse has a rider; unridden, it fights as usual.

**The mount panel showed a Lox.** `Hud.UpdateMount` sets the panel's `overrideSprite` to the saddle's `m_mountIcon`. The Odin Horse's saddle (`Horse_saddle` in its asset bundle) has none, so the panel falls back to its default picture, the Lox. When the icon is empty, or is the Lox's on something that isn't a Lox, PotatoRider renders the creature itself once (Jötunn's `RenderManager`, three-quarter view) and uses that.

## How it was measured

PotatoProbe is a test-only mod that is never released and changes nothing on screen. It writes `BepInEx/PotatoProbe.log`:
- **ARROW lines:** where the crosshair pointed at release, the launch angle off it, the camera and arrow yaw against the rider's facing, and where the arrow landed.
- **STEER lines:** once a frame while riding. They record the keys, the mouse, the saddle's speed state, the horse's and camera's turn that frame, the physics steps that frame, the camera relative to the saddle, the bow draw, and the horse's `turn_speed`, `forward_speed` and main clip.
- **Bones:** the rider's spine and chest at three moments in the frame (animated, after Odin Horse's twist, end of frame). Used for the flicker.

Each round was one ride, then reading the log, then one change. The guesses that came before the probe (sitting Better Archery out, narrowing the arc, bisecting configs) cost several rounds without progress. The probe found the flicker in one.

Each piece has its own live setting (PotatoRider in Configuration Manager), so they can be compared mid-ride.
