using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using Meta.WitAi.TTS.Utilities;

/// <summary>
/// Sends the current prompt text to the backend (a Google Apps Script endpoint) and
/// displays - and optionally speaks aloud via TTS - the answer that comes back.
///
/// Can be triggered two ways:
/// - Automatically, by calling Ask() from code (VoiceScript does this once speech
///   recognition finishes transcribing a question).
/// - Manually, by pressing "Manual Ask Button" on the controller - useful as a fallback,
///   or to re-send the same prompt again without re-recording.
/// </summary>
public class APIManager : MonoBehaviour
{
    [SerializeField] private string gasUrl;
    [SerializeField] private TextMeshProUGUI prompt;
    [SerializeField] private TextMeshProUGUI Response;

    [Tooltip("Status text shown to the user while the question is being sent/answered.")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Tooltip("Optional: speaks the answer aloud once it arrives. Leave empty to skip TTS.")]
    [SerializeField] private TTSSpeaker ttsSpeaker;

    [Tooltip("Controller button that manually (re)sends whatever is currently in the prompt field.")]
    [SerializeField] private OVRInput.Button manualAskButton = OVRInput.Button.Two;

    private bool _isAsking;

    private void Update()
    {
        if (OVRInput.GetUp(manualAskButton))
        {
            Ask();
        }
    }

    /// <summary>
    /// Sends whatever text is currently in the prompt field to the backend and displays
    /// (and speaks, if a TTSSpeaker is assigned) the answer. Safe to call multiple times -
    /// ignored if a request is already in flight, or if the prompt is empty.
    /// </summary>
    public void Ask()
    {
        if (_isAsking)
        {
            Debug.LogWarning("[APIManager] Ask() called while a request is already in flight - ignoring.");
            return;
        }

        if (!prompt || string.IsNullOrWhiteSpace(prompt.text))
        {
            Debug.LogWarning("[APIManager] Ask() called with an empty prompt - nothing to send.");
            return;
        }

        StartCoroutine(SendDataToGas());
    }

    private IEnumerator SendDataToGas()
    {
        _isAsking = true;
        SetStatus("Thinking...");
        Debug.Log($"[APIManager] Sending prompt: \"{prompt.text}\"");

        WWWForm form = new WWWForm();
        form.AddField("parameter", prompt.text);
        UnityWebRequest www = UnityWebRequest.Post(gasUrl, form);

        yield return www.SendWebRequest();
        string response;

        if (www.result == UnityWebRequest.Result.Success)
        {
            response = www.downloadHandler.text;
        }
        else
        {
            response = "There was an error!";
            Debug.LogError($"[APIManager] Request failed: {www.error}");
        }

        Debug.Log($"[APIManager] Response: {response}");
        if (Response)
        {
            Response.text = response;
        }

        SetStatus("Ready");

        if (ttsSpeaker && www.result == UnityWebRequest.Result.Success)
        {
            ttsSpeaker.Speak(response);
        }

        _isAsking = false;
    }

    private void SetStatus(string status)
    {
        if (statusText)
        {
            statusText.text = status;
        }
    }
}
