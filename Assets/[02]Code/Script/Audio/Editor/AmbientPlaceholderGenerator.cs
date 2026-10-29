#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// สร้างเสียงจำลอง (Placeholder สังเคราะห์ด้วยโค้ด — เสียงนก/ใบไม้/แมลง/ลม ฟังไม่เหมือนจริง) และ AmbientSoundSet ตั้งค่าเริ่มต้นให้
/// ไว้ทดสอบ AmbientDome (ทิศทาง ระยะ จังหวะ เวลากลางวัน-กลางคืน) ก่อนมีไฟล์เสียงจริง
/// เมนู: Tools > Photo Project > Generate Placeholder Ambient Sounds   (ไฟล์/Asset ที่มีอยู่แล้วจะไม่ถูกเขียนทับ)
/// พอได้ไฟล์เสียงจริง แค่ลากไปแทนช่อง Clips ใน AmbientSoundSet แต่ละชุด แล้วลบโฟลเดอร์ Placeholder ทิ้งได้เลย
///
/// ไฟล์: Assets/[05]Audio/Ambient/Placeholder/*.wav   ชุดเสียง: Assets/[05]Audio/Ambient/Sets/*.asset
/// </summary>
public static class AmbientPlaceholderGenerator
{
    private const int Rate = 44100;
    private const string ClipFolder = "Assets/[05]Audio/Ambient/Placeholder";
    private const string SetFolder = "Assets/[05]Audio/Ambient/Sets";

    [MenuItem("Tools/Photo Project/Generate Placeholder Ambient Sounds")]
    public static void Generate()
    {
        Directory.CreateDirectory(ClipFolder);
        Directory.CreateDirectory(SetFolder);

        var rng = new System.Random(1234);

        // ----- เสียงนก (กลางวัน) -----
        WriteClip("bird_a", BirdTripleChirp(rng));
        WriteClip("bird_b", BirdWarble());
        WriteClip("bird_c", BirdDescendingCall());
        // ----- ใบไม้/หญ้า -----
        WriteClip("leaves_a", Rustle(rng, 1.3f, 0.10f, 0.55f, 3.2f));
        WriteClip("leaves_b", Rustle(rng, 1.9f, 0.08f, 0.50f, 2.4f));
        WriteClip("grass_a", Rustle(rng, 0.7f, 0.22f, 0.70f, 6.0f));
        // ----- แมลง/กลางคืน -----
        WriteClip("insect_a", InsectBuzz());
        WriteClip("cricket_a", Crickets());
        WriteClip("owl_a", OwlHoot());
        // ----- ลม -----
        WriteClip("wind_gust_a", WindGust(rng));

        AssetDatabase.Refresh();

        //                                        clips                                      interval   distance   height     volume      min  max  lowpass hours        terrain
        CreateSet("BirdsDay",      new[] { "bird_a", "bird_b", "bird_c" },     new Vector2(3, 9),   new Vector2(10, 40), new Vector2(5, 18),  new Vector2(0.5f, 0.95f), 5,  55, 7000,  true, 6, 18, "");
        CreateSet("LeavesRustle",  new[] { "leaves_a", "leaves_b" },           new Vector2(6, 16),  new Vector2(5, 25),  new Vector2(2, 10),  new Vector2(0.3f, 0.6f),  3,  30, 8000,  false, 0, 0, "");
        CreateSet("GrassRustle",   new[] { "grass_a" },                         new Vector2(7, 18),  new Vector2(2, 10),  new Vector2(0, 0.5f), new Vector2(0.2f, 0.45f), 2,  14, 12000, false, 0, 0, "Grass");
        CreateSet("InsectsDay",    new[] { "insect_a" },                        new Vector2(5, 12),  new Vector2(4, 18),  new Vector2(0, 1.5f), new Vector2(0.15f, 0.35f), 3, 22, 9000,  true, 8, 18, "Grass");
        CreateSet("CricketsNight", new[] { "cricket_a" },                       new Vector2(3, 8),   new Vector2(5, 20),  new Vector2(0, 1),   new Vector2(0.2f, 0.5f),  3,  25, 9000,  true, 19, 5, "");
        CreateSet("OwlNight",      new[] { "owl_a" },                           new Vector2(18, 45), new Vector2(25, 50), new Vector2(8, 18),  new Vector2(0.4f, 0.7f),  8,  70, 3500,  true, 21, 4, "");
        CreateSet("WindGust",      new[] { "wind_gust_a" },                     new Vector2(14, 34), new Vector2(15, 40), new Vector2(3, 10),  new Vector2(0.3f, 0.6f),  8,  55, 4000,  false, 0, 0, "");

        AssetDatabase.SaveAssets();
        Debug.Log("[AmbientPlaceholderGenerator] สร้างเสียงจำลองใน " + ClipFolder + " และชุดเสียงใน " + SetFolder + " แล้ว");
    }

