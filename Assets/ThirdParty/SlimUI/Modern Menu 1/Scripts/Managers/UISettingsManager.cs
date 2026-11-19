using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using NostalgIA.Presentation.Audio;
using NostalgIA.Presentation.VFX;
using NostalgIA.Input;
using NostalgIA.Core.Game;
using NostalgIA.Core.Services;

namespace SlimUI.ModernMenu{
	public class UISettingsManager : MonoBehaviour {

		public enum Platform {Desktop, Mobile};
		public Platform platform;
		// toggle buttons
		[Header("MOBILE SETTINGS")]
		public GameObject mobileSFXtext;
		public GameObject mobileMusictext;
		public GameObject mobileShadowofftextLINE;
		public GameObject mobileShadowlowtextLINE;
		public GameObject mobileShadowhightextLINE;

		[Header("VIDEO SETTINGS")]
		public GameObject fullscreentext;
		public GameObject ambientocclusiontext;
		public GameObject shadowofftextLINE;
		public GameObject shadowlowtextLINE;
		public GameObject shadowhightextLINE;
		public GameObject aaofftextLINE;
		public GameObject aa2xtextLINE;
		public GameObject aa4xtextLINE;
		public GameObject aa8xtextLINE;
		public GameObject vsynctext;
		public GameObject motionblurtext;
		public GameObject texturelowtextLINE;
		public GameObject texturemedtextLINE;
		public GameObject texturehightextLINE;
		public GameObject textureveryhightextLINE;
		public GameObject cameraeffectstext; 

		[Header("GAME SETTINGS")]
		public GameObject showhudtext;
		public GameObject tooltipstext;
		public GameObject difficultynormaltext;
		public GameObject difficultynormaltextLINE;
		public GameObject difficultyhardcoretext;
		public GameObject difficultyhardcoretextLINE;

		[Header("CONTROLS SETTINGS")]
		public GameObject invertmousetext;

		// sliders
		public GameObject musicSlider;
		[Tooltip("Deslizador del volumen de efectos (NostalgIA).")]
		public GameObject sfxSlider;
		public GameObject sensitivityXSlider;
		public GameObject sensitivityYSlider;
		public GameObject mouseSmoothSlider;

		private float sliderValue = 0.0f;
		private float sliderValueSmoothing = 0.0f;
		

