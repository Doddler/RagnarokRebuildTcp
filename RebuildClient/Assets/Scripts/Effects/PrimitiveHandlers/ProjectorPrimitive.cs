using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("ProjectorPrimitive")]
    public class ProjectorPrimitive : IPrimitiveHandler
    {
        public void Init(RagnarokPrimitive primitive)
        {
            var go = primitive.gameObject;
            var decal = go.AddComponent<DecalProjector>();

            decal.material = primitive.Material;
            decal.pivot = Vector3.zero;
            decal.scaleMode = DecalScaleMode.InheritFromHierarchy;
            decal.size = new Vector3(2f, 2f, 6f);

            primitive.GetComponent<MeshRenderer>().enabled = false;
            primitive.SetDisposableComponent(decal);
        }
    }
}
