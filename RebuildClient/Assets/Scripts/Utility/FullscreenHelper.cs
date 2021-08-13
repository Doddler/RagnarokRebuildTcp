using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.PlayerLoop;

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
