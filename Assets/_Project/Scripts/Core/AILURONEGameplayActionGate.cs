public static class AILURONEGameplayActionGate
{
    private static bool _deploymentLocked;

    public static bool IsPaused =>
        GameManager.Instance != null &&
        GameManager.Instance.isGamePaused;

    public static bool AllowsGameplayActions =>
        !_deploymentLocked &&
        !IsPaused &&
        (GameManager.Instance == null ||
         !GameManager.Instance.IsLevelEnded);

    public static void SetDeploymentLocked(
        bool locked
    )
    {
        _deploymentLocked = locked;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetState()
    {
        _deploymentLocked = false;
    }
}
