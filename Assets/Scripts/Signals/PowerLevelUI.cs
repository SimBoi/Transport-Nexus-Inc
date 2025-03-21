using Signals;
using UnityEngine;
using TMPro;

public class PowerLevelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text textUi;
    private Vector3 position;
    private Port port1;
    private Port port2;

    public void Initialize(Port port1, Port port2)
    {
        position = (port1.transform.position + port2.transform.position) / 2;
        this.port1 = port1;
        this.port2 = port2;
    }

    void Update()
    {
        if (port1 == null || port2 == null || port1.signalChannel != port2.signalChannel || port1.signalChannel == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Camera.main.WorldToScreenPoint(position);
        textUi.text = port1.signalChannel.Read().ToString();
    }
}
