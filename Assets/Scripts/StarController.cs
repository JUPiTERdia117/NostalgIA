using UnityEngine;

public class StarController : MonoBehaviour
{
    GameController gameController;

    void Awake(){
        gameController = FindAnyObjectByType<GameController>();
    }


    void OnTriggerEnter(Collider col){
        if(col.tag == "Player"){
            Destroy(this.gameObject);
            gameController.CollectStar();
        }
    }
}
