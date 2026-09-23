using System;
using System.Collections.Generic;
using UnityEngine;

public static class AODoorCatalogV210
{
    [Serializable]
    public class DoorDef
    {
        public int objIndex;
        public bool locked;
        public AOWorldManagerV07.FrameSpec openFrame;
    }

    [Serializable]
    class Catalog
    {
        public DoorDef[] doors;
    }

    static Dictionary<int, DoorDef> byIndex;

    public static DoorDef Get(int index)
    {
        if (byIndex == null)
        {
            byIndex = new Dictionary<int, DoorDef>();
            TextAsset source = Resources.Load<TextAsset>(
                "AOMigrator/WorldV07/door_catalog");
            if (source != null)
            {
                Catalog data = JsonUtility.FromJson<Catalog>(source.text);
                if (data != null && data.doors != null)
                    foreach (DoorDef door in data.doors)
                        byIndex[door.objIndex] = door;
            }
        }
        return byIndex.TryGetValue(index, out DoorDef value) ? value : null;
    }
}
