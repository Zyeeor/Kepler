# Monster Design New — Owner Delta 2026-08-18

> **Status:** Owner-confirmed Design Intake source
> **Source:** Google Sheets「怪物设计（新）」+ Owner clarification in the 2026-08-18 design review
> **Scope:** Seven-Sins three-slot contracts, Monster-Type Cards, player-facing Card copy, and presentation requirements
> **Not in scope:** Code, Prefab, CardLibrary, numerical balancing beyond the explicitly confirmed playable baselines

## Global

- Input remains: `LMB = Attack`, `Space = Movement`, `E = Special`, `Q = Bullet Time`.
- Soul does not gain a normal attack.
- Values explicitly stated below are current `TUNABLE / PLAYABLE` baselines unless labeled as a fixed rule.

## Pride

- No baseline +5% movement passive.
- Movement: forward charge strike; baseline `Reload 2–5 / hp cost 5–25 / damage 20`.
- Attack: short-range aimed sword qi; baseline `Reload 1 / hp cost 5 / damage 20`.
- Special: 4 blinks at 0.25s each; baseline `Reload 1.5 / hp cost 20 / damage 10 per blink`; no legal target means no cast; one target can receive repeated hits.
- `PR-M01`: charge distance +50% and damage +50%.
- `PR-A01`: attack becomes three spread sword qi.
- `PR-A02`: cross sword qi.
- `PR-X01`: blink path appends sword qi in blink direction; it no longer applies to Movement.
- `PR-S01`: +2 blinks.
- New `PR-A04 王命远征`: sword qi gains longer legal range.

## Sloth

- Movement is a ground-directed launch/jump with visible wind-up. Baseline `Reload 1 / hp cost 20 / damage 0`.
- Attack remains hold-to-charge explosive cannon. Baseline `Reload 0 / hp cost 5 / damage 2–100`. Recoil is animation / feedback only, not gameplay displacement.
- Special throws a drone upward; it follows the player and repeatedly attacks the nearest enemy until death. Baseline: 4s lifetime, `Reload 1 / hp cost 50 / 5 damage per attack`, attack range 30.
- `SL-M01`: new mine mechanism, deployed before launch.
- `SL-M02`: landing causes immediate explosion.
- `SL-A03`: longer charge produces more scatter projectiles after explosion.
- `SL-A04`: fan-shot subprojectile damage is charged-cannon damage divided by subprojectile count, with a minimum damage of 3.
- `SL-S02`: any termination reason—natural expiry, destruction, or replacement—causes the drone to charge the nearest enemy and explode.
- `SL-S03`: drone attacks faster and continues pursuing the nearest enemy until it dies.

## Gluttony

- Basic Abyss Maw occurs under the caster; `GL-A03` enables bait thrown to a remote location, where the maw emerges.
- Devour hits only the nearest legal Enemy in its forward 1m fan and copies that enemy's E / Skill Ability.
- `GL-A02`: after Movement, the next Abyss Maw gains +100% area.
- `GL-A01` paired maws and `GL-A02` area bonus stack: both maws receive the area bonus.
- `GL-S03`: Devour consumes all legal flight attacks within the fan; exact legal-flight qualification remains the existing gameplay boundary unless separately changed.

## Envy

- Do not use highest-HP automatic targeting. Player keeps mouse aim; Enemy retains its existing target rule.
- Movement baseline: 1s flight, +200% movement speed, does not interrupt laser; `Reload 0 / hp cost 20 / damage 0`.
- Attack applies a target-owned mark. Each mark stores 20% of damage received by its target.
- Special chains lightning to all marked Enemies and consumes each target's own mark. No marks means the cast does not start, charge, or enter Reload. Baseline `Reload 1 / hp cost 50 / damage 10 + stored mark damage` per target.
- `EN-A01`: every 3 seconds, briefly attacks four total targets including the primary target.
- `EN-R01`: raises stored mark damage by 50%.
- `EN-S01`: retains its existing Stack 2 structure.
- `EN-TG01` is renamed to `万妒远证`.
- New `EN-M01 镜痕巡猎`: Movement path marks intersected legal Enemies.
- New `EN-A05 妒焰渐炽`: continuous laser damage rises with uninterrupted duration, capped at 50 damage per second.

## Wrath

- Baselines: Movement `Reload 2 / hp cost 10 / damage 20`; Attack `Reload 2 / hp cost 5 / Burning 5 damage per second for 3s`; Special `Reload 1 / hp cost 30 / 5 damage every 0.5s for 2s`.
- `WR-B01` means Attack CD reduction, not animation-speed increase.
- New `WR-S04` duplicates the confirmed table's “tornado duration +2s” effect; it coexists with `WR-S03` by Owner confirmation.

## Greed

- Basic Black Oil grants speed to the player / possessed Greed only. It does not baseline-slow Enemies.
- `GR-M01` extends Black Oil length and width and causes Enemy slow.
- Basic Hands: generate one hand each second, orbit the owner, max 6; LMB releases all hands sequentially at 0.2s intervals, spends 20 HP once per release, and hands without legal targets disappear.
- Basic Guard absorbs all damage, lasts 1s, prevents movement, converts each 100 absorbed damage to one hand, and still costs / enters cooldown on empty use.
- `GR-S04`: Guard lasts 3s; pressing Special again ends it early and immediately starts Reload.
- `GR-S01`: one hand per 60 absorbed damage; continuous lasers use their actually absorbed cumulative damage.
- New `GR-M02 圣路恩赐`: standing on own ordinary Black Oil prevents terrain damage.
- New `GR-A07 迂回纳贡`: released hands take a fixed side arc before converging on their legal target.

## Lust

- Movement uses mouse direction, leaves Anchor at start, and may swap with it on later use.
- Attack outbound and return hits have no additional double-hit reward.
- The following table lines map to existing Cards rather than new Cards: `LU-M03`, `LU-M05`, `LU-A03`, `LU-S06`, `LU-A04`.
- Pulling linked targets toward Anchor remains baseline Special, not a new Card.
