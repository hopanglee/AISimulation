using UnityEngine;

public static class MathExtension
{
    public static float SquaredDistance2D(Vector3 pos1, Vector3 pos2)
    {
        float dx = pos1.x - pos2.x;
        float dy = pos1.y - pos2.y;
        return dx * dx + dy * dy;
    }
}
