// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: Nivel3LaberintoBuilder.cs
// Propósito: construye el Nivel 3 (laberinto circular con dos rutas hacia el
// centro y combate contra la IA) con el kit del Tutorial.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Presentation.Audio;
using NostalgIA.Presentation.Levels;
using UnityEditor;
using UnityEngine;

namespace NostalgIA.EditorTools.Levels
{
    /// <summary>
    /// Nivel 3 según el boceto "NVL FINAL". El laberinto son tres anillos alrededor de la sala de
    /// la IA. Las posiciones van en coordenadas polares: <c>Polar(r, θ, y)</c> = (r·cos θ, y, r·sin θ),
    /// con θ en grados. La puerta de inicio está en θ = 52° (arriba a la derecha del boceto).
    /// </summary>
    /// <remarks>
    /// Anillos: sala de la IA r &lt; 24 m; pasillo interior 24–38 m; pasillo exterior 38–52 m.
    /// Las paredes son del kit del Tutorial (<c>M_Primavera_Kit</c>) en la capa Default, así que el
    /// gancho no se adhiere a ellas. La lava es el prefab <c>Lava</c> del Tutorial.
    /// <para>Recorrido (sentido de las agujas = θ decreciente):</para>
    /// <list type="bullet">
    /// <item>Llegada (θ 52°): pasillo con objetos y dos arañas.</item>
    /// <item>Derecha (θ 65°–124°): callejón sin salida con objetos, WinRar y estrella 1.</item>
    /// <item>Izquierda (θ 28°–−4°): lago de lava con una plataforma; se cruza con una esfera.</item>
    /// <item>Bifurcación en θ −9°. Derecha: entra al pasillo interior (paredes que aplastan, dos
    /// spam mail, callejón con plataforma móvil y estrella 2, lago con dos plataformas y aspas,
    /// estrella 3, vuelta en U con spawn y dos arañas, entrada a la IA).</item>
    /// <item>Adelante: spawn con arañas y spam mail, WinRar, bifurcación; adelante otro WinRar con la
    /// estrella 4 (sin salida); a la derecha el lago con muchas plataformas (derecha: estrella 5;
    /// izquierda: suelo firme, pared y vuelta a la derecha hacia la IA).</item>
    /// </list>
    /// </remarks>
    public static class Nivel3LaberintoBuilder
    {
        /// <summary>Región del Nivel 3.</summary>
        public static readonly Vector3 RegionOrigin = new Vector3(6000f, 0f, 0f);

        private const float ArenaRadius = 24f;
        private const float InnerMid = 31f;
        private const float Ring2 = 38f;
        private const float OuterMid = 45f;
        private const float Ring3 = 52f;
        private const float WallHeight = 6.2f;
        private const float WallThickness = 1.6f;
        private const float LavaY = -0.6f;
        private static int lavaCount;

        [MenuItem("NostalgIA/Niveles/Levels_test/Construir Nivel 3 (Laberinto IA)", priority = 352)]
        public static void Build()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        /// <summary>Construye y guarda la escena (sin preguntar por cambios pendientes).</summary>
        public static void BuildScene()
        {
            LevelKit.BeginScene("Nivel3_Laberinto");
            LevelKit.TutorialAtmosphere(true, new Color(0.04f, 0.06f, 0.16f), 45f, 170f);
            lavaCount = 0;

            BuildFloorsAndLava();
            BuildWalls();
            BuildEntryAndFirstBranches();
            BuildRightRoute();
            BuildForwardRoute();
            BuildAIArena();

            LevelKit.Entrance(Polar(60.5f, 52f, 0f), Inward(52f));
            LevelKit.StandalonePlayer(Polar(58.5f, 52f, 0.15f), Inward(52f));

            var ambient = new GameObject("Audio_AmbienteLaberinto");
            ambient.transform.SetParent(LevelKit.Root, false);
            LevelKit.AudioSlot(ambient, "Ambiente 2D del laberinto (zumbido digital)", AudioSlotTrigger.AmbientOrManual, 0f, 120f, 0.6f);

            LevelKit.EndScene(SceneNames.Paths.Nivel3, RegionOrigin,
                new Bounds(new Vector3(0f, 3f, 0f), new Vector3(136f, 14f, 136f)));
            Debug.Log("[Niveles] Nivel 3 (Laberinto IA) construido en " + SceneNames.Paths.Nivel3);
        }

