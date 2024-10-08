using UnityEngine;
using Mirror;

public class CameraController : NetworkBehaviour
{
    public Vector3 offset = new Vector3(0, 10, -10);  // Custom camera offset
    public float smoothSpeed = 10f;  // Smoothing speed for camera movement
    public float screenEdgeBuffer = 0.1f;  // Buffer to keep objects within screen edge

    private PlayerController playerController;
    private Camera playerCamera;

    [SerializeField]
    private Quaternion fixedRotation = Quaternion.Euler(75f, 0f, 0f);

    private bool isInitialized = false;
    private RaycastHit aimHit;

    private void Start()
    {
        // Delay initialization until after NetworkBehaviour is fully set up
        Invoke(nameof(Initialize), 0.1f);
    }

    private void Initialize()
    {
        if (isLocalPlayer)
        {
            playerController = GetComponent<PlayerController>();
            SetupCamera();
            isInitialized = true;
        }
        else
        {
            // If not local player, disable this component
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (!isInitialized || playerCamera == null) return;

        Vector3 targetPosition = CalculateCameraPosition();
        UpdateCameraPosition(targetPosition);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return; // Only draw when the game is running
        if (playerCamera == null || gameObject == null || playerController == null) return;

        // Get the aim hit from the player controller
        RaycastHit aimHit = playerController.GetAimHit();

        // Draw a white line representing the aiming ray from the player to the hit point
        Gizmos.color = Color.white;
        Gizmos.DrawLine(gameObject.transform.position, aimHit.point);

        // Draw a wireframe sphere at the hit point (mouse position) in the world
        Gizmos.DrawWireSphere(aimHit.point, 0.2f);

        // Optionally, draw the camera position and its view direction
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerCamera.transform.position, 0.5f);
            Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * 5f);
        }
    }

    private void SetupCamera()
    {
        GameObject cameraObject = new GameObject($"PlayerCamera_{netId}");
        playerCamera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        playerCamera.tag = "MainCamera";
        playerCamera.transform.rotation = fixedRotation;

        // Disable the original main camera in scene
        if (Camera.main != null) Camera.main.gameObject.SetActive(false);
    }

    private Vector3 CalculateCameraPosition()
    {
        Vector3 playerPosition = transform.position;
        Vector3 aimPoint = playerController.GetAimHit().point;
        Vector3 midPoint = (playerPosition + aimPoint) / 2f;
        Vector3 desiredPosition = midPoint + offset;

        return ClampView(playerPosition, desiredPosition);
    }

    private Vector3 ClampView(Vector3 playerPosition, Vector3 desiredPosition)
    {
        float distance = Vector3.Distance(playerPosition, desiredPosition);
        if (Physics.Raycast(playerPosition, desiredPosition - playerPosition, out RaycastHit hit, distance, LayerMask.GetMask("Default")))
        {
            aimHit = hit;
            return hit.point - (desiredPosition - playerPosition).normalized * 0.1f;
        }
        return desiredPosition;
    }

    /// <summary>
    /// Update the camera position to the target position with smoothing
    /// </summary>
    /// <param name="targetPosition">The target position to move the camera to</param>
    private void UpdateCameraPosition(Vector3 targetPosition)
    {
        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        playerCamera.transform.rotation = fixedRotation;
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();

        if (playerCamera != null) Destroy(playerCamera.gameObject);

        // Re-enable the original main camera
        GameObject originalMain = GameObject.FindWithTag("MainCamera");
        if (originalMain != null) originalMain.gameObject.SetActive(true);
    }

    public Camera GetPlayerCamera()
    {
        return playerCamera;
    }
}