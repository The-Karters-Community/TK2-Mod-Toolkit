# Aerial practice, shortcut selection, ricochet and mine traps

All four gameplay modules are in the source Studio's **Community Mods** pack.
They default off. The current source compiles against the installed BepInEx
interop, and Studio controls are checked separately. **Visible in-game behavior
of these revisions still needs validation.** No game is automatically launched.

## Compatibility policy

### Startup failure correction, 2026-10-19

User testing exposed three failed installs in `BepInEx/LogOutput.log`: the KCC
explicit interface method is exported by interop with underscores, the missile
passage check takes a float, and `WeaponsView.FireWeapon` takes an
`Il2CppStructArray<int>` rather than managed `int[]`. The previous compile-only
check missed all three reflection lookup failures. These signatures are now
corrected. Each feature resolves all of its hooks before applying them, and
rolls back its own patches if installation fails.

`tests/RuntimeHooks` loads the built plugin and installed interop without calling
game methods. It resolves the same 18 hook bindings used for installation and
checks Harmony handler argument types. Run after building:

```powershell
dotnet run --project tests/RuntimeHooks -- plugins/TK2.Customization/bin/Release/net6.0/TK2.Customization.dll 'C:\Program Files (x86)\Steam\steamapps\common\The Karters 2 Turbo Charged'
```

This verifies lookups and argument types, not visible gameplay. Air Shift reports
specific race/recovery/menu conditions instead of leaving an unchanged generic
waiting message when its callbacks are running. Mine reward selection and actual
trap placement are logged separately. Camera Setup's hotkey now requires its
main enable switch; disabling the module also closes the camera panel.

Following the user's 2026-10-19 instruction, there are no GameAssembly or
UnityPlayer binary-hash/version allowlists for module execution. Each feature
attempts its exact-signature hooks independently. A changed executable alone
never disables a module or the native profiler bridge. Missing methods, missing
internal calls and actual runtime exceptions are reported for the affected
feature. Offline guards and mandatory online/submission protection remain.
Hashes used in build receipts, backup integrity and config-change detection are
not compatibility gates and remain in place.

## Selecting controls

Every built-in keyboard shortcut has a selection menu, including Air Shift left
and right, Fast Fall, snapshot save/load, rewind and mine test-drop. The menu
includes letters, digits, function keys, arrows, modifiers, keypad keys and None.

Controller shortcut menus include named Xbox/PlayStation face buttons, bumpers,
triggers, stick presses, D-pad directions, Back/Share and Start/Options. Rewired's
`IGamepadTemplate` reads the assigned player's physical controller, independent
of racing action bindings. Raw button indices 0-31 provide a fallback for other
controllers. Existing Rewired action choices and saved custom action values
remain compatible. Blank means None (disabled). Practice Snapshots' existing
**Controller shortcuts** switch must also be enabled to use its controller menus.

| Module | Default shortcut | Behavior |
| --- | --- | --- |
| Air Shift | `[` / `]`, controller shortcuts off | Maintain a lateral correction for a chosen duration without changing heading or vertical velocity. |
| Fast Fall | Down Arrow, legacy MenuTriangle action | Keyboard and controller button can now both be selected. |
| Practice snapshots | F5 / F6 | Manual save/load with expanded driving/boost state. |
| Rewind / Practice Scrubbing | Backspace | Tap to rewind three seconds; hold to scrub farther; release to resume on a new timeline. |
| Ricochet Items | Normal weapon fire | Optional straight-firing bazooka and one wall bounce; homing missiles remain unchanged. |
| Mine Traps | Normal fire for inventory mines; M for free test drop | Item-box mines and an optional independent testing shortcut. |

## Air Shift

The correction runs at the final
`PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_UpdateVelocity`
postfix. The earlier implementation ran inside `PixelKartPhysics.UpdateVelocity`,
before the controller finished its own velocity changes, and applied a one-tick
impulse. The revised implementation maintains the selected lateral target across
physics ticks for **Shift duration**, so subsequent native lateral damping does
not immediately erase it. KCC still handles movement/collisions; this is not a
position teleport. Vertical velocity, steering rotation and native trick/boost
inputs are untouched, and Fast Fall remains independent.

Suggested starting values: **18 m/s lateral change, 0.25 seconds**. A maintained
18 m/s corresponds to 4.5 metres over 0.25 seconds before speed-budget effects and
collisions; this is a calculation, not an observed in-game displacement. Existing
saved strength values are preserved. Strength and duration have no upper limit
in Studio validation, exported settings or BepInEx bindings. Zero duration gives
an impulse. Suggested ranges are text hints rather than slider ceilings.

**Once per jump** can be turned off for repeated shifts. **Use horizontal speed
budget** can be turned off for unrestricted lateral additions. With the budget
on, multiplier 1 preserves the horizontal speed measured at shift activation;
0.9 reduces it, and values above 1 explicitly permit more speed. All tuning
numbers must be finite and non-negative. A hideable status line reports ready,
spent or active state and the applied lateral speed; accepted shifts are logged.

## Practice rewind and snapshots

Rewind retains its solo, current-checkpoint-segment scope. It records eligible
states at 20 Hz into a bounded rolling history and discards the abandoned future
when scrubbing ends. Scene/kart changes, checkpoint/lap changes, race reset,
finish, pause, teleport, recovery and disable clear history. Unresolved jump/trick
coroutines are not captured. The clock and other world objects are not rewound.

