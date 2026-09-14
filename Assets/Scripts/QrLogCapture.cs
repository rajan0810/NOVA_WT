using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Captures Debug.Log/LogWarning/LogError output relevant to QR scanning and engine
/// spawning from the moment the application starts - before any scene has even loaded -
/// so a debug UI panel added or enabled later still has the full history to show.
///
/// This is a plain static buffer, decoupled from any single GameObject's lifecycle:
/// subscription happens exactly once, as early as Unity allows
/// (RuntimeInitializeLoadType.SubsystemRegistration), and keeps running for the whole
/// session regardless of which scenes load/unload or which panel objects come and go.
/// </summary>
public static class QrLogCapture
{
    public readonly struct Entry
    {
        public readonly string Message;
        public readonly LogType Type;
        public readonly DateTime Time;

        public Entry(string message, LogType type, DateTime time)
        {
            Message = message;
            Type = type;
            Time = time;
        }
    }

    /// <summary>
    /// Only messages containing at least one of these substrings are kept. Covers the
    /// engine spawn/anchor pipeline, ZXing QR decode, and passthrough camera lifecycle -
    /// the camera tag matters so you can see *why* scanning never even started (e.g. a
    /// permission or init failure) instead of just silence before any QR is detected.
    /// </summary>
    public static readonly string[] TagFilters =
    {
        "[QrCodeDisplayManager]",
        "[QRCodeScanner]",
        "PCA:",
        "[EnginePartsResetter]",
        "[ResetButtonHandler]",
        "[VoiceScript]",
        "[APIManager]"
    };

    /// <summary>Maximum number of entries retained; oldest are dropped first.</summary>
    public const int MaxEntries = 300;

    private static readonly object Lock = new();
    private static readonly List<Entry> Buffer = new(MaxEntries);

    /// <summary>
    /// Bumped every time a new entry is captured. Panels can cheaply poll this to know
    /// whether they need to repaint instead of rebuilding text every frame.
    /// </summary>
    public static int Version { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        // Guard against duplicate subscription (e.g. domain reload in the Editor).
        Application.logMessageReceivedThreaded -= OnLog;
        Application.logMessageReceivedThreaded += OnLog;
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (!PassesFilter(message))
            return;

        var entry = new Entry(message, type, DateTime.Now);

        lock (Lock)
        {
            Buffer.Add(entry);
            if (Buffer.Count > MaxEntries)
            {
                Buffer.RemoveAt(0);
            }
            Version++;
        }
    }

    private static bool PassesFilter(string message)
    {
        foreach (var tag in TagFilters)
        {
            if (!string.IsNullOrEmpty(tag) && message.Contains(tag))
                return true;
        }
        return false;
    }

    /// <summary>Returns a snapshot copy of everything captured so far, oldest first.</summary>
    public static List<Entry> Snapshot()
    {
        lock (Lock)
        {
            return new List<Entry>(Buffer);
        }
    }

    /// <summary>Clears all captured history.</summary>
    public static void Clear()
    {
        lock (Lock)
        {
            Buffer.Clear();
            Version++;
        }
    }
}
