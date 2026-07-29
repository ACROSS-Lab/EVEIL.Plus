using UnityEngine;

/// <summary>
/// Internal bookkeeping component added by HoverLiftSetupWindow when it sets up an object.
/// It records exactly what the tool added, so Revert Setup can remove precisely that and
/// nothing else (for example, it won't remove a Collider or Interactable that already existed
/// on the object before the setup ran). You should not need to add or edit this by hand.
/// </summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public class HoverLiftSetupInfo : MonoBehaviour
{
    public enum AddedColliderType
    {
        None,
        Box,
        Mesh
    }

    [Tooltip("True if the mesh originally lived directly on this root object (no children) " +
             "and had to be moved onto a compensating child.")]
    public bool meshWasOnRoot;

    [Tooltip("True if the Collider was added by the tool (as opposed to already existing).")]
    public bool colliderWasAdded;

    public AddedColliderType addedColliderType = AddedColliderType.None;

    [Tooltip("True if the Interactable was added by the tool (as opposed to already existing).")]
    public bool interactableWasAdded;
}
