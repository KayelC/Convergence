# Battle Knowledge

Status and passive lifecycle rules now have a dedicated reviewed surface:
[Status And Passive Lifecycle](status-passive-lifecycle.md). This page retains
the separate battle-knowledge scope for Documentation Order 5.

## Knowledge Domains

Knowledge is separated into elemental affinity, ailment resistance, and
instant-defeat resistance stores. The keys include the known entity ID and the
relevant element, ailment, or channel, so the three domains cannot collide.

Almighty discoveries are ignored because Almighty always resolves normally.

**Framework capability:** knowledge snapshots can be persisted or scoped to an
encounter. The intended player model demonstrated by Training Annex is:

- enemy AI starts ordinary encounters with fresh encounter knowledge and learns
  only during that encounter;
- player knowledge persists between encounters when the host includes it in
  saves;
- analysis and observed outcomes can reveal typed defenses;
- familiar entities registered in the Compendium can import their authored
  defenses into player knowledge; and
- AI knowledge and player knowledge are separate stores.

Bosses or special encounters may receive preloaded knowledge because the host
owns encounter composition.

## Presentation

A Godot host can use player knowledge to annotate target cursors, element icons,
analysis panels, or bestiary pages. The host reads typed knowledge entries. It
does not infer a weakness from damage text or an animation.

Battle knowledge has not yet completed the collaborative source and owner
review. This page records the current direction without promoting Order 5.
