using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class EditorRendererSwitch : MonoBehaviour
{
    [SerializeField] int editorRendererIndex = 1;
    [SerializeField] int playRendererIndex = 0;

    // Cached via reflection since there is no public API to read the renderer list count
    private static FieldInfo _rendererDataListField;
    private bool _hasWarned;

    void OnEnable() => UpdateRenderer();

    void Update()
    {
        if (!Application.isPlaying) UpdateRenderer();
    }

    void UpdateRenderer()
    {
        var camData = GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) return;

        var desiredIndex = Application.isPlaying ? playRendererIndex : editorRendererIndex;

        if (!IsIndexValid(desiredIndex))
        {
            // Log once to avoid spamming the console every frame in edit mode
            if (!_hasWarned)
            {
                Debug.LogWarning($"[EditorRendererSwitch] Renderer index {desiredIndex} is out of range on '{name}', " +
                                  "check the Renderer List on your Universal Render Pipeline Asset. Skipping SetRenderer.", this);
                _hasWarned = true;
            }
            return;
        }

        _hasWarned = false;
        camData.SetRenderer(desiredIndex);
    }

    private static bool IsIndexValid(int index)
    {
        if (index < 0) return false;

        var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipelineAsset == null) return false;

        // UniversalRenderPipelineAsset does not expose the renderer count publicly,
        // so we read the private serialized list via reflection.
        _rendererDataListField ??= typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);

        if (_rendererDataListField == null)
        {
            // Reflection target not found (API changed in this URP version), fail safe rather than crash
            return false;
        }

        if (_rendererDataListField.GetValue(pipelineAsset) is ScriptableRendererData[] rendererDataList)
        {
            return index < rendererDataList.Length;
        }

        return false;
    }
}
