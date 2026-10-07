using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

public class GoogleSheetSyncWindow : EditorWindow
{
    // Public parameters configurable via the editor window
    public string sheetUrl = "";
    public string savePath = "";

    // EditorPrefs keys for persistence
    private const string UrlPrefKey = "GoogleSheetSync_Url";
    private const string PathPrefKey = "GoogleSheetSync_Path";

    private UnityWebRequest www;

    [MenuItem("Tools/Google Sheet Sync Settings")]
    public static void ShowWindow()
    {
        GetWindow<GoogleSheetSyncWindow>("Google Sheet Sync");
    }

    private void OnEnable()
    {
        // Load saved preferences when the window opens
        sheetUrl = EditorPrefs.GetString(UrlPrefKey, "YOUR_GOOGLE_SHEET_CSV_URL_HERE");
        savePath = EditorPrefs.GetString(PathPrefKey, "Assets/Data/GameData.csv");
    }

    private void OnGUI()
    {
        GUILayout.Label("Google Sheet Sync Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        EditorGUI.BeginChangeCheck();

        // Input fields for URL and Save Path
        sheetUrl = EditorGUILayout.TextField("Google Sheet CSV URL", sheetUrl);
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        // Save preferences automatically if any field value changed
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(UrlPrefKey, sheetUrl);
            EditorPrefs.SetString(PathPrefKey, savePath);
        }

        EditorGUILayout.Space(15);

        // Sync button
        if (GUILayout.Button("Synchronize Now", GUILayout.Height(30)))
        {
            StartDownload();
        }
    }

    private void StartDownload()
    {
        if (string.IsNullOrEmpty(sheetUrl))
        {
            Debug.LogError("Sheet URL is empty!");
            return;
        }

        www = UnityWebRequest.Get(sheetUrl);
        www.SendWebRequest();

        // Register to the editor update loop to wait for completion
        EditorApplication.update += EditorUpdate;
    }

    private void EditorUpdate()
    {
        if (www == null || !www.isDone)
            return;

        // Unsubscribe from the update loop once finished
        EditorApplication.update -= EditorUpdate;

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Download failed: " + www.error);
        }
        else
        {
            // Ensure the target directory exists
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save the CSV file
            File.WriteAllText(savePath, www.downloadHandler.text);
            AssetDatabase.Refresh();
            Debug.Log("Sync successful! File saved to: " + savePath);
        }

        www.Dispose();
        www = null;
    }
}