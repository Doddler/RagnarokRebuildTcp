namespace RebuildSharedData.Data
{
    public struct FloatPosition : IEquatable<FloatPosition>
    {
        public float X;
        public float Y;

        public FloatPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        public FloatPosition(Position p)
        {
            X = p.X + 0.5f;
            Y = p.Y + 0.5f;
        }

        public float DistanceTo(FloatPosition target)
        {
            var xLen = X - target.X;
            var yLen = Y - target.Y;
            return MathF.Sqrt(xLen * xLen + yLen * yLen);
        }

        private float Lerp(float a, float b, float val) => a * (1 - val) + b * val;

        public FloatPosition Lerp(FloatPosition dest, float by) => new(Lerp(X, dest.X, by), Lerp(Y, dest.Y, by));

        public static implicit operator Position(FloatPosition srcFloatPosition) => new Position((int)srcFloatPosition.X, (int)srcFloatPosition.Y);

        public static implicit operator FloatPosition(Position srcPosition) => new FloatPosition(srcPosition);

        public bool Equals(FloatPosition other)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"{X.ToString()},{Y.ToString()}";
        }
    }
}
