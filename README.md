# LuminaryHelper

An ExileApi plugin for minion builds.

- Draws a circle under each of your minions
- Shows at a glance which ones have lost your Flame Link
- Puts the link back on for you
- Calls minions back when they get low or wander off

Supports Spectres, Mercenaries and the Animate Guardian.

## Install

Copy the `LuminaryHelper` folder into `Plugins/Source` and start the HUD. It
compiles on first launch.

Built against ExileApi-Compiled 329.12.

## Getting started

Circles and link tracking work as soon as you enable the plugin.

Nothing presses a key on its own until you say so. Auto cast and both recall
options are off, and switching them on does nothing until you also tell them
which key the skill sits on in game.

## Circles

Every minion type has its own colour and size. A minion that has lost your link
is drawn in the unlinked colour with a second ring inside it, which is easier to
catch mid fight than a colour change alone.

Circles hide in town and while a panel is open. Both are under Display if you
want them back.

## Flame Link

Set **Flame Link key** to the key the skill is on, tick **Cast Flame Link for
me**, and it re-links minions that have dropped it.

It only acts while you hold a key to begin with, so you can watch it before
letting it loose. Turn off *Only while I hold a key* once you are happy.

Flame Link needs a target, so a cast runs over a few frames: the cursor moves
onto the minion, waits for the game to pick up the new hover, presses the key,
then goes back where it was. If it ever fires at the wrong thing, raise **Cursor
settle time**.

Casts are paced to the skill's real cast time, so it keeps up as your cast speed
changes.

### Picking targets

Out of the box every minion gets linked. To narrow it down, turn on **Only link
minions I picked** and tick the ones you want in the **Link targets** list at the
top of the settings.

Minions are remembered by name rather than by id, since ids change every time you
resummon. Two spectres with the same name share one tick box.

## Recall

Two skills, each with its own key, triggers and cooldown:

| | Skill | Watches |
|---|---|---|
| Recall: Mercenary | Order: To me! | Mercenaries |
| Recall: everything else | Convocation | Spectres, Animate Guardian |

Either can fire when a minion drops below a life threshold, when one strays past
a distance, or both. Pick neither and it stays quiet.

Timing comes from the skill's own cooldown. The game reports that with your
cooldown recovery already applied, so Convocation reads as 3s on a build with
recovery instead of its 5s base and nothing needs adjusting by hand.

## When it will not act

Nothing is sent while the game window is unfocused, the escape menu is up, you
are dead, grace period is running, you are in town, a text box has focus, chat is
open, a panel is open, or the skill is still on cooldown.

The settings header shows what is blocking right now, the gap it is using, and
how many presses it has sent. If something is not firing, that line tells you
why.

## Discovery window

Lists every entity the detector looked at with its metadata, the skill that
summoned it and its buffs, with buffs from you marked by a star. Below that is
every skill you have with its internal name, cooldown and cast time.

Use it if a minion is not being picked up, or to look up an internal name for one
of the skill name boxes.

## How minions are found

Only things you summoned are considered, read from your own deployed objects.
That is also where the summoning skill comes from, which matters for Spectres: a
Spectre reports the metadata of whatever monster it was raised from, so the skill
is the only thing that identifies one.

Matching rules live in the settings rather than in code, so a wrong one is a text
box away instead of a rebuild. The defaults:

| Type | Summoned by | Metadata |
|---|---|---|
| Spectre | `raise_spectre` | inherited from the raised monster |
| Mercenary | `melee` | `Metadata/Monsters/Mercenaries/` |
| Animate Guardian | `animate_guardian` | `Metadata/Monsters/AnimatedItem/AnimatedArmour` |

There is a **Scan allied monsters too** switch under Advanced. Leave it alone. It
looks at every friendly monster around and cannot tell yours from anyone else's,
so it will cheerfully circle reserve mercenaries standing in town.

## Building outside the HUD

```
dotnet build -p:exapiPackage="C:\path\to\ExileApi-Compiled-329.12"
```
