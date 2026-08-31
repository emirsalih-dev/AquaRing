# Aquaring — v0 Prototype

90's water-toy (Tomy Waterful) style game. A ring floats in a water tank; holding
the two bottom touch buttons fires water jets that push the ring up and sideways.
Goal: park the ring over the centre peg and hold it steady.

Unity **6000.3.16f1**, 2D URP, new Input System, portrait / Android.

---

## Get it running (2 minutes)

1. Open the project in Unity Hub (`~/UnityProjectss/AquaRing`).
2. Wait for the scripts to compile. A new **`Aquaring`** menu appears in the menu bar.
3. **`Aquaring ▸ Build Prototype Scene`** — generates `Assets/Scenes/Aquaring.unity`,
   wires every component, generates placeholder sprites into `Assets/Sprites/`,
   and registers the scene as scene 0 in Build Settings.
4. Press **Play**.
   - **Mouse:** hold the bottom-left / bottom-right buttons.
   - **Keyboard (editor convenience):** hold **A** / **D** (or ← / →).
   - **Device:** tap-and-hold the two buttons; both at once works.
5. Walk the ring from its start position onto the peg, keep it centred and calm
   for ~0.6 s → **"Ring landed!"** → **Try again** resets.

For an on-device test: **`Aquaring ▸ Configure Mobile (Android Portrait)`**
(locks portrait, IL2CPP, ARMv7+ARM64, app id `com.aquaring.prototype`, offers to
switch the build target), then `File ▸ Build And Run` with a device attached.

---

## How the "water feel" is faked (no fluid sim)

All physics is **2D only** — a single `Rigidbody2D` on the ring:

| Ingredient | Where | What it does |
|---|---|---|
| Buoyancy | `RingController.ApplyBuoyancy` | constant upward force cancelling ~82 % of gravity → slow, watery sink |
| Wobble | `RingController.ApplyWobble` | low-amplitude Perlin-noise force → idle bob & drift |
| Jets | `RingController.FireJet` | while a button is held, an off-centre `AddForceAtPosition` → lift + spin + a sideways nudge so you can steer |
| Damping | `Rigidbody2D` linear/angular damping | the "thick" resistance of water |

The **2.5D look** is entirely camera-side: a perspective camera raised and pitched
down ~9°, shaded placeholder sprites (gradient + highlight), and fake floor
shadows that shrink/fade with height (`GroundShadow`). The peg has **no solid
collider** — "putting the ring on the peg" = parking the ring in the peg's trigger
`CatchZone` and holding it steady.

---

## Project layout

```
Assets/
  Scripts/
    Gameplay/
      RingController.cs   – ring physics: buoyancy, wobble, jet impulses, freeze/reset
      PegTrigger.cs       – detects a seated ring (aligned + calm + held N seconds), raises OnRingSeated
      GroundShadow.cs     – cosmetic floor shadow that tracks the ring
    Input/
      IJetInput.cs        – read-only LeftHeld/RightHeld contract + JetSide enum
      JetInputRouter.cs    – hub: touch buttons + keyboard fallback feed this; RingController reads it
    UI/
      WaterJetButton.cs   – hold-to-fire pointer button (multi-touch friendly)
      WinPanel.cs         – "Ring landed! / Try again" overlay (owns no game state)
    Managers/
      GameManager.cs      – match flow: win detection, retry, ring spawn
      AppBootstrap.cs     – runtime: 60 fps target, no screen sleep (auto, no wiring)
  Editor/
    PrototypeSceneBuilder.cs   – "Aquaring ▸ Build Prototype Scene"
    MobileBuildConfigurator.cs – "Aquaring ▸ Configure Mobile (Android Portrait)"
    PlaceholderSpriteFactory.cs – procedural placeholder PNGs (ring, peg, tank, button, shadow)
  Scenes/    – Aquaring.unity is generated here
  Prefabs/ Materials/ Sprites/  – Materials + Sprites are populated by the builder
```

Everything is namespaced `Aquaring.*`. No assembly definitions — scripts live in
`Assembly-CSharp` / `Assembly-CSharp-Editor` for zero setup friction.

---

## Tuning

Select **Ring** in the scene and tweak `RingController` (buoyancy, jet force,
jet offset, side push, wobble, clamps). Select **Peg ▸ CatchZone** for
`PegTrigger` (hold-to-win time, alignment tolerance, require-calm).
`GameManager` holds the ring spawn position.

## Extending (the modular seams)

- **Second ring / more rings:** duplicate the Ring object; `PegTrigger` already
  keys off `RingController`. Give `GameManager` a list and a win-count.
- **Moving peg:** animate the `Peg` transform; `PegTrigger` uses `transform.position`
  live, nothing else changes.
- **Timer / score:** add to `GameManager`; it already owns state transitions.
- **New input source (tilt, gamepad, tutorial ghost):** implement `IJetInput`
  and feed `JetInputRouter`, or point `RingController._jetInputSource` at it.

---

## Notes / gotchas

- The **`Aquaring` menu only appears if all scripts compiled** — check the Console
  for errors first.
- The scene builder makes a **fresh empty scene**; save any unsaved work first
  (it prompts).
- Re-running the builder regenerates the scene from scratch but **reuses** the
  sprites/materials already in `Assets/Sprites` and `Assets/Materials` (delete
  those folders to force a regen).
- Active input handler is **"Input System Package" only** — the editor uses
  `InputSystemUIInputModule` with `AssignDefaultActions()`.
- If sprites ever render oddly under the perspective camera, select **Main Camera**
  and toggle **Projection → Orthographic** (size ≈ 4.7); the game logic is
  unaffected.
