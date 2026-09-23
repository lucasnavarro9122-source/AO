using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

public partial class AOInterfaceV0101
{
    enum TopDialog { None, Settings, Manual, Market, Exit }

    TopDialog topDialog;
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

        GUISkin previousSkin = GUI.skin;
        int previousDepth = GUI.depth;
        GUI.skin = AOClassicSkinV200.Get(previousSkin);
        GUI.depth = -85;

        GUI.Box(R(263, 186, 500, 344), "");
        GUI.Label(R(294, 215, 440, 31), DialogTitle());

        if (topDialog == TopDialog.Settings)
        {
            GUI.Label(R(294, 263, 440, 25), "Volumen general");
            AudioListener.volume = GUI.HorizontalSlider(
                R(294, 297, 440, 22), AudioListener.volume, 0f, 1f);
            GUI.Label(R(294, 327, 440, 24),
                      Mathf.RoundToInt(AudioListener.volume * 100f) + "%");
            bool fullscreen = GUI.Toggle(R(294, 372, 300, 26),
                                         Screen.fullScreen, "Pantalla completa");
            if (fullscreen != Screen.fullScreen)
                Screen.fullScreen = fullscreen;
        }
        else if (topDialog == TopDialog.Manual)
        {
            GUI.Label(R(294, 260, 440, 105),
                "WASD / flechas: mover   ·   E: interactuar\n" +
                "I: inventario   ·   M: mapa   ·   Q: misiones\n" +
                "F1: guardar   ·   F3: cargar   ·   F9: personaje");
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

    string DialogTitle()
    {
        switch (topDialog)
        {
            case TopDialog.Settings: return "AJUSTES";
            case TopDialog.Manual: return "MANUAL";
            case TopDialog.Market: return "MERCADO AO";
            case TopDialog.Exit: return "SALIR";
            default: return "";
        }
    }
}
