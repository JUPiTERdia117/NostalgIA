using UnityEngine;
using TMPro;

public class GameController : MonoBehaviour
{
    private int starsCollected = 0;

    [SerializeField] TextMeshProUGUI scoreText;


    public void CollectStar()
    {
        starsCollected++;
        scoreText.text = "Stars Collected: " + starsCollected.ToString() + "/3";
    }

}
