using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// เมนู Tools > Photo Project > Rebuild Journal UI — สร้างหน้าตาสมุด Journal ตาม Mockup ใน Player.prefab
/// แล้วผูก Reference ทั้งหมดเข้า JournalUI ให้เอง (journalManager / Input ของเดิมไม่แตะ)
///
/// ใช้ Sprite จากศิลปินใน [01]Art/Ui Elements/Journal (ปก/หน้ากระดาษ/แท็บ/โพลารอยด์/ไอคอน Contents/ช่องกริด/แถบ Task/Checkbox)
/// และตั้ง Import ของไฟล์เหล่านั้นเป็น Sprite ให้เอง — ชิ้นที่ยังไม่มี Sprite ใช้ของพื้นฐาน Unity เป็น Placeholder:
/// ลูกศรกาง/หุบ, ปุ่มเปลี่ยนรูป, กรอบรูปหน้า Detail, กรอบรูปหน้า Change Photos, จุดแดง Quest
/// ตำแหน่งทั้งหมดวัดจาก Mockup (จอ 1920x1080) เทียบกับกึ่งกลางหน้ากระดาษ (Book_3)
///
/// ระวัง: กดซ้ำ = ลบ JournalRootPanel ทั้งก้อนแล้วสร้างใหม่ ของที่แก้เองใน Prefab จะหายหมด
/// </summary>
public static class JournalUIBuilder
{
    private const string PlayerPrefabPath = "Assets/[03]Prefabs/Player.prefab";
    private const string PlayerVariantPath = "Assets/[03]Prefabs/Player_Code_Dev.prefab";
    private const string ArtFolder = "Assets/[01]Art/Ui Elements/Journal/";
    private const string CameraIconPath = "Assets/[01]Art/Ui Elements/Icon/UI_Cam.png";
    private const string FontPath = "Assets/[01]Art/Ui Elements/Font/Itim-Regular SDF.asset";

    // ขนาดตัวอักษรตาม UI Designer (ฟอนต์ Itim)
    private const float HeadFontSize = 42f;  // ชื่อหน้า (Tasks / Contents / ...)
    private const float TitleFontSize = 36f; // ชื่อบนแถบ (แถว Task / แถบชื่อหน้า Detail)
    private const float TextFontSize = 24f;  // เนื้อหา / รายละเอียด / ป้ายชื่อ

    // ตอนกด: สีจางลง (เมาส์วางค้าง = นูนขึ้น ทำโดย UIHoverPop ไม่ใช้สี)
    private static readonly Color PressedTint = new Color(1f, 1f, 1f, 0.55f);

    // สีของชิ้นที่ยังเป็น Placeholder + ตัวอักษร
    private static readonly Color InkColor = new Color32(0x4A, 0x30, 0x20, 0xFF);
    private static readonly Color BarColor = new Color32(0xDD, 0xC5, 0xA2, 0xFF);
    private static readonly Color BarBorderColor = new Color32(0xB8, 0x9A, 0x72, 0xFF);
    private static readonly Color PhotoFrameColor = new Color32(0x22, 0x20, 0x22, 0xFF);
    private static readonly Color ArrowColor = new Color32(0xD2, 0xBE, 0x98, 0xFF);
    private static readonly Color QuestDotColor = new Color32(0xD9, 0x5A, 0x5A, 0xFF);

    private static Sprite roundedSprite;
    private static Sprite circleSprite;
    private static Sprite arrowSprite;
    private static TMP_FontAsset font;
    private static int uiLayer;

    [MenuItem("Tools/Photo Project/Rebuild Journal UI")]
    public static void Build()
    {
        roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        arrowSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) Debug.LogWarning("[JournalUIBuilder] ไม่เจอฟอนต์ " + FontPath + " — ใช้ฟอนต์ตั้งต้นของ TMP แทน");

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            JournalUI ui = root.GetComponentInChildren<JournalUI>(true);
            if (ui == null)
            {
                Debug.LogError("[JournalUIBuilder] ไม่เจอ JournalUI ใน " + PlayerPrefabPath);
                return;
            }

            Transform canvas = ui.journalRootPanel != null ? ui.journalRootPanel.transform.parent : ui.transform.parent;
            uiLayer = canvas.gameObject.layer;
            if (ui.journalRootPanel != null) Object.DestroyImmediate(ui.journalRootPanel);

