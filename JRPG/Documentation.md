This document provides a comprehensive analysis of the **JRPG Prototype (SMT/Persona Framework)**, a specialized C# framework designed to unify the distinct playstyles of the *Shin Megami Tensei*, *Persona*, and *Digital Devil Saga* series into a single cohesive engine.

---

# **JRPG Prototype: SMT/Persona Framework Architectural Specification**

## **1. Framework Overview**
The JRPG Prototype is a modular framework built in .NET 9.0 that implements the high-stakes, resource-driven combat and entity-management systems characteristic of Atlus’s Megami Tensei franchise. 

### **Core Pillars**
*   **Press Turn System:** A high-fidelity implementation of the *SMT III: Nocturne* turn-icon economy.
*   **Hybrid Entity Model:** A unified `Combatant` shell that can shift behavior based on `ClassType` (Human, Demon, Persona User, Wild Card, Operator).
*   **Cathedral of Shadows:** A deterministic Fusion system supporting Binary, Sacrificial, and Mitama fusions.
*   **Decoupled Architecture:** Strict separation of logic (Engines), orchestration (Conductors), and presentation (Bridges/Messengers).

---

## **2. Architectural Philosophy: The CEBM Pattern**
The framework adheres to the **Conductor / Engine / Bridge / Messenger (CEBM)** pattern, ensuring that core RPG logic remains platform-agnostic and highly maintainable.

### **The Layers**
1.  **Conductors (`BattleConductor`, `FusionConductor`, `FieldConductor`):** These are the "Brain" modules. They manage the high-level state machine (e.g., Turn 1 -> Player Phase -> Enemy Phase -> Victory). They do not perform math; they delegate to Engines.
2.  **Engines (`PressTurnEngine`, `FusionCalculator`, `BehaviorEngine`):** These are "Pure Logic" units. They handle the "What" and "How much" (e.g., "How many icons does a Nullified attack consume?").
3.  **Bridges (`InteractionBridge`, `CathedralUIBridge`):** These encapsulate all I/O. They translate complex engine states into UI options and vice-versa, allowing the game to run in a Console today and a GUI tomorrow without changing the Conductor.
4.  **Messengers (`IBattleMessenger`, `IFusionMessenger`):** A Mediator pattern implementation. Logic engines publish events (e.g., "Jack Frost used Bufu") to a messenger, which observers (like a Logger) then display.

---

## **3. Mechanics & Feature Analysis**

### **3.1. Press Turn Battle System**
The system is modeled after the **Nocturne** implementation, tracking "Solid" and "Blinking" icons.
*   **Action Consumption:**
    *   **Weakness/Critical:** Consumes 1 icon but converts it to Blinking (chaining actions).
    *   **Normal/Miss/Null:** Standard consumption or multi-icon penalties (2 icons for a Miss).
    *   **Repel/Absorb:** Immediate phase termination.
*   **Strategic Pass:** Passing converts a Solid icon to Blinking, moving the turn to the next actor without losing the resource entirely.

### **3.2. Class Archetypes & Entity Design**
Entities are differentiated by their `ClassType`, which dictates their available commands and progression:
*   **Human:** Relies on Equipment. Element is dictated by Weapon (Slash/Strike/Pierce).
*   **Persona User:** Linked to a single `Persona` container. Affinities and skills are inherited from the mask.
*   **Wild Card:** Can carry multiple Personas and **Switch** them mid-battle as a free action (P3R/P5 Style).
*   **Operator:** The classic Summoner. Uses a COMP to manage a `DemonStock`, allowing for Summon/Return/Swap actions mid-battle.
*   **Demon:** Standard combatants with innate affinities and skills.

### **3.3. Fusion & The Cathedral of Shadows**
The Fusion system is a robust mutation engine supporting:
*   **Binary Fusion:** Creating new demons based on a 2D lookup table.
*   **Sacrificial Fusion:** Transferring Experience and exclusive skills (Full Moon only).
*   **Mitama/Rank Fusion:** Boosting stats or evolving/devolving demons within their race.
*   **Skill Inheritance:** Deterministic slot calculation based on parent skill counts.
*   **Fusion Accidents:** Random deviations in results, influenced by the Moon Phase. Accidents can shift the resulting demon to a lower tier or mutate inherited skills into different ranks.

