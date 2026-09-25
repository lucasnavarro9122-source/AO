using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// V272 · Retos de la Demo AO BATTLESERVER: formulario (frmRetos), aviso de invitación, conteo,
// marcador, resultado, carteles de los rings, menú de usuario y comandos del chat (docs/claude/demo/ui.md).
public partial class AOInterfaceV0101
{
    const string DuelArt = "AOMigrator/ClassicUI/Retos/";
    const float DuelFormW = 291f;
    const float DuelFormH = 490f;

    Texture2D duelWindow, duelField, duelFieldShort, duelCheck;
    Texture2D duelRetarOver, duelRetarOff, duelCloseOver, duelCloseOff;
    Texture2D duelPlusOver, duelPlusOff, duelMinusOver, duelMinusOff;
    Texture2D duelAccept, duelAcceptOver, duelReject, duelRejectOver, duelCancel, duelCancelOver;
    bool duelArtLoaded;

    readonly string[] duelNames = new string[AODuelUI.MaxPerTeam * 2];
    int duelTeamSize = 1;
    string duelBet = AODuelUI.MinBet.ToString();
    bool duelPotionLimit;
    string duelPotions = "5";
    string duelFormError = "";
    bool duelOpenForm;
    string[] duelPrefill;

    string duelMenuName = "";
    Vector2 duelMenuPos;
    AOCharacterIdentityV170 duelIdentity;

    GUIStyle duelFieldStyle, duelFieldCenter, duelLocalName, duelCountStyle, duelErrorStyle, duelHintStyle;
    GUIStyle duelBigStyle, duelBigShadow, duelMidStyle, duelMidShadow;
    GUIStyle duelTitleStyle, duelBodyStyle, duelBodyCenter, duelFootStyle, duelRingStyle, duelRingShadow;
    float duelStyleScale = -1f;

    // ---------- Enganches (Update, OnGUI, DrawTopDialog, DrawOriginalInfoButtons, SubmitChat) ----------

    void UpdateDuel()
    {
        if (!AOMainMenuV140.SessionActive)
        {
            if (AODuelUI.InDuel || AODuelUI.ChallengePending || AODuelUI.Invites.Count > 0)
                AODuelUI.ResetAll();
            duelMenuName = "";
            return;
        }

        // Online, el nombre lo pone el cliente de red al conectar (es el que usa el servidor en los equipos).
        if (!AODuelUI.Available)
        {
            if (duelIdentity == null && player != null)
                duelIdentity = player.GetComponent<AOCharacterIdentityV170>();
            AODuelUI.LocalName = duelIdentity != null && !string.IsNullOrEmpty(duelIdentity.CharacterName)
                ? duelIdentity.CharacterName : "Aventurero";
        }
        AODuelUI.Tick();

        if (duelOpenForm)
        {
            duelOpenForm = false;
            OpenDuelFormNow();
        }
        UpdateDuelUserMenuInput();
    }

    void DrawDuelOverlay()
    {
        if (!AOMainMenuV140.SessionActive || gameCamera == null)
            return;
        LoadDuelArt();
        EnsureDuelStyles();

        if (Event.current.type == EventType.Repaint)
        {
            AODuelUI.ClearDrawnRects();
            AODuelUI.ViewportRect = GameViewRectGUI();
        }

        Color old = GUI.color;
        DrawDuelRingLabels();
        DrawDuelScoreboard();
        DrawDuelCenter();
        DrawDuelNotice();
        DrawDuelUserMenu();
        GUI.color = old;
    }

    void DrawDuelInfoButton()
    {
        // Botón RETOS del panel inferior derecho, en su lugar original (frmMain.Retar dentro de panelInf).
        if (AOAudioV190.Clicked(GUI.Button(R(765f, 672f, 36f, 33f), GUIContent.none, invisibleButton)))
            OpenDuelForm(null);
    }

