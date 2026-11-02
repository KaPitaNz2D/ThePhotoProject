using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// สัญลักษณ์เหนือหัว NPC บอกว่าไปคุยแล้วจะได้อะไร — โชว์เมื่อผู้เล่นอยู่ในระยะ showRange (~10 ม.)
///   Task  (!)  : มีเควสให้รับ หรือถ่ายรูปที่เควสต้องการมาแล้ว (พร้อมส่ง)
///   Other (?)  : ถ่ายรูปสิ่งอื่นที่ไม่ใช่เควสมา และ NPC ยังไม่เคยเล่าเรื่องนั้นให้ฟัง
///   Talk  (…)  : ไม่มีอะไรข้างบน — แค่คุยเล่น
/// เข้ามาในระยะคุยได้ (NPCInteractable.interactRange) จะเปลี่ยนเป็นวงกลมขาวบอกปุ่มที่ใช้คุย (เช่น "E" อ่านจาก Binding ของ interactInput)
/// แล้วไอคอนสถานะย่อไปอยู่ในวงกลมเล็กมุมขวาบนของวงกลมนั้น
/// ซ่อนทั้งหมดตอนไม่ใช่ SystemState.Normal (กำลังคุย / ถ่ายรูป / เปิด UI อื่น)
///
/// เงื่อนไขต้องตรงกับ Router ใน NPC_Placeholder.yarn: เควสไล่ตามลำดับ questChain (เควสถัดไปเปิดเมื่อเควสก่อนหน้า Completed)
/// และ otherTopicIds = สิ่งที่ Node Discoveries มีบทพูดให้ (ถ่ายอย่างอื่นนอกลิสต์ NPC ไม่มีอะไรจะเล่า เลยไม่ขึ้น ?)
/// </summary>
public class NPCStatusIndicator : MonoBehaviour
{
    private enum Status { Hidden, Task, Talk, Other }

    [Header("Logic (ต้องตรงกับ Yarn)")]
    [Tooltip("ใช้ระยะคุย (interactRange) + ตำแหน่งผู้เล่นจากตัวนี้ — ไม่ใส่จะหาจาก GameObject เดียวกัน")]
    public NPCInteractable npc;
    [Tooltip("creatureId ของเควสเรียงตามลำดับที่ NPC ให้ (เควสถัดไปเปิดเมื่อเควสก่อนหน้า Completed)")]
    public List<string> questChain = new List<string> { "deer_01", "moose_01" };
    [Tooltip("creatureId ที่ NPC มีเรื่องเล่าให้ฟังเมื่อถ่ายรูปมา (Node Discoveries ใน Yarn)")]
    public List<string> otherTopicIds = new List<string> { "Chan", "FlyAgaric", "Honey", "Turkey", "puffball" };
    [Tooltip("ระยะที่เริ่มเห็นสัญลักษณ์ (เมตร)")]
    public float showRange = 10f;

    [Header("UI")]
    public GameObject root;
    [Tooltip("วงกลมบอกปุ่มกดคุย — โชว์เฉพาะตอนอยู่ในระยะคุย")]
    public GameObject keyPrompt;
    [Tooltip("ตัวอักษรปุ่มในวงกลม — ตั้งจาก Binding ของ NPCInteractable.interactInput ให้เอง")]
    public TMP_Text keyLabel;
    [Tooltip("วงกลมเล็กมุมขวาบนที่รองไอคอนสถานะ ตอนอยู่ในระยะคุย")]
    public GameObject iconBadge;
    public Image icon;
    public Sprite taskIcon;
    public Sprite talkIcon;
    public Sprite otherIcon;
    [Tooltip("ตำแหน่ง/ขนาดไอคอนตอนอยู่ไกล (ไอคอนเดี่ยว)")]
    public Vector2 farIconPosition = Vector2.zero;
    public float farIconScale = 1f;
    [Tooltip("ตอนอยู่ในระยะคุย ไอคอนย้ายไปกลาง iconBadge และย่อให้ด้านยาวเท่ากับสัดส่วนนี้ของขนาดวงกลมเล็ก\n(ย่อ/ย้าย iconBadge ใน Prefab ได้เลย ไอคอนตามไปเอง)")]
    [Range(0.3f, 1f)] public float badgeIconFill = 0.7f;

    private Status shownStatus = Status.Hidden;
    private bool shownNear;

