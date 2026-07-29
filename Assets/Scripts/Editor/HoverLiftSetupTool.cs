#if UNITY_EDITOR
using DG.Tweening;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Editor window that sets up, tweaks, or reverts the hierarchy required by HoverLiftEffect
/// on the currently selected GameObject(s).
///
/// Setup creates a "Visual" child pivoted at the combined renderer bounds center, moves the
/// mesh under it (compensating so nothing visually shifts), ensures a Collider and an
/// XRSimpleInteractable exist on the root (which stays static), and adds HoverLiftEffect
/// configured with the values set in this window.
///
/// Open it via Window > XR > Hover Lift Setup.
/// Place this script inside an "Editor" folder (e.g. Assets/Editor/).
/// </summary>
public class HoverLiftSetupWindow : EditorWindow
{
    private enum ColliderChoice
    {
        Box,
        Mesh
    }

    private const string VisualChildName = "Visual";
    private const string MeshHolderChildName = "MeshHolder";

    // Collider settings
    private ColliderChoice _colliderChoice = ColliderChoice.Box;
    private bool _convexMeshCollider = true;

    // HoverLiftEffect settings, mirroring the component's own defaults.
    private float _liftHeight = 0.1f;
    private float _liftDuration = 0.25f;
    private float _dropDuration = 0.2f;
    private Ease _liftEase = Ease.OutCubic;
    private Ease _dropEase = Ease.InCubic;
    private bool _addFloatingRotation = false;
    private float _rotationDuration = 4f;

