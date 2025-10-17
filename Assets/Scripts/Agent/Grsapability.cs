using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;

namespace BinPickingAI
{
    public class Graspability : MonoBehaviour
    {
        public ModelAsset modelAsset;
        Model yoloModel;
        Worker worker;
        void Start()
        {
        }

        public float[] GraspabilityInf(List<Texture2D> inputTextures)
        {
            float[] results = new float[inputTextures.Count];
            yoloModel = ModelLoader.Load(modelAsset);
            worker = new Worker(yoloModel, BackendType.GPUCompute);
            Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, inputTextures[0].height, inputTextures[0].width));

            for (int i = 0; i < inputTextures.Count; i++)
            {
                TextureConverter.ToTensor(inputTextures[i], inputTensor);
                worker.Schedule(inputTensor);
                Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
                float[] outputArray = outputTensor.DownloadToArray();
                results[i] = outputArray[0];
                outputTensor.Dispose();
            }
            inputTensor.Dispose();
            worker.Dispose();

            return results;
        }

    }
}