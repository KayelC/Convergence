# FusionConductor

Source: [`Logic/Fusion/FusionConductor.cs`](../../../Logic/Fusion/FusionConductor.cs)

## Purpose

`FusionConductor` is the root workflow orchestrator for the Cathedral of
Shadows. It does not own the fusion math, preview math, or final state mutation.
Its job is to route the player through the Cathedral flow and call the correct
collaborators at each step.

## Runtime Role

The conductor is entered from field services when the player chooses the
Cathedral. It owns the outer Cathedral menu loop and coordinates:

- binary fusion,
- sacrificial fusion,
- Compendium recall,
- Compendium registration.

After the second Fusion cleanup pass, the conductor is intentionally thinner.
Planning, preview construction, duplicate-result rules, and transaction details
are delegated to Fusion-owned helper classes.

## Key Collaborators

- `CathedralUIBridge`: all console-facing Cathedral menus and confirmations.
- `FusionCalculator`: predicts operation type and accident behavior.
- `FusionPlanFactory`: builds a complete non-mutating ritual plan.
- `FusionPreviewFactory`: builds staged preview combatants.
- `FusionOwnershipRules`: provides duplicate-result disabled reasons.
- `FusionMutator`: commits confirmed transactions.
- `CompendiumRegistry`: stores and retrieves registered demon snapshots.
- `FusionMessenger` and `FusionLogger`: publish and render Fusion messages.
- `PartyManager`: provides active party and ownership checks.
- `EconomyManager`: used by recall transactions through the mutator.

## Important Methods / Members

Current class shape:

```csharp
public class FusionConductor
{
    public void EnterCathedral()

    private void PerformFusionRitual(bool isSacrificial)
    private Dictionary<object, string> BuildOwnedDuplicateResultReasons(...)
    private void HandleCompendiumRecall()
    private void HandleRegistration()
}
```

Important collaborators are stored as fields:

```csharp
private readonly FusionCalculator _calculator;
private readonly FusionMutator _mutator;
private readonly CompendiumRegistry _compendium;
private readonly CathedralUIBridge _uiBridge;
private readonly FusionPlanFactory _planFactory;
private readonly FusionPreviewFactory _previewFactory;
private readonly FusionOwnershipRules _ownershipRules;
```

### Constructor

The constructor wires the current console-hosted Fusion stack:

- stores player, party, economy, UI state, and Compendium references,
- creates a `FusionMessenger`,
- subscribes a console `FusionLogger`,
- creates the calculator, mutator, bridge, plan factory, preview factory, and
  ownership rules.

This is still host-shaped wiring. Later framework extraction should inject more
of these collaborators rather than constructing them directly.

Key implementation:

```csharp
_messenger = new FusionMessenger();
_logger = new FusionLogger(_io);
_logger.Subscribe(_messenger);

_calculator = new FusionCalculator(_io, _messenger);
_mutator = new FusionMutator(_partyManager, _economy, _messenger);
_uiBridge = new CathedralUIBridge(_io, _uiState, _compendium);
_planFactory = new FusionPlanFactory(_calculator);
_previewFactory = new FusionPreviewFactory();
_ownershipRules = new FusionOwnershipRules(_partyManager);
```

This shows the current boundary problem clearly: the conductor still constructs
both framework-like collaborators and console-host collaborators.

### `EnterCathedral`

Runs the primary Cathedral loop. It asks the bridge for a typed
`FusionMainMenuResult`, exits on `Back`, and dispatches actions to:

- `PerformFusionRitual(false)` for binary fusion,
- `PerformFusionRitual(true)` for sacrificial fusion,
- `HandleCompendiumRecall`,
- `HandleRegistration`.

The typed result means the conductor no longer dispatches by raw menu string.

Key implementation:

```csharp
while (true)
{
    FusionMainMenuResult choice = _uiBridge.ShowCathedralMainMenu(MoonPhaseSystem.CurrentPhase);

    if (choice.Kind == FusionMenuResultKind.Back) return;

    switch (choice.Action)
    {
        case FusionMainMenuAction.BinaryFusion: PerformFusionRitual(isSacrificial: false); break;
        case FusionMainMenuAction.SacrificialFusion: PerformFusionRitual(isSacrificial: true); break;
        case FusionMainMenuAction.BrowseCompendium: HandleCompendiumRecall(); break;
        case FusionMainMenuAction.RegisterDemon: HandleRegistration(); break;
    }
}
```

The bridge owns menu rendering. The conductor only receives intent.

### `PerformFusionRitual`

