using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCam : MonoBehaviour
{
    [Header("References")]
    public Transform orientation; // Movement directional reference based on camera
    public Transform player;      // Reference to the main player GameObject
    public Transform playerObj;   // Visual mesh that rotates towards movement direction

    public float rotationSpeed = 7f;

    [Header("Input References")]
    public InputActionReference moveInput;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (moveInput != null) moveInput.action.Enable();
    }

    private void Update()
    {
        if (player == null || orientation == null || playerObj == null || moveInput == null) return;

        // Use the camera's actual look direction instead of camera->player position vector,
        // since shoulder cam offsets the camera sideways and breaks the position-based vector
        Vector3 camForward = transform.forward;
        camForward.y = 0;
        orientation.forward = camForward.normalized;

        // Map 2D input (WASD) relative to orientation's forward and right vectors
        Vector2 inputVector = moveInput.action.ReadValue<Vector2>();
        Vector3 moveDir = orientation.forward * inputVector.y + orientation.right * inputVector.x;

        // Smoothly rotate visual mesh to face the movement direction
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            playerObj.forward = Vector3.Slerp(playerObj.forward, moveDir.normalized, Time.deltaTime * rotationSpeed);
        }
    }
}