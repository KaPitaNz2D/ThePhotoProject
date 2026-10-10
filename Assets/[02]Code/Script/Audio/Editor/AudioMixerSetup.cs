#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// สร้าง AudioMixer หลักของเกม (Assets/[05]Audio/MainMixer.mixer) ผ่านเมนู Tools > Photo Project > Create Audio Mixer
/// Unity ไม่มี API สาธารณะสำหรับสร้าง Mixer ด้วยโค้ด เลยใช้ Reflection เรียก API ภายในของ Editor (UnityEditor.Audio.*) ถ้า Unity เวอร์ชันใหม่เปลี่ยนชื่อ API
/// ก็แค่สร้าง Mixer เองใน Editor ตามโครงสร้างด้านล่างได้เหมือนกัน (โค้ดตอนเล่นเกมไม่ได้พึ่ง API ภายในพวกนี้)
///
/// โครงสร้าง:  Master
///             ├── Music      (Exposed: MusicVolume)
///             ├── Ambient    (Exposed: AmbientVolume)
///             ├── SFX        (Exposed: SFXVolume)
///             └── UI         (Exposed: UIVolume)        + Master (Exposed: MasterVolume)
/// ค่า Exposed เป็นหน่วย dB (-80..0) ใช้ผ่าน AudioManager.SetGroupVolume() ที่แปลงจากสเกล 0..1 ให้
/// ถ้ามี Mixer อยู่แล้วจะไม่ทำซ้ำ (ลบไฟล์เองถ้าต้องการสร้างใหม่)
/// </summary>
public static class AudioMixerSetup
{
    public const string MixerPath = "Assets/[05]Audio/MainMixer.mixer";

    private static readonly string[] ChildGroups = { "Music", "Ambient", "SFX", "UI" };

    [MenuItem("Tools/Photo Project/Create Audio Mixer")]
    public static void CreateMixer()
    {
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
        {
            Debug.Log($"[AudioMixerSetup] มี Mixer อยู่แล้วที่ {MixerPath} ไม่สร้างซ้ำ");
            return;
        }

        Type controllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
        Type exposedType = Type.GetType("UnityEditor.Audio.ExposedAudioParameter, UnityEditor");
        if (controllerType == null || exposedType == null)
        {
            Debug.LogError("[AudioMixerSetup] หา API ภายในของ AudioMixer ไม่เจอ (Unity เปลี่ยนไปแล้ว) — สร้าง Mixer เองใน Editor ตามโครงสร้างในคอมเมนต์ไฟล์นี้");
            return;
        }

        BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // สร้างไฟล์ .mixer (มี Master + Snapshot เริ่มต้นให้ในตัว)
        object controller = controllerType.GetMethod("CreateMixerControllerAtPath", all).Invoke(null, new object[] { MixerPath });
        object master = controllerType.GetProperty("masterGroup", all).GetValue(controller);
        Type groupType = master.GetType();

        MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", all);
        MethodInfo addChild = controllerType.GetMethod("AddChildToParent", all);
        MethodInfo getVolumeGuid = groupType.GetMethod("GetGUIDForVolume", all);

        var exposed = new List<(string name, object guid)>
        {
            ("MasterVolume", getVolumeGuid.Invoke(master, null))
        };

        foreach (string groupName in ChildGroups)
        {
            object group = createGroup.Invoke(controller, new object[] { groupName, false });
            addChild.Invoke(controller, new object[] { group, master });
            exposed.Add((groupName + "Volume", getVolumeGuid.Invoke(group, null)));
        }

        // เปิด (Expose) พารามิเตอร์ Volume ของแต่ละกลุ่ม ให้โค้ดเรียก AudioMixer.SetFloat("AmbientVolume", dB) ได้
        Array parameters = Array.CreateInstance(exposedType, exposed.Count);
        FieldInfo guidField = exposedType.GetField("guid", all);
        FieldInfo nameField = exposedType.GetField("name", all);
        for (int i = 0; i < exposed.Count; i++)
        {
            object entry = Activator.CreateInstance(exposedType);
            guidField.SetValue(entry, exposed[i].guid);
            nameField.SetValue(entry, exposed[i].name);
            parameters.SetValue(entry, i);
        }
        controllerType.GetProperty("exposedParameters", all).SetValue(controller, parameters);

        EditorUtility.SetDirty((UnityEngine.Object)controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath);
        Debug.Log($"[AudioMixerSetup] สร้าง Mixer ที่ {MixerPath} แล้ว (Master + Music/Ambient/SFX/UI)");
    }
}
#endif
