// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: LevelShaderWarmup.cs
// Propósito: precalentar los shaders del nivel siguiente mientras está dormido,
// para que no se compilen todos de golpe cuando la puerta empieza a mostrarlo.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NostalgIA.Presentation.Levels
{
    /// <summary>
    /// Lista de pares (malla, material) del nivel siguiente que se dibujan uno por uno, de forma
    /// invisible, antes de que el jugador se acerque a la puerta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué hace falta.</b> La primera vez que la GPU dibuja un material con una
    /// combinación de palabras clave nueva, el driver tiene que preparar el programa del shader y
    /// el estado de la tubería gráfica (en Metal y Vulkan, el "PSO"). Eso tarda de 5 a 50 ms por
    /// variante en un teléfono. Cuando la puerta despertaba el nivel siguiente, la cámara del
    /// portal dibujaba por primera vez decenas de materiales en el mismo frame y el juego se
    /// trababa justo al llegar a las escaleras del Tutorial.
    /// </para>
    /// <para>
    /// <b>Cómo lo reparto.</b> Mientras el nivel siguiente duerme (el jugador todavía está lejos),
    /// dibujo un material por frame con <see cref="Graphics.RenderMesh"/> en dos lugares:
    /// <list type="number">
    /// <item>En la cámara del portal, sobre una textura diminuta con el mismo formato que su vista
    /// real y con el cielo y la niebla del nivel siguiente: así quedan listas las variantes que se
    /// usarán al despertar.</item>
    /// <item>En la cámara del jugador, escalado a una fracción de píxel delante del plano cercano:
    /// así quedan listas las variantes con el formato de la imagen principal, que se usarán al
    /// cruzar. No se ve, pero la GPU sí recibe la llamada de dibujo.</item>
    /// </list>
    /// Las variantes repetidas se descartan: dos objetos con el mismo material y el mismo formato
    /// de vértices comparten programa, así que basta con dibujar uno.
    /// </para>
    /// <para>
    /// La lista se arma una sola vez al preparar la puerta (ahí sí se asigna memoria, igual que
    /// al cargar la escena); el paso por frame no asigna nada.
    /// </para>
    /// </remarks>
    public sealed class LevelShaderWarmup
    {
        /// <summary>Un dibujo pendiente.</summary>
        private struct Item
        {
            public Mesh Mesh;
            public int Submesh;
            public Material Material;
            public int Layer;
            public float Radius;
        }

        private readonly List<Item> items = new List<Item>(256);
        private readonly HashSet<long> seen = new HashSet<long>();
        private int next;
        private static Mesh quad;

        /// <summary>Número de dibujos distintos que se encontraron.</summary>
        public int Count => items.Count;

        /// <summary><c>true</c> cuando ya se dibujaron todos.</summary>
        public bool Done => next >= items.Count;

        /// <summary>
        /// Recorre todos los renderers de la escena (también los que están apagados porque el nivel
        /// duerme) y guarda una entrada por cada combinación distinta de material y formato de malla.
        /// </summary>
        /// <param name="scene">Escena del nivel siguiente.</param>
        public void Collect(Scene scene)
        {
            items.Clear();
            seen.Clear();
            next = 0;
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = new List<GameObject>(64);
            var renderers = new List<Renderer>(512);
            var materials = new List<Material>(8);
            scene.GetRootGameObjects(roots);
            for (int r = 0; r < roots.Count; r++)
            {
                roots[r].GetComponentsInChildren(true, renderers);
                for (int i = 0; i < renderers.Count; i++) AddRenderer(renderers[i], materials);
            }
        }

        /// <summary>
        /// Dibuja las siguientes <paramref name="count"/> entradas de la lista.
        /// </summary>
        /// <param name="warmCamera">Cámara del portal (dibuja sobre la textura diminuta).</param>
        /// <param name="warmLayer">Capa que solo ve la cámara del portal durante el precalentado.</param>
        /// <param name="viewerCamera">Cámara del jugador (puede ser nula).</param>
        /// <param name="count">Entradas por frame.</param>
        /// <returns><c>true</c> al terminar la lista.</returns>
        public bool DrawNext(Camera warmCamera, int warmLayer, Camera viewerCamera, int count)
        {
            Transform warmTransform = warmCamera.transform;
            Vector3 warmPoint = warmTransform.position + warmTransform.forward * 3f;

            for (int k = 0; k < count && next < items.Count; k++)
            {
                Item item = items[next++];
                if (item.Mesh == null || item.Material == null) continue;

                // 1) Cámara del portal: la malla cabe en una esfera de 1 m frente a la cámara.
                float warmScale = 1f / item.Radius;
                var warmParams = new RenderParams(item.Material)
                {
                    camera = warmCamera,
                    layer = warmLayer,
                    worldBounds = new Bounds(warmPoint, Vector3.one * 2f),
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.BlendProbes,
                    reflectionProbeUsage = ReflectionProbeUsage.BlendProbes
                };
                Graphics.RenderMesh(warmParams, item.Mesh, item.Submesh,
                    Matrix4x4.TRS(warmPoint, Quaternion.identity, Vector3.one * warmScale));

                // 2) Cámara del jugador: toda la malla cabe en 0,05 mm, unas décimas de píxel a la
                //    distancia del plano cercano. Uso la capa original del objeto para que también
                //    se preparen los pases de las Renderer Features que filtran por capa.
                if (viewerCamera == null) continue;
                Transform viewer = viewerCamera.transform;
                Vector3 point = viewer.position + viewer.forward * (viewerCamera.nearClipPlane + 0.05f);
                int layer = (viewerCamera.cullingMask & (1 << item.Layer)) != 0 ? item.Layer : 0;
                float tinyScale = 0.00005f / item.Radius;
                var viewerParams = new RenderParams(item.Material)
                {
                    camera = viewerCamera,
                    layer = layer,
                    worldBounds = new Bounds(point, Vector3.one * 0.01f),
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.BlendProbes,
                    reflectionProbeUsage = ReflectionProbeUsage.BlendProbes
                };
                Graphics.RenderMesh(viewerParams, item.Mesh, item.Submesh,
                    Matrix4x4.TRS(point, Quaternion.identity, Vector3.one * tinyScale));
            }
            return next >= items.Count;
        }

        // ------------------------------------------------------------------ armado de la lista
        /// <summary>Agrega los materiales de un renderer con la malla que realmente usa.</summary>
        private void AddRenderer(Renderer renderer, List<Material> materials)
        {
            if (renderer == null) return;
            Mesh mesh = null;
            int firstSubmesh = 0;

            switch (renderer)
            {
                case MeshRenderer meshRenderer:
                    if (renderer.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;
                    // Con static batching la malla es la combinada de la escena y cada renderer usa
                    // un rango de sus submallas.
                    if (meshRenderer.isPartOfStaticBatch) firstSubmesh = meshRenderer.subMeshStartIndex;
                    break;
                case SkinnedMeshRenderer skinned:
                    mesh = skinned.sharedMesh;
                    break;
                case ParticleSystemRenderer particles:
                    if (particles.renderMode == ParticleSystemRenderMode.Mesh) mesh = particles.mesh;
                    // El material de las estelas también se compila aparte.
                    if (particles.trailMaterial != null) Add(Quad, 0, particles.trailMaterial, renderer.gameObject.layer);
                    break;
            }

            // Líneas, estelas, sprites y partículas sin malla se aproximan con un quad: el programa
            // del shader queda compilado aunque su formato de vértices sea distinto.
            if (mesh == null) mesh = Quad;

            renderer.GetSharedMaterials(materials);
            int lastSubmesh = Mathf.Max(0, mesh.subMeshCount - 1);
            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                Add(mesh, Mathf.Min(firstSubmesh + i, lastSubmesh), material, renderer.gameObject.layer);
            }
        }

        /// <summary>Guarda la entrada si su combinación de material y formato de vértices es nueva.</summary>
        private void Add(Mesh mesh, int submesh, Material material, int layer)
        {
            long key = ((long)material.GetInstanceID() << 32) ^ (uint)VertexLayoutHash(mesh);
            if (!seen.Add(key)) return;

            Bounds bounds = mesh.bounds;
            float radius = Mathf.Max(0.01f, bounds.center.magnitude + bounds.extents.magnitude);
            items.Add(new Item { Mesh = mesh, Submesh = submesh, Material = material, Layer = layer, Radius = radius });
        }

        /// <summary>
        /// Huella del formato de vértices (atributos, formato, dimensión y flujo). Es parte de la
        /// llave porque el estado de la tubería gráfica cambia con él.
        /// </summary>
        private static int VertexLayoutHash(Mesh mesh)
        {
            int hash = 17;
            int count = mesh.vertexAttributeCount;
            for (int i = 0; i < count; i++)
            {
                var attribute = mesh.GetVertexAttribute(i);
                hash = hash * 31 + (((int)attribute.attribute << 16) | ((int)attribute.format << 8) |
                                    (attribute.dimension << 4) | attribute.stream);
            }
            return hash;
        }

        /// <summary>Quad de 1 × 1 m que trae Unity (se crea uno propio si no estuviera disponible).</summary>
        private static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                if (quad != null) return quad;

                quad = new Mesh { name = "WarmupQuad" };
                quad.SetVertices(new List<Vector3>
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                });
                quad.SetUVs(0, new List<Vector2> { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
                quad.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
                quad.RecalculateNormals();
                quad.RecalculateTangents();
                return quad;
            }
        }
    }
}
