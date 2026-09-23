using System.Collections.Generic;
using UnityEngine;

public class AOLootPickupV09 : AOInteractable
{
    static readonly List<AOLootPickupV09>
        active =
            new List<AOLootPickupV09>();

    [SerializeField]
    int itemIndex;

    [SerializeField]
    int amount;

    TextMesh label;
    SpriteRenderer iconRenderer;

    public int ItemIndex =>
        itemIndex;

    public int Amount =>
        amount;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        active.Clear();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!active.Contains(
                this))
        {
            active.Add(
                this);
        }
    }

    protected override void OnDisable()
    {
        active.Remove(
            this);

        base.OnDisable();
    }

    public static AOLootPickupV09 Create(
        int item,
        string itemName,
        int quantity,
        int tileX,
        int tileY)
    {
        quantity =
            Mathf.Max(
                1,
                quantity);

        // AO permite stacks. Si ya hay el mismo item en el tile,
        // lo fusionamos para no llenar la escena con etiquetas duplicadas.
        foreach (
            AOLootPickupV09 current in
            active)
        {
            if (current == null ||
                !current.isActiveAndEnabled)
                continue;

            if (current.itemIndex ==
                    item &&
                current.TileX ==
                    tileX &&
                current.TileY ==
                    tileY)
            {
                current.amount +=
                    quantity;

                current.RefreshPresentation();
                return current;
            }
        }

        GameObject root =
            GameObject.Find(
                "AO Loot v0.18");

        if (root == null)
        {
            root =
                new GameObject(
                    "AO Loot v0.18");
        }

        GameObject go =
            new GameObject(
                "Loot_" +
                item +
                "_" +
                itemName);

        go.transform.SetParent(
            root.transform,
            false);

        AOGridMap grid =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOGridMap>();

        if (grid != null)
        {
            go.transform.position =
                grid.TileToWorld(
                    tileX,
                    tileY);
        }

        AOLootPickupV09 loot =
            go.AddComponent
                <AOLootPickupV09>();

        loot.itemIndex =
            item;

        loot.amount =
            quantity;

        loot.ConfigureInteraction(
            tileX,
            tileY,
            itemName,
            "Loot x" +
            quantity,
            false);

        loot.BuildPresentation();

        return loot;
    }

    void BuildPresentation()
    {
        GameObject icon =
            new GameObject(
                "LootIcon");

        icon.transform.SetParent(
            transform,
            false);

        icon.transform.localPosition =
            new Vector3(
                0f,
                0.05f,
                0f);

        iconRenderer =
            icon.AddComponent
                <SpriteRenderer>();

        iconRenderer.sprite =
            AOItemDatabaseV10.Icon(
                itemIndex);

        iconRenderer.sortingOrder =
            16000 +
            TileY;

        GameObject labelGo =
            new GameObject(
                "LootLabel");

        labelGo.transform.SetParent(
            transform,
            false);

        labelGo.transform.localPosition =
            new Vector3(
                0f,
                0.55f,
                0f);

        label =
            labelGo.AddComponent
                <TextMesh>();

        label.anchor =
            TextAnchor.LowerCenter;

        label.alignment =
            TextAlignment.Center;

        label.fontSize = 42;
        label.characterSize = 0.021f;

        MeshRenderer mr =
            labelGo.GetComponent
                <MeshRenderer>();

        if (mr != null)
        {
            mr.sortingOrder =
                17000 +
                TileY;
        }

        RefreshPresentation();
    }

    void RefreshPresentation()
    {
        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                itemIndex);

        string name =
            item == null
            ? DisplayName
            : item.name;

        if (label != null)
        {
            label.text =
                name +
                (amount > 1
                    ? " x" +
                      amount
                    : "");
        }

        ConfigureInteraction(
            TileX,
            TileY,
            name,
            "Loot x" +
            amount,
            false);
    }

    public static AOLootPickupV09 FindAt(
        int x,
        int y)
    {
        for (int i =
                 active.Count - 1;
             i >= 0;
             i--)
        {
            AOLootPickupV09 loot =
                active[i];

            if (loot == null)
            {
                active.RemoveAt(
                    i);
                continue;
            }

            if (loot.isActiveAndEnabled &&
                loot.TileX ==
                    x &&
                loot.TileY ==
                    y)
            {
                return loot;
            }
        }

        return null;
    }

    public void Consume()
    {
        gameObject.SetActive(
            false);

        Destroy(
            gameObject);
    }
}
