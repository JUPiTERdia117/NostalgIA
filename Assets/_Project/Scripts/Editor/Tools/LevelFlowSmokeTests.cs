// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: LevelFlowSmokeTests.cs
// Propósito: pruebas rápidas del recorrido entre niveles en modo de juego:
// puertas de nivel (precarga, despertar y cruce), combate contra la IA y portal
// a la estación final.
// -----------------------------------------------------------------------------

using System.IO;
using System.Reflection;
using System.Text;
using NostalgIA.Core;
using NostalgIA.Gameplay.Aim;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.EditorTools.Tools
{
    /// <summary>
    /// Recorre el final sin jugar las tres oleadas. Con la escena de Invierno en modo de juego:
    /// <list type="number">
    /// <item>Vence a la IA con el atajo de prueba de <see cref="AIBossController"/>.</item>
    /// <item>Coloca al jugador en el piso, 2,5 m a un lado del centro (no justo debajo, para
    /// probar un caso menos ideal).</item>
    /// <item>Espera a que el portal termine de precargar y despertar la estación final.</item>
    /// <item>Dispara el gancho de verdad hacia la esfera (mismo método que usa el botón).</item>
    /// <item>Comprueba que la escena activa pase a ser la estación final.</item>
    /// </list>
    /// El resultado queda en la consola y en <c>Temp/final_portal_test.txt</c>.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelFlowSmokeTests
    {
        private enum Step { Idle, Fight, WaitGate, Grapple, WaitCross, DoorPreload, DoorBaseline, DoorWake, DoorCross }

        private static Step step = Step.Idle;
        private static double deadline;
        private static LevelGate gate;
        private static Transform anchor;
        private static PlayerMotor motor;
        private static float defeatTime;
        private static AIBossController fightBoss;
        private static double nextFightTick;
        private static string doorDestination;
        private static float stepStart;
        private static float worstFrame;
        private static float baselineWorst;
        private static int slowFrames;
        private static readonly StringBuilder log = new StringBuilder();

        static LevelFlowSmokeTests()
        {
            EditorApplication.update += Update;
        }

        /// <summary>
        /// Variante completa: lleva al jugador al centro de la arena para que empiece el combate y,
        /// cada medio segundo, mata a los enemigos activos de las oleadas (con el mismo
        /// <c>EnemyHealth.Die</c> que usa el juego) y rompe el monitor que esté expuesto (con el
        /// mismo <c>ReceiveImpact</c> que recibe al chocar un objeto). Comprueba que las tres
        /// oleadas y los tres monitores lleven a la derrota de la IA y después sigue con la prueba
        /// del portal.
        /// </summary>
        [MenuItem("NostalgIA/Depuración/Probar combate completo contra la IA (juego)", priority = 921)]
        private static void RunFullFight()
        {
            log.Clear();
            fightBoss = Object.FindAnyObjectByType<AIBossController>();
            motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (!Application.isPlaying || fightBoss == null || motor == null)
            {
                Report("Entra al modo de juego con 40_Invierno_IA primero.");
                return;
            }
            motor.Teleport(fightBoss.transform.position + new Vector3(0f, 0.1f, -6f), Quaternion.identity);
            motor.SetVelocity(Vector3.zero);
            Report("Jugador en la arena: esperando a que arranque la Oleada 1.");
            step = Step.Fight;
            deadline = EditorApplication.timeSinceStartup + 120.0;
            nextFightTick = EditorApplication.timeSinceStartup + 1.0;
        }

        /// <summary>Un paso del combate simulado.</summary>
        private static void FightTick()
        {
            string phase = typeof(AIBossController).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(fightBoss)?.ToString();
            if (phase == "Defeated")
            {
                Report("OK: las tres oleadas y los tres monitores llevaron a la derrota de la IA.");
                StartPortalCheck(fightBoss);
                return;
            }
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Report($"FALLA: el combate se quedó en la fase {phase}.");
                step = Step.Idle;
                return;
            }
            if (EditorApplication.timeSinceStartup < nextFightTick) return;
            nextFightTick = EditorApplication.timeSinceStartup + 0.5;

            var bossSettings = new SerializedObject(fightBoss);
            int killed = 0;
            foreach (string wave in new[] { "wave1Container", "wave2Container", "wave3Container" })
            {
                var container = bossSettings.FindProperty(wave).objectReferenceValue as Transform;
                if (container == null || !container.gameObject.activeInHierarchy) continue;
                for (int i = 0; i < container.childCount; i++)
                {
                    Transform enemy = container.GetChild(i);
                    if (!enemy.gameObject.activeInHierarchy || enemy.GetComponent("EnemyHealth") == null) continue;
                    enemy.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
                    killed++;
                }
            }
            if (killed > 0) Report($"Fase {phase}: {killed} enemigos derrotados.");

            SerializedProperty monitors = bossSettings.FindProperty("wallMonitors");
            for (int i = 0; i < monitors.arraySize; i++)
            {
                var monitor = monitors.GetArrayElementAtIndex(i).objectReferenceValue as AIMonitorTarget;
                if (monitor == null || !monitor.IsVulnerable) continue;
                monitor.ReceiveImpact(monitor.transform.position, 50f);
                Report($"Fase {phase}: monitor {i + 1} roto.");
            }
        }

        /// <summary>
        /// Prueba la puerta de nivel de la escena activa como la vive el jugador: espera lejos a que
        /// el nivel siguiente se precargue y se precalienten sus shaders, mide el peor frame durante
        /// 2 s, se acerca a 6 m (el nivel siguiente despierta) y vuelve a medir 3 s; al final cruza
        /// con el mismo atajo del menú de pausa y comprueba la escena activa.
        /// </summary>
        [MenuItem("NostalgIA/Depuración/Probar puerta de nivel de esta escena (juego)", priority = 922)]
        private static void RunDoorTest()
        {
            log.Clear();
            motor = Object.FindAnyObjectByType<PlayerMotor>();
            gate = null;
            foreach (LevelGate candidate in Object.FindObjectsByType<LevelGate>(FindObjectsSortMode.None))
                if (candidate.HasValidDestination && candidate.gameObject.scene == SceneManager.GetActiveScene()) gate = candidate;
            if (!Application.isPlaying || gate == null || motor == null)
            {
                Report("Entra al modo de juego en un nivel con puerta de salida.");
                return;
            }
            doorDestination = gate.DestinationSceneName;
            Report($"Puerta {gate.name} → {doorDestination}. Distancia inicial {Vector3.Distance(motor.transform.position, gate.transform.position):0} m.");
            step = Step.DoorPreload;
            stepStart = Time.realtimeSinceStartup;
        }

        /// <summary>Pasos de la prueba de la puerta.</summary>
        private static void DoorTick()
        {
            float elapsed = Time.realtimeSinceStartup - stepStart;
            float dt = Time.unscaledDeltaTime;
            switch (step)
            {
                case Step.DoorPreload:
                    if (gate.Destination.IsValid() && gate.Destination.isLoaded && elapsed > 8f)
                    {
                        Report("Nivel siguiente precargado; precalentado de shaders con margen de 8 s.");
                        step = Step.DoorBaseline;
                        stepStart = Time.realtimeSinceStartup;
                        worstFrame = 0f;
                    }
                    else if (elapsed > 40f)
                    {
                        Report("FALLA: el nivel siguiente no se precargó en 40 s.");
                        step = Step.Idle;
                    }
                    break;

                case Step.DoorBaseline:
                    if (elapsed > 0.2f) worstFrame = Mathf.Max(worstFrame, dt);
                    if (elapsed > 2f)
                    {
                        baselineWorst = worstFrame;
                        Vector3 front = gate.transform.position + Vector3.ProjectOnPlane(gate.transform.forward, Vector3.up).normalized * 6f;
                        motor.Teleport(front + Vector3.up * 0.1f, Quaternion.LookRotation(-Vector3.ProjectOnPlane(gate.transform.forward, Vector3.up)));
                        motor.SetVelocity(Vector3.zero);
                        step = Step.DoorWake;
                        stepStart = Time.realtimeSinceStartup;
                        worstFrame = 0f;
                        slowFrames = 0;
                    }
                    break;

                case Step.DoorWake:
                    // El primer frame tras el salto incluye el propio traslado; mido desde el segundo.
                    if (elapsed > 0.05f)
                    {
                        worstFrame = Mathf.Max(worstFrame, dt);
                        if (dt > 0.05f) slowFrames++;
                        if (dt > 0.03f) Report($"  frame {Time.frameCount}: {dt * 1000f:0.0} ms a {elapsed:0.00} s del salto (despierto: {gate.IsDestinationAwake}).");
                    }
                    if (elapsed > 3f)
                    {
                        Report($"Peor frame lejos: {baselineWorst * 1000f:0.0} ms. Peor frame al despertar: {worstFrame * 1000f:0.0} ms; frames de más de 50 ms: {slowFrames}. Despierto: {gate.IsDestinationAwake}.");
                        gate.TriggerImmediateSkip();
                        step = Step.DoorCross;
                        stepStart = Time.realtimeSinceStartup;
                    }
                    break;

                case Step.DoorCross:
                    if (SceneManager.GetActiveScene().name == doorDestination)
                    {
                        Report($"OK: cruzó a {doorDestination}; jugador en {motor.transform.position}.");
                        step = Step.Idle;
                    }
                    else if (elapsed > 10f)
                    {
                        Report("FALLA: no cruzó en 10 s.");
                        step = Step.Idle;
                    }
                    break;
            }
        }

        /// <summary>Inicia la prueba (solo en modo de juego con Invierno cargado).</summary>
        [MenuItem("NostalgIA/Depuración/Probar final: vencer a la IA y cruzar el portal (juego)", priority = 920)]
        private static void Run()
        {
            log.Clear();
            if (!Application.isPlaying)
            {
                Report("Entra al modo de juego con 40_Invierno_IA primero.");
                return;
            }

            AIBossController boss = Object.FindAnyObjectByType<AIBossController>();
            motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (boss == null || motor == null)
            {
                Report("No encontré AIBossController o PlayerMotor.");
                return;
            }

            var bossSettings = new SerializedObject(boss);
            var portal = bossSettings.FindProperty("victoryPortal").objectReferenceValue as GameObject;
            var sphere = bossSettings.FindProperty("victoryGrappleAnchor").objectReferenceValue as GameObject;
            if (portal == null || sphere == null)
            {
                Report("El jefe no tiene portal o esfera de gancho asignados.");
                return;
            }

            typeof(AIBossController).GetMethod("DebugDefeat", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(boss, null);
            StartPortalCheck(boss);
        }

        /// <summary>Con la IA vencida: coloca al jugador y espera al portal.</summary>
        private static void StartPortalCheck(AIBossController boss)
        {
            var bossSettings = new SerializedObject(boss);
            var portal = bossSettings.FindProperty("victoryPortal").objectReferenceValue as GameObject;
            var sphere = bossSettings.FindProperty("victoryGrappleAnchor").objectReferenceValue as GameObject;
            Report($"IA vencida. Portal activo: {portal.activeInHierarchy}. Esfera activa: {sphere.activeInHierarchy}.");

            defeatTime = Time.time;
            gate = portal.GetComponent<LevelGate>();
            anchor = sphere.transform;
            Vector3 feet = new Vector3(anchor.position.x + 2.5f, boss.transform.position.y + 0.1f, anchor.position.z);
            motor.Teleport(feet, Quaternion.LookRotation(Vector3.left));
            motor.SetVelocity(Vector3.zero);
            // Levanto la mirada hacia el portal para poder revisarlo en la vista de juego.
            typeof(PlayerMotor).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(motor, -55f);
            step = Step.WaitGate;
            deadline = EditorApplication.timeSinceStartup + 15.0;
        }

        private static void Update()
        {
            if (step == Step.Idle) return;
            if (!Application.isPlaying)
            {
                step = Step.Idle;
                return;
            }

            switch (step)
            {
                case Step.Fight:
                    FightTick();
                    break;

                case Step.DoorPreload:
                case Step.DoorBaseline:
                case Step.DoorWake:
                case Step.DoorCross:
                    DoorTick();
                    break;

                case Step.WaitGate:
                    // Espero también a que desaparezcan los fragmentos del núcleo (duran hasta 2,5 s),
                    // que si no se interponen entre la cámara y la esfera.
                    if (gate != null && gate.IsDestinationAwake && Time.time > defeatTime + 6f)
                    {
                        Report($"Estación final precargada y despierta. Núcleo de la IA eliminado: {GameObject.Find("TEST_NucleoCentral_IA") == null}.");
                        step = Step.Grapple;
                    }
                    else if (EditorApplication.timeSinceStartup > deadline)
                    {
                        Report("FALLA: la estación final no despertó en 15 s.");
                        step = Step.Idle;
                    }
                    break;

                case Step.Grapple:
                    FireGrapple();
                    step = Step.WaitCross;
                    deadline = EditorApplication.timeSinceStartup + 6.0;
                    break;

                case Step.WaitCross:
                    if (SceneManager.GetActiveScene().name == SceneNames.EstacionFinal)
                    {
                        Report($"OK: el jugador cruzó a {SceneNames.EstacionFinal} y apareció en {motor.transform.position}.");
                        step = Step.Idle;
                    }
                    else if (EditorApplication.timeSinceStartup > deadline)
                    {
                        Report($"FALLA: no cruzó. Posición del jugador {motor.transform.position}.");
                        step = Step.Idle;
                    }
                    break;
            }
        }

        /// <summary>Apunta a la esfera desde la cámara y dispara el gancho con su propio método.</summary>
        private static void FireGrapple()
        {
            Camera camera = Camera.main;
            GrappleTool grapple = motor.GetComponentInChildren<GrappleTool>(true);
            Vector3 origin = camera != null ? camera.transform.position : motor.transform.position + Vector3.up * 1.6f;
            var ray = new Ray(origin, (anchor.position - origin).normalized);
            // Ignoro los fragmentos sueltos del núcleo que todavía estén en el aire.
            RaycastHit hit = default;
            bool found = false;
            RaycastHit[] hits = Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit candidate in hits)
            {
                if (candidate.collider.name == "Gib_Debris" || candidate.collider.GetComponentInParent<PlayerMotor>() != null) continue;
                hit = candidate;
                found = true;
                break;
            }
            if (!found)
            {
                Report("FALLA: el rayo hacia la esfera no pegó en nada.");
                return;
            }
            Report($"El rayo del gancho pegó en '{hit.collider.name}' (capa {LayerMask.LayerToName(hit.collider.gameObject.layer)}).");

            var aim = new AimSample(ray, true, hit.point, hit.normal, hit.distance, hit.collider, default);
            typeof(GrappleTool).GetMethod("TryFire", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(grapple, new object[] { aim });
            Report($"Gancho enganchado: {grapple.IsAttached}.");
        }

        private static void Report(string message)
        {
            Debug.Log("[Prueba final] " + message);
            log.AppendLine(message);
            File.WriteAllText("Temp/final_portal_test.txt", log.ToString());
        }
    }
}