Coordinates a single binary or sacrificial fusion attempt.

The method is structured as nested loops:

- participant-selection loop,
- skill-selection and preview loop.

This is deliberate. Canceling at different stages should return to different
parts of the ritual without mutating state.

High-level sequence:

1. Build the participant pool from the player class.
2. Select first parent.
3. Build duplicate-result disabled reasons for second parent choices.
4. Select second parent.
5. Select sacrifice if sacrificial fusion is active.
6. Convert raw objects into `FusionParticipant` wrappers.
7. Ask `FusionPlanFactory` for a plan.
8. Ask bridge for inherited skills.
9. Ask `FusionPreviewFactory` for a staged preview.
10. Ask bridge for ritual confirmation.
11. Apply accident skill replacement if needed.
12. Display ritual sequence.
13. Create `FusionContext`.
14. Dispatch to `FusionMutator`.

Participant pool code:

```csharp
if (_player.Class == ClassType.Operator)
{
    var demons = _partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
    demons.AddRange(_player.DemonStock);
    participantPool = demons.Distinct().Cast<object>().ToList();
}
else if (_player.Class == ClassType.WildCard)
{
    var personas = new List<Persona>();
    if (_player.ActivePersona != null) personas.Add(_player.ActivePersona);
    personas.AddRange(_player.PersonaStock);
    participantPool = personas.Distinct().Cast<object>().ToList();
}
```

This is where Operator and Wild Card flows diverge. Operators fuse demon
combatants. Wild Cards fuse Persona masks.

Second-parent duplicate UX code:

```csharp
Dictionary<object, string> p2DisabledReasons =
    BuildOwnedDuplicateResultReasons(participantPool, p1, parents);

RitualParticipantSelectionResult<object> p2Result =
    _uiBridge.SelectRitualParticipant<object>(
        participantPool,
        "CHOOSE THE SECOND PARTICIPANT:",
        parents,
        p2DisabledReasons);
```

This is the pre-selection guard that greys out second parents which would create
an already-owned result.

Planning code:

```csharp
FusionParticipant parentA = FusionParticipant.From(p1);
FusionParticipant parentB = FusionParticipant.From(p2);
FusionParticipant? sacrificeParticipant = sacrifice != null
    ? FusionParticipant.From(sacrifice)
    : null;

if (!_planFactory.TryCreate(
    parentA,
    parentB,
    sacrificeParticipant,
    isSacrificial,
    MoonPhaseSystem.CurrentPhase,
    out FusionPlan? plan) || plan == null)
{
    _messenger.Publish("The spirits remain silent. This combination yields no result.", ConsoleColor.Red, 1000);
    continue;
}
```

This is where raw selected objects become normalized Fusion participants and a
non-mutating plan.

Preview code:

```csharp
SkillInheritanceSelectionResult inheritanceResult = _uiBridge.SelectInheritedSkills(
    plan.DisplaySkills.ToList(),
    plan.MaxInheritanceSlots,
    plan.InherentSkills.ToList(),
    plan.ExclusiveSkills.ToList());

if (inheritanceResult.Kind == SkillInheritanceSelectionKind.Aborted) break;

List<string> chosenSkills = inheritanceResult.Skills.ToList();
Combatant? staged = _previewFactory.CreatePreview(plan, chosenSkills);
```

The staged combatant is the object shown to the player. It must not be a live
party or stock reference.

Transaction code:

```csharp
var context = new FusionContext(
    _player,
    parents,
    sacrifice,
    chosenSkills,
    plan.TargetId,
    _messenger,
    _partyManager);

_mutator.ExecuteFusionTransaction(context, plan.Operation);
```

This is the commit point. Anything before this should be safe to cancel.

Important cancel behavior:

- canceling first parent exits to the Cathedral menu,
- canceling second parent restarts parent selection,
- canceling sacrifice restarts parent selection,
- aborting skill inheritance exits the current staged plan,
- `Wait` at confirmation returns to skill inheritance,
- `Cancel` or `Forbidden` at confirmation returns to participant selection,
- only confirmation reaches the mutator.

### Participant Pool Construction

Operators use demons from:

- active party demons,
- `DemonStock`.

Wild Cards use Personas from:

- `ActivePersona`,
- `PersonaStock`.

The conductor currently builds these pools directly. This is acceptable for the
console prototype but is a future candidate for a roster/query service.

### Accident Handling

Fusion accidents are revealed only after confirmation. When `plan.IsAccident`
is true:

