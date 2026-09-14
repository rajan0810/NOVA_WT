using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Renders the log history captured by <see cref="QrLogCapture"/> into a
/// TextMeshProUGUI panel, so scanning/spawning logs can be read directly in headset
/// without needing adb logcat.
///
/// Because QrLogCapture starts listening before any scene even loads, this panel shows
/// the full history from app start the moment it becomes enabled - not just whatever
/// was logged after this GameObject happened to activate. That means you can toggle
/// this panel on partway through a session (e.g. via a menu button) and still see
/// everything relevant that happened before you opened it.
///
/// Setup:
/// - Add this component to a GameObject in the scene.
/// - Assign a TextMeshProUGUI in the "Log Text" field (ideally inside a scrollable
///   panel so older lines aren't just cut off).
/// </summary>
public class QrDebugLogPanel : MonoBehaviour
{
    [Tooltip("TextMeshProUGUI that will display the filtered log lines.")]
    [SerializeField] private TextMeshProUGUI logText;

    [Tooltip("If true, timestamps (HH:mm:ss) are prefixed to each line.")]
    [SerializeField] private bool showTimestamps = true;

    [Tooltip("Maximum number of most-recent lines to render. Older captured history beyond this stays in QrLogCapture and reappears if you raise this value, it's just not displayed.")]
    [SerializeField] private int maxDisplayedLines = 60;

    private int _lastRenderedVersion = -1;

    private void OnEnable()
    {
        // Force a repaint so full history since app start shows immediately,
        // even if this panel was just now enabled for the first time.
        _lastRenderedVersion = -1;
        Repaint();
    }

    private void Update()
    {
        if (QrLogCapture.Version != _lastRenderedVersion)
        {
            Repaint();
        }
    }

    private void Repaint()
    {
        if (!logText)
            return;

        _lastRenderedVersion = QrLogCapture.Version;

        var entries = QrLogCapture.Snapshot();
        var start = Mathf.Max(0, entries.Count - maxDisplayedLines);

        var sb = new StringBuilder();
        for (var i = start; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (showTimestamps)
            {
                sb.Append('[').Append(entry.Time.ToString("HH:mm:ss")).Append("] ");
            }
            sb.Append(ColorForType(entry.Type)).Append(entry.Message).Append("</color>\n");
        }

        logText.text = sb.ToString();
    }

    private static string ColorForType(LogType type)
    {
        return type switch
        {
            LogType.Error or LogType.Exception => "<color=#FF5555>",
            LogType.Warning => "<color=#FFD255>",
            _ => "<color=#DDDDDD>"
        };
    }

    /// <summary>
    /// Clears all captured history (not just what's currently displayed).
    /// Hook this up to a UI button if you want a manual "Clear" action.
    /// </summary>
    public void Clear()
    {
        QrLogCapture.Clear();
        Repaint();
    }
}
