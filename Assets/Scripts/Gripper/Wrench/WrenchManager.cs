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
        private Dictionary<Collider, List<Vector3>> contactData = new Dictionary<Collider, List<Vector3>>();
        private Dictionary<Collider, List<Vector3>> normalData = new Dictionary<Collider, List<Vector3>>();
         /// <summary>

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
        public void UpdateContacts(Collider collider, List<Vector3> contacts)
        {
            if (contactData.ContainsKey(collider))
            {
                contactData[collider] = contacts;
            }
            else
            {
                contactData.Add(collider, contacts);
            }
        }
        public void UpdateNormals(Collider collider, List<Vector3> normals)
        {
            if (normalData.ContainsKey(collider))
            {
                normalData[collider] = normals;
            }
            else
            {
                normalData.Add(collider, normals);
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
        public List<Vector3> GetAllContacts()
        {
            List<Vector3> allContacts = new List<Vector3>();

            foreach (var contact in contactData.Values)
            {
                allContacts.AddRange(contact);
            }

            return allContacts;
        }
        public List<Vector3> GetAllNormals()
        {
            List<Vector3> allNormals = new List<Vector3>();

            foreach (var normal in normalData.Values)
            {
                allNormals.AddRange(normal);
            }

            return allNormals;
        }
        public void ClearAll()
        {
            forceData.Clear();
            momentData.Clear();
            normalData.Clear();
            contactData.Clear();
        }
    }
}