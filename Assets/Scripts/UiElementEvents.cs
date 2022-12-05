using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UiElementEvents : MonoBehaviour, IPointerDownHandler, IPointerExitHandler, IPointerUpHandler
{

    public event Action OnPressed, OnReleased;
    
    private bool isPressed = false;
    
    public void OnPointerDown(PointerEventData eventData)
    {
        //Debug.Log("Pressed");
        isPressed = true;
        OnPressed?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //Debug.Log("Exit");
        if (isPressed)
        {
            isPressed = false;
            OnReleased?.Invoke();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        //Debug.Log("Released");
        if (isPressed)
        {
            isPressed = false;
            OnReleased?.Invoke();
        }
    }
}
