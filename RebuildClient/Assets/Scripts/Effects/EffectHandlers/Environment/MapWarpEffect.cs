﻿using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Effects.PrimitiveHandlers;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("MapWarpEffect")]
    public class MapWarpEffect : IEffectHandler
    {
        public static Material Ring1Material;
        public static Material Ring2Material;
        public static Material CircleMaterial;
        
        public static Ragnarok3dEffect StartWarp(GameObject parent)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.MapWarpEffect);
            effect.SetDurationByTime(float.MaxValue);
            effect.FollowTarget = parent;
            effect.DestroyOnTargetLost = true;
            effect.PositionOffset = new Vector3(0, 0.05f, 0f);
            
            if (Ring1Material == null)
            {
                Ring1Material = new Material(ShaderCache.Instance.AdditiveShader);
                Ring1Material.mainTexture = Resources.Load<Texture2D>("ring_blue");
                Ring1Material.color = new Color(170/255f, 170/255f, 1f, 1f);
                Ring1Material.renderQueue = 3001;
            }

            if (Ring2Material == null)
            {
                Ring2Material = new Material(ShaderCache.Instance.AdditiveShader);
                Ring2Material.mainTexture = Resources.Load<Texture2D>("ring_blue");
                Ring2Material.color = new Color(100 / 255f, 100 / 255f, 1f, 1f);
                Ring2Material.renderQueue = 3001;
            }

            if (CircleMaterial == null)
            {
                CircleMaterial = new Material(ShaderCache.Instance.AlphaBlendParticleShader);
                CircleMaterial.mainTexture = Resources.Load<Texture2D>("alpha_down");
                CircleMaterial.renderQueue = 3001;
                //CircleMaterial.color = new Color(1f, 1f, 1f, 1f);
            }

            
            var angle = 60;

            var prim = effect.LaunchPrimitive(PrimitiveType.Casting3D, Ring1Material, float.MaxValue);
            prim.CreateParts(4);
            prim.transform.localScale = new Vector3(2f, 2f, 2f);
            
            prim.UpdateHandler = CastingCylinderPrimitive.Update3DCasting4;
            prim.RenderHandler = CastingCylinderPrimitive.Render3DCasting;

            for (var i = 0; i < 4; i++)
            {
                prim.Parts[i].Active = true;
                prim.Parts[i].Step = 0;
                prim.Parts[i].CoverAngle = 360;
                prim.Parts[i].Alpha = 0;
            }
            
            prim.Parts[0].MaxHeight = 2.5f;
            prim.Parts[0].Angle = 270;
            prim.Parts[0].Distance = 2.5f;
            prim.Parts[0].RiseAngle = angle - 7;
            
            prim.Parts[1].MaxHeight = 5f;
            prim.Parts[1].Angle = 0;
            prim.Parts[1].Distance = 5f;
            prim.Parts[1].RiseAngle = angle;
            
            prim.Parts[2].MaxHeight = 7.5f;
            prim.Parts[2].Angle = 90;
            prim.Parts[2].Distance = 7.5f;
            prim.Parts[2].RiseAngle = angle - 5;
            
            prim.Parts[3].MaxHeight = 10f;
            prim.Parts[3].Angle = 180;
            prim.Parts[3].Distance = 10f;
            prim.Parts[3].RiseAngle = angle - 10;
    
            var prim2 = effect.LaunchPrimitive(PrimitiveType.Casting3D, Ring2Material, float.MaxValue);
            prim2.CreateParts(4);
            prim2.transform.localScale = new Vector3(2f, 2f, 2f);
            prim2.DelayTime = 1 / 60f;
            
            prim2.UpdateHandler = CastingCylinderPrimitive.Update3DCasting4;
            // prim2.RenderHandler = CastingCylinderPrimitive.Render3DCasting;
            
            for (var i = 0; i < 4; i++)
            {
                prim2.Parts[i].Active = true;
                prim2.Parts[i].Step = 0;
                prim2.Parts[i].CoverAngle = 360;
                prim2.Parts[i].Alpha = 0;
            }
            
            prim2.Parts[0].MaxHeight = 2.7f;
            prim2.Parts[0].Angle = 271;
            prim2.Parts[0].Distance = 2.7f;
            prim2.Parts[0].RiseAngle = angle - 8;
            
            prim2.Parts[1].MaxHeight = 5.2f;
            prim2.Parts[1].Angle = 1;
            prim2.Parts[1].Distance = 5.2f;
            prim2.Parts[1].RiseAngle = angle - 1;
            
            prim2.Parts[2].MaxHeight = 7.7f;
            prim2.Parts[2].Angle = 91;
            prim2.Parts[2].Distance = 7.7f;
            prim2.Parts[2].RiseAngle = angle - 6;
            
            prim2.Parts[3].MaxHeight = 10.2f;
            prim2.Parts[3].Angle = 181;
            prim2.Parts[3].Distance = 10.2f;
            prim2.Parts[3].RiseAngle = angle - 11;
            
            var circle = effect.LaunchPrimitive(PrimitiveType.Circle, CircleMaterial, float.MaxValue);
            circle.DelayTime = 2 / 60f;
            circle.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            circle.transform.localPosition += new Vector3(0f, 0.1f, 0f);
            circle.Duration = float.MaxValue;

            var cData = circle.GetPrimitiveData<CircleData>();

            cData.Radius = 15;
            cData.Alpha = 0;
            cData.MaxAlpha = 96;
            cData.AlphaSpeed = cData.MaxAlpha * 6f; //should reach max in 10/60 frames
            cData.ArcAngle = 36f;
            cData.FillCircle = true;

            var particles = GameObject.Instantiate(Resources.Load<GameObject>("PortalParticles"));
            effect.AttachChildObject(particles);

            return effect;
        }
    }
}