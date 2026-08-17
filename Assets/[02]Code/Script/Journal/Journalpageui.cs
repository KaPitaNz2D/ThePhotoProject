using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// แสดงผล "1 หน้ากระดาษ" ของสมุดบันทึก — เอาไปแปะ 2 ชุด (ซ้าย/ขวา) ใต้ JournalManager
/// ตัวนี้ไม่รู้เรื่อง Logic การปลดล็อกเลย แค่รับข้อมูลมาโชว์ตามที่ JournalManager สั่งเท่านั้น
/// </summary>
public class JournalPageUI : MonoBehaviour
{
    [Header("UI References")]
    public Image photoImage;
    public Image silhouetteImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    [Tooltip("Overlay ปิดทับตอนยังไม่ปลดล็อก เช่นกรอบมืดๆ หรือคำว่า \"ยังไม่ค้นพบ\" (ใส่หรือไม่ใส่ก็ได้)")]
    public GameObject lockedOverlay;

    private Sprite runtimePhotoSprite; // เก็บไว้ Destroy ตอนเปลี่ยนหน้า กัน Memory leak จาก Sprite ที่สร้างรันไทม์

    /// <summary>โชว์หน้าแบบยังไม่ปลดล็อก (เห็นแค่เงา/คำใบ้)</summary>
    public void ShowLocked(JournalEntry entry)
    {
        gameObject.SetActive(true);

        nameText.text = "???";
        descriptionText.text = "ยังไม่เคยถ่ายรูปสิ่งมีชีวิตนี้";

        if (photoImage != null) photoImage.gameObject.SetActive(false);

        if (silhouetteImage != null)
        {
            silhouetteImage.sprite = entry.silhouette;
            silhouetteImage.gameObject.SetActive(entry.silhouette != null);
        }

        if (lockedOverlay != null) lockedOverlay.SetActive(true);

        ClearRuntimeSprite();
    }

    /// <summary>โชว์หน้าแบบปลดล็อกแล้ว (มีรูปจริงที่ถ่ายมา)</summary>
    public void ShowUnlocked(JournalEntry entry, Texture2D photo)
    {
        gameObject.SetActive(true);

        nameText.text = entry.displayName;
        descriptionText.text = entry.description;

        if (silhouetteImage != null) silhouetteImage.gameObject.SetActive(false);
        if (lockedOverlay != null) lockedOverlay.SetActive(false);

        if (photoImage != null && photo != null)
        {
            ClearRuntimeSprite();
            runtimePhotoSprite = Sprite.Create(
                photo,
                new Rect(0, 0, photo.width, photo.height),
                new Vector2(0.5f, 0.5f)
            );
            photoImage.sprite = runtimePhotoSprite;
            photoImage.gameObject.SetActive(true);
        }
    }

    /// <summary>ซ่อนหน้านี้ไปเลย เผื่อจำนวนสิ่งมีชีวิตในสมุดเป็นเลขคี่ (หน้าสุดท้ายไม่มีคู่)</summary>
    public void ShowEmpty()
    {
        gameObject.SetActive(false);
        ClearRuntimeSprite();
    }

    private void ClearRuntimeSprite()
    {
        if (runtimePhotoSprite != null)
        {
            Destroy(runtimePhotoSprite);
            runtimePhotoSprite = null;
        }
    }
}