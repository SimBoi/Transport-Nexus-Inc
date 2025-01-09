using UnityEngine;

public class test : MonoBehaviour
{
    public bool isPlacing = false;
    public bool modelVisible = false;

    public void OnDrag()
    {
        modelVisible = isPlacing;
    }

    public void OnEndDrag()
    {
        if (!isPlacing) return;

        Debug.Log("Placed");
        modelVisible = false;
    }

    public void OnPointerExit()
    {
        isPlacing = true;
    }

    public void OnPointerEnter()
    {
        isPlacing = false;
    }

    public void OnPointerClick()
    {
        Debug.Log("Clicked");
    }
}
