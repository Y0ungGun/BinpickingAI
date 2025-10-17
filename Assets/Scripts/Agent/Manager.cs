using Unity.MLAgents;
using UnityEngine;

namespace BinPickingAI
{
    public class Manager : MonoBehaviour
    {
        private Trainer trainer;
        void Start()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            trainer = GetComponentInChildren<Trainer>();
            Academy.Instance.EnvironmentStep();
        }

        // Update is called once per frame
        void Update()
        {
            if (trainer.controlFlag.ReadyToObserve)
            {
                trainer.RequestDecision();
                Academy.Instance.EnvironmentStep();
            }
        }
    }
}