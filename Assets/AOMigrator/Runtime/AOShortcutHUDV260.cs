using System.Collections.Generic;
using UnityEngine;

public partial class AOActionBarV260
{
    AOActionBarDragDropV261 dragDrop;
    GUIStyle tooltipTitle, tooltipBody, tooltipHint;

    Rect BarRect()
    {
        var camera = GameCamera;
        if (camera == null) return Rect.zero;
        var view = camera.pixelRect;
        float width = Mathf.Min(352f, view.width - 24f);
        float x = view.x + 12f;
        float y = Screen.height - view.y - 55f;
        return new Rect(x, y, width, 51f);
    }

    bool ShowBar() => AOMainMenuV140.SessionActive && !AOOnlineClientV240.InputBlocked;

    bool HasAssignedItems()
    {
        for (int i = 0; i < 4; i++)
            if (AOPlayerSettingsV230.SlotAssignment(CharacterName, false, i) > 0)
                return true;
        return false;
    }

    public bool ShowBarPublic() => ShowBar();
    public Rect GetBarRectGUI() => BarRect();

    public Rect GetSlotRectGUI(int index)
    {
        var bar = BarRect();
        if (index < 0 || index > 7 || bar.width <= 0f) return Rect.zero;
        float cell = bar.width / 8f;
        return new Rect(bar.x + index * cell, bar.y, cell - 2f, bar.height);
    }

    public bool TryGetSlotAtGUI(Vector2 guiMouse, out int index)
    {
        index = -1;
        if (!ShowBar()) return false;
        for (int i = 0; i < 8; i++)
        {
            if (GetSlotRectGUI(i).Contains(guiMouse))
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    void OnGUI()
    {
        if (!ShowBar() || inventory == null || magic == null) return;
        var matrix = GUI.matrix;
        var color = GUI.color;
        int depth = GUI.depth;
        GUI.matrix = Matrix4x4.identity;
        GUI.depth = -18;
        var bar = BarRect();
        float cell = bar.width / 8f;
        int hoverIndex = -1;
        var label = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(9, Mathf.Min(12, (int)(cell / 3))),
            richText = false
        };
        var cooldownLabel = new GUIStyle(label)
        {
            fontSize = Mathf.Max(12, Mathf.Min(16, (int)(cell / 2.7f))),
            fontStyle = FontStyle.Bold
        };

        for (int i = 0; i < 8; i++)
        {
            bool spell = i < 4;
            int slot = i % 4;
            int id = AOPlayerSettingsV230.SlotAssignment(CharacterName, spell, slot);
            var rect = GetSlotRectGUI(i);
            bool enabled = !spell || AOPlayerSettingsV230.SpellMacrosEnabled;

            GUI.color = new Color(.08f, .07f, .05f, .95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = enabled ? Color.white : Color.gray;

            var icon = spell ? AOSpellDatabaseV120.Icon(id) : AOItemDatabaseV10.Icon(id);
            Rect iconRect = new Rect(rect.x + 7f, rect.y + 17f, cell - 16f, 27f);
            if (icon != null)
            {
                var uv = icon.textureRect;
                uv.x /= icon.texture.width;
                uv.y /= icon.texture.height;
                uv.width /= icon.texture.width;
                uv.height /= icon.texture.height;
                GUI.DrawTextureWithTexCoords(iconRect, icon.texture, uv);
            }

            var action = (spell ? AOGameAction.Spell1 : AOGameAction.Consumable1) + slot;
            string key = AOPlayerSettingsV230.KeyName(action);
            if (key.Length > 7) key = key.Substring(0, 6) + "…";
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 19f), key, label);

            if (spell && id > 0)
                DrawCooldown(id, iconRect, cooldownLabel);
            else if (!spell && id > 0)
            {
                string amount = inventory.CountItem(id).ToString();
                GUI.color = Color.black;
                GUI.Label(new Rect(rect.x + 1f, rect.y + 32f, rect.width, 19f), amount, label);
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x, rect.y + 31f, rect.width, 19f), amount, label);
            }

