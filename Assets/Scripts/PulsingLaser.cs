using UnityEngine;
using System.Collections;

/// <summary>
/// Controla un rayo láser de energía intermitente con fase de advertencia y daño mortal cuando está activo.
/// </summary>
public class PulsingLaser : MonoBehaviour
{
    [Header("Tiempos del Ciclo")]
    [SerializeField] private float offDuration = 2.0f;
    [SerializeField] private float warningDuration = 0.8f;
    [SerializeField] private float activeDuration = 2.2f;

    [Header("Referencias Visuales")]
    [SerializeField] private GameObject laserBeamObject;
    [SerializeField] private Collider laserDamageCollider;
    [SerializeField] private Renderer beamRenderer;
    [SerializeField] private Light laserLight;

    private Material beamMaterial;
    private Color originalEmission;

    void Awake()
    {
        if (beamRenderer != null && beamRenderer.material != null)
        {
            beamMaterial = beamRenderer.material;
            if (beamMaterial.HasProperty("_EmissionColor"))
                originalEmission = beamMaterial.GetColor("_EmissionColor");
        }
    }

    void Start()
    {
        StartCoroutine(LaserCycleRoutine());
    }

    private IEnumerator LaserCycleRoutine()
    {
        while (true)
        {
            // 1. Estado APAGADO
            SetLaserState(false, false);
            yield return new WaitForSeconds(offDuration);

            // 2. Estado ADVERTENCIA (parpadeo rápido)
            float warningElapsed = 0f;
            bool flicker = false;
            while (warningElapsed < warningDuration)
            {
                flicker = !flicker;
                SetLaserState(flicker, false);
                yield return new WaitForSeconds(0.1f);
                warningElapsed += 0.1f;
            }

            // 3. Estado ACTIVO (daño activo)
            SetLaserState(true, true);
            yield return new WaitForSeconds(activeDuration);
        }
    }

    private void SetLaserState(bool visualsActive, bool damageActive)
    {
        if (laserBeamObject != null)
            laserBeamObject.SetActive(visualsActive);

        if (laserDamageCollider != null)
            laserDamageCollider.enabled = damageActive;

        if (laserLight != null)
            laserLight.enabled = visualsActive;

        if (beamMaterial != null && beamMaterial.HasProperty("_EmissionColor"))
        {
            Color emColor = damageActive ? originalEmission * 2.5f : originalEmission * 0.5f;
            beamMaterial.SetColor("_EmissionColor", emColor);
        }
    }
}
