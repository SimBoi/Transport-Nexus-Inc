using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-99)]  // right after InputSystem's -100 execution order
public class UIClickThroughBlocker : MonoBehaviour
{
    InputAction[] blockableActions;
    InputAction pointAction;
    InputAction clickAction;

    void Start()
    {
        blockableActions = new InputAction[2];
        blockableActions[0] = InputSystem.actions.FindAction("Navigate");
        blockableActions[1] = InputSystem.actions.FindAction("FocusStructure");

        pointAction = InputSystem.actions.FindAction("Point");
        clickAction = InputSystem.actions.FindAction("Click");
    }

    void Update()
    {
        if (clickAction.WasPressedThisFrame())
        {
            Vector2 pointerPos = pointAction.ReadValue<Vector2>();
            if (IsOverUi(pointerPos))
            {
                foreach (InputAction action in blockableActions)
                {
                    if (action.WasPressedThisFrame())
                    {
                        action.Reset();
                        action.Disable();
                    }
                }
            }
        }
        else if (clickAction.WasReleasedThisFrame())
        {
            foreach (InputAction action in blockableActions)
                action.Enable();
        }
    }

    static bool IsOverUi(Vector2 pointerPos)
    {
        var eventDataCurrentPosition = new PointerEventData(EventSystem.current)
        {
            position = pointerPos
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        for (int i = 0; i < results.Count; i++)
            if (results[i].gameObject.layer == 5) //5 = UI layer
                return true;
        return false;
    }
}
