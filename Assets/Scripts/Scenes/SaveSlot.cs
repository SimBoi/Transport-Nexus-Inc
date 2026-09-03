using TMPro;
using UnityEngine;

public class SaveSlot : MonoBehaviour
{
    [SerializeField] private int saveSlot;
    [SerializeField] private TMP_Text playtimeUI;
    private bool isEmptySlot;

    void Start()
    {
        SaveMetadata metadata = SaveManager.Instance.GetSaveMetadata(saveSlot);
        if (metadata == null)
        {
            isEmptySlot = true;
            playtimeUI.text = "New Game";
        }
        else
        {
            isEmptySlot = false;
            playtimeUI.text = metadata.playtime.ToString();
        }
    }

    public void LoadGame()
    {
        if (isEmptySlot) SaveManager.Instance.StartNewGame(saveSlot);
        else _ = SaveManager.Instance.LoadGameAsync(saveSlot);
    }
}
