# Aerial practice, shortcut selection and mine traps

Air Dash, practice rewind and Mine Traps are in the source Studio's **Community Mods** pack.
They default off. The current source compiles against the installed BepInEx
interop, and Studio controls are checked separately. **Visible in-game behavior
of these revisions still needs validation.** No game is automatically launched.

## Compatibility policy

### Startup failure correction, 2026-10-19

Earlier testing exposed failed hook lookups: the KCC explicit interface method
is exported by interop with underscores, and `WeaponsView.FireWeapon` takes an
`Il2CppStructArray<int>` rather than managed `int[]`. These signatures were
corrected. Each feature resolves its hooks before applying them and rolls back
its own patches if installation fails.

`tests/RuntimeHooks` loads the built plugin and installed interop without calling
game methods. It resolves the same hook bindings used for installation and
checks Harmony handler argument types. Run after building:

```powershell
dotnet run --project tests/RuntimeHooks -- plugins/TK2.Customization/bin/Release/net6.0/TK2.Customization.dll 'C:\Program Files (x86)\Steam\steamapps\common\The Karters 2 Turbo Charged'
```

This verifies lookups and argument types, not visible gameplay. Air Dash reports
local input status without allowing bot callbacks to overwrite it. Mine reward
selection, placement, arming and triggers are logged separately. Camera Setup's hotkey now requires its
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

Every built-in keyboard shortcut has a selection menu, including Air Dash left
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
| Air Dash | `[` / `]`, controller shortcuts off | One lateral velocity burst per jump without turning the kart. |
| Fast Fall | Down Arrow, legacy MenuTriangle action | Keyboard and controller button can now both be selected. |
| Practice snapshots | F5 / F6 | Manual save/load with expanded driving/boost state. |
| Rewind / Practice Scrubbing | Backspace | Tap to rewind three seconds; hold to scrub farther; release to resume on a new timeline. |
| Mine Traps | Normal fire for inventory mines; M for free test drop | Item-box mines and an optional independent testing shortcut. |

## Air Dash

Air Dash adds a configurable lateral velocity impulse at the final
`KinematicCharacterController_ICharacterController_UpdateVelocity` boundary,
after native damping. The burst is held for its configured duration, preserves
kart heading and vertical velocity, and caps total horizontal speed at the
pre-dash value. It has no added streak or Teleport Dash effects.

Suggested starting values: **65 m/s for 0.25 seconds**. Both settings have no
upper ceiling. Air Dash only works while airborne and allows one dash per jump
by default; repeated dashes can be enabled. A hideable status line reports
grounded, ready, spent and current burst state. Visible movement still needs a
fresh in-game test.

## Practice rewind and snapshots

Rewind retains its solo, current-checkpoint-segment scope. It records eligible
states at 20 Hz into a bounded rolling history and discards the abandoned future
when scrubbing ends. Scene/kart changes, checkpoint/lap changes, race reset,
finish, pause, teleport, recovery and disable clear history. Unresolved jump/trick
coroutines are not captured. The clock and other world objects are not rewound.

The shared driving snapshot restores motor/grounding, steering/drift, reserves,
boost-speed components, both fill arrays, boost smoothing and normal route/respawn
tracking. Timestamps are rebased. Pending jump/boost input and Fast Fall's latch
are cleared. Air Dash's once-per-jump state is restored. Manual snapshots retain their
health/item and experimental checkpoint/clock options. This is a partial driving
rewind, not a full world save; temporary external item effects, moving platforms,
battle-arena world/history and already-started recovery are not rewound.

## Ricochet Items removed

The bazooka shot exploded on the ground and did not bounce reliably during
in-game testing. The feature and its controls have been removed; it is not
presented as usable until its projectile movement and collision path can be
verified in the game.

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
mine prefab and native `MineBehavior.ExplodeMine` damage/effect path. The
fixes leave the spawned prefab's `MineBehavior`, trigger callbacks and animation
enabled, use its native `ThrowMine()` forward toss and `ActivateMine()` arming,
and do not destroy the object when it explodes so native effect cleanup can
finish. The test drop and inventory fire path use this same trap behavior.

When enabled, **Include in item-box rewards** defaults on: each local reward has
a configurable **20%** chance to replace its first item with one mine. The second
reward remains intact. Mines use native inventory ammo and the normal item-fire
button. If a character's weapon set lacks the dormant mine slot, a temporary
local weapon shell supplies valid inventory/animation references and displays
the shipped mine model. Original assets are never edited. Temporary slots and
modified mine-targeting settings are restored/removed on cleanup.

The **Mine size multiplier** defaults to **5x**. It scales the MineEffect model
while compensating its native sphere collider, so the visible trap is larger
without making the game's contact volume five times larger. **Forward throw**
(default) launches a mine from ahead of the kart using Rigidbody velocity; it
then raycasts to the track and settles there. This bypasses native `ThrowMine()`,
whose prefab vector sent the mine backward in the user's build. **Drop behind**
does a stationary track-surface placement at the selected rear distance. Both
controls use the same trap lifetime and trigger behavior.

The fixed arming delay is **0.6 seconds**. At **45 seconds** the mine spawns its
explosion prefab at the mine's position and uses the game's native HP hit path
for every kart inside the configurable **10 m** damage radius. The remote owner
is never used as a fallback target; the native `ExplodeMine(target)` method is
avoided for timed detonations because it anchors the effect to the supplied
kart. An offline proximity scan triggers armed mines for karts inside the separate
**4 m** trigger radius. Owner triggering is always enabled. Damage defaults to
**80**. Visual throw, detonation and damage still need fresh in-game validation.

Mine settings are grouped into **Item boxes**, **Placement controls** and **Trap
behavior**. These groups keep reward odds/ammo, keyboard/controller shortcuts,
placement/size, and damage/timer/radii together without an arming-delay or owner
toggle that does not need player tuning.
The extra **M** shortcut drops a free testing mine; select **None** to rely only
on item-box inventory. Its controller shortcut is also selectable. Arming time,
lifetime, throw force, rear distance, reward ammo, trigger radii and damage have suggested
values without artificial upper ceilings. Turning off the module cleans up its traps.

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
checks; compiled plugin checks; deterministic lateral-impulse tests;
history branching/expiry tests; and online-protection policy tests. These do not
prove native hooks execute or that traps look correct in game.

Manual acceptance still required: visible airborne side dash,
repeated dashes, trick/boost/Fast Fall
combinations, restoring an active shift, physical controller mappings, item-box
mine pickup, normal ammo consumption, mine arming/contact damage/expiry, and blocked submissions after
mutation even when modules are switched off.
