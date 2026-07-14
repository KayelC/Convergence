# Problem: Battle And Skill Independence

## Current State

The framework has clean definitions and runtime services for:

- skills;
- effects;
- passives;
- item execution;
- targeting;
- battle actions;
- Press Turn;
- affinity and resistance resolution;
- status lifecycle;
- battle orchestration.

The console battle still has legacy paths for many ordinary actions, especially old `SkillData`, `ItemData`, string-driven effects, and live `Combatant` mutation.

## Problem

The framework cannot be battle-independent until a real battle loop uses clean skill/item definitions for all actions in that loop.

The current clean demos prove enough for technical confidence, but not enough gameplay breadth.

## Needed Data

Generic battle examples:

- physical damage skill;
- fire/ice/electric/wind skill;
- light/dark/almighty examples if approved for the sample;
- healing skill;
- cure skill;
- ailment application skill;
- buff skill;
- debuff skill;
- passive damage modifier;
- passive turn-end recovery;
- passive one-time survival.

Generic defenses:

- one weak affinity;
- one resist affinity;
- one ailment vulnerability/resistance;
- one instant-death channel example if needed.

## Decisions Still Needed

- Which ailment should be the first clean sample ailment?
- Should the sample pack include instant death, or leave that to unit tests only?
- Should basic attacks be equipment-driven in the first interactive clean loop?
- Which battle outcomes must be shown in sample content versus only unit tests?

## Recommended Next Step

Add one ailment and one buff/debuff to the clean sample content, then prove them through the clean runtime path.

Avoid porting old skill names, spell families, or legacy effect text.