        // ================================================================== pisos y lagos de lava
        private static void BuildFloorsAndLava()
        {
            Transform floors = LevelKit.Group("Pisos_y_Lava");

            // Sala de la IA y anillos firmes.
            LevelKit.Box("TEST_Piso_SalaIA", floors, new Vector3(0f, -0.5f, 0f), new Vector3(ArenaRadius * 2.05f, 1f, ArenaRadius * 2.05f),
                Quaternion.identity, LevelKit.Kit);
            FloorArc(floors, "Piso_Interior_Firme", InnerMid, 14.6f, -20f, 236f, 28);
            LavaArc(floors, "Lava_Interior_MuchasPlataformas", InnerMid, 13.6f, 236f, 340f, 12);
            FloorArc(floors, "Piso_Exterior_EntradaYDerecha", OuterMid, 14.6f, 28f, 128f, 14);
            LavaArc(floors, "Lava_Exterior_PasilloSimple", OuterMid, 13.6f, -4f, 28f, 5);
            FloorArc(floors, "Piso_Exterior_RutaAdelante", OuterMid, 14.6f, -134f, -4f, 18);
            FloorArc(floors, "Piso_Exterior_CruceOeste", OuterMid, 14.6f, 128f, 148f, 4);
            LavaArc(floors, "Lava_Exterior_DosPlataformas", OuterMid, 13.6f, 148f, 214f, 9);
            FloorArc(floors, "Piso_Exterior_OrillaVueltaU", OuterMid, 14.6f, 214f, 226f, 3);

            // Vestíbulo de la puerta de inicio.
            LevelKit.Box("TEST_Piso_VestibuloInicio", floors, Polar(57.5f, 52f, -0.5f), new Vector3(11.5f, 1f, 14f), Inward(52f), LevelKit.Kit);

            // Respaldo bajo todo el nivel.
            var kill = new GameObject("KillVolume_Respaldo");
            kill.transform.SetParent(floors, false);
            kill.transform.position = new Vector3(0f, -14f, 0f);
            kill.layer = Layers.Hazard;
            var col = kill.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(260f, 4f, 260f);
            kill.AddComponent<KillVolume>();
        }

        private static void FloorArc(Transform parent, string name, float radius, float width, float a0, float a1, int segments)
        {
            Transform group = LevelKit.Child(parent, name);
            float step = (a1 - a0) / segments;
            for (int i = 0; i < segments; i++)
            {
                float mid = a0 + (i + 0.5f) * step;
                float chord = 2f * (radius + width * 0.5f) * Mathf.Sin(Mathf.Abs(step) * 0.5f * Mathf.Deg2Rad) + 1.2f;
                LevelKit.Box($"{name}_{i + 1:00}", group, Polar(radius, mid, -0.5f), new Vector3(width, 1f, chord), Tangent(mid), LevelKit.Kit);
            }
        }

        /// <summary>
        /// Foso de lava curvo hecho con el prefab Lava del Tutorial. La superficie queda 0,6 m bajo
        /// el piso y debajo hay un fondo para que las cajas que caen no se pierdan.
        /// </summary>
        private static void LavaArc(Transform parent, string name, float radius, float width, float a0, float a1, int segments)
        {
            Transform group = LevelKit.Child(parent, name);
            float step = (a1 - a0) / segments;
            for (int i = 0; i < segments; i++)
            {
                float mid = a0 + (i + 0.5f) * step;
                float chord = 2f * (radius + width * 0.45f) * Mathf.Sin(Mathf.Abs(step) * 0.5f * Mathf.Deg2Rad) + 0.9f;
                LevelKit.LavaPatch($"Lava_{name}_{i + 1:00}", group, Polar(radius, mid, LavaY), width, chord, Tangent(mid), lavaCount++ % 3 == 0);
                LevelKit.Box($"FondoFoso_{i + 1:00}", group, Polar(radius, mid, -2.4f), new Vector3(width + 1f, 0.5f, chord + 0.8f), Tangent(mid), LevelKit.Wall);
            }
        }

