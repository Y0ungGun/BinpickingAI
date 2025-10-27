using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace BinPickingAI
{
    public static class Utils
    {
        private static int onlineDataID = 0;
        public static void SaveOnlineData(Texture2D img, float graspability, int success)
        {
            byte[] imgBytes = img.EncodeToPNG();
            string imgPath = Path.Combine(Application.persistentDataPath, "OnlineData", $"{onlineDataID}_{graspability}_{success}.png");
            string path = $"OnlineData/{onlineDataID}_{graspability}_{success}.png";
            File.WriteAllBytes(path, imgBytes);

            onlineDataID++;
        }
        private static RenderTexture rt = new RenderTexture(1280, 736, 16);
        public static Texture2D GetTexture2D(Camera cam)
        {
            cam.targetTexture = rt;
            RenderTexture.active = rt;
            cam.Render();

            // 중앙 기준 736x736 영역 계산
            int cropWidth = 736;
            int cropHeight = 736;
            int centerX = rt.width / 2;
            int centerY = rt.height / 2;
            int startX = centerX - cropWidth / 2;
            int startY = centerY - cropHeight / 2;

            Texture2D txt2D = new Texture2D(cropWidth, cropHeight, TextureFormat.RGBA32, false);
            txt2D.ReadPixels(new Rect(startX, startY, cropWidth, cropHeight), 0, 0);
            txt2D.Apply();

            RenderTexture.active = null;
            rt.Release();
            cam.targetTexture = null;

            return txt2D;
        }

        public static Vector3 GetTargetXYZ(float[,] output, int id, Camera depthCamera)
        {
            int x_ = ((int)output[id, 0] + (int)output[id, 2]) / 2;
            int y_ = 736 - ((int)output[id, 1] + (int)output[id, 3]) / 2;

            ComputeShader cs = Resources.Load<ComputeShader>("NormalizeDepth");
            RenderTexture resultTexture = new RenderTexture(1280, 736, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                name = "Result Texture (dynamic)"
            };
            RenderTexture depthTexture = new RenderTexture(1280, 736, 24, RenderTextureFormat.Depth)
            {
                enableRandomWrite = false,
                name = "Depth Texture (dynamic)"
            };
            resultTexture.Create();
            depthTexture.Create();

            depthCamera.targetTexture = depthTexture;
            depthCamera.Render();

            int kernelHandle = cs.FindKernel("CSMain");
            cs.SetTexture(kernelHandle, "Source", depthTexture); //source texture�� depthtexture�� ������.
            cs.SetTexture(kernelHandle, "Result", resultTexture);

            cs.Dispatch(kernelHandle, depthTexture.width / 8, depthTexture.height / 8, 1);

            RenderTexture.active = resultTexture;

            int cropSize = 736;
            int centerX = resultTexture.width / 2;
            int centerY = resultTexture.height / 2;
            int startX = centerX - (cropSize / 2);
            int startY = centerY - (cropSize / 2);

            Texture2D txt = new Texture2D(cropSize, cropSize, TextureFormat.RFloat, false);
            txt.ReadPixels(new Rect(startX, startY, cropSize, cropSize), 0, 0);
            txt.Apply();

            RenderTexture.active = null;
            // Fig// SaveTextureAsPNG(txt, "Depth.png");

            float DepthValue = txt.GetPixel(x_, y_).r;
            // float DepthValue = 1 - txt.GetPixel(x_, y_).r;
            float z_ = depthCamera.nearClipPlane / (1.0f - DepthValue * (1.0f - depthCamera.nearClipPlane / depthCamera.farClipPlane));
            
            resultTexture.Release();
            Object.Destroy(resultTexture);
            depthTexture.Release();
            Object.Destroy(depthTexture);
            Object.Destroy(txt);

            int a = (depthCamera.pixelWidth - 736) / 2;
            int b = (depthCamera.pixelHeight - 736) / 2;

            return depthCamera.ScreenToWorldPoint(new Vector3(x_ + a, y_ - b, z_));
        }

        public static GameObject GetTarget(GameObject Objects, float x, float y, float z)
        {
            Transform[] allChildren = Objects.GetComponentsInChildren<Transform>();
            GameObject closest = null;
            float minDist = float.MaxValue;

            foreach (Transform child in allChildren)
            {
                if (child == Objects.transform || !child.gameObject.activeInHierarchy)
                    continue;

                Vector3 pos = child.position;
                float distance = Vector3.Distance(pos, new Vector3(x, y, z));
                if (distance < minDist)
                {
                    minDist = distance;
                    closest = child.gameObject;
                }
            }
            return closest;
        }
        public static List<Texture2D> GetCrop2D(Texture2D inputTexture, float[,] output)
        {
            List<Texture2D> croppedTextures = new List<Texture2D>();

            int rows = output.GetLength(0); // YOLO 출력의 행 개수 (300)
            int cols = output.GetLength(1); // YOLO 출력의 열 개수 (6)

            int width = inputTexture.width;
            int height = inputTexture.height;

            // 1. 각 바운딩 박스 처리
            for (int i = 0; i < rows; i++)
            {
                if (output[i, 4] < 0.5f) // 신뢰도 임계값 설정
                    continue;
                Texture2D croppedTxt = new Texture2D(150, 150, TextureFormat.RGBA32, false);
                // YOLO 출력값에서 좌표 가져오기 (x1, y1, x2, y2)
                int x_center = Mathf.RoundToInt((output[i, 0] + output[i, 2]) / 2);
                int y_center = height - Mathf.RoundToInt((output[i, 1] + output[i, 3]) / 2);

                int x1 = Mathf.RoundToInt(output[i, 0]);
                int y1 = height - Mathf.RoundToInt(output[i, 1]);
                int x2 = Mathf.RoundToInt(output[i, 2]);
                int y2 = height - Mathf.RoundToInt(output[i, 3]);

                // x_center, y_center 기준으로 150x150 크롭 영역 계산
                int cropX1 = Mathf.Clamp(x_center - 75, 0, width - 150);
                int cropY1 = Mathf.Clamp(y_center - 75, 0, height - 150);
                croppedTxt.SetPixels(inputTexture.GetPixels(cropX1, cropY1, 150, 150));
                croppedTxt.Apply();
                croppedTextures.Add(croppedTxt);
            }
            return croppedTextures;
        }

        public static void SaveTextureAsPNG(Texture2D texture, string fileName)
        {
            byte[] bytes = texture.EncodeToPNG();
            string path = Path.Combine(Application.dataPath, fileName);
            File.WriteAllBytes(path, bytes);
        }
    }
}