### **3.4. Negotiation System (Inquiry & Demand)**
The framework implements the classic SMT recruitment mini-game, where players interact with enemies to convince them to join the party, provide items, or give Macca.
*   **Mood Score Logic:** Negotiations consist of up to 3 rounds of questions. Each response shifts a hidden "Mood Score." A score of 4 or higher triggers the "Demands" phase.
*   **Personality Mapping:** Demon personalities (Friendly, Gloomy, Arrogant, etc.) are mapped to their `Race`. This dictates which question sets and response weights are used.
*   **The Demands Phase:** Demands include Level checks (demons won't join if the player's level is too low), Macca requests (calculated as `TargetLevel^2`), and item requests.
*   **Full Moon Blockade:** During a Full Moon (Phase 8), demons are "frenzied" and typically refuse negotiation, leading to immediate combat or erratic behavior.

### **3.5. Moon Phase System**
A central time-tracking mechanism that influences multiple game systems.
*   **The 0-8 Cycle:** The moon progresses through 9 distinct phases (New Moon [0] -> Waxing -> Half -> Full Moon [8] -> Waning).
*   **Lunar Influence:**
    *   **Fusion Accidents:** The base accident rate is 1%. During a Full Moon, this spikes to 12%.
    *   **Sacrificial Fusion:** Certain high-level rituals are only available during the Full Moon.
    *   **Field/Dungeon:** Future implementations include "Moon-sensitive" doors and enemy stat scaling.

### **3.6. Status Ailment System**
Ailments are categorized into **Physical** and **Mental** types, each with unique removal triggers and combat effects.
*   **Physical Ailments (Freeze, Shock):** Often result in "Rigid Body" status, which negates physical resistances and guarantees critical hits for the attacker.
*   **Mental Ailments (Fear, Panic, Charm):** Affect action selection. Fear may cause a combatant to skip turns or flee; Panic may cause them to use random items or skills.
*   **Removal Triggers:**
    *   `NaturalRoll`: A luck-based check at the start of the turn.
    *   `OneTurn`: Automatically clears after one active turn.
    *   `OnHit`: Clears when the combatant takes damage.

### **3.7. Dungeon & Exploration (The Tartarus Loop)**
The exploration logic is built for verticality and persistence, modeled after *Persona 3's* Tartarus.
*   **Block & Floor Structure:** Dungeons are divided into "Blocks" (e.g., Thebel, Arqa) with specific level ranges and enemy pools.
*   **Fixed Floors:** Pre-defined floors containing Bosses (Gatekeepers), SafeRooms (with Terminals for warping/saving), and BlockEnds.
*   **Encounter Generation:** The `ExplorationProcessor` generates mixed enemy groups (1 to 3 enemies).
*   **Grouping Logic:** To maintain SMT fidelity, identical enemies are grouped and labeled alphabetically (e.g., "Pixie A", "Pixie B").

### **3.8. Game Loop, Economy & Inventory**
The framework manages player resources through a centralized economy and a structured inventory system.
*   **The Macca Economy:** Currency (Macca) is tracked by the `EconomyManager`. 
    *   **Buy/Sell Scaling:** Transaction prices are heavily influenced by the `Luck` (Lu) stat.
        *   `Buy Price = BasePrice * Max(0.5, 1.0 - (Luck * 0.01))` (Up to 50% discount).
        *   `Sell Price = BasePrice * (0.50 + (Luck * 0.01))` (Up to 100% value at 50 Luck).
*   **Inventory Structure:** The `InventoryManager` separates items into a quantitative dictionary and equipment into categorized lists (`OwnedWeapons`, `OwnedArmor`, etc.). 
*   **Shop Engine:** Offers are populated via `shop_inventory.json`, supporting categorized tabs (Items, Weapons, Armor, Boots, Accessories).

### **3.9. Enemy AI & Tactical Logic (Unified Tactical Model)**
Enemy behavior is governed by the `BehaviorEngine`, which simulates high-level player strategy through a **Tiered Priority Ladder**.
*   **The Priority Ladder:**
    1.  **Kill-Shot:** If a valid attack can reduce an opponent to 0 HP.
    2.  **Weakness/Rigid Exploitation:** Hunting for known elemental weaknesses or "Rigid" targets (Frozen/Shocked) to gain icons.
    3.  **Crisis Recovery:** Healing allies below 35% HP.
    4.  **Critical Fishing:** Using physical attacks on Rigid targets for 100% critical rate.
    5.  **Informed Pass:** Strategically passing a turn if a powerful ally behind can exploit a known weakness.
    6.  **Standard Pressure:** Using the highest-power offensive skill.
