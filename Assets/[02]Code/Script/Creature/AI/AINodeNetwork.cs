using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// สร้างตาราง node กระจายบนพื้น (อิง NavMesh ที่ bake ไว้แล้ว) แบบปรับระยะห่างได้
// เพราะอิง NavMesh ตรงๆ พื้นที่ชันเกิน/ใต้น้ำ/โดนสิ่งกีดขวางบัง จะถูกกรองออกให้อัตโนมัติ ไม่ต้องเช็คเงื่อนไขซ้ำเอง
// ไว้ให้ AI (เช่น CreatureAI) ดึง node ใกล้ตัวไปเป็นจุดหมายตอน Idle/Walking แทนการสุ่มจุดอิสระ
public class AINodeNetwork : MonoBehaviour
{
    [Header("Generation Settings")]
    [Tooltip("ระยะห่างระหว่างโหนดแต่ละจุด (หน่วย world) ยิ่งน้อยยิ่งได้โหนดถี่/เยอะ")]
    [SerializeField] private float nodeSpacing = 5f;

    [Tooltip("รัศมีค้นหา NavMesh รอบแต่ละจุดกริด")]
    [SerializeField] private float sampleRadius = 2f;

    [SerializeField] private List<Vector3> nodes = new List<Vector3>();

    public IReadOnlyList<Vector3> Nodes => nodes;
    public int NodeCount => nodes.Count;

    public void GenerateNodes()
    {
        nodes.Clear();

        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length == 0)
        {
            Debug.LogWarning("[AINodeNetwork] ไม่พบ Terrain ในซีน ไม่สามารถสร้างโหนดได้");
            return;
        }

        foreach (Terrain terrain in terrains)
        {
            GenerateForTerrain(terrain);
        }

        Debug.Log($"[AINodeNetwork] สร้างโหนดเสร็จ {nodes.Count} จุด (ระยะห่าง {nodeSpacing} หน่วย)");
    }

    private void GenerateForTerrain(Terrain terrain)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        for (float x = origin.x; x <= origin.x + size.x; x += nodeSpacing)
        {
            for (float z = origin.z; z <= origin.z + size.z; z += nodeSpacing)
            {
                float terrainHeight = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
                Vector3 probe = new Vector3(x, terrainHeight, z);

                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)
                    && !IsTooCloseToExisting(hit.position))
                {
                    nodes.Add(hit.position);
                }
            }
        }
    }

    private bool IsTooCloseToExisting(Vector3 point)
    {
        float minDistSqr = (nodeSpacing * 0.5f) * (nodeSpacing * 0.5f);
        foreach (Vector3 n in nodes)
        {
            if ((n - point).sqrMagnitude < minDistSqr) return true;
        }
        return false;
    }

    /// <summary>สุ่มโหนดจากทั้งเครือข่าย ใช้ตอนหา fallback ถ้าแถวนั้นไม่มีโหนดใกล้ตัวเลย</summary>
    public Vector3 GetRandomNode()
    {
        if (nodes.Count == 0) return transform.position;
        return nodes[Random.Range(0, nodes.Count)];
    }

    /// <summary>สุ่มโหนดที่อยู่ในรัศมีที่กำหนดจากจุด origin ถ้าไม่มีเลยจะ fallback ไปสุ่มทั้งเครือข่ายแทน</summary>
    public Vector3 GetRandomNodeNear(Vector3 origin, float radius)
    {
        List<Vector3> candidates = new List<Vector3>();
        float radiusSqr = radius * radius;
        foreach (Vector3 n in nodes)
        {
            if ((n - origin).sqrMagnitude <= radiusSqr) candidates.Add(n);
        }
        if (candidates.Count == 0) return GetRandomNode();
        return candidates[Random.Range(0, candidates.Count)];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        foreach (Vector3 n in nodes)
        {
            Gizmos.DrawWireSphere(n, 0.4f);
        }
    }
}
