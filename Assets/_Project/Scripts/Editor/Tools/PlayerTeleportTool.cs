// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: PlayerTeleportTool.cs
// Propósito: mover al jugador desde la vista de escena durante el modo de juego,
// con un manipulador colocado exactamente sobre la cápsula del jugador.
// -----------------------------------------------------------------------------
using NostalgIA.Core;
using NostalgIA.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace NostalgIA.EditorTools.Tools
{
    /// <summary>
    /// Herramienta de depuración para reubicar al jugador desde la vista de escena en modo de juego.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El manipulador de movimiento estándar de Unity aparece en el centro de los límites de toda la
    /// jerarquía seleccionada cuando el pivote está en modo <i>Center</i>. El prefab del jugador
    /// incluye lienzos de interfaz (retícula y controles táctiles), cuyos límites desplazan ese centro
    /// lejos de la cápsula; por eso el manipulador no coincidía con el jugador. Además, mover el
    /// <see cref="Transform"/> a mano compite con el <see cref="CharacterController"/>.
    /// </para>
    /// <para>
    /// En este módulo se dibuja un manipulador propio en los pies del jugador y la cápsula real en
    /// alambre; al arrastrarlo se llama a <see cref="PlayerMotor.Teleport"/>, que desactiva el
    /// controlador durante el cambio y anula la velocidad. Atajos:
    /// <list type="bullet">
    /// <item><b>Ctrl + Mayús + clic</b> (Cmd + Mayús en macOS) en la vista de escena: el jugador
    /// aparece sobre la superficie bajo el cursor.</item>
    /// <item>Menú <i>NostalgIA › Depuración › Traer jugador a la vista de escena</i>
    /// (Ctrl/Cmd + Mayús + T): lo coloca en el suelo bajo el centro de la vista.</item>
    /// </list>
    /// Solo actúa en modo de juego y no forma parte del build.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    public static class PlayerTeleportTool
    {
        private const string EnabledPref = "NostalgIA.PlayerTeleportTool.Enabled";
        private const string MenuToggle = "NostalgIA/Depuración/Manipulador del jugador en escena (juego)";

        private static PlayerMotor motor;

        static PlayerTeleportTool()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static bool IsEnabled
        {
            get => EditorPrefs.GetBool(EnabledPref, true);
            set => EditorPrefs.SetBool(EnabledPref, value);
        }

        /// <summary>Activa o desactiva el manipulador.</summary>
        [MenuItem(MenuToggle, priority = 910)]
        private static void Toggle() => IsEnabled = !IsEnabled;

        [MenuItem(MenuToggle, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuToggle, IsEnabled);
            return true;
        }

        /// <summary>Coloca al jugador en el suelo bajo el centro de la última vista de escena.</summary>
        [MenuItem("NostalgIA/Depuración/Traer jugador a la vista de escena %#t", priority = 911)]
        private static void BringPlayerToSceneView()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (!Application.isPlaying || view == null || !FindMotor()) return;
            Vector3 origin = view.camera.transform.position;
            Vector3 target = view.pivot;
            if (Physics.Raycast(target + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 500f, SolidMask, QueryTriggerInteraction.Ignore))
                target = hit.point;
            Vector3 look = target - origin;
            TeleportTo(target, look);
        }

        [MenuItem("NostalgIA/Depuración/Traer jugador a la vista de escena %#t", true)]
        private static bool BringValidate() => Application.isPlaying;

        /// <summary>
        /// Coloca al jugador 4 m delante del portal de salida de la escena activa, mirándolo, para
        /// probar el cruce entre escenas sin recorrer el nivel.
        /// </summary>
        [MenuItem("NostalgIA/Depuración/Llevar jugador al portal (juego)", priority = 912)]
        private static void BringPlayerToPortal()
        {
            if (!Application.isPlaying || !FindMotor()) return;
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Name != "TutorialDoor" || mb.gameObject.scene != motor.gameObject.scene) continue;
                var so = new SerializedObject(mb);
                if (so.FindProperty("isReturnPortal").boolValue) continue;
                Transform door = mb.transform;
                Vector3 back = -Vector3.ProjectOnPlane(door.forward, Vector3.up).normalized;
                Vector3 start = door.position + back * 4f;
                if (Physics.Raycast(start + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 20f, SolidMask, QueryTriggerInteraction.Ignore))
                    start = hit.point;
                TeleportTo(start, -back);
                return;
            }
        }

        private static int SolidMask => ~((1 << Layers.Player) | (1 << Layers.Trigger) | (1 << Layers.Debris));

        private static bool FindMotor()
        {
            // Búsqueda solo en el editor y únicamente cuando se perdió la referencia.
            if (motor == null) motor = Object.FindAnyObjectByType<PlayerMotor>();
            return motor != null;
        }

        private static void TeleportTo(Vector3 feet, Vector3 lookDirection)
        {
            Vector3 flat = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
            Quaternion rotation = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : motor.transform.rotation;
            motor.Teleport(feet + Vector3.up * 0.05f, rotation);
            motor.SetVelocity(Vector3.zero);
        }

        private static void OnSceneGui(SceneView view)
        {
            if (!Application.isPlaying || !IsEnabled || !FindMotor()) return;

            Event e = Event.current;
            CharacterController cc = motor.Controller;
            Vector3 feet = motor.transform.position;

            // Cápsula real del jugador en alambre, para ver dónde está sin depender de los lienzos.
            Handles.color = new Color(0.2f, 1f, 0.6f, 0.9f);
            if (cc != null)
            {
                Vector3 center = motor.CapsuleCenter;
                float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
                Vector3 top = center + Vector3.up * half;
                Vector3 bottom = center - Vector3.up * half;
                Handles.DrawWireDisc(top, Vector3.up, cc.radius);
                Handles.DrawWireDisc(bottom, Vector3.up, cc.radius);
                Handles.DrawLine(top + Vector3.right * cc.radius, bottom + Vector3.right * cc.radius);
                Handles.DrawLine(top - Vector3.right * cc.radius, bottom - Vector3.right * cc.radius);
                Handles.DrawLine(top + Vector3.forward * cc.radius, bottom + Vector3.forward * cc.radius);
                Handles.DrawLine(top - Vector3.forward * cc.radius, bottom - Vector3.forward * cc.radius);
            }
            Handles.Label(feet + Vector3.up * 2.3f, "Jugador  (arrastrar · Ctrl/Cmd+Mayús+clic para teletransportar)");

            // Manipulador propio en los pies, independiente del modo de pivote de la escena.
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(feet, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                TeleportTo(moved, motor.transform.forward);
            }

            // Ctrl/Cmd + Mayús + clic: teletransporte a la superficie bajo el cursor.
            bool modifier = e.shift && (e.control || e.command);
            if (e.type == EventType.MouseDown && e.button == 0 && modifier)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 2000f, SolidMask, QueryTriggerInteraction.Ignore))
                {
                    TeleportTo(hit.point, ray.direction);
                    e.Use();
                }
            }

            // Mantener la vista de escena repintándose para seguir al jugador en movimiento.
            if (e.type == EventType.Repaint) view.Repaint();
        }
    }
}
