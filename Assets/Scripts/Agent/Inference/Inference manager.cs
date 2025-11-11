using Unity.MLAgents;
using UnityEngine;

namespace BinPickingAI
{
    public class InfManager : MonoBehaviour
    {
        private Inferencer inferencer;
        void Start()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            inferencer = GetComponentInChildren<Inferencer>();
            Academy.Instance.EnvironmentStep();
        }

        // Update is called once per frame
        void Update()
        {
            if (inferencer.controlFlag.ReadyToObserve)
            {
                inferencer.RequestDecision();
                Academy.Instance.EnvironmentStep();
            }
        }
    }
}