using UnityEngine;

/// <summary>
/// Wires a pressable VR button to EnginePartsResetter.ResetAllParts(), with a short
/// cooldown so rapid double-presses (common with poke/ray interactions) don't stack
/// multiple resets, plus optional audio feedback.
///
/// Setup:
/// - Add this component next to (or instead of directly wiring) your button's press
///   event.
/// - Assign "Target Resetter" to the EnginePartsResetter on your spawned engine.
///   If left unassigned, it searches the scene for one automatically at Start, which
///   is convenient since the engine is spawned dynamically by QrCodeDisplayManager
///   rather than being present in the scene at edit time.
/// - Hook your button's press event (UI Button OnClick, or PokeInteractable's
///   WhenSelect / an InteractableUnityEventWrapper's _whenSelect) to this component's
///   Press() method.
/// </summary>
public class ResetButtonHandler : MonoBehaviour
{
    [Tooltip("The resetter to trigger. If left empty, one is found automatically in the scene at Start.")]
    [SerializeField] private EnginePartsResetter targetResetter;

    [Tooltip("Minimum seconds between presses, to prevent a single physical press (which can fire multiple pointer events) from triggering more than one reset.")]
    [SerializeField] private float cooldownSeconds = 0.5f;

    [Tooltip("Optional: played when the button is successfully pressed.")]
    [SerializeField] private AudioSource pressAudioSource;

    private float _lastPressTime = -999f;

    private void Start()
    {
        if (!targetResetter)
        {
            targetResetter = FindFirstObjectByType<EnginePartsResetter>();
            if (!targetResetter)
            {
                Debug.LogWarning("[ResetButtonHandler] No EnginePartsResetter found in the scene yet. " +
                                  "It will keep looking each press until the engine is spawned.");
            }
        }
    }

    /// <summary>
    /// Call this from your button's press/select event.
    /// </summary>
    public void Press()
    {
        if (Time.time - _lastPressTime < cooldownSeconds)
            return;

        _lastPressTime = Time.time;

        if (!targetResetter)
        {
            targetResetter = FindFirstObjectByType<EnginePartsResetter>();
        }

        if (!targetResetter)
        {
            Debug.LogWarning("[ResetButtonHandler] Reset pressed, but no EnginePartsResetter exists yet (engine not spawned?).");
            return;
        }

        if (pressAudioSource)
        {
            pressAudioSource.Play();
        }

        targetResetter.ResetAllParts();
    }
}
