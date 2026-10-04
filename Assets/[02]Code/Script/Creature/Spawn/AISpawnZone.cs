using System;
using System.Collections.Generic;
using UnityEngine;

// วงกลม (ระนาบ XZ) กำหนดพื้นที่ที่ Spawn ได้ + กำหนดว่าสัตว์ชนิดไหนเกิดในพื้นที่นี้
// ลาก Prefab สัตว์ใส่ช่อง Creatures ของโซนได้เลย — โซนที่ไม่มีสัตว์สักตัวจะไม่ถูกใช้ Spawn
public class AISpawnZone : MonoBehaviour
{
    [Serializable]
    public class SpawnEntry
    {
        public GameObject creaturePrefab;

        [Tooltip("น้ำหนักการสุ่ม ยิ่งมากยิ่งเกิดบ่อย (เช่น 3 กับ 1 = ชนิดแรกเกิดบ่อยกว่า 3 เท่า) ใส่ 0 = ปิดชั่วคราว")]
        public float weight = 1f;
    }

    [Tooltip("รัศมีของพื้นที่ Spawn (หน่วย world) วัดบนระนาบ XZ ไม่สนใจความสูง")]
    [SerializeField] private float radius = 30f;

    [Tooltip("สัตว์ที่เกิดในโซนนี้ ถ้าใส่หลายชนิดจะสุ่มตามน้ำหนัก (Weight)")]
    [SerializeField] private List<SpawnEntry> creatures = new List<SpawnEntry>();

    public bool HasCreatures
    {
        get
        {
            foreach (SpawnEntry entry in creatures)
            {
                if (IsValid(entry)) return true;
            }
            return false;
        }
    }

    public bool Contains(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= radius * radius;
    }

    public bool TryPickCreature(out GameObject prefab)
    {
        float totalWeight = 0f;
        foreach (SpawnEntry entry in creatures)
        {
            if (IsValid(entry)) totalWeight += entry.weight;
        }

        prefab = null;
        if (totalWeight <= 0f) return false;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        foreach (SpawnEntry entry in creatures)
        {
            if (!IsValid(entry)) continue;

            prefab = entry.creaturePrefab;
            roll -= entry.weight;
            if (roll <= 0f) break;
        }
        return true;
    }

    private static bool IsValid(SpawnEntry entry)
    {
        return entry != null && entry.creaturePrefab != null && entry.weight > 0f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = HasCreatures ? new Color(1f, 0.6f, 0f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.9f);

        const int segments = 48;
        Vector3 previous = transform.position + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