        // ================================================================== paredes (sin enganche)
        private static void BuildWalls()
        {
            Transform walls = LevelKit.Group("TEST_Paredes_Laberinto");

            // Perímetro exterior, abierto solo en la puerta de inicio (θ 46°–58°) y su vestíbulo.
            WallArc(walls, "Muro_Exterior", Ring3, 58f, 406f, 44);
            WallRadial(walls, "Muro_Vestibulo_Izq", 46f, Ring3, 64f, 2);
            WallRadial(walls, "Muro_Vestibulo_Der", 58f, Ring3, 64f, 2);
            WallArc(walls, "Muro_Vestibulo_Fondo", 64f, 45f, 59f, 2);

            // Muro entre anillos con cuatro aberturas: tras el primer lago (θ −16°…−2°), salida del
            // pasillo que aplasta (132°…146°), vuelta tras el lago de dos plataformas (201°…213°) y
            // entrada al lago de muchas plataformas (262°…276°).
            WallArc(walls, "Muro_Intermedio_Norte", Ring2, -2f, 132f, 16);
            WallArc(walls, "Muro_Intermedio_Oeste", Ring2, 146f, 201f, 7);
            WallArc(walls, "Muro_Intermedio_SurOeste", Ring2, 213f, 262f, 6);
            WallArc(walls, "Muro_Intermedio_SurEste", Ring2, 276f, 344f, 9);

            // Muro de la sala de la IA con dos entradas (ruta derecha θ 186°–202°, ruta adelante θ 219°–233°).
            WallArc(walls, "Muro_SalaIA", ArenaRadius, 233f, 546f, 28);
            WallArc(walls, "Muro_SalaIA_Separador", ArenaRadius, 202f, 219f, 2);

            // Muros radiales: callejones sin salida y la vuelta en U.
            WallRadial(walls, "Muro_Tope_CallejonDerecho", 128f, Ring2, Ring3, 2);
            WallRadial(walls, "Muro_Tope_CallejonPlataformaMovil", 175f, ArenaRadius, Ring2, 2);
            WallRadial(walls, "Muro_Divisor_OesteSur", 226f, Ring2, Ring3, 2);
            WallRadial(walls, "Muro_Divisor_VueltaU", 214f, ArenaRadius, Ring2, 2);
            WallRadial(walls, "Muro_Tope_Estrella5", 340f, ArenaRadius, Ring2, 2);
        }

        private static void WallArc(Transform parent, string name, float radius, float a0, float a1, int segments)
        {
            Transform group = LevelKit.Child(parent, name);
            float step = (a1 - a0) / segments;
            for (int i = 0; i < segments; i++)
            {
                float mid = a0 + (i + 0.5f) * step;
                float length = 2f * radius * Mathf.Sin(Mathf.Abs(step) * 0.5f * Mathf.Deg2Rad) + 0.35f;
                LevelKit.Box($"{name}_{i + 1:00}", group, Polar(radius, mid, WallHeight * 0.5f), new Vector3(length, WallHeight, WallThickness), Inward(mid), LevelKit.Kit);
            }
        }

        private static void WallRadial(Transform parent, string name, float angle, float rIn, float rOut, int segments)
        {
            Transform group = LevelKit.Child(parent, name);
            float segment = (rOut - rIn) / segments;
            for (int i = 0; i < segments; i++)
            {
                float r = rIn + (i + 0.5f) * segment;
                LevelKit.Box($"{name}_{i + 1:00}", group, Polar(r, angle, WallHeight * 0.5f), new Vector3(segment + 0.25f, WallHeight, WallThickness), Tangent(angle), LevelKit.Kit);
            }
        }

        // ================================================================== llegada y primeras rutas
        private static void BuildEntryAndFirstBranches()
        {
            Transform zone = LevelKit.Group("Zona1_Llegada");
            LevelKit.CheckpointAt("01_PuertaInicio", zone, Polar(53.5f, 52f, 0f), 52f + 180f);

            // Pasillo de llegada: objetos y dos arañas.
            Crates(zone, "Objeto_Llegada", new[] { Polar(48.5f, 50f, 0.6f), Polar(48.5f, 54f, 0.6f), Polar(45.5f, 48f, 0.6f), Polar(45.5f, 56f, 0.6f) });
            LevelKit.AmmoPad("Surtidor_Llegada", zone, Polar(50.5f, 52f, 0f));
            LevelKit.Enemy(LevelKit.HexbugPrefab, "Arana_Llegada_01", zone, Polar(44.5f, 44f, 0.1f), Tangent(44f));
            LevelKit.Enemy(LevelKit.HexbugPrefab, "Arana_Llegada_02", zone, Polar(44.5f, 60f, 0.1f), Tangent(240f));

            // Derecha: callejón sin salida con objetos y un WinRar que protege la estrella 1.
            Crates(zone, "Objeto_CallejonDerecho", new[] { Polar(43.5f, 78f, 0.6f), Polar(46.5f, 84f, 0.6f), Polar(44f, 92f, 0.6f), Polar(46f, 98f, 0.6f) });
            LevelKit.AmmoPad("Surtidor_CallejonDerecho", zone, Polar(45f, 88f, 0f));
            GameObject winRar = LevelKit.Enemy(LevelKit.WinRarPrefab, "WinRar_CallejonDerecho", zone, Polar(45f, 110f, 0.1f), Tangent(290f));
            LevelKit.TuneWinRar(winRar, 18f);
            GameObject star1 = LevelKit.Star("Star_01_CallejonWinRar", zone, Polar(45f, 120f, 1.35f), 0);
            LevelKit.Unlock("Desbloqueo_Estrella1", zone, new[] { winRar }, null, Vector3.zero, 0.5f, new[] { star1 },
                "» ¡WinRar derrotado! Recoge la estrella.", true);

            // Izquierda: lago de lava con una plataforma encima; se llega con una esfera de enganche.
            LevelKit.Box("TEST_Plataforma_LagoSimple", zone, Polar(45f, 12f, 0.05f), new Vector3(5.8f, 0.6f, 7.2f), Tangent(12f), LevelKit.Trim, Layers.Grapple);
            LevelKit.GrappleSphere("TEST_EsferaEnganche_LagoSimple", zone, Polar(45f, 12f, 5.2f), 1.65f);
            LevelKit.GrappleSphere("TEST_EsferaEnganche_LagoSimple_Salida", zone, Polar(45f, 0f, 5.2f), 1.5f);

            LevelKit.CheckpointAt("02_Bifurcacion", zone, Polar(45f, -9f, 0f), -99f);
        }

