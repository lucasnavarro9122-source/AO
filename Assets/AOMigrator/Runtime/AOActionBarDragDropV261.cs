using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(110)]
[DisallowMultipleComponent]
public class AOActionBarDragDropV261 : MonoBehaviour
{
    AOInterfaceV0101 ui;
    AOActionBarV260 actionBar;
    AOInventoryV10 inventory;
    AOPlayerMagicV120 magic;
    AOCharacterIdentityV170 identity;

    bool armed;
    bool dragging;
    bool dragSpell;
    bool dragFromBar;
    int dragId;
    int sourceBarIndex = -1;
    Vector2 dragStartGui;
    string dragLabel = "";
    int hoverBarIndex = -1;

    string CharacterName => identity != null && !string.IsNullOrEmpty(identity.CharacterName) ? identity.CharacterName : "Aventurero";
    public bool IsDragging => dragging;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallAfterSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall();

    static void TryInstall()
    {
        foreach (var player in Object.FindObjectsByType<AOTestPlayer>(FindObjectsSortMode.None))
        {
            if (player != null && player.GetComponent<AOActionBarDragDropV261>() == null)
                player.gameObject.AddComponent<AOActionBarDragDropV261>();
        }
    }

    void Awake() => Cache();
    void OnEnable() => Cache();

    void Cache()
    {
        if (actionBar == null) actionBar = GetComponent<AOActionBarV260>();
        if (inventory == null) inventory = GetComponent<AOInventoryV10>();
        if (magic == null) magic = GetComponent<AOPlayerMagicV120>();
        if (identity == null) identity = GetComponent<AOCharacterIdentityV170>();
        if (ui == null) ui = Object.FindFirstObjectByType<AOInterfaceV0101>();
    }

    void Update()
    {
        Cache();
        if (actionBar == null || ui == null || !AOMainMenuV140.SessionActive)
        {
            CancelDrag();
            return;
        }

        // Con el chat, una ventana de ciudad o de misiones abierta no se arrastra ni se vacía nada.
        if (!PointerAvailable() || AOInterfaceV0101.InputCaptured || AOCityUIV130.ModalOpen || AOQuestUIV150.ModalOpen)
        {
            CancelDrag();
            return;
        }

        Vector2 guiMouse = GuiMouse();

        if (!armed && SecondaryPressedThisFrame() && actionBar.TryGetSlotAtGUI(guiMouse, out int clearIndex))
            ClearBarSlot(clearIndex);

        if (PrimaryPressedThisFrame())
            BeginPotentialDrag(guiMouse);

        if (armed && !dragging && PrimaryHeld() && Vector2.Distance(guiMouse, dragStartGui) > 6f)
            dragging = dragId > 0;

        if (dragging)
            hoverBarIndex = actionBar.TryGetSlotAtGUI(guiMouse, out int idx) ? idx : -1;
        else
            hoverBarIndex = -1;

        if (PrimaryReleasedThisFrame())
            EndDrag(guiMouse);
    }

    void BeginPotentialDrag(Vector2 guiMouse)
    {
        CancelDrag(false);
        if (TryStartBarDrag(guiMouse)) return;
        if (TryStartInventoryDrag(guiMouse)) return;
        TryStartSpellDrag(guiMouse);
    }

    bool TryStartBarDrag(Vector2 guiMouse)
    {
        if (actionBar == null || !actionBar.TryGetSlotAtGUI(guiMouse, out int index))
            return false;

        bool spell = index < 4;
        int slot = index % 4;
        int id = AOPlayerSettingsV230.SlotAssignment(CharacterName, spell, slot);
        if (id <= 0)
            return false;

        armed = true;
        dragging = false;
        dragSpell = spell;
        dragFromBar = true;
        dragId = id;
        sourceBarIndex = index;
        dragStartGui = guiMouse;
        dragLabel = spell ? AOSpellDatabaseV120.Get(id)?.name : AOItemDatabaseV10.Get(id)?.name;
        return true;
    }

