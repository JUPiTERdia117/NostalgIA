// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de edición (NostalgIA.Tests.EditMode)
// Archivo: LayersTests.cs
// Propósito: verificar las constantes de capas y la configuración física del
// proyecto (TDD §13).
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NUnit.Framework;
using UnityEngine;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>
    /// Pruebas de <see cref="Layers"/> y de la matriz de colisiones configurada en el proyecto.
    /// </summary>
    /// <remarks>
    /// Las pruebas de configuración leen los ajustes reales del proyecto (Tags and Layers y la
    /// matriz de colisiones). Así se detecta si alguien renombra una capa o modifica una colisión
    /// sin actualizar el TDD y las constantes del núcleo.
    /// </remarks>
    public sealed class LayersTests
    {
        /// <summary>
        /// Cada máscara debe ser exactamente <c>1 &lt;&lt; índice</c>: un único bit encendido en la
        /// posición de su capa.
        /// </summary>
        [Test]
        public void Masks_CorrespondenAlBitDeCadaIndice()
        {
            Assert.AreEqual(1 << Layers.Default, Layers.Masks.Default);
            Assert.AreEqual(1 << Layers.Wall, Layers.Masks.Wall);
            Assert.AreEqual(1 << Layers.Water, Layers.Masks.Water);
            Assert.AreEqual(1 << Layers.Player, Layers.Masks.Player);
            Assert.AreEqual(1 << Layers.Absorbable, Layers.Masks.Absorbable);
            Assert.AreEqual(1 << Layers.Grapple, Layers.Masks.Grapple);
            Assert.AreEqual(1 << Layers.Enemy, Layers.Masks.Enemy);
            Assert.AreEqual(1 << Layers.Hazard, Layers.Masks.Hazard);
            Assert.AreEqual(1 << Layers.PlayerProjectile, Layers.Masks.PlayerProjectile);
            Assert.AreEqual(1 << Layers.EnemyProjectile, Layers.Masks.EnemyProjectile);
            Assert.AreEqual(1 << Layers.Debris, Layers.Masks.Debris);
            Assert.AreEqual(1 << Layers.Trigger, Layers.Masks.Trigger);
        }

        /// <summary>
        /// Los objetos ya serializados en escenas y prefabs usan los índices 7 y 8. El renombre de
        /// «Poolable» y «Graple» solo es seguro si esos índices no cambian.
        /// </summary>
        [Test]
        public void IndicesHeredados_SeConservanTrasElRenombre()
        {
            Assert.AreEqual(7, Layers.Absorbable, "La capa Absorbable debe conservar el índice de Poolable.");
            Assert.AreEqual(8, Layers.Grapple, "La capa Grapple debe conservar el índice de Graple.");
        }

        /// <summary>La geometría sólida incluye nivel, paredes y anclajes, y excluye al jugador y a los absorbibles.</summary>
        [Test]
        public void SolidGeometry_IncluyeNivelParedesYAnclajes()
        {
            int mask = Layers.Masks.SolidGeometry;
            Assert.IsTrue(Layers.Contains(mask, Layers.Default));
            Assert.IsTrue(Layers.Contains(mask, Layers.Wall));
            Assert.IsTrue(Layers.Contains(mask, Layers.Grapple));
            Assert.IsFalse(Layers.Contains(mask, Layers.Player));
            Assert.IsFalse(Layers.Contains(mask, Layers.Absorbable));
            Assert.IsFalse(Layers.Contains(mask, Layers.Trigger));
        }

        /// <summary><see cref="Layers.Contains"/> solo reconoce el bit de la capa consultada.</summary>
        [Test]
        public void Contains_DetectaSoloLaCapaConsultada()
        {
            int mask = Layers.Masks.Enemy | Layers.Masks.Hazard;
            Assert.IsTrue(Layers.Contains(mask, Layers.Enemy));
            Assert.IsTrue(Layers.Contains(mask, Layers.Hazard));
            Assert.IsFalse(Layers.Contains(mask, Layers.Player));
            Assert.IsFalse(Layers.Contains(0, Layers.Default));
        }

        /// <summary>La tabla de nombres cubre las 32 capas que admite Unity.</summary>
        [Test]
        public void ExpectedNames_TieneTreintaYDosPosiciones()
        {
            Assert.AreEqual(32, Layers.ExpectedNames.Count);
        }

        /// <summary>Los nombres configurados en Tags and Layers coinciden con el TDD (§13).</summary>
        [Test]
        public void ExpectedNames_CoincidenConLaConfiguracionDelProyecto()
        {
            for (int layer = 0; layer < Layers.ExpectedNames.Count; layer++)
            {
                string expected = Layers.ExpectedNames[layer];
                if (expected == null) continue;
                Assert.AreEqual(expected, LayerMask.LayerToName(layer), $"Nombre de la capa {layer}.");
            }
        }

        /// <summary>
        /// Los pares que el TDD declara sin colisión deben estar ignorados en la matriz de física.
        /// </summary>
        [Test]
        public void MatrizDeColisiones_IgnoraLosParesDelTDD()
        {
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Player, Layers.Debris));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Player, Layers.PlayerProjectile));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Enemy, Layers.EnemyProjectile));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Debris, Layers.Enemy));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Debris, Layers.Debris));
        }

        /// <summary>
        /// Prueba de regresión: las colisiones de las que depende el prototipo actual deben seguir
        /// activas después de editar la matriz.
        /// </summary>
        [Test]
        public void MatrizDeColisiones_ConservaLasColisionesDelPrototipo()
        {
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Player, Layers.Default), "Jugador contra el nivel.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Player, Layers.Wall), "Jugador contra paredes.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Player, Layers.Absorbable), "Jugador contra cajas absorbibles.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Player, Layers.Grapple), "Jugador contra superficies de anclaje.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Absorbable, Layers.Default), "Cajas contra el nivel.");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Absorbable, Layers.Absorbable), "Cajas entre sí.");
        }

        /// <summary>La capa Trigger solo debe interactuar con el jugador.</summary>
        [Test]
        public void MatrizDeColisiones_TriggerSoloInteractuaConElJugador()
        {
            for (int other = 0; other < 32; other++)
            {
                bool ignored = Physics.GetIgnoreLayerCollision(Layers.Trigger, other);
                if (other == Layers.Player)
                {
                    Assert.IsFalse(ignored, "Trigger debe detectar al jugador.");
                }
                else
                {
                    Assert.IsTrue(ignored, $"Trigger no debe interactuar con la capa {other}.");
                }
            }
        }
    }
}
