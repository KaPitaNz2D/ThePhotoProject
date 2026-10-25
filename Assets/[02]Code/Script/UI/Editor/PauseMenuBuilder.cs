using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// เมนู Tools > Photo Project > Create Pause Menu — สร้าง PauseMenu.prefab (หน้า Pause + หน้า Setting เสียง ตามเลย์เอาต์ Figma 1920x1080)
/// + Volume Profile เบลอฉากหลัง แล้วใส่ PauseMenu เข้าไปใน GameSystems.prefab (ทุกซีนที่มี GameSystems ได้เมนูนี้เอง)
/// กดซ้ำได้ — สร้าง Prefab ทับของเดิม ไม่ใส่ซ้ำใน GameSystems
/// </summary>
public static class PauseMenuBuilder
{
    private const string Folder = "Assets/[03]Prefabs/UI prefeb";
    private const string PrefabPath = Folder + "/PauseMenu.prefab";
    private const string ProfilePath = Folder + "/PauseBlurProfile.asset";
    private const string GameSystemsPath = "Assets/[03]Prefabs/GameSystems.prefab";
    private const string PlayerRigPath = "Assets/[03]Prefabs/PlayerRig.prefab";
    private const string InputActionsPath = "Assets/[02]Code/Script/PlayerControls.inputactions";

