# Creature Autobattler Roguelite

Open `Assets/Scenes/Expedition.unity` in Unity **6000.6.0f1** and press Play. The game fits a 480 × 854 portrait viewport inside the Game view. Tap the highlighted map destination to scout, tap a formation slot and choose a creature, then press **Go**. You can move creatures between battles, view the upgrades you kept, and switch combat between 1× and 2× speed.

Edit `Assets/Prototype/Battle Balance.asset` in the Inspector to tune the same combat engine the scene and simulations use. `RunContent.cs` defines encounter lineups and draft cards. The four solo fights are followed by the Chainkeeper recruit and three duo fights. The run stays in memory until victory, defeat, or leaving Play Mode.

## Playtest defaults

| Rule | Initial value |
| --- | --- |
| Post-win recovery | 22 HP for survivors, capped at max HP. A knocked-out ally returns at 50% max HP when a teammate wins. Recruitment fills the kaiju’s HP. |
| Drafts | Three distinct offers at a time. Picked upgrades leave the eligible pool. Kaiju pool sizes are 9, 8, and 7. Support pool sizes are 9 and 8. |
| Placement | Both teams use the same column coordinates −2.22, 0, and 2.22. Front rows are 1.36 units from center with another 1.30 units between rows. Player positions persist across wins. |
| Simulation | 30 steps per second. Basic attacks start ready with a 0.16 s windup and a locked target. Abilities start at 45% of their cooldown and wait for a valid target or position. |
| Kaiju | 200 HP. Claw Swipe deals 15 base damage every 0.72 s. Stomp deals 23 damage within 1.53 units every 4.1 s. Flame Breath deals 19 damage plus three 4-damage ticks, 0.6 s apart, every 6.4 s. The passive adds up to 36% damage in proportion to missing HP. |
| Chainkeeper | 96 HP. The beam lasts 2.8 s, covers a 1.38-unit radius, and ticks every 0.55 s for 4.8 healing or 3.8 damage. The passive advances allied cooldowns 20% faster inside the field. The team shield absorbs 16 damage per ally for up to 3.35 s. |
| Wizard | 217 HP and 90 mana. Successful summons cost 30 mana and have a 2.35 s cooldown. Depleted mana recharges to full over 10 s. Up to three living Shadow Knights and three living Shadow Mages can be on the field. |
| Victory | Clear the enemy team in battles 1–6. Killing the wizard ends battle 7 immediately. |

Health-based targeting uses percentage of max HP. Equal distances or health percentages break ties in spawn order. Combat keeps fractional HP and damage, with the HUD rounding remaining HP up. The wizard prefers a front slot aligned with his current column for knights and the back slot farthest from the nearest mage for mages. Occupied slots fall back by distance, then row proximity and slot order. An edited mana pool with a remainder smaller than one cast enters recharge after the last affordable summon.

## Verification

Run **Creature Run → Verify prototype** from the Unity menu. The check verifies a comfortable Slime Crossing win from every starting slot and compares four strategies on the same 128 draft seeds.

| Simulated strategy | Expeditions won |
| --- | ---: |
| Fixed center formation, first offer each draft | 17/128 |
| Fixed center formation, prioritized drafts | 39/128 |
| Encounter-specific formations, first offer each draft | 101/128 |
| Encounter-specific formations, prioritized drafts | 125/128 |

A separate 243-run sweep of fixed placements and draft picks yielded 44 victories. The 199 defeats occurred at the Briar Gate (9), Astral Step (48), and Hollow Spire (142). These counts come from deterministic combat simulations.

The command also checks draft eligibility, preview and spawn positions, pathing, recovery, upgrade interactions, clone and blink behavior, and the wizard’s summons and victory condition.

## Design notes

- Reserve a third world, additional starters, and larger recruitment pools for a later prototype. The current flow offers one recruit and can display up to three offers.
- Creature portraits and expedition terrain are placeholder assets generated with the built-in ImageGen tool. The project includes the [minimap](Assets/Prototype/Art/ExpeditionMap.png) and [creature sprite sheet](Assets/Prototype/Art/CreaturePortraits.png). Inter is bundled with its license.

<details>
<summary>ImageGen prompts</summary>

**Minimap**

> Portrait 2D illustrated minimap background for a Unity fantasy roguelite. An overhead painted cartography view of a mossy temperate forest and steep blue-slate mountains traced by a muted teal river and small scattered ruined stones, with greener lowlands at the bottom, desaturated violet mountains and a subtle distant stone spire in the upper half. Warm old-paper etched contour lines blended with gouache texture. Deep pine green and muted blue-gray, hints of olive and antique gold, dark enough for light text and amber pins drawn by the game. Strong coherent geography readable at mobile size. Fill the full portrait frame, no border, no text, no numbers, no icons, no discrete path or pins, no interface. The game will draw its own route over this backdrop.

**Creature sprites**

> A single transparent PNG sprite sheet for a hand-painted fantasy creature autobattler. Landscape 4:3 canvas, EXACTLY FOUR COLUMNS and FOUR ROWS regular grid of equally sized cells, no separation lines, no texture behind subjects, no text, no shadows outside the individual subjects. Center one fully visible character in each cell with generous transparent padding on every side so cells can be cropped automatically. Readable silhouette, strong clear color separation, slightly chunky storybook gouache look, dark ink outlines, three-quarter front view, unified character scale; designs legible when reduced to 64 pixels. Specific order, reading left to right: row 1 teal squat baby kaiju with three small horns, amber belly, thick tail and four stubby legs; hunched deep-purple cloaked creature wrapped in silver chains with two spindly three-clawed feet and two tiny lavender eyes; jade green translucent cute slime with dot eyes; olive goblin with small sword and round wooden shield. Row 2 moss green goblin priest with pale staff and hood; midnight many-legged spider with crimson eyes; ochre hooded goblin archer with bow; animated broad black iron armor with blue spectral light and raised shield. Row 3 ivory and sage hooded healer mage with gold staff; purple illusionist with smoky lilac magic; teal-blue blink mage with cyan sparks; elderly wizard with white beard, tall plum hat, dark red cloak and red crystal staff. Row 4 dark purple shadow knight with heavy square black shield; dark lilac shadow mage with pointed hood and magenta orb; leave last two cells entirely transparent. Real transparent background, no captions, no numbers, no decoration in gutters.

</details>
