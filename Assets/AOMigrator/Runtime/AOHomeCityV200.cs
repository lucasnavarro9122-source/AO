public static class AOHomeCityV200
{
    // Spawn positions from Ciudades.Dat. Lindos and Forgat use the migrated
    // city maps because their old map numbers now refer to sea maps.
    public static readonly string[] Names =
    {
        "Ullathorpe", "Nix", "Banderbill", "Lindos", "Arghal", "Forgat"
    };

    static readonly int[] Maps = { 1, 34, 59, 62, 151, 751 };
    static readonly int[] X = { 57, 40, 47, 63, 61, 48 };
    static readonly int[] Y = { 44, 87, 41, 39, 43, 65 };

    public static bool TryGet(int id, out string name, out int map,
                              out int x, out int y)
    {
        name = "";
        map = x = y = 0;
        if (id < 1 || id > Names.Length)
            return false;

        int index = id - 1;
        name = Names[index];
        map = Maps[index];
        x = X[index];
        y = Y[index];
        return true;
    }
}
