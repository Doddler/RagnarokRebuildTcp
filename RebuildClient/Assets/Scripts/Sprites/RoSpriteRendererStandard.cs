using Assets.Scripts.Effects;
using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.MapEditor;
using Assets.Scripts.UI.ConfigWindow;
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
        public float VerticalOffset;
        public float ZOffset;
        public float ShaderYOffset;

        private static readonly int Drain = Shader.PropertyToID("_ColorDrain");
        private static readonly int Offset = Shader.PropertyToID("_Offset");
        private static readonly int Width = Shader.PropertyToID("_Width");
        private static readonly int EnvColor = Shader.PropertyToID("_EnvColor");
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int LightingSamplePosition = Shader.PropertyToID("_LightingSamplePosition");
        private static readonly int IsMeshRenderer = Shader.PropertyToID("_IsMeshRenderer");
        private static readonly int VPos = Shader.PropertyToID("_VPos");
        private MaterialPropertyBlock propertyBlock;

        private Shader shader;

        private Dictionary<int, Mesh> meshCache;
        private Dictionary<int, Mesh> colliderCache;

        private bool isInitialized;
        private bool is8Direction;

        public int ActionId;
        public int CurrentFrame;
        public Color Color;
        public float ColorDrain;
        public float Angle;
        public float SpriteOffset;
        public int PaletteId;
        public RoSpriteData SpriteData;
        public bool IsHidden;
        
        private RoSpriteDrawCall drawCall;

        //public Direction Direction;
        public Direction Direction
        {
            get => RoAnimationHelper.GetFacingForAngle(Angle);
            set => Angle = RoAnimationHelper.FacingDirectionToRotation(value);
        }
        public Direction LastDirection;

        private Mesh _mesh;
        //public Texture2D AppliedPalette;

        public void SetAction(int action, bool is8Direction)
        {
            ActionId = action;
            this.is8Direction = is8Direction;
        }

        public void SetColor(Color color) => Color = color;
        public void SetColorDrain(float drainStrength) => ColorDrain = drainStrength;
        public void SetDirection(Direction direction) => Direction = direction;
        public void SetAngle(float angle) => Angle = angle;
        
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
                shader = GameConfig.Data.EnableXRay ? ShaderCache.Instance?.SpriteShaderWithXRay : ShaderCache.Instance?.SpriteShader;

            meshCache = SpriteMeshCache.GetMeshCacheForSprite(SpriteData.Name);
            colliderCache = SpriteMeshCache.GetColliderCacheForSprite(SpriteData.Name);

            Color = Color.white;

            CurrentAngleIndex = (int)Direction;
            if (UpdateAngleWithCamera)
                CurrentAngleIndex = RoAnimationHelper.GetSpriteIndexForAngle(Direction, 360 - CameraFollower.Instance.Rotation);
            
            isInitialized = true;

            Rebuild();
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
            
            if (OverrideMaterial != null)
            {
                MeshRenderer.sharedMaterial = OverrideMaterial;
            }

            if (!UpdateAngleWithCamera)
                CurrentAngleIndex = (int)Direction;

            if (SpriteData.ReverseSortingWhenFacingNorth && CurrentAngleIndex >= 2 && CurrentAngleIndex <= 5)
            {
                SortingGroup.sortingOrder =
                    SortingOrder < 0 ? -SortingOrder : SortingOrder - 10; //SortingOrder - 10; //this puts the shield behind the other sprite parts
                if (ZOffset != 0)
                    transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, -ZOffset);
            }
            else
            {
                SortingGroup.sortingOrder = SortingOrder; //we update this each frame because the parent might have changed our order based on the animation.
                if (ZOffset != 0)
                    transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, ZOffset);
            }

	        _mesh = GetMeshForFrame();
            var cMesh = GetColliderForFrame();

            MeshFilter.sharedMesh = null;
            MeshFilter.sharedMesh = _mesh;
            if (MeshCollider != null)
            {
                MeshCollider.sharedMesh = null;
                MeshCollider.sharedMesh = cMesh;
            }

            MeshRenderer.GetPropertyBlock(propertyBlock, 0);
            SetPropertyBlock();
            MeshRenderer.SetPropertyBlock(propertyBlock, 0);

            //Debug.Log($"Generating Mesh Data for {SpriteData.Atlas.name} at frame {Time.frameCount}");
        }

        private void SetPropertyBlock()
        {
            var envColor = RoMapRenderSettings.GetBakedLightContribution(new Vector2(transform.position.x, transform.position.z));
            
            propertyBlock.SetTexture(MainTex, SpriteData.Atlas);
            propertyBlock.SetColor(Color1, Color);
            propertyBlock.SetColor(EnvColor, envColor);
            propertyBlock.SetFloat(Width, SpriteData.AverageWidth / 25f);
            
            // We want to sample the light on a single point for all sprites.
            propertyBlock.SetVector(LightingSamplePosition, transform.parent.position);
            
            // Unity send light info differently on sprites and mesh renderers, so we need to let the shader know what path it should take.
            propertyBlock.SetFloat(IsMeshRenderer, 1f);
            
            // if (SpriteData.Palette != null || AppliedPalette != null)
            // {
            //     if(AppliedPalette != null)
            //         propertyBlock.SetTexture("_PalTex", AppliedPalette);
            //     else
            //         propertyBlock.SetTexture("_PalTex", SpriteData.Palette);
            // }

            if (Mathf.Approximately(0, SpriteOffset))
                propertyBlock.SetFloat(Offset, Mathf.Max(SpriteData.Size / 125f, 1f));
            else
                propertyBlock.SetFloat(Offset, SpriteOffset);
            
            propertyBlock.SetFloat(Drain, ColorDrain);
            propertyBlock.SetFloat(VPos, ShaderYOffset);

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

            var newMesh = SpriteMeshBuilder.BuildSpriteMesh(SpriteData, ActionId, CurrentAngleIndex, CurrentFrame, 1f, VerticalOffset, PaletteId);

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
            
	        MeshRenderer.enabled = !IsHidden;
            // MeshCollider.enabled = !IsHidden;
            
            bool result = false;
            
            if (UpdateAngleWithCamera)
            {
                // var targetDir = transform.position - CameraFollower.Instance.transform.position;
                // var subAngle = Vector3.SignedAngle(targetDir, CameraFollower.Instance.transform.forward, Vector3.up);
                //

                var rotation = 360 - (int)CameraFollower.Instance.Rotation;
                if(is8Direction)
                    rotation += 22;
                rotation %= 360;
                
                var angleIndex = RoAnimationHelper.GetSpriteIndexForAngle(Angle, rotation);
                // Debug.Log($"{SpriteData.Name} is8Direction:{is8Direction} Angle:{Angle} Camera:{CameraFollower.Instance.Rotation} -> {rotation} AngleIndex: {angleIndex}");
                if (angleIndex != CurrentAngleIndex)
                {
                    CurrentAngleIndex = angleIndex;

                    Rebuild();
                    result = true;
                }
            }

            shader = GameConfig.Data.EnableXRay ? ShaderCache.Instance.SpriteShaderWithXRay : ShaderCache.Instance.SpriteShader;
            if (MeshRenderer.material.shader != shader) MeshRenderer.material.shader = shader;
            
            UpdateDrawCall();
            return result;
        }
        
        private void OnEnable()
        {
	        StartCoroutine(WaitSpriteDataThenCreateDrawCall());
        }

        private IEnumerator WaitSpriteDataThenCreateDrawCall()
        {
	        while (!SpriteData)
	        {
		        yield return null;
	        }

	        UpdateDrawCall();
	        RoSpriteBatcher.Instance.drawCalls.AddItem(SpriteData.Atlas, drawCall);
        }
        
        private void OnDisable()
        {
	        if (drawCall == null) return;
	        RoSpriteBatcher.Instance.drawCalls.RemoveItem(SpriteData.Atlas, drawCall);
        }
        
        private void UpdateDrawCall()
        {
	        drawCall ??= new RoSpriteDrawCall();
	        
	        if (!RoSpriteBatcher.Instance.EnableInstancing)
	        {
		        MeshRenderer.enabled = !IsHidden;
		        return;
	        }
	        
	        // We are only instancing quads
	        if (MeshFilter.sharedMesh.vertexCount != 4)
	        {
		        MeshRenderer.enabled = true;
		        drawCall.Color = Color.clear;
		        return;
	        }
	        
	        // We can't sort transparency, so we must fall back to standard rendering.
	        if (MeshFilter.sharedMesh.colors.Length > 0 && MeshFilter.sharedMesh.colors[0].a < 0.5f)
	        {
		        MeshRenderer.enabled = true;
		        drawCall.Vertices = new[] { Vector3.zero };
		        drawCall.Color = Color.clear;
		        return;
	        }

	        MeshRenderer.enabled = false;
	        if (!isInitialized) return;
	        
	        drawCall.Transform = transform;
	        
	        drawCall.UV = _mesh.uv;
	        drawCall.Vertices = _mesh.vertices;
	        drawCall.VColor = _mesh.colors;
	        drawCall.IsHidden = IsHidden;
	        
	        if (propertyBlock != null)
	        {
		        drawCall.Color = propertyBlock.GetColor(Color1);
		        drawCall.Offset = propertyBlock.GetFloat(Offset);
		        drawCall.ColorDrain = propertyBlock.GetFloat(Drain);
		        drawCall.VPos = propertyBlock.GetFloat(VPos);
		        drawCall.Width = propertyBlock.GetFloat(Width);
	        }
        }
        
        //public void OnDestroy()
        //{
        //    if (belowWaterMat != null) Destroy(belowWaterMat);
        //    if (aboveWaterMat != null) Destroy(aboveWaterMat);
        //}
    }
}