    bool TryHandleDuelCommand(string clean)
    {
        if (string.IsNullOrEmpty(clean) || clean[0] != '/')
            return false;

        int space = clean.IndexOf(' ');
        string command = (space < 0 ? clean : clean.Substring(0, space)).ToUpperInvariant();
        string args = space < 0 ? "" : clean.Substring(space + 1).Trim();
        string error;

        switch (command)
        {
            case "/RETAR":
            case "/RETO":
            case "/CHALLENGE":
                OpenDuelForm(args.Length > 0
                    ? args.Split(new[] { '@' }, StringSplitOptions.RemoveEmptyEntries)
                    : null);
                return true;
            case "/ACEPTAR":
            case "/ACCEPT":
                if (args.Length == 0)
                    PushMessage("Faltan parámetros. Utilizá /ACEPTAR NOMBRE.");
                else if (!AODuelUI.SendAccept(args, out error))
                    PushMessage(error);
                return true;
            case "/CANCELAR":
            case "/CANCEL":
                if (!AODuelUI.SendCancel(out error)) PushMessage(error);
                return true;
            case "/ABANDONAR":
            case "/ABANDON":
                if (!AODuelUI.SendAbandon(out error)) PushMessage(error);
                return true;
            case "/RETOS":
                if (!AODuelUI.SendList(out error)) PushMessage(error);
                return true;
            default:
                return false;
        }
    }

    public void OpenDuelForm(string[] prefill)
    {
        duelPrefill = prefill;
        duelOpenForm = true;
    }

    void OpenDuelFormNow()
    {
        if (!AODuelUI.Available)
        {
            PushMessage("Los retos se juegan en la Demo AO BATTLESERVER con amigos.");
            return;
        }
        if (AODuelUI.InDuel)
        {
            PushMessage("Ya te encuentras en un reto.");
            return;
        }

        for (int i = 0; i < duelNames.Length; i++)
            duelNames[i] = "";
        duelNames[0] = AODuelUI.LocalName;

        int others = 0;
        if (duelPrefill != null)
        {
            foreach (string name in duelPrefill)
            {
                string clean = (name ?? "").Trim();
                if (clean.Length == 0 || others + 1 >= duelNames.Length) continue;
                duelNames[1 + others++] = clean;
            }
        }
        duelPrefill = null;
        duelTeamSize = Mathf.Clamp((others + 2) / 2, 1, AODuelUI.MaxPerTeam);
        duelFormError = "";
        duelMenuName = "";
        topDialog = TopDialog.Retos;
    }

    // ---------- Formulario de retos (frmRetos, ventanaretos.bmp 291×490) ----------

    void DrawDuelForm()
    {
        LoadDuelArt();
        EnsureDuelStyles();

        Rect win = R((REF_W - DuelFormW) * 0.5f, (REF_H - DuelFormH) * 0.5f, DuelFormW, DuelFormH);
        if (duelWindow != null)
            GUI.DrawTexture(win, duelWindow, ScaleMode.StretchToFill, true);
        else
            GUI.Box(win, "RETOS");

        if (DuelArtButton(FormRect(win, 260f, 1f, 31f, 28f), duelCloseOver, duelCloseOff))
        {
            topDialog = TopDialog.None;
            return;
        }

        if (DuelArtButton(FormRect(win, 159f, 90f, 21f, 21f), duelMinusOver, duelMinusOff))
            duelTeamSize = Mathf.Max(1, duelTeamSize - 1);
        GUI.Label(FormRect(win, 186f, 87f, 36f, 26f), duelTeamSize.ToString(), duelCountStyle);
        if (DuelArtButton(FormRect(win, 231f, 89f, 21f, 21f), duelPlusOver, duelPlusOff))
            duelTeamSize = Mathf.Min(AODuelUI.MaxPerTeam, duelTeamSize + 1);

        // Equipo 1 a la izquierda (vos y tus compañeros), equipo 2 a la derecha. Mismo orden que Jugador(i).
        for (int i = 0; i < duelNames.Length; i++)
        {
            int row = i / 2;
            float x = i % 2 == 0 ? 29f : 151f;
            float y = 146f + row * 32f;
            Rect campo = FormRect(win, x, y, 112f, 27f);
            if (row >= duelTeamSize)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(campo, Texture2D.whiteTexture);
                GUI.color = Color.white;
                continue;
            }
            if (duelField != null)
                GUI.DrawTexture(campo, duelField);
            Rect text = FormRect(win, x + 3f, y + 4f, 106f, 19f);
            if (i == 0)
                GUI.Label(text, duelNames[0], duelLocalName);
            else
            {
                string before = duelNames[i] ?? "";
                duelNames[i] = GUI.TextField(text, before, 30, duelFieldStyle);
                if (duelNames[i] != before) duelFormError = "";
            }
        }

