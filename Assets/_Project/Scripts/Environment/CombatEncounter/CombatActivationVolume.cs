#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Entering any Activation Volume can start or resume the encounter.
/// It does not constrain enemy movement.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatActivationVolume : CombatEncounterVolume
{
    public override CombatEncounterVolumeRole Role =>
        CombatEncounterVolumeRole.Activation;
}
