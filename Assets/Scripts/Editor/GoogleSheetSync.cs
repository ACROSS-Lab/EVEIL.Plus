using System;
using System.IO;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Converts a standard Google Sheets /edit URL into a direct CSV export URL if needed.
    /// </summary>
    private static string NormalizeCsvUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        string trimmed = url.Trim();

        // Match standard Google Sheets URLs: https://docs.google.com/spreadsheets/d/{id}/edit...
        Match sheetMatch = Regex.Match(trimmed, @"^(https://docs\.google\.com/spreadsheets/d/[^/]+)/(?:edit|view)(.*)$", RegexOptions.IgnoreCase);
        if (sheetMatch.Success)
        {
            string baseUrl = sheetMatch.Groups[1].Value;
            string tail = sheetMatch.Groups[2].Value;

            Match gidMatch = Regex.Match(tail, @"[?&#]gid=([0-9]+)", RegexOptions.IgnoreCase);
            if (gidMatch.Success)
            {
                return $"{baseUrl}/export?format=csv&gid={gidMatch.Groups[1].Value}";
            }

            return $"{baseUrl}/export?format=csv";
        }

        return trimmed;
    }

    private void StartDownload()
    {
        if (string.IsNullOrEmpty(sheetUrl))
        {
            Debug.LogError("Sheet URL is empty!");
            return;
        }

        string downloadUrl = NormalizeCsvUrl(sheetUrl);
        if (downloadUrl != sheetUrl)
        {
            Debug.Log($"Converted Google Sheet edit URL to CSV export URL: {downloadUrl}");
        }

        www = UnityWebRequest.Get(downloadUrl);
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
            string downloadedText = www.downloadHandler.text;

            // Guard against overwriting the CSV with an HTML page (e.g. login page or non-export URL)
            if (!string.IsNullOrEmpty(downloadedText) &&
                (downloadedText.TrimStart().StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                 downloadedText.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase)))
            {
                Debug.LogError("Sync aborted: The downloaded content is an HTML webpage instead of a CSV file. Make sure the Google Sheet is shared ('Anyone with the link') or published as CSV.");
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
                File.WriteAllText(savePath, downloadedText);
                AssetDatabase.Refresh();
                Debug.Log("Sync successful! File saved to: " + savePath);
            }
        }

        www.Dispose();
        www = null;
    }
}
