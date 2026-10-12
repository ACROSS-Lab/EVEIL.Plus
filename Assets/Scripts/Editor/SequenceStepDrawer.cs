using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SequenceStep))]
public class SequenceStepDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Event currentEvent = Event.current;

        // Detect click on this SequenceStep field in Inspector
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            // Expand rect slightly to the left to also catch clicks on the drag handle in a ReorderableList,
            // and 2px vertically to cover row padding
            Rect clickRect = new Rect(position.x - 24, position.y - 2, position.width + 24, position.height + 4);
            // The circle picker button on the far right is approximately 20px wide
            Rect pickerButtonRect = new Rect(position.xMax - 20, position.y, 20, position.height);

            if (clickRect.Contains(currentEvent.mousePosition) && !pickerButtonRect.Contains(currentEvent.mousePosition))
            {
                if (property.objectReferenceValue is SequenceStep clickedStep)
                {
                    SequenceStepEditorWindow.SelectStep(clickedStep);
                }
            }
        }

        EditorGUI.BeginChangeCheck();
        var newObj = EditorGUI.ObjectField(position, label, property.objectReferenceValue, typeof(SequenceStep), false);
        if (EditorGUI.EndChangeCheck())
        {
            property.objectReferenceValue = newObj;
            if (newObj is SequenceStep newStep)
            {
                SequenceStepEditorWindow.SelectStep(newStep);
            }
        }

        EditorGUI.EndProperty();
    }
}
