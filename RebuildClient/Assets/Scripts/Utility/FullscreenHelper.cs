using UnityEngine;

namespace Assets.Scripts.Utility
{
	class FullscreenHelper : MonoBehaviour
	{
		public void Start()
		{

		}

		public void Update()
		{
			if(Input.GetKeyDown(KeyCode.Return) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
			{

			}
		}
	}
}
