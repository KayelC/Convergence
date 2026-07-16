# Convergence Godot Host

This project is the real Godot 4.7.1 .NET reference consumer for
`Convergence.Framework`. It is a host example, not a framework dependency.

The build copies the canonical Training Annex pack into an ignored `res://Content`
directory. The sample then proves:

- content loading through `Godot.FileAccess`;
- catalog construction and ruleset binding;
- runtime actor IDs mapped to Godot `Node` instances;
- typed command selection and action execution;
- ordered encounter events mapped back to scene nodes; and
- host-owned JSON persistence restored and validated by the framework.

Run the noninteractive proof with the official Godot 4.7.1 .NET executable:

```powershell
godot --headless --path samples/Convergence.GodotHost -- --convergence-smoke
```

Success is reported by `CONVERGENCE_GODOT_SMOKE_OK` and process exit code `0`.