        // ================================================================== camino de la derecha
        private static void BuildRightRoute()
        {
            Transform zone = LevelKit.Group("Zona2_RutaDerecha");

            // Pasillo con paredes que se mueven de lado a lado y de arriba a abajo (como el Nivel 3 anterior).
            float[] angles = { 14f, 30f, 46f, 64f, 82f, 100f };
            for (int i = 0; i < angles.Length; i++)
            {
                float a = angles[i];
                if (i % 2 == 1)
                {
                    LevelKit.CrusherBlock($"TEST_ParedAplasta_Vertical_{i + 1:00}", zone, Polar(InnerMid, a, 5.2f), Tangent(a),
                        new Vector3(12.2f, 3.6f, 2.2f), 3.35f, 2.7f, i * 0.22f);
                }
                else
                {
                    LevelKit.SweeperBlock($"TEST_ParedAplasta_Lateral_{i + 1:00}", zone, Polar(ArenaRadius + 3.2f, a, 2.2f), Inward(a),
                        new Vector3(2.4f, 4.2f, 5.6f), new Vector3(0f, 0f, -7.2f), 2.5f, i * 0.22f);
                }
            }

            // Final del pasillo: dos spam mail y objetos. Se puede seguir adelante o ir a la derecha.
            LevelKit.CheckpointAt("03_FinPasilloAplastadores", zone, Polar(InnerMid, 114f, 0f), 204f);
            Crates(zone, "Objeto_FinPasillo", new[] { Polar(28.5f, 118f, 0.6f), Polar(33.5f, 121f, 0.6f), Polar(29f, 127f, 0.6f), Polar(33f, 131f, 0.6f) });
            LevelKit.AmmoPad("Surtidor_FinPasillo", zone, Polar(InnerMid, 124f, 0f));
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_FinPasillo_01", zone, Polar(29.5f, 128f, 2.8f), Tangent(308f));
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_FinPasillo_02", zone, Polar(32.5f, 134f, 3.2f), Tangent(314f));

            // Adelante: callejón sin salida con una plataforma que se mueve y la estrella 2 encima.
            Vector3 a0 = Polar(28.5f, 152f, 2.2f);
            Vector3 a1 = Polar(33.5f, 168f, 2.2f);
            GameObject moving = LevelKit.MovingPlatform("TEST_PlataformaMovil_Estrella2", zone, a0, new Vector3(4.2f, 0.55f, 4.2f), Tangent(160f),
                a1 - a0, 2.6f, 0f, Layers.Grapple);
            LevelKit.Star("Star_02_PlataformaMovil", moving.transform, a0 + Vector3.up * 1.35f, 1);
            LevelKit.GrappleSphere("TEST_EsferaEnganche_Estrella2", zone, Polar(InnerMid, 158f, 5.1f), 1.5f);

            // Derecha: pasillo de lava con dos plataformas que van a la mitad y regresan; aspas que
            // obligan a saltar.
            LevelKit.CheckpointAt("04_OrillaLavaDosPlataformas", zone, Polar(OuterMid, 144f, 0f), 234f);
            Vector3 meet = Polar(OuterMid, 181f, 0.05f);
            Vector3 startA = Polar(OuterMid, 152f, 0.05f);
            Vector3 startB = Polar(OuterMid, 210f, 0.05f);
            Vector3 half = (startB - startA).normalized * 2.8f;
            Vector3 size = new Vector3(5.2f, 0.6f, 5.4f);
            LevelKit.MovingPlatform("TEST_PlataformaMovil_Lava_01", zone, startA, size, Tangent(166f), (meet - half) - startA, 4.2f, 0f, Layers.Grapple);
            LevelKit.MovingPlatform("TEST_PlataformaMovil_Lava_02", zone, startB, size, Tangent(196f), (meet + half) - startB, 4.2f, 0f, Layers.Grapple);
            float[] blades = { 164f, 181f, 198f };
            for (int i = 0; i < blades.Length; i++)
                LevelKit.Spinner($"TEST_Aspas_Lava_{i + 1:00}", zone, Polar(OuterMid, blades[i], 0.35f), 5.6f, i % 2 == 0 ? 72f : -72f, false, 0.9f, false);

            // Al otro lado: estrella 3 frente a la pared; dos vueltas a la izquierda (vuelta en U)
            // llevan al spawn y dos arañas antes de la IA.
            LevelKit.Star("Star_03_OrillaLava", zone, Polar(OuterMid, 220f, 1.35f), 2);
            LevelKit.CheckpointAt("05_VueltaEnU", zone, Polar(OuterMid, 216f, 0f), 36f);
            Crates(zone, "Objeto_VueltaU", new[] { Polar(34f, 204f, 0.6f), Polar(28f, 200f, 0.6f), Polar(33f, 192f, 0.6f), Polar(28f, 186f, 0.6f) });
            LevelKit.AmmoPad("Surtidor_VueltaU", zone, Polar(31f, 198f, 0f));
            GameObject spawner = LevelKit.Enemy(LevelKit.HorsePrefab, "Spawn_VueltaU", zone, Polar(29.5f, 190f, 0.1f), Tangent(190f));
            GameObject s1 = LevelKit.Enemy(LevelKit.HexbugPrefab, "Arana_VueltaU_01", zone, Polar(32.5f, 194f, 0.1f), Tangent(194f));
            GameObject s2 = LevelKit.Enemy(LevelKit.HexbugPrefab, "Arana_VueltaU_02", zone, Polar(28.5f, 184f, 0.1f), Tangent(184f));
            GameObject door = LevelKit.Box("TEST_Compuerta_IA_RutaDerecha", zone, Polar(ArenaRadius, 194f, WallHeight * 0.5f),
                new Vector3(7.5f, WallHeight, WallThickness), Inward(194f), LevelKit.Hazard, Layers.Default, false);
            LevelKit.Unlock("Desbloqueo_IA_RutaDerecha", zone, new[] { spawner, s1, s2 }, door.transform,
                new Vector3(0f, -WallHeight - 0.5f, 0f), 1.2f, null, "» Se abrió el acceso a la IA.", false);
        }