*   **Ailment Hijack:** AI logic is bypassed if the actor is under certain mental ailments:
    *   **Rage:** Forced to use basic physical attacks.
    *   **Confusion/Charm:** May heal opponents or attack allies randomly.
*   **Risk Aversion:** The AI cross-references `BattleKnowledge` to avoid using skills against targets known to Null, Repel, or Absorb that element.

### **3.10. The Lunar Cycle (Moon Phase System)**
The `MoonPhaseSystem` is a 9-step progression cycle [0-8] that serves as a global state modifier.
*   **Phase Progression:** The moon advances one step (`Advance()`) upon floor transitions in a dungeon or during specific field events (resting/healing).
*   **Phases:** New Moon [0] -> Waxing [1-3] -> Half Moon [4] -> Waxing [5-7] -> Full Moon [8].
*   **Systemic Impacts:**
    *   **Negotiation:** Blocked during Full Moon (Phase 8); demons are "frenzied."
    *   **Fusion:** Accident rates spike from 1% to 12% during Full Moon.
    *   **Sacrificial Fusion:** Unique rituals are unlocked during the Full Moon phase.

### **3.11. Detailed Ailment Taxonomy**
The framework supports 11 distinct status ailments, each with specific combat modifiers and removal triggers.

| Ailment | Action Restriction | Combat Effect | Removal Trigger |
| :--- | :--- | :--- | :--- |
| **Poison** | None | 13% Max HP damage/turn | NaturalRoll (Luck) |
| **Freeze** | SkipTurn | 0 Evasion, 100% Crit Taken | OneTurn / OnHit |
| **Shock** | SkipTurn | 0 Evasion, 100% Crit Taken | OneTurn / OnHit |
| **Sleep** | SkipTurn | 0 Evasion, 50% Crit Taken, HP/SP Regen | NaturalRoll / OnHit |
| **Charm** | ConfusedAction | May attack allies / heal enemies | NaturalRoll |
| **Rage** | ForceAttack | 1.5x Dmg Dealt, 3x Dmg Taken | NaturalRoll |
| **Fear** | ChanceSkip/Flee | 15% flee chance, 40% skip turn | NaturalRoll |
| **Panic** | ChanceSkip | 50% skip turn, cannot use skills | NaturalRoll |
| **Distress** | None | 0 Evasion, 1.5x Dmg Taken | NaturalRoll |
| **Bind** | LimitedAction | Cannot use Skills or Items | NaturalRoll |
| **Stun** | SkipTurn | Forced turn skip | OneTurn |

### **3.12. Field Services & Restoration**
Outside of battle, the player interacts with the `FieldServiceEngine` to manage party health and logistics.
*   **Hospital (Restoration):**
    *   **Cost Formula:** 1 Macca per 1 HP missing + 5 Macca per 1 SP missing.
    *   **Full Restore:** Clears all HP/SP damage, persistent field ailments, and encounter-leftover buffs.
*   **Terminal System:** Unlocks warp points on fixed floors (e.g., Floor 10, 20) allowing for persistent shortcuts between the Lobby and the depths.
*   **Metadata Validation:** To prevent JSON-related crashes, the engine performs a "Repair" on unhydrated items/equipment, ensuring names and IDs are valid before possession.

---

## **4. Technical Implementation (The Code Aspect)**

### **4.1. Entity Composition (`Entities/`)**
*   **`Combatant.cs`:** The root container. It uses **Proxy/Facade** patterns, delegating complex math to `StatProcessor` (stat calculation with modifiers) and `DamageHandler` (affinity-based damage processing).
*   **`Persona.cs`:** A lightweight data container for skills and affinities. It allows for "hot-swapping" stats and resistances on a `Combatant`.
*   **`CombatantFactory.cs`:** A specialized factory that hydrates combatants from JSON. It handles level-scaling for enemies and ensures that player demons inherit the correct state from their fusion parents.

### **4.2. Combat Orchestration (`Logic/Battle/`)**
*   **`BattleConductor.cs`:** Manages the `while(!BattleEnded)` loop. It uses the `IBattleMessenger` to broadcast "Narration" without knowing *how* that narration is displayed.
*   **`ActionProcessor.cs`:** The authoritative coordinator of battle actions. 
    *   **Effectiveness Gate:** Before executing, it verifies if an action is redundant (e.g., curing a healthy ally) to prevent turn wastage.
    *   **Strategy Delegation:** Instead of large `switch` blocks, it uses the `BattleEffectRegistry` to fetch an `IBattleEffect` strategy based on the skill's category.
    *   **Persona Swapping:** Orchestrates the mid-battle swap for Wild Cards, ensuring resource pools are recalculated based on new stat weights without losing current HP/SP values.
