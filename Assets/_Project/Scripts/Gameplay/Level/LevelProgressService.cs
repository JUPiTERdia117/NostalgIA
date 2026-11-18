// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: LevelProgressService.cs
// Propósito: aplica las mejoras de herramientas de cada nivel, guarda el progreso
// al cruzar cada puerta y pide la pantalla de resultados al terminar el juego.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Levels;
using NostalgIA.Core.Services;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using System.Collections.Generic;
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
    /// <item><b>Cruce de puerta.</b> Cruzar la puerta del Tutorial (<see cref="LevelGateCrossed"/>)
    /// guarda el progreso del nivel sin mostrar ninguna pantalla: el juego sigue en el nivel nuevo.</item>
    /// <item><b>Fin del juego.</b> La meta final de Verano (<see cref="StationCompleted"/>) detiene
    /// al jugador y publica <see cref="LevelCompleted"/> con el tiempo del nivel, el tiempo de la
    /// partida completa (solo si empezó en el primer nivel) y las estrellas de todo el juego.</item>
    /// </list>
    /// Los tiempos usan <see cref="Time.time"/>, que no avanza en pausa ni durante las pantallas de
    /// carga (ahí la escala de tiempo es 0).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LevelProgressService : MonoBehaviour
    {
        private float levelStartTime;
        /// <summary>Inicio de la partida completa; negativo si no se empezó desde el primer nivel.</summary>
        private float runStartTime = -1f;

        // Estrellas de los niveles ya terminados en esta partida (recogidas y totales), por escena.
        private readonly Dictionary<string, Vector2Int> runStars = new Dictionary<string, Vector2Int>();

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

        /// <summary>
        /// Al cargar un nivel directamente (menú, reinicio o editor) empieza ahí. Si es el primer
        /// nivel del catálogo, también empieza a contar la partida completa.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            runStars.Clear();
            bool firstLevel = LevelCatalog.Levels.Length > 0 && LevelCatalog.Levels[0].Scene == scene.name;
            runStartTime = firstLevel ? Time.time : -1f;
            StartLevel(scene.name, false);
        }

        /// <summary>Cruzar una puerta guarda el nivel de origen (sin pantalla) y empieza el siguiente.</summary>
        private void OnLevelGateCrossed(in LevelGateCrossed evt)
        {
            Scene source = evt.Source;
            if (LevelCatalog.IsPlayable(source.name))
            {
                int stars = StarTracker.Collected(source);
                int total = StarTracker.Total(source);
                GameProgress.SaveCompletion(source.name, stars, total);
                runStars[source.name] = new Vector2Int(stars, total);
            }
            StartLevel(evt.Destination.name, true);
        }

        /// <summary>La puerta final (sin nivel siguiente) termina el juego y muestra los resultados.</summary>
        private void OnStationCompleted(in StationCompleted evt) => Complete(SceneManager.GetActiveScene());

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
        private void Complete(Scene scene)
        {
            if (!LevelCatalog.TryGet(scene.name, out LevelCatalog.Level level)) return;

            int stars = StarTracker.Collected(scene);
            int total = StarTracker.Total(scene);
            GameProgress.SaveCompletion(scene.name, stars, total);
            runStars[scene.name] = new Vector2Int(stars, total);

            // Estrellas de todo el juego: lo de esta partida y, para los niveles que no se jugaron
            // en ella, el mejor resultado guardado.
            int gameStars = 0, gameTotal = 0;
            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
            {
                string name = LevelCatalog.Levels[i].Scene;
                if (runStars.TryGetValue(name, out Vector2Int run))
                {
                    gameStars += run.x;
                    gameTotal += run.y;
                }
                else
                {
                    gameStars += GameProgress.BestStars(name);
                    gameTotal += GameProgress.TotalStars(name);
                }
            }

            PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
            if (motor != null) motor.SetVelocity(Vector3.zero);
            if (ServiceLocator.TryGet(out GameStateMachine machine)) machine.ChangeState(GameState.Results);

            float runTime = runStartTime >= 0f ? Time.time - runStartTime : -1f;
            EventBus<LevelCompleted>.Raise(new LevelCompleted(level.Title, Time.time - levelStartTime, runTime, gameStars, gameTotal));
        }
    }
}
