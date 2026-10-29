using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบุผิวน้ำของซีน (วางบน GameObject น้ำที่มี Renderer) แล้วถามได้จากทุกที่ว่าจุดหนึ่ง "อยู่ใต้น้ำ" ไหม
/// ใช้ความสูงผิวน้ำเทียบกับความสูง Terrain ตรงจุดนั้น (น้ำตื้น เดินลุยได้ NavMesh ผ่านน้ำ แต่ไม่ใช่พื้นหญ้า)
/// เช่น CreatureAI ใช้ห้ามกินหญ้ากลางน้ำ
/// </summary>
[DisallowMultipleComponent]
public class WaterSurface : MonoBehaviour
{
    [Tooltip("ลึกกว่านี้ (เมตร) ถึงนับว่า \"อยู่ในน้ำ\" กันขอบน้ำที่ตื้นเกือบเสมอพื้นถูกนับเป็นน้ำ")]
    public float minDepth = 0.05f;

    private static readonly List<WaterSurface> all = new List<WaterSurface>();

    private Bounds bounds;

    private void OnEnable()
    {
        Renderer r = GetComponent<Renderer>();
        bounds = r != null ? r.bounds : new Bounds(transform.position, Vector3.zero);
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    /// <summary>ความลึกของน้ำ ณ จุดนั้น (เมตร) 0 = ไม่มีน้ำ</summary>
    public static float GetDepth(Vector3 position)
    {
        float best = 0f;
        foreach (WaterSurface w in all)
        {
            if (position.x < w.bounds.min.x || position.x > w.bounds.max.x ||
                position.z < w.bounds.min.z || position.z > w.bounds.max.z) continue;

            float ground = GroundHeight(position);
            float depth = w.bounds.max.y - ground;
            if (depth >= w.minDepth && depth > best) best = depth;
        }
        return best;
    }

    public static bool IsInWater(Vector3 position) => GetDepth(position) > 0f;

    /// <summary>มีน้ำ ณ จุด (x, z) นี้ไหม — ได้ความสูงผิวน้ำ (พิกัดโลก) กลับมาด้วย ใช้วางจุดเสียงน้ำ</summary>
    public static bool TryGetSurface(Vector3 position, out float surfaceY)
    {
        surfaceY = 0f;
        float best = 0f;
        foreach (WaterSurface w in all)
        {
            if (position.x < w.bounds.min.x || position.x > w.bounds.max.x ||
                position.z < w.bounds.min.z || position.z > w.bounds.max.z) continue;

            float depth = w.bounds.max.y - GroundHeight(position);
            if (depth >= w.minDepth && depth > best)
            {
                best = depth;
                surfaceY = w.bounds.max.y;
            }
        }
        return best > 0f;
    }

    // พื้นใต้น้ำคือ Terrain (ถ้าไม่มี Terrain ตรงจุดนั้น ถือว่าไม่มีน้ำ)
    private static float GroundHeight(Vector3 position)
    {
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 local = position - t.transform.position;
            Vector3 size = t.terrainData.size;
            if (local.x < 0f || local.x > size.x || local.z < 0f || local.z > size.z) continue;
            return t.SampleHeight(position) + t.transform.position.y;
        }
        return float.PositiveInfinity;
    }
}
