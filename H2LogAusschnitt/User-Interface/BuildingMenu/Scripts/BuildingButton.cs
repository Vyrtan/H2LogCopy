using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System;

public class BuildingButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

    public Image square;
    public Image icon;
    public string title;
    public BuildingMenu myMenu;

    Color defaultColor;

    public void OnPointerEnter(PointerEventData eventData)
    {
        myMenu.selected = this;
        defaultColor = square.color;
        square.color = Color.white;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        myMenu.selected = null;
        square.color = defaultColor;
    }
}
