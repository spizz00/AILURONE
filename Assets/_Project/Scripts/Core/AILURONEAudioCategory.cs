using UnityEngine;

/// <summary>
/// Explicit override for AudioSources whose name does not identify whether
/// they are music or sound effects.
/// </summary>
public sealed class AILURONEAudioCategory : MonoBehaviour
{
    public enum Category
    {
        Automatic,
        Music,
        SoundEffects
    }

    public Category category = Category.Automatic;
}
