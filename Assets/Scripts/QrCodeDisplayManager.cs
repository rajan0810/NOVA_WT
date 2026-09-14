using System;
using System.Collections.Generic;
using UnityEngine;
using PassthroughCameraSamples;
using Meta.XR;

public class QrCodeDisplayManager : MonoBehaviour
{
#if ZXING_ENABLED
    [SerializeField] private QrCodeScanner scanner; // Reference to the QR code scanner
    [SerializeField] private EnvironmentRaycastManager envRaycastManager; // For raycasting in the environment
    [SerializeField] private WebCamTextureManager passthroughCameraManager; // Camera manager for passthrough

    [Tooltip("Prefab of the 3D object to spawn (e.g., engine model).")]
    [SerializeField] private GameObject enginePrefab;

    // Stores spawned objects by QR text
    private readonly Dictionary<string, GameObject> _spawnedObjects = new();
    private PassthroughCameraEye _passthroughCameraEye;

    // Key for saving anchor mappings in PlayerPrefs
    private const string AnchorMappingsKey = "AnchorMappings";

    [Serializable]
    public class AnchorMappingEntry
    {
        public string qrText;
        public string uuid;
    }

    [Serializable]
    public class AnchorMappingList
    {
        public List<AnchorMappingEntry> mappings = new();
    }

    private void Awake()
    {
        if (!scanner || !envRaycastManager || !passthroughCameraManager || !enginePrefab)
        {
            Debug.LogError("[QrCodeDisplayManager] One or more required references are unassigned in the Inspector " +
                            $"(scanner={(scanner ? "OK" : "MISSING")}, envRaycastManager={(envRaycastManager ? "OK" : "MISSING")}, " +
                            $"passthroughCameraManager={(passthroughCameraManager ? "OK" : "MISSING")}, enginePrefab={(enginePrefab ? "OK" : "MISSING")}). " +
                            "Scanning and spawning will not run until this is fixed.");
            enabled = false;
            return;
        }

        _passthroughCameraEye = passthroughCameraManager.Eye;
        // Attempt to load any saved anchors on startup
        LoadSavedAnchors();
    }

    private void Update()
    {
        DetectAndSpawnObjects();
    }

    private async void DetectAndSpawnObjects()
    {
        var qrResults = await scanner.ScanFrameAsync() ?? Array.Empty<QrCodeResult>();

        if (qrResults.Length > 0)
        {
            Debug.Log($"[QrCodeDisplayManager] Detected {qrResults.Length} QR code(s): {string.Join(", ", Array.ConvertAll(qrResults, r => r.text))}");
        }

        foreach (var qrResult in qrResults)
        {
            if (qrResult?.corners == null || qrResult.corners.Length < 4)
            {
                Debug.LogWarning($"[QrCodeDisplayManager] QR '{qrResult?.text}' skipped: fewer than 4 corners detected.");
                continue;
            }

            var centerUV = CalculateCenterUV(qrResult.corners);
            var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(_passthroughCameraEye);

            var centerPixel = new Vector2Int(
                Mathf.RoundToInt(centerUV.x * intrinsics.Resolution.x),
                Mathf.RoundToInt(centerUV.y * intrinsics.Resolution.y)
            );

            var centerRay = PassthroughCameraUtils.ScreenPointToRayInWorld(_passthroughCameraEye, centerPixel);

            if (!envRaycastManager.Raycast(centerRay, out var hitInfo))
            {
                Debug.LogWarning($"[QrCodeDisplayManager] QR '{qrResult.text}' decoded but environment raycast found no surface. Room may not be scanned yet, or QR is out of raycast range.");
                continue;
            }

            var spawnPosition = hitInfo.point; // Position in world space where the object will spawn
            var spawnRotation = Quaternion.LookRotation(hitInfo.normal); // Align rotation with surface normal

            if (!_spawnedObjects.ContainsKey(qrResult.text))
            {
                SpawnObject(qrResult.text, spawnPosition, spawnRotation);
            }
        }
    }

    private Vector2 CalculateCenterUV(Vector3[] corners)
    {
        Vector2 centerUV = Vector2.zero;
        foreach (var corner in corners)
        {
            centerUV += new Vector2(corner.x, corner.y);
        }
        return centerUV / corners.Length;
    }

