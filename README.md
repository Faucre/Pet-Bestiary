# Pet Bestiary

Pet Bestiary is a tModLoader 1.4.4-first mod that unlocks pet summon items into a virtual bestiary and lets players display multiple normal pets and light pets.

## Implemented scope

- Virtual unlock and active pet state stored in `ModPlayer`.
- Registry scan for vanilla and modded pet summon items where they expose `Item.buffType`, `Item.shoot`, `Main.vanityPet`, or `Main.lightPet`.
- Modded pet names are sourced from loaded buff, projectile, or item localization when available; modded unlock hints remain `???` unless a reliable curated hint is added.
- Inventory scanning unlocks pets permanently for the player.
- Basic hotkey UI titled `Pet Bestiary`.
- Normal Pets, Light Pets, and Presets tabs with page buttons.
- Click unlocked entries to activate or deactivate pets.
- Right click active entries to lock or unlock them.
- Equip All and Unequip All for the current pet category.
- Per-player presets for active normal pets, active light pets, and locked pet state.
- Pet item use is intercepted and routed through the virtual bestiary state to avoid native pet buff/projectile conflicts.
- Recognized native pet buffs/projectiles are cleared without touching inventory or misc equipment items.
- Pet entries prefer projectile sprites over summon item icons.
- DebugMode exposes Unlock All, Relock All, Clear Active Pets, and Resync Native State controls in the Presets tab.
- Direct projectile maintenance for active pets.
- Per-pet dye assignments store dye item identity and shader IDs only; real dye items are never moved, stored, consumed, or cloned.
- The Prismatic Palette unlocks dye items when they appear in the player's inventory or dye slots, then lets pet dyes be selected from the Bestiary without holding the dye item.

## Safety rules

This version does not create real pet equipment slots and does not store, move, consume, duplicate, or delete pet summon items. Pet items remain wherever the player keeps them.
Per-pet dye follows the same rule: assigning dye records a reference to the held dye item and leaves the item untouched.
Unlocked Prismatic Palette dyes are also virtual unlock records; the mod does not store or consume dye items.

## Current limitations

- No spoiler mode, advanced spacing, or Pets Overhaul compatibility yet.
- The mod avoids maintaining pet buffs because vanilla/tModLoader pet buff behavior can remove other pet buffs in the same category.
- Some pets may require custom handling if their projectile AI depends on unusual item, buff, or equipment state.
- Per-pet dye defaults to a vanilla-preserving draw hook that temporarily applies the selected dye shader through `Player.cPet` for normal pets and `Player.cLight` for light pets while Terraria draws the pet normally. `ManualShaderExperimental` is kept as a fallback/debug path, but it replaces vanilla drawing and can break special pet offsets, extra layers, or custom animation.
- Dye diagnostics can be run even when `EnablePerPetDye` is disabled. `VanillaHookLogOnly`, `VanillaHookForcePetField`, `VanillaHookForceLightField`, and `VanillaHookForceBothFields` help isolate whether vanilla draw reads `Player.cPet`, `Player.cLight`, or both for a specific pet. `PetRuntimeDebugMode.MaintainActivePetBuffs` is a debug-only experiment for checking whether missing vanilla pet buff state is the cause of broken pet animation.

## Porting notes

- tModLoader 1.4.4 still includes Tartar Sauce and the Mini Minotaur pet. Terraria 1.4.5 replaces them with the Beguiling Lyre and Faun, so the vanilla hint table and any saved-key migration should map `Terraria/TartarSauce` to the 1.4.5 item when stable tModLoader support exists.
