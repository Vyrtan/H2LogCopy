using UnityEngine;
using System.Collections;

public class Interactable : MonoBehaviour {

	
    [System.Serializable]
    public class Action
    {
        public Color color;
        public Sprite sprite;
        public string title;
    }

    public Action[] options;

    void OnMouseDown()
    {
        BuildingMenuSpawner.ins.SpawnMenu(this);
    }
}
