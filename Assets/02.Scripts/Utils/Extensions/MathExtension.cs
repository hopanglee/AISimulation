using UnityEngine;

public static class MathExtension
{
    public static float SquaredDistance2D(Vector3 pos1, Vector3 pos2)
    {
        float dx = pos1.x - pos2.x;
        float dz = pos1.z - pos2.z;
        return dx * dx + dz * dz;
    }
}
