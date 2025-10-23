using Unity.MLAgents;
using UnityEngine;
using System;
using Random = UnityEngine.Random;
using System.Linq;
using Unity.MLAgents.Sensors;
using System.IO;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;

namespace BinPickingAI
{
    [System.Serializable]
    public class GraspWrenchSpace
    {
        public WrenchConvexHull wrenchConvexHull;
    }
    [System.Serializable]
    public class CubeSpawn
    {
        public Spawner spawner;
        public int numCubes = 1;
        public GameObject Objects;
    }
    [System.Serializable]
    public class VisionModel
    {
        public YOLO yoloModel;
        public Graspability graspabilityModel;
        public RenderTextureSensorComponent renderTextureSensorComponent;
    }
    [System.Serializable]
    public class ControlFlag
    {
        public bool ShouldEndEpisode = false;
        public bool ReadyToObserve = false;
        public bool isMoving = false;
        public bool isClosing = false;
        public bool isGrasping = false;
        public int? movingStartFrame = null;
    }
    public class Trainer : Agent
    {
        private GameObject gripper;
        private ArticulationBody handE;
        private Vector3 StartPos;
        private Vector3 EndPos;
        private GameObject target;
        private Vector3 targetXYZ;
        private Camera cam;
        private int AgentID;
        private Texture2D beforeTarget;
        private Texture2D currentTarget;
        private float beforeGraspability;
        private float currentGraspability;
        public ControlFlag controlFlag;
        public CubeSpawn Cubespawn;
        public VisionModel visionModel;
        public GraspWrenchSpace graspWrenchSpace;
        
        void Start()
        {
            int.TryParse(transform.parent.gameObject.name.Substring(5), out AgentID);
            cam = transform.parent.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "RGBCam")?.GetComponent<Camera>();
            beforeTarget = new Texture2D(120, 120, TextureFormat.RGBA32, false);
            currentTarget = new Texture2D(120, 120, TextureFormat.RGBA32, false);
        }

