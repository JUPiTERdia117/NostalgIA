using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Inicializador y reparador automático para Nivel 1 en el Editor de Unity.
/// Al compilar o cargar la escena, se asegura de que todas las mallas nativas existan
/// y que cada objeto con ProceduralModelView tenga su malla 3D inyectada inmediatamente.
/// </summary>
[InitializeOnLoad]
public static class Level1AutoFixer
{
    static Level1AutoFixer()
    {
        EditorApplication.delayCall += OnEditorReady;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnEditorReady()
    {
        // 1. Asegurar generación de mallas nativas (.asset) en Assets/Art/Models
        string testAsset = "Assets/Art/Models/Platform_HexIsland.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(testAsset) == null)
        {
            Level1MeshGenerator.GenerateAllNativeMeshes();
        }

        // 2. Aplicar mallas a la escena activa si corresponde
        RefreshCurrentSceneMeshes(false);
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        RefreshCurrentSceneMeshes(false);
    }

    [MenuItem("Tools/Actualizar y Reparar Mallas 3D")]
    public static void ManualFixMeshes()
    {
        Level1MeshGenerator.GenerateAllNativeMeshes();
        int fixedCount = RefreshCurrentSceneMeshes(true);
        EditorUtility.DisplayDialog("Mallas 3D Reparadas", 
            $"¡Se verificaron y aplicaron las mallas 3D en {fixedCount} objetos del nivel exitosamente!", 
            "Aceptar");
    }

    public static int RefreshCurrentSceneMeshes(bool markDirty)
    {
        ProceduralModelView[] views = Object.FindObjectsByType<ProceduralModelView>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var view in views)
        {
            if (view != null)
            {
                view.ApplyMesh();
                count++;
            }
        }

        if (count > 0 && markDirty)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        return count;
    }
}