        if (GUI.Button(FormRect(win, 28f, 307f, 150f, 24f), GUIContent.none, invisibleButton))
            duelPotionLimit = !duelPotionLimit;
        if (duelPotionLimit)
        {
            if (duelCheck != null)
                GUI.DrawTexture(FormRect(win, 31f, 311f, 17f, 17f), duelCheck);
            if (duelFieldShort != null)
                GUI.DrawTexture(FormRect(win, 184f, 307f, 69f, 27f), duelFieldShort);
            duelPotions = Digits(GUI.TextField(FormRect(win, 188f, 311f, 61f, 19f), duelPotions, 8, duelFieldCenter), 8);
        }

        duelBet = Digits(GUI.TextField(FormRect(win, 112f, 363f, 113f, 21f), duelBet, 9, duelFieldCenter), 9);

        // "Apostar los items del inventario": en la demo nunca se apuestan ítems (CaenItems = falso).
        Rect itemsRow = FormRect(win, 52f, 394f, 186f, 24f);
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(itemsRow, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Entre la fila de ítems y el separador de RETAR: no toca el campo de oro.
        Rect errorRect = FormRect(win, 22f, 417f, 247f, 14f);
        if (duelFormError.Length > 0)
            GUI.Label(errorRect, duelFormError, duelErrorStyle);
        else if (itemsRow.Contains(Event.current.mousePosition))
            GUI.Label(errorRect, "En la demo no se apuestan ítems.", duelHintStyle);

        if (DuelArtButton(FormRect(win, 90f, 448f, 112f, 28f), duelRetarOver, duelRetarOff))
            SubmitDuelForm();
    }

    // Validar() del original, con la apuesta mínima de la demo.
    void SubmitDuelForm()
    {
        int total = duelTeamSize * 2;
        for (int i = 0; i < total; i++)
        {
            string name = (duelNames[i] ?? "").Trim();
            duelNames[i] = name;
            if (name.Length == 0)
            {
                duelFormError = "Faltan jugadores.";
                return;
            }
            if (!ValidDuelName(name))
            {
                duelFormError = "Nombre inválido '" + name + "'.";
                return;
            }
        }
        for (int i = 0; i < total - 1; i++)
            for (int j = i + 1; j < total; j++)
                if (string.Equals(duelNames[i], duelNames[j], StringComparison.OrdinalIgnoreCase))
                {
                    duelFormError = "Hay jugadores repetidos.";
                    return;
                }

        if (!long.TryParse(duelBet, out long bet))
        {
            duelFormError = "Ingresá la apuesta (0 = reto amistoso).";
            return;
        }
        if (bet != 0 && bet < AODuelUI.MinBet)
        {
            duelFormError = "La apuesta mínima es de " + AODuelUI.Gold(AODuelUI.MinBet) + " (0 = amistoso).";
            return;
        }
        if (bet > AODuelUI.MaxBet)
        {
            duelFormError = "La apuesta máxima es de " + AODuelUI.Gold(AODuelUI.MaxBet) + ".";
            return;
        }
        if (combat != null && bet > combat.Gold)
        {
            duelFormError = "No tenés oro suficiente.";
            return;
        }

        int potions = -1;
        if (duelPotionLimit && (!int.TryParse(duelPotions, out potions) || potions < 0 || potions > AODuelUI.MaxRedPotions))
        {
            duelFormError = "Cantidad de pociones inválida.";
            return;
        }

        var players = new string[total - 1];
        Array.Copy(duelNames, 1, players, 0, players.Length);
        if (AODuelUI.SendChallenge(players, (int)bet, potions, out string error))
            topDialog = TopDialog.None;
        else
            duelFormError = error;
    }

    static bool ValidDuelName(string name)
    {
        if (name.Length > 30) return false;
        foreach (char c in name)
            if (!char.IsLetter(c) && c != ' ')
                return false;
        return true;
    }

    // ---------- Aviso de invitación o de reto enviado (arriba a la derecha del viewport) ----------

