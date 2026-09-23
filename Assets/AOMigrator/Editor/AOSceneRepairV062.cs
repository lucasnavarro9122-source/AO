using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AOSceneRepairV062
{
    const string MapDataPath =
        "Assets/AOMigrator/WorldV06/Data/map_v03.json";

    [Serializable] class MapCell
    {
        public int x;
        public int y;
        public int layer;
        public int grh;
    }

    [Serializable] class MapBlock
    {
        public int x;
        public int y;
        public int flags;
    }

    [Serializable] class MapTrigger
    {
        public int x;
        public int y;
        public int trigger;
    }

    [Serializable] class MapDocument
    {
        public string name;
        public int xmin;
        public int xmax;
        public int ymin;
        public int ymax;
        public MapCell[] cells;
        public MapBlock[] blocks;
        public MapTrigger[] triggers;
    }

    [Serializable] class RepairDiagnostic
    {
        public string version = "0.6.2-alpha";
        public string unity;
        public bool success;
        public string message;
        public bool gridCreated;
        public bool playerRepaired;
        public bool cameraRepaired;
        public int missingScriptsRemoved;
    }

    static readonly HashSet<int> TreeGrh = new HashSet<int> {
        643,644,647,735,1121,2931,11903,11904,11905,14775,11906,
        70885,70884,71042,71041,15698,14504,14505,15697,15510,
        12581,12582,12583,12584,12585,12586,
        12164,12165,12166,12167,12168,12169,12170,12171,12172,
        12173,12174,12175,12176,12177,12178,12179,
        32142,32143,32144,32145,32146,32147,32148,32149,32150,
        32151,32152,32154,
        55626,55627,55628,55629,55630,55631,55632,55633,55634,
        55635,55636,55637,55638,55639,55640,55642,
        50985,50986,50987,50988,50989,50990,50991,
        2547,2548,2549,6597,6598,15108,15109,15110,12160,7220,
        462,463,1877,1878,1879,1880,1881,1890,1892,433,460,461,
        9513,9514,9515,9518,9519,9520,9529,14687,47726,
        12333,12330,20369,21120,21227,21352,12332,21226,
        8258,32118,32119,32129,32132,32133,32135
    };

    [MenuItem("AO Migrador/Reparar escena v0.6.2")]
    public static void RepairMenu()
    {
        try
        {
            string report;
            AOGridMap grid = EnsureScene(out report);
            EditorUtility.DisplayDialog(
                "AO Migrador v0.6.2",
                "Reparacion completada.\n\n" + report +
                "\n\nAOGridMap: " + (grid != null ? "OK" : "NO"),
                "OK");
        }
        catch (Exception e)
        {
            WriteDiagnostic(false, e.ToString(), false, false, false, 0);
            Debug.LogError("AO Scene Repair v0.6.2: " + e);
            EditorUtility.DisplayDialog(
                "AO Migrador v0.6.2 - error",
                e.Message +
                "\n\nSubime Assets/AOMigrator/scene_repair_v062.json.",
                "OK");
        }
    }

    public static AOGridMap EnsureScene(out string report)
    {
        if (EditorApplication.isPlaying)
            throw new Exception("Sali de Play antes de reparar la escena.");

        int removed = 0;
        bool gridCreated = false;
        bool playerRepaired = false;
        bool cameraRepaired = false;

        AOGridMap grid =
            UnityEngine.Object.FindFirstObjectByType<AOGridMap>();

        GameObject mapRoot = FindSceneObjectByPrefix("AO_MAP_");
        if (mapRoot == null)
            throw new Exception(
                "No encontre el objeto raiz AO_MAP_. " +
                "Abri AO_Ciudad_de_Ullathorpe_Playable.");

        if (grid == null)
        {
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                mapRoot);

            MapDocument map = LoadMapDocument();

            grid = mapRoot.GetComponent<AOGridMap>();
            if (grid == null)
                grid = Undo.AddComponent<AOGridMap>(mapRoot);

            RebuildGrid(grid, map);
            gridCreated = true;
        }

        GameObject debugOverlay =
            FindSceneObjectExact("AO Collision Debug");
        if (debugOverlay != null)
            grid.SetDebugOverlay(debugOverlay);

        GameObject playerGo =
            FindSceneObjectExact("AO Test Player");
        if (playerGo != null)
        {
            AOTestPlayer player = playerGo.GetComponent<AOTestPlayer>();
            int px;
            int py;

            if (player != null &&
                grid.InBounds(player.TileX, player.TileY))
            {
                px = player.TileX;
                py = player.TileY;
            }
            else
            {
                px = Mathf.RoundToInt(
                    playerGo.transform.position.x + 0.5f);
                py = Mathf.RoundToInt(
                    -playerGo.transform.position.y);

                px = Mathf.Clamp(px, grid.XMin, grid.XMax);
                py = Mathf.Clamp(py, grid.YMin, grid.YMax);
            }

            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                playerGo);

            player = playerGo.GetComponent<AOTestPlayer>();
            if (player == null)
                player = Undo.AddComponent<AOTestPlayer>(playerGo);

            if (!grid.IsSpawnCandidate(px, py))
            {
                Vector2Int fallback = FindNearestSpawn(grid, px, py);
                px = fallback.x;
                py = fallback.y;
            }

            player.Initialize(grid, px, py);
            playerRepaired = true;
            EditorUtility.SetDirty(player);
        }

        GameObject cameraGo =
            FindSceneObjectExact("AO Follow Camera");
        if (cameraGo != null && playerGo != null)
        {
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                cameraGo);

            Camera cam = cameraGo.GetComponent<Camera>();
            if (cam == null)
                cam = Undo.AddComponent<Camera>(cameraGo);

            AOCameraFollow follow =
                cameraGo.GetComponent<AOCameraFollow>();
            if (follow == null)
                follow = Undo.AddComponent<AOCameraFollow>(cameraGo);

            follow.Initialize(playerGo.transform, grid);
            cameraRepaired = true;
            EditorUtility.SetDirty(follow);
        }

        EditorUtility.SetDirty(grid);
        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene());

        report =
            "AOGridMap reconstruido: " + gridCreated +
            "\nJugador reparado: " + playerRepaired +
            "\nCamara reparada: " + cameraRepaired +
            "\nMissing Scripts eliminados: " + removed;

        WriteDiagnostic(
            true, report, gridCreated,
            playerRepaired, cameraRepaired, removed);

        return grid;
    }

    static MapDocument LoadMapDocument()
    {
        if (!File.Exists(MapDataPath))
            throw new Exception(
                "Falta " + MapDataPath +
                ". Volve a copiar el hotfix v0.6.2 completo.");

        MapDocument map = JsonUtility.FromJson<MapDocument>(
            File.ReadAllText(MapDataPath));

        if (map == null ||
            map.xmin <= 0 ||
            map.xmax < map.xmin ||
            map.ymin <= 0 ||
            map.ymax < map.ymin)
        {
            throw new Exception(
                "map_v03.json no tiene un formato valido.");
        }

        return map;
    }

    static void RebuildGrid(AOGridMap grid, MapDocument map)
    {
        grid.Initialize(
            map.xmin, map.xmax,
            map.ymin, map.ymax);

        if (map.blocks != null)
        {
            foreach (MapBlock block in map.blocks)
                grid.OrFlags(block.x, block.y, block.flags);
        }

        if (map.cells != null)
        {
            foreach (MapCell cell in map.cells)
            {
                if (cell.layer == 1)
                {
                    if (IsWaterGrh(cell.grh))
                        grid.OrFlags(
                            cell.x, cell.y,
                            AOGridMap.FLAG_WATER);
                    else if (IsLavaGrh(cell.grh))
                        grid.OrFlags(
                            cell.x, cell.y,
                            AOGridMap.FLAG_LAVA);
                }
                else if (cell.layer == 2)
                {
                    grid.OrFlags(
                        cell.x, cell.y,
                        AOGridMap.FLAG_COAST);
                }
                else if (
                    cell.layer == 3 &&
                    TreeGrh.Contains(cell.grh))
                {
                    grid.OrFlags(
                        cell.x, cell.y,
                        AOGridMap.FLAG_TREE);
                }
            }
        }

        if (map.triggers != null)
        {
            foreach (MapTrigger trigger in map.triggers)
            {
                grid.SetTrigger(
                    trigger.x, trigger.y,
                    trigger.trigger);

                if (
                    trigger.trigger ==
                        AOGridMap.TRIGGER_WATER_DETAIL ||
                    trigger.trigger ==
                        AOGridMap.TRIGGER_SWIM_VALID ||
                    trigger.trigger ==
                        AOGridMap.TRIGGER_SWIM_COMBINED ||
                    trigger.trigger ==
                        AOGridMap.TRIGGER_SWIM_ROOF)
                {
                    grid.ClearFlags(
                        trigger.x, trigger.y,
                        AOGridMap.FLAG_COAST);
                }
            }
        }
    }

    static Vector2Int FindNearestSpawn(
        AOGridMap grid, int originX, int originY)
    {
        if (grid.IsSpawnCandidate(originX, originY))
            return new Vector2Int(originX, originY);

        int maxRadius = Mathf.Max(grid.Width, grid.Height);
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int y = originY - radius;
                 y <= originY + radius; y++)
            {
                for (int x = originX - radius;
                     x <= originX + radius; x++)
                {
                    if (Mathf.Abs(x - originX) != radius &&
                        Mathf.Abs(y - originY) != radius)
                        continue;

                    if (grid.IsSpawnCandidate(x, y))
                        return new Vector2Int(x, y);
                }
            }
        }

        throw new Exception(
            "No encontre una casilla transitable para reparar al jugador.");
    }

    static bool IsWaterGrh(int grh)
    {
        return
            (grh >= 1505 && grh <= 1520) ||
            (grh >= 124 && grh <= 139) ||
            (grh >= 24223 && grh <= 24238) ||
            (grh >= 24303 && grh <= 24318) ||
            (grh >= 468 && grh <= 483) ||
            (grh >= 44668 && grh <= 44683) ||
            (grh >= 24143 && grh <= 24158) ||
            (grh >= 12628 && grh <= 12643) ||
            (grh >= 2948 && grh <= 2963);
    }

    static bool IsLavaGrh(int grh)
    {
        return
            (grh >= 57400 && grh <= 57415) ||
            (grh >= 16101 && grh <= 16116) ||
            (grh >= 26767 && grh <= 26782);
    }

    static GameObject FindSceneObjectExact(string objectName)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid())
                continue;
            if (go.name == objectName)
                return go;
        }
        return null;
    }

    static GameObject FindSceneObjectByPrefix(string prefix)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid())
                continue;
            if (go.name.StartsWith(
                prefix, StringComparison.Ordinal))
                return go;
        }
        return null;
    }

    static void WriteDiagnostic(
        bool success,
        string message,
        bool gridCreated,
        bool playerRepaired,
        bool cameraRepaired,
        int missingScriptsRemoved)
    {
        try
        {
            RepairDiagnostic diag = new RepairDiagnostic {
                unity = Application.unityVersion,
                success = success,
                message = message,
                gridCreated = gridCreated,
                playerRepaired = playerRepaired,
                cameraRepaired = cameraRepaired,
                missingScriptsRemoved = missingScriptsRemoved
            };

            File.WriteAllText(
                "Assets/AOMigrator/scene_repair_v062.json",
                JsonUtility.ToJson(diag, true));
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "No pude escribir scene_repair_v062.json: " + e);
        }
    }
}
