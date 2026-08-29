#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Retention Volumes keep an already active encounter alive while the player
/// moves around irregular or multi-level combat spaces. Entering one by itself
/// does not start a dormant encounter.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatRetentionVolume : CombatEncounterVolume
{
    public override CombatEncounterVolumeRole Role =>
        CombatEncounterVolumeRole.Retention;
}