    void DrawDuelNotice()
    {
        if (AODuelUI.InDuel) return;

        AODuelUI.Invite invite = AODuelUI.Invites.Count > 0 ? AODuelUI.Invites[0] : null;
        if (invite == null && !AODuelUI.ChallengePending) return;

        string title, body, foot = "";
        if (invite != null)
        {
            title = "RETO";
            body = invite.from + " (" + invite.level + ") te desafía:\n" +
                   AODuelUI.Summary(invite.teamA, invite.teamB, invite.bet, invite.maxPotions);
            if (invite.error.Length > 0) body += "\n" + invite.error;
            else if (invite.status.Length > 0) body += "\n" + invite.status;
            if (!invite.accepted)
            {
                foot = "Vence en " + Mathf.Max(0, Mathf.CeilToInt(invite.expiresAt - Time.unscaledTime)) + " s";
                if (AODuelUI.Invites.Count > 1) foot += " · (1 de " + AODuelUI.Invites.Count + ")";
            }
        }
        else
        {
            title = "RETO ENVIADO";
            body = AODuelUI.PendingSummary + "\n" + AODuelUI.PendingStatus;
        }

        bool buttons = invite == null || !invite.accepted;
        float pad = 8f * scale;
        float width = 300f * scale;
        float inner = width - pad * 2f;
        var bodyContent = new GUIContent(body);
        float titleH = duelTitleStyle.CalcHeight(new GUIContent(title), inner);
        float bodyH = duelBodyStyle.CalcHeight(bodyContent, inner);
        float footH = foot.Length > 0 ? duelFootStyle.CalcHeight(new GUIContent(foot), inner) : 0f;
        float buttonH = buttons ? 28f * scale : 0f;
        float height = pad * 2f + titleH + bodyH + (buttons ? buttonH + 6f * scale : 0f) + footH;

        Rect view = GameViewRectGUI();
        Rect box = new Rect(view.xMax - width - 8f * scale, view.y + 8f * scale, width, height);
        DrawDuelPanel(box);
        if (Event.current.type == EventType.Repaint) AODuelUI.NoticeRect = box;

        float y = box.y + pad;
        GUI.Label(new Rect(box.x + pad, y, inner, titleH), title, duelTitleStyle);
        y += titleH;
        GUI.Label(new Rect(box.x + pad, y, inner, bodyH), bodyContent, duelBodyStyle);
        y += bodyH + (buttons ? 6f * scale : 0f);

        if (buttons)
        {
            float bw = 132f * scale;
            if (invite != null)
            {
                string error;
                if (DuelImageButton(new Rect(box.x + pad, y, bw, buttonH), duelAccept, duelAcceptOver, "ACEPTAR") &&
                    !AODuelUI.SendAccept(invite.from, out error))
                    invite.error = error;
                if (DuelImageButton(new Rect(box.xMax - pad - bw, y, bw, buttonH), duelReject, duelRejectOver, "RECHAZAR"))
                    AODuelUI.SendReject(invite.from);
            }
            else if (DuelImageButton(new Rect(box.x + (width - bw) * 0.5f, y, bw, buttonH), duelCancel, duelCancelOver, "CANCELAR") &&
                     !AODuelUI.SendCancel(out string cancelError))
                PushMessage(cancelError);
            y += buttonH;
        }

        if (footH > 0f)
            GUI.Label(new Rect(box.x + pad, y, inner, footH), foot, duelFootStyle);
    }

    // ---------- Marcador (arriba al centro), conteo y resultado (primer tercio) ----------