        // ================================================================== camino de adelante
        private static void BuildForwardRoute()
        {
            Transform zone = LevelKit.Group("Zona3_RutaAdelante");

            // Spawn, varias arañas y varios spam mail; hay objetos alrededor para pelear.
            float[] objectAngles = { -16f, -22f, -28f, -34f, -42f, -50f, -56f };
            for (int i = 0; i < objectAngles.Length; i++)
                LevelKit.Crate($"Objeto_Horda_{i + 1:00}", zone, Polar(i % 2 == 0 ? 41.5f : 48.5f, objectAngles[i], 0.6f));
            LevelKit.AmmoPad("Surtidor_Horda_01", zone, Polar(42f, -25f, 0f));
            LevelKit.AmmoPad("Surtidor_Horda_02", zone, Polar(48f, -46f, 0f));
            LevelKit.Enemy(LevelKit.HorsePrefab, "Spawn_Horda", zone, Polar(OuterMid, -38f, 0.1f), Tangent(-38f));
            float[] spiderAngles = { -26f, -32f, -44f, -52f };
            for (int i = 0; i < spiderAngles.Length; i++)
                LevelKit.Enemy(LevelKit.HexbugPrefab, $"Arana_Horda_{i + 1:00}", zone, Polar(i % 2 == 0 ? 42f : 48f, spiderAngles[i], 0.1f), Tangent(spiderAngles[i]));
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_Horda_01", zone, Polar(43f, -30f, 3f), Tangent(-30f));
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_Horda_02", zone, Polar(47f, -40f, 3.4f), Tangent(-40f));
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_Horda_03", zone, Polar(44f, -50f, 2.9f), Tangent(-50f));

            // WinRar después de la horda.
            LevelKit.CheckpointAt("06_DespuesHorda", zone, Polar(OuterMid, -62f, 0f), -152f);
            Crates(zone, "Objeto_RutaWinRar", new[] { Polar(42f, -66f, 0.6f), Polar(48f, -68f, 0.6f) });
            LevelKit.AmmoPad("Surtidor_RutaWinRar", zone, Polar(OuterMid, -65f, 0f));
            LevelKit.TuneWinRar(LevelKit.Enemy(LevelKit.WinRarPrefab, "WinRar_Paso", zone, Polar(OuterMid, -75f, 0.1f), Tangent(-75f)), 18f);

            // Bifurcación. Adelante: otro WinRar que protege la estrella 4 (sin salida).
            Crates(zone, "Objeto_CallejonSur", new[] { Polar(42.5f, -100f, 0.6f), Polar(47.5f, -104f, 0.6f), Polar(45f, -108f, 0.6f) });
            LevelKit.AmmoPad("Surtidor_CallejonSur", zone, Polar(OuterMid, -102f, 0f));
            GameObject winRarStar = LevelKit.Enemy(LevelKit.WinRarPrefab, "WinRar_Estrella4", zone, Polar(OuterMid, -115f, 0.1f), Tangent(-115f));
            LevelKit.TuneWinRar(winRarStar, 18f);
            GameObject star4 = LevelKit.Star("Star_04_CallejonSur", zone, Polar(OuterMid, -125f, 1.35f), 3);
            LevelKit.Unlock("Desbloqueo_Estrella4", zone, new[] { winRarStar }, null, Vector3.zero, 0.5f, new[] { star4 },
                "» ¡WinRar derrotado! Recoge la estrella.", true);

            // Derecha: lago de lava con muchas plataformas suspendidas, enemigos encima y spam mail
            // volando sobre la lava (como el Tutorial).
            LevelKit.CheckpointAt("07_EntradaLagoPlataformas", zone, Polar(36.5f, 269f, 0f), 89f, new Vector3(6f, 4f, 6f));
            float[] platAngles = { 269f, 284f, 300f, 316f, 331f, 254f, 240f };
            float[] platRadii = { 31f, 29.5f, 32.5f, 29.5f, 31f, 32f, 30f };
            for (int i = 0; i < platAngles.Length; i++)
            {
                float a = platAngles[i];
                Vector3 p = Polar(platRadii[i], a, 0.05f);
                GameObject plat = LevelKit.Box($"TEST_PlataformaLava_{i + 1:00}", zone, p, new Vector3(5.4f, 0.65f, 6.2f), Tangent(a), LevelKit.Trim, Layers.Grapple);
                LevelKit.GrappleSphere($"TEST_EsferaEnganche_LagoPlataformas_{i + 1:00}", zone, Polar(InnerMid, a, 4.8f), 1.45f);
                if (i == 0 || i == 2 || i == 5)
                {
                    LevelKit.Crate($"Objeto_PlataformaLava_{i + 1:00}_A", zone, p + new Vector3(0.9f, 0.95f, 0f));
                    LevelKit.Crate($"Objeto_PlataformaLava_{i + 1:00}_B", zone, p + new Vector3(-0.9f, 0.95f, 0f));
                }
                if (i == 1 || i == 3 || i == 5)
                {
                    GameObject spider = LevelKit.Enemy(LevelKit.HexbugPrefab, $"Arana_PlataformaLava_{i + 1:00}", zone, p + Vector3.up * 0.5f, Tangent(a));
                    LevelKit.Leash(spider, plat.transform, new Vector2(2.2f, 2.6f));
                }
            }
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_Lava_01", zone, Polar(InnerMid, 292f, 3.2f), Tangent(292f));
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_Lava_02", zone, Polar(InnerMid, 322f, 3.4f), Tangent(322f));
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_Lava_03", zone, Polar(InnerMid, 248f, 3.2f), Tangent(248f));

            // Sobre el lago a la derecha: callejón con la estrella 5. A la izquierda: suelo firme,
            // pared y vuelta a la derecha hacia la IA.
            LevelKit.Star("Star_05_CallejonLava", zone, Polar(31f, 333f, 1.75f), 4);
            LevelKit.CheckpointAt("08_SueloFirme_EntradaIA", zone, Polar(28.5f, 226f, 0f), 46f);
        }

