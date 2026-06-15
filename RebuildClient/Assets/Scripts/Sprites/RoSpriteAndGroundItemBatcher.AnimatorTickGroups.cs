using System.Collections.Generic;
using Assets.Scripts.Sprites;
using UnityEngine;

public partial class RoSpriteAndGroundItemBatcher
{
    public bool EnableTickGroups = true;
    [HideInInspector] public int TickThreshold = 200;
    [HideInInspector] public int TickMaxGroups = 6;
    [HideInInspector] public float TickHotRadius = 15f;

    private static readonly List<RoSpriteAnimator> _registeredAnimators = new();

    private int _animatorCamPosFrame = -1;
    private Vector3 _animatorCamPos;

    public static void RegisterAnimator(RoSpriteAnimator a)
    {
        if (!a) return;
        a.GroupIndex = _registeredAnimators.Count;
        _registeredAnimators.Add(a);
    }

    public static void UnregisterAnimator(RoSpriteAnimator a)
    {
        if (!a) return;
        int idx = a.GroupIndex;
        if (idx < 0 || idx >= _registeredAnimators.Count || _registeredAnimators[idx] != a)
            idx = _registeredAnimators.IndexOf(a);
        if (idx < 0) return;
        int last = _registeredAnimators.Count - 1;
        if (idx != last)
        {
            _registeredAnimators[idx] = _registeredAnimators[last];
            _registeredAnimators[idx].GroupIndex = idx;
        }
        _registeredAnimators.RemoveAt(last);
        a.GroupIndex = -1;
    }

    public bool ShouldTickAnimator(RoSpriteAnimator a)
    {
        if (!EnableTickGroups) return true;
        if (_registeredAnimators.Count <= TickThreshold) return true;
        if (a.GroupIndex < 0) return true;
        if (a.NeedsImmediateTick || a.SetAsDirty) return true;

        int groups = Mathf.Clamp(Mathf.CeilToInt(_registeredAnimators.Count / (float)TickThreshold), 1, TickMaxGroups);
        if (groups <= 1) return true;
        if ((a.GroupIndex % groups) == (Time.frameCount % groups)) return true;

        if (TickHotRadius > 0f)
        {
            if (_animatorCamPosFrame != Time.frameCount)
            {
                _animatorCamPosFrame = Time.frameCount;
                var cam = Camera.main;
                _animatorCamPos = cam ? cam.transform.position : Vector3.zero;
            }
            float dx = a.LastKnownPosition.x - _animatorCamPos.x;
            float dz = a.LastKnownPosition.z - _animatorCamPos.z;
            if (dx * dx + dz * dz < TickHotRadius * TickHotRadius) return true;
        }

        return false;
    }
}
