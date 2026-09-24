using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using InputKey = UnityEngine.InputSystem.Key;
#endif

// Local preferences are separate from character saves.
public enum AOGameAction
{
    MoveUp, MoveDown, MoveLeft, MoveRight, Interact,
    Attack, AttackAlternate, PickUp, Inventory, Map,
    Quests, Character, QuickSave, QuickLoad,
    WorldCommand, Stop, Meditate, Spell1, Spell2, Spell3, Spell4,
    Consumable1, Consumable2, Consumable3, Consumable4
}

public static partial class AOPlayerSettingsV230
{
    #if UNITY_EDITOR
    public static string TestPrefixOverride;
#endif
    static string Prefix {
        get {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(TestPrefixOverride)) return TestPrefixOverride;
#endif
            return "AO.PlayerSettings.v1.";
        }
    }
    static bool initialized;
    static float effects = 1f;
    static float footsteps = 1f;
    static float ambient = 1f;
    static bool music = true;
    static bool arrowMovement = true;
    static bool showFps;
    static bool fullscreen;
    static bool vSync;
    static bool showSpeech = true;
    static bool centeredMinimap;
    static bool showMapNumber = true;
    static readonly KeyCode[] defaults =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
        KeyCode.E, KeyCode.LeftControl, KeyCode.Space,
        KeyCode.G, KeyCode.I, KeyCode.M, KeyCode.Q,
        KeyCode.F9, KeyCode.F1, KeyCode.F3,
        KeyCode.None, KeyCode.X, KeyCode.B, KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8,
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnStart()
    {
        initialized = false;
        Ensure();
    }

    static void Ensure()
    {
        if (initialized) return;
        initialized = true;
        effects = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "effects", 1f));
        footsteps = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "footsteps", 1f));
        ambient = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "ambient", 1f));
        music = PlayerPrefs.GetInt(Prefix + "music", 1) != 0;
        arrowMovement = PlayerPrefs.GetInt(Prefix + "arrows", 1) != 0;
        showFps = PlayerPrefs.GetInt(Prefix + "fps", 0) != 0;
        vSync = PlayerPrefs.GetInt(Prefix + "vsync", 0) != 0;
        showSpeech = PlayerPrefs.GetInt(Prefix + "speech", 1) != 0;
        centeredMinimap = PlayerPrefs.GetInt(Prefix + "centeredMinimap", 0) != 0;
        showMapNumber = PlayerPrefs.GetInt(Prefix + "mapNumber", 1) != 0;
        fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", Screen.fullScreen ? 1 : 0) != 0;
        AudioListener.volume = effects;
        QualitySettings.vSyncCount = vSync ? 1 : 0;
#if !UNITY_EDITOR
        Screen.fullScreen = fullscreen;
