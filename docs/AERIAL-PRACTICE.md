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
| Air Dash | `[` / `]`, controller shortcuts off | One collision-aware lateral reposition per jump without steering or changing velocity. |
| Fast Fall | Down Arrow, legacy MenuTriangle action | Keyboard and controller button can now both be selected. |
| Practice snapshots | F5 / F6 | Manual save/load with expanded driving/boost state. |
| Rewind / Practice Scrubbing | Backspace | Tap to rewind three seconds; hold to scrub farther; release to resume on a new timeline. |
| Mine Traps | Normal fire for inventory mines; M for free test drop | Item-box mines and an optional independent testing shortcut. |

## Air Dash

The ground-testing mode is removed. Air Dash applies a brief lateral target at
the final `KinematicCharacterController_ICharacterController_UpdateVelocity`
boundary, after native damping. It conserves the kart's original horizontal
speed, keeps its heading and vertical velocity, and adds a brief cyan directional
streak. It does not invoke the Teleport Dash item or its portals, reserves,
immunity or target logic; it matches a quick lateral burst, not the item's full
visual effect.

Suggested starting values: **3 metres over 0.12 seconds** (try 2-4 m and
0.1-0.2 s). Both settings have no upper ceiling. Speed conservation can reduce
the forward component during the burst. Air Dash only works while airborne and
allows one dash per jump by default; repeated dashes can be enabled. A hideable
status line reports grounded, ready, spent and dash distance. Visible movement
and streak rendering still need a fresh in-game test.

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
current fix leaves the spawned prefab's `MineBehavior`, trigger callbacks and
animation enabled, disables only its throw-to-ground attachment, arms it via
`ActivateMine`, and does not destroy the object when it explodes so its native
effect cleanup can finish. The test drop and inventory fire path use this same
trap behavior.

When enabled, **Include in item-box rewards** defaults on: each local reward has
a configurable **20%** chance to replace its first item with one mine. The second
reward remains intact. Mines use native inventory ammo and the normal item-fire
button. If a character's weapon set lacks the dormant mine slot, a temporary
local weapon shell supplies valid inventory/animation references and displays
the shipped mine model. Original assets are never edited. Temporary slots and
modified mine-targeting settings are restored/removed on cleanup.

A mine is placed on a track surface behind the kart, remains stationary, arms
after **0.6 seconds**, and expires after **45 seconds** by default. Native trigger
contacts remain enabled; an offline proximity scan also calls the native
`ExplodeMine` method for karts inside the configurable **2.5 m** radius. Owner
triggering defaults on for solo tests. Zero damage override keeps native damage.
No track surface behind the kart means no placement. Visible damage and effects
remain unverified until a new in-game test.
The extra **M** shortcut drops a free testing mine; select **None** to rely only
on item-box inventory. Its controller shortcut is also selectable. Arming time,
lifetime, drop distance, reward ammo and damage have suggested values
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
checks; compiled plugin checks; deterministic side-dash distance tests;
history branching/expiry tests; and online-protection policy tests. These do not
prove native hooks execute or that traps look correct in game.

Manual acceptance still required: visible airborne side dash and streak,
repeated dashes, trick/boost/Fast Fall
combinations, restoring an active shift, physical controller mappings, item-box
mine pickup, normal ammo consumption, mine arming/contact damage/expiry, and blocked submissions after
mutation even when modules are switched off.