        // ================================================================== centro: la IA
        private static void BuildAIArena()
        {
            Transform arena = LevelKit.Group("Zona4_Centro_IA");

            // Núcleo colgante estilo Portal: chasis gris, torso, ojo y columna al techo.
            Transform core = LevelKit.Child(arena, "TEST_NucleoCentral_IA");
            core.position = new Vector3(0f, 5.8f, 0f);
            LevelKit.Box("ColumnaSoporte_Techo", core, new Vector3(0f, 9.5f, 0f), new Vector3(2.4f, 4.2f, 2.4f), Quaternion.identity, LevelKit.Trim, Layers.Default, false);
            LevelKit.Box("ChasisSuperior_IA", core, new Vector3(0f, 6.8f, 0f), new Vector3(3.6f, 2.2f, 3.6f), Quaternion.Euler(0f, 25f, 0f), LevelKit.Wall, Layers.Default, false);
            LevelKit.Box("TorsoCurvo_IA", core, new Vector3(0f, 4.9f, 0f), new Vector3(2.6f, 2.4f, 2.8f), Quaternion.Euler(12f, 0f, 0f), LevelKit.Wall, Layers.Default, false);
            GameObject eye = LevelKit.Mesh("TEST_OjoOptico_IA", core, PrimitiveType.Sphere, LevelKit.Grapple, new Vector3(0f, 4.6f, 1.45f), new Vector3(1.5f, 1.5f, 0.9f), Quaternion.identity);

            float[] monitorAngles = { 30f, 120f, 300f };
            var monitors = new AIMonitorTarget[3];
            for (int i = 0; i < 3; i++)
            {
                float a = monitorAngles[i];
                LevelKit.Box($"TEST_Cable_IA_{i + 1:00}", arena, Polar(11.5f, a, 7.6f), new Vector3(0.45f, 0.45f, 21f), Inward(a), LevelKit.Trim, Layers.Default, false);
                monitors[i] = Monitor(arena, i, a);
            }

            // Objetos tirados en el suelo y surtidores para las tres oleadas.
            for (int i = 0; i < 12; i++)
                LevelKit.Crate($"Objeto_SalaIA_{i + 1:00}", arena, Polar(i % 2 == 0 ? 10.5f : 16.5f, i * 30f, 0.6f));
            foreach (float a in new[] { 0f, 90f, 180f, 270f })
                LevelKit.AmmoPad($"Surtidor_SalaIA_{a:0}", arena, Polar(13.5f, a, 0f));

            // Tres oleadas (AIBossController las enciende en orden).
            Transform w1 = LevelKit.Child(arena, "Oleada_01_IA");
            Wave(w1, "Oleada1", new[] { (LevelKit.HexbugPrefab, 14f, 20f, 0.1f), (LevelKit.HexbugPrefab, 14f, 140f, 0.1f), (LevelKit.HexbugPrefab, 14f, 280f, 0.1f),
                                         (LevelKit.FlyingEmailPrefab, 15f, 75f, 3f), (LevelKit.FlyingEmailPrefab, 15f, 330f, 3f) });
            Transform w2 = LevelKit.Child(arena, "Oleada_02_IA");
            Wave(w2, "Oleada2", new[] { (LevelKit.HorsePrefab, 15f, 60f, 0.1f), (LevelKit.HexbugPrefab, 13f, 110f, 0.1f), (LevelKit.HexbugPrefab, 13f, 250f, 0.1f),
                                         (LevelKit.HexbugPrefab, 13f, 340f, 0.1f), (LevelKit.FlyingEmailPrefab, 16f, 15f, 3.2f), (LevelKit.FlyingEmailPrefab, 16f, 165f, 3.2f) });
            Transform w3 = LevelKit.Child(arena, "Oleada_03_IA");
            Wave(w3, "Oleada3", new[] { (LevelKit.WinRarPrefab, 15f, 45f, 0.1f), (LevelKit.HorsePrefab, 15f, 135f, 0.1f), (LevelKit.HexbugPrefab, 13f, 90f, 0.1f),
                                         (LevelKit.HexbugPrefab, 13f, 310f, 0.1f), (LevelKit.FlyingEmailPrefab, 16f, 0f, 3.2f), (LevelKit.FlyingEmailPrefab, 16f, 270f, 3.2f) });

            var beacon = new GameObject("Baliza_Victoria_IA");
            beacon.transform.SetParent(arena, false);
            beacon.transform.position = new Vector3(0f, 3.2f, 0f);
            Light light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 32f;
            light.intensity = 3.5f;
            light.color = new Color(0.22f, 0.9f, 0.3f);
            light.shadows = LightShadows.None;
            beacon.SetActive(false);

            var controllerGo = new GameObject("AIBossController");
            controllerGo.transform.SetParent(arena, false);
            AIBossController boss = controllerGo.AddComponent<AIBossController>();
            boss.Configure(21f, w1, w2, w3, monitors, core, eye.GetComponent<Renderer>(), new[] { beacon });

            VictoryPortal(arena, boss, core.position);
            LevelKit.AudioSlot(controllerGo, "Colapso del núcleo de la IA y victoria", AudioSlotTrigger.LevelGateOrCompletion, 0f, 120f, 0.95f);
        }