#endif
    }

    public static float Effects { get { Ensure(); return effects; } set {
        Ensure(); effects = Mathf.Clamp01(value);
        AudioListener.volume = effects;
        SaveFloat("effects", effects);
    } }
    public static float Footsteps { get { Ensure(); return footsteps; } set {
        Ensure(); footsteps = Mathf.Clamp01(value); SaveFloat("footsteps", footsteps);
    } }
    public static float Ambient { get { Ensure(); return ambient; } set {
        Ensure(); ambient = Mathf.Clamp01(value); SaveFloat("ambient", ambient);
        AOAudioV190.RefreshAmbientPreference();
    } }
    public static bool Music { get { Ensure(); return music; } set {
        Ensure(); music = value; SaveBool("music", music);
        AOAudioV190.RefreshMusicPreference();
    } }
    public static bool ArrowMovement { get { Ensure(); return !IsMoba && arrowMovement; } set {
        Ensure();
        if (value && !CanEnableArrowMovement()) return;
        arrowMovement = value;
        SaveBool("arrows", arrowMovement);
    } }
    public static bool ShowFps { get { Ensure(); return showFps; } set {
        Ensure(); showFps = value; SaveBool("fps", showFps);
    } }
    public static bool VSync { get { Ensure(); return vSync; } set {
        Ensure(); vSync = value; SaveBool("vsync", vSync);
        QualitySettings.vSyncCount = vSync ? 1 : 0;
    } }
    public static bool ShowSpeech { get { Ensure(); return showSpeech; } set {
        Ensure(); showSpeech = value; SaveBool("speech", showSpeech);
    } }
    public static bool CenteredMinimap { get { Ensure(); return centeredMinimap; } set {
        Ensure(); centeredMinimap = value; SaveBool("centeredMinimap", centeredMinimap);
    } }
    public static bool ShowMapNumber { get { Ensure(); return showMapNumber; } set {
        Ensure(); showMapNumber = value; SaveBool("mapNumber", showMapNumber);
    } }
    public static bool Fullscreen { get { Ensure(); return fullscreen; } set {
        Ensure(); fullscreen = value; SaveBool("fullscreen", fullscreen);
#if !UNITY_EDITOR
        Screen.fullScreen = value;
#endif
    } }

    static void SaveFloat(string name, float value)
    {
        PlayerPrefs.SetFloat(Prefix + name, value);
        PlayerPrefs.Save();
    }

    static void SaveBool(string name, bool value)
    {
        PlayerPrefs.SetInt(Prefix + name, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    static bool IsArrow(KeyCode key) => key == KeyCode.UpArrow ||
        key == KeyCode.DownArrow || key == KeyCode.LeftArrow ||
        key == KeyCode.RightArrow;

    static KeyCode MovementArrow(AOGameAction action)
    {
        switch (action)
        {
            case AOGameAction.MoveUp: return KeyCode.UpArrow;
            case AOGameAction.MoveDown: return KeyCode.DownArrow;
            case AOGameAction.MoveLeft: return KeyCode.LeftArrow;
            case AOGameAction.MoveRight: return KeyCode.RightArrow;
            default: return KeyCode.None;
        }
    }

    public static bool CanEnableArrowMovement()
    {
        foreach (AOGameAction action in Enum.GetValues(typeof(AOGameAction)))
        {
            KeyCode key = Key(action);
            if (ActionEnabled(action) && IsArrow(key) && key != MovementArrow(action)) return false;
        }
        return true;
    }

    static bool ValidKey(KeyCode key) => key == KeyCode.None ||
        (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse4) ||
        key == KeyCode.LeftShift || key == KeyCode.RightShift ||
        key == KeyCode.LeftAlt || key == KeyCode.RightAlt ||
        (key >= KeyCode.A && key <= KeyCode.Z) ||
        (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) ||
        (key >= KeyCode.F1 && key <= KeyCode.F12) ||
        IsArrow(key) || key == KeyCode.Space || key == KeyCode.Tab ||
        key == KeyCode.LeftControl || key == KeyCode.RightControl;

    public static bool Held(AOGameAction action)
    {
        if (!ActionEnabled(action)) return false;
        if (PhysicalHeld(Key(action))) return true;
        if (!ArrowMovement) return false;
        switch (action)
        {
            case AOGameAction.MoveUp: return PhysicalHeld(KeyCode.UpArrow);
            case AOGameAction.MoveDown: return PhysicalHeld(KeyCode.DownArrow);
            case AOGameAction.MoveLeft: return PhysicalHeld(KeyCode.LeftArrow);
            case AOGameAction.MoveRight: return PhysicalHeld(KeyCode.RightArrow);
            default: return false;
        }
    }

    public static bool Pressed(AOGameAction action) => ActionEnabled(action) && PhysicalPressed(Key(action));

    static bool PhysicalHeld(KeyCode code)
    {
        if (code == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM
        if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse4) return MouseButton(code)?.isPressed ?? false;
        InputKey key = ToInputKey(code);
        return Keyboard.current != null && key != InputKey.None &&
            Keyboard.current[key].isPressed;
#else
        return Input.GetKey(code);
#endif
    }

    static bool PhysicalPressed(KeyCode code)
    {
        if (code == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM
        if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse4) return MouseButton(code)?.wasPressedThisFrame ?? false;
        InputKey key = ToInputKey(code);
        return Keyboard.current != null && key != InputKey.None &&
            Keyboard.current[key].wasPressedThisFrame;
#else
        return Input.GetKeyDown(code);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    static UnityEngine.InputSystem.Controls.ButtonControl MouseButton(KeyCode code)
    {
        var mouse=Mouse.current;if(mouse==null)return null;
        switch(code) {
            case KeyCode.Mouse0:return mouse.leftButton;
            case KeyCode.Mouse1:return mouse.rightButton;
            case KeyCode.Mouse2:return mouse.middleButton;
            case KeyCode.Mouse3:return mouse.backButton;
            case KeyCode.Mouse4:return mouse.forwardButton;
            default:return null;
        }
    }
    static InputKey ToInputKey(KeyCode code)
    {
        if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9)
            return Enum.TryParse("Digit" + ((int)code - (int)KeyCode.Alpha0),
                out InputKey digit) ? digit : InputKey.None;
        if (code == KeyCode.LeftControl) return InputKey.LeftCtrl;
        if (code == KeyCode.RightControl) return InputKey.RightCtrl;
        return Enum.TryParse(code.ToString(), out InputKey key) ? key : InputKey.None;
    }
#endif
}