            BuildBook(ui, canvas);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        CleanVariantOverrides();
        AssetDatabase.SaveAssets();
        Debug.Log("[JournalUIBuilder] สร้าง Journal UI ใหม่ใน " + PlayerPrefabPath + " แล้ว");
    }

    // Player_Code_Dev (Variant) เคย Override ช่องใน JournalUI + เพิ่ม Text ใน DetailPanel เก่า — ล้างทิ้งให้ใช้ของ Base ตัวใหม่
    private static void CleanVariantOverrides()
    {
        GameObject variant = PrefabUtility.LoadPrefabContents(PlayerVariantPath);
        try
        {
            JournalUI ui = variant.GetComponentInChildren<JournalUI>(true);
            if (ui == null) return;

            Transform journal = ui.transform.parent;
            foreach (var added in PrefabUtility.GetAddedGameObjects(variant))
            {
                if (added.instanceGameObject != null && added.instanceGameObject.transform.IsChildOf(journal))
                    Object.DestroyImmediate(added.instanceGameObject);
            }
            PrefabUtility.RevertObjectOverride(ui, InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(variant, PlayerVariantPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(variant);
        }
    }

    // โหลด Sprite ของศิลปิน — ไฟล์ที่ยัง Import เป็น Default Texture จะถูกตั้งเป็น Sprite (Single, ไม่มี Mipmap) ให้ก่อน
    private static Sprite Art(string relativePath)
    {
        string path = ArtFolder + relativePath;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[JournalUIBuilder] ไม่เจอไฟล์ " + path + " — ใช้ Placeholder แทน");
            return null;
        }
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ==================== สมุด ====================
    private static void BuildBook(JournalUI ui, Transform canvas)
    {
        RectTransform rootPanel = NewRect("JournalRootPanel", canvas);
        Stretch(rootPanel, 0f, 0f, 0f, 0f);
        AddImage(rootPanel, new Color(0f, 0f, 0f, 0f), null); // โปร่งใส แค่กันคลิกทะลุไปโดนอย่างอื่น
        ui.journalRootPanel = rootPanel.gameObject;

        // Book = กึ่งกลางหน้ากระดาษ ทุกชิ้นวางเทียบจุดนี้
        RectTransform book = NewRect("Book", rootPanel);
        Place(book, new Vector2(-25f, 27f), Vector2.zero);

        // ลำดับวาด (หลังสุดอยู่บนสุด): ปุ่ม X (โคนซ่อนใต้ปก) -> ปก -> แท็บ (โคนซ่อนใต้หน้า) -> โพลารอยด์ -> แผ่นรองหน้า -> หน้ากระดาษ
        RectTransform close = MakeArtImage("CloseButton", book, new Vector2(280f, 398f), Art("Book Component/Key_Escape.png"), new Vector2(64f, 100f), Color.white);
        ui.closeButton = AddButton(close);

        MakeArtImage("Cover", book, new Vector2(21f, -4f), Art("Book Component/Book_1.png"), new Vector2(682f, 806f), new Color32(0x4A, 0x6A, 0x9A, 0xFF));

        ui.tasksTab = MakeTab("TasksTab", "Key_Tasks.png", "Tasks", book, 275f);
        ui.contentsTab = MakeTab("ContentsTab", "Key_Content.png", "Contents", book, 199f);
        ui.plantsTab = MakeTab("PlantsTab", "Key_Plant.png", "Plants", book, 123f);
        ui.animalsTab = MakeTab("AnimalsTab", "Key_Animal.png", "Animals", book, 47f);

        // รูปโพลารอยด์ซ้อนกันโผล่มุมขวาล่าง (โคนอยู่ใต้หน้ากระดาษ)
        MakePolaroid("Polaroid_Back", "Photo_2.png", book, new Vector2(370f, -323f), new Vector2(288f, 302f), -30f);
        MakePolaroid("Polaroid_Front", "Photo_1.png", book, new Vector2(322f, -364f), new Vector2(223f, 246f), 12f);

        MakeArtImage("PageBacking", book, new Vector2(10f, -8f), Art("Book Component/Book_2.png"), new Vector2(658f, 785f), new Color32(0x80, 0x70, 0x60, 0xFF));
        // Book_3 มีห่วงสันสมุดกับไอคอนกล้องวาดไว้ในรูปแล้ว
        RectTransform page = MakeArtImage("Page", book, Vector2.zero, Art("Book Component/Book_3.png"), new Vector2(683f, 766f), new Color32(0xF2, 0xEC, 0xE2, 0xFF));

        TextMeshProUGUI title = MakeText("PageTitle", page, new Vector2(-5f, 318f), new Vector2(400f, 60f), "Tasks", HeadFontSize, TextAlignmentOptions.Left);
        ui.pageTitleText = title;

        // ◀ ▶ มุมล่าง
        ui.backButton = MakeArrowButton("BackButton", "Back.png", page, new Vector2(-238f, -312f), -90f);
        ui.forwardButton = MakeArrowButton("ForwardButton", "Next.png", page, new Vector2(292f, -312f), 90f);

        BuildTasksPage(ui, page);
        BuildContentsPage(ui, NewPageArea("ContentsPage", page));
        BuildGridPage(ui, NewPageArea("GridPage", page));
        BuildDetailPage(ui, NewPageArea("DetailPage", page));
        BuildChangePhotoPage(ui, NewPageArea("ChangePhotoPage", page));

        SetLayerRecursive(rootPanel, uiLayer);
        rootPanel.gameObject.SetActive(false);
    }

    // หน้าย่อยเต็มหน้ากระดาษ — ของข้างในวางด้วยพิกัดเทียบกึ่งกลางหน้ากระดาษ
    private static RectTransform NewPageArea(string name, RectTransform page)
    {
        RectTransform area = NewRect(name, page);
        Stretch(area, 0f, 0f, 0f, 0f);
        return area;
    }

    // ==================== หน้า Tasks ====================
    private static void BuildTasksPage(JournalUI ui, RectTransform page)
    {
        // พื้นที่รายการ: กว้างเท่าแถบ Task (540) เริ่มใต้หัวกระดาษ จบเหนือแถวลูกศร
        RectTransform area = NewRect("TasksPage", page);
        Stretch(area, 91f, 52f, 130f, 100f);
        ui.tasksPage = area.gameObject;

        ScrollRect scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        RectTransform viewport = NewRect("Viewport", area);
        Stretch(viewport, 0f, 0f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        AddImage(viewport, new Color(1f, 1f, 1f, 0f), null); // โปร่งใส แค่ให้เลื่อนด้วยการลากได้
        scroll.viewport = viewport;

        RectTransform content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 14f;
        list.childControlHeight = true;
        list.childControlWidth = true;
        list.childForceExpandHeight = false;
        list.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        ui.taskListContent = content;

        ui.taskRowTemplate = BuildTaskRow(content);

        TextMeshProUGUI empty = MakeText("EmptyText", area, Vector2.zero, new Vector2(520f, 120f),
            "No tasks yet.\nTalk to people you meet in the forest.", TextFontSize, TextAlignmentOptions.Center);
        empty.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.6f);
        ui.tasksEmptyText = empty.gameObject;
    }

    private static JournalTaskRowUI BuildTaskRow(Transform parent)
    {
        RectTransform row = NewRect("TaskRowTemplate", parent);
        VerticalLayoutGroup rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = true;
        JournalTaskRowUI rowUI = row.gameObject.AddComponent<JournalTaskRowUI>();

        RectTransform header = MakeBar("Header", row);
        rowUI.headerButton = AddButton(header);

        // ช่องว่าง (ph_check-fill-1) อยู่ตลอด ติ๊ก (ph_check-fill) เปิดทับเมื่อ Complete
        RectTransform checkbox = MakeArtImage("Checkbox", header, Vector2.zero, Art("Task/ph_check-fill-1.png"), new Vector2(28f, 28f), InkColor);
        AnchorLeft(checkbox, 25f);
        rowUI.checkMark = MakeArtImage("Check", checkbox, Vector2.zero, Art("Task/ph_check-fill.png"), new Vector2(28f, 28f), InkColor).gameObject;

        TextMeshProUGUI title = MakeText("Title", header, Vector2.zero, Vector2.zero, "Task name", TitleFontSize, TextAlignmentOptions.Left);
        Stretch(title.rectTransform, 48f, 56f, 0f, 0f);
        rowUI.titleText = title;

        RectTransform chevron = MakeImage("Chevron", header, Vector2.zero, new Vector2(30f, 30f), InkColor, arrowSprite);
        AnchorRight(chevron, 28f);
        rowUI.chevron = chevron;

        RectTransform details = NewRect("Details", row);
        VerticalLayoutGroup detailsLayout = details.gameObject.AddComponent<VerticalLayoutGroup>();
        detailsLayout.padding = new RectOffset(48, 10, 0, 4);
        detailsLayout.childControlHeight = true;
        detailsLayout.childControlWidth = true;
        detailsLayout.childForceExpandHeight = false;
        rowUI.detailsRoot = details.gameObject;
        rowUI.detailsText = MakeText("DetailsText", details, Vector2.zero, Vector2.zero, "Details", TextFontSize, TextAlignmentOptions.TopLeft);

        return rowUI;
    }

    // ==================== หน้า Contents ====================
    private static void BuildContentsPage(JournalUI ui, RectTransform area)
    {
        ui.contentsPage = area.gameObject;
        ui.contentsPlantsButton = MakeBigRoundButton("PlantsButton", "Content/Icon_Plant.png", "Plants", area, new Vector2(-114f, 6f));
        ui.contentsAnimalsButton = MakeBigRoundButton("AnimalsButton", "Content/Icon_Animal.png", "Animals", area, new Vector2(118f, 6f));
    }

    private static Button MakeBigRoundButton(string name, string iconPath, string label, Transform parent, Vector2 pos)
    {
        RectTransform icon = MakeArtImage(name, parent, pos, Art(iconPath), new Vector2(170f, 170f), BarColor);
        Button button = AddButton(icon);
        MakeNamePill(icon, label, new Vector2(0f, -76f));
        return button;
    }

    // ==================== หน้า Grid 3x3 ====================
    private static void BuildGridPage(JournalUI ui, RectTransform area)
    {
        ui.gridPage = area.gameObject;
        List<JournalGridSlotUI> slots = new List<JournalGridSlotUI>();
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                Vector2 pos = new Vector2(-177f + col * 185f, 189f - row * 192f);
                slots.Add(MakeGridSlot("Slot" + slots.Count, area, pos));
            }
        }
        ui.gridSlots = slots.ToArray();
    }

    private static JournalGridSlotUI MakeGridSlot(string name, Transform parent, Vector2 pos)
    {
        RectTransform circle = MakeArtImage(name, parent, pos, Art("Content/Icon_Emtry.png"), new Vector2(160f, 160f), BarColor);
        JournalGridSlotUI slot = circle.gameObject.AddComponent<JournalGridSlotUI>();
        circle.gameObject.AddComponent<UIHoverPop>(); // ไม่ใช่ Button แต่คลิกได้ — นูนเหมือนปุ่มอื่น

        Image silhouette = MakeImage("Silhouette", circle, Vector2.zero, new Vector2(110f, 110f), Color.white, null).GetComponent<Image>();
        silhouette.preserveAspect = true;
        silhouette.raycastTarget = false;
        slot.silhouetteImage = silhouette;

        RectTransform pill = MakeNamePill(circle, "name", new Vector2(0f, -79f));
        slot.namePill = pill.gameObject;
        slot.nameText = pill.GetComponentInChildren<TextMeshProUGUI>();

        // จุดแดง (Placeholder): Entry นี้มี Quest ที่ยังไม่ Complete
        slot.questIndicator = MakeImage("QuestIndicator", circle, new Vector2(56f, 56f), new Vector2(26f, 26f), QuestDotColor, circleSprite).gameObject;
        return slot;
    }

    // ==================== หน้า Detail ====================
    private static void BuildDetailPage(JournalUI ui, RectTransform area)
    {
        ui.detailPage = area.gameObject;

        // กรอบรูป (Placeholder): ขอบสีแทน + พื้นมืด
        RectTransform frame = MakeImage("PhotoFrame", area, new Vector2(8f, 135f), new Vector2(389f, 221f), BarBorderColor, roundedSprite);
        RectTransform inner = MakeImage("PhotoBackground", frame, Vector2.zero, Vector2.zero, PhotoFrameColor, null);
        Stretch(inner, 6f, 6f, 6f, 6f);

        Image silhouette = MakeImage("SilhouetteImage", inner, Vector2.zero, Vector2.zero, Color.white, null).GetComponent<Image>();
        Stretch(silhouette.rectTransform, 0f, 0f, 0f, 0f);
        silhouette.preserveAspect = true;
        ui.detailSilhouetteImage = silhouette;

        Image photo = MakeImage("PhotoImage", inner, Vector2.zero, Vector2.zero, Color.white, null).GetComponent<Image>();
        Stretch(photo.rectTransform, 0f, 0f, 0f, 0f);
        photo.preserveAspect = true;
        ui.detailPhotoImage = photo;

        TextMeshProUGUI unknown = MakeText("UnknownMark", inner, Vector2.zero, new Vector2(200f, 200f), "?", 110f, TextAlignmentOptions.Center);
        unknown.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        ui.detailUnknownMark = unknown.gameObject;

        // ปุ่มเปลี่ยนรูป (Placeholder): วงกลมสีแทน + ไอคอนกล้องของ HUD (รอไอคอนลูกศรวนจากศิลปิน)
        RectTransform changeButton = MakeImage("ChangePhotoButton", area, new Vector2(257f, 45f), new Vector2(44f, 44f), BarBorderColor, circleSprite);
        ui.changePhotoButton = AddButton(changeButton);
        RectTransform changeFill = MakeImage("Fill", changeButton, Vector2.zero, new Vector2(38f, 38f), BarColor, circleSprite);
        changeFill.GetComponent<Image>().raycastTarget = false;
        Sprite cameraIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CameraIconPath);
        if (cameraIcon != null)
        {
            Image icon = MakeImage("Icon", changeButton, Vector2.zero, new Vector2(26f, 26f), Color.white, cameraIcon).GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        RectTransform nameBar = MakeBar("NameBar", area);
        Place(nameBar, new Vector2(8f, -29f), new Vector2(540f, 44f));
        ui.detailNameBarButton = AddButton(nameBar);
        TextMeshProUGUI nameText = MakeText("NameText", nameBar, Vector2.zero, Vector2.zero, "Name", TitleFontSize, TextAlignmentOptions.Left);
        Stretch(nameText.rectTransform, 22f, 56f, 0f, 0f);
        ui.detailNameText = nameText;
        RectTransform chevron = MakeImage("Chevron", nameBar, Vector2.zero, new Vector2(30f, 30f), InkColor, arrowSprite);
        AnchorRight(chevron, 28f);
        ui.detailChevron = chevron;

        // รายละเอียดใต้แถบชื่อ: ย่อหน้าสถานที่ + ย่อหน้าความรู้จาก NPC เรียงลงมา
        RectTransform body = NewRect("Body", area);
        body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
        body.pivot = new Vector2(0.5f, 1f);
        Place(body, new Vector2(33f, -60f), new Vector2(500f, 220f));
        VerticalLayoutGroup bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = 12f;
        bodyLayout.childControlHeight = true;
        bodyLayout.childControlWidth = true;
        bodyLayout.childForceExpandHeight = false;
        ui.detailBody = body.gameObject;
        ui.detailHabitatText = MakeText("HabitatText", body, Vector2.zero, Vector2.zero, "Habitat", TextFontSize, TextAlignmentOptions.TopLeft);
        ui.detailDescriptionText = MakeText("DescriptionText", body, Vector2.zero, Vector2.zero, "Description", TextFontSize, TextAlignmentOptions.TopLeft);
    }

    // ==================== หน้า Change Photos 2x3 ====================
    private static void BuildChangePhotoPage(JournalUI ui, RectTransform area)
    {
        ui.changePhotoPage = area.gameObject;
        List<Button> slots = new List<Button>();
        List<Image> images = new List<Image>();
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                // กรอบรูป (Placeholder): ขอบสีแทนบาง + พื้นมืด
                Vector2 pos = new Vector2(-131f + col * 270f, 193f - row * 155f);
                RectTransform slot = MakeImage("PhotoSlot" + slots.Count, area, pos, new Vector2(249f, 140f), BarColor, roundedSprite);
                slots.Add(AddButton(slot));
                RectTransform background = MakeImage("Background", slot, Vector2.zero, Vector2.zero, PhotoFrameColor, null);
                Stretch(background, 3f, 3f, 3f, 3f);
                background.GetComponent<Image>().raycastTarget = false;

                Image photo = MakeImage("Photo", slot, Vector2.zero, Vector2.zero, Color.white, null).GetComponent<Image>();
                Stretch(photo.rectTransform, 3f, 3f, 3f, 3f);
                photo.preserveAspect = true;
                photo.raycastTarget = false;
                images.Add(photo);
            }
        }
        ui.changePhotoSlots = slots.ToArray();
        ui.changePhotoImages = images.ToArray();
    }

    // ==================== ชิ้นส่วนย่อย ====================
    // แท็บมีตัวหนังสือวาดไว้ในรูปแล้ว — โคนซ้ายสอดอยู่ใต้หน้ากระดาษนิดหน่อย
    private static Button MakeTab(string name, string fileName, string fallbackLabel, Transform parent, float y)
    {
        Sprite sprite = Art("Book Component/" + fileName);
        RectTransform tab = MakeArtImage(name, parent, new Vector2(388f, y), sprite, new Vector2(100f, 64f), BarColor);
        if (sprite == null) MakeText("Label", tab, Vector2.zero, new Vector2(100f, 64f), fallbackLabel, TextFontSize, TextAlignmentOptions.Center);
        return AddButton(tab);
    }

    private static void MakePolaroid(string name, string fileName, Transform parent, Vector2 pos, Vector2 size, float fallbackAngle)
    {
        Sprite sprite = Art("Book Component/" + fileName);
        RectTransform polaroid = MakeArtImage(name, parent, pos, sprite, size, Color.white);
        polaroid.GetComponent<Image>().raycastTarget = false;
        if (sprite == null) polaroid.localEulerAngles = new Vector3(0f, 0f, fallbackAngle); // รูปจริงเอียงมาในไฟล์แล้ว
    }

    // ลูกศรจากศิลปิน (24x30) พื้นที่กดใหญ่กว่ารูป — ไม่มีไฟล์ก็ใช้ลูกศรของ Unity หมุนแทน
    private static Button MakeArrowButton(string name, string fileName, Transform parent, Vector2 pos, float fallbackAngle)
    {
        RectTransform hit = MakeImage(name, parent, pos, new Vector2(64f, 64f), new Color(1f, 1f, 1f, 0f), null);
        Button button = AddButton(hit);
        Sprite sprite = Art("Content/" + fileName);
        RectTransform arrow = sprite != null
            ? MakeImage("Arrow", hit, Vector2.zero, new Vector2(24f, 30f), Color.white, sprite)
            : MakeImage("Arrow", hit, Vector2.zero, new Vector2(64f, 64f), ArrowColor, arrowSprite);
        if (sprite == null) arrow.localEulerAngles = new Vector3(0f, 0f, fallbackAngle);
        arrow.GetComponent<Image>().raycastTarget = false;
        button.targetGraphic = arrow.GetComponent<Image>();
        return button;
    }

    // แถบสีแทน (Task/Box.png) สูงเท่ารูปจริง 44px — ใช้ทั้งแถว Task และแถบชื่อหน้า Detail
    private static RectTransform MakeBar(string name, Transform parent)
    {
        RectTransform bar = MakeArtImage(name, parent, Vector2.zero, Art("Task/Box.png"), new Vector2(540f, 44f), BarColor);
        LayoutElement layout = bar.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 44f;
        layout.preferredHeight = 44f;
        return bar;
    }

    private static RectTransform MakeNamePill(Transform parent, string label, Vector2 pos)
    {
        RectTransform pill = MakeArtImage("NamePill", parent, pos, Art("Content/NameTextBox.png"), new Vector2(138f, 38f), Color.white);
        pill.GetComponent<Image>().raycastTarget = false;
        TextMeshProUGUI text = MakeText("Name", pill, Vector2.zero, new Vector2(124f, 34f), label, TextFontSize, TextAlignmentOptions.Center);
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f; // ชื่อยาว (เช่น Joe-Pye Weed) หดลงให้พอดีป้าย
        text.fontSizeMax = TextFontSize;
        return pill;
    }

    // รูปจากศิลปินขนาดตามที่วัดจาก Mockup — ถ้ายังไม่มีไฟล์ ใช้สี่เหลี่ยมมุมมนสี fallbackColor แทน
    private static RectTransform MakeArtImage(string name, Transform parent, Vector2 pos, Sprite sprite, Vector2 size, Color fallbackColor)
    {
        return MakeImage(name, parent, pos, size, sprite != null ? Color.white : fallbackColor, sprite != null ? sprite : roundedSprite);
    }

    private static RectTransform MakeImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color, Sprite sprite)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, pos, size);
        AddImage(rt, color, sprite);
        return rt;
    }

    private static Image AddImage(RectTransform rt, Color color, Sprite sprite)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        if (sprite == roundedSprite) image.type = Image.Type.Sliced;
        return image;
    }

    private static Button AddButton(RectTransform rt)
    {
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None; // กันปุ่มค้าง Selected หลังคลิก (เมาส์เป็นหลัก)
        button.navigation = nav;

        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = PressedTint;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        rt.gameObject.AddComponent<UIHoverPop>();
        return button;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, Vector2 pos, Vector2 size, string value, float fontSize, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, pos, size);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = InkColor;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void AnchorLeft(RectTransform rt, float x)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
    }

    private static void AnchorRight(RectTransform rt, float x)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-x, 0f);
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursive(child, layer);
    }
}
