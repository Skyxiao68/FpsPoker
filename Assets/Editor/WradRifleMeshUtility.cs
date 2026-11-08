using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Creates a viewmodel mesh; the imported WRAD mesh remains untouched.</summary>
public static class WradRifleMeshUtility
{
    public static void PrepareFirstPersonArms(Transform modelRoot,
        string outputFolder = "Assets/visuals/Combat")
    {
        if (modelRoot == null) throw new ArgumentNullException(nameof(modelRoot));
        Directory.CreateDirectory(outputFolder);
        SkinnedMeshRenderer[] renderers = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            string suffix = renderers.Length > 1 ? "_" + index : string.Empty;
            UseForearmsOnly(renderers[index], outputFolder + "/FirstPersonForearms" + suffix + ".asset");
        }
    }

    private static void UseForearmsOnly(SkinnedMeshRenderer renderer, string path)
    {
        Mesh original = renderer.sharedMesh;
        if (original == null) return;
        if (AssetDatabase.GetAssetPath(original) == path) return;
        BoneWeight[] weights = original.boneWeights;
        Transform[] bones = renderer.bones;
        if (weights.Length != original.vertexCount)
            throw new InvalidOperationException("WRAD viewmodel requires a mesh with skin weights.");

        bool[] distalBone = new bool[bones.Length];
        for (int index = 0; index < bones.Length; index++)
        {
            string name = bones[index] != null ? bones[index].name : string.Empty;
            distalBone[index] = name.StartsWith("forearm.", StringComparison.Ordinal) ||
                                name.StartsWith("wrist.", StringComparison.Ordinal) ||
                                name.StartsWith("finger_", StringComparison.Ordinal);
        }
        bool[] retain = new bool[weights.Length];
        for (int index = 0; index < weights.Length; index++)
        {
            BoneWeight weight = weights[index];
            float distal = WeightFor(distalBone, weight.boneIndex0, weight.weight0) +
                           WeightFor(distalBone, weight.boneIndex1, weight.weight1) +
                           WeightFor(distalBone, weight.boneIndex2, weight.weight2) +
                           WeightFor(distalBone, weight.boneIndex3, weight.weight3);
            retain[index] = distal >= 0.55f;
        }

        Mesh derived = UnityEngine.Object.Instantiate(original);
        derived.name = "FirstPersonForearms";
        int kept = 0;
        int removed = 0;
        for (int submesh = 0; submesh < original.subMeshCount; submesh++)
        {
            if (original.GetTopology(submesh) != MeshTopology.Triangles)
                throw new InvalidOperationException("WRAD viewmodel requires triangle topology.");
            int[] triangles = original.GetTriangles(submesh);
            var filtered = new List<int>(triangles.Length);
            for (int face = 0; face < triangles.Length; face += 3)
            {
                if (retain[triangles[face]] && retain[triangles[face + 1]] && retain[triangles[face + 2]])
                {
                    filtered.Add(triangles[face]);
                    filtered.Add(triangles[face + 1]);
                    filtered.Add(triangles[face + 2]);
                    kept++;
                }
                else removed++;
            }
            derived.SetTriangles(filtered, submesh, false);
        }
        if (kept == 0 || removed == 0)
        {
            UnityEngine.Object.DestroyImmediate(derived);
            throw new InvalidOperationException("Expected both forearm faces and removable upper-arm faces in the WRAD mesh.");
        }

        // Reuse the GUID on installer reruns so all prefab references remain valid.
        Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (asset == null)
        {
            AssetDatabase.CreateAsset(derived, path);
            asset = derived;
        }
        else
        {
            EditorUtility.CopySerialized(derived, asset);
            UnityEngine.Object.DestroyImmediate(derived);
            EditorUtility.SetDirty(asset);
        }
        renderer.sharedMesh = asset;
        // The cut is at the elbows, outside the first-person framing. Retain the
        // source normals, UVs, skin weights, bind poses and conservative bounds.
        Debug.Log($"WRAD first-person mesh: retained {kept} faces; removed {removed} shoulder/upper-arm faces.");
    }

    private static float WeightFor(bool[] distalBones, int index, float weight)
    {
        return index >= 0 && index < distalBones.Length && distalBones[index] ? weight : 0f;
    }
}