    void DrawDuelScoreboard()
    {
        if (!AODuelUI.InDuel) return;

        string line = "Ronda " + Mathf.Max(1, AODuelUI.Round) + " de 3  ·  " +
                      Short(AODuelUI.Names(AODuelUI.TeamA), 26) + "  " + AODuelUI.ScoreA + " — " +
                      AODuelUI.ScoreB + "  " + Short(AODuelUI.Names(AODuelUI.TeamB), 26) + "  ·  " +
                      AODuelUI.Clock(AODuelUI.FightSecondsLeft);
        if (AODuelUI.Down) line += "\nCaíste · esperá la próxima ronda";

        var content = new GUIContent(line);
        Rect view = GameViewRectGUI();
        float pad = 8f * scale;
        float width = Mathf.Min(view.width - 16f * scale, duelBodyStyle.CalcSize(content).x + pad * 2f + 4f);
        width = Mathf.Max(width, 300f * scale);
        float height = duelBodyStyle.CalcHeight(content, width - pad * 2f) + pad;
        Rect box = new Rect(view.center.x - width * 0.5f, view.y + 6f * scale, width, height);

        DrawDuelPanel(box);
        GUI.Label(new Rect(box.x + pad, box.y, box.width - pad * 2f, box.height), content, duelBodyCenter);
        if (Event.current.type == EventType.Repaint) AODuelUI.ScoreboardRect = box;
    }

    void DrawDuelCenter()
    {
        string big = null, small = "";
        if (AODuelUI.BannerVisible)
        {
            big = AODuelUI.BannerTitle;
            small = AODuelUI.BannerDetail;
        }
        else if (AODuelUI.CountdownActive)
            big = AODuelUI.CountdownSecondsLeft.ToString();
        if (string.IsNullOrEmpty(big)) return;

        Rect view = GameViewRectGUI();
        Rect r = new Rect(view.x, view.y + view.height * 0.22f, view.width, 64f * scale);
        DrawOutlined(r, big, duelBigStyle, duelBigShadow, 2f * scale);
        float textW = duelBigStyle.CalcSize(new GUIContent(big)).x;
        float bottom = r.yMax;
        if (!string.IsNullOrEmpty(small))
        {
            Rect detail = new Rect(view.x, r.yMax, view.width, 26f * scale);
            DrawOutlined(detail, small, duelMidStyle, duelMidShadow, 1f * scale);
            textW = Mathf.Max(textW, duelMidStyle.CalcSize(new GUIContent(small)).x);
            bottom = detail.yMax;
        }
        if (Event.current.type == EventType.Repaint)
            AODuelUI.CenterRect = Rect.MinMaxRect(view.center.x - textW * 0.5f, r.y, view.center.x + textW * 0.5f, bottom);
    }

    // ---------- Carteles de los rings para espectadores ----------

