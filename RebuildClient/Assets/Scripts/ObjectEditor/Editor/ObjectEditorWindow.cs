using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ObjectEditorWindow : EditorWindow
{
	[MenuItem("Ragnarok/Object Editor")]
	static void Init()
	{
		var window = (ObjectEditorWindow)GetWindow(typeof(ObjectEditorWindow), false, "Object Editor");
		window.Show();
	}
}
