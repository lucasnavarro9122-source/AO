using System;
using System.Collections.Generic;
using UnityEngine;

public static class AOItemDatabaseV10
{
    const string DatabaseResource =
        "AOMigrator/ItemsV10/items";
    const string TextureResource =
        "AOMigrator/ItemsV10/Textures/tex_";

    [Serializable]
    public class FrameSpec
    {
        public int fileNum;
        public int sx;
        public int sy;
        public int width;
        public int height;
        public string key;
    }

    [Serializable]
    public class DirectionSpec
    {
        public int heading;
        public FrameSpec[] body;
        public FrameSpec[] helmet;
        public FrameSpec[] weapon;
        public FrameSpec[] shield;
    }

    [Serializable]
    public class ItemDef
    {
        public int index;
        public string name;
        public string description;
        public int objType;
        public int grhIndex;
        public int value;
        public int minHit;
        public int maxHit;
        public int minHitToNpc;
        public int maxHitToNpc;
        public int minDef;
        public int maxDef;
        public bool twoHands;
        public int anim;
        public int weaponType;
        public int projectile;
        public int munition;
        public bool newbie;
        public FrameSpec icon;
        public DirectionSpec[] visualDirections;
        public bool hasBodyOverride;
        public int headOffsetX;
        public int headOffsetY;
        public int bodyShiftX;

        // v0.11: restricciones originales de obj.dat
        public int minLevel;
        public int maxLevel;
        public int requiredSkillIndex;
        public int requiredSkillValue;
        public int[] prohibitedClasses;
        public int[] prohibitedRaces;
        public int raceWhitelistMask;
        public bool maleOnly;
        public bool femaleOnly;

        // v0.11.1: body de armadura por raza/género.
        // Índice = (gender-1)*6 + (race-1)
        public int[] bodyByProfile;

        // v0.11.4: datos de uso/consumo desde obj.dat.
        public int subType;
        public int potionType;
        public int minModifier;
        public int maxModifier;
        public int percent;
        public int durationEffect;
        public int sound1;
        public int minHunger;
        public int minThirst;
        public int minStamina;
        public int closedBottleIndex;
        public int openBottleIndex;

        // v0.12.9 magia / pergaminos / accesorios.
        public int spellIndex;
        public int staffPower;
        public int magicDamageBonus;
        public int magicPenetration;
        public int magicAbsoluteBonus;
        public int magicResistance;
        public int elvenWood;
        public int power;

        // v0.13: reglas de comercio NPC desde obj.dat.
        public bool crucial;
        public bool untransferable;
        public bool destroyOnSell;

        public bool SellableToNPC =>
            !newbie &&
            !untransferable &&
            !destroyOnSell &&
            !(objType >= 38 &&
              objType <= 43);

        public bool IsParchment =>
            objType == 24 &&
            spellIndex > 0;

        public bool IsMagicAccessory =>
            objType == 21 ||
            objType == 30 ||
            objType == 35;

        public bool Consumable =>
            (objType == 1 &&
             minHunger > 0) ||
            (objType == 13 &&
             minThirst > 0) ||
            (objType == 34 &&
             minThirst > 0) ||
            (objType == 11 &&
             (potionType == 1 ||
              potionType == 2 ||
              potionType == 3 ||
              potionType == 4 ||
              potionType == 7));

        public int BodyForProfile(
            int raceId,
            int genderId)
        {
            if (bodyByProfile == null ||
                bodyByProfile.Length < 12)
                return 0;

            int index =
                (Mathf.Clamp(
                    genderId,
                    1,
                    2) - 1) *
                6 +
                (Mathf.Clamp(
                    raceId,
                    1,
                    6) - 1);

            return bodyByProfile[index];
        }

        public bool Equipable =>
            objType == 2 ||
            objType == 3 ||
            objType == 16 ||
            objType == 17 ||
            objType == 21 ||
            objType == 30 ||
            objType == 35;
    }

    [Serializable]
    class Database
    {
        public string version;
        public ItemDef[] items;
    }

    static bool loaded;

    static readonly Dictionary<int, ItemDef> byId =
        new Dictionary<int, ItemDef>();

    static readonly Dictionary<int, Texture2D> textures =
        new Dictionary<int, Texture2D>();

    static readonly Dictionary<string, Sprite> sprites =
        new Dictionary<string, Sprite>();

    public static int Count
    {
        get
        {
            EnsureLoaded();
            return byId.Count;
        }
    }

    public static ItemDef Get(int index)
    {
        EnsureLoaded();

        return byId.TryGetValue(
            index,
            out ItemDef item)
            ? item
            : null;
    }

