using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

// ponytail: playtest-only catch-all, any key or left click advances the line (same effect as
// the built-in Space binding). Duplicate-fires with Space specifically since Space is also
// "any key" - harmless for now, disable the LineAdvancer's KeyCodes component if that matters.
[RequireComponent(typeof(LineAdvancer))]
public class DialogueAnyKeyAdvance : MonoBehaviour
{
    private LineAdvancer lineAdvancer;

    private void Awake()
    {
        lineAdvancer = GetComponent<LineAdvancer>();
    }

    private void Update()
    {
        bool pressed = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (pressed)
        {
            lineAdvancer.OnInputHurryUpLines();
        }
    }
}
