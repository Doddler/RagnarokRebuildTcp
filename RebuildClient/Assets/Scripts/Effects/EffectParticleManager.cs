using System;
using UnityEngine;
using Utility;

namespace Assets.Scripts.Effects
{
    public struct TrailParticle
    {
        public float Size;
        public float StartTime;
        public float Lifetime;
        public float Acceleration;
        public Vector3 Position;
        public Vector3 Velocity;
        public Color32 Color;
        public float Gravity;
    }

    public class EffectParticleManager : MonoBehaviorSingleton<EffectParticleManager>
    {
        public ParticleSystem TrailSystem;
        public AnimationCurve AlphaOverTime;
        public TrailParticle[] Particles = new TrailParticle[256];
        public int ParticlesInUse = 0;
        private bool isInit;

        private void Initialize()
        {
            var go = GameObject.Instantiate(Resources.Load<GameObject>("TrailEffectParticleSystem"));
            go.transform.parent = gameObject.transform;
            go.transform.position = Vector3.zero;
            TrailSystem = go.GetComponent<ParticleSystem>();
            isInit = true;
        }

        private void SwapBack(int index)
        {
            Particles[index] = Particles[ParticlesInUse - 1];
            ParticlesInUse--;
        }

        public void AddParticle(float size, Vector3 position, Vector3 velocity, float lifetime, Color32 color, float acceleration = 0, float gravity = 0)
        {
            if (!isInit)
                Initialize();

            if (ParticlesInUse + 1 > Particles.Length)
            {
                Debug.Log($"Exceeding EffectParticle count of {Particles.Length}, resizing array");
                var newParticles = new TrailParticle[Particles.Length * 2];
                Array.Copy(Particles, newParticles, Particles.Length);
                Particles = newParticles;
            }

            Particles[ParticlesInUse] = new TrailParticle()
            {
                Size = size,
                StartTime = Time.time,
                Lifetime = lifetime,
                Position = position,
                Velocity = velocity,
                Color = color,
                Gravity = gravity,
                Acceleration = acceleration,
            };
            ParticlesInUse++;
            if (!enabled)
                enabled = true;
        }

        public void FixedUpdate()
        {
            for (var i = 0; i < ParticlesInUse; i++)
            {
                ref var p = ref Particles[i];

                if (Time.time > p.StartTime + p.Lifetime)
                {
                    SwapBack(i); //replaces this particle with the last particle in the list and shrink the list
                    i--;
                    continue;
                }

                p.Velocity *= 1 + p.Acceleration * Time.deltaTime;
                p.Position += p.Velocity * Time.deltaTime;
                var c = p.Color;
                var t = Time.time - p.StartTime;
                p.Velocity += new Vector3(0, -1f, 0f) * p.Gravity * Time.deltaTime;
                c.a = (byte)Mathf.Clamp((1 - t / p.Lifetime) * 2 * 255f * (c.a / 255f), 0, 255);

                TrailSystem.Emit(p.Position, Vector3.zero, p.Size, 3/60f, c);
            }

            if (ParticlesInUse <= 0)
                enabled = false;
        }
    }
}