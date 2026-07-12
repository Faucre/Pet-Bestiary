# Pet Bestiary

Pet Bestiary is a tModLoader 1.4.4-first mod that unlocks pet summon items into a virtual bestiary and lets players display, organize, dye, randomize, and manage multiple normal pets and light pets at once.

The mod is designed around a virtual pet system rather than Terraria’s native single pet/light pet equipment slots. Pet items and dye items are never moved, stored, consumed, duplicated, or deleted by the mod.

## Implemented Scope

### Core Pet Bestiary

- Virtual unlock and active pet state stored in `ModPlayer`.
- Registry scan for vanilla and modded pet summon items where they expose `Item.buffType`, `Item.shoot`, `Main.vanityPet`, or `Main.lightPet`.
- Modded pet names are sourced from loaded buff, projectile, or item localization when available.
- Modded unlock hints remain `???` unless a reliable curated hint is added.
- Inventory scanning unlocks pets permanently for the player once their summon item appears in inventory.
- Bestiary-style UI titled `Pet Bestiary`.
- Default hotkey: `P`.
- Manual Pet Bestiary toggle button next to the trash icon.
- Top-right close button for manually closing the Bestiary window.
- Normal Pets, Light Pets, and Presets tabs.
- Search bar for quickly finding pets.
- Collection progress bar at the bottom, similar to Terraria’s in-game Bestiary.
- Origin/source filtering, such as Vanilla, Calamity Mod, and other detected mods.
- Pet entries prefer projectile sprites over summon item icons.
- Click unlocked entries to activate or deactivate pets.
- Right-click active entries to lock or unlock them.
- Equip All and Unequip All for the current pet category.
- Dice/random pet button that equips a random unlocked pet that is not currently equipped.
- Pet limits increased up to 9999 for each category.
- General UI cleanup and layout polish.

### Presets

- Per-player presets for:
  - Active normal pets
  - Active light pets
  - Locked pet state
  - Pet dye assignments where applicable
- Presets respect progression limits where progression mode is enabled.
- Presets skip missing or unloaded pets safely instead of crashing.

### Progression Mode

- Optional progression mode for unlocking more pet slots over time.
- Progression mode status is shown through the collection progress bar tooltip.
- Progression mode is intended for players who want a more balanced experience, especially when using mods that make pets more impactful.

### Pet Spacing

- Visual pet spacing has been implemented.
- Pet spacing is controlled through a spacing preference slider.
- Spacing is visual/cosmetic and does not intentionally rewrite pet AI.
- Pets may still overlap in some edge cases, especially with unusual custom-drawn pets or large pet groups.

### Prismatic Palette

Pet Bestiary now includes the `Prismatic Palette`, a virtual dye selector for pets.

- Dye items unlock in the Prismatic Palette when they appear in the player’s inventory or dye slots.
- Dyeing pets no longer requires currently owning or holding the dye.
- Once a dye is unlocked, it can be reused as many times as desired.
- Dyes can be applied to as many pets as desired.
- Clicking the dye option on a pet opens the Prismatic Palette.
- Search bar for finding dyes quickly.
- Dice/random dye button.
- Dye All for currently equipped pets.
- Clear All Dyes for currently equipped pets.
- Per-pet dye assignments store dye item identity and shader IDs only.
- Real dye items are never moved, stored, consumed, or cloned.
- The pet preview panel now reflects assigned dyes.

### Debug Tools

When `DebugMode` is enabled, the mod exposes development/testing controls such as:

- Unlock All Pets
- Relock All Pets
- Clear Active Pets
- Resync Native State
- Clear dye-related state where applicable

Debug tools are intended for testing and troubleshooting, not normal gameplay.

## Safety Rules

Pet Bestiary does not create real pet equipment slots.

The mod does not store, move, consume, duplicate, or delete pet summon items. Pet items remain wherever the player keeps them.

Per-pet dye follows the same rule. Dyeing a pet records a virtual reference to an unlocked dye and leaves the actual dye item untouched.

Unlocked Prismatic Palette dyes are virtual unlock records. The mod does not store or consume dye items.

This safety rule exists because earlier experiments with custom dye slots could sometimes cause dye items to be eaten. Pet Bestiary intentionally avoids that approach.

## Native Pet Slot Handling

