using UnityEngine;
using UnityEngine.EventSystems;

public class TouchMoveButton : MonoBehaviour, IPointerDownHandler
{
    public Vector3 direction;
    public PlayerController player;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManager.Instance != null && GameManager.Instance.RegisterEscapeInput(true)) return;
        if (player != null) player.QueueMove(direction);
    }
}
