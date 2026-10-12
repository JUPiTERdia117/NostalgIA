using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la victoria en la Terraza de Combate de los Centinelas.
/// Al derrotar a los centinelas, revela la Estrella 2 y otorga retroalimentación audiovisual.
/// </summary>
public class ArenaGateManager : MonoBehaviour
{
    [SerializeField] private BasicEnemy[] enemies;
    [SerializeField] private GameObject starReward;
    [SerializeField] private Transform optionalGate;
    [SerializeField] private Vector3 gateOpenOffset = new Vector3(0, -6f, 0);

    private bool cleared = false;

    void Update()
    {
        if (cleared) return;

        bool allDefeated = true;
        if (enemies != null && enemies.Length > 0)
        {
            foreach (BasicEnemy enemy in enemies)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy)
                {
                    allDefeated = false;
                    break;
                }
            }
        }

        if (allDefeated)
        {
            cleared = true;
            OnArenaCleared();
        }
    }

    private void OnArenaCleared()
    {
        if (GameAudio.Instance != null)
        {
            GameAudio.Instance.PlayCheckpoint();
        }

        LevelUIController ui = FindAnyObjectByType<LevelUIController>();
        if (ui != null)
        {
            ui.ShowToast("¡CENTINELAS DERROTADOS! ¡ESTRELLA 2 DESBLOQUEADA!");
        }

        if (starReward != null)
        {
            starReward.SetActive(true);
        }

        if (optionalGate != null)
        {
            StartCoroutine(AnimateGate());
        }
    }

    private IEnumerator AnimateGate()
    {
        Vector3 start = optionalGate.position;
        Vector3 target = start + gateOpenOffset;
        float elapsed = 0f;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            optionalGate.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / 1.5f));
            yield return null;
        }
        optionalGate.position = target;
    }
}
