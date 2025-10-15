using System;

public static class DeterministicRandom
{
    public static Random Instance { get; private set; } = new Random(20001114);

    public static void Initialize(int seed)
    {
        Instance = new Random(seed);
    }
}