    bool TryStartInventoryDrag(Vector2 guiMouse)
    {
        if (!ui.IsInventoryTabVisible || inventory == null) return false;
        for (int i = 0; i < ui.VisibleInventorySlots; i++)
        {
            if (!ui.InventorySlotRectGUI(i).Contains(guiMouse))
                continue;

            int id = ui.InventoryItemIdAt(i);
            var item = AOItemDatabaseV10.Get(id);
            if (item == null || !item.Consumable)
                return false;

            armed = true;
            dragging = false;
            dragSpell = false;
            dragFromBar = false;
            sourceBarIndex = -1;
            dragId = id;
            dragLabel = item.name;
            dragStartGui = guiMouse;
            return true;
        }
        return false;
    }

    bool TryStartSpellDrag(Vector2 guiMouse)
    {
        if (!ui.IsSpellsTabVisible || magic == null || magic.KnownSpellCount <= 0)
            return false;

        Rect viewport = ui.SpellViewportRectGUI;
        if (!viewport.Contains(guiMouse))
            return false;

        float localY = guiMouse.y - viewport.y + ui.SpellScrollValue.y;
        int row = Mathf.FloorToInt(localY / Mathf.Max(1f, ui.SpellRowHeightGUI));
        if (row < 0 || row >= magic.KnownSpellCount)
            return false;

        var spell = magic.GetKnownSpellAt(row);
        if (spell == null)
            return false;

        armed = true;
        dragging = false;
        dragSpell = true;
        dragFromBar = false;
        sourceBarIndex = -1;
        dragId = spell.id;
        dragLabel = spell.name;
        dragStartGui = guiMouse;
        return true;
    }

    void EndDrag(Vector2 guiMouse)
    {
        if (dragging && dragId > 0 && actionBar != null && actionBar.TryGetSlotAtGUI(guiMouse, out int barIndex))
        {
            if (dragFromBar)
                MoveOrSwapBarSlot(barIndex);
            else
                ApplyExternalDrop(barIndex);
        }
        CancelDrag();
    }

    void MoveOrSwapBarSlot(int targetIndex)
    {
        if (sourceBarIndex < 0 || targetIndex < 0 || targetIndex > 7 || targetIndex == sourceBarIndex)
            return;

        bool sourceSpell = sourceBarIndex < 4;
        bool targetSpell = targetIndex < 4;
        if (sourceSpell != targetSpell)
        {
            AOInterfaceV0101.PushMessage(sourceSpell
                ? "Los hechizos solo pueden ordenarse entre Q, W, E y R."
                : "Los consumibles solo pueden ordenarse entre 1, 2, 3 y 4.");
            return;
        }

        int sourceSlot = sourceBarIndex % 4;
        int targetSlot = targetIndex % 4;
        int sourceId = AOPlayerSettingsV230.SlotAssignment(CharacterName, sourceSpell, sourceSlot);
        int targetId = AOPlayerSettingsV230.SlotAssignment(CharacterName, targetSpell, targetSlot);
        if (sourceId <= 0) return;

        AOPlayerSettingsV230.AssignSlot(CharacterName, sourceSpell, sourceSlot, targetId);
        AOPlayerSettingsV230.AssignSlot(CharacterName, targetSpell, targetSlot, sourceId);

        string sourceKey = AOPlayerSettingsV230.KeyName((sourceSpell ? AOGameAction.Spell1 : AOGameAction.Consumable1) + sourceSlot);
        string targetKey = AOPlayerSettingsV230.KeyName((targetSpell ? AOGameAction.Spell1 : AOGameAction.Consumable1) + targetSlot);
        AOInterfaceV0101.PushMessage(targetId > 0
            ? "Slots " + sourceKey + " y " + targetKey + " intercambiados."
            : "Asignación movida de " + sourceKey + " a " + targetKey + ".");
    }

