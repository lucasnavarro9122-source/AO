using UnityEngine;

public class AOWorldObjectMetadata : AOInteractable
{
    [SerializeField] int objIndex;
    [SerializeField] int objType;
    [SerializeField] int amount;
    [SerializeField] int grhIndex;

    public int ObjIndex => objIndex;
    public int ObjType => objType;
    public int Amount => amount;
    public int GrhIndex => grhIndex;

    public void ConfigureObject(
        int index, int x, int y, string objectName, string desc,
        int type, int objectAmount, int grh)
    {
        objIndex = index;
        objType = type;
        amount = objectAmount;
        grhIndex = grh;

        // El CSM ya aporta gran parte de las colisiones de objetos.
        // No añadimos bloqueo extra todavía para evitar duplicar reglas.
        ConfigureInteraction(x, y, objectName, desc, false);
    }
}
