using UnityEngine;

[DisallowMultipleComponent]
public class AOCharacterIdentityV170 : MonoBehaviour
{
    [SerializeField]
    string characterName =
        "Aventurero";

    [SerializeField]
    int homeCityId = 1;

    [SerializeField]
    int homeMap = 1;

    [SerializeField]
    int homeX = 57;

    [SerializeField]
    int homeY = 44;

    public string CharacterName =>
        string.IsNullOrWhiteSpace(
            characterName)
        ? "Aventurero"
        : characterName;

    public int HomeCityId =>
        homeCityId;

    public int HomeMap =>
        homeMap;

    public int HomeX =>
        homeX;

    public int HomeY =>
        homeY;

    public void Configure(
        string newName,
        int cityId,
        int map,
        int x,
        int y)
    {
        characterName =
            string.IsNullOrWhiteSpace(
                newName)
            ? "Aventurero"
            : newName.Trim();

        homeCityId =
            Mathf.Max(
                1,
                cityId);

        homeMap =
            Mathf.Max(
                1,
                map);

        homeX =
            Mathf.Max(
                1,
                x);

        homeY =
            Mathf.Max(
                1,
                y);
    }
}
