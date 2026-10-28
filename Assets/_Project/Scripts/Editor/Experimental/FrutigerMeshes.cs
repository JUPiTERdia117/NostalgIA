// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: FrutigerMeshes.cs
// Propósito: generadores de mallas procedurales del nivel Frutiger Aero (rama
// experimental): islas low poly, islas flotantes, agua, peces, cintas de luz,
// haces de luz, anillos de espuma y toro. Todo es malla (0 % Terrain).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NostalgIA.EditorTools.Experimental
{
    /// <summary>Constructores de mallas usados por <see cref="FrutigerLevelBuilder"/>.</summary>
    /// <remarks>
    /// Las mallas se generan en el editor y se guardan como assets; en ejecución no se crea
    /// ninguna. Los ruidos usan un hash entero determinista, así que repetir el montaje produce
    /// exactamente la misma geometría.
    /// </remarks>
    internal static class FrutigerMeshes
    {
        /// <summary>Hash determinista en [0, 1) a partir de dos enteros y una semilla.</summary>
        private static float Hash(int a, int b, int seed)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777216f;
            }
        }

        // ================================================================== islas
        /// <summary>
        /// Isla con playa y lecho marino. Perfil radial (t = distancia / radio):
        ///   t ≤ 1:  h = cima·(1 − smoothstep(0,2; 1; t)) + relieve   (domo con meseta)
        ///   t > 1:  h = −profundidad·smoothstep(1; 1,35; t)          (talud bajo el agua)
        /// Cada vértice recibe ±6 % de radio y ±0,25 m de altura de ruido, que con el sombreado
        /// plano del material produce las facetas low poly.
        /// </summary>
        public static Mesh Island(string name, float radius, float top, float depth, int seed)
        {
            const int segments = 30;
            const int rings = 13;
            var vertices = new List<Vector3> { new Vector3(0f, top + 0.2f, 0f) };
            var triangles = new List<int>();

            for (int r = 1; r <= rings; r++)
            {
                float t = r / (float)rings * 1.35f;
                for (int s = 0; s < segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    float jitterR = 1f + (Hash(r, s, seed) - 0.5f) * 0.12f;
                    float lobes = 1f + 0.08f * Mathf.Sin(angle * 3f + seed) + 0.05f * Mathf.Sin(angle * 5f + seed * 0.7f);
                    float dist = t * radius * jitterR * lobes;
                    float h;
                    if (t <= 1f)
                    {
                        float dome = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, t));
                        h = top * dome + (Hash(s, r, seed + 11) - 0.5f) * 0.5f * (1f - t * 0.7f);
                        if (t > 0.92f) h = Mathf.Min(h, 0.25f);   // Playa casi plana junto al agua.
                    }
                    else
                    {
                        h = -depth * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 1.35f, t));
                    }
                    vertices.Add(new Vector3(Mathf.Cos(angle) * dist, h, Mathf.Sin(angle) * dist));
                }
            }

            // Abanico central.
            for (int s = 0; s < segments; s++)
            {
                int a = 1 + s;
                int b = 1 + (s + 1) % segments;
                triangles.Add(0); triangles.Add(b); triangles.Add(a);
            }
            // Cuadriláteros entre anillos (dos triángulos cada uno).
            for (int r = 1; r < rings; r++)
            {
                int inner = 1 + (r - 1) * segments;
                int outer = 1 + r * segments;
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    triangles.Add(inner + s); triangles.Add(inner + s1); triangles.Add(outer + s);
                    triangles.Add(outer + s); triangles.Add(inner + s1); triangles.Add(outer + s1);
                }
            }
            return Finish(name, vertices, triangles, null);
        }

        /// <summary>
        /// Isla flotante: domo superior y base rocosa invertida. La base sigue
        /// radio = R·(1 − u)^0,8 y altura = −profundidad·u^0,9 (u de 0 en el borde a 1 en la punta),
        /// con ruido para que parezca una roca tallada.
        /// </summary>
        public static Mesh FloatingIsland(string name, float radius, float top, float depth, int seed)
        {
            const int segments = 26;
            const int topRings = 6;
            const int bottomRings = 7;
            var vertices = new List<Vector3> { new Vector3(0f, top, 0f) };
            var triangles = new List<int>();

            int totalRings = topRings + bottomRings;
            for (int r = 1; r <= totalRings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    float jitter = 1f + (Hash(r, s, seed) - 0.5f) * 0.14f;
                    float dist;
                    float h;
                    if (r <= topRings)
                    {
                        float t = r / (float)topRings;
                        dist = t * radius * jitter;
                        h = top * (1f - t * t) + (Hash(s, r, seed + 3) - 0.5f) * 0.3f;
                    }
                    else
                    {
                        float u = (r - topRings) / (float)bottomRings;
                        dist = radius * Mathf.Pow(1f - u, 0.8f) * jitter;
                        h = -depth * Mathf.Pow(u, 0.9f) + (Hash(s, r, seed + 7) - 0.5f) * 0.8f;
                    }
                    vertices.Add(new Vector3(Mathf.Cos(angle) * dist, h, Mathf.Sin(angle) * dist));
                }
            }
            int tip = vertices.Count;
            vertices.Add(new Vector3(0f, -depth - 0.6f, 0f));

            for (int s = 0; s < segments; s++)
            {
                triangles.Add(0); triangles.Add(1 + (s + 1) % segments); triangles.Add(1 + s);
            }
            for (int r = 1; r < totalRings; r++)
            {
                int inner = 1 + (r - 1) * segments;
                int outer = 1 + r * segments;
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    triangles.Add(inner + s); triangles.Add(inner + s1); triangles.Add(outer + s);
                    triangles.Add(outer + s); triangles.Add(inner + s1); triangles.Add(outer + s1);
                }
            }
            int last = 1 + (totalRings - 1) * segments;
            for (int s = 0; s < segments; s++)
            {
                triangles.Add(last + s); triangles.Add(last + (s + 1) % segments); triangles.Add(tip);
            }
            return Finish(name, vertices, triangles, null);
        }

        // ================================================================== agua
        /// <summary>Rejilla cuadrada plana centrada (las ondas las mueve el shader).</summary>
        public static Mesh WaterGrid(string name, float size, int cells)
        {
            var vertices = new List<Vector3>((cells + 1) * (cells + 1));
            var triangles = new List<int>(cells * cells * 6);
            float step = size / cells;
            float half = size * 0.5f;
            for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                    vertices.Add(new Vector3(x * step - half, 0f, z * step - half));
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    int i = z * (cells + 1) + x;
                    triangles.Add(i); triangles.Add(i + cells + 1); triangles.Add(i + 1);
                    triangles.Add(i + 1); triangles.Add(i + cells + 1); triangles.Add(i + cells + 2);
                }
            }
            Mesh mesh = Finish(name, vertices, triangles, null);
            // Las ondas desplazan los vértices hasta ±1 m: se amplían los límites para el culling.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 4f, size));
            return mesh;
        }

        /// <summary>Corona plana (espuma): radio interior 0,9, exterior 1,4; UV.x = ángulo, UV.y = radial.</summary>
        public static Mesh FoamRing(string name)
        {
            const int segments = 72;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float u = s / (float)segments;
                float angle = u * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(dir * 0.9f); uvs.Add(new Vector2(u, 0f));
                vertices.Add(dir * 1.4f); uvs.Add(new Vector2(u, 1f));
            }
            for (int s = 0; s < segments; s++)
            {
                int i = s * 2;
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i + 3);
            }
            return Finish(name, vertices, triangles, uvs);
        }

        // ================================================================== fauna
        /// <summary>
        /// Pez low poly orientado a +Z: cuerpo de anillos elípticos con perfil sin(π·i/N)^0,7,
        /// aleta caudal y aleta dorsal. UV.x = 0 en la cabeza y 1 en la cola (peso del nado).
        /// </summary>
        public static Mesh Fish(string name)
        {
            const int rings = 10;
            const int sides = 8;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            vertices.Add(new Vector3(0f, 0f, 0.52f)); uvs.Add(new Vector2(0f, 0.5f));
            for (int i = 1; i < rings; i++)
            {
                float t = i / (float)rings;
                float z = 0.5f - t;
                float profile = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f);
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * 0.09f * profile, Mathf.Sin(a) * 0.17f * profile, z));
                    uvs.Add(new Vector2(t, s / (float)sides));
                }
            }
            int tailIndex = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, -0.5f)); uvs.Add(new Vector2(1f, 0.5f));

            for (int s = 0; s < sides; s++)
            {
                triangles.Add(0); triangles.Add(1 + s); triangles.Add(1 + (s + 1) % sides);
            }
            for (int i = 0; i < rings - 2; i++)
            {
                int a = 1 + i * sides;
                int b = a + sides;
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    triangles.Add(a + s); triangles.Add(b + s); triangles.Add(a + s1);
                    triangles.Add(a + s1); triangles.Add(b + s); triangles.Add(b + s1);
                }
            }
            int lastRing = 1 + (rings - 2) * sides;
            for (int s = 0; s < sides; s++)
            {
                triangles.Add(lastRing + s); triangles.Add(tailIndex); triangles.Add(lastRing + (s + 1) % sides);
            }

            // Aleta caudal (horquilla) y aleta dorsal: triángulos de dos caras (el shader usa Cull Off).
            AddTriangle(vertices, uvs, triangles, new Vector3(0f, 0f, -0.44f), new Vector3(0f, 0.24f, -0.74f), new Vector3(0f, 0f, -0.62f), 1f);
            AddTriangle(vertices, uvs, triangles, new Vector3(0f, 0f, -0.44f), new Vector3(0f, 0f, -0.62f), new Vector3(0f, -0.24f, -0.74f), 1f);
            AddTriangle(vertices, uvs, triangles, new Vector3(0f, 0.14f, 0.12f), new Vector3(0f, 0.3f, -0.08f), new Vector3(0f, 0.13f, -0.2f), 0.45f);
            return Finish(name, vertices, triangles, uvs);
        }

        private static void AddTriangle(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, float u)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            uv.Add(new Vector2(u, 0f)); uv.Add(new Vector2(u, 0.5f)); uv.Add(new Vector2(u, 1f));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        // ================================================================== luz
        /// <summary>
        /// Cinta de luz: tira de 96 segmentos a lo largo de un arco ondulado
        /// p(u) = (u − ½)·L·derecha + A·sin(π·1,3·u + φ)·arriba + B·cos(π·u)·adelante,
        /// con ancho w(u) = W·(0,4 + 0,6·sin(π·u)) y un giro progresivo de la tira.
        /// </summary>
        public static Mesh Ribbon(string name, float length, float width, float amplitude, float depth, float phase, float twist)
        {
            const int segments = 96;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                var center = new Vector3((u - 0.5f) * length, amplitude * Mathf.Sin(Mathf.PI * 1.3f * u + phase), depth * Mathf.Cos(Mathf.PI * u));
                float w = width * (0.4f + 0.6f * Mathf.Sin(Mathf.PI * u));
                float roll = twist * (u - 0.5f);
                var side = new Vector3(0f, Mathf.Cos(roll), Mathf.Sin(roll));
                vertices.Add(center - side * w * 0.5f); uvs.Add(new Vector2(u, 0f));
                vertices.Add(center + side * w * 0.5f); uvs.Add(new Vector2(u, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
                triangles.Add(a + 2); triangles.Add(a + 1); triangles.Add(a + 3);
            }
            Mesh mesh = Finish(name, vertices, triangles, uvs);
            return mesh;
        }

        /// <summary>Cono abierto (haz de luz) de altura 1 sobre +Y: UV.y = 0 abajo y 1 arriba.</summary>
        public static Mesh LightCone(string name, float bottomRadius, float topRadius)
        {
            const int segments = 24;
            const int rows = 4;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int r = 0; r <= rows; r++)
            {
                float v = r / (float)rows;
                float radius = Mathf.Lerp(bottomRadius, topRadius, v);
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments;
                    float a = u * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    vertices.Add(dir * radius + Vector3.up * v);
                    normals.Add(dir);
                    uvs.Add(new Vector2(u, v));
                }
            }
            for (int r = 0; r < rows; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s;
                    int b = a + segments + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Toro (anillo de la meta): radio mayor 1, menor 'minor'; UV.y recorre la sección.</summary>
        public static Mesh Torus(string name, float minor)
        {
            const int major = 64;
            const int sides = 16;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= major; i++)
            {
                float u = i / (float)major;
                float a = u * Mathf.PI * 2f;
                var center = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= sides; j++)
                {
                    float v = j / (float)sides;
                    float b = v * Mathf.PI * 2f;
                    Vector3 normal = center * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    vertices.Add(center + normal * minor);
                    uvs.Add(new Vector2(u, 0.5f + 0.5f * normal.y));
                }
            }
            for (int i = 0; i < major; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j;
                    int b = a + sides + 1;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
                }
            }
            return Finish(name, vertices, triangles, uvs);
        }

        // ================================================================== utilidades
        private static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            if (uvs != null) mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
