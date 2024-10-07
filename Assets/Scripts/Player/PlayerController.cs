using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float maxHealth = 100;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private LayerMask aimMask;

    private Camera mainCam;
    private Rigidbody rb;
    private Vector3 movement;
    private RaycastHit hit;
    private WeaponController weaponController;
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>();

    void Start()
    {
        mainCam = Camera.main;
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
        weaponController = GetComponent<WeaponController>();
    }

    void Update()
    {
        if (!IsOwner) return;
        // Get input from the horizontal and vertical axes
        float moveHorizontal = Input.GetAxisRaw("Horizontal");
        float moveVertical = Input.GetAxisRaw("Vertical");

        // Calculate movement direction
        movement = new Vector3(moveHorizontal, 0, moveVertical).normalized;

        // Handle player aiming
        Aim();

        // Smooth rotation towards movement direction
        if (movement != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    // Called every physics update
    void FixedUpdate()
    {
        Move();
    }

    #region HealthFunctions
    public override void OnNetworkSpawn() // Called when the player is spawned
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
        currentHealth.OnValueChanged += OnHealthChanged;
        Debug.Log($"Player {OwnerClientId} spawned with health {currentHealth.Value}");
    }

    public override void OnNetworkDespawn() // Called when the player is despawned
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
        base.OnNetworkDespawn();
        Debug.Log($"Player {OwnerClientId} despawned");
    }

    private void OnHealthChanged(float previousValue, float newValue) // Called when the player's health changes
    {
        // Handle health change events (e.g., update UI)
        Debug.Log($"Player {OwnerClientId} health changed from {previousValue} to {newValue}");
    }

    [ServerRpc(RequireOwnership = false)] // This is a server-side RPC that can be called by any client.
    public void TakeDamageServerRpc(float damage) // Called when the player takes damage
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    [ClientRpc]
    private void UpdateHealthClientRpc(int newHealth)
    {
        // This method is called on all clients to update the health
        Debug.Log($"Player {OwnerClientId} health updated to {newHealth}");
    }

    private void Die()
    {
        Debug.Log($"Player {OwnerClientId} has died!");
        Destroy(gameObject);
    }
    #endregion

    #region MovementFunctions
    void Move()
    {
        // Smooth player movement
        Vector3 newPosition = rb.position + movement * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
    }
    #endregion

    #region AimFunctions
    void Aim()
    {
        var (success, hitInfo) = GetMousePosition();
        hit = hitInfo;

        if (success)
        {
            // Calculate direction towards the hit point
            Vector3 direction = hit.point - transform.position;
            direction.y = 0;  // Keep aiming plane horizontal

            // Smooth rotation towards aim direction
            transform.forward = direction.normalized;

            // Inform the weapon about the aim
            if (weaponController != null)
            {
                weaponController.UpdateAimInfo(hitInfo);
            }
        }
    }

    (bool success, RaycastHit hitInfo) GetMousePosition()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, aimMask))
        {
            return (true, hitInfo);
        }
        return (false, new RaycastHit());
    }
    #endregion

    #region Getters
    public float GetCurrentHealth()
    {
        return currentHealth.Value;
    }

    public RaycastHit GetHit()
    {
        return hit;
    }
    #endregion
}
