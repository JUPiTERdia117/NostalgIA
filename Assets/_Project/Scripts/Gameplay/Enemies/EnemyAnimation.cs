// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: EnemyAnimation.cs
// Propósito: puente entre la lógica de un enemigo y su Animator: reproduce los
// estados por nombre, sabe cuánto dura cada clip y ejecuta la animación de muerte.
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using UnityEngine;

namespace NostalgIA.Gameplay.Enemies
{
    /// <summary>
    /// Reproduce las animaciones de un enemigo pidiendo los estados por nombre
    /// (<c>Idle</c>, <c>Rawr</c>, <c>Bite</c>, <c>Flying</c>, <c>Shoot</c>, <c>Spawn</c>, <c>Death</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los controladores de comportamiento (WinRar, Spam Mail, Troyano) deciden <b>qué</b> hacer;
    /// este componente solo hace el cruce suave entre estados con <c>CrossFadeInFixedTime</c>.
    /// Así el Animator Controller no necesita transiciones ni parámetros: cada estado tiene su clip
    /// y el código elige cuál se ve.
    /// </para>
    /// <para>
    /// <see cref="EnemyHealth"/> lo busca al morir: si el Animator tiene el estado
    /// <see cref="deathState"/>, espera a que termine el clip antes de lanzar los fragmentos.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimation : MonoBehaviour
    {
        [Tooltip("Animator del modelo. Si está vacío se busca en los hijos.")]
        [SerializeField] private Animator animator;
        [Tooltip("Tiempo de mezcla entre dos animaciones, en segundos.")]
        [SerializeField, Min(0f)] private float crossFade = 0.12f;
        [Tooltip("Estado que se reproduce al morir.")]
        [SerializeField] private string deathState = "Death";
        [Tooltip("Pausa en la última pose de muerte antes de los fragmentos, en segundos.")]
        [SerializeField, Min(0f)] private float deathHold = 0.1f;
        [Tooltip("Cuántos cuadros de la animación de muerte se reproducen (0 = el clip completo). " +
                 "El Spam Mail usa 3: se ve el inicio de la muerte y enseguida salen los fragmentos.")]
        [SerializeField, Min(0)] private int deathFrames = 0;

        // Estado que se está reproduciendo en cada capa, para no reiniciar un ciclo que ya corre.
        private readonly int[] current = new int[4];

        /// <summary>Hay un Animator con controlador listo para reproducir.</summary>
        public bool IsAnimated => animator != null && animator.runtimeAnimatorController != null;

        /// <summary>El Animator tiene animación de muerte.</summary>
        public bool HasDeath => IsAnimated && animator.HasState(0, Animator.StringToHash(deathState));

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            // El enemigo se mueve por código; la animación no debe desplazar la raíz.
            if (animator != null) animator.applyRootMotion = false;
        }

        /// <summary>Cambia a <paramref name="state"/> en la capa indicada.</summary>
        /// <param name="state">Nombre del estado en el Animator Controller.</param>
        /// <param name="layer">Capa del Animator (0 = base).</param>
        /// <param name="restart">Vuelve a empezar aunque ya se esté reproduciendo.</param>
        public void Play(string state, int layer = 0, bool restart = false)
        {
            if (!IsAnimated || layer < 0 || layer >= current.Length) return;
            int hash = Animator.StringToHash(state);
            if (!restart && current[layer] == hash) return;
            if (!animator.HasState(layer, hash)) return;
            animator.CrossFadeInFixedTime(hash, crossFade, layer, 0f);
            current[layer] = hash;
        }

        /// <summary>Duración en segundos del clip de un estado (0 si no existe).</summary>
        /// <remarks>Los clips vienen del FBX con el nombre «Armature|bite»; se buscan por el final.</remarks>
        public float Length(string state)
        {
            AnimationClip clip = FindClip(state);
            return clip != null ? clip.length : 0f;
        }

        private AnimationClip FindClip(string state)
        {
            if (!IsAnimated) return null;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name.EndsWith(state, StringComparison.OrdinalIgnoreCase))
                    return clip;
            return null;
        }

        /// <summary>Reproduce la muerte y espera a que termine (lo usa <see cref="EnemyHealth"/>).</summary>
        public IEnumerator PlayDeath()
        {
            // La muerte gana sobre cualquier capa superior (por ejemplo, el disparo del Spam Mail).
            for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f);
            if (deathFrames > 0)
            {
                // Solo unos cuadros: sin mezcla, para que empiece en su primer cuadro en este momento.
                animator.Play(Animator.StringToHash(deathState), 0, 0f);
                current[0] = Animator.StringToHash(deathState);
            }
            else Play(deathState, 0, true);

            AnimationClip clip = FindClip(deathState);
            float duration = clip != null ? clip.length : 0f;
            if (deathFrames > 0 && clip != null && clip.frameRate > 0f)
                duration = Mathf.Min(duration, deathFrames / clip.frameRate);
            yield return new WaitForSeconds(duration);

            // Se congela la pose alcanzada (en el Spam Mail, el tercer cuadro) hasta los fragmentos.
            if (deathFrames > 0) animator.speed = 0f;
            yield return new WaitForSeconds(deathHold);
        }
    }
}
