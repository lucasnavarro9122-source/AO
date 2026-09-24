using UnityEngine;

public partial class AOInterfaceV0101
{
    public bool IsInventoryTabVisible => upperTab == UpperTab.Inventory;
    public bool IsSpellsTabVisible => upperTab == UpperTab.Spells;
    public float UIScale => scale;
    public Vector2 SpellScrollValue => spellScroll;
    public Rect SpellViewportRectGUI => R(783f, 205f, 220f, 127f);
    public float SpellRowHeightGUI => 23f * scale;
    public int VisibleInventorySlots => inventory == null ? 0 : Mathf.Min(AOInventoryV10.BASIC_SLOTS, inventory.SlotCount);
    public Rect InventorySlotRectGUI(int index) => InventorySlotRect(index);
    public int InventoryItemIdAt(int index) => inventory == null ? 0 : inventory.GetSlotItemIndex(index);
}