*   **`PressTurnEngine.cs`:** A state machine that tracks `_fullIcons` and `_blinkingIcons`. It contains the "Laws of SMT" (e.g., `TerminatePhase()` on Repel).

### **4.3. UI Bridging & Interaction Design**
The `InteractionBridge` handles the high-level UI flow, ensuring that game logic remains decoupled from terminal rendering.
*   **Menu Affordances:** Menus support "Disabled" states (grayed out) when a player lacks resources or is restricted by an ailment (e.g., Panic blocking Skills).
*   **Integrated Persona Menu:** For Wild Cards, Skills and the "Change Persona" action are unified into a single list to streamline combat flow.
*   **COMP Interaction:** A hierarchical menu system for Operators to Summon, Return, or Swap demons mid-battle.
*   **HUD Rendering:** The `GetBattleContext` method produces a unified string containing the Press Turn icons, Enemy groups, and Party status (including Buff/Debuff track icons).

### **4.4. Fusion Logic (`Logic/Fusion/`)**
*   **`FusionCalculator.cs`:** Uses `Database.jsons` to predict outcomes. It calculates accidents, skill mutations, and inheritance slots.
*   **`FusionMutator.cs`:** The "Transaction Manager." It performs the actual modification of the player's stock, ensuring macca costs are deducted and entities are removed/added atomically.
*   **`Strategies/`:** Implements the **Strategy Pattern** for different fusion types (`RankMutationStrategy`, `StatBoostStrategy`), keeping the `FusionConductor` clean of specific ritual logic.
*   **Inheritance Scaling:** The number of inherited skills is deterministic, scaling based on the total unique skills in the parent pool:
    *   1-6 unique skills -> 1 slot
    *   7-9 unique skills -> 2 slots
    *   10-13 unique skills -> 3 slots
    *   14-18 unique skills -> 4 slots
    *   19-23 unique skills -> 5 slots
    *   24+ unique skills -> 6 slots

### **4.5. The Mathematical Kernel (`CombatMath.cs`)**
All game math is centralized in a pure, stateless kernel to ensure consistency across the framework.
*   **SMT III Damage Formula:** $5.0 \times \sqrt{\text{SkillPower} \times (\text{AtkStat} / \text{DefStat})}$
*   **Accuracy Formula:** $\text{SkillBaseAcc} + (\text{AgilityDelta} \times 2) + \text{LuckDelta}$
*   **Critical Chance (Physical Only):** $((\text{AttackerLuck} - \text{TargetLuck}) / 2) + 5$, modified by passives like *Apt Pupil*.
*   **EXP Yield (Cubic):** $1.5 \times \text{Level}^3 / 50.0$, adjusted by a "Stat Density Bonus" for stronger/boss enemies.
*   **Macca Yield (Quadratic):** $0.25 \times \text{Level}^2 + (\text{Luck} \times 5)$, plus a 10% variance.
*   **CombatResult DTO:** Communication between engines and conductors is handled via the `CombatResult` object, which encapsulates `DamageDealt`, `HitType` (Normal, Weak, Crit, Miss, etc.), and `IsCritical` flags.

### **4.6. Progression & Stat Engineering**
The framework implements a tiered stat influence model to distinguish between Humanoids and Demons.
*   **`GrowthProcessor.cs` (The Progression Engine):**
    *   **Cubic EXP Curve:** $1.5 \times \text{Level}^3$.
    *   **Randomized Growth:** Level ups grant randomized base HP/SP increases and 1 manual stat point.
    *   **Resource Recalculation:** `MaxHP` and `MaxSP` are dynamically synchronized with `Vitality` (Vi) and `Magic` (Ma) stats, capped at the SMT III limits (666 HP / 333 SP).
*   **`StatProcessor.cs` (The Math Engine):**
    *   **Weighted Influence:** Persona stats do not replace base stats; they influence them by weight:
        *   **St/Ma:** 40% Influence.
        *   **Vi/Ag:** 25% Influence.
        *   **Lu:** 50% Influence.
    *   **Global Hard Cap:** Base stats (before buffs) are capped at 40 to maintain game balance.
    *   **Battle Buffs (Kaja/Nda):** Buffs apply a 1.4x multiplier; Debuffs apply a 0.6x multiplier to the capped value.

