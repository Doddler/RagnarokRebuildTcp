using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("CastTargetCircle")]
    public class CastTargetCircle : IEffectHandler
    {
        private static Material baseMaterial;
        private static Stack<Material> materialPool = new();
        private static Texture texture;
        private static Dictionary<string, Texture2D> castTextures = new();

        public static Ragnarok3dEffect CreateFriendly(Vector3 position, float size, float duration) =>
            Create("magic_target_grey", position, new Color(0.24f, 0.75f, 1f, 0.5f), size, duration);
        
        public static Ragnarok3dEffect CreateUnfriendly(Vector3 position, float size, float duration) =>
            Create("magic_target_bad", position, new Color(0.65f, 0f, 0.4f, 0.5f), size, duration);
        
        public static Ragnarok3dEffect Create(bool isAllied, Vector3 position, float size, float duration) => 
            isAllied ? CreateFriendly(position, size, duration) : CreateUnfriendly(position, size, duration);
        
        public static Ragnarok3dEffect Create(string textureName, Vector3 position, Color color, float size, float duration)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CastTargetCircle);

            

            if (baseMaterial == null)
            {
                baseMaterial = new Material(ShaderCache.Instance.ProjectorAdditiveShader);
                baseMaterial.color = Color.white;
            }

            if (!castTextures.TryGetValue(textureName, out var tex))
            {
                tex = Resources.Load<Texture2D>(textureName);
                castTextures.Add(textureName, tex);
            }

            if (!materialPool.TryPop(out var mat))
                mat = GameObject.Instantiate(baseMaterial);

            mat.color = color;
            mat.mainTexture = tex;
            mat.SetTexture("_ShadowTex", tex);

            effect.transform.localPosition = position + new Vector3(0f, 1.5f, 0f);
            effect.SetDurationByTime(duration);
            effect.UpdateOnlyOnFrameChange = false;
            effect.DestroyOnTargetLost = false;

            var prim = effect.LaunchPrimitive(PrimitiveType.ProjectorPrimitive, mat);
            prim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            prim.transform.localScale = new Vector3(size, size, size);
            prim.Duration = duration;
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            var primitives = effect.GetPrimitives;
            for (var i = 0; i < primitives.Count; i++)
            {
                var rot = primitives[i].gameObject.transform.localRotation.eulerAngles;
                primitives[i].gameObject.transform.localRotation = Quaternion.Euler(rot.x, rot.y + 170f * Time.deltaTime, rot.z);
            }
            
            return step < effect.DurationFrames;
        }

        public void OnCleanup(Ragnarok3dEffect effect)
        {
            var primitives = effect.GetPrimitives;
            if(primitives.Count > 0)
                materialPool.Push(primitives[0].Material);
        }
    }
}