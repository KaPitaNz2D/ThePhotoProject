using UnityEngine;
using System.Collections.Generic;

// Bridges InstantiateEnviromentObject into terrain-painted trees. Whether trees were painted with
// Unity's native "Paint Trees" tool or VegetationSpawner, they only ever exist as TreeInstance
// entries in TerrainData - Unity does not instantiate the prototype prefab as a real GameObject to
// draw them, and this project's InstancedTreeRenderer draws them procedurally too. That means an
// InstantiateEnviromentObject on a tree prototype prefab never has its Start() called.
//
// At runtime, this walks the terrain's tree instances once, and for every prototype whose prefab
// carries an InstantiateEnviromentObject, spawns a real object at that instance's world
// position/rotation/scale, then removes those instances from TerrainData so they aren't also
// drawn as placeholder trees.
[RequireComponent(typeof(Terrain))]
public class TerrainTreeEnviromentSpawner : MonoBehaviour
{
    private void Start()
    {
        Terrain terrain = GetComponent<Terrain>();
        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null) return;

        TreePrototype[] prototypes = terrainData.treePrototypes;
        var spawners = new Dictionary<int, InstantiateEnviromentObject>();
        for (int i = 0; i < prototypes.Length; i++)
        {
            GameObject prefab = prototypes[i].prefab;
            if (prefab == null) continue;

            InstantiateEnviromentObject spawner = prefab.GetComponent<InstantiateEnviromentObject>();
            if (spawner != null) spawners[i] = spawner;
        }
        if (spawners.Count == 0) return;

        TreeInstance[] instances = terrainData.treeInstances;
        var remaining = new List<TreeInstance>(instances.Length);
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        foreach (TreeInstance instance in instances)
        {
            if (!spawners.TryGetValue(instance.prototypeIndex, out InstantiateEnviromentObject spawner))
            {
                remaining.Add(instance);
                continue;
            }

            Vector3 worldPos = terrainPos + Vector3.Scale(instance.position, terrainSize);
            Quaternion worldRot = Quaternion.Euler(0f, instance.rotation * Mathf.Rad2Deg, 0f);

            GameObject spawned = spawner.SpawnRandom(worldPos, worldRot, transform);
            if (spawned != null)
            {
                spawned.transform.localScale = new Vector3(instance.widthScale, instance.heightScale, instance.widthScale);
            }
        }

        // Only rewrite TerrainData when something was actually converted, to avoid needlessly
        // dirtying terrains that have no InstantiateEnviromentObject prototypes at all.
        if (remaining.Count != instances.Length)
        {
            terrainData.SetTreeInstances(remaining.ToArray(), false);
        }
    }
}
