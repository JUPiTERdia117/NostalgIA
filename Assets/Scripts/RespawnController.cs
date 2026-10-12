using UnityEngine;

public class RespawnController : MonoBehaviour
{
    private GameController gameController;
    [SerializeField] private ParticleSystem activationParticles;
    [SerializeField] private Renderer beaconRenderer;
    [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.4f);

    void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            if (gameController == null) gameController = FindAnyObjectByType<GameController>();
            if (gameController != null)
            {
                gameController.UpdateSpawnPoint(transform.position);
            }

            if (beaconRenderer != null)
            {
                beaconRenderer.material.SetColor("_EmissionColor", activeColor * 2f);
                beaconRenderer.material.SetColor("_BaseColor", activeColor);
            }

            if (activationParticles != null)
            {
                activationParticles.Play();
            }

            // Desactivar el trigger collider para no reactivarlo
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }
}
