using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class WeaponController : NetworkBehaviour
{
    // --- Audio ---
    public AudioClip GunShotClip;
    public AudioSource source;
    public Vector2 audioPitch = new Vector2(.9f, 1.1f);

    // --- Crosshair ---
    public RawImage cursorImage;  // Reference to the UI image that represents the custom cursor
    public Vector2 cursorOffset;  // Offset to adjust cursor position relative to the mouse position
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
    [SerializeField] private float timeLastFired;

    // --- Configs ---
    [SerializeField] private float damage = 10f; // Amount of damage this weapon deals
    private NetworkVariable<float> netDamage = new NetworkVariable<float>(10f);
    private NetworkVariable<float> netTimeLastFired = new NetworkVariable<float>(0f);
    public float Damage
    {
        get => netDamage.Value;
        set => netDamage.Value = value;
    }

    // --- Gizmo Variables ---
    private Vector3 lastShootPosition;
    private Vector3 lastShootDirection;
    private RaycastHit lastHit;

    void Start()
    {
        if (source != null) source.clip = GunShotClip;

        netDamage = new NetworkVariable<float>(damage);

        GameObject cursorObject = GameObject.Find("Crosshair");
        if (cursorObject != null)
        {
            cursorImage = cursorObject.GetComponent<RawImage>();
        }

        if (cursorImage == null)
        {
            Debug.LogError("Cursor RawImage with name '" + "Crosshair" + "' not found in the scene!");
        }
    }

    void Update()
    {
        // Crosshair
        if (cursorImage != null)
        {
            Vector2 mousePosition = Input.mousePosition;
            cursorImage.rectTransform.position = mousePosition + cursorOffset;
        }

        if (IsOwner && Input.GetMouseButton(0) && (Time.time - timeLastFired > shootDelay))
        {
            FireWeaponServerRpc();
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
    [ServerRpc]
    void FireWeaponServerRpc()
    {
        // Server-side logic
        Vector3 shootDirection = GetShootDirection();
        lastShootDirection = shootDirection;
        lastShootPosition = bulletSpawnPoint.position;

        if (Physics.Raycast(bulletSpawnPoint.position, shootDirection, out RaycastHit hit, shootableMask))
        {
            lastHit = hit;  // Save the hit point for gizmo visualization
            DealDamage(hit);
        }

        // Trigger client-side effects
        FireWeaponClientRpc();

        // Update last fire time
        timeLastFired = Time.time;
    }

    [ClientRpc]
    private void FireWeaponClientRpc()
    {
        PlayShootEffect();
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

        // Play audio
        PlayWeaponAudio();

        // Spawn bullet trail
        SpawnBulletTrail();
    }

    private void ReEnableDisabledProjectile()
    {
        projectileToDisableOnFire.SetActive(true);
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

    private void SpawnBulletTrail()
    {
        Vector3 shootDirection = GetShootDirection();
        if (Physics.Raycast(bulletSpawnPoint.position, shootDirection, out RaycastHit hit, shootableMask))
        {
            TrailRenderer trail = Instantiate(bulletTrail, bulletSpawnPoint.position, Quaternion.identity);
            StartCoroutine(SpawnTrail(trail, hit));
        }
    }

    private IEnumerator SpawnTrail(TrailRenderer trail, RaycastHit hit)
    {
        float time = 0;
        Vector3 startPosition = trail.transform.position;
        while (time < 1)
        {
            trail.transform.position = Vector3.Lerp(startPosition, hit.point, time);
            time += Time.deltaTime / trail.time;
            yield return null;
        }
        trail.transform.position = hit.point;
        GameObject impactObj = Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        Destroy(trail.gameObject, trail.time);
        Destroy(impactObj, impactObj.GetComponent<ParticleSystem>().main.duration);
    }
    #endregion

    private void DealDamage(RaycastHit hit)
    {
        if (hit.collider.CompareTag("Player"))
        {
            PlayerController playerController = hit.collider.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamageServerRpc(damage);
                Debug.Log($"Dealt {damage} damage to player");
            }
        }
    }

    public void UpdateAimInfo(RaycastHit aimInfo)
    {
        currentAim = aimInfo;
        // Perform any weapon-specific aiming logic here
    }
}
