using System.Linq;
using UnityEngine;

public class Face_Controller : MonoBehaviour
{
    [Header("Mouth")]
    public int MouthmaterialIndex = 1;
    public Vector3 Mouth_restLocalPos = new Vector3(0.25f, 0.5f, 0f);
    public bool Mouth_InvertX = false;
    public bool Mouth_InvertY = false;
    private Material mouth_material;

    [Header("Eyes")]
    public int EyesmaterialIndex = 1;
    public Vector3 Eyes_restLocalPos = new Vector3(0.25f, 0.5f, 0f);
    public bool Eyes_InvertX = false;
    public bool Eyes_InvertY = false;
    private Material eyes_material;

    private SkinnedMeshRenderer skinnedMesh;
    private Transform mouthBone;
    private Transform eyesBone;

    void Start()
    {
        skinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        mouthBone = GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "CTRL_Mouth");
        eyesBone = GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "CTRL_Eyes");

        if (mouthBone == null) Debug.LogError("CTRL_Mouth bone not found!");
        if (eyesBone == null) Debug.LogError("CTRL_Eyes bone not found!");
        if (skinnedMesh == null) { Debug.LogError("No SkinnedMeshRenderer found!"); return; }

        var mats = skinnedMesh.materials;
        if (MouthmaterialIndex < mats.Length) mouth_material = mats[MouthmaterialIndex];
        else Debug.LogError("MouthmaterialIndex out of range!");
        if (EyesmaterialIndex < mats.Length) eyes_material = mats[EyesmaterialIndex];
        else Debug.LogError("EyesmaterialIndex out of range!");
    }

    void Update()
    {
        if (mouth_material == null || eyes_material == null) return;

        // ── Mouth ──────────────────────────────────────────────────────────────
        Vector3 mouth_delta = mouthBone.localPosition - Mouth_restLocalPos;
        float mouth_rawX = Mathf.Clamp01(mouth_delta.x * 2f);
        float mouth_rawY = Mathf.Clamp01(mouth_delta.z * 2f);
        if (Mouth_InvertX) mouth_rawX = 1f - mouth_rawX;
        if (Mouth_InvertY) mouth_rawY = 1f - mouth_rawY;
        mouth_material.SetFloat("_OffsetX", Mathf.Floor(mouth_rawX * 4f) / 4f);
        mouth_material.SetFloat("_OffsetY", Mathf.Floor(mouth_rawY * 4f) / 4f);

        // ── Eyes ───────────────────────────────────────────────────────────────
        Vector3 eyes_delta = eyesBone.localPosition - Eyes_restLocalPos;
        float eyes_rawX = Mathf.Clamp01(eyes_delta.x * 2f);
        float eyes_rawY = Mathf.Clamp01(eyes_delta.z * 2f);
        if (Eyes_InvertX) eyes_rawX = 1f - eyes_rawX;
        if (Eyes_InvertY) eyes_rawY = 1f - eyes_rawY;
        eyes_material.SetFloat("_OffsetX", Mathf.Floor(eyes_rawX * 4f) / 4f);
        eyes_material.SetFloat("_OffsetY", Mathf.Floor(eyes_rawY * 4f) / 4f);
    }
}