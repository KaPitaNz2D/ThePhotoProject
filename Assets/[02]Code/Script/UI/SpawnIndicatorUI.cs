using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สัญลักษณ์ตกใจ (!) บนจอตอนสัตว์เกิดใหม่ (ฟัง CreatureSpawner.OnCreatureSpawned ทุกตัวในซีน)
///   - ลอยเหนือหัวสัตว์ ตามตำแหน่งสัตว์ไปเรื่อยๆ และค่อยๆ เฟดหายใน fadeDuration วินาที
///   - สัตว์อยู่นอกจอ/หลังกล้อง -> ไปติดขอบจอฝั่งที่สัตว์อยู่ (เว้น screenMargin)
/// วางบน RectTransform เต็มจอใน Canvas แบบ Screen Space - Overlay — indicatorTemplate = Image ลูกที่ปิดไว้ ใช้ Clone
/// </summary>
public class SpawnIndicatorUI : MonoBehaviour
{
    [Tooltip("Image ของสัญลักษณ์ (ปิดไว้) — Clone ออกมาหนึ่งอันต่อสัตว์หนึ่งตัว")]
    public Image indicatorTemplate;
    [Tooltip("เวลา (วินาที) ที่สัญลักษณ์เฟดจากชัดสุดจนหาย")]
    public float fadeDuration = 5f;
    [Tooltip("ยกสัญลักษณ์ขึ้นเหนือขอบบนของตัวสัตว์ (เมตร)")]
    public float heightOffset = 0.8f;
    [Tooltip("ระยะกันขอบจอ (px ของ Canvas)")]
    public float screenMargin = 48f;

    private class Indicator
    {
        public Transform creature;
        public float height; // ความสูงขอบบนของตัวสัตว์จาก Pivot วัดตอนเกิด
        public RectTransform rect;
        public Image image;
        public float startTime;
    }

    private readonly List<Indicator> indicators = new List<Indicator>();
    private CreatureSpawner[] spawners;

    private void Start()
    {
        indicatorTemplate.gameObject.SetActive(false);
        spawners = FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None);
        foreach (CreatureSpawner spawner in spawners) spawner.OnCreatureSpawned += HandleCreatureSpawned;
    }

    private void OnDestroy()
    {
        if (spawners == null) return;
        foreach (CreatureSpawner spawner in spawners)
        {
            if (spawner != null) spawner.OnCreatureSpawned -= HandleCreatureSpawned;
        }
    }

    private void HandleCreatureSpawned(GameObject creature, Vector3 point) => Show(creature.transform);

    public void Show(Transform creature)
    {
        Image image = Instantiate(indicatorTemplate, indicatorTemplate.transform.parent);
        image.gameObject.SetActive(true);

        float height = 1.5f;
        Renderer[] renderers = creature.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            height = bounds.max.y - creature.position.y;
        }

        indicators.Add(new Indicator
        {
            creature = creature,
            height = height + heightOffset,
            rect = image.rectTransform,
            image = image,
            startTime = Time.time,
        });
        UpdateIndicator(indicators[indicators.Count - 1]);
    }

    private void LateUpdate()
    {
        for (int i = indicators.Count - 1; i >= 0; i--)
        {
            Indicator indicator = indicators[i];
            float age = Time.time - indicator.startTime;
            if (indicator.creature == null || age >= fadeDuration)
            {
                Destroy(indicator.image.gameObject);
                indicators.RemoveAt(i);
                continue;
            }
            UpdateIndicator(indicator);
        }
    }

    private void UpdateIndicator(Indicator indicator)
    {
        Camera cam = Camera.main;
        RectTransform area = indicator.rect.parent as RectTransform;
        if (cam == null || area == null) return;

        Color color = indicator.image.color;
        color.a = 1f - Mathf.Clamp01((Time.time - indicator.startTime) / fadeDuration);
        indicator.image.color = color;

        Vector2 half = area.rect.size * 0.5f;
        Vector3 viewport = cam.WorldToViewportPoint(indicator.creature.position + Vector3.up * indicator.height);
        Vector2 local = new Vector2((viewport.x - 0.5f) * half.x * 2f, (viewport.y - 0.5f) * half.y * 2f);

        // หลังกล้อง: Viewport กลับด้าน -> กลับคืนแล้วดันไปไกลๆ ให้ไปติดขอบจอฝั่งที่สัตว์อยู่
        if (viewport.z < 0f)
        {
            local = -local;
            if (local.sqrMagnitude < 1f) local = Vector2.down;
            local = local.normalized * 100000f;
        }

        // อยู่นอกจอ -> หดเข้าหากลางจอตามแนวเดิมจนติดขอบ (ทิศจากกลางจอยังชี้ไปทางสัตว์)
        Vector2 limit = half - Vector2.one * screenMargin;
        float scale = 1f;
        if (Mathf.Abs(local.x) > limit.x) scale = Mathf.Min(scale, limit.x / Mathf.Abs(local.x));
        if (Mathf.Abs(local.y) > limit.y) scale = Mathf.Min(scale, limit.y / Mathf.Abs(local.y));

        indicator.rect.anchorMin = indicator.rect.anchorMax = new Vector2(0.5f, 0.5f);
        indicator.rect.anchoredPosition = local * scale;
    }
}
