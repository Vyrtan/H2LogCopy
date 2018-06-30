using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class CubeMove : MonoBehaviour
{
	[SerializeField] private UIScript theUiScript;

	void Update()
	{
		Vector3 movePos = new Vector3();
		movePos.x = Input.GetAxis("Horizontal");
		movePos.z = Input.GetAxis("Vertical");

		transform.position += movePos * 2f * Time.deltaTime;
	}

	void OnMouseDown()
	{
		theUiScript.Show();
	}
}
