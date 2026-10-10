using UnityEngine.InputSystem;

/// <summary>
/// หา Input Action ของกล้องถ่ายรูปจากชื่อ ถ้าไม่ได้ลากใส่ช่อง Inspector ไว้
/// ใช้ Input Action ตัวอื่นที่อยู่ใน Asset เดียวกัน (anchor เช่น zoomInput ที่ผูกไว้ใน Prefab อยู่แล้ว) เป็นทางไปหา InputActionAsset
/// ทำให้ Action ที่เพิ่มใหม่ใน PlayerControls.inputactions ใช้งานได้เลยโดยไม่ต้องไปลากใส่ Prefab ก่อน
/// (ถ้าลากใส่ช่อง Inspector ไว้ ใช้ตัวนั้นก่อนเสมอ)
/// </summary>
public static class PhotoInputLookup
{
    /// <param name="explicitRef">ตัวที่ลากใส่ Inspector (ถ้ามี ใช้ตัวนี้)</param>
    /// <param name="anchor">Reference ใดก็ได้ที่อยู่ใน InputActionAsset เดียวกัน ใช้หา Asset</param>
    /// <param name="actionPath">เช่น "Photograph/PushUpCamera"</param>
    public static InputAction Resolve(InputActionReference explicitRef, InputActionReference anchor, string actionPath)
    {
        if (explicitRef != null) return explicitRef.action;

        InputAction anchorAction = anchor != null ? anchor.action : null;
        InputActionAsset asset = anchorAction?.actionMap?.asset;
        return asset != null ? asset.FindAction(actionPath, false) : null;
    }
}
