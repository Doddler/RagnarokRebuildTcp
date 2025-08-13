using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("ProjectorPrimitive")]
    public class ProjectorPrimitive : IPrimitiveHandler
    {
        public void Init(RagnarokPrimitive primitive)
        {
            var go = primitive.gameObject;
            var projector = go.AddComponent<Projector>();

            projector.material = primitive.Material;
            projector.orthographic = true;
            projector.nearClipPlane = 0.1f;
            projector.farClipPlane = 3f;
            projector.orthographicSize = go.transform.localScale.x; //yeah

            primitive.GetComponent<MeshRenderer>().enabled = false;
            primitive.SetDisposableComponent(projector);
        }

    }
}