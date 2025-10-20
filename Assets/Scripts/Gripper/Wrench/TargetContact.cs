using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BinPickingAI
{

    public class TargetContact : MonoBehaviour
    {
        public int n = 8;
        public bool isContact = false;
        private Rigidbody rb;
        private float lambda = 1.0f; // 특성 길이(초기값 1.0, Start에서 자동 계산)
        public bool CollectWrench = false;

        public WrenchManager wc;   
        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            // lambda를 물체의 bounding box 대각선으로 자동 설정
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                lambda = 1 / col.bounds.size.magnitude;
            }
        }
        public void SetCollector(WrenchManager collector)
        {
            wc = collector;
        }
        private void Update()
        {
        }
        
        private void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.name.Contains("Finger") && wc != null && CollectWrench)
            {
                // 충돌에 사용된 collider의 크기로 lambda 갱신
                lambda = 1 / collision.collider.bounds.size.magnitude;
                isContact = true;
                Vector3 Impulse = collision.impulse;
                float deltaTime = Time.fixedDeltaTime;
                Vector3 Force = Impulse;
                Vector3 PointForce = Force;
                List<Vector6> Wrenches = new List<Vector6>();
                List<Vector3> forces = new List<Vector3>();
                List<Vector3> moments = new List<Vector3>();
                float minDist = 0.001f; // 1mm
                float now = Time.time;
                foreach (ContactPoint contact in collision.contacts)
                {
                    Vector3 contactPoint = contact.point;
                    bool isDuplicate = false;
                    foreach (var prev in forces)
                    {
                        if ((prev - contactPoint).sqrMagnitude < minDist * minDist)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    if (isDuplicate) continue;

                    Vector3 centerOfMass = rb.worldCenterOfMass;
                    float NormalForce = Vector3.Dot(PointForce, contact.normal);
                    NormalForce = 1;
                    Vector3 NormalForceVector = contact.normal; // * NormalForce;
                    float FrictionForce = 0.5f * NormalForce;

                    for (int k = 0; k < n; k++)
                    {
                        float angle = (2 * Mathf.PI / n) * k;
                        Vector3 tangent = Vector3.Cross(contact.normal, Vector3.up).normalized;
                        Vector3 bitangent = Vector3.Cross(contact.normal, tangent).normalized;
                        Vector3 FrictionForceVector = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * FrictionForce;
                        Vector3 force = FrictionForceVector + NormalForceVector;
                        Vector3 moment = Vector3.Cross(contactPoint - centerOfMass, force) * lambda; // lambda로 곱함
                        Vector6 Wrench = new Vector6(force, moment);
                        Wrenches.Add(Wrench);
                        forces.Add(force);
                        moments.Add(moment);
                    }
                }
                wc.UpdateForces(collision.collider, forces);
                wc.UpdateMoments(collision.collider, moments);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            isContact = false;
        }
    }
}
