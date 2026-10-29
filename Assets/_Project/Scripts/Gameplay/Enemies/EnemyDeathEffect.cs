// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: EnemyDeathEffect.cs
// Propósito: efecto de muerte de los enemigos (fragmentos de cubo que salen
// disparados). Lo usan EnemyHealth y el jefe final para que se vean igual.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Enemies
{
    /// <summary>
    /// Fragmentos que deja un enemigo al morir. Antes este código vivía dentro de
    /// <c>EnemyHealth.SpawnDebris</c>; lo saqué aquí sin cambiar ningún número para poder
    /// reutilizar exactamente el mismo efecto al destruir a la IA del jefe final.
    /// </summary>
    /// <remarks>
    /// Cada fragmento es un cubo con el material del enemigo, colocado dentro de una esfera de
    /// 0,35 m alrededor del centro y con un lado entre 0,12 y 0,28 m. Recibe un impulso
    /// <c>J = normalize(dirección_aleatoria + 0,75·arriba) · U(3, 7)</c> (masa 0,2 kg), una velocidad
    /// angular aleatoria de hasta 10 rad/s y se destruye entre 1,5 y 2,5 s después.
    /// <para>
    /// El parámetro <paramref name="scale"/> solo agranda las medidas (radio y tamaño de los
    /// cubos) para objetos grandes; con 1 es idéntico al de los enemigos normales.
    /// </para>
    /// </remarks>
    public static class EnemyDeathEffect
    {
        /// <summary>Lanza los fragmentos de muerte.</summary>
        /// <param name="center">Centro del enemigo en el mundo.</param>
        /// <param name="material">Material que heredan los fragmentos (puede ser nulo).</param>
        /// <param name="count">Número de fragmentos.</param>
        /// <param name="scale">Escala de radio y tamaño (1 = enemigo normal).</param>
        public static void Spawn(Vector3 center, Material material, int count, float scale = 1f)
        {
            scale = Mathf.Max(0.1f, scale);
            for (int i = 0; i < count; i++)
            {
                GameObject gib = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gib.name = "Gib_Debris";
                gib.transform.position = center + Random.insideUnitSphere * (0.35f * scale);
                gib.transform.localScale = Vector3.one * (Random.Range(0.12f, 0.28f) * scale);

                if (material != null)
                {
                    gib.GetComponent<Renderer>().sharedMaterial = material;
                }

                Rigidbody rb = gib.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                Vector3 force = (Random.onUnitSphere + Vector3.up * 0.75f).normalized * Random.Range(3f, 7f);
                rb.AddForce(force, ForceMode.Impulse);
                rb.angularVelocity = Random.insideUnitSphere * 10f;

                Object.Destroy(gib, Random.Range(1.5f, 2.5f));
            }
        }
    }
}
