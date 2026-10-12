using UnityEngine;
using UnityEngine.AI;

public class EnemyBasic : MonoBehaviour
{

    

    private GameObject player;

    bool isDead = false;

    NavMeshAgent agent;

    Rigidbody rb;

    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
    }
    void Update () {
        if (isDead) return;
        agent.destination = player.transform.position;
    }


    public void TakeDamage(int damage)
    {
        if (isDead) return;
        // Aquí puedes agregar efectos de daño, animaciones, etc.
        Die();
    }

    void Die()
    {
        isDead = true;
        rb.isKinematic = false;
        agent.enabled = false;
        // Aquí puedes agregar efectos de muerte, animaciones, etc.
        //Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Projectile"))
        {
            Debug.Log("Enemy hit by projectile!");
            Die();
        }
    }
    


}
