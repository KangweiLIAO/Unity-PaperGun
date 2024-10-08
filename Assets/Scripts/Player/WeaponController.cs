using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class WeaponController : NetworkBehaviour
{
    // --- Audio ---
    public AudioClip GunShotClip;
    public AudioSource source;
    public Vector2 audioPitch = new Vector2(.9f, 1.1f);

    // --- Crosshair ---
    public Vector2 crosshairOffset;  // Offset to adjust cursor position relative to the mouse position
    public Vector2 crosshairSize = new Vector2(32, 32);
    public Color crosshairColor = Color.white;  // Default color is white
    public Texture2D crosshairTexture;
    private RawImage crosshairImage;  // Reference to the UI image that represents the custom cursor
    private Canvas crosshairCanvas;
    private RaycastHit currentAim;

    // --- Muzzle ---
    public GameObject muzzlePrefab;
    public GameObject muzzlePosition;

    // --- Projectile ---
    [Tooltip("The projectile gameobject to instantiate each time the weapon is fired.")]
    public GameObject projectilePrefab;
    [Tooltip("Sometimes a mesh will want to be disabled on fire. For example: when a rocket is fired, we instantiate a new rocket, and disable" +
        " the visible rocket attached to the rocket launcher")]
    public GameObject projectileToDisableOnFire;
    [SerializeField] private bool hasBulletSpread = true;
    [SerializeField]
    private Vector3 bulletSpreadVariance = new Vector3(0.03f, 0f, 0.03f);
    [SerializeField]
    private Transform bulletSpawnPoint;
    [SerializeField]
    private GameObject impactPrefab; // impact effect of bullets
    [SerializeField]
    private TrailRenderer bulletTrail;
    [SerializeField]
    private float shootDelay = 0.5f;
    [SerializeField]
    private LayerMask shootableMask;

    // --- Timing ---
    [SerializeField] private float localCooldown = 0f; // Cooldown for local player
    private float timeLastFired;


    // --- Damage Configs ---
    [SyncVar]
    private float netDamage = 10f;

    public float Damage
    {
        get => netDamage;
        set => netDamage = value;
    }

    // --- Gizmo Variables ---
    private Vector3 lastShootPosition;
    private Vector3 lastShootDirection;
    private RaycastHit lastHit;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        if (!isLocalPlayer) return;
        
        if (source != null) source.clip = GunShotClip;
        SetupCrosshair();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        // Crosshair
        if (crosshairImage != null)
        {
            Vector2 mousePosition = Input.mousePosition;
            crosshairImage.rectTransform.position = mousePosition + crosshairOffset;
        }

        if (Input.GetMouseButton(0) && (Time.time >= localCooldown))
        {
            FireWeapon();
            localCooldown = Time.time + shootDelay;
        }
    }

    private void OnDrawGizmos()
    {
        // If the last shot direction and hit data are available, draw a line to represent the shot
        if (lastShootDirection != Vector3.zero)
        {
            // Draw a ray from the bullet spawn point in the last shoot direction
            Gizmos.color = Color.red;
            Gizmos.DrawLine(lastShootPosition, lastShootPosition + lastShootDirection * lastHit.distance);

            // If a hit occurred, draw a sphere at the impact point
            if (lastHit.collider != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(lastHit.point, 0.1f);
            }
        }
    }

    #region Weapon Firing
    [Command]
    void FireWeapon()
    {
        // Server-side logic
        Vector3 shootDirection = GetShootDirection();
        if (Physics.Raycast(bulletSpawnPoint.position, shootDirection, out RaycastHit serverHit, float.MaxValue, shootableMask))
        {
            DealDamage(serverHit);
        }

        // Trigger client-side effects
        FireWeaponClientRpc();

        // Update last fire time
        timeLastFired = Time.time;
    }

    [ClientRpc]
    private void FireWeaponClientRpc()
    {
        if (!isLocalPlayer) return;

        Vector3 shootDirection = GetShootDirection();
        PlayShootEffect();

        // Perform client-side raycast for visual effects
        if (Physics.Raycast(bulletSpawnPoint.position, shootDirection, out RaycastHit clientHit, float.MaxValue, shootableMask))
        {
            SpawnBulletTrail(clientHit.point, clientHit.distance);
            SpawnImpactEffect(clientHit.point, clientHit.normal);
        }
        else
        {
            // If no hit, show trail going to a far distance
            Vector3 farPoint = bulletSpawnPoint.position + shootDirection * 1000f;
            SpawnBulletTrail(farPoint, 1000f);
        }
    }

    private void PlayShootEffect()
    {
        // Instantiate muzzle flash effect
        if (muzzlePrefab != null)
        {
            GameObject flash = Instantiate(muzzlePrefab, muzzlePosition.transform.position, muzzlePosition.transform.rotation, transform);
            Destroy(flash, 0.1f);
        }

        // Handle projectile
        if (projectilePrefab != null)
        {
            GameObject newProjectile = Instantiate(projectilePrefab, muzzlePosition.transform.position, muzzlePosition.transform.rotation, transform);
        }

        // Disable any gameobjects, if needed
        if (projectileToDisableOnFire != null)
        {
            projectileToDisableOnFire.SetActive(false);
            Invoke("ReEnableDisabledProjectile", 3);
        }

        PlayWeaponAudio(); // Play audio
    }

    private void ReEnableDisabledProjectile()
    {
        projectileToDisableOnFire.SetActive(true);
    }

    /// <summary>
    /// Get the shoot direction based on the current aim and bullet spread
    /// </summary>
    /// <returns></returns>
    private Vector3 GetShootDirection()
    {
        Vector3 direction = currentAim.point - bulletSpawnPoint.position;
        direction.Normalize();

        if (hasBulletSpread)
        {
            direction += new Vector3(
                Random.Range(-bulletSpreadVariance.x, bulletSpreadVariance.x),
                Random.Range(-bulletSpreadVariance.y, bulletSpreadVariance.y),
                Random.Range(-bulletSpreadVariance.z, bulletSpreadVariance.z)
                );
            direction.Normalize();
        }
        return direction;
    }

    private void PlayWeaponAudio()
    {
        if (source != null)
        {
            AudioSource audioSource = source.transform.IsChildOf(transform) ? source : Instantiate(source);
            if (audioSource != null && audioSource.outputAudioMixerGroup != null && audioSource.outputAudioMixerGroup.audioMixer != null)
            {
                audioSource.outputAudioMixerGroup.audioMixer.SetFloat("Pitch", Random.Range(audioPitch.x, audioPitch.y));
                audioSource.pitch = Random.Range(audioPitch.x, audioPitch.y);
                audioSource.PlayOneShot(GunShotClip);

                if (!audioSource.transform.IsChildOf(transform))
                {
                    Destroy(audioSource.gameObject, 4);
                }
            }
        }
    }

    // --- Bullet Trail ---
    private void SpawnBulletTrail(Vector3 hitPoint, float hitDistance)
    {
        TrailRenderer trail = Instantiate(bulletTrail, bulletSpawnPoint.position, Quaternion.identity);
        StartCoroutine(SpawnTrail(trail, hitPoint, hitDistance));
    }

    private IEnumerator SpawnTrail(TrailRenderer trail, Vector3 hitPoint, float hitDistance)
    {
        float time = 0;
        Vector3 startPosition = trail.transform.position;
        while (time < 1)
        {
            trail.transform.position = Vector3.Lerp(startPosition, hitPoint, time);
            time += Time.deltaTime / trail.time;
            yield return null;
        }
        trail.transform.position = hitPoint;
        GameObject impactObj = Instantiate(impactPrefab, hitPoint, Quaternion.LookRotation((startPosition - hitPoint).normalized));
        Destroy(trail.gameObject, trail.time);
        Destroy(impactObj, impactObj.GetComponent<ParticleSystem>().main.duration);
    }

    private void SpawnImpactEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        GameObject impactObj = Instantiate(impactPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
        Destroy(impactObj, impactObj.GetComponent<ParticleSystem>().main.duration);
    }
    #endregion

    private void SetupCrosshair()
    {
        // Create a new canvas for the crosshair
        GameObject canvasObject = new GameObject("CrosshairCanvas");
        crosshairCanvas = canvasObject.AddComponent<Canvas>();
        crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        // Create a RawImage for the crosshair
        GameObject crosshairObject = new GameObject("Crosshair");
        crosshairObject.transform.SetParent(canvasObject.transform, false);
        crosshairImage = crosshairObject.AddComponent<RawImage>();
        crosshairImage.texture = crosshairTexture;
        crosshairImage.rectTransform.sizeDelta = crosshairSize;
        crosshairImage.color = crosshairColor;

        // Set the crosshair to be centered initially
        crosshairImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairImage.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void DealDamage(RaycastHit hit)
    {
        if (hit.collider.CompareTag("Player"))
        {
            PlayerController playerController = hit.collider.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(Damage);
                Debug.Log($"Dealt {Damage} damage to player");
            }
        }
    }

    public void UpdateAimInfo(RaycastHit aimInfo)
    {
        // if (!IsOwner) return;
        currentAim = aimInfo;
        // Perform any weapon-specific aiming logic here
    }
}
