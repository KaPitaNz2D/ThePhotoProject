using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover preset ตาม UI Designer: เอาเมาส์วางค้างบนปุ่มแล้วปุ่ม "นูนขึ้น" (ขยายนิดๆ แบบนุ่มๆ) เอาออกแล้วหดกลับ
/// แปะคู่กับ Button (สีจางลงตอนกดใช้ ColorBlock ของ Button เอง) หรือของที่คลิกได้อย่างอื่นก็ได้
/// ใช้ unscaledDeltaTime — ทำงานได้แม้เกมหยุดเวลา (timeScale = 0)
/// </summary>
public class UIHoverPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("ขนาดตอนเมาส์วางค้าง เทียบกับขนาดปกติ")]
    public float hoverScale = 1.08f;
    [Tooltip("ความเร็วขยาย/หด (ยิ่งมากยิ่งไว)")]
    public float speed = 14f;

    private Vector3 baseScale;
    private bool hovered;
    private Selectable selectable;

    private void Awake()
    {
        baseScale = transform.localScale;
        selectable = GetComponent<Selectable>();
    }

    private void OnDisable()
    {
        // ปิดหน้าไปตอนเมาส์ค้างอยู่ -> เปิดกลับมาต้องไม่ค้างขนาดใหญ่
        hovered = false;
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;

    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void Update()
    {
        // ปุ่มที่กดไม่ได้ (interactable = false) ไม่นูน
        bool pop = hovered && (selectable == null || selectable.IsInteractable());
        Vector3 target = pop ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }
}
