using UnityEngine;
using System.Collections;

public class BuildingMenuSpawner : MonoBehaviour {

    public static BuildingMenuSpawner ins;
    public BuildingMenu prefab;

    void Awake()
    {
        ins = this;
    }

    public void SpawnMenu(Interactable obj)
    {
        BuildingMenu newMenu = Instantiate(prefab) as BuildingMenu;
        newMenu.transform.SetParent(transform, false);
        newMenu.transform.position = Input.mousePosition;

        newMenu.SpawnButtons(obj);
    }
}

