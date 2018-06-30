using UnityEngine;
using System.Collections;

public class BuildingMenu : MonoBehaviour {

    public BuildingButton buttonPrefab;
    public BuildingButton selected;

    public void SpawnButtons(Interactable obj)
    {   
        for(int i = 0; i < obj.options.Length; i++) {
            BuildingButton newButton = Instantiate(buttonPrefab) as BuildingButton;
            newButton.transform.SetParent(transform, false);
            float theta = (2 * Mathf.PI / obj.options.Length) * i;
            float xPos = Mathf.Sin(theta);
            float yPos = Mathf.Cos(theta);
            newButton.transform.localPosition = new Vector3(xPos, yPos, 0f) *100f;
            newButton.square.color = obj.options[i].color;
            newButton.icon.sprite = obj.options[i].sprite;
            newButton.title = obj.options[i].title;
            newButton.myMenu = this;
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            if(selected)
            {
                //throw Event for Building
                Debug.Log(selected.title + "selected");
            }
            Destroy(gameObject);
        }
    }
}
