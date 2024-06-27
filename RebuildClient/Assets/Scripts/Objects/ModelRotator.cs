using Assets.Scripts.MapEditor;
using RebuildSharedData.Extensions;
using UnityEngine;

namespace Objects
{
    public class ModelRotator : MonoBehaviour
    {
        public Quaternion Start;
        public Vector3 TargetRotation;
        public Vector3 OffsetInitialPosition = Vector3.zero;
        public float Speed = 1f;
        public float Countdown = 5f;
        public RoMapRenderSettings Settings;
        private float acc;
        private float angle;
        private float length;
        private Color startColor;
        

        public void Awake()
        {
            Start = transform.localRotation;

            var target = Quaternion.Euler(TargetRotation);
            angle = Quaternion.Angle(Start, target);
            length = angle * (1 / Speed);
            startColor = Settings.Diffuse;
        }

        public void Update()
        {
            Countdown -= Time.deltaTime;
            if (Countdown > 0)
                return;
            
            var target = Quaternion.Euler(TargetRotation);
            acc += Time.deltaTime * Speed;
            
            if(Countdown < (-length * 0.4f))
                Settings.Diffuse = Color.Lerp(startColor, new Color(0.7f, 0.5f, 0.4f), RemapExtension.Remap(Countdown, -length * 0.4f, -length * 0.7f, 0, 1f));
            if(Countdown < (-length * 0.7f))
                Settings.Diffuse = Color.Lerp(new Color(0.7f, 0.5f, 0.4f), new Color(0.05f, 0.18f, 0.37f), RemapExtension.Remap(Countdown, -length * 0.7f, -length * 1f, 0, 1f));
            if (Countdown < (-length * 0.5f))
                Settings.Opacity = Mathf.Lerp(0.5f, 0f, Countdown.Remap(-length * 0.5f, -length * 1f, 0f, 1f));
            
            transform.localRotation = Quaternion.RotateTowards(Start, target, acc);
            transform.localPosition = OffsetInitialPosition;
        }
    }
}