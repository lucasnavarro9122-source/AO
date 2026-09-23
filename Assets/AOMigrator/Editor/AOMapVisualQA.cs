using UnityEditor;
using UnityEngine;

public static class AOMapVisualQA
{
    [MenuItem("AO Migrador/QA visual/Mapa 1 - Ciudad")]
    static void ShowCity()
    {
        Load(1, 50, 50);
    }

    [MenuItem("AO Migrador/QA visual/Mapa 2 - Exterior")]
    static void ShowExterior()
    {
        Load(2, 50, 50);
    }

    [MenuItem("AO Migrador/QA visual/Mapa 40 - Interior")]
    static void ShowInterior()
    {
        Load(40, 46, 16);
    }

    [MenuItem("AO Migrador/QA visual/Mapa 40 - Antorchas")]
    static void ShowInteriorLights()
    {
        Load(40, 46, 28);
    }

    [MenuItem("AO Migrador/QA visual/Mapa 4 - NPC migrados")]
    static void ShowMigratedNPCs()
    {
        Load(4, 33, 49);
    }

    [MenuItem("AO Migrador/QA visual/Clima - Lluvia (mapa 1)")]
    static void ShowRain()
    {
        AOWorldManagerV07 world = Load(1, 50, 50);
        if (world != null)
            world.SetWeather(AOMapWeather.Precipitation.Rain, 0);
    }

    [MenuItem("AO Migrador/QA visual/Clima - Nieve (mapa 119)")]
    static void ShowSnow()
    {
        AOWorldManagerV07 world = Load(119, 50, 50);
        if (world != null)
            world.SetWeather(AOMapWeather.Precipitation.Snow, 0);
    }

    [MenuItem("AO Migrador/QA visual/Clima - Niebla (mapa 126)")]
    static void ShowFog()
    {
        AOWorldManagerV07 world = Load(126, 50, 50);
        if (world != null)
            world.SetWeather(AOMapWeather.Precipitation.None, 75);
    }

    [MenuItem("AO Migrador/QA visual/Clima - Despejado")]
    static void ClearWeather()
    {
        AOWorldManagerV07 world =
            Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world != null && EditorApplication.isPlaying)
            world.SetWeather(AOMapWeather.Precipitation.None, 0);
    }

    [MenuItem("AO Migrador/QA visual/Transición 1 a 2")]
    static void TestExit()
    {
        AOWorldManagerV07 world = Load(1, 50, 91, false);
        if (world == null)
            return;

        int frames = 0;
        void CheckExit()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= CheckExit;
                return;
            }

            if (world.CurrentMapNumber == 2)
            {
                Debug.Log("AO_MAP_EXIT_QA_OK from=1 to=2");
                EditorApplication.update -= CheckExit;
            }
            else if (++frames > 180)
            {
                Debug.LogError("AO_MAP_EXIT_QA_FAILED current=" +
                               world.CurrentMapNumber +
                               " inputCaptured=" + AOInterfaceV0101.InputCaptured);
                EditorApplication.update -= CheckExit;
            }
        }

        EditorApplication.update += CheckExit;
    }

    static AOWorldManagerV07 Load(int mapNumber, int x, int y,
                                  bool suppressArrivalExit = true)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Entrá en Play antes de abrir un mapa de prueba.");
            return null;
        }

        AOWorldManagerV07 world =
            Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world == null)
        {
            Debug.LogError("No encontré AOWorldManagerV07 en la escena.");
            return null;
        }

        AOMainMenuV140 menu =
            Object.FindFirstObjectByType<AOMainMenuV140>();
        if (menu != null)
            menu.HideForVisualQA();

        AOCharacterCreationV170 creator =
            Object.FindFirstObjectByType<AOCharacterCreationV170>();
        if (creator != null)
            creator.HideForVisualQA();

        Time.timeScale = 1f;
        world.LoadMap(mapNumber, x, y, suppressArrivalExit);
        Debug.Log("AO_MAP_VISUAL_QA_OK map=" + world.CurrentMapNumber +
                  " name=" + world.CurrentMapName);
        return world;
    }
}
