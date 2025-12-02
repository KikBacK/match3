using TMPro;
using UnityEngine;

namespace DefaultNamespace
{
    public class ScoreView : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;  
        
        public void SetScore(float score) => scoreText.text = score.ToString("0");
    }
}