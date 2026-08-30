using UnityEngine;
using System.Collections.Generic;

public class InstantiateEnviromentObject : MonoBehaviour
{
    [SerializeField]protected List<GameObject> enviromentObjects = new List<GameObject>();

    // Terrain-painted trees (native "Paint Trees" or VegetationSpawner) never instantiate this
    // prefab as a real GameObject at runtime - the terrain (and this project's custom
    // InstancedTreeRenderer) draws tree instances straight from TerrainData, so Start() below
    // never fires for those. TerrainTreeEnviromentSpawner covers that case by reading
    // EnviromentObjects off the prototype prefab directly instead of relying on this Start().
    public IReadOnlyList<GameObject> EnviromentObjects => enviromentObjects;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SpawnRandom(transform.position, transform.rotation);
        Destroy(gameObject); // Destroy the empty game object after instantiating the enviroment object
    }

    // Randomly selects one of enviromentObjects and instantiates it at the given world
    // position/rotation. Exposed so TerrainTreeEnviromentSpawner can reuse the same selection
    // logic for prototype prefabs that never get their own Start() called.
    public GameObject SpawnRandom(Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (enviromentObjects.Count == 0) return null;

        int randomIndex = Random.Range(0, enviromentObjects.Count);
        GameObject selectedObject = enviromentObjects[randomIndex];
        return Instantiate(selectedObject, position, rotation, parent);
    }

}