            if (rect.Contains(Event.current.mousePosition))
                hoverIndex = i;
        }

        GUI.color = Color.white;
        if (hoverIndex >= 0 && !IsDraggingSlot())
            DrawSlotTooltip(hoverIndex, bar);

        GUI.depth = depth;
        GUI.color = color;
        GUI.matrix = matrix;
    }

    void DrawCooldown(int spellId, Rect iconRect, GUIStyle style)
    {
        float remaining = magic.CooldownRemaining(spellId);
        if (remaining <= .05f) return;

        var spell = AOSpellDatabaseV120.Get(spellId);
        float total = spell != null ? Mathf.Max(.1f, spell.cooldown) : Mathf.Max(.1f, remaining);
        float fraction = Mathf.Clamp01(remaining / total);

        // Dark mask drains vertically as the spell becomes ready.
        GUI.color = new Color(0f, 0f, 0f, .68f);
        GUI.DrawTexture(new Rect(iconRect.x, iconRect.y, iconRect.width, iconRect.height * fraction), Texture2D.whiteTexture);

        string value = remaining < 10f ? remaining.ToString("0.0") : Mathf.CeilToInt(remaining).ToString();
        GUI.color = Color.black;
        GUI.Label(new Rect(iconRect.x + 1f, iconRect.y + 1f, iconRect.width, iconRect.height), value, style);
        GUI.color = Color.white;
        GUI.Label(iconRect, value, style);
    }

    bool IsDraggingSlot()
    {
        if (dragDrop == null) dragDrop = GetComponent<AOActionBarDragDropV261>();
        return dragDrop != null && dragDrop.IsDragging;
    }

    void EnsureTooltipStyles()
    {
        if (tooltipTitle != null) return;
        tooltipTitle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true, richText = false };
        tooltipTitle.normal.textColor = new Color(1f, .84f, .45f);
        tooltipBody = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = false };
        tooltipBody.normal.textColor = new Color(.93f, .91f, .86f);
        tooltipHint = new GUIStyle(tooltipBody) { fontSize = 11, fontStyle = FontStyle.Italic };
        tooltipHint.normal.textColor = new Color(.62f, .6f, .55f);
    }

    // Full hotbar tooltip: spell (name, key, mana, CD, target, effect) or consumable (name, key, amount, effect).
    void DrawSlotTooltip(int index, Rect bar)
    {
        bool spell = index < 4;
        int slot = index % 4;
        int id = AOPlayerSettingsV230.SlotAssignment(CharacterName, spell, slot);
        string key = AOPlayerSettingsV230.KeyName((spell ? AOGameAction.Spell1 : AOGameAction.Consumable1) + slot);

        string title, body, hint;
        if (id <= 0)
        {
            title = (spell ? "Hechizo " : "Consumible ") + (slot + 1) + " (vacío)";
            body = "Tecla: " + key;
            hint = spell ? "Arrastrá un hechizo desde el libro de hechizos." : "Arrastrá un consumible desde el inventario.";
        }
        else if (spell)
        {
            var s = AOSpellDatabaseV120.Get(id);
            title = s?.name ?? "Hechizo " + id;
            body = SpellTooltipBody(s, id, key);
            hint = "Arrastrá para mover · Clic derecho para vaciar";
        }
        else
        {
            var item = AOItemDatabaseV10.Get(id);
            title = item?.name ?? "Consumible " + id;
            body = ItemTooltipBody(item, id, key);
            hint = "Arrastrá para mover · Clic derecho para vaciar";
        }

        EnsureTooltipStyles();
        const float width = 250f, pad = 7f;
        float inner = width - pad * 2f;
        var titleContent = new GUIContent(title);
        var bodyContent = new GUIContent(body);
        var hintContent = new GUIContent(hint);
        float titleH = tooltipTitle.CalcHeight(titleContent, inner);
        float bodyH = tooltipBody.CalcHeight(bodyContent, inner);
        float hintH = tooltipHint.CalcHeight(hintContent, inner);
        float height = titleH + bodyH + hintH + pad * 2f + 4f;

        var slotRect = GetSlotRectGUI(index);
        float x = Mathf.Clamp(slotRect.center.x - width * .5f, 2f, Mathf.Max(2f, Screen.width - width - 2f));
        float y = Mathf.Max(2f, bar.y - height - 4f);
        var box = new Rect(x, y, width, height);

        GUI.color = new Color(.08f, .07f, .05f, .96f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(.55f, .45f, .25f, 1f);
        DrawFrame(box);
        GUI.color = Color.white;

        float cy = y + pad;
        GUI.Label(new Rect(x + pad, cy, inner, titleH), titleContent, tooltipTitle);
        cy += titleH;
        GUI.Label(new Rect(x + pad, cy, inner, bodyH), bodyContent, tooltipBody);
        cy += bodyH + 4f;
        GUI.Label(new Rect(x + pad, cy, inner, hintH), hintContent, tooltipHint);
    }

    string SpellTooltipBody(AOSpellDatabaseV120.SpellDef s, int id, string key)
    {
        if (s == null) return "Tecla: " + key + "\nHechizo desconocido.";

        string cost = "Maná: " + s.manaRequired;
        if (s.staminaRequired > 0) cost += " · Energía: " + s.staminaRequired;

        string cooldown = s.cooldown > 0 ? "CD: " + s.cooldown + " s" : "CD: sin enfriamiento";
        float remaining = magic.CooldownRemaining(id);
        cooldown += remaining > .05f ? " (faltan " + remaining.ToString("0.0") + " s)" : " · Listo";

        string target = AOSkillShotConfigV267.IsSkillShot(id) ? "Dirección (skill shot)" : s.TargetLabel;
        if (s.areaAffects != 0 && s.areaRadius > 0) target += " · área " + s.areaRadius;

        var lines = new List<string> { "Tecla: " + key, cost, cooldown, "Objetivo: " + target };
        string effect = SpellEffectText(s);
        if (effect.Length > 0) lines.Add("Efecto: " + effect);
        if (!string.IsNullOrWhiteSpace(s.description)) lines.Add(Shorten(s.description.Trim(), 120));
        if (!s.supportedLocal) lines.Add("No disponible todavía.");
        if (!magic.KnowsSpell(id)) lines.Add("No aprendiste este hechizo.");
        if (!AOPlayerSettingsV230.SpellMacrosEnabled) lines.Add("Macros de hechizos apagadas (Ajustes > Controles).");
        return string.Join("\n", lines);
    }

    string ItemTooltipBody(AOItemDatabaseV10.ItemDef item, int id, string key)
    {
        var lines = new List<string> { "Tecla: " + key, "Cantidad: " + inventory.CountItem(id) };
        if (item == null) return string.Join("\n", lines);
        string effect = ItemEffectText(item);
        if (effect.Length > 0) lines.Add("Efecto: " + effect);
        if (!string.IsNullOrWhiteSpace(item.description)) lines.Add(Shorten(item.description.Trim(), 120));
        return string.Join("\n", lines);
    }

    // Mirrors the potion/food handling of AOInventoryV10.UseConsumableById.
    static string ItemEffectText(AOItemDatabaseV10.ItemDef item)
    {
        if (item.objType == 1 && item.minHunger > 0) return "+" + item.minHunger + " hambre.";
        if ((item.objType == 13 || item.objType == 34) && item.minThirst > 0) return "+" + item.minThirst + " sed.";
        if (item.objType != 11) return "";
        switch (item.potionType)
        {
            case 1: return "Agilidad +" + Range(item.minModifier, item.maxModifier) + " por " + Mathf.Max(1, item.durationEffect) + " s.";
            case 2: return "Fuerza +" + Range(item.minModifier, item.maxModifier) + " por " + Mathf.Max(1, item.durationEffect) + " s.";
            case 3: return "Recupera " + Range(Mathf.Max(0, item.minModifier), Mathf.Max(0, item.maxModifier)) + " de vida.";
            case 4: return "Recupera " + item.percent + "% del maná.";
            case 7: return "Recupera " + Range(item.minModifier, item.maxModifier) + " de energía.";
            default: return "";
        }
    }

    // Mirrors the fields applied by AOPlayerMagicV120 when a spell lands.
    static string SpellEffectText(AOSpellDatabaseV120.SpellDef s)
    {
        var parts = new List<string>();
        if (s.raiseHp == 1) parts.Add("cura " + Range(s.minHp, s.maxHp) + " de vida");
        else if (s.raiseHp == 2) parts.Add("daño " + Range(s.minHp, s.maxHp));
        AddStat(parts, s.raiseMana, s.minMana, s.maxMana, "maná");
        AddStat(parts, s.raiseStamina, s.minStamina, s.maxStamina, "energía");
        AddStat(parts, s.raiseHunger, s.minHunger, s.maxHunger, "hambre");
        AddStat(parts, s.raiseThirst, s.minThirst, s.maxThirst, "sed");
        AddStat(parts, s.raiseAgility, s.minAgility, s.maxAgility, "agilidad");
        AddStat(parts, s.raiseStrength, s.minStrength, s.maxStrength, "fuerza");
        AddStat(parts, s.raiseCharisma, s.minCharisma, s.maxCharisma, "carisma");
        if (s.speed > 0f) parts.Add("más velocidad");
        AddFlag(parts, s.paralyze, "paraliza");
        AddFlag(parts, s.immobilize, "inmoviliza");
        AddFlag(parts, s.removeParalysis, "quita parálisis");
        AddFlag(parts, s.poison, "envenena");
        AddFlag(parts, s.curePoison, "cura veneno");
        AddFlag(parts, s.invisibility, "invisibilidad");
        AddFlag(parts, s.removeInvisibility, "revela invisibles");
        AddFlag(parts, s.incinerate, "incinera");
        AddFlag(parts, s.blindness, "ceguera");
        AddFlag(parts, s.dumb, "estupidez");
        AddFlag(parts, s.removeDumb, "quita estupidez");
        AddFlag(parts, s.curse, "maldición");
        AddFlag(parts, s.removeCurse, "quita maldición");
        AddFlag(parts, s.removeDebuff, "quita estados negativos");
        AddFlag(parts, s.stealBuff, "roba un efecto");
        AddFlag(parts, s.resurrect, "resucita");
        AddFlag(parts, s.mimic, "mimetismo");
        if (s.summonNpc > 0) parts.Add(s.summonCount > 1 ? "invoca " + s.summonCount + " criaturas" : "invoca una criatura");
        AddFlag(parts, s.materializeObject, "materializa un objeto");
        AddFlag(parts, s.teleport, "teletransporta");
        AddFlag(parts, s.eotId, "efecto en el tiempo");
        if (parts.Count == 0) return "";
        string text = string.Join(", ", parts);
        return char.ToUpper(text[0]) + text.Substring(1) + ".";
    }

    static void AddStat(List<string> parts, int mode, int min, int max, string stat)
    {
        if (mode != 0) parts.Add((mode == 1 ? "+" : "-") + Range(min, max) + " " + stat);
    }

    static void AddFlag(List<string> parts, int value, string text)
    {
        if (value != 0) parts.Add(text);
    }

    static string Range(int a, int b)
    {
        int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        return lo == hi ? lo.ToString() : lo + "-" + hi;
    }

    static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1).TrimEnd() + "…";

    static void DrawFrame(Rect r)
    {
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), Texture2D.whiteTexture);
    }
}
