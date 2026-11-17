// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: LevelProgressService.cs
// Propósito: aplica las mejoras de herramientas de cada nivel, detecta cuándo se
// completa un nivel, guarda el progreso y pide la pantalla de resultados.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Levels;
using NostalgIA.Core.Services;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Servicio persistente (vive en <c>GameRoot</c>) que lleva el progreso entre niveles.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>Mejoras.</b> Al empezar un nivel (carga de escena o cruce de puerta) fija la
    /// capacidad de la aspiradora y el alcance del gancho que indica <see cref="LevelCatalog"/>.</item>
    /// <item><b>Nivel completado.</b> Cruzar la puerta del Tutorial (<see cref="LevelGateCrossed"/>)
    /// o llegar a la meta final de Verano (<see cref="StationCompleted"/>) completa el nivel: se
    /// guardan las estrellas, se detiene al jugador y se publica <see cref="LevelCompleted"/> con el
    /// estado global en <see cref="GameState.Results"/>.</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LevelProgressService : MonoBehaviour
    {
        private float levelStartTime;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EventBus<LevelGateCrossed>.Subscribe(OnLevelGateCrossed);
            EventBus<StationCompleted>.Subscribe(OnStationCompleted);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EventBus<LevelGateCrossed>.Unsubscribe(OnLevelGateCrossed);
            EventBus<StationCompleted>.Unsubscribe(OnStationCompleted);
        }

        /// <summary>Al cargar un nivel directamente (menú, reinicio o editor) empieza ahí.</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) StartLevel(scene.name, false);
        }

        /// <summary>Cruzar una puerta completa el nivel de origen y empieza el siguiente.</summary>
        private void OnLevelGateCrossed(in LevelGateCrossed evt)
        {
            Complete(evt.Source, true, UpgradeText(evt.Source.name, evt.Destination.name));
            StartLevel(evt.Destination.name, true);
        }

        /// <summary>La puerta final (sin nivel siguiente) completa el nivel activo.</summary>
        private void OnStationCompleted(in StationCompleted evt) => Complete(SceneManager.GetActiveScene(), false, string.Empty);

        /// <summary>Aplica las mejoras del nivel y empieza a contar su tiempo.</summary>
        private void StartLevel(string scene, bool announce)
        {
            if (!LevelCatalog.TryGet(scene, out LevelCatalog.Level level)) return;
            levelStartTime = Time.time;

            VacuumTool vacuum = FindAnyObjectByType<VacuumTool>(FindObjectsInactive.Include);
            if (vacuum != null) vacuum.SetCapacity(level.VacuumCapacity);
            GrappleTool grapple = FindAnyObjectByType<GrappleTool>(FindObjectsInactive.Include);
            if (grapple != null) grapple.SetRange(grapple.BaseRange * level.GrappleRangeMultiplier);

            // El HUD lee la capacidad nueva al recibir este evento.
            if (announce) EventBus<UpgradeApplied>.Raise(new UpgradeApplied(ToolId.Vacuum, level.VacuumCapacity));
        }

        /// <summary>Guarda el resultado, detiene al jugador y pide la pantalla de resultados.</summary>
        private void Complete(Scene scene, bool hasNext, string upgradeText)
        {
            if (!LevelCatalog.TryGet(scene.name, out LevelCatalog.Level level)) return;

            int stars = StarTracker.Collected(scene);
            int total = StarTracker.Total(scene);
            GameProgress.SaveCompletion(scene.name, stars, total);

            PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
            if (motor != null) motor.SetVelocity(Vector3.zero);
            if (ServiceLocator.TryGet(out GameStateMachine machine)) machine.ChangeState(GameState.Results);

            EventBus<LevelCompleted>.Raise(new LevelCompleted(level.Title, stars, total, Time.time - levelStartTime, hasNext, upgradeText));
        }

        /// <summary>Describe la mejora entre dos niveles (por ejemplo, «Aspiradora: 3 → 8 objetos»).</summary>
        private static string UpgradeText(string from, string to)
        {
            if (!LevelCatalog.TryGet(from, out LevelCatalog.Level a) || !LevelCatalog.TryGet(to, out LevelCatalog.Level b))
                return string.Empty;
            string text = string.Empty;
            if (b.VacuumCapacity > a.VacuumCapacity)
                text += $"Aspiradora: {a.VacuumCapacity} → {b.VacuumCapacity} objetos";
            if (b.GrappleRangeMultiplier > a.GrappleRangeMultiplier)
            {
                if (text.Length > 0) text += "\n";
                text += $"Gancho: +{Mathf.RoundToInt((b.GrappleRangeMultiplier / a.GrappleRangeMultiplier - 1f) * 100f)} % de alcance";
            }
            return text;
        }
    }
}