- deliberate chosen skills are cleared,
- random skills are selected from `plan.PickableSkills`,
- each selected skill has a chance to mutate through `FusionCalculator`.

This means the player confirms the intended ritual, then the accident overrides
the inherited kit.

Key implementation:

```csharp
if (plan.IsAccident)
{
    chosenSkills.Clear();

    Random rnd = new Random();
    var accidentPool = plan.PickableSkills
        .OrderBy(x => rnd.Next())
        .Take(plan.MaxInheritanceSlots)
        .ToList();

    for (int i = 0; i < accidentPool.Count; i++)
    {
        if (rnd.Next(0, 100) < 20)
        {
            accidentPool[i] = _calculator.GetMutatedSkill(accidentPool[i]);
        }
    }

    chosenSkills = accidentPool;
}
```

This is still not deterministic-test friendly because it creates a local
`Random`. That should eventually be replaced with injectable randomness.

### `HandleCompendiumRecall`

Gets a typed recall result from the bridge, calculates recall cost, validates
whether the player class has somewhere to place the recalled entity, retrieves a
snapshot, and asks the mutator to finalize recall.

The conductor performs capacity pre-checks. Duplicate and Macca transaction
checks are finalized inside `FusionMutator.FinalizeRecall`.

Key implementation:

```csharp
CompendiumRecallResult recall = _uiBridge.ShowCompendiumRecallMenu();
if (recall.Kind != CompendiumRecallResultKind.Selected || recall.Entry == null) return;

int cost = _compendium.CalculateRecallCost(entry.SourceId);

bool canRecall = _player.Class switch
{
    ClassType.Operator => _partyManager.ActiveParty.Count < 4 ||
                          _partyManager.HasOpenDemonStockSlot(_player),
    ClassType.WildCard => _partyManager.HasOpenPersonaStockSlot(_player),
    _ => false
};

Combatant? snapshot = _compendium.GetRecallEntry(entry.SourceId);
if (snapshot != null && _mutator.FinalizeRecall(_player, snapshot, cost))
{
    _messenger.Publish($"{snapshot.Name} has been materialized.", ConsoleColor.Cyan, 800);
}
```

### `HandleRegistration`

Operators register demon combatants from active party plus stock. Wild Cards
register Persona masks by converting the selected Persona into a transient
combatant through `FusionParticipant.CreateTransientCombatant`.

The Compendium currently stores one combatant-shaped snapshot regardless of
whether the source was an Operator demon or Wild Card Persona.

## State And Mutation

The conductor should not directly mutate party, stock, economy, or Compendium
state except for calling `CompendiumRegistry.RegisterDemon`.

Committed Fusion mutation is expected to happen through `FusionMutator`.
Preview mutation must remain isolated to staged clones created by
`FusionPreviewFactory`.

## Invariants And Safety Rules

- No cancel/back path should consume materials.
- No preview path should mutate real party or stock.
- Duplicate create-results should be blocked before transaction execution and
  guarded again by the mutator.
- The conductor should stay a workflow router, not a rule container.
- Console menu labels should stay in the bridge, not in workflow logic.

## Data Dependencies

The conductor indirectly depends on:

- static `Database` through calculator, planning, preview, and Compendium logic,
- `MoonPhaseSystem.CurrentPhase`,
- current player class and stock state,
- field UI state used by the bridge.

## Failure / Cancel / Edge Behavior

The conductor treats expected navigation as typed result states rather than raw
`null` wherever the Fusion bridge has been migrated:

- main menu back,
- ritual participant cancel/unavailable,
- skill inheritance abort,
- confirmation wait/cancel/forbidden,
- Compendium recall unavailable/back,
- registration unavailable/back.

No-result fusion combinations publish a message and return to selection.

## Tests And Verification

Relevant regression coverage lives in:

- [`Convergence.Tests/FusionBridgeResultTests.cs`](../../../Convergence.Tests/FusionBridgeResultTests.cs)
- [`Convergence.Tests/FusionBugRegressionTests.cs`](../../../Convergence.Tests/FusionBugRegressionTests.cs)

Important manual smoke checks:

- cancel from every Cathedral menu layer,
- normal binary fusion,
- sacrificial fusion,
- duplicate-result second parent disabled,
- Mitama stat boost,
- Compendium recall duplicate blocked,
- registration update path.

## Refactor Notes

The next architecture step is not to split projects immediately. First, the
remaining bridge and strategy files should receive the same documentation and
test coverage. After Field and Battle reach similar baselines, this conductor
can be turned into a host-facing workflow adapter around framework commands,
results, and state transitions.
