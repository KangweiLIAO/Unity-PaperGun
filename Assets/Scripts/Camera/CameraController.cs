using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Vector3 offset;  // Custom camera offset
    public float smoothIdx = 10f;  // Smoothing speed for camera movement
    public float screenEdgeBuffer = 0.1f;  // Buffer to keep objects within screen edge

    [SerializeField] private GameObject player;  // Reference to the player GameObject
    private Vector3 hitPoint;  // The aimed hit point
    private Camera cam;  // Reference to the main camera
    private Quaternion fixedRotation;  // Camera's fixed rotation

    void Start()
    {
        cam = Camera.main;
        fixedRotation = Quaternion.Euler(75f, 0f, 0f);  // fixed rotation
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Get hit point from player's aiming
        hitPoint = player.GetComponent<PlayerController>().GetHit().point;
        hitPoint = ClampHitPoint(hitPoint);

        // Calculate the midpoint between the player and the aimed point
        Vector3 midPoint = (player.transform.position + hitPoint) / 2f;

        // Calculate the desired camera position using the midpoint and offset
        Vector3 camPosition = new Vector3(midPoint.x, midPoint.y + offset.y, midPoint.z + offset.z);

        // Adjust the camera position to ensure both the player and hit point are within the viewport
        camPosition = ClampCameraPos(camPosition, player.transform.position, hitPoint);

        // Smooth transition between current and desired position
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, camPosition, smoothIdx * Time.deltaTime);

        // Assign the position and lock rotation
        transform.position = smoothedPosition;
        transform.rotation = fixedRotation;  // Maintain fixed rotation (75, 0, 0)
    }

    void OnDrawGizmos()
    {
        if (cam == null || player == null) return;

        // Draw a white line representing the aiming ray from the player to the mouse position
        Gizmos.color = Color.white;
        Gizmos.DrawLine(player.transform.position, hitPoint);

        // Draw a wireframe sphere at the hit point (mouse position) in the world
        Gizmos.DrawWireSphere(hitPoint, 0.2f);  // Sphere size is adjustable
    }

    // Adjust the camera position to ensure both the player and the hit point are visible on screen
    Vector3 ClampCameraPos(Vector3 camPosition, Vector3 playerPos, Vector3 aimPos)
    {
        // convert the player's position and the hit point to viewport space
        Vector3 playerViewportPos = cam.WorldToViewportPoint(playerPos);
        Vector3 hitpointViewportPos = cam.WorldToViewportPoint(aimPos);

        // Check if the player or the hit point is outside the viewport bounds (with a buffer)
        if (!IsInViewport(playerViewportPos) || !IsInViewport(hitpointViewportPos))
        {
            // Debug.Log(playerViewportPos + " " + hitpointViewportPos);
            // Adjust the camera's position to center both the player and the hit point within the viewport
            Vector3 midpointViewport = (playerViewportPos + hitpointViewportPos) / 2f;

            // Move the camera to recenter the player and hit point based on their world positions
            Vector3 newCamPosition = cam.ViewportToWorldPoint(new Vector3(midpointViewport.x, midpointViewport.y, cam.nearClipPlane));
            camPosition = new Vector3(newCamPosition.x, camPosition.y, newCamPosition.z);  // Maintain Y offset from the original camera position
        }

        return camPosition;
    }

    // Clamps the hit point within the viewport boundaries
    Vector3 ClampHitPoint(Vector3 worldHitPoint)
    {
        // Convert hit point from world space to viewport space
        Vector3 hitpointViewportPos = cam.WorldToViewportPoint(worldHitPoint);

        // Clamp the viewport position to stay within the screen edges
        hitpointViewportPos.x = Mathf.Clamp(hitpointViewportPos.x, screenEdgeBuffer, 1 - screenEdgeBuffer);
        hitpointViewportPos.y = Mathf.Clamp(hitpointViewportPos.y, screenEdgeBuffer, 1 - screenEdgeBuffer);

        // Convert the clamped viewport position back to world space
        Vector3 clampedWorldHitPoint = cam.ViewportToWorldPoint(new Vector3(hitpointViewportPos.x, hitpointViewportPos.y, hitpointViewportPos.z));

        return clampedWorldHitPoint;
    }

    // Helper method to check if an object is within the viewport bounds
    bool IsInViewport(Vector3 viewportPos)
    {
        return viewportPos.x >= screenEdgeBuffer && viewportPos.x <= (1 - screenEdgeBuffer) &&
               viewportPos.y >= screenEdgeBuffer && viewportPos.y <= (1 - screenEdgeBuffer);
    }
}
