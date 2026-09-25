using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

public partial class AOInterfaceV0101
{
    enum TopDialog { None, Settings, Manual, Market, Exit, Retos }
    enum SettingsTab { Gameplay, Audio, Video, Controls }

    TopDialog topDialog;
    SettingsTab settingsTab;
    int controlsPage;
    AOGameAction? bindingAction;
    string settingsStatus = "";
    bool leaveSessionNextFrame;
    bool quitNextFrame;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr window, int command);
#endif

    void UpdateTopActions()
    {
        if (!leaveSessionNextFrame && !quitNextFrame)
            return;

        AOSaveGameV140 save = player == null
            ? null : player.GetComponent<AOSaveGameV140>();
        if (AOMainMenuV140.SessionActive && save != null)
            save.SaveGame(false);

        if (leaveSessionNextFrame)
        {
            leaveSessionNextFrame = false;
            topDialog = TopDialog.None;
            AOMainMenuV140.ShowFromCreator();
            return;
        }

        quitNextFrame = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void DrawTopButtons()
    {
        if (invisibleButton == null)
            return;
        if (AOCityUIV130.ModalOpen || AOQuestUIV150.ModalOpen)
            return;

        if (AOAudioV190.Clicked(GUI.Button(R(759, 2, 68, 27),
                                         GUIContent.none, invisibleButton)))
            topDialog = topDialog == TopDialog.Settings
                ? TopDialog.None : TopDialog.Settings;

        if (AOAudioV190.Clicked(GUI.Button(R(830, 2, 68, 27),
                                         GUIContent.none, invisibleButton)))
            topDialog = topDialog == TopDialog.Manual
                ? TopDialog.None : TopDialog.Manual;

        if (AOAudioV190.Clicked(GUI.Button(R(901, 2, 67, 27),
                                         GUIContent.none, invisibleButton)))
            topDialog = topDialog == TopDialog.Market
                ? TopDialog.None : TopDialog.Market;

        if (AOAudioV190.Clicked(GUI.Button(R(973, 2, 21, 27),
                                         GUIContent.none, invisibleButton)))
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr window = GetActiveWindow();
            if (window != IntPtr.Zero) ShowWindow(window, 2);
#else
            PushMessage("Minimizar está disponible en el ejecutable Windows.");
#endif
        }

        if (AOAudioV190.Clicked(GUI.Button(R(999, 2, 21, 27),
                                         GUIContent.none, invisibleButton)))
            topDialog = TopDialog.Exit;
    }

    void DrawTopDialog()
    {
        if (topDialog == TopDialog.None)
            return;

        Event current = Event.current;
        if (topDialog == TopDialog.Settings && bindingAction.HasValue &&
            current != null && (current.type == EventType.KeyDown || current.type == EventType.MouseDown))
        {
            if (current.keyCode == KeyCode.Escape)
                settingsStatus = "Cambio cancelado.";
            else if (current.type == EventType.MouseDown && !bindingRect.Contains(current.mousePosition))
                settingsStatus = "Cambio cancelado (clic fuera del recuadro).";
            else if (AOPlayerSettingsV230.SetKey(bindingAction.Value,
                         current.type == EventType.MouseDown ? KeyCode.Mouse0 + current.button : current.keyCode,
                         out string error))
                settingsStatus = "Tecla actualizada.";
            else
                settingsStatus = error;
            bindingAction = null;
            current.Use();
            return;
        }
        if (current != null && current.type == EventType.KeyDown &&
            current.keyCode == KeyCode.Escape)
        {
            topDialog = TopDialog.None;
            current.Use();
            return;
        }

        GUISkin previousSkin = GUI.skin;
        int previousDepth = GUI.depth;
        GUI.skin = AOClassicSkinV200.Get(previousSkin);
        GUI.depth = -85;

        if (topDialog == TopDialog.Settings)
        {
            DrawSettingsDialog();
            GUI.depth = previousDepth;
            GUI.skin = previousSkin;
            return;
        }

        if (topDialog == TopDialog.Retos)
        {
            DrawDuelForm();
            GUI.depth = previousDepth;
            GUI.skin = previousSkin;
            return;
        }

        GUI.Box(R(263, 186, 500, 344), "");
        GUI.Label(R(294, 215, 440, 31), DialogTitle());

        if (topDialog == TopDialog.Manual)
        {
            GUI.Label(R(294, 260, 440, 105),
                (AOPlayerSettingsV230.IsMoba ? "MOBA: " + AOPlayerSettingsV230.KeyName(AOGameAction.WorldCommand) + " mover/atacar" : "AO · Mover: " + AOPlayerSettingsV230.KeyName(AOGameAction.MoveUp) +
                "/" + AOPlayerSettingsV230.KeyName(AOGameAction.MoveLeft) +
                "/" + AOPlayerSettingsV230.KeyName(AOGameAction.MoveDown) +
                "/" + AOPlayerSettingsV230.KeyName(AOGameAction.MoveRight)) +
                "   ·   Interactuar: " + AOPlayerSettingsV230.KeyName(AOGameAction.Interact) + "\n" +
                "Inventario: " + AOPlayerSettingsV230.KeyName(AOGameAction.Inventory) +
                "   ·   Mapa: " + AOPlayerSettingsV230.KeyName(AOGameAction.Map) +
                "   ·   Misiones: " + AOPlayerSettingsV230.KeyName(AOGameAction.Quests) + "\n" +
                "Guardar: " + AOPlayerSettingsV230.KeyName(AOGameAction.QuickSave) +
                "   ·   Cargar: " + AOPlayerSettingsV230.KeyName(AOGameAction.QuickLoad) + "\n" +
                "Retos: /RETAR · /ACEPTAR nombre · /CANCELAR · /ABANDONAR · /RETOS");
            if (AOAudioV190.Clicked(GUI.Button(R(294, 382, 440, 35),
                                             "Abrir wiki original")))
                Application.OpenURL("https://www.argentumonline.com.ar/wiki");
        }
        else if (topDialog == TopDialog.Market)
        {
            GUI.Label(R(294, 263, 440, 78),
                "Mercado AO es un servicio web externo del cliente original.");
            if (AOAudioV190.Clicked(GUI.Button(R(294, 382, 440, 35),
                                             "Abrir Mercado AO")))
                Application.OpenURL("https://www.argentumonline.com.ar/mercadoao");
        }
        else
        {
            GUI.Label(R(294, 263, 440, 72),
                      "Se guardará la partida antes de salir.");
            if (AOAudioV190.Clicked(GUI.Button(R(294, 365, 210, 40),
                                             "Volver al menú")))
                leaveSessionNextFrame = true;
            if (AOAudioV190.Clicked(GUI.Button(R(524, 365, 210, 40),
                                             "Cerrar juego")))
                quitNextFrame = true;
        }

        if (AOAudioV190.Clicked(GUI.Button(R(597, 470, 137, 33), "Cerrar")))
            topDialog = TopDialog.None;

        GUI.depth = previousDepth;
        GUI.skin = previousSkin;
    }

    void DrawSettingsDialog()
    {
        GUI.Box(R(170, 88, 684, 594), "");
        GUI.Label(R(204, 112, 610, 34), "AJUSTES");

        string[] tabs = { "JUEGO", "AUDIO", "VIDEO", "CONTROLES" };
        for (int i = 0; i < tabs.Length; i++)
        {
            if (AOAudioV190.Clicked(GUI.Button(R(202 + i * 153, 158, 145, 38),
                    tabs[i])))
            {
                settingsTab = (SettingsTab)i;
                bindingAction = null;
                settingsStatus = "";
            }
        }

        switch (settingsTab)
        {
            case SettingsTab.Gameplay: DrawGameplaySettings(); break;
            case SettingsTab.Audio: DrawAudioSettings(); break;
            case SettingsTab.Video: DrawVideoSettings(); break;
            case SettingsTab.Controls: DrawControlSettings(); break;
        }

        if (!string.IsNullOrEmpty(settingsStatus))
            GUI.Label(R(203, 604, 472, 53), settingsStatus, new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = Mathf.Max(10, Mathf.RoundToInt(14 * scale)) });
        if (AOAudioV190.Clicked(GUI.Button(R(687, 627, 133, 35), "Cerrar")))
        {
            bindingAction = null;
            topDialog = TopDialog.None;
        }
    }

    void DrawGameplaySettings()
    {
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !AOPlayerSettingsV230.IsMoba;
        bool arrows = GUI.Toggle(R(208, 226, 545, 38),
            AOPlayerSettingsV230.ArrowMovement, "Flechas alternativas para mover (AO)");
        GUI.enabled = previousEnabled;
        if (arrows != AOPlayerSettingsV230.ArrowMovement)
        {
            AOPlayerSettingsV230.ArrowMovement = arrows;
            if (arrows && !AOPlayerSettingsV230.ArrowMovement)
                settingsStatus = "Una flecha está asignada a otra acción.";
        }

        bool speech = GUI.Toggle(R(208, 283, 545, 38),
            AOPlayerSettingsV230.ShowSpeech,
            "Mostrar mensajes sobre el personaje");
        if (speech != AOPlayerSettingsV230.ShowSpeech)
            AOPlayerSettingsV230.ShowSpeech = speech;

        bool centered = GUI.Toggle(R(208, 340, 545, 38),
            AOPlayerSettingsV230.CenteredMinimap, "Minimapa centrado");
        if (centered != AOPlayerSettingsV230.CenteredMinimap)
            AOPlayerSettingsV230.CenteredMinimap = centered;

        bool mapName = GUI.Toggle(R(208, 397, 545, 38),
            AOPlayerSettingsV230.ShowMapNumber, "Mostrar número del mapa");
        if (mapName != AOPlayerSettingsV230.ShowMapNumber)
            AOPlayerSettingsV230.ShowMapNumber = mapName;

        GUI.Label(R(208, 488, 570, 67),
            "Estas preferencias son locales y no cambian la partida.\n" +
            "Enter abre el chat y Escape cierra ventanas.");
    }

    void DrawAudioSettings()
    {
        DrawVolumeSetting("Volumen de efectos", 210,
            AOPlayerSettingsV230.Effects, value => AOPlayerSettingsV230.Effects = value);
        DrawVolumeSetting("Volumen de pasos", 304,
            AOPlayerSettingsV230.Footsteps, value => AOPlayerSettingsV230.Footsteps = value);
        DrawVolumeSetting("Ambiente (lluvia)", 398,
            AOPlayerSettingsV230.Ambient, value => AOPlayerSettingsV230.Ambient = value);

        bool music = GUI.Toggle(R(208, 505, 545, 38),
            AOPlayerSettingsV230.Music, "Música de inicio y mapas");
        if (music != AOPlayerSettingsV230.Music)
            AOPlayerSettingsV230.Music = music;
        GUI.Label(R(208, 548, 585, 31),
            "La música MIDI original admite encendido y apagado.");
    }

    void DrawVolumeSetting(string label, float y, float current,
                           System.Action<float> change)
    {
        GUI.Label(R(208, y, 420, 30), label);
        float value = GUI.HorizontalSlider(R(208, y + 39, 485, 24),
            current, 0f, 1f);
        GUI.Label(R(709, y + 31, 100, 32),
            Mathf.RoundToInt(value * 100f) + "%");
        if (Mathf.Abs(value - current) > 0.005f)
            change(value);
    }

    bool lightingApplied;

    // Aplica la luz guardada una vez por escena (AOLighting2DV283 la cambia al instante).
    void ApplySavedLightingOnce()
    {
        if (lightingApplied) return;
        lightingApplied = true;
        AOLighting2DV283.SetEnhanced(AOPlayerSettingsV230.EnhancedLighting);
    }

    void DrawVideoSettings()
    {
        bool fullscreen = GUI.Toggle(R(208, 226, 545, 38),
            AOPlayerSettingsV230.Fullscreen, "Pantalla completa");
        if (fullscreen != AOPlayerSettingsV230.Fullscreen)
            AOPlayerSettingsV230.Fullscreen = fullscreen;

        bool vsync = GUI.Toggle(R(208, 283, 545, 38),
            AOPlayerSettingsV230.VSync, "Sincronización vertical");
        if (vsync != AOPlayerSettingsV230.VSync)
            AOPlayerSettingsV230.VSync = vsync;

        bool fps = GUI.Toggle(R(208, 340, 545, 38),
            AOPlayerSettingsV230.ShowFps, "Mostrar FPS");
        if (fps != AOPlayerSettingsV230.ShowFps)
            AOPlayerSettingsV230.ShowFps = fps;

        bool light = GUI.Toggle(R(208, 397, 545, 38),
            AOPlayerSettingsV230.EnhancedLighting, "Luz mejorada (no original: noche, antorchas y brillo)");
        if (light != AOPlayerSettingsV230.EnhancedLighting)
        {
            AOPlayerSettingsV230.EnhancedLighting = light;
            AOLighting2DV283.SetEnhanced(light);
        }

        bool hd = GUI.Toggle(R(208, 454, 545, 38),
            AOPlayerSettingsV230.HDTextures, "Gráficos HD (remaster; apagado = original)");
        if (hd != AOPlayerSettingsV230.HDTextures)
        {
            AOPlayerSettingsV230.HDTextures = hd;
            AOWorldManagerV07.SetHDTextures(hd);
        }

        GUI.Label(R(208, 508, 570, 50),
            "En el editor, pantalla completa queda guardada\n" +
            "para el ejecutable del juego.");
    }

    void DrawControlSettings() { DrawControlProfiles(); }

    string DialogTitle()
    {
        switch (topDialog)
        {
            case TopDialog.Settings: return "AJUSTES";
            case TopDialog.Manual: return "MANUAL";
            case TopDialog.Market: return "MERCADO AO";
            case TopDialog.Exit: return "SALIR";
            case TopDialog.Retos: return "RETOS";
            default: return "";
        }
    }
}
