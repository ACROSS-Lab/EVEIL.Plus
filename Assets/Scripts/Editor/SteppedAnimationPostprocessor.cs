using UnityEngine;
using UnityEditor;

public class SteppedAnimationPostprocessor : AssetPostprocessor
{
    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        if (!clip.name.Contains("Eyes") && !clip.name.Contains("Mouth")) return;

        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

            for (int i = 0; i < curve.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }

            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }
}