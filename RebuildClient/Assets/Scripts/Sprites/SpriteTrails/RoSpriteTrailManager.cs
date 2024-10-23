using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Assets.Scripts.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace Assets.Scripts.Sprites
{
    public class RoSpriteTrailManager : MonoBehaviorSingleton<RoSpriteTrailManager>
    {
        struct ActiveTrail
        {
            public ServerControllable Entity;
            public float Interval;
            public float Lifetime;
            public float NextSpawn;
        }

        private List<ActiveTrail> Trails = new();
        private Stack<RoSpriteTrail> TrailPool = new();
        private Stack<RoSpriteTrailRenderer> RendererPool = new();

        public void ReturnRenderer(RoSpriteTrailRenderer renderer)
        {
            renderer.gameObject.SetActive(false);
            renderer.transform.parent = transform;
            RendererPool.Push(renderer);
        }

        public void ReturnTrail(RoSpriteTrail trail)
        {
            trail.gameObject.SetActive(false);
            trail.transform.parent = transform;
            TrailPool.Push(trail);
            // Debug.Log($"Returning trail, pool count {TrailPool.Count}");
        }

        private int GetActiveTrailId(ServerControllable target)
        {
            for (var i = 0; i < Trails.Count; i++)
            {
                if (Trails[i].Entity == target)
                    return i;
            }

            return -1;
        }

        public void RemoveTrailFromEntity(ServerControllable target)
        {
            for (var i = 0; i < Trails.Count; i++)
            {
                if (Trails[i].Entity == target)
                {
                    Trails.RemoveAt(i);
                    return;
                }
            }
        }

        public void AttachTrailToEntity(ServerControllable entity, float interval = 0.1f, float lifetime = 0.5f)
        {
            var existingId = GetActiveTrailId(entity);
            if (existingId >= 0)
            {
                var existing = Trails[existingId];
                existing.Interval = interval;
                existing.NextSpawn = Time.timeSinceLevelLoad + interval;
                Trails[existingId] = existing;
                return;
            }

            var trail = new ActiveTrail()
            {
                Entity = entity,
                Interval = interval,
                Lifetime = lifetime,
                NextSpawn = Time.timeSinceLevelLoad + interval
            };

            Trails.Add(trail);
        }

        private void CloneSpriteForTrail(RoSpriteRendererStandard sprite, RoSpriteTrail trail, int order)
        {
            if (sprite == null || sprite.MeshFilter == null)
                return;
            
            if (!RendererPool.TryPop(out var renderer))
            {
                var go = new GameObject("Renderer");
                renderer = go.AddComponent<RoSpriteTrailRenderer>();
            }
            

            renderer.Init();
            renderer.MeshRenderer.sharedMaterial = sprite.MeshRenderer.sharedMaterial;
            renderer.MeshFilter.sharedMesh = sprite.MeshFilter.sharedMesh;
            renderer.SortingGroup.sortingOrder = order;
            renderer.SpriteData = sprite.SpriteData;
            renderer.SpriteOffset = sprite.SpriteOffset;
            renderer.MeshRenderer.sortingOrder = sprite.MeshRenderer.sortingOrder;
            renderer.transform.localRotation = sprite.gameObject.transform.localRotation;
            renderer.transform.localPosition = sprite.gameObject.transform.localPosition;
            
            trail.AddTrailSprite(renderer, order);
        }

        private void SpawnTrail(int id)
        {
            var data = Trails[id];
            var animator = data.Entity.SpriteAnimator;
            var renderer = (RoSpriteRendererStandard)animator.SpriteRenderer;

            if (renderer == null)
                return;

            if (!TrailPool.TryPop(out var trail))
            {
                var go = new GameObject("Trail");
                trail = go.AddComponent<RoSpriteTrail>();
                trail.Billboard = go.AddComponent<BillboardObject>();
                trail.Billboard.Style = BillboardStyle.Character;
                trail.SortingGroup = go.AddComponent<SortingGroup>();
                trail.SortingGroup.sortingOrder = -1;
            }

            trail.gameObject.SetActive(true);
            trail.transform.SetParent(null);
            trail.transform.localScale = data.Entity.transform.localScale;
            trail.transform.localPosition = data.Entity.transform.localPosition;
            trail.transform.localRotation = Quaternion.identity;

            trail.Color = animator.Color;
            trail.Duration = data.Lifetime;
            trail.RemainingTime = data.Lifetime;

            trail.SortingGroup.sortingOrder = animator.State == SpriteState.Walking ? 0 : -1;
            //
            // var hue = (int)(Time.timeSinceLevelLoad * 255) % 255;
            // var c = Color.HSVToRGB(hue/255f, 1f, 0.5f);
            // trail.Color = c;
            
            CloneSpriteForTrail(renderer, trail, 0);
            for (var i = 0; i < animator.ChildrenSprites.Count; i++)
            {
                CloneSpriteForTrail((RoSpriteRendererStandard)animator.ChildrenSprites[i].SpriteRenderer, trail, animator.ChildrenSprites[i].SpriteOrder);
            }
        }

        private void Update()
        {
            for (var i = 0; i < Trails.Count; i++)
            {
                var trail = Trails[i];
                if (trail.Entity == null)
                {
                    Trails.RemoveAt(i);
                    i--;
                    continue;
                }
                
                if (Time.timeSinceLevelLoad > trail.NextSpawn)
                {
                    SpawnTrail(i);
                    trail.NextSpawn = Time.timeSinceLevelLoad + trail.Interval;
                    if (trail.NextSpawn < Time.timeSinceLevelLoad)
                        trail.NextSpawn = Time.timeSinceLevelLoad;
                    Trails[i] = trail;
                    // Debug.Log(trail.NextSpawn);
                }
            }
        }
    }
}