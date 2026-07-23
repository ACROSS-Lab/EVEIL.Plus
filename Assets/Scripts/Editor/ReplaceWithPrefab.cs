using UnityEngine;
using UnityEditor;

public class ReplaceWithPrefab : EditorWindow
{
    private GameObject prefabToUse;
    private bool keepPosition = true;
    private bool keepRotation = true;
    private bool keepScale = false;
    private bool keepName = false;

    [MenuItem("Tools/Replace With Prefab")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceWithPrefab>("Replace With Prefab");
    }

    private void OnGUI()
    {
        GUILayout.Label("Replace Selected Objects", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        prefabToUse = (GameObject)EditorGUILayout.ObjectField(
            "Prefab", prefabToUse, typeof(GameObject), false
        );

        EditorGUILayout.Space(8);
        GUILayout.Label("Options", EditorStyles.boldLabel);
        keepPosition = EditorGUILayout.Toggle("Keep Position", keepPosition);
        keepRotation = EditorGUILayout.Toggle("Keep Rotation", keepRotation);
        keepScale = EditorGUILayout.Toggle("Keep Scale", keepScale);
        keepName = EditorGUILayout.Toggle("Keep Name", keepName);

        EditorGUILayout.Space(10);

        int count = Selection.gameObjects.Length;
        EditorGUI.BeginDisabledGroup(prefabToUse == null || count == 0);

        string label = count == 0
            ? "Replace (no selection)"
            : $"Replace {count} object{(count > 1 ? "s" : "")}";

        if (GUILayout.Button(label, GUILayout.Height(36)))
            ReplaceSelected();

        EditorGUI.EndDisabledGroup();

        if (prefabToUse == null)
        {
            EditorGUILayout.HelpBox("Assign a prefab above.", MessageType.Warning);
        }
        else if (count == 0)
        {
            EditorGUILayout.HelpBox("Select one or more objects in the scene.", MessageType.Info);
        }
    }

    private void ReplaceSelected()
    {
        GameObject[] selected = Selection.gameObjects;
        Undo.SetCurrentGroupName("Replace With Prefab");
        int group = Undo.GetCurrentGroup();

        foreach (GameObject obj in selected)
        {
            // Instantiate the prefab
            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabToUse, obj.transform.parent);
            Undo.RegisterCreatedObjectUndo(newObj, "Create Prefab Instance");

            // Apply transforms
            newObj.transform.position = keepPosition ? obj.transform.position : prefabToUse.transform.position;
            newObj.transform.rotation = keepRotation ? obj.transform.rotation : prefabToUse.transform.rotation;
            newObj.transform.localScale = keepScale ? obj.transform.localScale : prefabToUse.transform.localScale;

            // Sibling index so the new object sits where the old one was in the hierarchy
            newObj.transform.SetSiblingIndex(obj.transform.GetSiblingIndex());

            if (keepName)
                newObj.name = obj.name;

            Undo.DestroyObjectImmediate(obj);
        }

        Undo.CollapseUndoOperations(group);
    }
}