        public override void OnEpisodeBegin()
        {
            // Cubespawn.spawner.SpawnCubes(Cubespawn.numCubes);
            Cubespawn.spawner.SpawnDebug();
        }
        public override void CollectObservations(VectorSensor sensor)
        {
            controlFlag.ReadyToObserve = false;

            Texture2D yoloInput = Utils.GetTexture2D(cam);
            float[,] yoloOutput = visionModel.yoloModel.YOLOv11(yoloInput);

            List<Texture2D> cropImgs = Utils.GetCrop2D(yoloInput, yoloOutput);
            if (cropImgs.Count == 0)
            {
                Destroy(yoloInput);
                controlFlag.ShouldEndEpisode = true;
                Debug.Log("No objects detected, ending episode.");
                return;
            }
            float[] graspabilities = visionModel.graspabilityModel.GraspabilityInf(cropImgs);
            int targetIdx = Array.IndexOf(graspabilities, graspabilities.Max());

            targetXYZ = Utils.GetTargetXYZ(yoloOutput, targetIdx, cam);
            target = Utils.GetTarget(Cubespawn.spawner.Objects, targetXYZ.x, targetXYZ.y, targetXYZ.z);

            target.AddComponent<TargetContact>();
            graspWrenchSpace.wrenchConvexHull.SetTargetContact(target);
            graspWrenchSpace.wrenchConvexHull.targetContact.SetCollector(graspWrenchSpace.wrenchConvexHull.wrenchManager);

            Graphics.CopyTexture(cropImgs[targetIdx], currentTarget);
            currentGraspability = graspabilities[targetIdx];

            Graphics.Blit(currentTarget, visionModel.renderTextureSensorComponent.RenderTexture);

            Destroy(yoloInput);
            foreach (var img in cropImgs)
            {
                Destroy(img);
            }
            // Destroy(targetImg);
        }
        public override void OnActionReceived(ActionBuffers actions)
        {
            float x = targetXYZ.x + actions.ContinuousActions[0] * 0.1f;
            float y = targetXYZ.y + actions.ContinuousActions[1] * 0.1f;
            float z = targetXYZ.z + actions.ContinuousActions[2] * 0.1f;
            float rx = actions.ContinuousActions[3] * 20;
            float ry = actions.ContinuousActions[4] * 180 + 90f;
            float rz = actions.ContinuousActions[5] * 20;
            // y = targetXYZ.y - 0.1f;
            gripper = SpawnGripper(x, y, z, rx, ry, rz);
            handE = gripper.GetComponentsInChildren<ArticulationBody>().FirstOrDefault(ab => ab.name == "HandE");

            controlFlag.isMoving = true;
            //controlFlag.ReadyToObserve = true;
        }
        public GameObject SpawnGripper(float x, float y, float z, float rx, float ry, float rz)
        {
            GameObject gripperPrefab = Resources.Load<GameObject>("Meshes/Root");

            Quaternion rotation = Quaternion.Euler(0, 0, rz) * Quaternion.Euler(0, ry, 0) * Quaternion.Euler(0, 0, rx);
            GameObject root = Instantiate(gripperPrefab, new Vector3(x, y, z) + rotation * Vector3.up * 1.0f, rotation, transform.parent);
            root.name = "gripper";

            return root;
        }
        void FixedUpdate()
        {
            if (controlFlag.ShouldEndEpisode)
            {
                controlFlag.ShouldEndEpisode = false;
                Destroy(gripper);
                EndEpisode();
                return;
            }
            if (!gripper) return;
            if (controlFlag.isMoving)
            {
                if (!controlFlag.movingStartFrame.HasValue)
                {
                    controlFlag.movingStartFrame = Time.frameCount;
                }
                
                int framesPassed = Time.frameCount - controlFlag.movingStartFrame.Value;
                bool reachedTarget = Mathf.Abs(handE.jointPosition[0] - (-1.0f)) < 0.01f;
                bool timeoutReached = framesPassed >= 10; 
                
                if (reachedTarget || timeoutReached)
                {
                    ArticulationDrive handEdrive = handE.yDrive;
                    handEdrive.driveType = ArticulationDriveType.Target;
                    handEdrive.target = handE.jointPosition[0];
                    handE.yDrive = handEdrive;

                    controlFlag.isMoving = false;
                    controlFlag.isClosing = true;
                    controlFlag.movingStartFrame = null;
                }
                else
                {
                    ArticulationDrive handEdrive = handE.yDrive;
                    handEdrive.target = -1.0f;
                    handE.yDrive = handEdrive;
                }

            }
            if (controlFlag.isClosing)
            {
                PincherController pincherController = handE.GetComponentInChildren<PincherController>();

                if (pincherController.IsClosed())
                {
                    pincherController.gripState = GripState.Fixed;
                    controlFlag.isClosing = false;
                    controlFlag.isGrasping = true;
                }
                
                pincherController.Close = true;
            }
            if (controlFlag.isGrasping)
            {
                if (Mathf.Abs(handE.jointPosition[0]) < 0.01f)
                {
                    ArticulationDrive handEdrive = handE.yDrive;
                    handEdrive.driveType = ArticulationDriveType.Target;
                    handEdrive.target = 0.0f;
                    handE.yDrive = handEdrive;

                    controlFlag.isGrasping = false;
                    CalcReward();
                }
                else
                {
                    ArticulationDrive handEdrive = handE.yDrive;
                    handEdrive.driveType = ArticulationDriveType.Velocity;
                    handEdrive.targetVelocity = -0.2f;
                    handE.yDrive = handEdrive;
                    graspWrenchSpace.wrenchConvexHull.targetContact.CollectWrench = true;
                }

            }
        }
        public void CalcReward()
        {
            PincherController pincherController = handE.GetComponentInChildren<PincherController>();
            float reward =  2 * pincherController.GetGrip() - 1;
            // float reward = 10 * graspWrenchSpace.wrenchConvexHull.GetEpsilon();
            graspWrenchSpace.wrenchConvexHull.ClearWrench();
            Debug.Log($"Epsilon Reward: {reward}");
                        
            // Log reward to CSV
            string csvPath = Path.Combine(Application.dataPath, "Logs", "rewards.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath));  
            
            bool fileExists = File.Exists(csvPath);
            using (StreamWriter writer = new StreamWriter(csvPath, true))
            {
                if (!fileExists)
                {
                    writer.WriteLine("Deg,Reward");
                }
                writer.WriteLine($"{Cubespawn.spawner.RotationInt},{reward}");
            }
            SetReward(reward);

            SaveData();
            Destroy(gripper);
            Destroy(target);
            controlFlag.ReadyToObserve = true;

            if (Cubespawn.Objects.transform.childCount <= 1)
            {
                EndEpisode();
                controlFlag.ReadyToObserve = false;
            }
        }

        public void SaveData()
        {
            if (beforeGraspability != 0)
            {
                Utils.SaveOnlineData(beforeTarget, beforeGraspability, 0);
            }
            Graphics.CopyTexture(currentTarget, beforeTarget);
            beforeGraspability = currentGraspability;
        }
    }
}