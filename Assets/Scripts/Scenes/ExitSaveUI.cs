using UnityEngine;

public class ExitSaveUI : MonoBehaviour
{
    public void ExitWithoutSaving()
    {
        SaveManager.Instance.ExitToMainMenu();
    }

    public void SaveAndExitToMainMenu()
    {
        SaveManager.Instance.SaveAndExitToMainMenu();
    }
}
