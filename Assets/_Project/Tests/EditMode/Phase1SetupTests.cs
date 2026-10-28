// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de edición (NostalgIA.Tests.EditMode)
// Archivo: Phase1SetupTests.cs
// Propósito: comprobar que el constructor de la Fase 1 dejó configurados los
// assets, el prefab del jugador y el Tutorial (tareas F1-A14 a F1-A16).
// -----------------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NostalgIA.Core;
using NostalgIA.EditorTools.Setup;
using NostalgIA.Gameplay.Aim;
using NostalgIA.Gameplay.Bootstrap;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>
    /// Pruebas de aceptación del montaje de la Fase 1. Fallan hasta ejecutar
    /// <c>NostalgIA › Fase 1 › Montar todo</c>.
    /// </summary>
    public sealed class Phase1SetupTests
    {
        /// <summary>Los assets de datos existen con los valores del prototipo.</summary>
        [Test]
        public void Datos_ExistenConLosValoresDelPrototipo()
        {
            var player = AssetDatabase.LoadAssetAtPath<NostalgIA.Gameplay.Player.PlayerSettings>(Phase1Builder.Paths.PlayerSettings);
            var vacuum = AssetDatabase.LoadAssetAtPath<VacuumStats>(Phase1Builder.Paths.VacuumStats);
            var grapple = AssetDatabase.LoadAssetAtPath<GrappleStats>(Phase1Builder.Paths.GrappleStats);
            Assert.IsNotNull(player);
            Assert.IsNotNull(vacuum);
            Assert.IsNotNull(grapple);

            Assert.AreEqual(5f, player.walkSpeed);
            Assert.AreEqual(8f, player.sprintSpeed);
            Assert.AreEqual(5f, player.jumpSpeed);
            Assert.AreEqual(3, player.maxHearts);
            Assert.GreaterOrEqual(player.maxAirSpeed, player.sprintSpeed, "El salto corriendo no debe perder velocidad (F1-A06).");
            Assert.LessOrEqual(player.respawnDelay, 1f, "La reaparición debe tardar como máximo 1 s.");
            Assert.AreEqual(1.5f, grapple.cooldown);
            Assert.AreEqual(0.85f, grapple.releaseMomentumRetention);
        }

        /// <summary>El prefab persistente está en una carpeta Resources y se puede cargar.</summary>
        [Test]
        public void GameRoot_SeCargaDesdeResources()
        {
            GameRoot root = Resources.Load<GameRoot>(GameRoot.ResourcePath);
            Assert.IsNotNull(root, "Resources/GameRoot no existe.");
        }

        /// <summary>El prefab del jugador tiene la arquitectura completa de la Fase 1.</summary>
        [Test]
        public void Player_TieneLosComponentesDeLaFase1()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1Builder.Paths.Player);
            Assert.IsNotNull(prefab);
            Assert.AreEqual(Layers.Player, prefab.layer, "El jugador debe estar en la capa Player.");

            Assert.IsNotNull(prefab.GetComponent<PlayerMotor>());
            Assert.IsNotNull(prefab.GetComponent<PlayerHealth>());
            Assert.IsNotNull(prefab.GetComponent<PlayerContactDamage>());
            Assert.IsNotNull(prefab.GetComponent<AimService>());
            Assert.IsNotNull(prefab.GetComponent<ToolController>());
            Assert.IsNotNull(prefab.GetComponent<RespawnService>());
            Assert.IsNotNull(prefab.GetComponentInChildren<VacuumTool>(true));
            Assert.IsNotNull(prefab.GetComponentInChildren<GrappleTool>(true));

            var motor = new SerializedObject(prefab.GetComponent<PlayerMotor>());
            Assert.IsNotNull(motor.FindProperty("settings").objectReferenceValue, "PlayerMotor sin PlayerSettings.");
            var vacuum = new SerializedObject(prefab.GetComponentInChildren<VacuumTool>(true));
            Assert.IsNotNull(vacuum.FindProperty("stats").objectReferenceValue, "VacuumTool sin VacuumStats.");
            var grapple = new SerializedObject(prefab.GetComponentInChildren<GrappleTool>(true));
            Assert.IsNotNull(grapple.FindProperty("stats").objectReferenceValue, "GrappleTool sin GrappleStats.");
        }

        /// <summary>
        /// El Tutorial contiene el volumen de vacío y ya no contiene el controlador monolítico ni
        /// las guías por zona retiradas.
        /// </summary>
        [Test]
        public void Tutorial_TieneVolumenDeVacioYSinComponentesRetirados()
        {
            string scene = File.ReadAllText(SceneNames.Paths.Tutorial);
            string[] killScripts = AssetDatabase.FindAssets("KillVolume t:MonoScript");
            Assert.IsNotEmpty(killScripts);
            StringAssert.Contains("guid: " + killScripts[0], scene, "Falta el KillVolume del Tutorial.");

            // GUID del antiguo TutorialZonePrompt: ya no debe aparecer en la escena.
            StringAssert.DoesNotContain("8272adc31486454f9c6d9b020bb079aa", scene);
        }

        /// <summary>Los once checkpoints del Tutorial tienen índices consecutivos asignados.</summary>
        [Test]
        public void Tutorial_CheckpointsIndexados()
        {
            string scene = File.ReadAllText(SceneNames.Paths.Tutorial);
            string checkpointGuid = AssetDatabase.FindAssets("Checkpoint t:MonoScript")
                is { Length: > 0 } guids ? guids[0] : string.Empty;
            Assert.IsNotEmpty(checkpointGuid);

            MatchCollection blocks = Regex.Matches(scene,
                @"m_Script: \{fileID: 11500000, guid: " + checkpointGuid + @", type: 3\}[\s\S]*?\n  index: (-?\d+)");
            Assert.AreEqual(11, blocks.Count, "Se esperaban 11 checkpoints.");
            var seen = new bool[11];
            foreach (Match m in blocks)
            {
                int index = int.Parse(m.Groups[1].Value);
                Assert.That(index, Is.InRange(0, 10));
                seen[index] = true;
            }
            CollectionAssert.DoesNotContain(seen, false, "Faltan índices de checkpoint.");
        }
    }
}
