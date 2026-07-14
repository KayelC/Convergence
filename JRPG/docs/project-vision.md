# Project Vision: Convergence

> **Status: Strategic direction.** This document records long-term goals rather than an implementation or schema contract.

## Purpose

Convergence began as a hobby Persona-like JRPG prototype and evolved into a broader C# framework for complex turn-based RPG systems. The primary host target is Godot, while the framework itself remains engine-neutral so other compatible .NET hosts can consume the same rules and state contracts.

This document records the intent behind the project so future refactors, features, and documentation stay aligned with the framework direction.

## North Star

Convergence should become a polished, open-source RPG systems framework that captures the strategic depth of several beloved turn-based RPG traditions while allowing developers to build original games on top of it.

The framework should eventually provide:

- mainline SMT-style Press Turn combat
- Persona-style One More combat as an optional ruleset
- Persona Users and Wild Cards as playable class models
- Digital Devil Saga-style Avatars and progression as a future class/progression model
- reusable demon/persona/entity progression, affinity, skill, fusion, negotiation, party, inventory, dungeon, and economy systems
- clean integration seams for game engines and custom frontends
- example datasets that can serve as testbeds without being required by downstream games

## Strategic Direction

The current console project should be treated as a prototype host and reference implementation. Over time, framework logic should move away from interactive console assumptions and toward reusable state machines, services, and data contracts.

The ideal shape is:

1. A core framework/library containing deterministic gameplay systems.
2. A console sample app for testing and demonstration.
3. A Godot adapter that translates engine input, UI, scenes, animation, and persistence into framework commands and state updates, with room for other .NET host adapters later.
4. Example content packs that demonstrate behavior without locking the framework to any protected IP.

## Release Philosophy

The first serious release should prioritize the SMT-inspired feature set already closest to maturity:

- Press Turn combat
- affinities and elemental interactions
- status ailments and buffs/debuffs
- demons/entities
- fusion and compendium foundations
- negotiation/recruitment foundations
- party and stock management
- data-driven skill/entity/item/dungeon content

Persona-style One More rules, deeper Persona class behavior, Wild Card expansion, DDS Avatar progression, and other franchise-inspired systems should follow after the core framework is cleaner, tested, and modular.

## IP And Branding Direction

The project currently uses familiar franchise-inspired terminology and datasets as a development testbed. Before a public-facing release intended for broad reuse or personal IP development, protected branding and direct IP references should be removed or isolated.

Preferred direction:

- keep the framework mechanics generic and original in naming
- move franchise-inspired datasets into optional examples or private test fixtures where appropriate
- avoid requiring any protected content for framework operation
- make it easy for developers to provide their own original content

## Open Source Motivation

The project is open source partly as a portfolio piece and proof of serious systems engineering ability. It should demonstrate:

- strong C# design
- game systems architecture
- data-driven modeling
- testable state machine thinking
- technical documentation
- long-term product judgment

Because the author may build a personal commercial IP later, Convergence should separate framework mechanics from original story, setting, characters, art, and proprietary content.

## Architectural Implications

Future technical decisions should be judged against the framework goal.

High-value refactor principles:

- engine-agnostic core logic first
- no direct console dependency in framework systems
- deterministic state transitions where possible
- explicit commands and results instead of hidden side effects
- injectable randomness for tests and replayability
- validated content contracts
- stable public APIs for downstream games
- optional rulesets rather than hard-coded franchise assumptions

The current conductor/bridge/messenger model is a useful transitional architecture. The next maturation step is to move toward explicit game states, commands, transitions, and adapters.

## Intended Framework Shape

Convergence should eventually expose systems that a host game can compose:

- battle state machine
- field/exploration state machine
- fusion service
- recruitment/negotiation service
- party and stock service
- entity progression service
- content database/repository interfaces
- ruleset configuration for Press Turn, One More, and future variants
- event/result streams for UI, animation, logging, and engine integration

The host engine should own visuals, input, audio, scene flow, persistence UI, and presentation timing. Convergence should own rules, state, validation, and deterministic gameplay outcomes.

## Success Criteria

Convergence is on track when:

- core gameplay systems can run without console input
- tests cover rule-heavy mechanics
- Unity/Godot integration would not require rewriting battle/fusion/party logic
- content can be swapped without changing code
- franchise-inspired rules can be enabled as generic, configurable systems
- developers can build original games on top of the framework without understanding every internal edge case

## Guiding Sentence

Convergence is not just a JRPG prototype. It is a path toward a reusable C# framework for expressive, data-driven, turn-based RPG systems.
