using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แถวปรับเสียง 1 แถวในหน้า Setting — ลาก Slider หรือพิมพ์ตัวเลข 0-100 ในช่องข้างๆ ก็ได้ (สองอย่างตามกันเสมอ)
/// ค่าส่งไป AudioManager.SetVolume ทันทีและเซฟข้ามการเปิดเกม
/// </summary>
public class VolumeSettingRow : MonoBehaviour
{
    public AudioManager.VolumeChannel channel;
    public Slider slider;
    public TMP_InputField input;

    private void Awake()
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
        if (input != null)
        {
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 3;
            input.onEndEdit.AddListener(OnInputEnded);
        }
    }

    // เปิดหน้า Setting ทุกครั้ง โชว์ค่าที่เซฟไว้ล่าสุด
    private void OnEnable()
    {
        Show(Mathf.RoundToInt(AudioManager.GetSavedVolume(channel) * 100f));
    }

    private void OnSliderChanged(float value)
    {
        int v = Mathf.RoundToInt(value);
        if (input != null) input.SetTextWithoutNotify(v.ToString());
        Apply(v);
    }

    private void OnInputEnded(string text)
    {
        int v = int.TryParse(text, out int parsed) ? Mathf.Clamp(parsed, 0, 100) : Mathf.RoundToInt(slider != null ? slider.value : 100f);
        Show(v);
        Apply(v);
    }

    private void Show(int value)
    {
        if (slider != null) slider.SetValueWithoutNotify(value);
        if (input != null) input.SetTextWithoutNotify(value.ToString());
    }

    private void Apply(int value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetVolume(channel, value / 100f);
        else PlayerPrefs.SetFloat("Volume." + channel, value / 100f); // ซีนที่ไม่มี AudioManager ก็ยังเซฟไว้ใช้ทีหลังได้
    }
}
