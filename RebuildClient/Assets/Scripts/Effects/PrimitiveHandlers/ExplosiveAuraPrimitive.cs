using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("ExplosiveAura", typeof(SimpleSpriteData))]
    public class ExplosiveAuraPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderPrimitive;

        public static void UpdatePrimitive(RagnarokPrimitive primitive)
        {
            //Ok so this effect is weird. Each part has 4 sparks, and there can be anywhere from 1-4 parts.
            //This effect doesn't line up well with the primitive data array, so there's some improvisation.
            //Progress, Alpha, Angle, and Height are stored in Heights array (4 floats per spark)
            //The texture used is stored in the flags array.

            if (!primitive.IsStepFrame)
                return;

            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];

                if (!p.Active)
                    continue;
                
                for (var i = 0; i < 4; i++)
                {
                    var s = i * 4; //spark #
                    p.Heights[s]++;
                    if (p.Heights[s] < 10) //-1 is alpha going up, 1 is alpha going down
                    {
                        p.Heights[s + 1] += 25; //increase alpha
                    }
                    else
                    {
                        p.Heights[s + 1] -= 12; //fade alpha
                        if (p.Heights[s + 1] < 12)
                        {
                            p.Heights[s] = 0; //reset progress
                            p.Heights[s + 1] = 0; //reset alpha
                            p.Heights[s + 2] = Random.Range(0, 360f); //reset angle
                            p.Heights[s + 3] = Random.Range(3, 8f); //reset height
                            p.Flags[i] = Random.Range(0, 5); //reset texture #
                        }
                    }
                }
            }
        }

        private static readonly string[] spriteNames = { "super1", "super2", "super3", "super4", "super5" };

        private void RenderPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<SimpleSpriteData>();
            var cameraAngle = CameraFollower.Instance.Rotation;
            const float size = 2.7f;
            const float dist = 3.5f;

            for (var ec = 0; ec < primitive.PartsCount; ec++)
            {
                var p = primitive.Parts[ec];
                for (var i = 0; i < 4; i++)
                {
                    var s = i * 4; //spark #
                    var spriteId = p.Flags[i];
                    var color = new Color(data.Color.r, data.Color.g, data.Color.b, Mathf.Clamp01(p.Heights[s + 1] / 255f));
                    var angle = p.Heights[s + 2];
                    var height = p.Heights[s + 3];
                    var sprite = EffectSharedMaterialManager.GetAtlasSprite(data.Atlas, spriteNames[spriteId]);
                    var spriteFlip = spriteId == 0 || spriteId == 2 ? -1 : 1; //sprites 0 and 2 face left, the rest face right

                    var offset = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * dist, height, Mathf.Cos(angle * Mathf.Deg2Rad) * dist);
                    var viewAngle = angle - cameraAngle; //the +20 just works with these sprites
                    if (viewAngle < 0)
                        viewAngle += 360;
                    
                    //Debug.Log($"Offset: {offset} ViewAngle {viewAngle} ({angle} - {cameraAngle})");

                    //no matter our view angle the sparks have to be angled away from the player
                    if (viewAngle > 180)
                        spriteFlip *= -1;

                    primitive.AddTexturedBillboardSprite(sprite, offset, size * spriteFlip, size, color);
                }
            }
        }
    }
}