    void DrawDuelRingLabels()
    {
        Func<int, Vector2Int?> ringTile = AODuelUI.RingLabelTile ?? MapRingLabelTile;
        Rect view = GameViewRectGUI();

        foreach (AODuelUI.RingInfo info in AODuelUI.Rings)
        {
            if (AODuelUI.InDuel && info.ring == AODuelUI.Ring) continue;
            Vector2Int? tile = ringTile(info.ring);
            if (!tile.HasValue) continue;

            // Borde superior de la casilla (misma conversión que AOActionBarV260.CursorTile).
            Vector3 world = new Vector3(tile.Value.x - 0.5f, -tile.Value.y + 0.5f, 0f);
            Vector3 screen = gameCamera.WorldToScreenPoint(world);
            if (screen.z < 0f) continue;

            string text = AODuelUI.RingLine(info);
            Vector2 size = duelRingStyle.CalcSize(new GUIContent(text));
            Rect r = new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y);
            if (!view.Overlaps(r)) continue;
            r.x = Mathf.Clamp(r.x, view.x, Mathf.Max(view.x, view.xMax - r.width));
            DrawOutlined(r, text, duelRingStyle, duelRingShadow, Mathf.Max(1.5f, 1.5f * scale), true);
            if (Event.current.type == EventType.Repaint) AODuelUI.RingLabelRects.Add(r);
        }
    }

    // Rings del mapa actual: `arenaRings` [{id,x,y,w,h}] del JSON (mapa 1001, Programación).
    // El cartel va en la fila de grada de arriba (y − 2; el borde está en y − 1), al centro del ring.
    [Serializable] class DuelArenaRing { public int id, x, y, w, h; }
    [Serializable] class DuelArenaMap { public DuelArenaRing[] arenaRings; }

    int duelRingsMap = -1;
    DuelArenaRing[] duelMapRings;

    Vector2Int? MapRingLabelTile(int ring)
    {
        int map = world != null ? world.CurrentMapNumber : 0;
        if (map != duelRingsMap)
        {
            duelRingsMap = map;
            duelMapRings = null;
            TextAsset json = map > 0 ? Resources.Load<TextAsset>("AOMigrator/WorldV07/Maps/map_" + map) : null;
            if (json != null && json.text.Contains("\"arenaRings\""))
                duelMapRings = JsonUtility.FromJson<DuelArenaMap>(json.text)?.arenaRings;
        }
        if (duelMapRings == null) return null;
        foreach (DuelArenaRing r in duelMapRings)
            if (r.id == ring)
                return new Vector2Int(r.x + r.w / 2, r.y - 2);
        return null;
    }

    // ---------- Menú de usuario (MenuUser.frm): clic sobre otro jugador → Retar ----------

    void UpdateDuelUserMenuInput()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (AODuelUI.RemotePlayerAtGUI == null || mouse == null || !AODuelUI.Available)
            return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            duelMenuName = "";

        // AO: la acción "interactuar" del original (clic derecho). MOBA: el derecho camina, se usa el izquierdo.
        bool pressed = AOPlayerSettingsV230.IsMoba
            ? mouse.leftButton.wasPressedThisFrame
            : mouse.rightButton.wasPressedThisFrame;
        if (!pressed || InputCaptured || topDialog != TopDialog.None) return;
        if (magicV120 != null && !string.IsNullOrEmpty(magicV120.TargetPrompt)) return;
        if (combat != null && (combat.IsRangedTargeting || combat.ConsumedInputThisFrame)) return;

        Vector2 raw = mouse.position.ReadValue();
        Vector2 gui = new Vector2(raw.x, Screen.height - raw.y);
        if (!GameViewRectGUI().Contains(gui)) return;
        AOActionBarV260 bar = player != null ? player.GetComponent<AOActionBarV260>() : null;
        if (bar != null && bar.ShowBarPublic() && bar.GetBarRectGUI().Contains(gui)) return;

        string name = AODuelUI.RemotePlayerAtGUI(gui);
        if (string.IsNullOrEmpty(name)) return;
        duelMenuName = name;
        duelMenuPos = gui;
#endif
    }

    void DrawDuelUserMenu()
    {
        if (duelMenuName.Length == 0) return;

        float width = 150f * scale;
        float titleH = 24f * scale;
        float buttonH = 28f * scale;
        float pad = 6f * scale;
        // El clic que abre el menú cae sobre el título, así no dispara "Retar" al soltar.
        Rect box = new Rect(duelMenuPos.x - pad, duelMenuPos.y - pad, width, titleH + buttonH + pad * 2f);
        box.x = Mathf.Clamp(box.x, 0f, Mathf.Max(0f, Screen.width - box.width));
        box.y = Mathf.Clamp(box.y, 0f, Mathf.Max(0f, Screen.height - box.height));

        Event e = Event.current;
        if (e.type == EventType.MouseDown && !box.Contains(e.mousePosition))
        {
            duelMenuName = "";
            return;
        }

        DrawDuelPanel(box);
        if (e.type == EventType.Repaint) AODuelUI.UserMenuRect = box;
        GUI.Label(new Rect(box.x + pad, box.y + pad, box.width - pad * 2f, titleH), Short(duelMenuName, 18), duelTitleStyle);
        GUISkin previous = GUI.skin;
        GUI.skin = AOClassicSkinV200.Get(previous);
        if (AOAudioV190.Clicked(GUI.Button(new Rect(box.x + pad, box.y + pad + titleH, box.width - pad * 2f, buttonH), "Retar")))
        {
            string target = duelMenuName;
            duelMenuName = "";
            OpenDuelForm(new[] { target });
        }
        GUI.skin = previous;
    }

    // ---------- Ayudantes ----------

    Rect GameViewRectGUI()
    {
        if (gameCamera == null) return new Rect(0f, 0f, Screen.width, Screen.height);
        Rect v = gameCamera.pixelRect;
        return new Rect(v.x, Screen.height - v.yMax, v.width, v.height);
    }

    Rect FormRect(Rect win, float x, float y, float w, float h) =>
        new Rect(win.x + x * scale, win.y + y * scale, w * scale, h * scale);

    // Botón del original: el estado normal está dibujado en el fondo; se pinta "over" y "off" (apretado).
    bool DuelArtButton(Rect r, Texture2D over, Texture2D off)
    {
        if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint)
        {
            Texture2D art = MouseHeld() && off != null ? off : over;
            if (art != null) GUI.DrawTexture(r, art);
        }
        return AOAudioV190.Clicked(GUI.Button(r, GUIContent.none, invisibleButton));
    }

    // Botón con su propia imagen (ACEPTAR, RECHAZAR, CANCELAR del original).
    bool DuelImageButton(Rect r, Texture2D normal, Texture2D over, string fallback)
    {
        Texture2D art = r.Contains(Event.current.mousePosition) && over != null ? over : normal;
        if (art != null)
        {
            GUI.DrawTexture(r, art, ScaleMode.StretchToFill, true);
            return AOAudioV190.Clicked(GUI.Button(r, GUIContent.none, invisibleButton));
        }
        GUISkin previous = GUI.skin;
        GUI.skin = AOClassicSkinV200.Get(previous);
        bool clicked = AOAudioV190.Clicked(GUI.Button(r, fallback));
        GUI.skin = previous;
        return clicked;
    }

    static bool MouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return false;
