using System;

namespace CheckmateRPG.Core
{
    public sealed class SeededRandomProvider
    {
        private const int DefaultSeed = 1001;
        private readonly object _lock = new();
        private uint _state;

        private static SeededRandomProvider _shared;
        public static SeededRandomProvider Shared => _shared ??= new SeededRandomProvider(DefaultSeed);

        public SeededRandomProvider(int seed)
        {
            Reseed(seed);
        }

        public static void SetGlobalSeed(int seed)
        {
            Shared.Reseed(seed);
        }

        public void Reseed(int seed)
        {
            unchecked
            {
                uint normalized = (uint)seed;
                if (normalized == 0)
                    normalized = 0xA341316Cu;
                _state = normalized;
            }
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");

            uint span = (uint)(maxExclusive - minInclusive);
            uint value = NextUInt();
            return minInclusive + (int)(value % span);
        }

        public int NextInt(int maxExclusive)
        {
            return NextInt(0, maxExclusive);
        }

        public float NextFloat01()
        {
            const float divisor = uint.MaxValue;
            return NextUInt() / divisor;
        }

        public bool NextBool(float trueProbability = 0.5f)
        {
            if (trueProbability <= 0f)
                return false;
            if (trueProbability >= 1f)
                return true;
            return NextFloat01() < trueProbability;
        }

        public Guid NextGuid()
        {
            Span<byte> bytes = stackalloc byte[16];
            for (int i = 0; i < bytes.Length; i += 4)
            {
                uint value = NextUInt();
                bytes[i] = (byte)value;
                bytes[i + 1] = (byte)(value >> 8);
                bytes[i + 2] = (byte)(value >> 16);
                bytes[i + 3] = (byte)(value >> 24);
            }

            return new Guid(bytes);
        }

        private uint NextUInt()
        {
            lock (_lock)
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
        }
    }
}
