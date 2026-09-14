using UnityEngine;
#if ZXING_ENABLED
using ZXing;
#endif

/// <summary>
/// Editor/desktop-only sanity check for a QR code image, completely independent of the
/// passthrough camera pipeline. Since Passthrough Camera Access only works on physical
/// Quest 3 / Quest 3S hardware (it is not emulated by Editor Play mode, Quest Link, or the
/// Meta XR Simulator), this lets you verify "is this QR code actually decodable by ZXing"
/// without needing a device build at all.
///
/// Setup:
/// - Add this component to any GameObject in a scene (or a scratch scene).
/// - Assign a Texture2D of your QR code to "Qr Texture". Easiest way: drag the PNG file
///   into Assets, select it, and in the Inspector set Texture Type to "Default" and
///   enable "Read/Write" so pixel data is accessible at runtime, then drag it here.
/// - Press Play, or right-click the component header and choose "Decode Now".
/// </summary>
public class QrDecodeTester : MonoBehaviour
{
    [Tooltip("The QR code image to test. Must have Read/Write enabled in its import settings.")]
    [SerializeField] private Texture2D qrTexture;

    [Tooltip("If true, also attempts decoding once automatically on Play.")]
    [SerializeField] private bool decodeOnStart = true;

    private void Start()
    {
        if (decodeOnStart)
        {
            Decode();
        }
    }

    [ContextMenu("Decode Now")]
    public void Decode()
    {
#if ZXING_ENABLED
        if (!qrTexture)
        {
            Debug.LogWarning("[QrDecodeTester] No texture assigned.");
            return;
        }

        Color32[] pixels;
        try
        {
            pixels = qrTexture.GetPixels32();
        }
        catch (UnityException ex)
        {
            Debug.LogError($"[QrDecodeTester] Could not read pixels from '{qrTexture.name}'. " +
                            "Make sure 'Read/Write Enabled' is checked in its texture import settings. " +
                            $"Error: {ex.Message}");
            return;
        }

        var width = qrTexture.width;
        var height = qrTexture.height;

        // Unity's GetPixels32 returns rows bottom-to-top; ZXing expects top-to-bottom,
        // so flip vertically while packing into a flat RGBA32 byte buffer.
        var rgba = new byte[pixels.Length * 4];
        for (var y = 0; y < height; y++)
        {
            var srcRow = (height - 1 - y) * width;
            var dstRow = y * width;
            for (var x = 0; x < width; x++)
            {
                var c = pixels[srcRow + x];
                var o = (dstRow + x) * 4;
                rgba[o] = c.r;
                rgba[o + 1] = c.g;
                rgba[o + 2] = c.b;
                rgba[o + 3] = c.a;
            }
        }

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = { TryHarder = true }
        };

        var result = reader.Decode(rgba, width, height, RGBLuminanceSource.BitmapFormat.RGBA32);

        if (result != null)
        {
            Debug.Log($"[QrDecodeTester] SUCCESS decoding '{qrTexture.name}' ({qrTexture.width}x{qrTexture.height}): \"{result.Text}\"");
        }
        else
        {
            Debug.LogWarning($"[QrDecodeTester] FAILED to decode '{qrTexture.name}' ({qrTexture.width}x{qrTexture.height}). " +
                              "Try a larger/higher-contrast QR code, or check the image isn't blurry/cropped.");
        }
#else
        Debug.LogWarning("[QrDecodeTester] ZXING_ENABLED is not defined for this build target, cannot decode.");
#endif
    }
}
