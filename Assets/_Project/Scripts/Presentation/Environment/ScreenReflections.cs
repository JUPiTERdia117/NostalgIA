// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: ScreenReflections.cs
// Propósito: enciende los reflejos en espacio de pantalla (SSR) solo en la escena
// que tenga este componente.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Presentation.Environment
{
    /// <summary>
    /// El pase de SSR (shader <c>NOS/ScreenSpaceReflection</c>) vive en el renderer y por eso
    /// correría en todos los niveles. Este componente le pasa su intensidad con una variable global
    /// priorizando la instancia de la escena activa, para que precargar o dormir el nivel siguiente
    /// en segundo plano no apague los reflejos del nivel actual.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ScreenReflections : MonoBehaviour
    {
        private static readonly int StrengthId = Shader.PropertyToID("_NOS_ScreenReflections");
        private static readonly List<ScreenReflections> ActiveInstances = new List<ScreenReflections>(4);
        private static bool subscribed;

        [Tooltip("Intensidad de los reflejos del piso en esta escena (0 los apaga).")]
        [SerializeField, Range(0f, 2f)] private float strength = 1f;

        private void OnEnable()
        {
            if (!ActiveInstances.Contains(this)) ActiveInstances.Add(this);
            if (!subscribed)
            {
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                subscribed = true;
            }
            RefreshGlobal();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) RefreshGlobal();
        }

        private void OnDisable()
        {
            ActiveInstances.Remove(this);
            RefreshGlobal();
        }

        private static void OnActiveSceneChanged(Scene previous, Scene current) => RefreshGlobal();

        private static void RefreshGlobal()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            for (int i = ActiveInstances.Count - 1; i >= 0; i--)
            {
                ScreenReflections instance = ActiveInstances[i];
                if (instance == null)
                {
                    ActiveInstances.RemoveAt(i);
                    continue;
                }
                if (instance.gameObject.scene == activeScene)
                {
                    Shader.SetGlobalFloat(StrengthId, instance.strength);
                    return;
                }
            }

            float fallback = ActiveInstances.Count > 0 ? ActiveInstances[ActiveInstances.Count - 1].strength : 0f;
            Shader.SetGlobalFloat(StrengthId, fallback);
        }
    }
}
