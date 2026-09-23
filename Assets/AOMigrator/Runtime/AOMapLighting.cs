using System;
using UnityEngine;

[Serializable] public class AOMapLightPlacement
{
    public int x, y, color, range;
}

[Serializable] public class AOMapEnvironment
{
    public int mapNumber, baseLight;
    public bool rain, snow, fog;
}

[Serializable] public class AOMapEnvironmentLibrary
{
    public string version;
    public AOMapEnvironment[] maps;
}

public static class AOMapLighting
{
    // ModMetereologia.bas DayColors(0..23), indexed by displayed hour.
    static readonly byte[,] Day = {
        {120,120,120},{120,120,120},{120,120,120},{120,120,120},
        {138,138,138},{156,156,145},{170,170,155},{185,185,185},
        {200,200,200},{220,220,220},{235,235,235},{245,245,245},
        {255,255,255},{255,255,255},{255,255,255},{245,245,245},
        {230,230,230},{220,220,220},{200,200,180},{180,160,160},
        {160,160,160},{140,140,140},{120,120,140},{120,120,120}
    };

    // VB6 vertices: lower-left, upper-left, lower-right, upper-right.
    public static Color32[] Build(AOWorldManagerV07.WorldMapData map,
                                  int baseLight, float worldHour)
    {
        int width = map.xmax - map.xmin + 1;
        int height = map.ymax - map.ymin + 1;
        if (width <= 0 || height <= 0) return new Color32[0];
        Color32 ambient = baseLight == 0 ? DayColor(worldHour) : Decode(baseLight);
        Color32[] result = new Color32[width * height * 4];
        for (int i = 0; i < result.Length; i++) result[i] = ambient;
        if (map.lights == null) return result;
        foreach (AOMapLightPlacement light in map.lights)
        {
            int range = light.range >= 100 ? light.range - 99 : light.range;
            if (range <= 0 || range > 128) continue;
            if (light.range >= 100) Round(map, result, width, light, range);
            else Square(map, result, width, light, range);
        }
        return result;
    }

    public static Color32 DayColor(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        int current = Mathf.FloorToInt(hour), previous = (current + 23) % 24;
        return Color32.Lerp(new Color32(Day[previous,0], Day[previous,1], Day[previous,2], 255),
            new Color32(Day[current,0], Day[current,1], Day[current,2], 255), hour - current);
    }

    static void Round(AOWorldManagerV07.WorldMapData map, Color32[] result,
                      int width, AOMapLightPlacement light, int range)
    {
        Color32 color = Decode(light.color);
        float radius = range * 32f + 16f, radius2 = radius * radius;
        for (int y = Mathf.Max(map.ymin, light.y - range); y <= Mathf.Min(map.ymax, light.y + range); y++)
        for (int x = Mathf.Max(map.xmin, light.x - range); x <= Mathf.Min(map.xmax, light.x + range); x++)
        {
            int offset = ((y - map.ymin) * width + x - map.xmin) * 4;
            for (int corner = 0; corner < 4; corner++)
            {
                float vx = x * 32f + (corner >= 2 ? 32f : 0f);
                float vy = y * 32f + ((corner & 1) == 0 ? 32f : 0f);
                float dx = light.x * 32f + 16f - vx, dy = light.y * 32f + 16f - vy;
                float distance2 = dx * dx + dy * dy;
                if (distance2 <= radius2)
                    result[offset + corner] = Color32.Lerp(color,
                        result[offset + corner], distance2 / radius2);
            }
        }
    }

    static void Square(AOWorldManagerV07.WorldMapData map, Color32[] result,
                       int width, AOMapLightPlacement light, int range)
    {
        Color32 color = Decode(light.color);
        for (int y = Mathf.Max(map.ymin, light.y - range); y <= Mathf.Min(map.ymax, light.y + range); y++)
        for (int x = Mathf.Max(map.xmin, light.x - range); x <= Mathf.Min(map.xmax, light.x + range); x++)
        {
            int dx = x - light.x, dy = y - light.y;
            int offset = ((y - map.ymin) * width + x - map.xmin) * 4;
            if (Mathf.Abs(dx) < range && Mathf.Abs(dy) < range)
            { for (int c = 0; c < 4; c++) result[offset + c] = color; continue; }
            // clsLucesCuadradas.Light_Render border and corner assignments.
            if (dx == -range && dy == -range) result[offset + 2] = color;
            if (dx == range && dy == -range) result[offset] = color;
            if (dx == range && dy == range) result[offset + 1] = color;
            if (dx == -range && dy == range) result[offset + 3] = color;
            if (dy == -range && Mathf.Abs(dx) < range)
            { result[offset] = color; result[offset + 2] = color; }
            if (dy == range && Mathf.Abs(dx) < range)
            { result[offset + 1] = color; result[offset + 3] = color; }
            if (dx == -range && Mathf.Abs(dy) < range)
            { result[offset + 2] = color; result[offset + 3] = color; }
            if (dx == range && Mathf.Abs(dy) < range)
            { result[offset] = color; result[offset + 1] = color; }
        }
    }

    static Color32 Decode(int packed) => new Color32(
        (byte)((packed >> 16) & 255), (byte)((packed >> 8) & 255),
        (byte)(packed & 255), 255);
}
