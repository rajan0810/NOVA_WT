using UnityEngine;

/// <summary>
/// Continuously rotates this object to face the camera, so it stays legible/reachable
/// no matter where the user walks around the spawned engine. Matches the same billboard
/// convention already used by MarkerController for QR markers, for consistency across
/// the app.
///
/// Setup:
/// - Add this component to the Reset button (or its parent), alongside the button's
///   interaction components.
/// - Leave "Target Camera" unassigned to auto-use the headset's main camera; only
///   assign it manually if you need to face a specific camera instead.
/// </summary>
public class FaceCamera : MonoBehaviour
{
    [Tooltip("Camera to face. Leave empty to auto-use Camera.main (the headset's centered camera).")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("If true, only rotates around the vertical (Y) axis, keeping the object upright instead of tilting to match head pitch when the user looks up/down. Recommended for buttons/UI.")]
    [SerializeField] private bool lockYAxis = true;

    [Tooltip("If the button appears to face away from the user, enable this to flip it 180 degrees.")]
    [SerializeField] private bool invertFacing = false;

    private void OnEnable()
    {
        if (!targetCamera)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (!targetCamera)
        {
            // OVRCameraRig's camera may not be ready yet at first enable; keep trying.
            targetCamera = Camera.main;
            if (!targetCamera)
                return;
        }

        Vector3 direction = transform.position - targetCamera.transform.position;
        if (invertFacing)
        {
            direction = -direction;
        }

        if (lockYAxis)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
            return; // Camera is essentially at the same position; avoid a zero-length LookRotation.

        transform.rotation = Quaternion.LookRotation(direction);
    }
}
