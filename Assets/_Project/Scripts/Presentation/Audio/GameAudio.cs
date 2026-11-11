// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: GameAudio.cs
// Propósito: volumen general de música y efectos, guardado entre partidas y
// aplicado con el mixer de audio del juego.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Audio;

namespace NostalgIA.Presentation.Audio
{
    /// <summary>
    /// Acceso al mixer del juego (<c>Resources/Audio/NostalgIA_Mixer</c>) y a los volúmenes de
    /// música y efectos que elige el jugador en el menú.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Toda fuente de audio debe salir por uno de los dos grupos del mixer: <see cref="Music"/> o
    /// <see cref="Sfx"/>. Así los deslizadores del menú controlan cualquier sonido nuevo sin tocar
    /// código: basta con poner ese grupo en el campo <c>Output</c> del <c>AudioSource</c> (o
    /// asignarlo por código, como hacen <c>GameSfx</c> y <c>VacuumAudio</c>).
    /// </para>
    /// <para>
    /// El deslizador va de 0 a 1 y 0,5 equivale al volumen con el que se mezclaron los sonidos.
    /// La ganancia es <c>2 · valor</c>: a la mitad queda igual (0 dB), al máximo suena al doble
    /// (+6 dB) y en 0 se silencia. El mixer trabaja en decibeles, por eso convierto con
    /// <c>20 · log10(ganancia)</c>.
    /// </para>
    /// </remarks>
    public static class GameAudio
    {
        private const string MixerPath = "Audio/NostalgIA_Mixer";
        private const string MusicParameter = "MusicVolume";
        private const string SfxParameter = "SfxVolume";
        private const string MusicKey = "NostalgIA.MusicVolume";
        private const string SfxKey = "NostalgIA.SfxVolume";

        /// <summary>Valor del deslizador que deja los sonidos como se mezclaron.</summary>
        public const float DefaultVolume = 0.5f;

        private static AudioMixer mixer;
        private static AudioMixerGroup musicGroup;
        private static AudioMixerGroup sfxGroup;

        /// <summary>Grupo del mixer para la música.</summary>
        public static AudioMixerGroup Music { get { Load(); return musicGroup; } }

        /// <summary>Grupo del mixer para los efectos de sonido.</summary>
        public static AudioMixerGroup Sfx { get { Load(); return sfxGroup; } }

        /// <summary>Volumen de la música elegido en el menú (0 a 1).</summary>
        public static float MusicVolume => PlayerPrefs.GetFloat(MusicKey, DefaultVolume);

        /// <summary>Volumen de los efectos elegido en el menú (0 a 1).</summary>
        public static float SfxVolume => PlayerPrefs.GetFloat(SfxKey, DefaultVolume);

        /// <summary>Guarda y aplica el volumen de la música.</summary>
        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
            Apply();
        }

        /// <summary>Guarda y aplica el volumen de los efectos.</summary>
        public static void SetSfxVolume(float value)
        {
            PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
            Apply();
        }

        /// <summary>Pasa los volúmenes guardados al mixer.</summary>
        public static void Apply()
        {
            Load();
            if (mixer == null) return;
            mixer.SetFloat(MusicParameter, ToDecibels(MusicVolume));
            mixer.SetFloat(SfxParameter, ToDecibels(SfxVolume));
        }

        /// <summary>Deslizador (0 a 1) a decibeles; 0,5 → 0 dB.</summary>
        private static float ToDecibels(float value) => value <= 0.001f ? -80f : 20f * Mathf.Log10(value * 2f);

        private static void Load()
        {
            if (mixer != null) return;
            mixer = Resources.Load<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                Debug.LogWarning($"[GameAudio] No encontré el mixer en Resources/{MixerPath}.");
                return;
            }
            musicGroup = mixer.FindMatchingGroups("Music")[0];
            sfxGroup = mixer.FindMatchingGroups("SFX")[0];
        }

        /// <summary>
        /// Al arrancar el juego creo un objeto persistente que aplica los volúmenes en su
        /// <c>Start</c>: el mixer ignora <c>SetFloat</c> si se llama antes de que termine de cargar.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateApplier()
        {
            var go = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Applier>();
        }

        /// <summary>Solo existe para llamar a <see cref="Apply"/> en <c>Start</c>.</summary>
        private sealed class Applier : MonoBehaviour
        {
            private void Start() => Apply();
        }
    }
}
