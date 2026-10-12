using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class MaterialPropertyTrigger : MonoBehaviour
{
    public enum PropertyType
    {
        Float,
        Color,
        Boolean
    }

    [System.Serializable]
    public struct ShaderProperty
    {
        [Tooltip("Exact name of the property in the Shader Graph (e.g., _EmissionToggle)")]
        public string propertyName;
        
        [Tooltip("Type of property to modify")]
        public PropertyType propertyType;

        [Tooltip("Value used if type is Float")]
        public float floatValue;

        [Tooltip("Value used if type is Color")]
        public Color colorValue;

        [Tooltip("Value used if type is Boolean")]
        public bool boolValue;
    }

    [Header("Properties to Apply")]
    [SerializeField] private ShaderProperty[] propertiesToApply;

    [Header("Properties to Revert (Optional)")]
    [SerializeField] private bool useCustomRevertValues = false;
    [SerializeField] private ShaderProperty[] propertiesToRevert;

    private Renderer myRenderer;
    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        myRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    // Apply the main properties
    public void ApplyProperties()
    {
        myRenderer.GetPropertyBlock(propBlock);

        foreach (var prop in propertiesToApply)
        {
            ApplyPropertyToBlock(prop);
        }

        myRenderer.SetPropertyBlock(propBlock);
    }

    // Revert properties
    public void RevertProperties()
    {
        myRenderer.GetPropertyBlock(propBlock);

        if (useCustomRevertValues && propertiesToRevert.Length > 0)
        {
            // Revert using a custom set of values defined in the Inspector
            foreach (var prop in propertiesToRevert)
            {
                ApplyPropertyToBlock(prop);
            }
        }
        else
        {
            // Or completely clear the property block to fallback to the base Material asset values
            myRenderer.SetPropertyBlock(null);
            return;
        }

        myRenderer.SetPropertyBlock(propBlock);
    }

    private void ApplyPropertyToBlock(ShaderProperty prop)
    {
        switch (prop.propertyType)
        {
            case PropertyType.Float:
                propBlock.SetFloat(prop.propertyName, prop.floatValue);
                break;
            case PropertyType.Color:
                propBlock.SetColor(prop.propertyName, prop.colorValue);
                break;
            case PropertyType.Boolean:
                propBlock.SetFloat(prop.propertyName, prop.boolValue ? 1f : 0f);
                break;
        }
    }
}