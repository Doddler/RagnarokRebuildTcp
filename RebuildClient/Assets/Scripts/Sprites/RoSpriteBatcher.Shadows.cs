using System.Collections.Generic;
using Assets.Scripts.Sprites;
using Unity.Collections;
using UnityEngine;

public partial class RoSpriteBatcher
{
    private struct ShadowQuery
    {
        public RoSpriteAnimator Animator;
        public Vector3 Origin;
        public float ShadeLevel;
    }

    private static readonly List<ShadowQuery> _shadowQueries = new();
    private static Light _shadowDirLight;
    private static int _shadowMask;

    private NativeArray<RaycastCommand> _raycastCommands;
    private NativeArray<RaycastHit> _raycastResults;
    private int _raycastCapacity;

    public static void QueueShadowRaycast(RoSpriteAnimator animator, Vector3 origin, float shadeLevel)
    {
        if (!animator) return;
        _shadowQueries.Add(new ShadowQuery
        {
            Animator = animator,
            Origin = origin,
            ShadeLevel = shadeLevel,
        });
    }

    private void ProcessShadowRaycasts()
    {
        int count = _shadowQueries.Count;
        if (count == 0) return;

        if (!_shadowDirLight)
        {
            var dl = GameObject.Find("DirectionalLight");
            if (dl) _shadowDirLight = dl.GetComponent<Light>();
            if (!_shadowDirLight)
            {
                _shadowQueries.Clear();
                return;
            }
        }

        if (_shadowMask == 0)
            _shadowMask = LayerMask.GetMask("Ground", "Object");

        var sunDir = _shadowDirLight.transform.rotation * Vector3.forward * -1f;
        var queryParams = new QueryParameters(_shadowMask, false, QueryTriggerInteraction.UseGlobal, Physics.queriesHitBackfaces);

        EnsureRaycastCapacity(count);

        for (int i = 0; i < count; i++)
        {
            var q = _shadowQueries[i];
            _raycastCommands[i] = new RaycastCommand(q.Origin, sunDir, queryParams, 50f);
        }

        var commands = _raycastCommands.GetSubArray(0, count);
        var results = _raycastResults.GetSubArray(0, count);
        var handle = RaycastCommand.ScheduleBatch(commands, results, 64);
        handle.Complete();

        for (int i = 0; i < count; i++)
        {
            var q = _shadowQueries[i];
            if (!q.Animator) continue;
            q.Animator.TargetShade = _raycastResults[i].colliderInstanceID != 0 ? q.ShadeLevel : 1f;
        }

        _shadowQueries.Clear();
    }

    private void EnsureRaycastCapacity(int count)
    {
        if (_raycastCapacity >= count) return;
        if (_raycastCommands.IsCreated) _raycastCommands.Dispose();
        if (_raycastResults.IsCreated) _raycastResults.Dispose();
        _raycastCapacity = Mathf.NextPowerOfTwo(count);
        _raycastCommands = new NativeArray<RaycastCommand>(_raycastCapacity, Allocator.Persistent);
        _raycastResults = new NativeArray<RaycastHit>(_raycastCapacity, Allocator.Persistent);
    }

    private void DisposeShadowRaycasts()
    {
        if (_raycastCommands.IsCreated) _raycastCommands.Dispose();
        if (_raycastResults.IsCreated) _raycastResults.Dispose();
        _shadowQueries.Clear();
        _raycastCapacity = 0;
    }
}
