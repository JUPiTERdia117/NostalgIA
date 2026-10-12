using System.Collections;
using UnityEngine;

/// <summary>
/// Pedestal dispensador de núcleos de energía.
/// Si el núcleo actual es absorbido por la aspiradora o lanzado, genera uno nuevo tras 3 segundos.
/// </summary>
public class EnergyDispenser : MonoBehaviour
{
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private float respawnDelay = 3f;

    private GameObject currentOrb;

    void Start()
    {
        SpawnOrb();
    }

    void Update()
    {
        if (currentOrb == null || !currentOrb.activeInHierarchy || Vector3.Distance(transform.position, currentOrb.transform.position) > 2.5f)
        {
            if (currentOrb != null && Vector3.Distance(transform.position, currentOrb.transform.position) > 2.5f)
            {
                currentOrb = null; // Se alejó
            }
            StartCoroutine(RespawnRoutine());
        }
    }

    private bool isRespawning = false;
    private IEnumerator RespawnRoutine()
    {
        if (isRespawning) yield break;
        isRespawning = true;
        yield return new WaitForSeconds(respawnDelay);
        SpawnOrb();
        isRespawning = false;
    }

    private void SpawnOrb()
    {
        if (orbPrefab == null) return;
        currentOrb = Instantiate(orbPrefab, transform.position + Vector3.up * 0.8f, Quaternion.identity, transform);
        currentOrb.name = "EnergyOrb_Dispensed";
    }

    public void SetPrefab(GameObject prefab)
    {
        orbPrefab = prefab;
        if (currentOrb == null) SpawnOrb();
    }
}
