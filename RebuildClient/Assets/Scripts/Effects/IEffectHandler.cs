namespace Assets.Scripts.Effects
{
    public interface IEffectHandler
    {
        public bool Update(Ragnarok3dEffect effect, float pos, int step) => true;
        public void OnEvent(Ragnarok3dEffect effect, RagnarokPrimitive sender) {}
    }
}