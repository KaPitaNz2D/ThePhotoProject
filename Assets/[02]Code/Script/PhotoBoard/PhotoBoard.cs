using System;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// แปะไว้ที่ Prefab บอร์ดแขวนรูปในโลก 3D — เก็บได้สูงสุด 5 รูป ต่อบอร์ด 1 ตัว (วางได้หลายบอร์ดในฉาก แต่ละบอร์ดจำรูปของตัวเองแยกกัน)
///
/// รูปที่แขวนยังเป็นของ PhotoStorage เหมือนเดิม (แค่ "อ้างอิง" ไป ไม่ย้าย/ไม่ลบออกจาก Storage) แขวนรูปเดียวกันได้หลายบอร์ด
/// ถ้ารูปถูกลบออกจาก Storage จากที่อื่น (เช่นหน้า Storage UI) ช่องที่แขวนรูปนั้นอยู่จะว่างอัตโนมัติ
/// รูปที่แขวน (ชื่อไฟล์ใน Storage ต่อช่อง) เซฟลง Save/boards.json ทุกครั้งที่แขวน/เอาออก แล้วแขวนกลับคืนตอน Start
///
/// ช่องที่ยังไม่มีรูป = ปิด Renderer ไว้เฉยๆ (ไม่มีอะไรขึ้นบนบอร์ด) ต้องลาก Renderer ของกรอบรูปแต่ละช่องมาใส่ไว้ใน slots ที่ Inspector
/// </summary>
public class PhotoBoard : MonoBehaviour
{
    [Serializable]
    public class BoardSlot
    {
        [Tooltip("Renderer ของกรอบรูปช่องนี้ในโลก 3D (เช่น Quad ที่แปะ Texture รูปที่แขวน) — ปิดไว้เองตอนช่องว่าง")]
        public Renderer displayRenderer;

        [NonSerialized] public PhotoStorage.StoredPhoto hungPhoto;
        [NonSerialized] public Texture2D loadedTexture; // PhotoBoard เป็นเจ้าของ ต้อง Destroy เองตอนเปลี่ยน/ว่าง/ทำลายบอร์ด
    }

    [Tooltip("ช่องแขวนรูปของบอร์ดนี้ (ปกติ 5 ช่อง) ลาก Renderer ของแต่ละกรอบมาใส่ให้ครบ")]
    public BoardSlot[] slots = new BoardSlot[5];

    [Tooltip("กล้องที่ซูมมาเล็งหน้าบอร์ดนี้ตอนกด Interact (CinemachineCamera ลูกของบอร์ดนี้เอง — แต่ละบอร์ดจัดมุมกล้องของตัวเองได้อิสระ) ปล่อยว่างได้ถ้าไม่ต้องการตัดกล้อง")]
    public CinemachineCamera boardCam;

    [Tooltip("ชื่อที่ใช้เซฟรูปที่แขวนของบอร์ดนี้ (Save/boards.json) — ปล่อยว่างได้ จะใช้ path ใน Hierarchy แทน " +
             "ตั้งเองถ้ามีหลายบอร์ดชื่อซ้ำกัน หรือกลัวย้าย/เปลี่ยนชื่อใน Hierarchy แล้วรูปที่เซฟไว้หาย")]
    public string saveId;

    public int SlotCount => slots.Length;

    private const string SaveFileName = "boards.json";

    [Serializable]
    private class BoardSaveData
    {
        public System.Collections.Generic.List<BoardRecord> boards = new System.Collections.Generic.List<BoardRecord>();
    }

    [Serializable]
    private class BoardRecord
    {
        public string boardId;
        public string[] slotFileNames; // ชื่อไฟล์ภาพใน PhotoStorage ต่อช่อง ("" = ช่องว่าง)
    }

