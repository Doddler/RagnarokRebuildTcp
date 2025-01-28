using System;
using System.Collections.Generic;
using Assets.Scripts.Utility;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Effects
{
    public enum ParticleDisplayMode : byte
    {
        Normal,
        Pulse,
    }

    public struct EffectParticle
    {
        public float Size;
        public float ShrinkLength;
        public float StartTime;
        public float Lifetime;
        public float AlphaSpeed;
        public float AlphaMax;
        public float Acceleration;
        public float FadeStartTime;
        public Vector3 Position;
        public Vector3 Velocity;
        public Color32 Color;
        public float Gravity;
        public float GravityAccel;
        public float UniqueValue;
        public ParticleDisplayMode Mode;
        public byte ParticleId; //sprite id
        public float DelayTime;
        public GameObject RelativeTarget;
    }

    public class EffectParticleManager : MonoBehaviorSingleton<EffectParticleManager>
    {
        public EffectParticle[] Particles = new EffectParticle[2048];
        public Material Material;
        public List<Sprite> Sprites;
        public int ParticlesInUse = 0;
        private bool isInit;

        private new CameraFollower camera;

        private MeshBuilder mb;
        private MeshRenderer mr;
        private MeshFilter mf;
        private Mesh mesh;

        //these are temporary to be fed into the meshbuilder
        private Vector3[] verts = new Vector3[4];
        private Vector3[] normals = new Vector3[4];
        private Color32[] colors = new Color32[4];
        private Vector2[] uvs = new Vector2[4];
        //private Vector3[] uv3s = new Vector3[4];
        private int[] tris;

        private void Initialize()
        {
            mb = new MeshBuilder();
            mr = GetComponent<MeshRenderer>();
            mf = GetComponent<MeshFilter>();
            mesh = new Mesh();
            mr.sharedMaterial = Material;
            mf.sharedMesh = mesh;
            Material.mainTexture = Sprites[0].texture; //assume they're all on the same packed sprite sheet
            camera = CameraFollower.Instance;
            isInit = true;
        }

        private void SwapBack(int index)
        {
            Particles[index] = Particles[ParticlesInUse - 1];
            ParticlesInUse--;
        }

        public void AddParticle(ref EffectParticle p)
        {
            if(!isInit)
                Initialize();

            p.StartTime = Time.time;
            p.UniqueValue = Random.Range(0, 1000f);
            if (p.AlphaMax <= 0)
                p.AlphaMax = p.Color.a / 255f;
            Particles[ParticlesInUse] = p;
            ParticlesInUse++;
        }

        //this is actually only used in the hit particle effect, but I'm too lazy to refactor it
        public void AddTrailParticle(float size, Vector3 position, Vector3 velocity, float lifetime, Color32 color, float acceleration = 0,
            float gravity = 0, int spriteId = 0, ParticleDisplayMode displayMode = ParticleDisplayMode.Normal)
        {
            if (!isInit)
                Initialize();

            if (ParticlesInUse + 1 > Particles.Length)
            {
                Debug.Log($"Exceeding EffectParticle count of {Particles.Length}, resizing array");
                var newParticles = new EffectParticle[Particles.Length * 2];
                Array.Copy(Particles, newParticles, Particles.Length);
                Particles = newParticles;
            }

            var particle = new EffectParticle()
            {
                Size = size,
                AlphaSpeed = 6,
                AlphaMax = color.a,
                StartTime = Time.time,
                Lifetime = lifetime,
                FadeStartTime = lifetime/2,
                Position = position,
                Velocity = velocity,
                Color = color,
                Gravity = gravity,
                GravityAccel = 0,
                Acceleration = acceleration,
                ParticleId = (byte)spriteId,
                UniqueValue = Random.Range(0f, 10000f),
                DelayTime = 0f,
                Mode = displayMode
            };

            var curSize = size;
            var alpha = color.a;

            for (var i = 0; i < 1; i++)
            {
                Particles[ParticlesInUse + i] = particle;
                Particles[ParticlesInUse + i].DelayTime = 1 / 60f * i;
                Particles[ParticlesInUse + i].StartTime += 1 / 60f * i;
                Particles[ParticlesInUse + i].Color = new Color32(color.r, color.g, color.b, alpha);
                Particles[ParticlesInUse + i].Size = curSize;
                Particles[ParticlesInUse + i].ShrinkLength = lifetime / 2f;
                
                curSize /= 2;
                alpha = (byte)(alpha / 2);
            }

            ParticlesInUse += 4;
            if (!enabled)
                enabled = true;
        }

        public void BuildParticleMesh()
        {
            mb.Clear();

            for (var i = 0; i < ParticlesInUse; i++)
            {
                var p = Particles[i];
                if (p.StartTime + p.DelayTime > Time.time)
                    continue;
                
                var offset = p.Position;
                if (p.RelativeTarget != null) //for matching the position and billboard rotation of an object
                    offset = p.RelativeTarget.transform.rotation * offset + p.RelativeTarget.transform.position;

                var lookAt = offset - camera.transform.position;
                var rotation = Quaternion.LookRotation(lookAt, Vector3.up);

                var c = p.Color;
                var t = Time.time - p.StartTime;
                var a = (1 - t / p.Lifetime) * 2 * (c.a / 255f);
                
                if (p.Mode == ParticleDisplayMode.Pulse)
                {
                    var pong = Mathf.PingPong((p.UniqueValue + Time.timeSinceLevelLoad) * 5f, 1f);
                    a *= pong;
                }
                
                var a2 = Mathf.Clamp((int)(a * 255f), 0, p.Color.a);
                c = new Color32(c.r, c.g, c.b, (byte)a2);

                var size = p.Size;
                var sizePos = (p.Lifetime - p.ShrinkLength);

                if (t > sizePos)
                    size = Mathf.Lerp(p.Size, 0, (t - sizePos) / (p.Lifetime - p.ShrinkLength));
                
                var width = size;
                var height = size;

                colors[0] = c;
                colors[1] = c;
                colors[2] = c;
                colors[3] = c;

                verts[0] = rotation * new Vector3(-width, height) + offset;
                verts[1] = rotation * new Vector3(width, height) + offset;
                verts[2] = rotation * new Vector3(-width, -height) + offset;
                verts[3] = rotation * new Vector3(width, -height) + offset;

                var sprite = Sprites[p.ParticleId];

                var spriteUVs = sprite.uv;

                // var spriteUVs = SpriteUtility.GetSpriteUVs(sprite, true);
                // var rect = sprite.textureRect;

                uvs[0] = spriteUVs[0];
                uvs[1] = spriteUVs[1];
                uvs[2] = spriteUVs[2];
                uvs[3] = spriteUVs[3];

                //completely unused really
                normals[0] = Vector3.up;
                normals[1] = Vector3.up;
                normals[2] = Vector3.up;
                normals[3] = Vector3.up;

                mb.AddQuad(verts, normals, uvs, colors);
            }

            if (mesh.triangles.Length != mb.TriangleCount)
                mesh.Clear();
            mf.sharedMesh = mb.ApplyToMesh(mesh);
        }

        public void Update()
        {
            if (!isInit)
                Initialize();

            for (var i = 0; i < ParticlesInUse; i++)
            {
                ref var p = ref Particles[i];
                
                if (p.StartTime + p.DelayTime > Time.time)
                    continue;

                if (Time.time > p.StartTime + p.Lifetime)
                {
                    SwapBack(i); //replaces this particle with the last particle in the list and shrink the list
                    i--;
                    continue;
                }

                p.Position += p.Velocity * Time.deltaTime;
                p.Velocity += p.Velocity.normalized * (p.Acceleration * Time.deltaTime);
                p.Gravity += p.Gravity * (p.GravityAccel * Time.deltaTime);
                
                var c = p.Color;
                var t = Time.time - p.StartTime;
                if(!Mathf.Approximately(p.Gravity, 0))
                    p.Velocity += new Vector3(0, -1f, 0f) * (p.Gravity * Time.deltaTime);

                var a = p.Color.a / 255f;
                if (t < p.FadeStartTime)
                {
                    a += p.AlphaSpeed * Time.deltaTime;
                    if (a > p.AlphaMax)
                        a = p.AlphaMax;
                }
                else
                {
                    var fadeSpeed = 1 / (p.Lifetime - p.FadeStartTime);
                    a -= fadeSpeed * Time.deltaTime;
                    if (a < 0)
                        a = 0;

                }

                c.a = (byte)Mathf.Clamp(c.a + a, 0, p.AlphaMax);

                // c.a = (byte)Mathf.Clamp((1 - t / p.Lifetime) * 2 * 255f * (c.a / 255f), 0, p.Color.a);

            }

            if (ParticlesInUse > 0)
            {
                mr.enabled = true;
                BuildParticleMesh();
            }
            else
            {
                mr.enabled = false;
            }
        }
    }
}