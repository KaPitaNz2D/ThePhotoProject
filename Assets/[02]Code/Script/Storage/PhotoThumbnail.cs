using UnityEngine;

/// <summary>
/// สร้างรูปย่อ (Thumbnail) จากรูปเต็ม — ไม่ต้องอ่าน/ถอดรหัส PNG ขนาดจริงซ้ำทุกครั้งที่เปิดหน้าคลังภาพหรือ Journal
/// มี 2 ทาง: ย่อด้วย GPU จาก Texture (Create) และย่อด้วย CPU จากพิกเซลดิบ (DownscaleRgbaToRgb24 — ปลอดภัยต่อ Thread เบื้องหลัง
/// ใช้ตอนบันทึกภาพแบบ async ใน PhotoStorage)
/// </summary>
public static class PhotoThumbnail
{
    /// <summary>คำนวณขนาดรูปย่อ: ด้านยาวสุดไม่เกิน maxSize คงสัดส่วน ไม่ขยายถ้าเล็กกว่าอยู่แล้ว</summary>
    public static void GetThumbnailSize(int sourceWidth, int sourceHeight, int maxSize, out int width, out int height)
    {
        float scale = Mathf.Min(1f, (float)maxSize / Mathf.Max(sourceWidth, sourceHeight));
        width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
        height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
    }

    /// <summary>
    /// ย่อพิกเซลดิบ RGBA32 เป็น RGB24 ด้วยการหาค่าเฉลี่ยกล่อง (Box Filter) — ไม่เรียก API ของ Unity เลย เรียกจาก Thread เบื้องหลังได้
    /// ลำดับแถวคงเดิมกับต้นฉบับ (แถวล่างสุดก่อน ถ้าต้นฉบับเป็นแบบ Texture2D)
    /// </summary>
    public static byte[] DownscaleRgbaToRgb24(byte[] rgba, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        byte[] result = new byte[targetWidth * targetHeight * 3];

        for (int y = 0; y < targetHeight; y++)
        {
            int y0 = y * sourceHeight / targetHeight;
            int y1 = System.Math.Max(y0 + 1, (y + 1) * sourceHeight / targetHeight);

            for (int x = 0; x < targetWidth; x++)
            {
                int x0 = x * sourceWidth / targetWidth;
                int x1 = System.Math.Max(x0 + 1, (x + 1) * sourceWidth / targetWidth);

                int r = 0, g = 0, b = 0, count = 0;
                for (int sy = y0; sy < y1; sy++)
                {
                    int index = (sy * sourceWidth + x0) * 4;
                    for (int sx = x0; sx < x1; sx++, index += 4)
                    {
                        r += rgba[index];
                        g += rgba[index + 1];
                        b += rgba[index + 2];
                        count++;
                    }
                }

                int o = (y * targetWidth + x) * 3;
                result[o] = (byte)(r / count);
                result[o + 1] = (byte)(g / count);
                result[o + 2] = (byte)(b / count);
            }
        }

        return result;
    }

    /// <summary>สร้าง Texture2D (RGB24) จากพิกเซลดิบที่ได้จาก DownscaleRgbaToRgb24 — ต้องเรียกบน Main Thread ผู้เรียก Destroy เอง</summary>
    public static Texture2D FromRgb24(byte[] rgb, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.LoadRawTextureData(rgb);
        texture.Apply(false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    /// <summary>
    /// ย่อ Texture ด้วย GPU ให้ด้านยาวสุดไม่เกิน maxSize (Blit ย่อทีละครึ่งแล้วย่อรอบสุดท้าย → ReadPixels) คืน Texture2D RGB24 ไม่มี Mipmap
    /// ผู้เรียกเป็นเจ้าของผลลัพธ์ ต้อง Destroy() เองเมื่อเลิกใช้ — ใช้กับภาพที่ไม่มีรูปย่อติดมา (สร้างจากไฟล์ตอนมีคนขอ)
    /// </summary>
    public static Texture2D Create(Texture source, int maxSize)
    {
        if (source == null || maxSize < 1) return null;

        int sourceWidth = source.width;
        int sourceHeight = source.height;
        GetThumbnailSize(sourceWidth, sourceHeight, maxSize, out int targetWidth, out int targetHeight);

        RenderTexture previousActive = RenderTexture.active;
        Texture current = source;
        RenderTexture currentTemp = null;
        int currentWidth = sourceWidth;
        int currentHeight = sourceHeight;

        // ย่อทีละครึ่งตราบที่ยังใหญ่กว่าเป้าหมายเกิน 2 เท่า
        while (currentWidth / 2 >= targetWidth && currentHeight / 2 >= targetHeight)
        {
            currentWidth /= 2;
            currentHeight /= 2;
            RenderTexture half = RenderTexture.GetTemporary(currentWidth, currentHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(current, half);
            if (currentTemp != null) RenderTexture.ReleaseTemporary(currentTemp);
            currentTemp = half;
            current = half;
        }

        RenderTexture final = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(current, final);
        if (currentTemp != null) RenderTexture.ReleaseTemporary(currentTemp);

        RenderTexture.active = final;
        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply(false);
        result.filterMode = FilterMode.Bilinear;
        result.wrapMode = TextureWrapMode.Clamp;

        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(final);
        return result;
    }

    /// <summary>ห่อ Texture2D เป็น Sprite เต็มภาพ (จุดหมุนกึ่งกลาง)</summary>
    public static Sprite ToSprite(Texture2D texture)
    {
        return texture == null
            ? null
            : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}
