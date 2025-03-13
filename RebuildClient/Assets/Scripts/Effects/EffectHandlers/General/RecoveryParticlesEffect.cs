using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("RecoveryParticles")]
    public class RecoveryParticlesEffect : IEffectHandler
    {
        public static void LaunchRecoveryParticles(ServerControllable source, bool isHpRecovery)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.RecoveryParticles);
            effect.SourceEntity = source;
            effect.UpdateOnlyOnFrameChange = true;
            effect.SetDurationByFrames(100);
            effect.FollowTarget = source.gameObject;
            effect.DestroyOnTargetLost = false;
            effect.transform.position = source.transform.position;
            effect.transform.localScale = Vector3.one;
            effect.Flags[0] = isHpRecovery ? 0 : 1;
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ParticleAdditive);

            var sound = isHpRecovery ? "_heal_effect.ogg" : "흡기.ogg";
            AudioManager.Instance.OneShotSoundEffect(-1, sound, effect.transform.position, 0.7f);
        }

        private static string[] Textures = { "pok1", "pok3" };

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step > 20 && effect.Primitives.Count == 0)
                return false;
            
            if (step < 20 && step % 4 == 0)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.ParticleUp, effect.Material, 1.5f);
                prim.CreateParts(4);
                prim.Flags[0] = 1; //heal type flickering effect
                var dat = prim.GetPrimitiveData<ParticleUpData>();
                dat.SpriteNames = Textures;
                dat.Atlas = EffectSharedMaterialManager.GetParticleSpriteAtlas();

                var c = effect.Flags[0] == 0
                    ? new Color(220f / 255f, 255f / 255f, 220f / 255f, 1f) //heal hp 
                    : new Color(150 / 255f, 150 / 255f, 250 / 255f, 1f); //heal sp

                for (var i = 0; i < 4; i++)
                {
                    prim.Parts[i] = new EffectPart()
                    {
                        Active = true,
                        Step = 0,
                        Position = VectorHelper.RandomPositionInCylinder(0f, 3.5f, Random.Range(0, 12f)) / 5f,
                        Distance = Random.Range(1.5f, 2.5f) / 10f, //size
                        RiseAngle = Random.Range(0.2f, 0.4f) * 8f, //speed
                        Color = c,
                        RotStart = Random.Range(0, 360f),
                    };
                    prim.Parts[i].Flags[0] = 0; //texture id
                }
            }

            return effect.IsTimerActive;
        }
    }
}