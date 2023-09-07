using System;
using UnityEngine;

namespace Objects
{
    public class ModelRotator : MonoBehaviour
    {
        public float RotationSpeed;
        public Vector3 OffsetInitialPosition = Vector3.zero;

        public void Awake()
        {
            
        }

        public void Update()
        {
            var last = transform.localRotation.eulerAngles;
            
            transform.localRotation = Quaternion.Euler(last.x, last.y + Time.deltaTime * 360 * RotationSpeed, last.z);
            transform.localPosition = OffsetInitialPosition;
        }
    }
}