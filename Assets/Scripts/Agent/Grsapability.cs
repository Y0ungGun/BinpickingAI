using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using System.Linq;

namespace BinPickingAI
{
    public class Graspability : MonoBehaviour
    {
        public ModelAsset EncoderPath;
        public ModelAsset HeadPath;
        Model ModelEncoder;
        Model ModelHead;
        Model yoloModel;
        Worker WorkerEncoder;
        Worker WorkerHead;
        void Start()
        {
            ModelEncoder = ModelLoader.Load(EncoderPath);
            WorkerEncoder = new Worker(ModelEncoder, BackendType.GPUCompute);
        }

        public float[] GraspabilityInf(List<Texture2D> inputTextures)
        {
            ModelHead = ModelLoader.Load(HeadPath);
            WorkerHead = new Worker(ModelHead, BackendType.GPUCompute);

            List<float[]> features = new List<float[]>();
            float[] results = new float[inputTextures.Count];
            Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, inputTextures[0].height, inputTextures[0].width));

            for (int i = 0; i < inputTextures.Count; i++)
            {
                TextureConverter.ToTensor(inputTextures[i], inputTensor);
                WorkerEncoder.Schedule(inputTensor);
                Tensor<float> FeatureTensor = WorkerEncoder.PeekOutput() as Tensor<float>;

                WorkerHead.Schedule(FeatureTensor);
                Tensor<float> GraspabilityTensor = WorkerHead.PeekOutput() as Tensor<float>;

                float[] FeatureArray = FeatureTensor.DownloadToArray();
                float[] Graspability = GraspabilityTensor.DownloadToArray();
                features.Add(FeatureArray);
                results[i] = Graspability[0];

                GraspabilityTensor.Dispose();
                FeatureTensor.Dispose();
            }
            float[] returns = new float[258];

            int bestId = System.Array.IndexOf(results, results.Max());
            returns[0] = bestId;
            returns[1] = results[bestId];
            for (int j = 0; j < features[bestId].Length; j++)
            {
                returns[j + 2] = features[bestId][j];
            }

            inputTensor.Dispose();
            WorkerHead.Dispose();

            return returns;
        }

    }
}