        /// <summary>Sección de monitor en la pared: se abre tras cada oleada y se rompe con un objeto.</summary>
        private static AIMonitorTarget Monitor(Transform parent, int index, float angle)
        {
            Vector3 pos = Polar(ArenaRadius - 1.35f, angle, 2.75f);
            Quaternion rot = Inward(angle);
            var root = new GameObject($"TEST_MonitorPared_IA_0{index + 1}");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            root.AddComponent<BoxCollider>().size = new Vector3(4.6f, 3.4f, 1.2f);

            LevelKit.Box("Marco", root.transform, pos - rot * Vector3.forward * 0.25f, new Vector3(5f, 3.8f, 0.7f), rot, LevelKit.Wall, Layers.Default, false);
            GameObject screen = LevelKit.Box("Pantalla", root.transform, pos + rot * Vector3.forward * 0.12f, new Vector3(4.2f, 3f, 0.25f), rot, LevelKit.Bubble, Layers.Default, false);
            GameObject shutter = LevelKit.Box("Compuerta", root.transform, pos + rot * Vector3.forward * 0.32f, new Vector3(4.5f, 3.2f, 0.22f), rot, LevelKit.Hazard, Layers.Default, false);
            GameObject beacon = LevelKit.Mesh("Baliza", root.transform, PrimitiveType.Sphere, LevelKit.Beacon, pos + Vector3.up * 2.5f + rot * Vector3.forward * 0.4f, Vector3.one * 0.95f, Quaternion.identity);
            beacon.SetActive(false);

            AIMonitorTarget target = root.AddComponent<AIMonitorTarget>();
            target.Configure(index, screen.GetComponent<Renderer>(), shutter.transform, new Vector3(0f, 3.5f, 0f), beacon);
            LevelKit.AudioSlot(root, $"Activación e impacto del monitor {index + 1}", AudioSlotTrigger.ArenaGateOpen, 1f, 45f, 0.95f);
            return target;
        }

