using UnityEngine;

namespace BinPickingAI
{
    public class Spawner : MonoBehaviour
    {
        public GameObject Objects;
        private GameObject Agent;
        private Trainer trainer;
        private Vector3 SpawnRangeMin;
        private Vector3 SpawnRangeMax;
        private bool spawned = false;
        private int frameCount = 0;
        private System.Random random;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            Agent = transform.parent.gameObject;
            trainer = Agent.GetComponentInChildren<Trainer>();
            SpawnRangeMin = Agent.transform.position - new Vector3(1, -1, 1);
            SpawnRangeMax = Agent.transform.position + new Vector3(1, 1, 1);

            random = new System.Random(System.Guid.NewGuid().GetHashCode());
        }
        void FixedUpdate()
        {
            if (spawned)
            {
                frameCount++;
                if (frameCount > 100)
                {
                    DeleteOutliers();
                    frameCount = 0;
                    spawned = false;
                    trainer.controlFlag.ReadyToObserve = true;
                }
            }
        }
        public void SpawnCubes(int count)
        {
            foreach (Transform child in Objects.transform)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < count; i++)
            {
                SpawnCube();
            }
            spawned = true;
        }
        public void SpawnCube()
        {
            GameObject cube = new GameObject("obj");
            cube.AddComponent<MeshFilter>().mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            cube.AddComponent<MeshRenderer>();
            cube.AddComponent<BoxCollider>();
            cube.AddComponent<Rigidbody>();

            cube.transform.parent = Objects.transform;
            cube.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            cube.transform.position = new Vector3(Random.Range(SpawnRangeMin.x, SpawnRangeMax.x), Random.Range(SpawnRangeMin.y, SpawnRangeMax.y), Random.Range(SpawnRangeMin.z, SpawnRangeMax.z));
            cube.transform.rotation = Random.rotation;

            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            Color[] colors = { new Color(0.84f, 0.258f, 0.336f), new Color(0.93f, 0.785f, 0.273f), new Color(0.086f, 0.45f, 0.35f), new Color(0.074f, 0.551f, 0.852f), new Color(0.574f, 0.336f, 0.742f) }; // Purple
            renderer.material.color = colors[Random.Range(0, colors.Length)];
            renderer.material.SetFloat("_Smoothness", 0.0f);

            
            Rigidbody rb = cube.GetComponent<Rigidbody>();
            rb.useGravity = true;
        }
        public void DeleteOutliers()
        {
            foreach (Transform child in Objects.transform)
            {
                if (child.position.x < SpawnRangeMin.x - 0.5 || child.position.x > SpawnRangeMax.x + 0.5 ||
                    child.position.z < SpawnRangeMin.z - 0.5 || child.position.z > SpawnRangeMax.z + 0.5)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
}