The shared driving snapshot restores motor/grounding, steering/drift, reserves,
boost-speed components, both fill arrays, boost smoothing and normal route/respawn
tracking. Timestamps are rebased. Pending jump/boost input and Fast Fall's latch
are cleared. Air Shift's used state, correction direction, target, budget and
remaining active duration are restored together. Manual snapshots retain their
health/item and experimental checkpoint/clock options. This is a partial driving
rewind, not a full world save; temporary external item effects, moving platforms,
battle-arena world/history and already-started recovery are not rewound.

## Ricochet behavior

The first implementation did **not** convert auto-aiming weapons to straight
shots. It only accepted an already non-targeting straight missile.

The revision adds **Straight-firing bazooka** (on by default within this
otherwise disabled module). The firing revision temporarily disables the local
bazooka's target requirement in both native `WeaponsController.FireInput(bool)`
and `Shoot()` calls, restoring the original settings in a finalizer. The native
code checks `bRequirePlayerTarget` before creating the projectile, so changing
missile initialization alone was insufficient. Local straight shots receive an
empty player-target list and do not auto-fire backward when front targets are
absent. Explicit backward fire remains available. It clears local bazooka target IDs, captures
kart heading at launch, preserves backward-fire direction, and owns the shot's
horizontal flight so target/muzzle tracking cannot bend it afterward. It does
not alter the homing-missile class. Turn the option off to retain ordinary
bazooka aiming; targeted shots then remain outside the bounce path.

A qualifying static physics wall (non-trigger collider without a Rigidbody,
excluding player/item/physics-trigger objects) reflects the projectile once.
Near-horizontal floors are excluded. Unity's static-batching checkbox is not
required. The old-target passage explosion is suppressed for owned straight or
bounced shots; native damage, visual height handling and lifetime remain. The
next impact follows the ordinary collision path. Existing owned shots finish
their path if the module switch is turned off mid-flight.

## Mine trap revival

The supplied image matches the shipped **SM_Mine_01** mesh. Current asset evidence:
`resources.assets`, mesh IDs 1427/1428, GameObject 11744; `MineEffect` GameObject
444508, `MineBehavior` component 1995543. The game's ResourceManager maps
**mineeffect** to that prefab. A local render of the shipped mesh confirmed the
shape. Extracted assets/render evidence remain ignored local files and are not
redistributed in the repository.

The native item enum is **MINA_09 = 9**. Current Ghidra inspection shows the
`WeaponsView.InitMine` body is empty and `InitEffect` omits case 9. The module
restores use through the normal `WeaponsView.FireWeapon` entry, using the real
mine prefab and native `MineBehavior.ExplodeMine` damage/effect path.

When enabled, **Include in item-box rewards** defaults on: each local reward has
a configurable **20%** chance to replace its first item with one mine. The second
reward remains intact. Mines use native inventory ammo and the normal item-fire
button. If a character's weapon set lacks the dormant mine slot, a temporary
local weapon shell supplies valid inventory/animation references and displays
the shipped mine model. Original assets are never edited. Temporary slots and
modified mine-targeting settings are restored/removed on cleanup.

A mine is placed on a track surface behind the kart, remains stationary, arms
after **0.6 seconds**, and expires after **45 seconds** by default. Trigger radius
is **2 metres**; owner triggering defaults off. Zero damage override keeps the
prefab's native damage. No track surface behind the kart means no placement.
The extra **M** shortcut drops a free testing mine; select **None** to rely only
on item-box inventory. Its controller shortcut is also selectable. Arming time,
lifetime, radius, drop distance, reward ammo and damage have suggested values
without artificial upper ceilings. Turning off the module cleans up its traps.

## Evidence and validation

Native evidence was read from the current installed binary, with exact interop
hook signatures additionally checked by the runtime reflection probe. Compilation
alone cannot validate reflection lookups. Local files are in
`local/aerial-practice-evidence/`. These are metadata and native pseudocode,
not recovered original C#. Relevant current RVAs include controller UpdateVelocity
`0x5F0EE0`, PixelKartPhysics
UpdateVelocity `0x5FC1A0`, StraightMissileController.Init `0x4ABD90`,
RaycastCollisionDetector.Hit `0x4AABB0`, WeaponsView.InitEffect `0x9AC930`,
InitMine `0x3FC4B0`, MineBehavior.ThrowMine `0xC37630`, Attach `0xC368D0`,
OnTriggerStay `0xC37220`, and ExplodeMine `0xC36EC0`.

Validation includes the full Python suite, UI tests/build and browser selection
checks; compiled plugin checks; deterministic sustained-shift/speed-budget tests;
history branching/expiry tests; and online-protection policy tests. These do not
prove native hooks execute or that traps/bounces look correct in game.

Manual acceptance still required: visible lateral displacement at saved 15 and
suggested 18, repeated/unrestricted shifts, trick/boost/Fast Fall combinations,
restoring an active shift, physical controller mappings, item-box mine pickup,
normal ammo consumption, mine arming/contact damage/expiry, straight bazooka
flight and one bounce, unchanged homing missiles, and blocked submissions after
mutation even when modules are switched off.
