using Assets.Scripts.Effects;
using System;
using System.Collections.Generic;
using Assets.Scripts.MapEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Sprites
{
    internal class RoSpriteRendererStandard : RoSpriteRendererBase
    {
        private int _currentAngleIndex;
        public int CurrentAngleIndex
        {
            get => _currentAngleIndex;
            set
            {
                _currentAngleIndex = value;
            }
        }
        public bool UpdateAngleWithCamera;
        public bool SecondPassForWater;
        public int SortingOrder;
        public Vector2Int LastPosition;
        public bool HasWater;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public MeshCollider MeshCollider;
        public SortingGroup SortingGroup;

        private static Material belowWaterMat;
        private static Material aboveWaterMat;
        private static Material[] materialArrayNormal;
        private static Material[] materialArrayWater;
        private MaterialPropertyBlock propertyBlock;

        private Shader shader;

        private Dictionary<int, Mesh> meshCache;
        private Dictionary<int, Mesh> colliderCache;

        private bool isInitialized;

        public override void Initialize(bool makeCollider = false)
        {
            if (isInitialized)
                return;
            if (gameObject == null)
                return;
            if (SpriteData == null)
                return;

            MeshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer = gameObject.AddComponent<MeshRenderer>();

            MeshRenderer.sortingOrder = SortingOrder;
            if (makeCollider)
                MeshCollider = gameObject.AddComponent<MeshCollider>();

            SortingGroup = gameObject.GetOrAddComponent<SortingGroup>();
            SortingGroup.sortingOrder = SortingOrder;

            MeshRenderer.receiveShadows = false;
            MeshRenderer.lightProbeUsage = LightProbeUsage.Off;
            MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            propertyBlock = new MaterialPropertyBlock();

            if (shader == null)
                shader = ShaderCache.Instance?.SpriteShader;

            meshCache = SpriteMeshCache.GetMeshCacheForSprite(SpriteData.Name);
            colliderCache = SpriteMeshCache.GetColliderCacheForSprite(SpriteData.Name);

            Color = Color.white;

            CurrentAngleIndex = (int)Direction;
            if (UpdateAngleWithCamera)
                CurrentAngleIndex = RoAnimationHelper.GetSpriteIndexForAngle(Direction, 360 - CameraFollower.Instance.Rotation);

            CreateMaterials();

            isInitialized = true;

            Rebuild();
        }

        private void CreateMaterials()
        {
            if (aboveWaterMat == null)
            {
                aboveWaterMat = new Material(shader);
                aboveWaterMat.EnableKeyword("WATER_ABOVE");

                materialArrayNormal = new Material[1];
                materialArrayNormal[0] = aboveWaterMat;
            }

            if (belowWaterMat == null)
            {
                belowWaterMat = new Material(shader);
                belowWaterMat.EnableKeyword("WATER_BELOW");
                belowWaterMat.renderQueue -= 2;

                materialArrayWater = new Material[2];
                materialArrayWater[0] = belowWaterMat;
                materialArrayWater[1] = aboveWaterMat;
            }
        }

        public override void Rebuild()
        {
            if (!isInitialized)
                return;

            CreateMaterials();

            if (SecondPassForWater)
            {

                if (RoWalkDataProvider.Instance.GetMapPositionForWorldPosition(transform.position, out var pos) && pos != LastPosition)
                {
                    LastPosition = pos;
                    
                    HasWater = RoWalkDataProvider.Instance.IsPositionNearWater(transform.position, 1);

                    if (HasWater)

                        MeshRenderer.sharedMaterials = materialArrayWater;
                    else
                        MeshRenderer.sharedMaterials = materialArrayNormal;
                }

                //Debug.Log($"{pos} {LastPosition} {HasWater}");
            }
            else
            {
                MeshRenderer.sharedMaterials = materialArrayNormal;
            }


            if (!UpdateAngleWithCamera)
                CurrentAngleIndex = (int)Direction;

            var mesh = GetMeshForFrame();
            var cMesh = GetColliderForFrame();

            MeshFilter.sharedMesh = null;
            MeshFilter.sharedMesh = mesh;
            if (MeshCollider != null)
            {
                MeshCollider.sharedMesh = null;
                MeshCollider.sharedMesh = cMesh;
            }

            MeshRenderer.GetPropertyBlock(propertyBlock, 0);
            SetPropertyBlock();
            MeshRenderer.SetPropertyBlock(propertyBlock, 0);

            if (SecondPassForWater && HasWater)
            {
                MeshRenderer.GetPropertyBlock(propertyBlock, 1);
                SetPropertyBlock();
                MeshRenderer.SetPropertyBlock(propertyBlock, 1);
            }

        }

        private void SetPropertyBlock()
        {
            propertyBlock.SetTexture("_MainTex", SpriteData.Atlas);
            propertyBlock.SetColor("_Color", Color);
            propertyBlock.SetFloat("_Offset", SpriteOffset);

            if (Mathf.Approximately(0, SpriteOffset))
                propertyBlock.SetFloat("_Offset", SpriteData.Size / 125f);
            else
                propertyBlock.SetFloat("_Offset", SpriteOffset);

            MeshRenderer.SetPropertyBlock(propertyBlock);
        }

        public Mesh GetMeshForFrame()
        {
            //just a simple hash
            var id = ((ActionId + CurrentAngleIndex) << 16) + CurrentFrame;

            if (meshCache == null)
                throw new Exception("Meshcache is not initialized! But how? isInitialized status is " + isInitialized);

            if (meshCache.TryGetValue(id, out var mesh))
                return mesh;

            var newMesh = SpriteMeshBuilder.BuildSpriteMesh(SpriteData, ActionId, CurrentAngleIndex, CurrentFrame);

            meshCache.Add(id, newMesh);

            return newMesh;
        }
        private Mesh GetColliderForFrame()
        {
            var id = ((ActionId + CurrentAngleIndex) << 16) + CurrentFrame;

            if (colliderCache.TryGetValue(id, out var mesh))
                return mesh;

            var newMesh = SpriteMeshBuilder.BuildColliderMesh(SpriteData, ActionId, CurrentAngleIndex, CurrentFrame);

            colliderCache.Add(id, newMesh);

            return newMesh;
        }

        public void Update()
        {
            if (!isInitialized)
                return;

            if (UpdateAngleWithCamera)
            {
                var angleIndex = RoAnimationHelper.GetSpriteIndexForAngle(Direction, 360 - CameraFollower.Instance.Rotation);
                if (angleIndex != CurrentAngleIndex)
                {
                    CurrentAngleIndex = angleIndex;
                    Rebuild();
                }
            }

        }

        //public void OnDestroy()
        //{
        //    if (belowWaterMat != null) Destroy(belowWaterMat);
        //    if (aboveWaterMat != null) Destroy(aboveWaterMat);
        //}
    }
}
