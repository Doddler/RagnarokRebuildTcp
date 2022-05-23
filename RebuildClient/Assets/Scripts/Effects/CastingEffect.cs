using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Utility;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Effects
{
    class CastingEffect : MonoBehaviour
    {
        public static Dictionary<string, Material> CastMaterials = new Dictionary<string, Material>();
        public Material CastMaterial;
        public GameObject FollowTarget;
        public float Duration;

        private PrimitiveCylinderEffect prim;
        
        public static void StartCasting(float duration, string texture, GameObject followTarget)
        {
            Debug.Log("Start casting animation");

            var go = new GameObject("CastingEffect");
            var cast = go.AddComponent<CastingEffect>();

            if (CastMaterials.ContainsKey(texture))
            {
                cast.CastMaterial = CastMaterials[texture];
            }
            else
            {
                cast.CastMaterial = new Material(Shader.Find("Mobile/Particles/Additive"));
                cast.CastMaterial.mainTexture = Resources.Load<Texture2D>(texture);
                CastMaterials.Add(texture, cast.CastMaterial);
            }

            cast.FollowTarget = followTarget;

            //Time.timeScale = 0.2f;

            cast.Duration = duration;
            cast.Init();
        }
        
        public void Init()
        {
            prim = PrimitiveCylinderEffect.LaunchEffect(gameObject, CastMaterial, 4, Duration);
            prim.Updater = prim.Update3DCasting;
            prim.Renderer = prim.Render3DCasting;

            prim.FollowEntity(FollowTarget);

            transform.localScale = new Vector3(2f, 2f, 2f);
            


            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 315,
                MaxHeight = 25,
                Angle = 0,
                Alpha = 180,
                Distance = 4.5f, //4.5f,
                RiseAngle = 70
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 315,
                MaxHeight = 22,
                Angle = 90,
                Alpha = 180,
                Distance = 4.5f, //5f,
                RiseAngle = 57
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 315,
                MaxHeight = 19,
                Angle = 45,
                Alpha = 180,
                Distance = 4.5f, //5.5f,
                RiseAngle = 45
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 360,
                MaxHeight = 250,
                Angle = Random.Range(0f, 360f),
                Alpha = 70,
                Distance = 4f, //4f,
                RiseAngle = 89
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.PartCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 0;
                }
            }
        }

    }
}
