using UnityEngine;
using NostalgIA.Presentation.Audio;

namespace SlimUI.ModernMenu{
	/// <summary>
	/// NostalgIA: manda la música de esta fuente al grupo Music del mixer del juego, así la controla
	/// el deslizador de música del menú. Sirve para cualquier música nueva: basta con agregar este
	/// componente junto a su AudioSource (o poner el grupo Music en su campo Output).
	/// </summary>
	public class CheckMusicVolume : MonoBehaviour {
		public void Start (){
			GetComponent<AudioSource>().outputAudioMixerGroup = GameAudio.Music;
		}

		// El volumen ya lo aplica el mixer; se deja el método porque el deslizador lo llama.
		public void UpdateVolume (){
		}
	}
}
