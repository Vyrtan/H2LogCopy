using UnityEngine;

public class UIScript : MonoBehaviour
{
	[SerializeField] private Transform theCube;
	private RectTransform thisRectTransform;

	void Start()
	{
		thisRectTransform = GetComponent<RectTransform>();
	}
	
	void Update ()
	{
		Vector3 thePos = Camera.main.WorldToScreenPoint(theCube.position);
		thePos.y += 100f;
		thisRectTransform.position = thePos;
	}

	public void Show()
	{
		gameObject.SetActive(true);
	}

	public void Hide()
	{
		gameObject.SetActive(false);
	}
}
