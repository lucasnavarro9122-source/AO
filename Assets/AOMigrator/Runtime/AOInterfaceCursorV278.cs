using UnityEngine;

// V278 · Cursores gráficos del original (clsCursor.cls, recursos 101 y 103 de AO20.res):
// E_ARROW mientras se apunta con arco (UsingSkill = Proyectiles) y E_CAST mientras se apunta un hechizo.
public partial class AOInterfaceV0101
{
    const string CursorArt = "AOMigrator/ClassicUI/Cursores/";

    enum GameCursor { Normal, Arrow, Cast }

    GameCursor currentCursor = GameCursor.Normal;
    Texture2D cursorArrow, cursorCast;
    bool cursorArtLoaded;

    void UpdateGameCursor()
    {
        GameCursor wanted = GameCursor.Normal;
        if (AOMainMenuV140.SessionActive && !InputCaptured)
        {
            if (combat != null && combat.IsRangedTargeting)
                wanted = GameCursor.Arrow;
            else if (magicV120 != null && !string.IsNullOrEmpty(magicV120.TargetPrompt))
                wanted = GameCursor.Cast;
        }
        SetGameCursor(wanted);
    }

    void SetGameCursor(GameCursor wanted)
    {
        if (wanted == currentCursor) return;
        if (!cursorArtLoaded)
        {
            cursorArtLoaded = true;
            cursorArrow = Resources.Load<Texture2D>(CursorArt + "cursor_arrow");
            cursorCast = Resources.Load<Texture2D>(CursorArt + "cursor_cast");
        }

        Texture2D art = wanted == GameCursor.Arrow ? cursorArrow : wanted == GameCursor.Cast ? cursorCast : null;
        if (wanted != GameCursor.Normal && art == null) return;
        // El hotspot del original es la punta de la flecha, arriba a la izquierda.
        Cursor.SetCursor(art, Vector2.zero, CursorMode.Auto);
        currentCursor = wanted;
    }
}
