using MIConvexHull;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;

namespace BinPickingAI
{
    public class WrenchConvexHull : MonoBehaviour
    {
        public WrenchManager wrenchManager;
        public TargetContact targetContact;

        private void Start()
        {
            
        }
        //private void Update()
        //{
        //    if (targetContact != null)
        //    {
        //        GenerateWrenchConvexHull();
        //    }

        //}
        public float GetEpsilon()
        {
            List<Vector3> Forces = wrenchManager.GetAllForces();
            List<Vector3> Moments = wrenchManager.GetAllMoments();
            // 캐시된 wrench 리스트를 TargetContact에서 받아옴
            List<Wrench> Wrenches = new List<Wrench>();
            for (int i = 0; i < Forces.Count; i++)
            {
                Wrenches.Add(new Wrench(Forces[i], Moments[i]));
            }
            // --- 입력 좌표 반올림 및 정렬 ---
            Wrenches = Wrenches
                .Select(w => new Wrench(
                    new Vector3(
                        (float)Math.Round(w.Position[0], 6),
                        (float)Math.Round(w.Position[1], 6),
                        (float)Math.Round(w.Position[2], 6)),
                    new Vector3(
                        (float)Math.Round(w.Position[3], 6),
                        (float)Math.Round(w.Position[4], 6),
                        (float)Math.Round(w.Position[5], 6))))
                .OrderBy(w => string.Join("_", w.Position.Select(x => x.ToString("G6"))))
                .ToList();

            // Convex Hull 계산 및 epsilon 구하기
            var convexHull = ConvexHull.Create(Wrenches);
            float epsilon = 0f;
            if (convexHull.ErrorMessage == "")
            {
                double eps = CalculateEpsilon(convexHull);
                epsilon = (float)eps;
                //Debug.Log($"Epsilon, Radius:{eps}, {epsilon}");
            }

            return epsilon;
        }
        public void GenerateWrenchConvexHull()
        {
            List<Vector3> Forces = wrenchManager.GetAllForces();
            List<Vector3> Moments = wrenchManager.GetAllMoments();
            // 캐시된 wrench 리스트를 TargetContact에서 받아옴
            List<Wrench> Wrenches = new List<Wrench>();
            for (int i = 0; i < Forces.Count; i++)
            {
                Wrenches.Add(new Wrench(Forces[i], Moments[i]));
            }
            // --- 입력 좌표 반올림 및 정렬 ---
            Wrenches = Wrenches
                .Select(w => new Wrench(
                    new Vector3(
                        (float)Math.Round(w.Position[0], 6),
                        (float)Math.Round(w.Position[1], 6),
                        (float)Math.Round(w.Position[2], 6)),
                    new Vector3(
                        (float)Math.Round(w.Position[3], 6),
                        (float)Math.Round(w.Position[4], 6),
                        (float)Math.Round(w.Position[5], 6))))
                .OrderBy(w => string.Join("_", w.Position.Select(x => x.ToString("G6"))))
                .ToList();

            // Convex Hull 계산 및 epsilon 구하기
            var convexHull = ConvexHull.Create(Wrenches);
            float epsilon = 0f;
            if (convexHull.ErrorMessage == "")
            {
                double eps = CalculateEpsilon(convexHull);
                epsilon = (float)eps * 5f;
                Debug.Log($"Epsilon, Radius:{eps}, {epsilon}");
            }
        }

        private static double CalculateEpsilon(ConvexHullCreationResult<Wrench, DefaultConvexFace<Wrench>> hullResult)
        {
            double epsilon = double.MaxValue;

            // Convex Hull의 각 face에 대해 Chebyshev 반지름 계산
            foreach (var face in hullResult.Result.Faces)
            {
                var normal = CalculateNormal(face.Vertices.Select(v => v.Position).ToList());
                var samplePoint = face.Vertices.First().Position;
                double b = VectorDot(normal, samplePoint);

                double distance = Math.Abs(b / VectorNorm(normal));

                epsilon = Math.Min(epsilon, distance);
            }

            return epsilon;
        }


        private static double[] CalculateNormal(List<double[]> vertices)
        {
            var mat = Matrix<double>.Build.Dense(vertices.Count, 6);
            for (int i = 0; i < vertices.Count; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    mat[i, j] = vertices[i][j];
                }
            }

            var origin = mat.Row(0);
            for (int i = 0; i < mat.RowCount; i++)
                mat.SetRow(i, mat.Row(i) - origin);

            var svd = mat.Svd(true);
            Vector<double> normal = svd.VT.Row(svd.VT.RowCount - 1);

            return normal.ToArray();
        }

        private static double VectorNorm(double[] vector)
        {
            return Math.Sqrt(vector.Select(x => x * x).Sum());
        }

        private static double VectorDot(double[] v1, double[] v2)
        {
            return v1.Zip(v2, (x, y) => x * y).Sum();
        }
        public void SetTargetContact(GameObject target)
        {
            targetContact = target.GetComponent<TargetContact>();
        }
        public void ClearWrench()
        {
            wrenchManager.ClearAll();
        }
    }
}

public class Wrench : IVertex
{
    public double[] Position { get; private set; }

    public Wrench(Vector3 force, Vector3 moment)
    {
        Position = new double[] { force.x, force.y, force.z, moment.x, moment.y, moment.z };
    }
}