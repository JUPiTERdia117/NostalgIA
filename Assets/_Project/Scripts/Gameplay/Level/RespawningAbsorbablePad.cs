// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: RespawningAbsorbablePad.cs
// Propósito: surtidor de objetos absorbibles para combates obligatorios (como el
// Spawner central o el WinRAR final), evitando bloqueos por falta de munición.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Gameplay.Tools;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Mantiene un orbe absorbible disponible sobre su base: cuando el objeto anterior se rompe
    /// contra un enemigo o cae lejos de la arena, genera uno nuevo tras una pausa breve.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RespawningAbsorbablePad : MonoBehaviour
    {
        [Header("Apariencia y física")]
        [Tooltip("Malla compartida del objeto absorbible generado.")]
        [SerializeField] private Mesh orbMesh;
        [Tooltip("Material compartido del objeto absorbible (asset del proyecto, nunca instanciado).")]
        [SerializeField] private Material orbMaterial;
        [Tooltip("Radio del orbe generado, en metros.")]
        [SerializeField, Range(0.2f, 1f)] private float orbRadius = 0.44f;
        [Tooltip("Masa del Rigidbody para el cálculo F = m · a al impactar enemigos.")]
        [SerializeField, Min(0.1f)] private float orbMass = 0.85f;

        [Header("Reposición")]
        [Tooltip("Segundos de espera antes de reponer el orbe cuando el anterior fue destruido.")]
        [SerializeField, Min(0.5f)] private float respawnDelay = 3.5f;
        [Tooltip("Distancia máxima respecto a la base antes de considerar que el orbe cayó fuera de alcance.")]
        [SerializeField, Min(5f)] private float maxLeashDistance = 36f;

        private GameObject currentOrb;
        private Absorbable currentAbsorbable;
        private float timer;

        /// <summary>Configura malla, material y tiempos desde el constructor del nivel.</summary>
        public void Configure(Mesh sharedMesh, Material sharedMaterial, float radius = 0.44f, float delay = 3.5f)
        {
            orbMesh = sharedMesh;
            orbMaterial = sharedMaterial;
            orbRadius = radius;
            respawnDelay = Mathf.Max(0.5f, delay);
        }

        private void Start()
        {
            SpawnFreshOrb();
        }

        private void Update()
        {
            // Mientras el orbe esté guardado dentro del depósito de la aspiradora queda inactivo en
            // jerarquía (activeSelf == false); ahí NO debo reponerlo todavía para no llenar la escena.
            if (currentOrb != null)
            {
                if (!currentOrb.activeInHierarchy) return;
                if (currentAbsorbable != null && currentAbsorbable.IsDestroyed)
                {
                    currentOrb = null;
                    currentAbsorbable = null;
                    timer = respawnDelay;
                }
                else
                {
                    float sqrDist = (currentOrb.transform.position - transform.position).sqrMagnitude;
                    if (sqrDist > maxLeashDistance * maxLeashDistance)
                    {
                        Destroy(currentOrb);
                        currentOrb = null;
                        currentAbsorbable = null;
                        timer = respawnDelay;
                    }
                    return;
                }
            }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                SpawnFreshOrb();
            }
        }

        private void SpawnFreshOrb()
        {
            if (orbMesh == null || orbMaterial == null) return;

            var go = new GameObject("Absorbible_Arena");
            go.transform.SetParent(transform.parent, true);
            go.transform.position = transform.position + Vector3.up * (orbRadius + 0.18f);
            go.transform.localScale = Vector3.one * (orbRadius * 2f);
            go.layer = Layers.Absorbable;

            go.AddComponent<MeshFilter>().sharedMesh = orbMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = orbMaterial;
            go.AddComponent<SphereCollider>();

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = orbMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            currentAbsorbable = go.AddComponent<Absorbable>();
            currentOrb = go;
            timer = respawnDelay;
        }
    }
}
