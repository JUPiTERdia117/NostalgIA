// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de edición (NostalgIA.Tests.EditMode)
// Archivo: InputAndToolsTests.cs
// Propósito: verificar el asset de acciones contra la tabla de controles del GDD,
// el almacén FIFO de la aspiradora y las máscaras de puntería
// (tareas F1-A02, A07, A09 y A10).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using NostalgIA.Core;
using NostalgIA.Gameplay.Aim;
using NostalgIA.Gameplay.Tools;
using NostalgIA.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>Pruebas de entrada y herramientas que no requieren modo de juego.</summary>
    public sealed class InputAndToolsTests
    {
        private const string ActionsPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";

        private static InputActionAsset LoadActions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            Assert.IsNotNull(asset, $"No existe {ActionsPath}.");
            return asset;
        }

        /// <summary>Recoge las rutas de los bindings de una acción.</summary>
        private static List<string> Paths(InputAction action)
        {
            var result = new List<string>();
            foreach (InputBinding b in action.bindings) result.Add(b.path);
            return result;
        }

        /// <summary>El asset tiene exactamente los mapas y acciones del TDD §7.1.</summary>
        [Test]
        public void Acciones_MapasYAccionesDelTDD()
        {
            InputActionAsset asset = LoadActions();
            Assert.AreEqual(2, asset.actionMaps.Count, "Solo deben existir los mapas Gameplay y UI.");

            InputActionMap gameplay = asset.FindActionMap(InputActionNames.GameplayMap);
            Assert.IsNotNull(gameplay);
            Assert.AreEqual(InputActionNames.GameplayActions.Length, gameplay.actions.Count);
            foreach (string name in InputActionNames.GameplayActions)
                Assert.IsNotNull(gameplay.FindAction(name), $"Falta la acción {name}.");

            InputActionMap ui = asset.FindActionMap(InputActionNames.UIMap);
            Assert.IsNotNull(ui);
            foreach (string name in InputActionNames.UIActions)
                Assert.IsNotNull(ui.FindAction(name), $"Falta la acción de interfaz {name}.");
        }

        /// <summary>Solo se conservan los esquemas KeyboardMouse, Gamepad y Touch.</summary>
        [Test]
        public void Acciones_TresEsquemasDeControl()
        {
            InputActionAsset asset = LoadActions();
            Assert.AreEqual(InputActionNames.ControlSchemes.Length, asset.controlSchemes.Count);
            foreach (string scheme in InputActionNames.ControlSchemes)
                Assert.IsTrue(asset.FindControlSchemeIndex(scheme) >= 0, $"Falta el esquema {scheme}.");
        }

        /// <summary>Los controles coinciden con las tablas del GDD (§2.2 y §2.3).</summary>
        [TestCase(InputActionNames.Jump, "<Keyboard>/space", "<Gamepad>/buttonSouth")]
        [TestCase(InputActionNames.Sprint, "<Keyboard>/leftShift", "<Gamepad>/leftStickPress")]
        [TestCase(InputActionNames.Crouch, "<Keyboard>/c", "<Gamepad>/rightStickPress")]
        [TestCase(InputActionNames.SwitchTool, "<Keyboard>/f", "<Gamepad>/buttonNorth")]
        [TestCase(InputActionNames.Absorb, "<Mouse>/rightButton", "<Gamepad>/leftTrigger")]
        [TestCase(InputActionNames.Fire, "<Mouse>/leftButton", "<Gamepad>/rightTrigger")]
        [TestCase(InputActionNames.Grapple, "<Mouse>/rightButton", "<Gamepad>/rightTrigger")]
        [TestCase(InputActionNames.Pause, "<Keyboard>/escape", "<Gamepad>/start")]
        [TestCase(InputActionNames.LookDelta, "<Mouse>/delta", null)]
        [TestCase(InputActionNames.LookStick, "<Gamepad>/rightStick", null)]
        public void Acciones_BindingsDelGDD(string action, string keyboardMouse, string gamepad)
        {
            InputAction a = LoadActions().FindActionMap(InputActionNames.GameplayMap).FindAction(action);
            List<string> paths = Paths(a);
            if (keyboardMouse != null) CollectionAssert.Contains(paths, keyboardMouse, $"{action}: falta {keyboardMouse}.");
            if (gamepad != null) CollectionAssert.Contains(paths, gamepad, $"{action}: falta {gamepad}.");
        }

        /// <summary>El almacén es FIFO: el primero en entrar es el primero en salir.</summary>
        [Test]
        public void VacuumStorage_EsFIFOYRespetaLaCapacidad()
        {
            var objects = new GameObject[3];
            var items = new Absorbable[3];
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    objects[i] = new GameObject("Absorbible_" + i);
                    items[i] = objects[i].AddComponent<Absorbable>();
                }

                var storage = new VacuumStorage(2);
                Assert.IsTrue(storage.TryStore(items[0]));
                Assert.IsTrue(storage.TryStore(items[1]));
                Assert.IsFalse(storage.TryStore(items[2]), "No debe superar la capacidad.");
                Assert.IsTrue(storage.IsFull);
                Assert.AreEqual(1f, storage.Fill01);

                Assert.IsTrue(storage.TryPeek(out Absorbable front));
                Assert.AreSame(items[0], front, "El frente debe ser el primero absorbido.");
                Assert.AreEqual(2, storage.Count, "Consultar el frente no debe consumirlo.");
                Assert.AreSame(items[0], storage.Dequeue());
                Assert.AreSame(items[1], storage.Dequeue());
            }
            finally
            {
                foreach (GameObject go in objects) if (go != null) Object.DestroyImmediate(go);
            }
        }

        /// <summary>Una mejora de capacidad nunca descarta objetos ya guardados.</summary>
        [Test]
        public void VacuumStorage_SetCapacityNoBajaDeLoAlmacenado()
        {
            // Absorbable no admite dos componentes en el mismo objeto: se usan dos objetos.
            var a = new GameObject("Absorbible_A");
            var b = new GameObject("Absorbible_B");
            try
            {
                var storage = new VacuumStorage(3);
                Assert.IsTrue(storage.TryStore(a.AddComponent<Absorbable>()));
                Assert.IsTrue(storage.TryStore(b.AddComponent<Absorbable>()));
                storage.SetCapacity(1);
                Assert.AreEqual(2, storage.Capacity);
                storage.SetCapacity(8);
                Assert.AreEqual(8, storage.Capacity);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        /// <summary>
        /// La puntería ignora al jugador, los volúmenes lógicos, los fragmentos y los proyectiles
        /// propios, y sí detecta absorbibles, anclajes y enemigos.
        /// </summary>
        [Test]
        public void AimService_MascaraExcluyeAlJugadorYTriggers()
        {
            int mask = AimService.AimMask;
            Assert.IsFalse(Layers.Contains(mask, Layers.Player));
            Assert.IsFalse(Layers.Contains(mask, Layers.Trigger));
            Assert.IsFalse(Layers.Contains(mask, Layers.Debris));
            Assert.IsFalse(Layers.Contains(mask, Layers.PlayerProjectile));
            Assert.IsFalse(Layers.Contains(mask, Layers.IgnoreRaycast));
            Assert.IsTrue(Layers.Contains(mask, Layers.Absorbable));
            Assert.IsTrue(Layers.Contains(mask, Layers.Grapple));
            Assert.IsTrue(Layers.Contains(mask, Layers.Enemy));
            Assert.IsTrue(Layers.Contains(mask, Layers.Default));
        }

        /// <summary>
        /// Criterio de rendimiento de la Fase 1: ningún script de juego llama a
        /// <c>Physics.RaycastAll</c> (que asigna un arreglo por llamada).
        /// </summary>
        [Test]
        public void Scripts_NoUsanRaycastAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Project/Scripts" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Editor/")) continue;
                string text = System.IO.File.ReadAllText(path);
                StringAssert.DoesNotContain("Physics.RaycastAll(", text, path);
            }
        }
    }
}
