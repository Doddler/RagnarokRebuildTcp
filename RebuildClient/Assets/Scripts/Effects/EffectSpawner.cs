using System;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Environment;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class EffectSpawner : MonoBehaviour
    {
        public string EffectTypeName;

        //this whole shit is set up this way because the EffectType enum list will change when adding new effects, so we store a string instead
        public EffectType EffectType
        {
            set => EffectTypeName = value.ToString();
        }

        public bool IsRemotelyManaged;
        public int Variant;
        public float Size;
        public Vector3 EffectCenterOffset;

        private bool isInitialized;
        private EffectType properType;

        private Ragnarok3dEffect activeEffect;

        public void Awake()
        {
            if (IsRemotelyManaged)
                return;
            
            if (NetworkManager.Instance == null)
                return;

            Activate();
        }

        public void Activate()
        {
            if (activeEffect != null)
                return;

            if (!isInitialized)
            {
                properType = Enum.Parse<EffectType>(EffectTypeName);
                isInitialized = true;
            }

            switch (properType)
            {
                case EffectType.BlueWaterfallEffect:
                    BlueWaterfallEffect.Create(Variant, transform.position);
                    break;
                case EffectType.ForestLightEffect:
                    activeEffect = ForestLightEffect.Create((ForestLightType)Variant, transform.position);
                    break;
                case EffectType.MapPillar:
                    var pillarSize = Variant == 1 ? 11f : 2f;
                    var matType = Variant switch
                    {
                        2 => EffectMaterialType.MapPillarGreen,
                        3 => EffectMaterialType.MapPillarRed,
                        _ => EffectMaterialType.MapPillarBlue,
                    };
                    activeEffect = MapPillarEffect.CreateJunoPillar(transform.position, pillarSize, matType);
                    break;
            }
        }

        public void Deactivate()
        {
            if (activeEffect == null)
                return;
            
            activeEffect.EndEffect();
            activeEffect = null;
        }
        
        
        private void OnDrawGizmos()
        {
            // Set the color with custom alpha.
            Gizmos.color = new Color(1f, 0f, 1f, 0.5f); // Red with custom alpha

            // Draw the sphere.
            Gizmos.DrawSphere(transform.position + EffectCenterOffset, Size);

            // Draw wire sphere outline.
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position + EffectCenterOffset, Size);
        }
    }
}