using UnityEngine;
using Unity.InferenceEngine;
using System.IO;

namespace BinPickingAI
{
    public class YOLO : MonoBehaviour
    {
        public ModelAsset modelAsset;
        Model yoloModel;
        Worker worker;
        void Start()
        {
            yoloModel = ModelLoader.Load(modelAsset);
            worker = new Worker(yoloModel, BackendType.GPUCompute);
        }


        public float[,] YOLOv11(Texture2D inputTexture)
        {
            Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, inputTexture.height, inputTexture.width));
            TextureConverter.ToTensor(inputTexture, inputTensor);

            worker.Schedule(inputTensor);
            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
            var array = ConvertTo2DArray(outputTensor);

            inputTensor.Dispose();
            outputTensor.Dispose();

            return array;
        }
        
        public float[,] ConvertTo2DArray(Tensor<float> tensor)
        {
            int rows = tensor.shape[1]; // 첫 번째 차원 (300)
            int cols = tensor.shape[2]; // 두 번째 차원 (6)
            float[] flatArray = tensor.DownloadToArray(); // 1차원 배열로 다운로드

            float[,] result = new float[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = flatArray[i * cols + j];
                }
            }

            return result;
        }
    }

}