    void ApplyExternalDrop(int barIndex)
    {
        if (barIndex < 0 || barIndex > 7 || dragId <= 0)
            return;

        if (dragSpell)
        {
            if (barIndex > 3)
            {
                AOInterfaceV0101.PushMessage("Los hechizos solo se pueden soltar en Q, W, E o R.");
                return;
            }
            AOPlayerSettingsV230.AssignSlot(CharacterName, true, barIndex, dragId);
            string key = AOPlayerSettingsV230.KeyName(AOGameAction.Spell1 + barIndex);
            string macroError = "";
            if (AOPlayerSettingsV230.SpellMacrosEnabled || AOPlayerSettingsV230.SetSpellMacros(true, out macroError))
                AOInterfaceV0101.PushMessage("Hechizo asignado a " + key + ".");
            else
                AOInterfaceV0101.PushMessage("Hechizo asignado a " + key + ", pero las macros siguen apagadas: " + macroError);
            return;
        }

        if (barIndex < 4)
        {
            AOInterfaceV0101.PushMessage("Los consumibles solo se pueden soltar en 1, 2, 3 o 4.");
            return;
        }

        AOPlayerSettingsV230.AssignSlot(CharacterName, false, barIndex - 4, dragId);
        AOInterfaceV0101.PushMessage("Consumible asignado a " + AOPlayerSettingsV230.KeyName(AOGameAction.Consumable1 + (barIndex - 4)) + ".");
    }

    void ClearBarSlot(int index)
    {
        if (index < 0 || index > 7) return;
        if (AOInterfaceV0101.InputCaptured || AOCityUIV130.ModalOpen || AOQuestUIV150.ModalOpen) return;

        bool spell = index < 4;
        int slot = index % 4;
        if (AOPlayerSettingsV230.SlotAssignment(CharacterName, spell, slot) <= 0) return;

        AOPlayerSettingsV230.AssignSlot(CharacterName, spell, slot, 0);
        string key = AOPlayerSettingsV230.KeyName((spell ? AOGameAction.Spell1 : AOGameAction.Consumable1) + slot);
        AOInterfaceV0101.PushMessage("Slot " + key + " vaciado.");
    }

    void CancelDrag(bool clearMessage = true)
    {
        armed = false;
        dragging = false;
        dragSpell = false;
        dragFromBar = false;
        dragId = 0;
        sourceBarIndex = -1;
        dragStartGui = default;
        hoverBarIndex = -1;
        if (clearMessage) dragLabel = "";
    }

    void OnGUI()
    {
        if (!dragging || dragId <= 0 || actionBar == null || !PointerAvailable()) return;

        var oldColor = GUI.color;
        int depth = GUI.depth;
        GUI.depth = -19;

        if (hoverBarIndex >= 0)
        {
            var slot = actionBar.GetSlotRectGUI(hoverBarIndex);
            bool compatible = (hoverBarIndex < 4) == dragSpell;
            GUI.color = compatible
                ? new Color(1f, 0.9f, 0.35f, 0.35f)
                : new Color(0.85f, 0.15f, 0.12f, 0.35f);
            GUI.DrawTexture(slot, Texture2D.whiteTexture);
        }

        Sprite icon = dragSpell ? AOSpellDatabaseV120.Icon(dragId) : AOItemDatabaseV10.Icon(dragId);
        if (icon != null)
        {
            Vector2 guiMouse = GuiMouse();
            Rect r = new Rect(guiMouse.x - 18f, guiMouse.y - 18f, 36f, 36f);
            GUI.color = new Color(1f, 1f, 1f, 0.84f);
            DrawSprite(icon, r);
        }

        if (!string.IsNullOrEmpty(dragLabel))
        {
            Vector2 guiMouse = GuiMouse();
            GUI.color = new Color(0.08f, 0.07f, 0.05f, 0.94f);
            GUI.DrawTexture(new Rect(guiMouse.x + 18f, guiMouse.y - 10f, 190f, 22f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(guiMouse.x + 23f, guiMouse.y - 11f, 180f, 22f), dragLabel);
        }

        GUI.depth = depth;
        GUI.color = oldColor;
    }

    static bool PointerAvailable()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null;
#else
        return true;
#endif
    }

    static bool PrimaryPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static bool SecondaryPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    static bool PrimaryHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    static bool PrimaryReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }

    static Vector2 GuiMouse()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : default;
#else
        Vector2 mouse = Input.mousePosition;
#endif
        return new Vector2(mouse.x, Screen.height - mouse.y);
    }

    static void DrawSprite(Sprite icon, Rect rect)
    {
        if (icon == null || icon.texture == null) return;
        Rect tr = icon.textureRect;
        Rect uv = new Rect(tr.x / icon.texture.width, tr.y / icon.texture.height, tr.width / icon.texture.width, tr.height / icon.texture.height);
        GUI.DrawTextureWithTexCoords(rect, icon.texture, uv, true);
    }
}
