// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: RobotBreakApart.cs
// Propósito: al morir, el modelo del robot se desarma en piezas que caen al suelo
// y la cámara se aleja para verlas.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using NostalgIA.Core;
using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Presentation.VFX
{
    /// <summary>
    /// Desarma el modelo del robot (el que se ve en primera persona) cuando el jugador muere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada malla del modelo se copia en una pieza física: un objeto nuevo con la misma malla y
    /// los mismos materiales (no se crean materiales), un <see cref="BoxCollider"/> y un
    /// <see cref="Rigidbody"/> con un pequeño impulso hacia afuera. Las mallas con huesos se
    /// «hornean» con <see cref="SkinnedMeshRenderer.BakeMesh(Mesh, bool)"/> para conservar la pose
    /// del momento. El modelo original se oculta mientras dura la muerte.
    /// </para>
    /// <para>
    /// La cámara (su pivote) sube y retrocede suavemente mirando al centro de las piezas, de modo
    /// que el jugador ve su robot descompuesto en el piso. Al reaparecer, las piezas se destruyen,
    /// el pivote vuelve a su posición local y el modelo se muestra de nuevo.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RobotBreakApart : MonoBehaviour
    {
        [Tooltip("Pivote de la cámara (CameraParent). Si falta, se usa el padre de la cámara del jugador.")]
        [SerializeField] private Transform cameraPivot;
        [Tooltip("Impulso de cada pieza al desarmarse, en N·s.")]
        [SerializeField, Min(0f)] private float burstImpulse = 0.15f;
        [Tooltip("Masa de cada pieza, en kg.")]
        [SerializeField, Min(0.01f)] private float pieceMass = 0.4f;
        [Tooltip("Distancia hacia atrás y altura de la cámara mientras se ven las piezas, en metros.")]
        [SerializeField] private Vector2 cameraOffset = new Vector2(1.8f, 1.3f);
        [Tooltip("Segundos que tarda la cámara en alejarse.")]
        [SerializeField, Min(0.1f)] private float cameraMoveTime = 1.2f;

        private readonly List<Renderer> modelRenderers = new List<Renderer>();
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly List<GameObject> pieces = new List<GameObject>();
        private readonly List<Mesh> bakedMeshes = new List<Mesh>();

        private Vector3 pivotLocalPosition;
        private Quaternion pivotLocalRotation;
        private Vector3 cameraStart;
        private Quaternion cameraStartRotation;
        private float cameraTime = -1f;

        private void Awake()
        {
            if (cameraPivot == null)
            {
                Camera playerCamera = GetComponentInChildren<Camera>(true);
                if (playerCamera != null) cameraPivot = playerCamera.transform.parent;
            }
        }

        private void OnEnable()
        {
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<PlayerRespawned>.Subscribe(OnPlayerRespawned);
        }

        private void OnDisable()
        {
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<PlayerRespawned>.Unsubscribe(OnPlayerRespawned);
        }

        private void OnDestroy() => ClearPieces();

        // ------------------------------------------------------------------ muerte
        private void OnPlayerDied(in PlayerDied evt)
        {
            if (cameraPivot == null) return;
            ClearPieces();
            CollectModelRenderers();

            Vector3 center = Vector3.zero;
            for (int i = 0; i < modelRenderers.Count; i++)
            {
                GameObject piece = MakePiece(modelRenderers[i]);
                if (piece == null) continue;
                pieces.Add(piece);
                center += piece.transform.position;
                modelRenderers[i].enabled = false;
            }
            if (pieces.Count == 0) return;
            center /= pieces.Count;
            HideBodyAndReticle();

            // Impulso hacia afuera desde el centro, con algo hacia arriba y giro al azar.
            for (int i = 0; i < pieces.Count; i++)
            {
                Rigidbody body = pieces[i].GetComponent<Rigidbody>();
                Vector3 outward = pieces[i].transform.position - center;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.onUnitSphere;
                body.AddForce((outward + Vector3.up * 0.5f) * burstImpulse, ForceMode.Impulse);
                body.AddTorque(Random.onUnitSphere * burstImpulse * 0.5f, ForceMode.Impulse);
            }

            // La cámara se aleja desde donde estaba.
            pivotLocalPosition = cameraPivot.localPosition;
            pivotLocalRotation = cameraPivot.localRotation;
            cameraStart = cameraPivot.position;
            cameraStartRotation = cameraPivot.rotation;
            cameraTime = 0f;
        }

        /// <summary>La cámara sigue mirando al centro de las piezas mientras caen.</summary>
        private void LateUpdate()
        {
            if (cameraTime < 0f || pieces.Count == 0) return;
            cameraTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, cameraTime / cameraMoveTime);

            Vector3 center = Vector3.zero;
            for (int i = 0; i < pieces.Count; i++) center += pieces[i].transform.position;
            center /= pieces.Count;

            Vector3 back = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 target = center - back * cameraOffset.x + Vector3.up * cameraOffset.y;
            cameraPivot.position = Vector3.Lerp(cameraStart, target, t);
            Quaternion look = Quaternion.LookRotation(center - cameraPivot.position, Vector3.up);
            cameraPivot.rotation = Quaternion.Slerp(cameraStartRotation, look, t);
        }

        // ------------------------------------------------------------------ reaparición
        private void OnPlayerRespawned(in PlayerRespawned evt)
        {
            ClearPieces();
            cameraTime = -1f;
            if (cameraPivot != null)
            {
                cameraPivot.localPosition = pivotLocalPosition;
                cameraPivot.localRotation = pivotLocalRotation;
            }
            for (int i = 0; i < modelRenderers.Count; i++)
                if (modelRenderers[i] != null) modelRenderers[i].enabled = true;
            for (int i = 0; i < hiddenRenderers.Count; i++)
                if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = true;
            for (int i = 0; i < hiddenCanvases.Count; i++)
                if (hiddenCanvases[i] != null) hiddenCanvases[i].enabled = true;
            hiddenRenderers.Clear();
            hiddenCanvases.Clear();
        }

        /// <summary>
        /// Mientras se ven las piezas se ocultan la cápsula del cuerpo (solo existía para la sombra)
        /// y la mira, que estorbarían la vista en tercera persona.
        /// </summary>
        private void HideBodyAndReticle()
        {
            hiddenRenderers.Clear();
            hiddenCanvases.Clear();
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.transform.IsChildOf(cameraPivot)) continue;
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
            foreach (Canvas c in GetComponentsInChildren<Canvas>())
            {
                if (!c.enabled || !c.name.Contains("Reticle")) continue;
                c.enabled = false;
                hiddenCanvases.Add(c);
            }
        }

        private void ClearPieces()
        {
            for (int i = 0; i < pieces.Count; i++) if (pieces[i] != null) Destroy(pieces[i]);
            for (int i = 0; i < bakedMeshes.Count; i++) if (bakedMeshes[i] != null) Destroy(bakedMeshes[i]);
            pieces.Clear();
            bakedMeshes.Clear();
        }

        // ------------------------------------------------------------------ piezas
        /// <summary>Mallas visibles del modelo bajo el pivote de cámara (se ignoran partículas).</summary>
        private void CollectModelRenderers()
        {
            modelRenderers.Clear();
            Renderer[] all = cameraPivot.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (!r.enabled) continue;
                if (r is SkinnedMeshRenderer || (r is MeshRenderer && r.GetComponent<MeshFilter>() != null))
                    modelRenderers.Add(r);
            }
        }

        /// <summary>Crea una pieza física con la malla y los materiales del renderer.</summary>
        private GameObject MakePiece(Renderer source)
        {
            Mesh mesh;
            Vector3 scale;
            if (source is SkinnedMeshRenderer skinned)
            {
                // BakeMesh con escala: la malla ya incluye la escala del objeto.
                mesh = new Mesh();
                skinned.BakeMesh(mesh, true);
                bakedMeshes.Add(mesh);
                scale = Vector3.one;
            }
            else
            {
                mesh = source.GetComponent<MeshFilter>().sharedMesh;
                scale = source.transform.lossyScale;
            }
            if (mesh == null || mesh.vertexCount == 0) return null;

            var piece = new GameObject(source.name + "_Pieza");
            piece.layer = Layers.Debris;
            piece.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            piece.transform.localScale = scale;
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;

            var box = piece.AddComponent<BoxCollider>();
            // Una caja mínima evita que las piezas planas atraviesen el piso.
            box.size = Vector3.Max(box.size, Vector3.one * 0.02f);
            var body = piece.AddComponent<Rigidbody>();
            body.mass = pieceMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return piece;
        }
    }
}
