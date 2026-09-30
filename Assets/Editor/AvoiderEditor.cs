using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using AvoiderPlugin;

[CustomEditor(typeof(Avoider))]
public sealed class AvoiderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var avoider = (Avoider)target;
        if (!avoider.GetComponent<NavMeshAgent>())
            EditorGUILayout.HelpBox("Add a NavMeshAgent, then bake a NavMesh.", MessageType.Warning);
        if (!avoider.Avoidee)
            EditorGUILayout.HelpBox("Drag your player into the Avoidee field.", MessageType.Warning);
        if (avoider.CoverMask.value == 0)
            EditorGUILayout.HelpBox("Cover Mask must include solid walls and exclude both characters.", MessageType.Warning);
        if (Application.isPlaying) EditorGUILayout.LabelField("Status", avoider.Status);
    }
}
