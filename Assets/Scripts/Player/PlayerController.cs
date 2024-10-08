using System.Collections;
using UnityEngine;
using Mirror;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private LayerMask aimMask;

    private CameraController cameraController;
    private Camera playerCamera;
    private Rigidbody rb;
    private Vector3 movement;
    private RaycastHit hit;
    private WeaponController weaponController;

    [SyncVar(hook = nameof(OnHealthChanged))]
    [SerializeField] private float currentHealth = 100f;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Debug.Log($"Local Player {netId} Started");
        cameraController = gameObject.GetComponent<CameraController>();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        weaponController = GetComponent<WeaponController>();

        if (!isLocalPlayer) return;
        StartCoroutine(WaitForCamera());
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        Aim();
    }

    // Called every physics update
    void FixedUpdate()
    {
        if (!isLocalPlayer) return;
        Move();
    }

    private void Move()
    {
        // handle keyboard inputs
        float moveHorizontal = Input.GetAxisRaw("Horizontal");
        float moveVertical = Input.GetAxisRaw("Vertical");
        Vector3 movement = new Vector3(moveHorizontal, 0, moveVertical).normalized;

        // Move
        Vector3 newPosition = rb.position + movement * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);

        // Rotate
        if (movement != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    #region Aim Functions
    private void Aim()
    {
        var (success, hitInfo) = GetMousePosition();
        if (success)
        {
            Vector3 direction = (hitInfo.point - transform.position).normalized;
            transform.forward = direction;
            hit = hitInfo; // update the hit info
            weaponController?.UpdateAimInfo(hitInfo); // update the aim info
        }
    }

    private (bool success, RaycastHit hitInfo) GetMousePosition()
    {
        if (playerCamera == null) return (false, new RaycastHit());

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, aimMask))
        {
            return (true, hitInfo);
        }
        return (false, new RaycastHit());
    }
    #endregion

    #region Health Functions
    private void OnHealthChanged(float oldValue, float newValue)
    {
        Debug.Log($"Player {netId} health changed from {oldValue} to {newValue}");
    }

    [Command(requiresAuthority = false)]
    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthClientRpc(currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    [ClientRpc]
    private void UpdateHealthClientRpc(float newHealth)
    {
        Debug.Log($"Player {netId} health updated to {newHealth}");
    }

    private void Die()
    {
        Debug.Log($"Player {netId} has died!");
        NetworkServer.Destroy(gameObject);
    }
    #endregion

    private IEnumerator WaitForCamera()
    {
        while (cameraController.GetPlayerCamera() == null)
        {
            yield return null;
        }
        playerCamera = cameraController.GetPlayerCamera();
        // Any other setup that depends on the camera being ready
    }

    public float GetCurrentHealth() => currentHealth;
    public RaycastHit GetAimHit() => hit;
}