    [MenuItem("Window/XR/Hover Lift Setup")]
    private static void OpenWindow()
    {
        HoverLiftSetupWindow window = GetWindow<HoverLiftSetupWindow>();
        window.titleContent = new GUIContent("Hover Lift Setup");
        window.minSize = new Vector2(300f, 380f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Collider settings", EditorStyles.boldLabel);
        _colliderChoice = (ColliderChoice)EditorGUILayout.EnumPopup("Collider Type", _colliderChoice);

        if (_colliderChoice == ColliderChoice.Mesh)
        {
            _convexMeshCollider = EditorGUILayout.Toggle(
                new GUIContent("Convex", "Required if this collider ever needs to work as a trigger " +
                                          "or with a non-kinematic Rigidbody (e.g. XRGrabInteractable " +
                                          "with physics-based movement)."),
                _convexMeshCollider);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hover Lift Effect settings", EditorStyles.boldLabel);
        _liftHeight = EditorGUILayout.FloatField("Lift Height", _liftHeight);
        _liftDuration = EditorGUILayout.FloatField("Lift Duration", _liftDuration);
        _dropDuration = EditorGUILayout.FloatField("Drop Duration", _dropDuration);
        _liftEase = (Ease)EditorGUILayout.EnumPopup("Lift Ease", _liftEase);
        _dropEase = (Ease)EditorGUILayout.EnumPopup("Drop Ease", _dropEase);
        _addFloatingRotation = EditorGUILayout.Toggle("Add Floating Rotation", _addFloatingRotation);
        if (_addFloatingRotation)
        {
            _rotationDuration = EditorGUILayout.FloatField("Rotation Duration", _rotationDuration);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);

        GameObject[] selection = Selection.gameObjects;
        if (selection == null || selection.Length == 0)
        {
            EditorGUILayout.HelpBox("Select one or more GameObjects in the Hierarchy.", MessageType.Info);
        }
        else
        {
            foreach (GameObject go in selection)
            {
                EditorGUILayout.LabelField("• " + go.name);
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(selection == null || selection.Length == 0))
        {
            if (GUILayout.Button("Setup Hover Lift Effect", GUILayout.Height(30f)))
            {
                foreach (GameObject target in selection)
                {
                    SetupHoverLift(target, _colliderChoice, _convexMeshCollider, BuildSettings());
                }
            }

            if (GUILayout.Button("Apply Settings Only (existing setup)", GUILayout.Height(22f)))
            {
                foreach (GameObject target in selection)
                {
                    ApplySettingsOnly(target, BuildSettings());
                }
            }

            GUILayout.Space(6f);

            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("Revert Setup", GUILayout.Height(26f)))
            {
                foreach (GameObject target in selection)
                {
                    RevertSetup(target);
                }
            }
            GUI.backgroundColor = Color.white;
        }
    }

    private void OnSelectionChange()
    {
        Repaint();
    }

    private HoverLiftSettings BuildSettings()
    {
        return new HoverLiftSettings
        {
            liftHeight = _liftHeight,
            liftDuration = _liftDuration,
            dropDuration = _dropDuration,
            liftEase = _liftEase,
            dropEase = _dropEase,
            addFloatingRotation = _addFloatingRotation,
            rotationDuration = _rotationDuration
        };
    }

    private struct HoverLiftSettings
    {
        public float liftHeight;
        public float liftDuration;
        public float dropDuration;
        public Ease liftEase;
        public Ease dropEase;
        public bool addFloatingRotation;
        public float rotationDuration;
    }

    private static void ApplyLiftSettings(HoverLiftEffect lift, HoverLiftSettings settings)
    {
        SerializedObject serialized = new SerializedObject(lift);
        serialized.FindProperty("liftHeight").floatValue = settings.liftHeight;
        serialized.FindProperty("liftDuration").floatValue = settings.liftDuration;
        serialized.FindProperty("dropDuration").floatValue = settings.dropDuration;
        serialized.FindProperty("liftEase").enumValueIndex = (int)settings.liftEase;
        serialized.FindProperty("dropEase").enumValueIndex = (int)settings.dropEase;
        serialized.FindProperty("addFloatingRotation").boolValue = settings.addFloatingRotation;
        serialized.FindProperty("rotationDuration").floatValue = settings.rotationDuration;
        serialized.ApplyModifiedProperties();
    }

    private static void ApplySettingsOnly(GameObject root, HoverLiftSettings settings)
    {
        HoverLiftEffect lift = root.GetComponent<HoverLiftEffect>();
        if (lift == null)
        {
            Debug.LogWarning($"'{root.name}' has no HoverLiftEffect component. Run Setup first.", root);
            return;
        }

        Undo.RecordObject(lift, "Apply Hover Lift Settings");
        ApplyLiftSettings(lift, settings);
        EditorUtility.SetDirty(lift);
        Debug.Log($"Hover Lift settings applied on '{root.name}'.", root);
    }

    private static void SetupHoverLift(GameObject root, ColliderChoice colliderChoice, bool convexMeshCollider,
        HoverLiftSettings settings)
    {
        Transform existingVisual = root.transform.Find(VisualChildName);
        if (existingVisual != null)
        {
            Debug.LogWarning($"'{root.name}' already has a '{VisualChildName}' child. Skipping to avoid " +
                              "breaking an existing setup. Use Revert Setup first if you want to redo this.", root);
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"No Renderer found under '{root.name}'. Cannot compute a visual center, aborting.", root);
            return;
        }

        Undo.SetCurrentGroupName("Setup Hover Lift Effect");
        int undoGroup = Undo.GetCurrentGroup();

        // Combined world bounds of every renderer under the root, used as the pivot center.
        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        // Create the Visual pivot, centered on the mesh bounds. This is what gets animated.
        GameObject visualGO = new GameObject(VisualChildName);
        Undo.RegisterCreatedObjectUndo(visualGO, "Setup Hover Lift Effect");
        Transform visual = visualGO.transform;
        visual.SetParent(root.transform, false);
        visual.position = worldBounds.center;
        visual.rotation = root.transform.rotation;

        // Case 1: mesh lives directly on the root (no children). Move the renderer components
        // onto a compensating child so the visible result does not shift.
        MeshFilter rootMeshFilter = root.GetComponent<MeshFilter>();
        MeshRenderer rootMeshRenderer = root.GetComponent<MeshRenderer>();

        // Grab a reference to the mesh now, before anything gets moved around, in case a Mesh Collider is requested.
        Mesh sourceMeshForCollider = rootMeshFilter != null
            ? rootMeshFilter.sharedMesh
            : renderers[0].GetComponent<MeshFilter>()?.sharedMesh;

        bool meshWasOnRoot = rootMeshFilter != null && rootMeshRenderer != null;

        if (meshWasOnRoot)
        {
            GameObject meshHolderGO = new GameObject(MeshHolderChildName);
            Undo.RegisterCreatedObjectUndo(meshHolderGO, "Setup Hover Lift Effect");
            Transform meshHolder = meshHolderGO.transform;
            meshHolder.SetParent(visual, false);
            // Compensate: put the mesh holder back exactly where the root originally was,
            // so the mesh renders in the same place even though Visual is offset to the bounds center.
            meshHolder.SetPositionAndRotation(root.transform.position, root.transform.rotation);

            ComponentUtility.CopyComponent(rootMeshFilter);
            ComponentUtility.PasteComponentAsNew(meshHolderGO);
            ComponentUtility.CopyComponent(rootMeshRenderer);
            ComponentUtility.PasteComponentAsNew(meshHolderGO);

            Undo.DestroyObjectImmediate(rootMeshRenderer);
            Undo.DestroyObjectImmediate(rootMeshFilter);
        }
        else
        {
            // Case 2: mesh(es) already live on children. Reparent them under Visual;
            // SetParent with worldPositionStays = true keeps their world position/rotation intact.
            Transform[] originalChildren = new Transform[root.transform.childCount];
            int count = 0;
            foreach (Transform child in root.transform)
            {
                if (child == visual)
                {
                    continue;
                }
                originalChildren[count] = child;
                count++;
            }

            for (int i = 0; i < count; i++)
            {
                Undo.SetTransformParent(originalChildren[i], visual, "Setup Hover Lift Effect");
            }
        }

        // Ensure a Collider exists on the root for hover detection. The root never moves,
        // so the ray keeps hovering it even while the Visual child lifts and rotates.
        Collider existingCollider = root.GetComponent<Collider>();
        bool colliderWasAdded = existingCollider == null;
        HoverLiftSetupInfo.AddedColliderType addedColliderType = HoverLiftSetupInfo.AddedColliderType.None;

        if (colliderWasAdded)
        {
            if (colliderChoice == ColliderChoice.Mesh)
            {
                if (sourceMeshForCollider == null)
                {
                    Debug.LogWarning($"No mesh found to build a Mesh Collider on '{root.name}', " +
                                      "falling back to a Box Collider instead.", root);
                    AddBoxCollider(root, worldBounds);
                    addedColliderType = HoverLiftSetupInfo.AddedColliderType.Box;
                }
                else
                {
                    MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(root);
                    meshCollider.sharedMesh = sourceMeshForCollider;
                    meshCollider.convex = convexMeshCollider;
                    addedColliderType = HoverLiftSetupInfo.AddedColliderType.Mesh;
                }
            }
            else
            {
                AddBoxCollider(root, worldBounds);
                addedColliderType = HoverLiftSetupInfo.AddedColliderType.Box;
            }
        }

        // Ensure an interactable exists.
        XRBaseInteractable interactable = root.GetComponent<XRBaseInteractable>();
        bool interactableWasAdded = interactable == null;
        if (interactableWasAdded)
        {
            interactable = Undo.AddComponent<XRSimpleInteractable>(root);
        }

        // Add and configure HoverLiftEffect.
        HoverLiftEffect lift = root.GetComponent<HoverLiftEffect>();
        if (lift == null)
        {
            lift = Undo.AddComponent<HoverLiftEffect>(root);
        }

        SerializedObject serializedLift = new SerializedObject(lift);
        SerializedProperty visualProp = serializedLift.FindProperty("visualTransform");
        if (visualProp != null)
        {
            visualProp.objectReferenceValue = visual;
            serializedLift.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("Could not find 'visualTransform' field on HoverLiftEffect. " +
                              "Assign it manually in the inspector.", lift);
        }

        ApplyLiftSettings(lift, settings);

        // Record what this run actually added, so Revert Setup can undo precisely that later,
        // even after the Undo history has been cleared (e.g. after closing and reopening Unity).
        HoverLiftSetupInfo info = Undo.AddComponent<HoverLiftSetupInfo>(root);
        info.meshWasOnRoot = meshWasOnRoot;
        info.colliderWasAdded = colliderWasAdded;
        info.addedColliderType = addedColliderType;
        info.interactableWasAdded = interactableWasAdded;

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(root);
        Debug.Log($"Hover Lift setup complete on '{root.name}'.", root);
    }

    private static void RevertSetup(GameObject root)
    {
        HoverLiftSetupInfo info = root.GetComponent<HoverLiftSetupInfo>();
        if (info == null)
        {
            Debug.LogWarning($"No Hover Lift setup detected on '{root.name}' (no {nameof(HoverLiftSetupInfo)} " +
                              "found), nothing to revert.", root);
            return;
        }

        Undo.SetCurrentGroupName("Revert Hover Lift Effect");
        int undoGroup = Undo.GetCurrentGroup();

        Transform visual = root.transform.Find(VisualChildName);
        if (visual != null)
        {
            if (info.meshWasOnRoot)
            {
                Transform meshHolder = visual.Find(MeshHolderChildName);
                if (meshHolder != null)
                {
                    MeshFilter meshFilter = meshHolder.GetComponent<MeshFilter>();
                    MeshRenderer meshRenderer = meshHolder.GetComponent<MeshRenderer>();
                    if (meshFilter != null && meshRenderer != null)
                    {
                        ComponentUtility.CopyComponent(meshFilter);
                        ComponentUtility.PasteComponentAsNew(root);
                        ComponentUtility.CopyComponent(meshRenderer);
                        ComponentUtility.PasteComponentAsNew(root);
                    }

                    Undo.DestroyObjectImmediate(meshHolder.gameObject);
                }
            }
            else
            {
                // Move Visual's children back onto the root, preserving their world transforms.
                Transform[] visualChildren = new Transform[visual.childCount];
                int count = 0;
                foreach (Transform child in visual)
                {
                    visualChildren[count] = child;
                    count++;
                }

                for (int i = 0; i < count; i++)
                {
                    Undo.SetTransformParent(visualChildren[i], root.transform, "Revert Hover Lift Effect");
                }
            }

            Undo.DestroyObjectImmediate(visual.gameObject);
        }

        HoverLiftEffect lift = root.GetComponent<HoverLiftEffect>();
        if (lift != null)
        {
            Undo.DestroyObjectImmediate(lift);
        }

        if (info.colliderWasAdded)
        {
            if (info.addedColliderType == HoverLiftSetupInfo.AddedColliderType.Box)
            {
                BoxCollider box = root.GetComponent<BoxCollider>();
                if (box != null)
                {
                    Undo.DestroyObjectImmediate(box);
                }
            }
            else if (info.addedColliderType == HoverLiftSetupInfo.AddedColliderType.Mesh)
            {
                MeshCollider meshCollider = root.GetComponent<MeshCollider>();
                if (meshCollider != null)
                {
                    Undo.DestroyObjectImmediate(meshCollider);
                }
            }
        }

        if (info.interactableWasAdded)
        {
            XRBaseInteractable interactable = root.GetComponent<XRBaseInteractable>();
            if (interactable != null)
            {
                Undo.DestroyObjectImmediate(interactable);
            }
        }

        Undo.DestroyObjectImmediate(info);

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(root);
        Debug.Log($"Hover Lift setup reverted on '{root.name}'.", root);
    }

    private static void AddBoxCollider(GameObject root, Bounds worldBounds)
    {
        BoxCollider box = Undo.AddComponent<BoxCollider>(root);
        box.center = root.transform.InverseTransformPoint(worldBounds.center);

        Vector3 lossyScale = root.transform.lossyScale;
        box.size = new Vector3(
            worldBounds.size.x / Mathf.Max(Mathf.Abs(lossyScale.x), 0.0001f),
            worldBounds.size.y / Mathf.Max(Mathf.Abs(lossyScale.y), 0.0001f),
            worldBounds.size.z / Mathf.Max(Mathf.Abs(lossyScale.z), 0.0001f));
    }
}
#endif