    private void Awake()
    {
        if (npc == null) npc = GetComponent<NPCInteractable>();
        if (root != null) root.SetActive(false);

        // ตัวอักษรปุ่มตาม Binding จริง (เปลี่ยนปุ่มใน Input Actions แล้วป้ายเปลี่ยนตาม)
        // อ่านจาก Path ไม่ใช่ GetBindingDisplayString — อันนั้นแปลตามภาษาคีย์บอร์ดที่เปิดอยู่ (คีย์บอร์ดไทยปุ่ม E กลายเป็น "ำ")
        if (keyLabel != null && npc != null && npc.interactInput != null && npc.interactInput.action.bindings.Count > 0)
        {
            string key = InputControlPath.ToHumanReadableString(npc.interactInput.action.bindings[0].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            if (!string.IsNullOrEmpty(key)) keyLabel.text = key.ToUpperInvariant();
        }
    }

    private void Update()
    {
        Transform player = npc != null ? npc.player : null;
        bool normal = StateManager.Instance == null || StateManager.Instance.CurrentSystemState == StateManager.SystemState.Normal;
        float distance = player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;

        Status status = normal && distance <= showRange ? EvaluateStatus() : Status.Hidden;
        bool near = npc != null && distance <= npc.interactRange;
        if (status == shownStatus && near == shownNear) return;

        shownStatus = status;
        shownNear = near;
        Refresh();
    }

    private Status EvaluateStatus()
    {
        QuestManager quests = QuestManager.Instance;
        if (quests == null) return Status.Talk;

        // ไล่เควสตามลำดับ: เจอเควสแรกที่ยังไม่ Completed แล้วหยุด (เควสถัดไปยังไม่เปิดจนกว่าเควสนี้จะเสร็จ)
        foreach (string questId in questChain)
        {
            QuestManager.QuestStatus questStatus = quests.GetStatus(questId);
            if (questStatus == QuestManager.QuestStatus.Completed) continue;
            if (questStatus == QuestManager.QuestStatus.NotStarted) return Status.Task;   // มีเควสให้รับ
            if (QuestManager.HasPhoto(questId)) return Status.Task;                        // Active + ถ่ายมาแล้ว = พร้อมส่ง
            break;                                                                        // Active แต่ยังไม่ได้ถ่าย
        }

        foreach (string topicId in otherTopicIds)
        {
            if (QuestManager.HasPhoto(topicId) && !quests.IsInformed(topicId)) return Status.Other;
        }
        return Status.Talk;
    }

    private void Refresh()
    {
        if (root == null) return;
        root.SetActive(shownStatus != Status.Hidden);
        if (shownStatus == Status.Hidden) return;

        if (keyPrompt != null) keyPrompt.SetActive(shownNear);
        if (iconBadge != null) iconBadge.SetActive(shownNear);
        if (icon != null)
        {
            icon.sprite = shownStatus == Status.Task ? taskIcon : shownStatus == Status.Other ? otherIcon : talkIcon;
            icon.SetNativeSize();
            if (shownNear && iconBadge != null) FitIconToBadge();
            else
            {
                icon.rectTransform.anchoredPosition = farIconPosition;
                icon.rectTransform.localScale = Vector3.one * farIconScale;
            }
        }
    }

    // วางไอคอนกลางวงกลมเล็ก ขนาดคิดจากขนาดจริงของวงกลม (sizeDelta x scale) — ปรับวงกลมใน Prefab แล้วไอคอนไม่หลุดตำแหน่ง
    // (ไอคอนไม่ได้เป็นลูกของวงกลม เพราะตอนอยู่ไกลต้องโชว์ไอคอนเดี่ยวโดยไม่มีวงกลม)
    private void FitIconToBadge()
    {
        RectTransform badge = (RectTransform)iconBadge.transform;
        float badgeSize = Mathf.Min(badge.rect.width * badge.localScale.x, badge.rect.height * badge.localScale.y);
        Vector2 nativeSize = icon.rectTransform.sizeDelta;
        float iconLongSide = Mathf.Max(nativeSize.x, nativeSize.y, 1f);

        icon.rectTransform.anchoredPosition = badge.anchoredPosition;
        icon.rectTransform.localScale = Vector3.one * (badgeSize * badgeIconFill / iconLongSide);
    }
}