		public void  Start (){
			// check difficulty
			if(PlayerPrefs.GetInt("NormalDifficulty") == 1){
				difficultynormaltextLINE.gameObject.SetActive(true);
				difficultyhardcoretextLINE.gameObject.SetActive(false);
			}
			else
			{
				difficultyhardcoretextLINE.gameObject.SetActive(true);
				difficultynormaltextLINE.gameObject.SetActive(false);
			}

			// check slider values
			// NostalgIA: los volúmenes viven en GameAudio (mixer del juego); 0,5 = volumen original.
			musicSlider.GetComponent<Slider>().value = GameAudio.MusicVolume;
			if(sfxSlider != null) sfxSlider.GetComponent<Slider>().value = GameAudio.SfxVolume;
			// NostalgIA: la sensibilidad vive en LookSettings (la usa InputReader y el menú de pausa).
			// Antes solo se guardaba al arrastrar y el juego nunca leía el valor.
			SetupSensitivitySlider(sensitivityXSlider, LookSettings.SensitivityX, value => LookSettings.SensitivityX = value);
			SetupSensitivitySlider(sensitivityYSlider, LookSettings.SensitivityY, value => LookSettings.SensitivityY = value);
			mouseSmoothSlider.GetComponent<Slider>().value = PlayerPrefs.GetFloat("MouseSmoothing");

			// check full screen
			if(Screen.fullScreen == true){
				fullscreentext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(Screen.fullScreen == false){
				fullscreentext.GetComponent<TMP_Text>().text = "apagado";
			}

			// check hud value (NostalgIA: HudSettings; por defecto el HUD se muestra)
			showhudtext.GetComponent<TMP_Text>().text = HudSettings.Show ? "encendido" : "apagado";

			// check tool tip value (NostalgIA: la escena del menú quitó esta opción, por eso reviso que exista)
			if(tooltipstext == null){}
			else if(PlayerPrefs.GetInt("ToolTips")==0){
				tooltipstext.GetComponent<TMP_Text>().text = "apagado";
			}
			else{
				tooltipstext.GetComponent<TMP_Text>().text = "encendido";
			}

			// NostalgIA: las sombras las define el preset de «Calidad gráfica» (GameGraphics).
			// Los QualitySettings.shadow* del original no afectan a URP y aquí solo estorbaban.


			// check vsync
			if(QualitySettings.vSyncCount == 0){
				vsynctext.GetComponent<TMP_Text>().text = "apagado";
			}
			else if(QualitySettings.vSyncCount == 1){
				vsynctext.GetComponent<TMP_Text>().text = "encendido";
			}

			// check mouse inverse
			invertmousetext.GetComponent<TMP_Text>().text = LookSettings.Invert ? "encendido" : "apagado";

			// NostalgIA: filas para cambiar las teclas de agacharse y de cambiar herramienta (solo PC).
			if(!Application.isMobilePlatform) AddKeyRows();

			// check motion blur
			if(PlayerPrefs.GetInt("MotionBlur")==0){
				motionblurtext.GetComponent<TMP_Text>().text = "apagado";
			}
			else if(PlayerPrefs.GetInt("MotionBlur")==1){
				motionblurtext.GetComponent<TMP_Text>().text = "encendido";
			}

			// check ambient occlusion
			if(PlayerPrefs.GetInt("AmbientOcclusion")==0){
				ambientocclusiontext.GetComponent<TMP_Text>().text = "apagado";
			}
			else if(PlayerPrefs.GetInt("AmbientOcclusion")==1){
				ambientocclusiontext.GetComponent<TMP_Text>().text = "encendido";
			}

			// NostalgIA: la fila de texturas es la «Calidad gráfica» y marca el preset guardado.
			SetupVeryHighQualityButton();
			ShowGraphicsLine(GameGraphics.Current);
		}

		public void Update (){
			//sliderValue = musicSlider.GetComponent<Slider>().value;
			sliderValueSmoothing = mouseSmoothSlider.GetComponent<Slider>().value;
		}

		public void FullScreen (){
			Screen.fullScreen = !Screen.fullScreen;

			if(Screen.fullScreen == true){
				fullscreentext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(Screen.fullScreen == false){
				fullscreentext.GetComponent<TMP_Text>().text = "apagado";
			}
		}

		// NostalgIA: el deslizador de música controla el grupo Music del mixer (toda la música del juego).
		public void MusicSlider (){
			GameAudio.SetMusicVolume(musicSlider.GetComponent<Slider>().value);
		}

		// NostalgIA: el deslizador de efectos controla el grupo SFX del mixer (todos los efectos).
		public void SfxSlider (){
			if(sfxSlider != null) GameAudio.SetSfxVolume(sfxSlider.GetComponent<Slider>().value);
		}

		// NostalgIA: el EventTrigger de cada deslizador llama a estos dos al arrastrar.
		// El valor ya se guarda con onValueChanged (SetupSensitivitySlider); los dejo para no romper esa llamada.
		public void SensitivityXSlider (){
			LookSettings.SensitivityX = sensitivityXSlider.GetComponent<Slider>().value;
		}

		public void SensitivityYSlider (){
			LookSettings.SensitivityY = sensitivityYSlider.GetComponent<Slider>().value;
		}

		// NostalgIA: rango de 1 a 10 (5 = sensibilidad original) y guardado al mover el deslizador.
		private static void SetupSensitivitySlider (GameObject sliderObject, float value, UnityEngine.Events.UnityAction<float> onChange){
			if(sliderObject == null) return;
			Slider slider = sliderObject.GetComponent<Slider>();
			slider.minValue = LookSettings.Min;
			slider.maxValue = LookSettings.Max;
			slider.SetValueWithoutNotify(value);
			slider.onValueChanged.AddListener(onChange);
		}

		public void SensitivitySmoothing (){
			PlayerPrefs.SetFloat("MouseSmoothing", sliderValueSmoothing);
			Debug.Log(PlayerPrefs.GetFloat("MouseSmoothing"));
		}

		// the playerprefs variable that is checked to enable hud while in game
		// NostalgIA: el HUD y los controles táctiles escuchan HudSettings y se ocultan al momento.
		public void ShowHUD (){
			HudSettings.Show = !HudSettings.Show;
			showhudtext.GetComponent<TMP_Text>().text = HudSettings.Show ? "encendido" : "apagado";
		}

		// ---------------------------------------------------------------- teclas configurables
		// NostalgIA: se copian la etiqueta y el botón de «invertir mouse» dos veces, debajo de él,
		// para no rehacer a mano el estilo del menú. Cada copia muestra su tecla y, al pulsarla,
		// espera la tecla nueva (InputReader.StartRebind).
		private TMP_Text crouchKeyText;
		private TMP_Text switchKeyText;

		private void AddKeyRows (){
			RectTransform invertButton = invertmousetext.transform.parent as RectTransform;
			RectTransform invertLabel = FindLabel(invertButton.parent, "invertir mouse");
			RectTransform sensYButton = sensitivityYSlider.GetComponent<RectTransform>();
			if(invertButton == null || invertLabel == null) return;
			// Separación entre filas: la misma que hay entre «sensibilidad Y» e «invertir mouse».
			float step = invertButton.anchoredPosition.y - sensYButton.anchoredPosition.y;
			crouchKeyText = AddKeyRow(invertLabel, invertButton, step, "agacharse", InputActionNames.Crouch);
			switchKeyText = AddKeyRow(invertLabel, invertButton, step * 2f, "cambiar herramienta", InputActionNames.SwitchTool);
			RefreshKeyRows();
		}

		private TMP_Text AddKeyRow (RectTransform label, RectTransform button, float offsetY, string title, string action){
			RectTransform newLabel = Instantiate(label, label.parent);
			newLabel.anchoredPosition += new Vector2(0f, offsetY);
			newLabel.GetComponent<TMP_Text>().text = title;

			RectTransform newButton = Instantiate(button, button.parent);
			newButton.anchoredPosition += new Vector2(0f, offsetY);
			Button clickable = newButton.GetComponent<Button>();
			clickable.onClick = new Button.ButtonClickedEvent(); // quita la llamada a InvertMouse de la copia
			TMP_Text keyText = newButton.GetComponentInChildren<TMP_Text>();
			clickable.onClick.AddListener(() => {
				if(!ServiceLocator.TryGet(out InputReader reader)) return;
				keyText.text = "pulsa una tecla";
				reader.StartRebind(action, RefreshKeyRows);
			});
			return keyText;
		}

		private void RefreshKeyRows (){
			if(crouchKeyText == null || !ServiceLocator.TryGet(out InputReader reader)) return;
			crouchKeyText.text = reader.GetKeyLabel(InputActionNames.Crouch);
			switchKeyText.text = reader.GetKeyLabel(InputActionNames.SwitchTool);
		}

		private static RectTransform FindLabel (Transform parent, string text){
			foreach(TMP_Text label in parent.GetComponentsInChildren<TMP_Text>(true)){
				if(label.text == text) return label.rectTransform;
			}
			return null;
		}

		// the playerprefs variable that is checked to enable mobile sfx while in game
		public void MobileSFXMute (){
			if(PlayerPrefs.GetInt("Mobile_MuteSfx")==0){
				PlayerPrefs.SetInt("Mobile_MuteSfx",1);
				mobileSFXtext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(PlayerPrefs.GetInt("Mobile_MuteSfx")==1){
				PlayerPrefs.SetInt("Mobile_MuteSfx",0);
				mobileSFXtext.GetComponent<TMP_Text>().text = "apagado";
			}
		}

		public void MobileMusicMute (){
			if(PlayerPrefs.GetInt("Mobile_MuteMusic")==0){
				PlayerPrefs.SetInt("Mobile_MuteMusic",1);
				mobileMusictext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(PlayerPrefs.GetInt("Mobile_MuteMusic")==1){
				PlayerPrefs.SetInt("Mobile_MuteMusic",0);
				mobileMusictext.GetComponent<TMP_Text>().text = "apagado";
			}
		}

		// show tool tips like: 'How to Play' control pop ups
		public void ToolTips (){
			if(PlayerPrefs.GetInt("ToolTips")==0){
				PlayerPrefs.SetInt("ToolTips",1);
				tooltipstext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(PlayerPrefs.GetInt("ToolTips")==1){
				PlayerPrefs.SetInt("ToolTips",0);
				tooltipstext.GetComponent<TMP_Text>().text = "apagado";
			}
		}

		public void NormalDifficulty (){
			difficultyhardcoretextLINE.gameObject.SetActive(false);
			difficultynormaltextLINE.gameObject.SetActive(true);
			PlayerPrefs.SetInt("NormalDifficulty",1);
			PlayerPrefs.SetInt("HardCoreDifficulty",0);
		}

		public void HardcoreDifficulty (){
			difficultyhardcoretextLINE.gameObject.SetActive(true);
			difficultynormaltextLINE.gameObject.SetActive(false);
			PlayerPrefs.SetInt("NormalDifficulty",0);
			PlayerPrefs.SetInt("HardCoreDifficulty",1);
		}

		public void ShadowsOff (){
			PlayerPrefs.SetInt("Shadows",0);
			QualitySettings.shadowCascades = 0;
			QualitySettings.shadowDistance = 0;
			shadowofftextLINE.gameObject.SetActive(true);
			shadowlowtextLINE.gameObject.SetActive(false);
			shadowhightextLINE.gameObject.SetActive(false);
		}

		public void ShadowsLow (){
			PlayerPrefs.SetInt("Shadows",1);
			QualitySettings.shadowCascades = 2;
			QualitySettings.shadowDistance = 75;
			shadowofftextLINE.gameObject.SetActive(false);
			shadowlowtextLINE.gameObject.SetActive(true);
			shadowhightextLINE.gameObject.SetActive(false);
		}

		public void ShadowsHigh (){
			PlayerPrefs.SetInt("Shadows",2);
			QualitySettings.shadowCascades = 4;
			QualitySettings.shadowDistance = 500;
			shadowofftextLINE.gameObject.SetActive(false);
			shadowlowtextLINE.gameObject.SetActive(false);
			shadowhightextLINE.gameObject.SetActive(true);
		}

		public void MobileShadowsOff (){
			PlayerPrefs.SetInt("MobileShadows",0);
			QualitySettings.shadowCascades = 0;
			QualitySettings.shadowDistance = 0;
			mobileShadowofftextLINE.gameObject.SetActive(true);
			mobileShadowlowtextLINE.gameObject.SetActive(false);
			mobileShadowhightextLINE.gameObject.SetActive(false);
		}

		public void MobileShadowsLow (){
			PlayerPrefs.SetInt("MobileShadows",1);
			QualitySettings.shadowCascades = 2;
			QualitySettings.shadowDistance = 75;
			mobileShadowofftextLINE.gameObject.SetActive(false);
			mobileShadowlowtextLINE.gameObject.SetActive(true);
			mobileShadowhightextLINE.gameObject.SetActive(false);
		}

		public void MobileShadowsHigh (){
			PlayerPrefs.SetInt("MobileShadows",2);
			QualitySettings.shadowCascades = 4;
			QualitySettings.shadowDistance = 500;
			mobileShadowofftextLINE.gameObject.SetActive(false);
			mobileShadowlowtextLINE.gameObject.SetActive(false);
			mobileShadowhightextLINE.gameObject.SetActive(true);
		}

		public void vsync (){
			if(QualitySettings.vSyncCount == 0){
				QualitySettings.vSyncCount = 1;
				vsynctext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(QualitySettings.vSyncCount == 1){
				QualitySettings.vSyncCount = 0;
				vsynctext.GetComponent<TMP_Text>().text = "apagado";
			}
		}

		// NostalgIA: la inversión vive en LookSettings para que el juego la lea sin consultar PlayerPrefs cada frame.
		public void InvertMouse (){
			LookSettings.Invert = !LookSettings.Invert;
			invertmousetext.GetComponent<TMP_Text>().text = LookSettings.Invert ? "encendido" : "apagado";
		}

		public void MotionBlur (){
			if(PlayerPrefs.GetInt("MotionBlur")==0){
				PlayerPrefs.SetInt("MotionBlur",1);
				motionblurtext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(PlayerPrefs.GetInt("MotionBlur")==1){
				PlayerPrefs.SetInt("MotionBlur",0);
				motionblurtext.GetComponent<TMP_Text>().text = "apagado";
			}
		}

		public void AmbientOcclusion (){
			if(PlayerPrefs.GetInt("AmbientOcclusion")==0){
				PlayerPrefs.SetInt("AmbientOcclusion",1);
				ambientocclusiontext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(PlayerPrefs.GetInt("AmbientOcclusion")==1){
				PlayerPrefs.SetInt("AmbientOcclusion",0);
				ambientocclusiontext.GetComponent<TMP_Text>().text = "apagado";
			}
		}

		public void CameraEffects (){
			if(PlayerPrefs.GetInt("CameraEffects")==0){
				PlayerPrefs.SetInt("CameraEffects",1);
				cameraeffectstext.GetComponent<TMP_Text>().text = "encendido";
			}
			else if(PlayerPrefs.GetInt("CameraEffects")==1){
				PlayerPrefs.SetInt("CameraEffects",0);
				cameraeffectstext.GetComponent<TMP_Text>().text = "apagado";
			}
		}

		// NostalgIA: los botones bajo/medio/alto/muy alto de «Calidad gráfica» aplican el preset completo
		// (resolución, texturas, HDR, Bloom y sombras) con GameGraphics.
		public void TexturesLow (){
			GameGraphics.Set(GraphicsPreset.Low);
			ShowGraphicsLine(GraphicsPreset.Low);
		}

		public void TexturesMed (){
			GameGraphics.Set(GraphicsPreset.Medium);
			ShowGraphicsLine(GraphicsPreset.Medium);
		}

		public void TexturesHigh (){
			GameGraphics.Set(GraphicsPreset.High);
			ShowGraphicsLine(GraphicsPreset.High);
		}

		public void TexturesVeryHigh (){
			GameGraphics.Set(GraphicsPreset.VeryHigh);
			ShowGraphicsLine(GraphicsPreset.VeryHigh);
		}

		private void SetupVeryHighQualityButton (){
			if(texturelowtextLINE == null || texturemedtextLINE == null || texturehightextLINE == null) return;
			if(textureveryhightextLINE != null) return;

			Transform container = texturehightextLINE.transform.parent;
			if(container == null) return;

			Transform lowT = container.Find("TextureLow_Btn");
			Transform medT = container.Find("TextureMed_Btn");
			Transform highT = container.Find("TextureHigh_Btn");
			if(lowT == null || medT == null || highT == null) return;

			GameObject veryHighBtnObj = Instantiate(highT.gameObject, container);
			veryHighBtnObj.name = "TextureVeryHigh_Btn";
			Button veryHighBtn = veryHighBtnObj.GetComponent<Button>();
			if(veryHighBtn != null){
				veryHighBtn.onClick = new Button.ButtonClickedEvent();
				veryHighBtn.onClick.AddListener(TexturesVeryHigh);
			}
			TMP_Text veryHighTmp = veryHighBtnObj.GetComponentInChildren<TMP_Text>(true);
			if(veryHighTmp != null) veryHighTmp.text = "muy alto";

			textureveryhightextLINE = Instantiate(texturehightextLINE, container);
			textureveryhightextLINE.name = "VERYHIGHLINE1";

			RectTransform[] buttons = {
				lowT as RectTransform,
				medT as RectTransform,
				highT as RectTransform,
				veryHighBtnObj.GetComponent<RectTransform>()
			};
			RectTransform[] lines = {
				texturelowtextLINE.GetComponent<RectTransform>(),
				texturemedtextLINE.GetComponent<RectTransform>(),
				texturehightextLINE.GetComponent<RectTransform>(),
				textureveryhightextLINE.GetComponent<RectTransform>()
			};

			// El ancho de cada opción se mide con la fuente real (JikiTourai es ancha): así las cuatro
			// caben en la fila sin encimarse. Si no caben, se reduce la letra de las cuatro por igual.
			const float rowLeft = 190f;
			const float rowSpan = 400f;
			const float minGap = 18f;
			float[] widths = new float[4];
			TMP_Text[] labels = new TMP_Text[4];
			float totalWidth = 0f;
			for(int i = 0; i < 4; i++){
				TMP_Text tmp = buttons[i].GetComponentInChildren<TMP_Text>(true);
				labels[i] = tmp;
				if(tmp != null){
					float size = tmp.enableAutoSizing ? tmp.fontSizeMax : tmp.fontSize;
					tmp.enableAutoSizing = false;
					tmp.fontSize = size * 0.8f;
					tmp.textWrappingMode = TextWrappingModes.NoWrap;
					tmp.horizontalAlignment = HorizontalAlignmentOptions.Left; // empieza donde la fila la pone
					widths[i] = tmp.GetPreferredValues(tmp.text).x;
				}
				totalWidth += widths[i];
			}
			float room = rowSpan - minGap * 3f;
			if(totalWidth > room && totalWidth > 0f){
				float shrink = room / totalWidth;
				for(int i = 0; i < 4; i++){
					if(labels[i] != null) labels[i].fontSize *= shrink;
					widths[i] *= shrink;
				}
				totalWidth = room;
			}
			float gap = Mathf.Max(minGap, (rowSpan - totalWidth) / 3f);
			float cursorX = rowLeft;

			for(int i = 0; i < 4; i++){
				RectTransform btnRect = buttons[i];
				RectTransform lineRect = lines[i];
				TMP_Text tmp = btnRect.GetComponentInChildren<TMP_Text>(true);
				float textLeftOffset = -50.45f;
				if(tmp != null){
					RectTransform textRect = tmp.rectTransform;
					textLeftOffset = -btnRect.sizeDelta.x * btnRect.pivot.x
					                 + textRect.anchoredPosition.x
					                 - textRect.sizeDelta.x * textRect.pivot.x;
				}

				float w = widths[i];
				btnRect.anchoredPosition = new Vector2(cursorX - textLeftOffset, btnRect.anchoredPosition.y);
				lineRect.anchoredPosition = new Vector2(cursorX + w * 0.5f, lineRect.anchoredPosition.y);
				lineRect.sizeDelta = new Vector2(w, lineRect.sizeDelta.y);
				cursorX += w + gap;
			}
		}

		private void ShowGraphicsLine (GraphicsPreset preset){
			if(texturelowtextLINE != null) texturelowtextLINE.SetActive(preset == GraphicsPreset.Low);
			if(texturemedtextLINE != null) texturemedtextLINE.SetActive(preset == GraphicsPreset.Medium);
			if(texturehightextLINE != null) texturehightextLINE.SetActive(preset == GraphicsPreset.High);
			if(textureveryhightextLINE != null) textureveryhightextLINE.SetActive(preset == GraphicsPreset.VeryHigh);
		}
	}
}