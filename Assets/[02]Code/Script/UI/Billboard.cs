using UnityEngine;

// หันตามกล้องตลอด ใช้กับ UI ใน World Space เช่นป้าย "กด E" เหนือหัว NPC
public class Billboard : MonoBehaviour
{
    [Tooltip("กล้องที่จะหันตาม ถ้าไม่ใส่ไว้จะใช้ Camera.main ให้เอง")]
    public Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        transform.rotation = targetCamera.transform.rotation;
    }
}
