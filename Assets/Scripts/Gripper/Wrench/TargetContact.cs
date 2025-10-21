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
        public bool CollectWrench = false;

        public WrenchManager wc;   

        private List<Vector3> debugContactPoints = new List<Vector3>();
        private List<Vector3> debugNormals = new List<Vector3>();
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }
        public void SetCollector(WrenchManager collector)
        {
            wc = collector;
        }
        private void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.name.Contains("Finger") && wc != null && CollectWrench)
            {
                // 충돌에 사용된 collider의 크기로 lambda 갱신
                isContact = true;
                
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
                    debugContactPoints.Add(contactPoint);
                    debugNormals.Add(contact.normal);

                    Vector3 centerOfMass = rb.worldCenterOfMass;
                    Vector3 NormalForceVector = contact.normal;
                    float FrictionForce = 0.5f;

                    for (int k = 0; k < n; k++)
                    {
                        float angle = 2 * Mathf.PI / n * k;
                        Vector3 tangent = Vector3.Cross(contact.normal, Vector3.up).normalized;
                        Vector3 bitangent = Vector3.Cross(contact.normal, tangent).normalized;
                        Vector3 FrictionForceVector = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * FrictionForce;
                        Vector3 force = FrictionForceVector + NormalForceVector;
                        Vector3 moment = Vector3.Cross(contactPoint - centerOfMass, force) / (contactPoint - centerOfMass).magnitude;
                        Vector6 Wrench = new Vector6(force, moment);
                        Wrenches.Add(Wrench);
                        forces.Add(force);
                        moments.Add(moment);
                    }
                }
                if (debugContactPoints == null)
                {
                    Debug.Log("No contact points collected.");
                }
                wc.UpdateForces(collision.collider, forces);
                wc.UpdateMoments(collision.collider, moments);
                wc.UpdateContacts(collision.collider, debugContactPoints);
                wc.UpdateNormals(collision.collider, debugNormals);
                debugContactPoints.Clear();
                debugNormals.Clear();
            }
        }

        // private void OnCollisionStay(Collision collision)
        // {
        //     if (collision.gameObject.name.Contains("Finger") && wc != null && CollectWrench)
        //     {
        //         isContact = true;
                
        //         List<Vector6> Wrenches = new List<Vector6>();
        //         List<Vector3> forces = new List<Vector3>();
        //         List<Vector3> moments = new List<Vector3>();
        //         float minDist = 0.001f; 

        //         foreach (ContactPoint contact in collision.contacts)
        //         {
        //             Vector3 contactPoint = contact.point;
        //             bool isDuplicate = false;
        //             foreach (var prev in forces)
        //             {
        //                 if ((prev - contactPoint).sqrMagnitude < minDist * minDist)
        //                 {
        //                     isDuplicate = true;
        //                     break;
        //                 }
        //             }
        //             if (isDuplicate) continue;

        //             // ✅ Raycast를 사용하여 정확한 표면 법선 가져오기
        //             Vector3 rayOrigin = contactPoint; // 충돌 지점에서 약간 떨어진 위치
        //             Vector3 rayDirection = -contact.normal; // 충돌 표면 방향으로 Raycast
        //             RaycastHit hit;
        //             // Vector3 surfaceNormal = contact.normal; // 기본값으로 contact.normal 사용
        //             Vector3 surfaceNormal = contact.normal; // 기본값으로 contact.normal 사용
        //             Physics.Raycast(rayOrigin, rayDirection, out hit, 0.02f, ~0, QueryTriggerInteraction.Ignore);
        //             if (hit.articulationBody)
        //             {
        //                 surfaceNormal = hit.normal; // RaycastHit.normal로 대체
        //                 contactPoint = hit.point;
        //             }
        //             else
        //             {
        //                 continue;
        //             }

        //             Vector3 centerOfMass = rb.worldCenterOfMass;
        //             float NormalForce = 1;
        //             Vector3 NormalForceVector = surfaceNormal; // * NormalForce;
        //             float FrictionForce = 0.5f * NormalForce;
                    
        //             for (int k = 0; k < n; k++)
        //             {
        //                 float angle = 2 * Mathf.PI / n * k;
        //                 Vector3 tangent = Vector3.Cross(surfaceNormal, Vector3.up).normalized;
        //                 Vector3 bitangent = Vector3.Cross(surfaceNormal, tangent).normalized;
        //                 Vector3 FrictionForceVector = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * FrictionForce;
        //                 Vector3 force = FrictionForceVector + NormalForceVector;
        //                 Vector3 moment = Vector3.Cross(contactPoint - centerOfMass, force) / (contactPoint - centerOfMass).magnitude;
        //                 Vector6 Wrench = new Vector6(force, moment);
        //                 Wrenches.Add(Wrench);
        //                 forces.Add(force);
        //                 moments.Add(moment);
        //             }
        //         }
        //         wc.UpdateForces(collision.collider, forces);
        //         wc.UpdateMoments(collision.collider, moments);
        //     }
        // }

        private void OnCollisionExit(Collision collision)
        {
            isContact = false;
        }
    }
}
