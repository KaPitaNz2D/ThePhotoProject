using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineRotationComposer))]
public class CameraShoulderSwitch : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference switchShoulderInput;

    [Header("Settings")]
    public float ShoulderOffset = 0.1f;
    public float switchSpeed = 8f;

    private CinemachineRotationComposer rotationComposer;
    private bool isRightShoulder = true;
    private float targetX;

    private void Awake()
    {
        rotationComposer = GetComponent<CinemachineRotationComposer>();
        targetX = -ShoulderOffset;
    }

    private void OnEnable()
    {
        // Subscribe to input action event
        if (switchShoulderInput != null)
        {
            switchShoulderInput.action.Enable();
            switchShoulderInput.action.performed += OnSwitchShoulder;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from input action event to prevent memory leaks
        if (switchShoulderInput != null)
        {
            switchShoulderInput.action.performed -= OnSwitchShoulder;
        }
    }

    // Toggle target shoulder offset on input trigger
    private void OnSwitchShoulder(InputAction.CallbackContext ctx)
    {
        isRightShoulder = !isRightShoulder;
        targetX = isRightShoulder ? ShoulderOffset : -ShoulderOffset;
    }

    private void Update()
    {
        if (rotationComposer == null) return;

        Vector2 pos = rotationComposer.Composition.ScreenPosition;

        // Smoothly interpolate or instantly snap ScreenPosition.x toward target shoulder side
        if (switchSpeed <= 0f)
        {
            pos.x = targetX;
        }
        else
        {
            pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * switchSpeed);
        }

        rotationComposer.Composition.ScreenPosition = pos;
    }
}