        private static void Wave(Transform parent, string prefix, (string prefab, float r, float a, float y)[] enemies)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                GameObject enemy = LevelKit.Enemy(e.prefab, $"{prefix}_{i + 1:00}", parent, Polar(e.r, e.a, e.y), Inward(e.a));
                if (e.prefab == LevelKit.WinRarPrefab) LevelKit.TuneWinRar(enemy, 22f);
            }
        }

        /// <summary>
        /// Portal que deja la IA al ser destruida: puerta acostada mirando hacia abajo donde estaba
        /// el núcleo y una esfera de gancho 2,5 m arriba; el gancho jala al jugador a través de la
        /// abertura. Lleva a la estación final. Los dos se guardan apagados.
        /// </summary>
        private static void VictoryPortal(Transform arena, AIBossController boss, Vector3 center)
        {
            Quaternion facingDown = Quaternion.Euler(90f, 0f, 0f);
            Vector3 pivot = center - facingDown * Vector3.up * (LevelGateBuilder.Aperture.y * 0.5f);
            LevelGate gate = LevelKit.Gate("LevelGate_EstacionFinal", arena, pivot, facingDown, SceneNames.EstacionFinal, SceneNames.Paths.EstacionFinal);

            var gateSettings = new SerializedObject(gate);
            gateSettings.FindProperty("preloadDelay").floatValue = 0.2f;
            gateSettings.ApplyModifiedPropertiesWithoutUndo();
            var visuals = gate.GetComponent<LevelGateVisuals>();
            if (visuals != null)
            {
                var look = new SerializedObject(visuals);
                look.FindProperty("colorTop").colorValue = new Color(0.78f, 0.45f, 1f, 1f);
                look.FindProperty("colorBottom").colorValue = new Color(0.45f, 0.3f, 1f, 1f);
                look.FindProperty("idleGlow").floatValue = 0.9f;
                look.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject anchor = LevelKit.GrappleSphere("EsferaEnganche_PortalFinal", arena, center + Vector3.up * 2.5f, 1.65f);
            boss.ConfigureVictoryPortal(gate.gameObject, anchor);
            gate.gameObject.SetActive(false);
            anchor.SetActive(false);
        }

        // ================================================================== utilidades
        private static void Crates(Transform parent, string prefix, Vector3[] positions)
        {
            for (int i = 0; i < positions.Length; i++) LevelKit.Crate($"{prefix}_{i + 1:00}", parent, positions[i]);
        }

        private static Vector3 Polar(float radius, float angleDeg, float y)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, y, Mathf.Sin(rad) * radius);
        }

        /// <summary>Frente (+Z) hacia el centro del laberinto.</summary>
        private static Quaternion Inward(float angleDeg) => Quaternion.LookRotation(-Polar(1f, angleDeg, 0f), Vector3.up);

        /// <summary>Frente (+Z) a lo largo de la tangente antihoraria.</summary>
        private static Quaternion Tangent(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return Quaternion.LookRotation(new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)), Vector3.up);
        }
    }
}
