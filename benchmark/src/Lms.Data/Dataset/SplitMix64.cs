namespace Lms.Data.Dataset;

/// <summary>
/// SplitMix64 PRNG. Used instead of System.Random so the generated dataset is bit-identical across
/// .NET versions and operating systems (doc 03 §2.3: same seed ⇒ identical output).
/// Modulo reduction has a negligible, deterministic bias; acceptable for synthetic data.
/// </summary>
public sealed class SplitMix64(ulong seed)
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform-ish integer in [0, maxExclusive).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    /// <summary>Independent sub-stream for one table, so changing one table's generation never shifts another's.</summary>
    public static SplitMix64 ForStream(ulong seed, ulong streamId) => new(seed ^ (streamId * 0xD1B54A32D192ED03UL));
}