    private string BoardId
    {
        get
        {
            if (!string.IsNullOrEmpty(saveId)) return saveId;
            string path = name;
            for (Transform t = transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return gameObject.scene.name + ":" + path;
        }
    }

    private void Start()
    {
        // แขวนรูปที่เซฟไว้กลับคืน (PhotoStorage โหลดรายชื่อภาพเสร็จตั้งแต่ Awake แล้ว) ภาพที่ถูกลบไปแล้วจะข้าม
        BoardRecord record = LoadData().boards.Find(b => b.boardId == BoardId);
        if (record?.slotFileNames == null || PhotoStorage.Instance == null) return;

        for (int i = 0; i < slots.Length && i < record.slotFileNames.Length; i++)
        {
            PhotoStorage.StoredPhoto photo = PhotoStorage.Instance.FindByFileName(record.slotFileNames[i]);
            if (photo != null) ShowPhoto(i, photo);
        }
    }

    private static BoardSaveData LoadData() => SaveSystem.Load<BoardSaveData>(SaveFileName) ?? new BoardSaveData();

    private void SaveToDisk()
    {
        BoardSaveData data = LoadData();
        string id = BoardId;
        data.boards.RemoveAll(b => b.boardId == id);

        string[] fileNames = new string[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            fileNames[i] = slots[i].hungPhoto != null ? System.IO.Path.GetFileName(slots[i].hungPhoto.filePath) : "";
        }
        data.boards.Add(new BoardRecord { boardId = id, slotFileNames = fileNames });
        SaveSystem.Save(SaveFileName, data);
    }

    private void OnEnable()
    {
        // ช่องว่างตอนเริ่มต้องไม่โชว์อะไร
        for (int i = 0; i < slots.Length; i++) SetSlotVisible(slots[i], false);

        if (PhotoStorage.Instance != null) PhotoStorage.Instance.OnStorageChanged += HandleStorageChanged;
    }

    private void OnDisable()
    {
        if (PhotoStorage.Instance != null) PhotoStorage.Instance.OnStorageChanged -= HandleStorageChanged;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < slots.Length; i++) ReleaseSlotTexture(slots[i]);
    }

    public PhotoStorage.StoredPhoto GetSlotPhoto(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return null;
        return slots[slotIndex].hungPhoto;
    }

    /// <summary>แขวนรูปลงช่องนี้ (แทนที่รูปเดิมถ้ามี) — โหลดภาพเต็มจากดิสก์มาแปะบนบอร์ดจริงในโลก 3D ด้วย</summary>
    public void PlacePhoto(int slotIndex, PhotoStorage.StoredPhoto photo)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length || photo == null || PhotoStorage.Instance == null) return;

        ShowPhoto(slotIndex, photo);
        SaveToDisk();
    }

    private void ShowPhoto(int slotIndex, PhotoStorage.StoredPhoto photo)
    {
        BoardSlot slot = slots[slotIndex];
        ReleaseSlotTexture(slot);

        slot.hungPhoto = photo;
        slot.loadedTexture = PhotoStorage.Instance.LoadPhotoTexture(photo);

        if (slot.displayRenderer != null && slot.loadedTexture != null)
            slot.displayRenderer.material.mainTexture = slot.loadedTexture;
        SetSlotVisible(slot, slot.loadedTexture != null);
    }

    /// <summary>เอารูปออกจากช่องนี้ (ไม่กระทบ Storage — รูปยังอยู่ที่นั่นตามเดิม)</summary>
    public void RemovePhoto(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        BoardSlot slot = slots[slotIndex];
        ReleaseSlotTexture(slot);
        slot.hungPhoto = null;
        SetSlotVisible(slot, false);
        SaveToDisk();
    }

    // เปิด/ปิดตัวรูป + ทุกอย่างที่เป็นลูก (กรอบขาวแบบรูปปริ้นท์) ไปพร้อมกัน
    private static void SetSlotVisible(BoardSlot slot, bool visible)
    {
        if (slot?.displayRenderer == null) return;
        foreach (Renderer r in slot.displayRenderer.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
    }

    private void ReleaseSlotTexture(BoardSlot slot)
    {
        if (slot == null) return;
        if (slot.loadedTexture != null) Destroy(slot.loadedTexture);
        slot.loadedTexture = null;
    }

    // รูปที่แขวนอยู่ถูกลบออกจาก Storage จากที่อื่น -> ช่องนี้ต้องว่างตาม
    private void HandleStorageChanged()
    {
        if (PhotoStorage.Instance == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            PhotoStorage.StoredPhoto hung = slots[i].hungPhoto;
            if (hung == null) continue;

            if (!StillExistsInStorage(hung)) RemovePhoto(i);
        }
    }

    private static bool StillExistsInStorage(PhotoStorage.StoredPhoto photo)
    {
        var photos = PhotoStorage.Instance.Photos;
        for (int i = 0; i < photos.Count; i++)
        {
            if (photos[i] == photo) return true;
        }
        return false;
    }
}
