using UnityEngine;

public static class AORPGAutoBootstrapV11
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRPG()
    {
        AOTestPlayer player =
            Object.FindFirstObjectByType
                <AOTestPlayer>();

        if (player == null)
            return;

        AOPlayerRPGV11 rpg =
            player.GetComponent
                <AOPlayerRPGV11>();

        if (rpg == null)
        {
            rpg =
                player.gameObject
                    .AddComponent
                        <AOPlayerRPGV11>();

            rpg.InitializeProfile(
                1,
                1,
                3,
                true);

            Debug.Log(
                "[AO v0.11.1] Perfil automático: Humano / Hombre / Guerrero.");
        }

        AOCharacterProfileVisualV111 visualProfile =
            player.GetComponent
                <AOCharacterProfileVisualV111>();

        if (visualProfile == null)
        {
            visualProfile =
                player.gameObject
                    .AddComponent
                        <AOCharacterProfileVisualV111>();
        }

        visualProfile.SyncFromRPG();

        AOPlayerCombatV09 combat =
            player.GetComponent
                <AOPlayerCombatV09>();

        if (combat != null)
            combat.SyncFromRPG(false);

        AOInventoryV10 inventory =
            player.GetComponent
                <AOInventoryV10>();

        if (inventory != null)
            inventory.ValidateEquipmentForRPG();
    }
}
