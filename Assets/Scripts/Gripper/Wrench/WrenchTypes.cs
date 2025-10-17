using MIConvexHull;
using UnityEngine;

namespace BinPickingAI
{
    // 6D Wrench 벡터
    public class Vector6
    {
        public float fx, fy, fz;
        public float mx, my, mz;
        public Vector3 Force;
        public Vector3 Moment;
        public Vector6(float fx, float fy, float fz, float mx, float my, float mz)
        {
            this.fx = fx; this.fy = fy; this.fz = fz;
            this.mx = mx; this.my = my; this.mz = mz;
            Force = new Vector3(fx, fy, fz);
            Moment = new Vector3(mx, my, mz);
        }
        public Vector6(Vector3 force, Vector3 moment)
        {
            Force = force;
            Moment = moment;
            fx = force.x; fy = force.y; fz = force.z;
            mx = moment.x; my = moment.y; mz = moment.z;
        }
        public float[] ToArray() => new float[] { fx, fy, fz, mx, my, mz };
        public override string ToString() => $"Force: {Force}, Moment: {Moment}";
    }

    // MIConvexHull용 ForceVertex
    public class ForceVertex : IVertex
    {
        public double[] Position { get; set; }
        public ForceVertex(Vector3 vector)
        {
            Position = new double[] { vector.x, vector.y, vector.z };
        }
    }

    // MIConvexHull용 6D Wrench
    public class Wrench : IVertex
    {
        public double[] Position { get; private set; }
        public Wrench(Vector3 force, Vector3 moment)
        {
            Position = new double[] { force.x, force.y, force.z, moment.x, moment.y, moment.z };
        }
    }
}