    private static readonly Color ButtonColor = new Color(0.851f, 0.851f, 0.851f, 1f); // #D9D9D9
    private static readonly Color TrackColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    private static readonly Color KnobColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.3f);

    private static int uiLayer;

    [MenuItem("Tools/Photo Project/Create Pause Menu")]
    public static void Build()
    {
        uiLayer = LayerMask.NameToLayer("UI");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/[03]Prefabs", "UI prefeb");

        VolumeProfile profile = CreateBlurProfile();
        GameObject root = BuildHierarchy(profile);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        AddToGameSystems();
        EnsureCameraPostProcessing();
        Debug.Log("[PauseMenuBuilder] สร้าง " + PrefabPath + " แล้ว (ใส่ใน GameSystems.prefab ด้วย)");
    }

    // ==================== Blur ====================
    private static VolumeProfile CreateBlurProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        if (!profile.TryGet(out DepthOfField dof))
        {
            dof = profile.Add<DepthOfField>(true);
            dof.name = "DepthOfField";
            AssetDatabase.AddObjectToAsset(dof, profile);
        }
        // Gaussian: ตั้งแต่ระยะ 0.5 ม. ขึ้นไปเบลอเต็มที่ = เบลอทั้งฉาก (UI แบบ Overlay ไม่โดนเบลอ)
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(0f);
        dof.gaussianEnd.Override(0.5f);
        dof.gaussianMaxRadius.Override(1.5f);
        dof.highQualitySampling.Override(true);

        EditorUtility.SetDirty(dof);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // ==================== Hierarchy ====================
    private static GameObject BuildHierarchy(VolumeProfile profile)
    {
        var root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = uiLayer;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // อยู่เหนือ UI อื่นทั้งหมด
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var ui = root.AddComponent<PauseMenuUI>();
        ui.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

        // Volume เบลอฉากหลัง
        var blurGo = new GameObject("BlurVolume");
        blurGo.transform.SetParent(root.transform, false);
        var volume = blurGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;
        volume.sharedProfile = profile;
        ui.blurVolume = volume;

        TMP_DefaultControls.Resources tmpRes = TmpResources();
        DefaultControls.Resources uguiRes = UguiResources();

        // ---------- หน้า Pause ----------
        RectTransform pause = FullScreenPanel("PausePanel", root.transform);
        Button resume = MakeButton("Resume", "Resume", pause, new Vector2(0f, 155f), tmpRes);
        Button setting = MakeButton("Setting", "Setting", pause, new Vector2(0f, 0f), tmpRes);
        Button menu = MakeButton("BackToMenu", "Back to Menu", pause, new Vector2(0f, -155f), tmpRes);
        UnityEventTools.AddPersistentListener(resume.onClick, new UnityAction(ui.Resume));
        UnityEventTools.AddPersistentListener(setting.onClick, new UnityAction(ui.OpenSettings));
        UnityEventTools.AddPersistentListener(menu.onClick, new UnityAction(ui.BackToMenu));
        ui.pausePanel = pause.gameObject;
        ui.firstPauseButton = resume.gameObject;

        // ---------- หน้า Setting ----------
        RectTransform settings = FullScreenPanel("SettingsPanel", root.transform);
        var box = NewRect("Box", settings);
        box.sizeDelta = new Vector2(1175f, 732f);
        box.anchoredPosition = Vector2.zero;
        box.gameObject.AddComponent<Image>().color = ButtonColor;

        TextMeshProUGUI title = MakeText("Title", "Sound", box, 64f, TextAlignmentOptions.MidlineLeft);
        PlaceTopLeft((RectTransform)title.transform, 44f, 46f, 400f, 77f);

        Button back = MakeButton("Back", "Back", box, Vector2.zero, tmpRes);
        PlaceTopLeft((RectTransform)back.transform, 1007f, 10f, 149f, 77f);
        back.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        back.GetComponentInChildren<TextMeshProUGUI>().fontSize = 56f;
        UnityEventTools.AddPersistentListener(back.onClick, new UnityAction(ui.CloseSettings));

        string[] labels = { "Master", "Ambient", "Music", "Sound Effect" };
        var channels = new[] { AudioManager.VolumeChannel.Master, AudioManager.VolumeChannel.Ambient, AudioManager.VolumeChannel.Music, AudioManager.VolumeChannel.SFX };
        Slider firstSlider = null;
        for (int i = 0; i < labels.Length; i++)
        {
            Slider slider = MakeVolumeRow(labels[i], channels[i], box, 193f + 82f * i, tmpRes, uguiRes);
            if (firstSlider == null) firstSlider = slider;
        }
        ui.settingsPanel = settings.gameObject;
        ui.firstSettingsSelectable = firstSlider.gameObject;

        SetLayerRecursive(root.transform, uiLayer);
        blurGo.layer = 0; // Volume ไม่ใช่ UI
        return root;
    }

    private static Slider MakeVolumeRow(string label, AudioManager.VolumeChannel channel, RectTransform box, float top, TMP_DefaultControls.Resources tmpRes, DefaultControls.Resources uguiRes)
    {
        RectTransform row = NewRect(label + "Row", box);
        PlaceTopLeft(row, 44f, top, 1087f, 47f);
        var rowComp = row.gameObject.AddComponent<VolumeSettingRow>();
        rowComp.channel = channel;

        TextMeshProUGUI text = MakeText("Label", label, row, 36f, TextAlignmentOptions.MidlineLeft);
        PlaceTopLeft((RectTransform)text.transform, 0f, 0f, 380f, 47f);

        // Slider: เส้นหนา 8 + ปุ่มกลม 47 (ตาม Figma) ราง x 811-1400
        GameObject sliderGo = DefaultControls.CreateSlider(uguiRes);
        sliderGo.name = "Slider";
        var srt = (RectTransform)sliderGo.transform;
        srt.SetParent(row, false);
        PlaceTopLeft(srt, 389f, 0f, 589f, 47f);
        foreach (string strip in new[] { "Background", "Fill Area" })
        {
            var s = (RectTransform)sliderGo.transform.Find(strip);
            s.anchorMin = new Vector2(0f, 0.5f);
            s.anchorMax = new Vector2(1f, 0.5f);
            s.sizeDelta = new Vector2(0f, 8f);
            s.anchoredPosition = Vector2.zero;
        }
        sliderGo.transform.Find("Background").GetComponent<Image>().color = TrackColor;
        var fill = (RectTransform)sliderGo.transform.Find("Fill Area/Fill");
        fill.sizeDelta = new Vector2(0f, 0f);
        fill.GetComponent<Image>().color = TrackColor;
        var fillArea = (RectTransform)sliderGo.transform.Find("Fill Area");
        fillArea.offsetMin = new Vector2(0f, fillArea.offsetMin.y);
        fillArea.offsetMax = new Vector2(0f, fillArea.offsetMax.y);
        var slideArea = (RectTransform)sliderGo.transform.Find("Handle Slide Area");
        slideArea.offsetMin = new Vector2(23.5f, 0f);
        slideArea.offsetMax = new Vector2(-23.5f, 0f);
        var handle = (RectTransform)sliderGo.transform.Find("Handle Slide Area/Handle");
        handle.sizeDelta = new Vector2(47f, 0f);
        handle.GetComponent<Image>().color = KnobColor;
        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 100f; slider.wholeNumbers = true; slider.value = 100f;
        rowComp.slider = slider;

        // ช่องตัวเลข 0-100 (x 1430 ใน Figma)
        GameObject inputGo = TMP_DefaultControls.CreateInputField(tmpRes);
        inputGo.name = "Value";
        var irt = (RectTransform)inputGo.transform;
        irt.SetParent(row, false);
        PlaceTopLeft(irt, 1008f, 0f, 90f, 47f);
        inputGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
        var field = inputGo.GetComponent<TMP_InputField>();
        field.contentType = TMP_InputField.ContentType.IntegerNumber;
        field.characterLimit = 3;
        field.text = "100";
        foreach (var t in inputGo.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            t.fontSize = 32f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.black;
        }
        if (field.placeholder is TextMeshProUGUI ph) { ph.text = "0-100"; ph.color = new Color(0f, 0f, 0f, 0.35f); ph.fontSize = 24f; }
        rowComp.input = field;

        return slider;
    }

    // ==================== Helpers ====================
    private static RectTransform FullScreenPanel(string name, Transform parent)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var dim = rt.gameObject.AddComponent<Image>();
        dim.color = DimColor; // บังคลิกที่จะทะลุไปโดน UI อื่นข้างหลังด้วย
        return rt;
    }

    private static Button MakeButton(string name, string label, Transform parent, Vector2 center, TMP_DefaultControls.Resources res)
    {
        GameObject go = TMP_DefaultControls.CreateButton(res);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(407f, 112f);
        rt.anchoredPosition = center;
        go.GetComponent<Image>().color = ButtonColor;
        var text = go.GetComponentInChildren<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 40f;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Center;
        return go.GetComponent<Button>();
    }

    private static TextMeshProUGUI MakeText(string name, string value, Transform parent, float size, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = size;
        t.color = Color.black;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    // ตำแหน่งแบบ Figma: x/y นับจากมุมซ้ายบนของ parent (y ลงล่าง)
    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayerRecursive(c, layer);
    }

    private static TMP_DefaultControls.Resources TmpResources() => new TMP_DefaultControls.Resources
    {
        standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
        mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
    };

    private static DefaultControls.Resources UguiResources() => new DefaultControls.Resources
    {
        standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
        mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
    };

    // ==================== ใส่ใน GameSystems / เปิด Post Processing ====================
    private static void AddToGameSystems()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GameSystemsPath) == null)
        {
            Debug.LogWarning("[PauseMenuBuilder] ไม่พบ " + GameSystemsPath + " — ลาก PauseMenu.prefab ใส่ซีนเอง");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(GameSystemsPath);
        try
        {
            if (root.GetComponentInChildren<PauseMenuUI>(true) != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            instance.name = "PauseMenu";
            PrefabUtility.SaveAsPrefabAsset(root, GameSystemsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // DoF ทำงานเฉพาะกล้องที่เปิด Post Processing
    private static void EnsureCameraPostProcessing()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPath) == null) return;
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
        try
        {
            bool changed = false;
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (!cam.CompareTag("MainCamera")) continue;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && !data.renderPostProcessing)
                {
                    data.renderPostProcessing = true;
                    changed = true;
                    Debug.Log("[PauseMenuBuilder] เปิด Post Processing ที่ " + cam.name + " (ต้องใช้กับเบลอฉากหลัง)");
                }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, PlayerRigPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
