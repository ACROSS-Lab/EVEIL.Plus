using UnityEngine;

public class RandomColumnAssigner : MonoBehaviour
{
    static readonly int ColumnSeedID = Shader.PropertyToID("_ColumnSeed");
    MaterialPropertyBlock _propBlock;
    Renderer _renderer;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();

        float seed = Random.value; // entre 0 et 1, unique par instance
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(ColumnSeedID, seed);
        _renderer.SetPropertyBlock(_propBlock);
    }
}