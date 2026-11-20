using System.Collections.Generic;
using System.Linq;

namespace Brodun
{
    public interface IRandomSource { double Next(); }
    // Web B.Rng (Mulberry32): unsigned shifts, low 32-bit multiplication, same Fisher-Yates order.
    public sealed class SeededRandom : IRandomSource
    {
        private uint state;
        public SeededRandom(uint seed) { state = seed; }
        public double Next()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = (state ^ (state >> 15)) * (1u | state);
                t ^= t + ((t ^ (t >> 7)) * (61u | t));
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
    public static class Shuffling
    {
        public static List<T> Shuffle<T>(IEnumerable<T> source, IRandomSource rng)
        {
            var result = source.ToList();
            for (int i = result.Count - 1; i > 0; i--)
            { int j = (int)(rng.Next() * (i + 1)); T value = result[i]; result[i] = result[j]; result[j] = value; }
            return result;
        }
    }
}
