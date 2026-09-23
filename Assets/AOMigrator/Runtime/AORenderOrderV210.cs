using UnityEngine;

// AO draws each map row in order: characters, large objects, then layer 3.
public static class AORenderOrderV210
{
    const int Base = 10000;
    const int RowStride = 16;

    public static int Character(float tileY) =>
        Base + Mathf.RoundToInt(tileY * RowStride);

    public static int LargeObject(int tileY) =>
        Base + tileY * RowStride + 7;

    public static int Layer3(int tileY) =>
        Base + tileY * RowStride + 8;
}
