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
        public bool ReadyToObserve = false;
        public bool isMoving = false;
        public bool isClosing = false;
        public bool isGrasping = false;
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
        public ControlFlag controlFlag;
        public CubeSpawn Cubespawn;
        public VisionModel visionModel;
        
        void Start()
        {
            int.TryParse(transform.parent.gameObject.name.Substring(5), out AgentID);
            cam = transform.parent.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "RGBCam")?.GetComponent<Camera>();
        }

        public override void OnEpisodeBegin()
        {
            Cubespawn.spawner.SpawnCubes(Cubespawn.numCubes);
        }
        public override void CollectObservations(VectorSensor sensor)
        {
            controlFlag.ReadyToObserve = false;

            Texture2D yoloInput = Utils.GetTexture2D(cam);
            float[,] yoloOutput = visionModel.yoloModel.YOLOv11(yoloInput);
            if (yoloOutput.GetLength(0) == 0)
            {
                Destroy(yoloInput);
                EndEpisode();
            }

            List<Texture2D> cropImgs = Utils.GetCrop2D(yoloInput, yoloOutput);
            float[] graspabilities = visionModel.graspabilityModel.GraspabilityInf(cropImgs);
            int targetIdx = Array.IndexOf(graspabilities, graspabilities.Max());

            targetXYZ = Utils.GetTargetXYZ(yoloOutput, targetIdx, cam);
            target = Utils.GetTarget(Cubespawn.spawner.Objects, targetXYZ.x, targetXYZ.y, targetXYZ.z);
            Texture2D targetImg = cropImgs[targetIdx];
            float graspability = graspabilities[targetIdx];

            Graphics.Blit(targetImg, visionModel.renderTextureSensorComponent.RenderTexture);
            SaveImageWithBoundingBoxes(yoloInput, yoloOutput, $"YOLORESULT.png");

            Destroy(yoloInput);
            foreach (var img in cropImgs)
            {
                Destroy(img);
            }
            Destroy(targetImg);
        }
        public override void OnActionReceived(ActionBuffers actions)
        {
            float x = targetXYZ.x + actions.ContinuousActions[0] * 0.1f;
            float y = targetXYZ.y + actions.ContinuousActions[1] * 0.1f;
            float z = targetXYZ.z + actions.ContinuousActions[2] * 0.1f;
            float rx = actions.ContinuousActions[3] * 30;
            float ry = actions.ContinuousActions[4] * 180 + 90f;
            float rz = actions.ContinuousActions[5] * 30;

            x = targetXYZ.x + Random.Range(-1f, 1f) * 0.1f;
            y = targetXYZ.y + Random.Range(-1f, 1f) * 0.1f;
            z = targetXYZ.z + Random.Range(-1f, 1f) * 0.1f;
            rx = Random.Range(-1f, 1f) * 30;
            ry = Random.Range(-1f, 1f) * 180 + 90f;
            rz = Random.Range(-1f, 1f) * 30;
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
            if (controlFlag.isMoving)
            {
                if (handE.jointPosition[0] - (-0.9f) < 0.01f)
                {
                    ArticulationDrive handEdrive = handE.yDrive;
                    handEdrive.driveType = ArticulationDriveType.Target;
                    handEdrive.target = -0.9f;
                    handE.yDrive = handEdrive;

                    controlFlag.isMoving = false;
                    controlFlag.isClosing = true;
                    //Destroy(gripper);
                    //controlFlag.ReadyToObserve = true;
                }
                else
                {
                    ArticulationDrive handEdrive = handE.yDrive;
                    handEdrive.targetVelocity = 0.2f;
                    handE.yDrive = handEdrive;
                }

            }
            if (controlFlag.isClosing)
            {
                PincherController pincherController = handE.GetComponentInChildren<PincherController>();
                pincherController.Close = true;
                if (pincherController.IsClosed())
                {
                    pincherController.gripState = GripState.Fixed;
                    controlFlag.isClosing = false;
                    controlFlag.isGrasping = true;
                }
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
                }

            }
        }
        public void CalcReward()
        {
            Destroy(gripper);
            Destroy(target);
            controlFlag.ReadyToObserve = true;

            if (Cubespawn.Objects.transform.childCount <= 1)
            {
                EndEpisode();
                controlFlag.ReadyToObserve = false;
            }

        }
        
        //////////////////////////////////////////////////////////////////////////////////////////////
        public void SaveImageWithBoundingBoxes(Texture2D inputTexture, float[,] output, string filePath)
        {
            // 2. YOLO 출력값을 사용해 바운딩 박스 그리기
            DrawBoundingBoxes(inputTexture, output);

            // 3. 결과 이미지를 PNG로 저장
            SaveTextureAsPNG(inputTexture, filePath);

            Debug.Log($"Image with bounding boxes saved to: {filePath}");
        }
        private void DrawBoundingBoxes(Texture2D texture, float[,] output)
        {
            int rows = output.GetLength(0); // YOLO 출력의 행 개수 (300)
            int cols = output.GetLength(1); // YOLO 출력의 열 개수 (6)

            int width = texture.width;
            int height = texture.height;

            if (cols < 4)
            {
                Debug.LogError("Output does not contain enough columns for bounding box coordinates.");
                return;
            }

            // 1. 각 바운딩 박스 처리
            for (int i = 0; i < rows; i++)
            {
                if (output[i, 4] < 0.5f) // 신뢰도 임계값 설정
                    continue;
                // YOLO 출력값에서 좌표 가져오기 (x1, y1, x2, y2)
                int x1 = Mathf.RoundToInt(output[i, 0]);
                int y1 = height - Mathf.RoundToInt(output[i, 1]);
                int x2 = Mathf.RoundToInt(output[i, 2]);
                int y2 = height - Mathf.RoundToInt(output[i, 3]);

                

                // 3. 바운딩 박스 그리기
                DrawRectangle(texture, x1, y2, x2, y1, Color.red);
            }

            // 4. 텍스처 적용
            texture.Apply();
        }

        private void DrawRectangle(Texture2D texture, int x1, int y1, int x2, int y2, Color color)
        {
            // 상단 선
            for (int x = x1; x <= x2; x++)
            {
                texture.SetPixel(x, y1, color);
            }

            // 하단 선
            for (int x = x1; x <= x2; x++)
            {
                texture.SetPixel(x, y2, color);
            }

            // 좌측 선
            for (int y = y1; y <= y2; y++)
            {
                texture.SetPixel(x1, y, color);
            }

            // 우측 선
            for (int y = y1; y <= y2; y++)
            {
                texture.SetPixel(x2, y, color);
            }
        }

        private void SaveTextureAsPNG(Texture2D texture, string filePath)
        {
            byte[] pngData = texture.EncodeToPNG();
            if (pngData != null)
            {
                File.WriteAllBytes(filePath, pngData);
            }
            else
            {
                Debug.LogError("Failed to encode texture to PNG.");
            }
        }
    }
}