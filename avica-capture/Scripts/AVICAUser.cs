using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AVICA.Capture
{
    public class AVICAUser : MonoBehaviour
    {
        public string UserId { get; private set; }
        public Transform proxyTransform;

        [Header("Complex Visibility Check")]
        [Tooltip("If enabled, an additional raycast from the camera to the player will be done to check to see if they're visible")]
        public bool useComplexVisibilityCheck = true;

        [Tooltip("The layers that will be treated as obstructions, do not add your player mask to this list")]
        public LayerMask layerMask;

        [Tooltip("Should the complex visibility raycast collide with triggers?")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        private void OnEnable()
        {
            AVICAManager.Instance.RegisterUser(this);
        }

        private void OnDisable()
        {
            if(AVICAManager.Instance != null)
                AVICAManager.Instance.UnregisterUser(this);
        }

        public void SetUserId(string id)
        {
            UserId = id;
            Debug.Log("[AVICA] User ID " + id + " set for " + gameObject.name);
        }

        public bool IsVisibleOnCamera(Camera cam)
        {
            Transform t = proxyTransform != null ? proxyTransform : transform;

            Vector3 pointOnCamera = cam.WorldToViewportPoint(t.position);

            if (pointOnCamera.z < 0f)
                return false; // Behind the camera

            if ((pointOnCamera.x < 0f) || (pointOnCamera.x > cam.pixelWidth))
                return false; // Off screen horizontally

            if ((pointOnCamera.y < 0f) || (pointOnCamera.y > cam.pixelHeight))
                return false; // Off screen vertically

            if (useComplexVisibilityCheck && Physics.Raycast(new Ray(cam.transform.position, t.position - cam.transform.position), (t.position - cam.transform.position).magnitude - 0.1f, layerMask, triggerInteraction))
                return false; // Something is in the way

            return true;
        }
    }
}