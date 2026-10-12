using UnityEngine;

/// <summary>
/// Componente que inyecta automáticamente la malla 3D procedural correspondiente
/// en el MeshFilter y MeshCollider del GameObject tanto en el Editor como en tiempo de ejecución.
/// Evita por completo errores de 'Missing (Mesh)'.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class ProceduralModelView : MonoBehaviour
{
    public enum ModelType
    {
        HexIsland = 0,
        HoverPlatform = 1,
        GrapplePylon = 2,
        SpikeTrap = 3,
        SawBlade = 4,
        LaserPillar = 5,
        SentinelDrone = 6,
        CollectibleStar = 7,
        CheckpointShrine = 8,
        EnergyCube = 9,
        VictoryArch = 10
    }

    [Tooltip("Tipo de modelo 3D que este GameObject debe representar.")]
    [SerializeField] private ModelType modelType = ModelType.HexIsland;

    public ModelType SelectedModelType
    {
        get => modelType;
        set
        {
            modelType = value;
            ApplyMesh();
        }
    }

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    void Awake()
    {
        ApplyMesh();
    }

    void OnEnable()
    {
        ApplyMesh();
    }

    void Start()
    {
        ApplyMesh();
    }

    void OnValidate()
    {
        ApplyMesh();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying)
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            Mesh targetMesh = Level1MeshVault.GetMesh(modelType);
            if (meshFilter != null && (meshFilter.sharedMesh == null || meshFilter.sharedMesh != targetMesh))
            {
                ApplyMesh();
            }
        }
    }
#endif

    public void ApplyMesh()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        Mesh targetMesh = Level1MeshVault.GetMesh(modelType);
        if (targetMesh != null && meshFilter != null)
        {
            if (meshFilter.sharedMesh != targetMesh)
            {
                meshFilter.sharedMesh = targetMesh;
            }
        }

        if (meshCollider == null)
            meshCollider = GetComponent<MeshCollider>();

        if (meshCollider != null && targetMesh != null)
        {
            if (meshCollider.sharedMesh != targetMesh)
            {
                meshCollider.sharedMesh = targetMesh;
            }
        }
    }
}
