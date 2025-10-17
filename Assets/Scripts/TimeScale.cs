using UnityEngine;

namespace BinPickingAI
{
    public class TimeScale : MonoBehaviour
    {
        public float timeScale = 1.0f;
        void Start()
        {
            Time.timeScale = timeScale;
        }
    }
}