    /// <summary>
    /// Loads saved anchor mappings from persistent storage and binds each saved anchor.
    /// </summary>
    private async void LoadSavedAnchors()
    {
        AnchorMappingList mappingList = GetAnchorMappings();
        if (mappingList.mappings.Count == 0)
        {
            Debug.Log("[QrCodeDisplayManager] No saved anchors found.");
            return;
        }

        // Prepare lists to load saved anchors.
        List<Guid> uuids = new();
        Dictionary<Guid, string> uuidToQR = new();

        foreach (var entry in mappingList.mappings)
        {
            if (Guid.TryParse(entry.uuid, out Guid guid))
            {
                uuids.Add(guid);
                uuidToQR[guid] = entry.qrText;
            }
        }

        List<OVRSpatialAnchor.UnboundAnchor> unboundAnchors = new();
        var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, unboundAnchors);
        if (loadResult.Success)
        {
            Debug.Log("[QrCodeDisplayManager] Anchors loaded successfully.");
            foreach (var unboundAnchor in unboundAnchors)
            {
                // Await localization for each anchor.
                bool localized = await unboundAnchor.LocalizeAsync();
                if (localized)
                {
                    // Create a new game object for the localized anchor.
                    string qrText = uuidToQR[unboundAnchor.Uuid];
                    var newObj = Instantiate(enginePrefab, Vector3.zero, Quaternion.identity);
                    newObj.transform.localScale = Vector3.one;
                    var spatialAnchor = newObj.AddComponent<OVRSpatialAnchor>();
                    // Bind the unbound anchor to the new spatial anchor component.
                    unboundAnchor.BindTo(spatialAnchor);

                    _spawnedObjects[qrText] = newObj;
                    Debug.Log($"[QrCodeDisplayManager] Loaded and bound anchor for QR code {qrText}");
                }
                else
                {
                    Debug.LogError($"[QrCodeDisplayManager] Localization failed for anchor {unboundAnchor.Uuid}");
                }
            }
        }
        else
        {
            Debug.LogError($"[QrCodeDisplayManager] Failed to load anchors with error {loadResult.Status}");
        }
    }

    /// <summary>
    /// Saves a new spawned object's anchor mapping (QR text to anchor UUID) to persistent storage.
    /// </summary>
    private void SaveAnchorMapping(string qrText, Guid uuid)
    {
        AnchorMappingList mappingList;
        if (PlayerPrefs.HasKey(AnchorMappingsKey))
        {
            string json = PlayerPrefs.GetString(AnchorMappingsKey);
            mappingList = JsonUtility.FromJson<AnchorMappingList>(json);
            if (mappingList == null)
                mappingList = new AnchorMappingList();
        }
        else
        {
            mappingList = new AnchorMappingList();
        }

        // Update existing entry or add new.
        var existing = mappingList.mappings.Find(entry => entry.qrText == qrText);
        if (existing != null)
        {
            existing.uuid = uuid.ToString();
        }
        else
        {
            mappingList.mappings.Add(new AnchorMappingEntry { qrText = qrText, uuid = uuid.ToString() });
        }

        string newJson = JsonUtility.ToJson(mappingList);
        PlayerPrefs.SetString(AnchorMappingsKey, newJson);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Retrieves saved anchor mappings from persistent storage.
    /// </summary>
    private AnchorMappingList GetAnchorMappings()
    {
        if (PlayerPrefs.HasKey(AnchorMappingsKey))
        {
            string json = PlayerPrefs.GetString(AnchorMappingsKey);
            AnchorMappingList mappingList = JsonUtility.FromJson<AnchorMappingList>(json);
            return mappingList;
        }
        return new AnchorMappingList();
    }

    /// <summary>
    /// Spawns the engine object and creates a spatial anchor for it.
    /// </summary>
    private async void SpawnObject(string qrText, Vector3 position, Quaternion rotation)
    {
        var spawnedObject = Instantiate(enginePrefab, position, rotation);
        spawnedObject.transform.localScale = Vector3.one;

        // Add the spatial anchor component and save the anchor asynchronously.
        var spatialAnchor = spawnedObject.AddComponent<OVRSpatialAnchor>();

        var saveResult = await spatialAnchor.SaveAnchorAsync();
        if (saveResult.Success)
        {
            Debug.Log($"[QrCodeDisplayManager] Anchor {spatialAnchor.Uuid} saved successfully for QR code {qrText}");
            SaveAnchorMapping(qrText, spatialAnchor.Uuid);
        }
        else
        {
            Debug.LogError($"[QrCodeDisplayManager] Failed to save anchor for QR code {qrText} with error {saveResult.Status}");
        }

        _spawnedObjects[qrText] = spawnedObject;
        Debug.Log($"[QrCodeDisplayManager] Spawned object for QR code: {qrText} at {position}");
    }

    /// <summary>
    /// Despawns the object associated with the given QR text and erases its spatial anchor.
    /// </summary>
    public void DespawnObject(string qrText)
    {
        if (_spawnedObjects.TryGetValue(qrText, out var obj))
        {
            var spatialAnchor = obj.GetComponent<OVRSpatialAnchor>();
            if (spatialAnchor)
            {
                spatialAnchor.EraseAnchorAsync();
            }

            Destroy(obj);
            _spawnedObjects.Remove(qrText);
            Debug.Log($"[QrCodeDisplayManager] Despawned object for QR code: {qrText}");
        }
    }
#endif
}
