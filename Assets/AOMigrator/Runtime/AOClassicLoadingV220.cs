using UnityEngine;

public static class AOClassicLoadingV220
{
    static Texture2D artwork;
    static GUIStyle messageStyle;

    public static void Draw(string message)
    {
        if (artwork == null)
            artwork = Resources.Load<Texture2D>(
                "AOMigrator/EntryUI/eac_splash_art");

        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.font = Resources.Load<Font>(
                "AOMigrator/ClassicUI/Cardo-Regular");
            messageStyle.fontSize = 23;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.alignment = TextAnchor.MiddleCenter;
            messageStyle.normal.textColor = Color.white;
        }

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -200;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (artwork != null)
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                artwork, ScaleMode.ScaleAndCrop, true);

        float bandHeight = Mathf.Max(44f, Screen.height * 0.075f);
        Rect band = new Rect(0, Screen.height - bandHeight,
            Screen.width, bandHeight);
        GUI.color = new Color(0f, 0f, 0f, 0.68f);
        GUI.DrawTexture(band, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(band, message, messageStyle);

        GUI.depth = previousDepth;
        GUI.color = previousColor;
    }
}