    public static Sprite Icon(int index)
    {
        ItemDef item = Get(index);

        if (item == null ||
            item.icon == null)
            return null;

        return SpriteFor(item.icon);
    }

    public static AOCharacterRenderer.DirectionVisual[]
        BuildEquipmentVisuals(
            ItemDef armor,
            ItemDef helmet,
            ItemDef weapon,
            ItemDef shield)
    {
        var result =
            new AOCharacterRenderer.DirectionVisual[4];

        for (int h = 1; h <= 4; h++)
        {
            result[h - 1] =
                new AOCharacterRenderer.DirectionVisual {
                    heading = h,
                    body =
                        Frames(
                            Find(armor, h, "body")),
                    head =
                        new Sprite[0],
                    helmet =
                        Frames(
                            Find(helmet, h, "helmet")),
                    weapon =
                        Frames(
                            Find(weapon, h, "weapon")),
                    shield =
                        Frames(
                            Find(shield, h, "shield"))
                };
        }

        return result;
    }

    static FrameSpec[] Find(
        ItemDef item,
        int heading,
        string part)
    {
        if (item == null ||
            item.visualDirections == null)
            return new FrameSpec[0];

        foreach (DirectionSpec d in item.visualDirections)
        {
            if (d == null ||
                d.heading != heading)
                continue;

            if (part == "body")
                return d.body ??
                       new FrameSpec[0];

            if (part == "helmet")
                return d.helmet ??
                       new FrameSpec[0];

            if (part == "weapon")
                return d.weapon ??
                       new FrameSpec[0];

            if (part == "shield")
                return d.shield ??
                       new FrameSpec[0];
        }

        return new FrameSpec[0];
    }

    static Sprite[] Frames(
        FrameSpec[] specs)
    {
        if (specs == null ||
            specs.Length == 0)
            return new Sprite[0];

        Sprite[] result =
            new Sprite[specs.Length];

        for (int i = 0;
             i < specs.Length; i++)
        {
            result[i] =
                SpriteFor(specs[i]);
        }

        return result;
    }

    static Sprite SpriteFor(
        FrameSpec frame)
    {
        if (frame == null)
            return null;

        string key =
            string.IsNullOrEmpty(
                frame.key)
            ? "f" + frame.fileNum +
              "_" + frame.sx +
              "_" + frame.sy +
              "_" + frame.width +
              "_" + frame.height
            : frame.key;

        if (sprites.TryGetValue(
                key,
                out Sprite cached))
            return cached;

        Texture2D texture =
            Texture(frame.fileNum);

        if (texture == null)
            return null;

        int unityY =
            texture.height -
            frame.sy -
            frame.height;

        if (frame.sx < 0 ||
            unityY < 0 ||
            frame.width <= 0 ||
            frame.height <= 0 ||
            frame.sx + frame.width >
                texture.width ||
            unityY + frame.height >
                texture.height)
        {
            Debug.LogWarning(
                "AO Items v0.10: recorte inválido " +
                key);
            return null;
        }

        Sprite sprite =
            Sprite.Create(
                texture,
                new Rect(
                    frame.sx,
                    unityY,
                    frame.width,
                    frame.height),
                new Vector2(0.5f, 0f),
                32f,
                0,
                SpriteMeshType.FullRect);

        sprite.name = key;
        sprites[key] = sprite;

        return sprite;
    }

    static Texture2D Texture(
        int fileNum)
    {
        if (textures.TryGetValue(
                fileNum,
                out Texture2D cached))
            return cached;

        Texture2D texture =
            Resources.Load<Texture2D>(
                TextureResource + fileNum);

        if (texture == null)
        {
            Debug.LogWarning(
                "AO Items v0.10: falta tex_" +
                fileNum);
            return null;
        }

        textures[fileNum] = texture;
        return texture;
    }

    static void EnsureLoaded()
    {
        if (loaded)
            return;

        TextAsset text =
            Resources.Load<TextAsset>(
                DatabaseResource);

        if (text == null)
        {
            Debug.LogError(
                "AO Items v0.10: no encuentro " +
                DatabaseResource + ".json");

            loaded = true;
            return;
        }

        Database db =
            JsonUtility.FromJson<Database>(
                text.text);

        byId.Clear();

        if (db != null &&
            db.items != null)
        {
            foreach (ItemDef item in db.items)
            {
                if (item != null &&
                    item.index > 0)
                {
                    byId[item.index] = item;
                }
            }
        }

        loaded = true;

        Debug.Log(
            "[AO v0.10] Items cargados: " +
            byId.Count);
    }
}
