using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Haz visible desde la boquilla hasta el impacto real de la mira.</summary>
public sealed class ToolRayVisual
{
    private readonly LineRenderer line;
    private readonly Material material;

    public ToolRayVisual(Transform tool, Color color)
    {
        GameObject beam = new GameObject("Rayo visible");
        beam.transform.SetParent(tool, false);
        line = beam.AddComponent<LineRenderer>();
        material = new Material(Resources.Load<Shader>("ToolRay"));
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = 0.018f;
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
    }

    public void Draw(Vector3 origin, Vector3 destination, bool active)
    {
        line.enabled = true;
        line.startWidth = line.endWidth = active ? 0.035f : 0.018f;
        line.SetPosition(0, origin);
        line.SetPosition(1, destination);
    }

    public void Hide() { if (line != null) line.enabled = false; }
    public void Dispose()
    {
        if (line != null) Object.Destroy(line.gameObject);
        if (material != null) Object.Destroy(material);
    }
}
