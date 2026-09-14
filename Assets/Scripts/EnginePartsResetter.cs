using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Snapshots the local position/rotation of every grabbable part under this engine's
/// root at startup, then can restore all of them back to that original layout on demand
/// (e.g. from a Reset button). Works regardless of which interaction components each
/// part uses (Grabbable / DistanceGrabInteractable / DistanceHandGrabInteractable) -
/// it only cares about Rigidbody + Transform, since every grabbable part in this engine
/// has its own Rigidbody.
///
/// Setup:
/// - Add this component to the root GameObject of the assembled engine (the one that
///   gets instantiated by QrCodeDisplayManager, or a parent of it).
/// - Nothing else needs wiring - it auto-discovers every Rigidbody in its children at
///   Start and remembers their starting local pose.
/// - Call ResetAllParts() from a UI Button's OnClick, or any other trigger.
/// </summary>
public class EnginePartsResetter : MonoBehaviour
{
    [Tooltip("Fired after a reset completes. Hook up sound/haptics/UI feedback here.")]
    [SerializeField] private UnityEvent onReset;

    [Tooltip("If true, briefly disables and re-enables each part's GameObject during reset, which forces any hand/controller currently holding it to release cleanly instead of fighting the reset.")]
    [SerializeField] private bool forceReleaseActiveGrabs = true;

    private struct PartSnapshot
    {
        public Transform PartTransform;
        public Rigidbody PartRigidbody;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    private readonly List<PartSnapshot> _snapshots = new();

    private void Start()
    {
        CapturePartSnapshots();
    }

    /// <summary>
    /// Records the current local position/rotation of every Rigidbody found under this
    /// object as the "original" layout to restore to later. Safe to call again manually
    /// (e.g. after spawning parts dynamically) to re-baseline what "reset" means.
    /// </summary>
    public void CapturePartSnapshots()
    {
        _snapshots.Clear();

        var rigidbodies = GetComponentsInChildren<Rigidbody>(includeInactive: true);
        foreach (var rb in rigidbodies)
        {
            _snapshots.Add(new PartSnapshot
            {
                PartTransform = rb.transform,
                PartRigidbody = rb,
                LocalPosition = rb.transform.localPosition,
                LocalRotation = rb.transform.localRotation
            });
        }

        Debug.Log($"[EnginePartsResetter] Captured original layout for {_snapshots.Count} part(s).");
    }

    /// <summary>
    /// Restores every part to the local position/rotation it had when snapshots were
    /// captured (normally at Start). Call this from your Reset button.
    /// </summary>
    public void ResetAllParts()
    {
        if (_snapshots.Count == 0)
        {
            Debug.LogWarning("[EnginePartsResetter] No snapshots captured yet - nothing to reset.");
            return;
        }

        foreach (var snapshot in _snapshots)
        {
            if (!snapshot.PartTransform)
                continue; // Part may have been destroyed since capture.

            if (forceReleaseActiveGrabs)
            {
                // Toggling the GameObject forces any interactor currently holding this
                // part to drop it cleanly (Grabbable.OnDisable ends the active transform),
                // rather than the reset fighting an in-progress grab this same frame.
                var go = snapshot.PartTransform.gameObject;
                go.SetActive(false);
                go.SetActive(true);
            }

            if (snapshot.PartRigidbody)
            {
                snapshot.PartRigidbody.linearVelocity = Vector3.zero;
                snapshot.PartRigidbody.angularVelocity = Vector3.zero;
            }

            snapshot.PartTransform.SetLocalPositionAndRotation(snapshot.LocalPosition, snapshot.LocalRotation);
        }

        Debug.Log($"[EnginePartsResetter] Reset {_snapshots.Count} part(s) to their original layout.");
        onReset?.Invoke();
    }
}
