using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class MeshValidator
{
    static MeshValidator()
    {
        RunInspectionAndFix();
    }

    [MenuItem("Tools/Fix Level 1 Meshes Now")]
    public static void RunInspectionAndFix()
    {
        string logPath = Path.Combine(Application.dataPath, "../mesh_debug.txt");
        try
        {
            using (StreamWriter writer = new StreamWriter(logPath, false))
            {
                writer.WriteLine("--- MESH INSPECTION & AUTO-FIX --- " + DateTime.Now);

                // 1. Inspect all models in Assets/Models
                string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" });
                writer.WriteLine("Found model assets: " + modelGuids.Length);

                Dictionary<string, Mesh> meshMap = new Dictionary<string, Mesh>();

                foreach (string guid in modelGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    writer.WriteLine("\n[Model File] " + path + " (GUID: " + guid + ")");

                    UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    writer.WriteLine("  Sub-assets count: " + subAssets.Length);
                    foreach (var sub in subAssets)
                    {
                        if (sub != null)
                        {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sub, out string assetGuid, out long localId);
                            writer.WriteLine($"    -> Type: {sub.GetType().Name}, Name: '{sub.name}', LocalFileId: {localId}");

                            if (sub is Mesh mesh && !meshMap.ContainsKey(fileName))
                            {
                                meshMap[fileName] = mesh;
                                writer.WriteLine($"       (Registered as main mesh for {fileName})");
                            }
                        }
                    }
                }

                // 2. Open Nivel1.unity and fix all MeshFilters
                string scenePath = "Assets/Scenes/Nivel1.unity";
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                writer.WriteLine("\n--- FIXING SCENE: " + scene.name + " ---");

                int fixedCount = 0;
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
                    foreach (MeshFilter mf in meshFilters)
                    {
                        GameObject go = mf.gameObject;
                        string goName = go.name;
                        writer.WriteLine($"Checking GameObject: '{goName}', Current Mesh: {(mf.sharedMesh != null ? mf.sharedMesh.name : "NULL")}");

                        // Determine target model
                        string targetModelName = null;
                        if (goName.Contains("HexSanctuary") || goName.Contains("Sanctuary") || goName.Contains("Hex"))
                            targetModelName = "Platform_HexSanctuary";
                        else if (goName.Contains("Island") || goName.Contains("Step"))
                            targetModelName = "Platform_FloatingIsland";
                        else if (goName.Contains("Bridge") || goName.Contains("MovingPlatform"))
                            targetModelName = "Platform_Bridge";
                        else if (goName.Contains("Saw") || goName.Contains("Blade"))
                            targetModelName = "Hazard_SpinningSaw";
                        else if (goName.Contains("Spike"))
                            targetModelName = "Hazard_SpikeTrap";
                        else if (goName.Contains("Drone") || goName.Contains("Sentinel"))
                            targetModelName = "Enemy_SentinelDrone";
                        else if (goName.Contains("Portal") || goName.Contains("Celestial"))
                            targetModelName = "Goal_CelestialPortal";
                        else if (goName.Contains("Anchor") || goName.Contains("Grapple"))
                            targetModelName = "Prop_GrappleAnchor";
                        else if (goName.Contains("Cube") || goName.Contains("Energy"))
                            targetModelName = "Prop_EnergyCube";
                        else if (goName.Contains("Brazier"))
                            targetModelName = "Prop_AncientBrazier";

                        if (targetModelName != null && meshMap.TryGetValue(targetModelName, out Mesh foundMesh))
                        {
                            mf.sharedMesh = foundMesh;
                            EditorUtility.SetDirty(mf);
                            EditorUtility.SetDirty(go);

                            // Also fix MeshCollider if present
                            if (go.TryGetComponent<MeshCollider>(out var mc))
                            {
                                mc.sharedMesh = foundMesh;
                                EditorUtility.SetDirty(mc);
                            }

                            fixedCount++;
                            writer.WriteLine($"  ==> ASSIGNED MESH: '{foundMesh.name}' from {targetModelName}");
                        }
                    }
                }

                writer.WriteLine($"\nTotal MeshFilters updated: {fixedCount}");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                writer.WriteLine("Scene saved successfully!");
            }
            Debug.Log("MeshValidator: Auto-fix completed successfully! See mesh_debug.txt");
        }
        catch (Exception ex)
        {
            File.WriteAllText(logPath, "ERROR during RunInspectionAndFix: " + ex.ToString());
            Debug.LogError("MeshValidator error: " + ex);
        }
    }
}