#endif
    }

    void DrawDuelPanel(Rect box)
    {
        GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.9f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(0.55f, 0.45f, 0.25f, 1f);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(box.x, box.yMax - 1f, box.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(box.x, box.y, 1f, box.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(box.xMax - 1f, box.y, 1f, box.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // Texto con contorno negro y sin fondo (misma regla que el texto hablado).
    static void DrawOutlined(Rect r, string text, GUIStyle style, GUIStyle shadow, float offset, bool diagonals = false)
    {
        offset = Mathf.Max(1f, offset);
        GUI.Label(new Rect(r.x - offset, r.y, r.width, r.height), text, shadow);
        GUI.Label(new Rect(r.x + offset, r.y, r.width, r.height), text, shadow);
        GUI.Label(new Rect(r.x, r.y - offset, r.width, r.height), text, shadow);
        GUI.Label(new Rect(r.x, r.y + offset, r.width, r.height), text, shadow);
        if (diagonals)
        {
            GUI.Label(new Rect(r.x - offset, r.y - offset, r.width, r.height), text, shadow);
            GUI.Label(new Rect(r.x + offset, r.y - offset, r.width, r.height), text, shadow);
            GUI.Label(new Rect(r.x - offset, r.y + offset, r.width, r.height), text, shadow);
            GUI.Label(new Rect(r.x + offset, r.y + offset, r.width, r.height), text, shadow);
        }
        GUI.Label(r, text, style);
    }

    // Líneas visibles del chat (DrawChat): para el chequeo de superposición de QA.
    public Rect ChatLinesRectGUI => R(16f, 39f, 612f, 72f);

    static string Digits(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var chars = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            if (c >= '0' && c <= '9' && chars.Length < max)
                chars.Append(c);
        return chars.ToString();
    }

    void LoadDuelArt()
    {
        if (duelArtLoaded) return;
        duelArtLoaded = true;
        duelWindow = Resources.Load<Texture2D>(DuelArt + "ventanaretos");
        duelField = Resources.Load<Texture2D>(DuelArt + "campo-retar");
        duelFieldShort = Resources.Load<Texture2D>(DuelArt + "campo-corto");
        duelCheck = Resources.Load<Texture2D>(DuelArt + "check-amarillo");
        duelRetarOver = Resources.Load<Texture2D>(DuelArt + "boton-retar-over");
        duelRetarOff = Resources.Load<Texture2D>(DuelArt + "boton-retar-off");
        duelCloseOver = Resources.Load<Texture2D>(DuelArt + "boton-cerrar-over");
        duelCloseOff = Resources.Load<Texture2D>(DuelArt + "boton-cerrar-off");
        duelPlusOver = Resources.Load<Texture2D>(DuelArt + "boton-sm-mas-over");
        duelPlusOff = Resources.Load<Texture2D>(DuelArt + "boton-sm-mas-off");
        duelMinusOver = Resources.Load<Texture2D>(DuelArt + "boton-sm-menos-over");
        duelMinusOff = Resources.Load<Texture2D>(DuelArt + "boton-sm-menos-off");
        duelAccept = Resources.Load<Texture2D>(DuelArt + "boton-aceptar-default");
        duelAcceptOver = Resources.Load<Texture2D>(DuelArt + "boton-aceptar-over");
        duelReject = Resources.Load<Texture2D>(DuelArt + "boton-rechazar-default");
        duelRejectOver = Resources.Load<Texture2D>(DuelArt + "boton-rechazar-over");
        duelCancel = Resources.Load<Texture2D>(DuelArt + "boton-cancelar-default");
        duelCancelOver = Resources.Load<Texture2D>(DuelArt + "boton-cancelar-over");
    }

    void EnsureDuelStyles()
    {
        if (duelFieldStyle != null && Mathf.Approximately(duelStyleScale, scale)) return;
        duelStyleScale = scale;

        GUISkin classic = AOClassicSkinV200.Get(GUI.skin);
        Font font = classic.label.font;
        Color gold = new Color(1f, 0.84f, 0.45f);
        Color text = new Color(0.93f, 0.91f, 0.86f);

        duelFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            font = font,
            fontSize = DuelPx(12f),
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(3, 3, 0, 0),
            clipping = TextClipping.Clip
        };
        duelFieldStyle.normal.background = null;
        duelFieldStyle.hover.background = null;
        duelFieldStyle.focused.background = null;
        duelFieldStyle.active.background = null;
        duelFieldStyle.normal.textColor = Color.white;
        duelFieldStyle.hover.textColor = Color.white;
        duelFieldStyle.focused.textColor = Color.white;
        duelFieldStyle.active.textColor = Color.white;

        duelFieldCenter = new GUIStyle(duelFieldStyle) { alignment = TextAnchor.MiddleCenter };
        duelLocalName = new GUIStyle(duelFieldStyle);
        duelLocalName.normal.textColor = gold;
        duelCountStyle = new GUIStyle(duelFieldCenter) { fontSize = DuelPx(15f), fontStyle = FontStyle.Bold };
        duelCountStyle.normal.textColor = Color.white;

        duelErrorStyle = MakeDuelLabel(font, 11f, new Color(1f, 0.35f, 0.3f), TextAnchor.MiddleCenter, false);
        duelHintStyle = MakeDuelLabel(font, 11f, new Color(0.7f, 0.68f, 0.62f), TextAnchor.MiddleCenter, false);
        duelTitleStyle = MakeDuelLabel(font, 14f, gold, TextAnchor.UpperCenter, false);
        duelTitleStyle.fontStyle = FontStyle.Bold;
        duelBodyStyle = MakeDuelLabel(font, 12f, text, TextAnchor.UpperLeft, true);
        duelBodyCenter = new GUIStyle(duelBodyStyle) { alignment = TextAnchor.MiddleCenter };
        duelFootStyle = MakeDuelLabel(font, 11f, new Color(0.65f, 0.63f, 0.58f), TextAnchor.UpperCenter, true);

        duelBigStyle = MakeDuelLabel(font, 44f, gold, TextAnchor.MiddleCenter, false);
        duelBigStyle.fontStyle = FontStyle.Bold;
        duelBigShadow = new GUIStyle(duelBigStyle);
        duelBigShadow.normal.textColor = Color.black;
        duelMidStyle = MakeDuelLabel(font, 17f, text, TextAnchor.MiddleCenter, false);
        duelMidShadow = new GUIStyle(duelMidStyle);
        duelMidShadow.normal.textColor = Color.black;
        duelRingStyle = MakeDuelLabel(font, 13f, gold, TextAnchor.MiddleCenter, false);
        duelRingStyle.fontStyle = FontStyle.Bold;
        duelRingShadow = new GUIStyle(duelRingStyle);
        duelRingShadow.normal.textColor = Color.black;
    }

    GUIStyle MakeDuelLabel(Font font, float size, Color color, TextAnchor anchor, bool wrap)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = DuelPx(size),
            alignment = anchor,
            wordWrap = wrap,
            richText = false
        };
        style.normal.textColor = color;
        return style;
    }

    int DuelPx(float size) => Mathf.Max(8, Mathf.RoundToInt(size * scale));
}
