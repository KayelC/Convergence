namespace JRPGPrototype.Core;

/// <summary>
/// Compatibility vocabulary used only by the legacy console combat path.
/// Clean framework actions use typed affinity and Press Turn outcomes.
/// </summary>
public enum HitType
{
    Normal,
    Critical,
    Weakness,
    Miss,
    Repel,
    Absorb,
    Null
}
