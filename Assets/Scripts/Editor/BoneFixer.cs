using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor tool: finds bones far from the skeleton center.
/// Use: select the octopus root in Hierarchy, then Window > Octo > Find Long Bones.
/// </summary>
public class BoneFixer : EditorWindow
{
    [MenuItem("Window/Octo/Find Long Bones")]
    static void FindLongBones()
    {
        var selected = Selection.activeTransform;
        if (selected == null)
        {
            Debug.LogError("Select the octopus root object first!");
            return;
        }

        // Find the SkinnedMeshRenderer to get root bone reference
        var smr = selected.GetComponentInChildren<SkinnedMeshRenderer>();
        Vector3 center = smr != null ? smr.bounds.center : selected.position;

        var allBones = selected.GetComponentsInChildren<Transform>();
        var boneDistances = new List<(Transform bone, float dist)>();

        foreach (var bone in allBones)
        {
            float dist = Vector3.Distance(bone.position, center);
            boneDistances.Add((bone, dist));
        }

        // Sort by distance, farthest first
        boneDistances.Sort((a, b) => b.dist.CompareTo(a.dist));

        Debug.Log($"=== Top 10 farthest bones from center ({center}) ===");
        for (int i = 0; i < Mathf.Min(10, boneDistances.Count); i++)
        {
            var (bone, dist) = boneDistances[i];
            string path = GetPath(bone, selected);
            Debug.Log($"  {dist:F2}m - {path} (localPos: {bone.localPosition}, worldPos: {bone.position})");
        }
        Debug.Log("=== Select any of these in the Hierarchy and set Local Scale to (0,0,0) if they're too far ===");
    }

    static string GetPath(Transform t, Transform root)
    {
        var parts = new List<string>();
        while (t != null && t != root)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join(" > ", parts);
    }
}
