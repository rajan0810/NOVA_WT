using UnityEngine;
using Oculus.Voice;
using TMPro;

/// <summary>
/// Drives the full voice Q&amp;A flow: toggles mic recording on a single controller
/// button (press once to start listening, press again to stop - rather than only ever
/// starting), keeps the status text updated at every stage (Listening / Thinking / Ready /
/// error), and automatically sends the transcribed question to APIManager once speech
/// recognition finishes, so you don't need a second button press to get an answer.
///
/// Setup:
/// - Assign "Voice Experience" to the scene's AppVoiceExperience component (already
///   wired to a Wit.ai configuration with speech-to-text).
/// - Assign "Status Text" to whatever TextMeshProUGUI shows Listening/Thinking/Ready.
/// - Assign "Api Manager" to the APIManager that sends the question and shows/speaks
///   the answer.
/// - The existing AppVoiceExperience Inspector events (OnStartListening -> status text,
///   OnFullTranscription -> prompt text) can stay as they are; this script only adds the
///   pieces that were missing (stop handling, error handling, and auto-triggering the
///   answer) rather than fighting that existing wiring.
/// </summary>
public class VoiceScript : MonoBehaviour
{
    [Tooltip("The Wit.ai voice experience component that handles speech-to-text.")]
    public AppVoiceExperience voiceExperience;

    [Tooltip("Controller button that toggles mic recording: press once to start listening, press again to stop.")]
    public OVRInput.Button micButton = OVRInput.Button.One;

    [Tooltip("Status text shown to the user (Listening / Thinking / Ready / error messages).")]
    public TextMeshProUGUI statusText;

    [Tooltip("If true, automatically sends the transcribed question to APIManager as soon as speech recognition finishes, so a single button press gets you an answer. If false, you'll need to trigger APIManager's manual ask button yourself.")]
    public bool autoAskAfterTranscription = true;

    [Tooltip("Handles sending the question and displaying/speaking the answer.")]
    public APIManager apiManager;

    private void OnEnable()
    {
        if (!voiceExperience)
        {
            Debug.LogWarning("[VoiceScript] Voice Experience is not assigned - voice input will not work.");
            return;
        }

        voiceExperience.VoiceEvents.OnStartListening.AddListener(HandleStartListening);
        voiceExperience.VoiceEvents.OnStoppedListening.AddListener(HandleStoppedListening);
        voiceExperience.VoiceEvents.OnFullTranscription.AddListener(HandleFullTranscription);
        voiceExperience.VoiceEvents.OnError.AddListener(HandleError);
        voiceExperience.VoiceEvents.OnAborted.AddListener(HandleAborted);
    }

    private void OnDisable()
    {
        if (!voiceExperience)
            return;

        voiceExperience.VoiceEvents.OnStartListening.RemoveListener(HandleStartListening);
        voiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(HandleStoppedListening);
        voiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(HandleFullTranscription);
        voiceExperience.VoiceEvents.OnError.RemoveListener(HandleError);
        voiceExperience.VoiceEvents.OnAborted.RemoveListener(HandleAborted);
    }

    private void Update()
    {
        if (!voiceExperience)
            return;

        if (OVRInput.GetUp(micButton))
        {
            if (voiceExperience.Active)
            {
                Debug.Log("[VoiceScript] Mic button pressed while listening - stopping.");
                voiceExperience.Deactivate();
            }
            else
            {
                Debug.Log("[VoiceScript] Mic button pressed while idle - starting to listen.");
                voiceExperience.Activate();
            }
        }
    }

    private void HandleStartListening()
    {
        SetStatus("Listening...");
    }

    private void HandleStoppedListening()
    {
        // Transcription/answer status will overwrite this shortly; this just covers the
        // brief gap right after the mic stops, so status never looks "stuck".
        SetStatus("Thinking...");
    }

    private void HandleFullTranscription(string transcription)
    {
        Debug.Log($"[VoiceScript] Transcribed: \"{transcription}\"");

        if (autoAskAfterTranscription)
        {
            if (apiManager)
            {
                apiManager.Ask();
            }
            else
            {
                Debug.LogWarning("[VoiceScript] Auto Ask After Transcription is enabled but Api Manager is not assigned.");
            }
        }
    }

    private void HandleError(string errorCode, string errorMessage)
    {
        Debug.LogWarning($"[VoiceScript] Speech recognition error: {errorCode} - {errorMessage}");
        SetStatus("Didn't catch that - try again");
    }

    private void HandleAborted()
    {
        SetStatus("Ready");
    }

    private void SetStatus(string status)
    {
        if (statusText)
        {
            statusText.text = status;
        }
    }
}
