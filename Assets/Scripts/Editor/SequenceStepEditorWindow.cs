using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

public class SequenceStepEditorWindow : EditorWindow
{
    [SerializeField] private SequenceStep currentStep;
    private Editor cachedEditor;
    private Vector2 scrollPos;
    private bool isLocked = false;

    [MenuItem("Window/Sequences/Sequence Step Editor")]
    public static void Open()
    {
        var window = GetWindow<SequenceStepEditorWindow>("Sequence Step");
        window.minSize = new Vector2(360, 480);
        window.Show();
    }

    /// <summary>
    /// Automatically opens or focuses this window when double-clicking any SequenceStep asset in the Project view.
    /// </summary>
    [OnOpenAsset]
    public static bool OnOpenAsset(int instanceID)
    {
        var step = EditorUtility.EntityIdToObject(instanceID) as SequenceStep;
        if (step != null)
        {
            var window = GetWindow<SequenceStepEditorWindow>("Sequence Step");
            window.SetTarget(step);
            window.Show();
            return true;
        }
        return false;
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        if (currentStep != null)
        {
            CreateCachedEditor();
        }
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        DestroyCachedEditor();
    }

    private void OnUndoRedo()
    {
        Repaint();
    }

    private void OnSelectionChange()
    {
        // If locked, do not switch the target step when selecting other assets or GameObjects
        if (isLocked) return;

        if (Selection.activeObject is SequenceStep step)
        {
            SetTarget(step);
            Repaint();
        }
    }

    public void SetTarget(SequenceStep step)
    {
        if (currentStep == step && cachedEditor != null) return;

        currentStep = step;
        CreateCachedEditor();
    }

    private void CreateCachedEditor()
    {
        DestroyCachedEditor();
        if (currentStep != null)
        {
            cachedEditor = Editor.CreateEditor(currentStep);
        }
    }

    private void DestroyCachedEditor()
    {
        if (cachedEditor != null)
        {
            DestroyImmediate(cachedEditor);
            cachedEditor = null;
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (currentStep == null)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.HelpBox("No SequenceStep selected.\n\nSelect a SequenceStep asset in the Project view or drag one into the toolbar above.", MessageType.Info);
            return;
        }

        if (cachedEditor == null || cachedEditor.target != currentStep)
        {
            CreateCachedEditor();
        }

        // Draw the full inspector for SequenceStep (including NaughtyAttributes)
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (cachedEditor != null)
        {
            EditorGUI.BeginChangeCheck();

            cachedEditor.OnInspectorGUI();

            // When properties or dropdowns change, guarantee dirty flag and disk save
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(currentStep);
                AssetDatabase.SaveAssetIfDirty(currentStep);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Lock Toggle
        GUIContent lockContent = new GUIContent(
            isLocked ? "Locked" : "Unlocked",
            isLocked ? "Window is locked to this SequenceStep. Selecting other objects in the scene won't change it."
                     : "Window is unlocked. Selecting a SequenceStep in the Project view will display it here."
        );
        isLocked = GUILayout.Toggle(isLocked, lockContent, EditorStyles.toolbarButton, GUILayout.Width(85));

        // Save Asset Button
        if (currentStep != null)
        {
            bool isDirty = EditorUtility.IsDirty(currentStep);
            GUIContent saveContent = new GUIContent(isDirty ? "Save *" : "Save", isDirty ? "Asset has unsaved changes. Click to save now." : "Asset is saved to disk.");
            if (GUILayout.Button(saveContent, EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                EditorUtility.SetDirty(currentStep);
                AssetDatabase.SaveAssetIfDirty(currentStep);
            }
        }

        // Object Picker field in toolbar (stretches to fill remaining space)
        var newStep = (SequenceStep)EditorGUILayout.ObjectField(currentStep, typeof(SequenceStep), false, GUILayout.MinWidth(150), GUILayout.ExpandWidth(true));
        if (newStep != currentStep)
        {
            SetTarget(newStep);
        }

        EditorGUILayout.EndHorizontal();
    }
}