### **4.7. Battle Effects Strategy Pattern (`Logic/Battle/Effects/`)**
The `IBattleEffect` interface decouples action logic from the `ActionProcessor`.
*   **`BuffEffect`:** Manages stat modifications via the `StatusRegistry`, ensuring stacks are capped at ±4.
*   **`DamageEffect`:** The primary executioner for offensive actions, handling affinity lookups, critical hits, and damage calculation.
*   **`AilmentEffect`:** Handles the chance-based infliction of status effects, cross-referencing target resistances.
*   **`Cure/RecoveryEffect`:** Manages healing and ailment removal, including specialized logic for reviving fallen allies.
*   **`ShieldEffect` (Karns/Walls):** Implements temporary affinity overrides, allowing for reflection or nullification of specific elements.

### **4.8. Buff/Debuff Management (The 4-Track System)**
The `StatusRegistry` manages stat modifications through four independent tracks, each with a stack depth of [-4, +4].
*   **Stat Tracks:**
    *   **PhysAtk (Tarukaja):** Multiplier for physical damage.
    *   **MagAtk (Makakaja):** Multiplier for magical damage.
    *   **Defense (Rakukaja):** Multiplier for damage reduction (Vitality).
    *   **Agility (Sukukaja):** Multiplier for hit and evasion rates.
*   **Stacking Rules:** Each Kaja/Nda application shifts the track by 1. Stacks are capped strictly to ensure combat remains within predictable bounds.
*   **Omni-Modifiers:** High-level skills like *Heat Riser* (Buff) and *Debilitate* (Debuff) apply their effects to all four tracks simultaneously.
*   **Auto-Kaja Passives:** Combatants with Auto-Skills automatically trigger these buffs at the start of battle, supporting both single-target and party-wide (Maha) variants.

### **4.9. Core Helpers & Utilities (`Core/`)**
*   **`ElementHelper.cs`:** Centralized logic for element parsing and category mapping. It handles non-standard mappings like "Electric -> Elec" and "Block -> Null" during data hydration.
*   **`Enums.cs`:** Defines the system's core vocabulary, including `Element`, `Affinity`, `ClassType`, and `StatType`.

---

## **5. Data Layer & Hydration**
*   **`Database.cs`:** A static registry that loads all game data from `Data/Jsons/` using `Newtonsoft.Json`. 
*   **Normalization:** All IDs and keys are normalized to **lowercase** during hydration (`p.Id.ToLower()`), preventing the classic "Case-Mismatch" bug during lookups.
*   **Categorized Hydration:** The database uses specialized loaders for Equipment (`weapons.json`, `armor.json`, etc.) and a generic loader for Items and Skills, mapping them into strongly-typed Dictionaries.
*   **Schema:** The framework uses POCOs (Plain Old C# Objects) like `SkillData.cs` and `PersonaData.cs` to mirror the JSON structure.
*   **Dynamic Repair:** The `FieldServiceEngine` performs on-the-fly metadata repair for unhydrated items, ensuring consistency between the raw JSON and the player's inventory.

---

## **6. Current Achievements & Functional Foundation**
As of the current build, the framework successfully achieves:
1.  **Complete Battle Lifecycle:** Encounter -> Initiative -> Press Turn Phases -> Rewards -> Cleanup.
2.  **Robust Status/Ailment Registry:** Handles turn-start restrictions (Freeze/Panic) and turn-end decay (Buffs/Poison).
3.  **Advanced Party Management:** Seamless swapping between active party and stocks (COMP for Operators, Persona Stock for Wild Cards).
4.  **Deterministic Fusion:** A fully functional Cathedral of Shadows with complex inheritance and Moon Phase sensitivity.
5.  **Clean I/O Abstraction:** The entire game runs through `IGameIO`, making it ready for a move to Unity or Godot with minimal logic changes.

---

## **7. Future Expansion (DDS/Atma Concepts)**
The architecture is prepared for the **Atma Avatar (Digital Devil Saga)** playstyle:
*   **`Evolutionary Line Chart`:** Can be implemented as a specialized `GrowthProcessor`.
*   **`Hunt Skills`:** The `ActionProcessor` can be extended to handle "Eat" mechanics, triggering unique progression flags on the `Combatant` shell.
*   **`Transformation`:** The `ClassType.Avatar` logic is already stubbed, allowing for a "Human vs. Demon" form-swapping state machine mid-battle.