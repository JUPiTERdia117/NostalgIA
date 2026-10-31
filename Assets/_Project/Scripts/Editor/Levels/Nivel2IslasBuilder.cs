// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: Nivel2IslasBuilder.cs
// Propósito: construye el Nivel 2 (océano infinito, tres islas, plataformas,
// lote de edificios y puerta de meta acostada) con el kit del Tutorial.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Gameplay.Level;
using NostalgIA.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace NostalgIA.EditorTools.Levels
{
    /// <summary>
    /// Nivel 2 según el boceto "NVL 2". Ejes locales: +Z es hacia delante (de la puerta de inicio
    /// hacia la meta) y −X es la izquierda del jugador, que en el boceto visto desde arriba es la
    /// parte de arriba de la hoja.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Medidas de salto.</b> Con <c>PlayerSettings_Default</c> (salto 5 m/s, gravedad 9,81,
    /// correr 8 m/s) el salto sube 1,27 m y dura 1,02 s. Por eso las plataformas que se pasan
    /// saltando están a 3 m o menos de borde a borde y suben 0,9 m como máximo; lo que está más
    /// lejos o más alto es turquesa (capa Grapple) o tiene una esfera de enganche.
    /// </para>
    /// <para>
    /// <b>Alturas.</b> Isla de inicio 1 m → plataformas 1,5 m → islas 2,4 m → plataformas
    /// flotantes 3,3 m → plataformas móviles 4,2 m → primeros techos 5 m → edificio central 8,5 m →
    /// techos finales 9–10 m → plataforma final 12,5 m. La puerta de meta está 4 m más abajo que
    /// el final de esa plataforma, mirando hacia arriba. Una caída de 13 m llega a 16 m/s, por
    /// debajo de la velocidad letal (24 m/s): caer al agua no hace daño.
    /// </para>
    /// </remarks>
    public static class Nivel2IslasBuilder
    {
        /// <summary>Región del Nivel 2 (lejos del Tutorial, que está en el origen).</summary>
        public static readonly Vector3 RegionOrigin = new Vector3(3000f, 0f, 0f);

        private static readonly Quaternion FaceForward = Quaternion.identity;
        // Puerta acostada mirando hacia arriba: frente = +Y, "arriba" de la puerta = +Z.
        private static readonly Quaternion GateFacingUp = new Quaternion(0f, 0.7071068f, 0.7071068f, 0f);

        [MenuItem("NostalgIA/Niveles/Levels_test/Construir Nivel 2 (Islas)", priority = 351)]
        public static void Build()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        /// <summary>Construye y guarda la escena (sin preguntar por cambios pendientes).</summary>
        public static void BuildScene()
        {
            LevelKit.BeginScene("Nivel2_Islas");
            LevelKit.TutorialAtmosphere(true, new Color(0.07f, 0.11f, 0.3f), 90f, 420f);

            BuildOcean();
            BuildStartIsland();
            BuildSteppingPlatformsAndIslands();
            BuildFloatingAndMovingPlatforms();
            BuildBuildings();
            BuildFinalPlatformAndGoal();
            BuildWaterThreatsAndBubbles();

            LevelKit.Entrance(new Vector3(-5f, 1f, -4.5f), FaceForward);
            LevelKit.StandalonePlayer(new Vector3(-5f, 1.1f, -2.5f), FaceForward);

            var ambient = new GameObject("Audio_AmbienteOceano");
            ambient.transform.SetParent(LevelKit.Root, false);
            LevelKit.AudioSlot(ambient, "Ambiente 2D del océano (olas suaves y viento)", AudioSlotTrigger.AmbientOrManual, 0f, 200f, 0.6f);

            LevelKit.EndScene(SceneNames.Paths.Nivel2, RegionOrigin,
                new Bounds(new Vector3(0f, 7f, 62f), new Vector3(90f, 22f, 170f)));
            Debug.Log("[Niveles] Nivel 2 (Islas) construido en " + SceneNames.Paths.Nivel2);
        }

        // ================================================================== océano
        /// <summary>
        /// Océano "infinito": un piso de 3 km con el material azul <c>Objects</c>; el agua es poco
        /// profunda, así que se camina sobre él. La niebla del Tutorial oculta el borde. Encima va
        /// un amortiguador de caída por si el jugador cae desde lo más alto.
        /// </summary>
        private static void BuildOcean()
        {
            Transform group = LevelKit.Group("TEST_Oceano_PocoProfundo");
            LevelKit.Box("TEST_Agua_PisoCaminable", group, new Vector3(0f, -0.5f, 60f), new Vector3(3000f, 1f, 3000f),
                Quaternion.identity, LevelKit.Water);

            var cushion = new GameObject("AmortiguadorCaida_Agua");
            cushion.transform.SetParent(group, false);
            cushion.transform.position = new Vector3(0f, 0.6f, 60f);
            cushion.layer = Layers.Trigger;
            var cushionCollider = cushion.AddComponent<BoxCollider>();
            cushionCollider.size = new Vector3(400f, 1.6f, 400f);
            cushionCollider.isTrigger = true;
            cushion.AddComponent<WaterFallCushion>();
        }

        // ================================================================== isla de inicio
        private static void BuildStartIsland()
        {
            Transform zone = LevelKit.Group("Zona1_IslaInicio");
            Island(zone, "TEST_Isla_Inicio", new Vector3(0f, 0f, 0f), 10f, 9f, 1.0f);

            // Objetos para recolectar (los tres puntos verdes del boceto).
            LevelKit.Crate("Objeto_IslaInicio_01", zone, new Vector3(4.5f, 1.6f, 1.0f));
            LevelKit.Crate("Objeto_IslaInicio_02", zone, new Vector3(6.0f, 1.6f, 2.4f));
            LevelKit.Crate("Objeto_IslaInicio_03", zone, new Vector3(4.2f, 1.6f, 3.4f));
            LevelKit.AmmoPad("Surtidor_IslaInicio", zone, new Vector3(2.5f, 1f, -2.5f));

            LevelKit.CheckpointAt("01_IslaInicio", zone, new Vector3(-3f, 1f, -1f), 0f);
        }

        // ================================================================== plataformas de salto e islas
        private static void BuildSteppingPlatformsAndIslands()
        {
            Transform zone = LevelKit.Group("Zona2_Islas");

            // Plataformas pequeñas de enganche (círculos azules del boceto).
            Stepping(zone, "TEST_Plataforma_Centro", new Vector3(1f, 0f, 14f), 2.4f, 1.5f);
            Stepping(zone, "TEST_Plataforma_DerechaCerca", new Vector3(12.5f, 0f, 9.5f), 2.2f, 1.6f);
            Stepping(zone, "TEST_Plataforma_DerechaAtras", new Vector3(12.5f, 0f, -3f), 2.0f, 1.3f);

            // --- Isla de la izquierda: dos enemigos (spam mail volador y bicho terrestre). ---
            GameObject left = Island(zone, "TEST_Isla_Izquierda_Enemigos", new Vector3(-7f, 0f, 23f), 7.5f, 7f, 2.4f);
            LevelKit.Enemy(LevelKit.FlyingEmailPrefab, "SpamMail_IslaIzquierda", zone, new Vector3(-6f, 5.2f, 25f), Quaternion.Euler(0f, 180f, 0f));
            GameObject bug = LevelKit.Enemy(LevelKit.HexbugPrefab, "Bicho_IslaIzquierda", zone, new Vector3(-8f, 2.55f, 26f), Quaternion.Euler(0f, 180f, 0f));
            LevelKit.Leash(bug, left.transform, new Vector2(5.5f, 5f));
            LevelKit.AmmoPad("Surtidor_IslaIzquierda", zone, new Vector3(-10f, 2.4f, 20.5f));
            LevelKit.CheckpointAt("02_IslaIzquierda", zone, new Vector3(-5f, 2.4f, 19f), 0f);

            // --- Isla de la derecha: aspas giratorias que hacen daño al tocarlas. ---
            Island(zone, "TEST_Isla_Derecha_Aspas", new Vector3(10.5f, 0f, 22.5f), 8f, 7.5f, 2.4f);
            LevelKit.Spinner("TEST_Aspas_IslaDerecha", zone, new Vector3(10.5f, 2.4f, 23.5f), 5.2f, 70f, true);
            LevelKit.CheckpointAt("03_IslaDerecha", zone, new Vector3(13f, 2.4f, 16.5f), 0f, new Vector3(5f, 4f, 3f));
        }

        // ================================================================== plataformas flotantes y móviles
        private static void BuildFloatingAndMovingPlatforms()
        {
            Transform zone = LevelKit.Group("Zona3_Plataformas");

            // Dos plataformas flotantes después de las islas.
            Stepping(zone, "TEST_PlataformaFlotante_Izq", new Vector3(-3.5f, 0f, 34f), 2.3f, 3.3f);
            Stepping(zone, "TEST_PlataformaFlotante_Der", new Vector3(7.5f, 0f, 35f), 2.3f, 3.3f);
            LevelKit.Crate("Objeto_PlataformaFlotante_Der", zone, new Vector3(7.5f, 3.9f, 35f));

            // Dos plataformas que se desplazan entre las flotantes y el primer techo: hay que
            // subirse con precisión. Van desfasadas medio ciclo para que no lleguen juntas.
            Vector3 size = new Vector3(4f, 0.6f, 4f);
            LevelKit.MovingPlatform("TEST_PlataformaMovil_Izq", zone, new Vector3(-5f, 3.9f, 40f), size, FaceForward,
                new Vector3(-1f, 0f, 7.5f), 2.8f, 0f);
            LevelKit.MovingPlatform("TEST_PlataformaMovil_Der", zone, new Vector3(9f, 3.9f, 41.5f), size, FaceForward,
                new Vector3(1f, 0f, 7f), 2.8f, 0.5f);

            // En su defecto, esferas de enganche sobre el borde de los primeros techos.
            LevelKit.GrappleSphere("TEST_EsferaEnganche_TechosIzq", zone, new Vector3(-7f, 9.5f, 51f));
            LevelKit.GrappleSphere("TEST_EsferaEnganche_TechosDer", zone, new Vector3(11f, 9.5f, 52.5f));
        }

        // ================================================================== lote de edificios
        private static void BuildBuildings()
        {
            Transform zone = LevelKit.Group("Zona4_Edificios");

            // Primera fila (techos a 5 m) donde aterrizan las plataformas móviles.
            GameObject b1 = Building(zone, "TEST_Edificio_01_IzqGirado", new Vector3(-14f, 0f, 57f), new Vector2(8f, 12f), 5.0f, true, 20f);
            GameObject b2 = Building(zone, "TEST_Edificio_02_Frente", new Vector3(0f, 0f, 54.5f), new Vector2(11f, 7f), 5.0f, true);
            Building(zone, "TEST_Edificio_04_Der", new Vector3(15.5f, 0f, 55f), new Vector2(7f, 8f), 5.0f, true);
            Building(zone, "TEST_Edificio_03_Centro", new Vector3(3f, 0f, 64f), new Vector2(10f, 7f), 5.8f, true);
            LevelKit.Crate("Objeto_TechoFrente", zone, new Vector3(0f, 5.6f, 55.5f));
            LevelKit.CheckpointAt("04_PrimerosTechos", zone, new Vector3(-2f, 5f, 54.5f), 0f, new Vector3(6f, 4f, 6f));

            // Enemigo en el techo girado de la izquierda (círculo rojo del boceto).
            GameObject roofBug = LevelKit.Enemy(LevelKit.HexbugPrefab, "Bicho_TechoIzq", zone, new Vector3(-14f, 5.15f, 58f), Quaternion.Euler(0f, 200f, 0f));
            LevelKit.Leash(roofBug, b1.transform, new Vector2(3f, 5f));

            // Edificios altos con enganche a los lados del edificio central: desde ellos se baja al central.
            Building(zone, "TEST_Edificio_06_IzqAlto", new Vector3(-19f, 0f, 80f), new Vector2(6f, 6f), 10f, true);
            LevelKit.Crate("Objeto_TechoIzqAlto", zone, new Vector3(-19f, 10.6f, 80f));
            GameObject b8 = Building(zone, "TEST_Edificio_08_DerAlto", new Vector3(18f, 0f, 88f), new Vector2(11f, 9f), 10f, true);
            Sweepers(zone, "TEST_Obstaculo_TechoDer", b8.transform.position.x, 10f, 88f, new Vector3(0f, 0f, 6.5f), 2);

            // Esfera para pasar del techo del centro (5,8 m) al edificio central (8,5 m).
            LevelKit.GrappleSphere("TEST_EsferaEnganche_HaciaCentral", zone, new Vector3(1f, 12.5f, 69.5f));

            // Edificio central más amplio, sin enganche: enemigo de spawn. Al derrotarlo aparece la
            // estrella y baja el muro que cierra el paso a la última plataforma.
            Building(zone, "TEST_Edificio_05_Central", new Vector3(-1f, 0f, 79.5f), new Vector2(20f, 17f), 8.5f, false);
            GameObject spawner = LevelKit.Enemy(LevelKit.HorsePrefab, "Spawn_EdificioCentral", zone, new Vector3(-1f, 8.6f, 83f), Quaternion.Euler(0f, 180f, 0f));
            LevelKit.AmmoPad("Surtidor_EdificioCentral", zone, new Vector3(-6f, 8.5f, 75.5f));
            LevelKit.AmmoPad("Surtidor_EdificioCentral_02", zone, new Vector3(5f, 8.5f, 75.5f));
            LevelKit.Crate("Objeto_EdificioCentral_01", zone, new Vector3(-4f, 9.1f, 74f));
            LevelKit.Crate("Objeto_EdificioCentral_02", zone, new Vector3(3f, 9.1f, 74f));
            LevelKit.CheckpointAt("05_EdificioCentral", zone, new Vector3(-1f, 8.5f, 74f), 0f, new Vector3(8f, 4f, 4f));

            // Última fila: techo con obstáculos a la izquierda y el edificio que lleva a la rampa final.
            Building(zone, "TEST_Edificio_07_IzqObstaculos", new Vector3(-14f, 0f, 95f), new Vector2(10f, 8f), 9.0f, true);
            Sweepers(zone, "TEST_Obstaculo_TechoIzq", -14f, 9f, 95f, new Vector3(7f, 0f, 0f), 2);
            Building(zone, "TEST_Edificio_09_Final", new Vector3(3f, 0f, 96f), new Vector2(8f, 8f), 9.2f, true);

            // Estrella que se consigue al derrotar al spawn (aparece donde cayó).
            GameObject star = LevelKit.Star("Star_01_SpawnEdificioCentral", zone, new Vector3(-1f, 9.8f, 83f), 0);

            // Muro que impide avanzar a la rampa de la última plataforma; baja al vencer al spawn.
            GameObject wall = LevelKit.Box("TEST_Muro_BloqueoPlataformaFinal", zone, new Vector3(3f, 9.2f + 4.5f, 99.6f),
                new Vector3(12f, 9f, 0.8f), Quaternion.identity, LevelKit.Hazard, Layers.Default, false);

            // Ayudas que solo aparecen al abrir el paso (no se pueden usar para saltarse el muro).
            GameObject rampSphere = LevelKit.GrappleSphere("TEST_EsferaEnganche_Rampa", zone, new Vector3(3f, 16f, 106f));
            GameObject returnBubble = LevelKit.TrampolineBubble("BurbujaTrampolin_RegresoPlataformaFinal", zone,
                new Vector3(-7f, 0.9f, 118f), 1.8f, 14.5f, 3.5f, new Vector3(3f, 0f, 118f));

            // Son dos controles porque el primero coloca sus objetos donde murió el enemigo (la
            // estrella), y la esfera y la burbuja deben quedarse en su lugar.
            LevelKit.Unlock("Desbloqueo_Estrella_SpawnCentral", zone, new[] { spawner }, null,
                Vector3.zero, 0.5f, new[] { star }, "» ¡Spawn derrotado! Recoge la estrella.", true);
            LevelKit.Unlock("Desbloqueo_Muro_PlataformaFinal", zone, new[] { spawner }, wall.transform,
                new Vector3(0f, -9.6f, 0f), 1.4f, new[] { rampSphere, returnBubble },
                "» Se abrió el paso hacia la última plataforma.", false);
        }

        // ================================================================== última plataforma y meta
        private static void BuildFinalPlatformAndGoal()
        {
            Transform zone = LevelKit.Group("Zona5_PlataformaFinal_Meta");

            // Rampa desde el techo final (9,2 m) hasta la plataforma (12,5 m): 18° de pendiente.
            float rise = 12.5f - 9.2f;
            float run = 10f;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(rise * rise + run * run);
            LevelKit.Box("TEST_Rampa_PlataformaFinal", zone, new Vector3(3f, 9.2f + rise * 0.5f - 0.25f, 100f + run * 0.5f),
                new Vector3(4f, 0.5f, length), Quaternion.Euler(-angle, 0f, 0f), LevelKit.Wall);

            // Plataforma larga y alta, sin enganche (blanca en el boceto).
            LevelKit.Box("TEST_PlataformaFinal_WinRar", zone, new Vector3(3f, 12.25f, 119f), new Vector3(7f, 0.5f, 18f),
                Quaternion.identity, LevelKit.Wall);
            LevelKit.AmmoPad("Surtidor_PlataformaFinal", zone, new Vector3(3f, 12.5f, 112f));
            LevelKit.Crate("Objeto_PlataformaFinal", zone, new Vector3(1.5f, 13.1f, 113.5f));
            LevelKit.CheckpointAt("06_PlataformaFinal", zone, new Vector3(3f, 12.5f, 111.5f), 0f, new Vector3(6f, 4f, 3f));

            // WinRar al final de la plataforma.
            GameObject winRar = LevelKit.Enemy(LevelKit.WinRarPrefab, "WinRar_FinalPlataforma", zone, new Vector3(3f, 12.6f, 124f), Quaternion.Euler(0f, 180f, 0f));
            LevelKit.TuneWinRar(winRar, 16f);

            // Puerta de meta 4 m debajo del final de la plataforma, mirando hacia arriba: hay que
            // saltar y caer dentro. Centro de la abertura en z = 131,6 (el pivote va en el borde
            // inferior, 2,2 m antes). Lleva al Nivel 3.
            Vector3 gateCenter = new Vector3(3f, 8.5f, 131.6f);
            Vector3 pivot = gateCenter - GateFacingUp * Vector3.up * (LevelGateBuilder.Aperture.y * 0.5f);
            LevelKit.Gate("LevelGate_Meta_Nivel3", zone, pivot, GateFacingUp, SceneNames.Nivel3, SceneNames.Paths.Nivel3);
        }

        // ================================================================== agua: enemigos y burbujas
        private static void BuildWaterThreatsAndBubbles()
        {
            Transform zone = LevelKit.Group("Zona6_Agua_EnemigosYBurbujas");

            // Un spawn y dos bichos cerca de los edificios, y un WinRar cerca de la puerta.
            LevelKit.Enemy(LevelKit.HorsePrefab, "Spawn_AguaEdificios", zone, new Vector3(-24f, 0.1f, 68f), Quaternion.Euler(0f, 90f, 0f));
            LevelKit.Enemy(LevelKit.HexbugPrefab, "Bicho_AguaEdificios_01", zone, new Vector3(24f, 0.1f, 66f), Quaternion.Euler(0f, 270f, 0f));
            LevelKit.Enemy(LevelKit.HexbugPrefab, "Bicho_AguaEdificios_02", zone, new Vector3(-24f, 0.1f, 92f), Quaternion.Euler(0f, 90f, 0f));
            GameObject winRar = LevelKit.Enemy(LevelKit.WinRarPrefab, "WinRar_AguaPuerta", zone, new Vector3(10f, 0.1f, 136f), Quaternion.Euler(0f, 220f, 0f));
            LevelKit.TuneWinRar(winRar, 14f);
            LevelKit.AmmoPad("Surtidor_AguaEdificios", zone, new Vector3(-20f, 0f, 60f));

            // Burbujas trampolín para volver a subir después de caer (altura = lo que suben sobre la burbuja).
            LevelKit.TrampolineBubble("BurbujaTrampolin_IslaInicio", zone, new Vector3(-6f, 0.9f, 11f), 1.6f, 3.5f, 2.5f, new Vector3(1f, 0f, 14f));
            LevelKit.TrampolineBubble("BurbujaTrampolin_Islas", zone, new Vector3(1.5f, 0.9f, 30f), 1.6f, 5f, 3f, new Vector3(-3.5f, 0f, 34f));
            LevelKit.TrampolineBubble("BurbujaTrampolin_PlataformasMoviles", zone, new Vector3(2f, 0.9f, 45f), 1.7f, 7f, 3.5f, new Vector3(0f, 0f, 54.5f));
            LevelKit.TrampolineBubble("BurbujaTrampolin_EdificiosIzq", zone, new Vector3(-24f, 0.9f, 76f), 1.8f, 11.5f, 3f, new Vector3(-19f, 0f, 80f));
            LevelKit.TrampolineBubble("BurbujaTrampolin_EdificiosDer", zone, new Vector3(26f, 0.9f, 80f), 1.8f, 11.5f, 3f, new Vector3(18f, 0f, 88f));
            LevelKit.TrampolineBubble("BurbujaTrampolin_TechosFinales", zone, new Vector3(-6f, 0.9f, 104f), 1.8f, 10.5f, 3f, new Vector3(-14f, 0f, 95f));
        }

        // ================================================================== piezas
        /// <summary>Isla: base de arena (Tutorial_Floor) y pasto verde encima (Tutorial_Route).</summary>
        private static GameObject Island(Transform parent, string name, Vector3 center, float radiusX, float radiusZ, float top)
        {
            Transform group = LevelKit.Child(parent, name);
            LevelKit.Cylinder(name + "_Arena", group, center, radiusX + 0.8f, radiusZ + 0.8f, -0.6f, top - 0.25f, LevelKit.Floor);
            return LevelKit.Cylinder(name + "_Pasto", group, center, radiusX, radiusZ, top - 0.6f, top, LevelKit.Grass);
        }

        /// <summary>Plataforma redonda de enganche (círculo azul del boceto) sobre una columna.</summary>
        private static void Stepping(Transform parent, string name, Vector3 center, float radius, float top)
        {
            LevelKit.Cylinder(name, parent, center, radius, radius, top - 0.5f, top, LevelKit.Trim, Layers.Grapple);
            LevelKit.Cylinder(name + "_Columna", parent, center, radius * 0.45f, radius * 0.45f, -0.5f, top - 0.5f, LevelKit.Wall);
        }

        /// <summary>
        /// Edificio: turquesa con capa Grapple si el gancho sirve en él (azul en el boceto); gris si
        /// no. El techo queda a <paramref name="roof"/> metros.
        /// </summary>
        private static GameObject Building(Transform parent, string name, Vector3 centerXZ, Vector2 footprint, float roof, bool grapple, float yaw = 0f)
        {
            Vector3 center = new Vector3(centerXZ.x, (roof - 0.5f) * 0.5f, centerXZ.z);
            GameObject body = LevelKit.Box(name, parent, center, new Vector3(footprint.x, roof + 0.5f, footprint.y),
                Quaternion.Euler(0f, yaw, 0f), grapple ? LevelKit.Trim : LevelKit.Wall, grapple ? Layers.Grapple : Layers.Default);
            // Cornisa gris para leer el borde del techo desde lejos.
            GameObject cornice = LevelKit.Mesh(name + "_Cornisa", body.transform, PrimitiveType.Cube, LevelKit.Wall,
                new Vector3(centerXZ.x, roof - 0.15f, centerXZ.z), Vector3.one, Quaternion.Euler(0f, yaw, 0f));
            cornice.transform.localScale = new Vector3(1.02f, 0.3f / (roof + 0.5f), 1.02f);   // escala relativa al edificio
            LevelKit.MarkStatic(cornice);
            return body;
        }

        /// <summary>Bloques rojos que barren un techo de lado a lado (obstáculos del boceto).</summary>
        private static void Sweepers(Transform parent, string name, float x, float roof, float z, Vector3 travel, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * 3.2f;
                Vector3 axis = travel.normalized;
                Vector3 across = new Vector3(axis.z, 0f, axis.x);
                Vector3 start = new Vector3(x, roof + 0.7f, z) - travel * 0.5f + across * offset;
                LevelKit.SweeperBlock($"{name}_{i + 1:00}", parent, start, Quaternion.identity, new Vector3(1.2f, 1.4f, 1.2f),
                    travel, 2.6f + i * 0.6f, i * 0.5f);
            }
        }
    }
}