- Pet item use is intercepted and routed through the virtual bestiary state where possible.
- Recognized native pet buffs/projectiles are cleared without touching inventory or misc equipment items.
- This avoids conflicts with Terraria’s native pet and light pet slots.
- Direct projectile maintenance is used for active Pet Bestiary pets.

## Bug Fixes / Recent Fixes

- Fixed an issue with `Resplendent Dessert` that kept it from summoning both pets at once.
  - This is currently a targeted fix.
  - A broader fix may be needed later for similar multi-pet summon items.
- The preview panel now reflects assigned dyes.
- Preview animations for many pets have been corrected.
- Fixed dyed pets using incorrect animations.
- Fixed an issue where pet dyes broke after the first 10 dyed pets due to vanilla draw behavior limits.
- Fixed a bug that played the summon pet sound every tick when equipping a pet into a normal pet slot.
- Fixed vanilla pet unlock conditions.
- Added tentative multiplayer sync fixes.
  - These have been tested, but more multiplayer testing is still needed.

## Current Limitations / Known Bugs

- No spoiler mode yet.
- No Pets Overhaul compatibility yet.
- Some modded unlock conditions may be unknown and display as `???`.
- Some pets may require custom handling if their projectile AI depends on unusual item, buff, or equipment state.
- Some custom-drawn modded pets may not support dye perfectly.
- Certain custom pets added by other mods can disable dyes for the rest of your pets.
  - Currently known pet with this behavior: `Thief's Dime` from Calamity Mod.
- Some pets cannot be displayed fully or at all in the preview.
  - Known examples include `Exo Gemstone` and `Miniature Elemental Heart` from Calamity’s Vanities.
- Multiplayer sync has tentative fixes, but more dedicated multiplayer testing is needed.
- The mod avoids maintaining pet buffs because vanilla/tModLoader pet buff behavior can remove other pet buffs in the same category.

## Dye Rendering Notes

Per-pet dye defaults to a vanilla-preserving draw hook that temporarily applies the selected dye shader through `Player.cPet` for normal pets and `Player.cLight` for light pets while Terraria draws the pet normally.

This approach is preferred because it preserves vanilla pet animations, custom offsets, and extra draw layers better than manually redrawing pets.

`ManualShaderExperimental` is kept as a fallback/debug path, but it replaces vanilla drawing and can break special pet offsets, extra layers, or custom animation.

Dye diagnostics can be run even when `EnablePerPetDye` is disabled.

Useful diagnostics include:

- `VanillaHookLogOnly`
- `VanillaHookForcePetField`
- `VanillaHookForceLightField`
- `VanillaHookForceBothFields`

These help isolate whether vanilla draw reads `Player.cPet`, `Player.cLight`, or both for a specific pet.

`PetRuntimeDebugMode.MaintainActivePetBuffs` is a debug-only experiment for checking whether missing vanilla pet buff state is the cause of broken pet animation.

## Mod Compatibility

Pet Bestiary attempts to detect pets from other mods automatically.

If a modded pet can be identified as a normal pet or light pet, it should appear in the Bestiary. However, modded pets can be implemented in many different ways, so some may have:

- Unknown unlock conditions
- Missing or unusual icons
- Limited dye support
- Limited preview support
- Custom draw behavior that requires compatibility handling

Future compatibility work may include curated unlock hints and targeted fixes for popular pet mods.

## Planned Features

- Spoiler mode:
  - Hide locked pet appearances
  - Hide locked pet names
  - Hide unlock conditions
- Optional duplicate pets
- Better modded pet compatibility
- Pets Overhaul compatibility
- More filtering/search tools
- More preset polish
- More progression polish
- Better handling for unusual custom-drawn pets
- Broader fix for multi-pet summon items similar to `Resplendent Dessert`
- Additional multiplayer testing and sync polish

## Porting Notes

tModLoader 1.4.4 still includes Tartar Sauce and the Mini Minotaur pet.

Terraria 1.4.5 replaces them with the Beguiling Lyre and Faun, so the vanilla hint table and any saved-key migration should map `Terraria/TartarSauce` to the 1.4.5 item when stable tModLoader support exists.

## Development Notes

This mod is intended for tModLoader 1.4.4.

Builds should be tested through tModLoader where possible, especially for:

- Pet spawning
- Dye rendering
- Multiplayer sync
- Modded pet compatibility
- Custom-drawn pet behavior

## Credits

Developed by Luminous Blaze.

Pet Bestiary is a complete rebuild of the previous `More Pet Slots` mod.