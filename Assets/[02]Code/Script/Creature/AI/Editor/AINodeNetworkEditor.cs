using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AINodeNetwork))]
public class AINodeNetworkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AINodeNetwork network = (AINodeNetwork)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("จำนวนโหนดตอนนี้", network.NodeCount.ToString());

        if (GUILayout.Button("Generate Nodes"))
        {
            Undo.RecordObject(network, "Generate AI Nodes");
            network.GenerateNodes();
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(network.gameObject.scene);
        }
    }
}