    // ==================== สร้าง AmbientSoundSet ====================
    private static void CreateSet(string name, string[] clipNames, Vector2 interval, Vector2 distance, Vector2 height, Vector2 volume,
                                  float minDistance, float maxDistance, float lowPass, bool limitByHour, int fromHour, int toHour, string terrainKeyword)
    {
        string path = $"{SetFolder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<AmbientSoundSet>(path) != null) return;

        var set = ScriptableObject.CreateInstance<AmbientSoundSet>();
        set.clips = Array.ConvertAll(clipNames, n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/{n}.wav"));
        set.intervalRange = interval;
        set.distanceRange = distance;
        set.heightAboveGround = height;
        set.volumeRange = volume;
        set.minDistance = minDistance;
        set.maxDistance = maxDistance;
        set.lowPassAtMaxDistance = lowPass;
        set.limitByHour = limitByHour;
        set.fromHour = fromHour;
        set.toHour = toHour;
        set.requiredTerrainLayerKeyword = terrainKeyword;
        AssetDatabase.CreateAsset(set, path);
    }

    // ==================== เสียงสังเคราะห์ ====================
    private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

    // โน้ตเดี่ยว: ความถี่เลื่อนจาก f0 ไป f1 ตลอดความยาว มี Envelope เข้า-ออกนุ่มๆ
    private static void AddSweep(float[] buffer, float start, float length, float f0, float f1, float amp, float vibratoHz = 0f, float vibratoDepth = 0f)
    {
        int s = Mathf.RoundToInt(start * Rate);
        int n = Mathf.RoundToInt(length * Rate);
        double phase = 0.0;
        for (int i = 0; i < n && s + i < buffer.Length; i++)
        {
            float t = (float)i / n;
            float freq = Mathf.Lerp(f0, f1, t) + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * i / Rate);
            phase += 2.0 * Math.PI * freq / Rate;
            float envelope = Mathf.Sin(Mathf.PI * t); // 0 -> 1 -> 0
            envelope *= envelope;
            buffer[s + i] += (float)Math.Sin(phase) * envelope * amp;
        }
    }

    private static float[] BirdTripleChirp(System.Random rng)
    {
        float[] b = Buffer(0.8f);
        for (int i = 0; i < 3; i++) AddSweep(b, 0.05f + i * 0.2f, 0.12f, 3200f + i * 150f, 4300f + i * 120f, 0.8f);
        return b;
    }

    private static float[] BirdWarble()
    {
        float[] b = Buffer(1.2f);
        AddSweep(b, 0.05f, 1.0f, 2700f, 3100f, 0.8f, vibratoHz: 15f, vibratoDepth: 600f);
        return b;
    }

    private static float[] BirdDescendingCall()
    {
        float[] b = Buffer(1.0f);
        AddSweep(b, 0.05f, 0.35f, 4300f, 2600f, 0.8f);
        AddSweep(b, 0.55f, 0.28f, 3000f, 3900f, 0.7f);
        return b;
    }

    // เสียงเสียดสี (ใบไม้/หญ้า): สัญญาณรบกวนกรองเอาเฉพาะย่านกลาง-สูง คูณ Envelope ที่สั่นไหวสุ่ม
    private static float[] Rustle(System.Random rng, float seconds, float lowAlpha, float highAlpha, float flutterHz)
    {
        float[] b = Buffer(seconds);
        float lowState = 0f, highState = 0f;
        float flutterPhase = (float)rng.NextDouble() * 6.28f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / b.Length;
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowState += lowAlpha * (n - lowState);          // ตัดย่านสูงมาก
            highState += highAlpha * (lowState - highState); // ตัดย่านต่ำ: ได้ band-pass คร่าวๆ
            float band = lowState - highState;
            float flutter = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * flutterHz * t * seconds + flutterPhase);
            float shape = Mathf.Sin(Mathf.PI * t);
            b[i] = band * flutter * shape;
        }
        return b;
    }

    private static float[] InsectBuzz()
    {
        float[] b = Buffer(1.6f);
        double phase = 0.0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / b.Length;
            phase += 2.0 * Math.PI * (5200f + 120f * Mathf.Sin(2f * Mathf.PI * 3f * t)) / Rate;
            float am = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 38f * i / Rate);
            b[i] = (float)Math.Sin(phase) * am * Mathf.Sin(Mathf.PI * t);
        }
        return b;
    }

    private static float[] Crickets()
    {
        float[] b = Buffer(2.0f);
        for (int group = 0; group < 3; group++)
            for (int pulse = 0; pulse < 3; pulse++)
                AddSweep(b, 0.05f + group * 0.55f + pulse * 0.09f, 0.045f, 4300f, 4350f, 0.7f);
        return b;
    }

    private static float[] OwlHoot()
    {
        float[] b = Buffer(1.5f);
        AddSweep(b, 0.1f, 0.4f, 430f, 400f, 0.8f, vibratoHz: 5f, vibratoDepth: 12f);
        AddSweep(b, 0.7f, 0.55f, 440f, 380f, 0.8f, vibratoHz: 5f, vibratoDepth: 12f);
        return b;
    }

    private static float[] WindGust(System.Random rng)
    {
        float[] b = Buffer(3.6f);
        float lowState = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / b.Length;
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowState += 0.03f * (n - lowState);
            float swell = Mathf.Sin(Mathf.PI * t);
            b[i] = lowState * swell * swell;
        }
        return b;
    }

    // ==================== เขียน WAV (16-bit, mono) ====================
    private static void WriteClip(string name, float[] samples)
    {
        string path = $"{ClipFolder}/{name}.wav";
        if (File.Exists(path)) return;

        float peak = 0.0001f;
        foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
        float gain = 0.8f / peak; // ปรับให้ยอดเสียง ~0.8 ทุกไฟล์ ความดังจริงคุมที่ Volume ใน AmbientSoundSet

        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            int dataBytes = samples.Length * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);       // PCM
            writer.Write((short)1);       // mono
            writer.Write(Rate);
            writer.Write(Rate * 2);       // byte rate
            writer.Write((short)2);       // block align
            writer.Write((short)16);      // bits
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            foreach (float s in samples)
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * gain * 32767f), -32767, 32767));
        }
    }
}
#endif
