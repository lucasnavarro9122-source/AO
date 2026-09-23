using UnityEngine;

public class AONPCMetadata : AOInteractable
{
    [SerializeField] int npcIndex;
    [SerializeField] int npcType;
    [SerializeField] int movement;
    [SerializeField] int heading;
    [SerializeField] int body;
    [SerializeField] int head;
    [SerializeField] int helmet;
    [SerializeField] int weapon;
    [SerializeField] int shield;

    public int NpcIndex => npcIndex;
    public int NpcType => npcType;
    public int Movement => movement;
    public int Heading => heading;

    public void ConfigureNPC(
        int index, int x, int y, string npcName, string desc,
        int type, int movementType, int initialHeading,
        int bodyIndex, int headIndex, int helmetIndex,
        int weaponIndex, int shieldIndex)
    {
        npcIndex = index;
        npcType = type;
        movement = movementType;
        heading = initialHeading;
        body = bodyIndex;
        head = headIndex;
        helmet = helmetIndex;
        weapon = weaponIndex;
        shield = shieldIndex;

        ConfigureInteraction(x, y, npcName, desc, true);
    }

    public void MoveToTile(int x, int y, int newHeading)
    {
        heading = newHeading;
        ConfigureInteraction(
            x, y,
            DisplayName,
            Description,
            true);
    }

    public void SetHeading(int newHeading)
    {
        heading = newHeading;
    }
}
