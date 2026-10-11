// Tree Replacer created by Seta
// https://www.youtube.com/@SetaLevelDesign
// Licence: Creative Commons
// Modified: no key press / raycast. Trees with a matching replacement prefab are swapped in
// automatically while near the camera, so PhotoShooter's SphereCast can hit them.
using System.Collections.Generic;
using UnityEngine;

public class TreeReplacer : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain; //reference to the terrain
    public Camera playerCamera; //distance reference; falls back to Camera.main

    [Header("Performance")]
    public float cellSize = 10f; //size of grid cells
    [Tooltip("Trees closer than this to the camera become their replacement prefab. Keep >= PhotoShooter castDistance x maxZoomCastDistanceMultiplier or zoomed-in shots miss far trees.")]
    public float spawnDistance = 100f;
    [Tooltip("Replaced trees farther than this go back to the terrain. Forced above spawnDistance to avoid flicker.")]
    public float despawnDistance = 120f;
    public float scanInterval = 0.25f; //seconds between proximity scans

    [Header("PlayMode Options")]
    public bool restoreTrees = true; //option to restore trees when exiting Play Mode

    [System.Serializable]
    public class TreeReplacement
    {
        public string treeName; //name of the tree prefab in the terrain
        public GameObject replacementPrefab; //prefab that will replace this tree
    }

    [Header("Replacements")]
    public TreeReplacement[] replacements; //array of tree replacements defined in the inspector

    private TerrainData tData; //reference to terrain data (tree instances, heightmap)
    private TreeInstance[] allTrees; //every tree as it was at Start; terrain array is rebuilt from this minus hidden
    private bool[] hidden; //hidden[i] = allTrees[i] is currently a spawned prefab instead of a terrain tree
    private Dictionary<Vector3Int, List<TreeRef>> treeGrid; //only trees that have a replacement prefab
    private readonly List<TreeRef> activeSpawned = new List<TreeRef>(); //list of currently spawned replacements
    private float nextScanTime;

    public class TreeRef
    {
        public int index; //position in allTrees
        public TreeInstance original; //original tree instance data
        public Vector3 worldPos; //world position of the tree
        public GameObject prefab; //replacement prefab for this tree's type
        public GameObject spawnedGO; //spawned replacement, null while the tree lives in the terrain
    }

    void Start()
    {
        if (terrain == null || terrain.terrainData == null) //check if terrain and data exist
        {
            enabled = false; //disable script if not valid
            return;
        }

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null)
        {
            Debug.LogError("[TreeReplacer] No playerCamera assigned and no Camera.main found.", this);
            enabled = false;
            return;
        }

        tData = terrain.terrainData; //get terrain data reference

        if (restoreTrees) //use if restore option is enabled
        {
            tData = Instantiate(terrain.terrainData); //clone terrain data
            terrain.terrainData = tData; //assign cloned data to terrain
        }

        despawnDistance = Mathf.Max(despawnDistance, spawnDistance + cellSize);
        BuildTreeGrid(); //build the tree grid
    }

    void Update()
    {
        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanInterval;
        Scan();
    }

    private void BuildTreeGrid()
    {
        var prefabByName = new Dictionary<string, GameObject>();
        foreach (var r in replacements) //loop through defined replacements
        {
            if (!prefabByName.ContainsKey(r.treeName) && r.replacementPrefab != null) //avoid duplicates
                prefabByName.Add(r.treeName, r.replacementPrefab);
        }

        var protos = tData.treePrototypes;
        var prefabByProto = new GameObject[protos.Length]; //prototype index -> replacement prefab
        for (int i = 0; i < protos.Length; i++)
        {
            if (protos[i].prefab != null) prefabByName.TryGetValue(protos[i].prefab.name, out prefabByProto[i]);
        }

        allTrees = tData.treeInstances;
        hidden = new bool[allTrees.Length];
        treeGrid = new Dictionary<Vector3Int, List<TreeRef>>();

        for (int i = 0; i < allTrees.Length; i++) //loop through all trees
        {
            int proto = allTrees[i].prototypeIndex;
            if ((uint)proto >= (uint)prefabByProto.Length || prefabByProto[proto] == null) continue; //no replacement for this type

            var tr = new TreeRef
            {
                index = i,
                original = allTrees[i],
                worldPos = NormalizedToWorld(allTrees[i].position), //convert normalized pos to world pos
                prefab = prefabByProto[proto]
            };

            Vector3Int cell = WorldToCell(tr.worldPos); //determine which grid cell this tree belongs to
            if (!treeGrid.TryGetValue(cell, out var list))
            {
                list = new List<TreeRef>();
                treeGrid[cell] = list;
            }
            list.Add(tr);
        }
    }

    private Vector3 NormalizedToWorld(Vector3 normalizedPos) => Vector3.Scale(normalizedPos, tData.size) + terrain.transform.position; //convert normalized tree position to world position

    private Vector3Int WorldToCell(Vector3 pos) => new Vector3Int(Mathf.FloorToInt(pos.x / cellSize), 0, Mathf.FloorToInt(pos.z / cellSize)); //convert world pos to grid cell index

    private void Scan()
    {
        Vector3 camPos = playerCamera.transform.position;
        bool changed = false;

        float despawnSqr = despawnDistance * despawnDistance;
        for (int i = activeSpawned.Count - 1; i >= 0; i--) //loop backwards through active replacements
        {
            var tr = activeSpawned[i];
            if (tr.spawnedGO != null && (camPos - tr.worldPos).sqrMagnitude <= despawnSqr) continue; //still near enough

            if (tr.spawnedGO != null) Destroy(tr.spawnedGO);
            tr.spawnedGO = null;
            hidden[tr.index] = false; //tree goes back to the terrain
            activeSpawned.RemoveAt(i);
            changed = true;
        }

        float spawnSqr = spawnDistance * spawnDistance;
        int range = Mathf.CeilToInt(spawnDistance / cellSize);
        Vector3Int center = WorldToCell(camPos);
        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                if (!treeGrid.TryGetValue(new Vector3Int(center.x + x, 0, center.z + z), out var list)) continue;

                foreach (var tr in list)
                {
                    if (hidden[tr.index] || (camPos - tr.worldPos).sqrMagnitude > spawnSqr) continue;

                    Quaternion rot = Quaternion.Euler(0f, tr.original.rotation * Mathf.Rad2Deg, 0f); //convert terrain rotation to Quaternion
                    Vector3 scale = new Vector3(tr.original.widthScale, tr.original.heightScale, tr.original.widthScale); //get tree scaling

                    hidden[tr.index] = true;
                    tr.spawnedGO = Instantiate(tr.prefab, tr.worldPos, rot);
                    tr.spawnedGO.transform.localScale = Vector3.Scale(tr.spawnedGO.transform.localScale, scale); //apply terrain scaling to prefab
                    activeSpawned.Add(tr);
                    changed = true;
                }
            }
        }

        if (changed) PushTrees();
    }

    // One terrain write per scan, however many trees changed. Built from allTrees rather than
    // read back from the terrain, so no position matching; assumes nothing else edits the
    // terrain's trees while this runs.
    private void PushTrees()
    {
        var visible = new List<TreeInstance>(allTrees.Length);
        for (int i = 0; i < allTrees.Length; i++)
        {
            if (!hidden[i]) visible.Add(allTrees[i]);
        }
        tData.treeInstances = visible.ToArray();
    }
}
