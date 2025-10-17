using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BinPickingAI
{
    public class WrenchManager : MonoBehaviour
    {
        // Dictionary to store Wrenches for each Collider
        private Dictionary<Collider, List<Vector3>> forceData = new Dictionary<Collider, List<Vector3>>();
        private Dictionary<Collider, List<Vector3>> momentData = new Dictionary<Collider, List<Vector3>>();

        /// <summary>
        /// Updates the wrenches for a specific collider.
        /// </summary>
        public void UpdateForces(Collider collider, List<Vector3> forces)
        {
            if (forceData.ContainsKey(collider))
            {
                forceData[collider] = forces;
            }
            else
            {
                forceData.Add(collider, forces);
            }
        }
        public void UpdateMoments(Collider collider, List<Vector3> moments)
        {
            if (momentData.ContainsKey(collider))
            {
                momentData[collider] = moments;
            }
            else
            {
                momentData.Add(collider, moments);
            }
        }
        /// <summary>
        /// Retrieves the wrenches for a specific collider.
        /// </summary>
        public List<Vector3> GetAllForces()
        {
            List<Vector3> allForces = new List<Vector3>();

            foreach (var force in forceData.Values)
            {
                allForces.AddRange(force);
            }

            return allForces;
        }
        public List<Vector3> GetAllMoments()
        {
            List<Vector3> allMoments = new List<Vector3>();

            foreach (var moment in momentData.Values)
            {
                allMoments.AddRange(moment);
            }

            return allMoments;
        }
        public void ClearAll()
        {
            forceData.Clear();
            momentData.Clear();
        }
    }
}