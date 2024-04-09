using Assets.Scripts.Effects;
using System;
using System.Collections.Generic;
using Assets.Scripts.MapEditor;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Sprites
{
    internal class RoSpriteRendererStandard : MonoBehaviour, IRoSpriteRenderer
    {
        private int _currentAngleIndex;

        public int CurrentAngleIndex
        {
            get => _currentAngleIndex;
            set { _currentAngleIndex = value; }
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

        public Material OverrideMaterial;

        private static Material belowWaterMat;
        private static Material aboveWaterMat;
        private static Material[] materialArrayNormal;
        private static Material[] materialArrayWater;
        private MaterialPropertyBlock propertyBlock;

        private Shader shader;

        private Dictionary<int, Mesh> meshCache;
        private Dictionary<int, Mesh> colliderCache;

        private bool isInitialized;
        private bool is8Direction;

        public int ActionId;
        public int CurrentFrame;
        public Color Color;
        public Direction Direction;
        public float SpriteOffset;
        public RoSpriteData SpriteData;
        //public Texture2D AppliedPalette;

        public void SetAction(int action, bool is8Direction)
        {
            ActionId = action;
            this.is8Direction = is8Direction;
        }

        public void SetColor(Color color) => Color = color;
        public void SetDirection(Direction direction) => Direction = direction;
        public void SetFrame(int frame) => CurrentFrame = frame;
        public void SetSprite(RoSpriteData sprite) => SpriteData = sprite;
        public void SetOffset(float offset) => SpriteOffset = offset;

        public RoFrame GetActiveRendererFrame()
        {
            var actions = SpriteData.Actions[ActionId + CurrentAngleIndex];
            var frame = CurrentFrame;

            if (frame >= actions.Frames.Length)
                frame = actions.Frames.Length - 1;

            return actions.Frames[frame];
        }

        public void Initialize(bool makeCollider = false)
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
            MeshRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
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
            if (shader == null)
                return;
            
            if (materialArrayNormal == null || materialArrayNormal.Length <= 0)
            {
                var noWaterMat = new Material(shader);
                noWaterMat.EnableKeyword("WATER_OFF");
                // if(SpriteData.Palette != null || AppliedPalette != null)
                //     noWaterMat.EnableKeyword("PALETTE_ON");

                materialArrayNormal = new Material[1];
                materialArrayNormal[0] = noWaterMat;
            }

            if (materialArrayWater == null || materialArrayWater.Length <= 0)
            {
                aboveWaterMat = new Material(shader);
                //aboveWaterMat.EnableKeyword("WATER_ABOVE");

                //belowWaterMat = new Material(shader);
                //belowWaterMat.EnableKeyword("WATER_BELOW");
                //belowWaterMat.renderQueue -= 2;
                
                // if(SpriteData.Palette != null || AppliedPalette != null)
                //     aboveWaterMat.EnableKeyword("PALETTE_ON");

                materialArrayWater = new Material[1];
                //materialArrayWater[0] = belowWaterMat;
                materialArrayWater[0] = aboveWaterMat;
            }
        }

        public void SetActive(bool isActive)
        {
            // Debug.Log($"{gameObject.GetGameObjectPath()} {isActive}");
            gameObject.SetActive(isActive);
        }

        public void SetOverrideMaterial(Material mat)
        {
            OverrideMaterial = mat;
            //MeshRenderer.sharedMaterials = null;
            MeshRenderer.sharedMaterial = OverrideMaterial;
        }

        public void SetLightProbeAnchor(GameObject anchor)
        {
            MeshRenderer.probeAnchor = anchor.transform;
        }

        public void Rebuild()
        {
            if (!isInitialized)
                return;

            CreateMaterials();

            if (OverrideMaterial != null)
            {
                MeshRenderer.sharedMaterial = OverrideMaterial;
            }
            else
            {
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
                    if(materialArrayNormal != null)
                        MeshRenderer.sharedMaterials = materialArrayNormal;
                }
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

            //if (SecondPassForWater && HasWater)
            //{
            //    MeshRenderer.GetPropertyBlock(propertyBlock, 1);
            //    SetPropertyBlock();
            //    MeshRenderer.SetPropertyBlock(propertyBlock, 1);
            //}
        }

        private void SetPropertyBlock()
        {
            var envColor = RoMapRenderSettings.GetBakedLightContribution(new Vector2(transform.position.x, transform.position.z));
            
            propertyBlock.SetTexture("_MainTex", SpriteData.Atlas);
            propertyBlock.SetColor("_Color", Color);
            propertyBlock.SetColor("_EnvColor", envColor);
            propertyBlock.SetFloat("_Width", SpriteData.AverageWidth / 25f);
            
            // if (SpriteData.Palette != null || AppliedPalette != null)
            // {
            //     if(AppliedPalette != null)
            //         propertyBlock.SetTexture("_PalTex", AppliedPalette);
            //     else
            //         propertyBlock.SetTexture("_PalTex", SpriteData.Palette);
            // }

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

        public bool UpdateRenderer()
        {
            if (!isInitialized)
                return false;

            if (UpdateAngleWithCamera)
            {
                // var targetDir = transform.position - CameraFollower.Instance.transform.position;
                // var subAngle = Vector3.SignedAngle(targetDir, CameraFollower.Instance.transform.forward, Vector3.up);
                //

                var rotation = 360 - CameraFollower.Instance.Rotation;
                if(!is8Direction)
                    rotation -= 22;
                if (rotation < 0)
                    rotation += 360;
                
                var angleIndex = RoAnimationHelper.GetSpriteIndexForAngle(Direction, rotation);
                if (angleIndex != CurrentAngleIndex)
                {
                    CurrentAngleIndex = angleIndex;

                    Rebuild();
                    return true;
                }
            }

            return false;
        }

        //public void OnDestroy()
        //{
        //    if (belowWaterMat != null) Destroy(belowWaterMat);
        //    if (aboveWaterMat != null) Destroy(aboveWaterMat);
        //}
    }
}