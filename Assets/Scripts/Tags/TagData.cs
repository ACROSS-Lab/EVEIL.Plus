using UnityEngine;

[CreateAssetMenu(fileName = "TagData", menuName = "Point Of Interest/Tag Data")]
public class TagData : ScriptableObject
{
    public Sprite icon;
    public string localizationKey;
}