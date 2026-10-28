#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilidad de depuración del editor que renderiza el modelo de la herramienta (robot.fbx) en poses
/// controladas y las guarda como PNG en <c>Temp/ToolCaptures</c>. Se utiliza para revisar de forma
/// reproducible el inflado de la recámara y la transición de modo sin necesidad de entrar en juego.
/// </summary>
/// <remarks>
/// Se instancia el prefab del jugador fuera de la zona jugable, se fuerza cada pose mediante
/// <see cref="ToolVisualController.PreviewPose"/> y se renderiza con la cámara del propio jugador
/// (vista en primera persona) y con una cámara auxiliar lateral. Todo se destruye al terminar.
/// </remarks>
public static class ToolPoseCapture
{
    private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
    private const string OutputDir = "Temp/ToolCaptures";

    /// <summary>Renderiza la serie completa de poses de la herramienta.</summary>
    [MenuItem("NostalgIA/Depuración/Capturar poses de herramienta", priority = 900)]
    public static void CaptureAll()
    {
        Directory.CreateDirectory(OutputDir);
        foreach (string f in Directory.GetFiles(OutputDir)) File.Delete(f);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // Se desempaqueta la instancia para permitir el re-emparentado que hace el controlador al iniciar.
        PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        player.hideFlags = HideFlags.DontSave;
        player.transform.position = new Vector3(0f, -800f, 0f);

        var log = new StringBuilder();
        try
        {
            ToolVisualController visual = player.GetComponentInChildren<ToolVisualController>(true);
            Camera fpCam = player.GetComponentInChildren<Camera>(true);
            DumpHierarchy(visual.transform, log, 0);

            // Se ocultan los renderers que no pertenecen al modelo (cápsula del jugador, retícula).
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(visual.transform) || r.transform == visual.transform) r.enabled = false;

            var sideGo = new GameObject("CaptureSideCam") { hideFlags = HideFlags.DontSave };
            Camera side = sideGo.AddComponent<Camera>();
            side.fieldOfView = 30f;
            side.nearClipPlane = 0.01f;

            float[,] poses =
            {
                // grappleBlend, fill (0..1), clawClamp, swallowWave
                { 0f, 0f, 0f, -1f },
                { 0f, 0.34f, 0f, -1f },
                { 0f, 0.67f, 0f, -1f },
                { 0f, 1f, 0f, -1f },
                { 0f, 0.34f, 0f, 0.45f },
                { 0.25f, 0f, 0f, -1f },
                { 0.5f, 0f, 0f, -1f },
                { 0.75f, 0f, 0f, -1f },
                { 1f, 0f, 0f, -1f },
                { 1f, 0f, 1f, -1f },
            };

            for (int i = 0; i < poses.GetLength(0); i++)
            {
                visual.PreviewPose(poses[i, 0], poses[i, 1], poses[i, 2], poses[i, 3]);
                string tag = $"{i:00}_mode{poses[i, 0]:0.00}_fill{poses[i, 1]:0.00}_clamp{poses[i, 2]:0}_wave{poses[i, 3]:0.00}";
                Render(fpCam, $"{OutputDir}/{tag}_fp.png", 640, 360);

                Vector3 c = fpCam.transform.TransformPoint(new Vector3(0.05f, -0.25f, 0.55f));
                sideGo.transform.position = c - fpCam.transform.right * 1.6f + fpCam.transform.up * 0.2f;
                sideGo.transform.LookAt(c, fpCam.transform.up);
                Render(side, $"{OutputDir}/{tag}_side.png", 480, 360);

                sideGo.transform.position = c + fpCam.transform.right * 0.8f + fpCam.transform.up * 0.5f + fpCam.transform.forward * 1.3f;
                sideGo.transform.LookAt(c, fpCam.transform.up);
                Render(side, $"{OutputDir}/{tag}_front.png", 480, 360);

                // Primer plano del cabezal para revisar las garras.
                Transform head = FindDeep(visual.transform, "Bone.014");
                if (head != null)
                {
                    Vector3 hc = (head.position + FindDeep(visual.transform, "Bone.012").position + FindDeep(visual.transform, "Bone.016").position) / 3f;
                    sideGo.transform.position = hc - fpCam.transform.right * 0.7f + fpCam.transform.up * 0.08f;
                    sideGo.transform.LookAt(hc, fpCam.transform.up);
                    Render(side, $"{OutputDir}/{tag}_headside.png", 400, 300);
                    sideGo.transform.position = hc + fpCam.transform.forward * 0.6f + fpCam.transform.up * 0.18f - fpCam.transform.right * 0.25f;
                    sideGo.transform.LookAt(hc, fpCam.transform.up);
                    Render(side, $"{OutputDir}/{tag}_headfront.png", 400, 300);
                }

                if (i == 0)
                {
                    // Vista de partes: cada malla con un color distinto para identificarla.
                    var block = new MaterialPropertyBlock();
                    Renderer[] parts = visual.GetComponentsInChildren<Renderer>(true);
                    for (int k = 0; k < parts.Length; k++)
                    {
                        if (!parts[k].enabled) continue;
                        Color col = Color.HSVToRGB((k * 0.137f) % 1f, 0.9f, 1f);
                        block.SetColor("_BaseColor", col);
                        block.SetColor("_Color", col);
                        parts[k].SetPropertyBlock(block);
                        log.AppendLine($"PART {parts[k].name} color=#{ColorUtility.ToHtmlStringRGB(col)}");
                    }
                    Render(side, $"{OutputDir}/parts_front.png", 480, 360);
                    sideGo.transform.position = c - fpCam.transform.right * 1.6f + fpCam.transform.up * 0.2f;
                    sideGo.transform.LookAt(c, fpCam.transform.up);
                    Render(side, $"{OutputDir}/parts_side.png", 480, 360);
                    Render(fpCam, $"{OutputDir}/parts_fp.png", 640, 360);
                    foreach (Renderer r in parts) r.SetPropertyBlock(null);
                }
            }
            // Aspiración activa: garras abiertas para dejar pasar los objetos.
            visual.PreviewSuction(1f);
            CaptureHead(visual, fpCam, side, sideGo, "suction");

            // Gancho anclado a 6 m: abierto (vuelo) y mordiendo (agarre).
            Vector3 target = fpCam.transform.position + fpCam.transform.forward * 6f;
            visual.PreviewPose(1f, 0f, 0f, -1f);
            visual.PreviewHook(target, 0f, 1f);
            Render(fpCam, $"{OutputDir}/hook_flight_fp.png", 640, 360);
            CaptureHook(target, fpCam, side, sideGo, "hook_flight");
            visual.PreviewPose(1f, 0f, 0f, -1f);
            visual.PreviewHook(target, 1f, 0f);
            Render(fpCam, $"{OutputDir}/hook_grip_fp.png", 640, 360);
            CaptureHook(target, fpCam, side, sideGo, "hook_grip");

            Object.DestroyImmediate(sideGo);
        }
        catch (System.Exception e)
        {
            log.AppendLine("ERROR " + e);
        }
        finally
        {
            Object.DestroyImmediate(player);
            File.WriteAllText($"{OutputDir}/log.txt", log.ToString());
        }
    }

    /// <summary>Renderiza vistas fijas de la escena abierta para revisar la composición del nivel.</summary>
    [MenuItem("NostalgIA/Depuración/Capturar vistas del nivel", priority = 903)]
    public static void CaptureLevel()
    {
        Directory.CreateDirectory("Temp/LevelCaptures");
        var go = new GameObject("LevelCaptureCam") { hideFlags = HideFlags.DontSave };
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.farClipPlane = 1000f;
        var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        // posición, punto al que mira
        Vector3[,] views =
        {
            { new Vector3(45f, 70f, -40f), new Vector3(0f, 5f, 80f) },
            { new Vector3(-40f, 50f, 160f), new Vector3(0f, 8f, 90f) },
            { new Vector3(0f, 1.75f, -4f), new Vector3(0f, 1.5f, 20f) },
            { new Vector3(0f, 2.75f, 36f), new Vector3(0f, 5f, 60f) },
            { new Vector3(0f, 5.75f, 67f), new Vector3(0f, 5f, 90f) },
            { new Vector3(0f, 5.75f, 103f), new Vector3(0f, 5f, 120f) },
            { new Vector3(0f, 6f, 121f), new Vector3(0f, 13f, 140f) },
            { new Vector3(0f, 17.75f, 148f), new Vector3(0f, 17f, 166f) },
        };
        for (int i = 0; i < views.GetLength(0); i++)
        {
            go.transform.position = views[i, 0];
            go.transform.LookAt(views[i, 1]);
            RenderSky(cam, $"Temp/LevelCaptures/view{i}.png", 960, 540);
        }
        Object.DestroyImmediate(go);
    }

    private static void RenderSky(Camera cam, string path, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
    }

    /// <summary>
    /// En modo juego, simula la absorción de varios objetos encolando referencias en el almacén real
    /// de la aspiradora (por reflexión), para comprobar el inflado sin tener que apuntar a mano.
    /// </summary>
    [MenuItem("NostalgIA/Depuración/Simular absorción x5 (juego)", priority = 901)]
    public static void SimulateAbsorb()
    {
        if (!Application.isPlaying) return;
        var vac = Object.FindAnyObjectByType<NostalgIA.Gameplay.Tools.VacuumTool>();
        var storage = typeof(NostalgIA.Gameplay.Tools.VacuumTool)
            .GetField("storage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(vac) as NostalgIA.Gameplay.Tools.VacuumStorage;
        var item = Object.FindAnyObjectByType<NostalgIA.Gameplay.Tools.Absorbable>();
        storage.SetCapacity(5);
        for (int i = 0; i < 5; i++) storage.TryStore(item);
    }

    /// <summary>En modo juego, vacía el almacén simulado.</summary>
    [MenuItem("NostalgIA/Depuración/Vaciar almacén (juego)", priority = 902)]
    public static void SimulateEmpty()
    {
        if (!Application.isPlaying) return;
        var vac = Object.FindAnyObjectByType<NostalgIA.Gameplay.Tools.VacuumTool>();
        var storage = typeof(NostalgIA.Gameplay.Tools.VacuumTool)
            .GetField("storage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(vac) as NostalgIA.Gameplay.Tools.VacuumStorage;
        while (storage.Count > 0) storage.Dequeue();
        storage.SetCapacity(20);
    }

    private static void CaptureHead(ToolVisualController visual, Camera fpCam, Camera side, GameObject sideGo, string tag)
    {
        Transform head = FindDeep(visual.transform, "Bone.014");
        Vector3 hc = (head.position + FindDeep(visual.transform, "Bone.012").position + FindDeep(visual.transform, "Bone.016").position) / 3f;
        sideGo.transform.position = hc - fpCam.transform.right * 0.7f + fpCam.transform.up * 0.08f;
        sideGo.transform.LookAt(hc, fpCam.transform.up);
        Render(side, $"{OutputDir}/{tag}_headside.png", 400, 300);
        sideGo.transform.position = hc + fpCam.transform.forward * 0.6f + fpCam.transform.up * 0.18f - fpCam.transform.right * 0.25f;
        sideGo.transform.LookAt(hc, fpCam.transform.up);
        Render(side, $"{OutputDir}/{tag}_headfront.png", 400, 300);
    }

    private static void CaptureHook(Vector3 target, Camera fpCam, Camera side, GameObject sideGo, string tag)
    {
        sideGo.transform.position = target - fpCam.transform.right * 1.2f - fpCam.transform.forward * 0.4f + fpCam.transform.up * 0.2f;
        sideGo.transform.LookAt(target, fpCam.transform.up);
        Render(side, $"{OutputDir}/{tag}_side.png", 400, 300);
        sideGo.transform.position = target - fpCam.transform.right * 3.5f - fpCam.transform.forward * 3f + fpCam.transform.up * 0.5f;
        sideGo.transform.LookAt(target - fpCam.transform.forward * 3f, fpCam.transform.up);
        Render(side, $"{OutputDir}/{tag}_wide.png", 400, 300);
    }

    private static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { Transform f = FindDeep(c, n); if (f != null) return f; }
        return null;
    }

    private static void DumpHierarchy(Transform t, StringBuilder sb, int depth)
    {
        Renderer r = t.GetComponent<Renderer>();
        sb.Append(' ', depth * 2).Append(t.name)
          .Append(" lp=").Append(t.localPosition.ToString("F3"))
          .Append(" lr=").Append(t.localEulerAngles.ToString("F1"))
          .Append(" ls=").Append(t.localScale.ToString("F3"));
        if (r != null) sb.Append(" bounds=").Append(r.bounds.size.ToString("F3"));
        sb.AppendLine();
        foreach (Transform c in t) DumpHierarchy(c, sb, depth + 1);
    }

    private static void Render(Camera cam, string path, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevTarget = cam.targetTexture;
        CameraClearFlags prevFlags = cam.clearFlags;
        Color prevBg = cam.backgroundColor;
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.32f, 0.36f, 0.42f, 1f);
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        cam.targetTexture = prevTarget;
        cam.clearFlags = prevFlags;
        cam.backgroundColor = prevBg;
        RenderTexture.ReleaseTemporary(rt);
    }
}
#endif
