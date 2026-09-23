using UnityEngine;

public static class AOClassicSkinV200
{
    static GUISkin skin;

    public static GUISkin Get(GUISkin baseSkin)
    {
        if (skin != null) return skin;
        skin = Object.Instantiate(baseSkin);

        const string root = "AOMigrator/ClassicUI/";
        Texture2D frame = Resources.Load<Texture2D>(root + "frame");
        Texture2D leather = Resources.Load<Texture2D>(root + "leather_black");
        Texture2D button = Resources.Load<Texture2D>(root + "button_gray_default");
        Texture2D over = Resources.Load<Texture2D>(root + "button_gray_over");
        Font font = Resources.Load<Font>(root + "Cardo-Regular");

        if (frame != null) skin.window.normal.background = frame;
        if (leather != null) skin.box.normal.background = leather;
        if (button != null) skin.button.normal.background = button;
        if (over != null)
        {
            skin.button.hover.background = over;
            skin.button.active.background = over;
        }

        skin.button.border = new RectOffset(12, 12, 10, 10);
        skin.window.border = new RectOffset(18, 18, 18, 18);
        skin.box.border = new RectOffset(10, 10, 10, 10);
        skin.window.padding = new RectOffset(20, 20, 34, 18);

        Color text = new Color(0.93f, 0.87f, 0.74f);
        GUIStyle[] styles = { skin.window, skin.box, skin.label,
                              skin.button, skin.textField, skin.toggle };
        foreach (GUIStyle style in styles)
        {
            if (font != null) style.font = font;
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            style.focused.textColor = text;
        }
        skin.window.fontSize = 17;
        skin.window.fontStyle = FontStyle.Bold;
        skin.button.fontSize = 15;
        skin.label.fontSize = 14;
        skin.textField.fontSize = 15;
        return skin;
    }
}
