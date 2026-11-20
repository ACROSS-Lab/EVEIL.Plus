// A placer dans un dossier "Editor" du projet Unity.
// Menu : Tools > Appliquer un materiau aux FBX
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ApplyMaterialToFbxWindow : EditorWindow
{
    private Material targetMaterial;
    private bool replaceAllSlots = true;
    private string onlyMaterialName = "";

    [MenuItem("Tools/Appliquer un materiau aux FBX")]
    private static void Open()
    {
        GetWindow<ApplyMaterialToFbxWindow>("Material FBX");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Selectionne les FBX dans la fenetre Project.\n" +
            "2. Choisis le materiau.\n" +
            "3. Clique sur Appliquer (equivalent du On Demand Remap).",
            MessageType.Info);

        targetMaterial = (Material)EditorGUILayout.ObjectField("Materiau", targetMaterial, typeof(Material), false);
        replaceAllSlots = EditorGUILayout.Toggle("Remplacer tous les slots", replaceAllSlots);

        using (new EditorGUI.DisabledScope(replaceAllSlots))
        {
            onlyMaterialName = EditorGUILayout.TextField("Nom du slot a remplacer", onlyMaterialName);
        }

        using (new EditorGUI.DisabledScope(targetMaterial == null))
        {
            if (GUILayout.Button("Appliquer aux FBX selectionnes"))
            {
                Apply();
            }
        }

        if (GUILayout.Button("Supprimer les remaps des FBX selectionnes"))
        {
            ClearRemaps();
        }
    }

    private static List<ModelImporter> GetSelectedImporters()
    {
        return Selection.GetFiltered<Object>(SelectionMode.Assets)
            .Select(o => AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o)) as ModelImporter)
            .Where(i => i != null)
            .Distinct()
            .ToList();
    }

    private void Apply()
    {
        var importers = GetSelectedImporters();
        if (importers.Count == 0)
        {
            Debug.LogWarning("Aucun FBX selectionne.");
            return;
        }

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var importer in importers)
            {
                var names = GetSourceMaterialNames(importer);

                if (!replaceAllSlots)
                {
                    names = names.Where(n => n == onlyMaterialName).ToList();
                }

                // Les materiaux doivent etre importes pour que le remap soit pris en compte.
                if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                }

                foreach (var name in names)
                {
                    importer.AddRemap(
                        new AssetImporter.SourceAssetIdentifier(typeof(Material), name),
                        targetMaterial);
                }

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log($"{importer.assetPath} : {names.Count} slot(s) remappe(s).");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
    }

    private static void ClearRemaps()
    {
        foreach (var importer in GetSelectedImporters())
        {
            var keys = importer.GetExternalObjectMap()
                .Where(kv => kv.Key.type == typeof(Material))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keys)
            {
                importer.RemoveRemap(key);
            }

            importer.SaveAndReimport();
        }
    }

    // Recupere les noms des materiaux d'origine du FBX (ceux listes dans On Demand Remap).
    private static List<string> GetSourceMaterialNames(ModelImporter importer)
    {
        var names = new HashSet<string>();

        // Materiaux deja remappes
        foreach (var kv in importer.GetExternalObjectMap())
        {
            if (kv.Key.type == typeof(Material))
            {
                names.Add(kv.Key.name);
            }
        }

        // Materiaux integres au FBX
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Material>())
        {
            names.Add(m.name);
        }

        // Materiaux utilises par les renderers
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);
        if (root != null)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null)
                    {
                        names.Add(m.name);
                    }
                }
            }
        }

        return names.ToList();
    }
}
