using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts;
using Assets.Scripts.MapEditor;
using Assets.Scripts.MapEditor.Editor;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Editor
{
	public enum MapEditMode
	{
		Mesh,
		Texture,
		Settings
	}

	public class RoMapEditorWindow : EditorWindow
	{
		[MenuItem("Ragnarok/Map Editor")]
		static void Init()
		{
			var window = (RoMapEditorWindow)GetWindow(typeof(RoMapEditorWindow), false, "Map Editor");
			window.Show();
		}

		private GameObject targetGameObject;
		private RoMapEditor currentEditor;
		private RoMapData currentData;

		private IMapBrush[] mapBrushList;
		private string[] mapBrushNames;
		public int CurrentBrushId;
		private IMapBrush currentBrush;

		public MapEditMode EditMode;

		private bool isInitialized;
		private Vector2 scrollPosition;

		private void LoadBrushList()
		{
			var domain = AppDomain.CurrentDomain;
			var assemblies = domain.GetAssemblies();

			var attrDictionary = new Dictionary<Type, MapBrushAttribute>();
			var types = new List<Type>();

			foreach (var a in assemblies)
			{
				foreach (var t in a.GetTypes())
				{
					if (!types.Contains(t) && t.GetCustomAttribute(typeof(MapBrushAttribute)) is MapBrushAttribute attr)
					{
						attrDictionary.Add(t, attr);
						types.Add(t);
					}
				}
			}

			mapBrushList = new IMapBrush[types.Count];
			mapBrushNames = new string[types.Count];

			var sorted = types.OrderBy(t => attrDictionary[t].Order).ToList();
			for (var i = 0; i < types.Count; i++)
			{
				var t = sorted[i];
				if (Activator.CreateInstance(t) is IMapBrush brush)
				{
					mapBrushList[i] = brush;
					mapBrushNames[i] = attrDictionary[t].Name;
				}
			}

			if (CurrentBrushId > 0 && CurrentBrushId > mapBrushList.Length - 1)
				CurrentBrushId = 0;
			currentBrush = mapBrushList[CurrentBrushId];
		}

		private void EnsureBrushEnabled()
		{
			if (currentBrush == null)
				currentBrush = mapBrushList[CurrentBrushId];

			if (!currentBrush.IsEnabled())
				currentBrush.OnEnable(this, currentEditor);

		}

		private bool HasSelectedMap()
		{
			if (Selection.activeGameObject != null)
			{
				var selEditor = Selection.activeGameObject.GetComponent<RoMapEditor>();
				if (selEditor != null)
				{
					if (currentEditor != null && selEditor.gameObject != currentEditor.gameObject)
					{
						LeaveEditorMode();
					}

					targetGameObject = Selection.activeGameObject;
					currentEditor = selEditor;
					currentData = currentEditor.MapData;

					if (currentData != null)
						return true;
				}
			}

			LeaveEditorMode();

			return false;
		}


		public void LeaveEditorMode()
		{
			//Debug.Log("LeaveEditMode");

			if (currentEditor != null)
				currentEditor.LeaveEditMode();

			currentBrush?.OnDisable();
			currentBrush = null;

			targetGameObject = null;
			//currentEditor = null;
			//currentData = null;
		}

		private int createWidth = 64;
		private int createHeight = 64;

		public void CreateNewMap(string savePath)
		{
			currentData = ScriptableObject.CreateInstance<RoMapData>();
			AssetDatabase.CreateAsset(currentData, savePath);

			currentData.CreateNew(createWidth, createHeight);

			LoadDataInScene();
		}

		public void LoadDataInScene()
		{
			targetGameObject = new GameObject(currentData.name);
			currentEditor = targetGameObject.AddComponent<RoMapEditor>();

			currentEditor.Initialize(currentData);
			currentEditor.EnterEditMode();
			Selection.activeGameObject = targetGameObject;
		}

		public void UnselectedGUI()
		{
			//EditorGUILayout.LabelField("To load an existing map, select a map in scene or use the load button on the map asset in the project folder.");

			currentData = (RoMapData)EditorGUILayout.ObjectField("Map File", currentData, typeof(RoMapData), false);

			if (GUILayout.Button("Load") && currentData != null)
			{
				LoadDataInScene();
			}

			EditorGuiLayoutUtility.HorizontalLine();

			EditorGUILayout.LabelField("Create a new map:");

			const int stepSize = 64;

			createWidth = stepSize * ((EditorGUILayout.IntSlider("Width", createWidth, stepSize, 512)) / stepSize);
			createHeight = stepSize * ((EditorGUILayout.IntSlider("Height", createHeight, stepSize, 512)) / stepSize);

			if (GUILayout.Button("Create New"))
			{
				var path = EditorUtility.SaveFilePanelInProject("Create New Map", "mapname", "asset", "Map Asset");
				if (!string.IsNullOrEmpty(path))
					CreateNewMap(path);
			}
		}

		private void SwapMapBrushes(int newBrushId)
		{
			currentBrush.OnDisable();
			currentBrush = mapBrushList[newBrushId];
			currentBrush.OnEnable(this, currentEditor);
			CurrentBrushId = newBrushId;
		}

		private void Initialize()
		{
			LoadBrushList();
			isInitialized = true;
		}

		public void EditModeTexture()
		{
			var dropArea = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.Height(60));
			var boxStyle = new GUIStyle(GUI.skin.box);
			boxStyle.alignment = TextAnchor.MiddleCenter;
			boxStyle.normal.textColor = Color.white;
			GUI.Box(dropArea, "Add Texture (Drop Here)", boxStyle);

			if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
			{
				if (!dropArea.Contains(Event.current.mousePosition))
					return;

				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

				if (Event.current.type == EventType.DragPerform)
				{
					DragAndDrop.AcceptDrag();

					var objects = DragAndDrop.objectReferences;
					var textures = new List<Texture2D>();

					foreach (var o in objects)
					{
						if (o is Texture2D tex)
							textures.Add(tex);
					}

					currentData.AddTextures(textures);
					currentEditor.UpdateAtlasTexture();
				}

				return;
			}

			var removeTexture = new List<Texture2D>();

			foreach (var tex in currentData.Textures)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.ObjectField(tex, typeof(Texture2D), false);
				var style = new GUIStyle(GUI.skin.button) { normal = { textColor = new Color(1f, 0.7f, 0.7f) } };
				if (GUILayout.Button("X", style, GUILayout.Width(25)))
					removeTexture.Add(tex);
				EditorGUILayout.EndHorizontal();
			}

			foreach (var r in removeTexture)
				currentData.RemoveTexture(r);

			if (removeTexture.Count > 0)
				currentEditor.UpdateAtlasTexture();
		}

		public void EditModeMesh()
		{
			currentEditor.DragSeparated = EditorGUILayout.Toggle("Vertical Segments", currentEditor.DragSeparated);

			currentEditor.HeightSnap = EditorGUILayout.DelayedFloatField("Snap Interval", currentEditor.HeightSnap);

			EditorGuiLayoutUtility.HorizontalLine();

			var newMode = EditorGUILayout.Popup("Current brush", CurrentBrushId, mapBrushNames);
			if (newMode != CurrentBrushId)
				SwapMapBrushes(newMode);

			EditorGuiLayoutUtility.HorizontalLine();

			if (mapBrushList == null || mapBrushList.Length == 0)
			{
				EditorGUILayout.LabelField("No brushes exist in the project to use.");
				return;
			}
			else
			{
				EnsureBrushEnabled();

				currentBrush.EditorUI();
			}

			EditorGuiLayoutUtility.HorizontalLine();

			if (currentEditor.CursorVisible)
			{
				var cell = currentData.Cell(currentEditor.HoveredTile);

                var uvMin = new Vector2(1f, 1f);
                var uvMax = new Vector2(0f, 0f);

                if (cell.Top != null)
                {
                    for (var j = 0; j < 4; j++)
                    {
                        uvMin = Vector2.Min(uvMin, cell.Top.UVs[j]);
                        uvMax = Vector2.Max(uvMax, cell.Top.UVs[j]);
                    }
                }


                EditorStyles.label.wordWrap = true;
				EditorGUILayout.LabelField($"Hover Tile: {currentEditor.HoveredTile}\n{cell.ToString().Replace("|", "\n")}\n{uvMin} {uvMax}");


                //if (Event.current.isKey && Event.current.shift && Event.current.keyCode == KeyCode.C)
                //    Debug.Log("HI");
			}
		}

		public void EditModeSettings()
		{
			if (GUILayout.Button("Rebuild Map"))
				currentEditor.Reload();

			if (GUILayout.Button("Rebuild Atlas"))
			{
				currentData.RebuildAtlas();
				currentEditor.UpdateAtlasTexture();
			}

			if (GUILayout.Button("Refresh Texture Lookup"))
				currentData.RefreshTextureLookup();

			if (GUILayout.Button("Rebuild Secondary UVs"))
				currentEditor.RebuildUVDataInArea(currentData.Rect);
			
			if(GUILayout.Button("Remove Color From Selected Tiles"))
				currentEditor.RemoveColorFromTiles();
			
			var paint = GUILayout.Toggle(currentEditor.PaintEmptyTileColorsBlack, "Black Out Empty Tile Vertex Colors");
			if (paint != currentEditor.PaintEmptyTileColorsBlack)
			{
				currentEditor.PaintEmptyTileColorsBlack = paint;
				currentEditor.Reload();
			}

			if (currentEditor.MapData.IsWalkTable)
			{
				EditorGuiLayoutUtility.HorizontalLine();

				if (currentEditor.IsEditorStartupMode)
				{
					if (GUILayout.Button("Cancel Debug Startup"))
						currentEditor.IsEditorStartupMode = false;
				}
				else
				{
					if (GUILayout.Button("Enter Debug Startup"))
					{
						currentEditor.IsEditorStartupMode = true;
						currentEditor.HasSelection = false;
						
					}
				}
			}
		}

		public void OnGUI()
		{
			if (!isInitialized || mapBrushList == null)
				Initialize();

			var bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, clipping = TextClipping.Overflow };

			GUILayout.Space(10);
			EditorGUILayout.LabelField("Map Editor", bigStyle);
			GUILayout.Space(10);
			EditorGuiLayoutUtility.HorizontalLine();

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Mesh")) EditMode = MapEditMode.Mesh;
			if (GUILayout.Button("Texture")) EditMode = MapEditMode.Texture;
			if (GUILayout.Button("Settings")) EditMode = MapEditMode.Settings;
			EditorGUILayout.EndHorizontal();

			var hasMap = HasSelectedMap();

			if (hasMap)
			{
				if (currentEditor.gameObject.isStatic && !currentData.IsWalkTable)
				{
					if (GUILayout.Button("Enter Edit Mode"))
						currentEditor.RemoveStatic();

					LeaveEditorMode();
					return;
				}

				if (currentEditor.CurrentMode == Scripts.MapEditor.EditMode.Startup)
					currentEditor.CurrentMode = Scripts.MapEditor.EditMode.Height;

				EditorGUILayout.BeginHorizontal();

				if (GUILayout.Button("Copy"))
				{
					if (currentEditor.HasSelection)
						currentData.Copy(currentEditor.SelectedRegion);
				}

				if (GUILayout.Button("Paste"))
				{
					if (currentEditor.HasSelection && currentEditor.SelectedRegion.size == Vector2Int.one)
					{
						if (currentData.Paste(currentEditor.SelectedRegion.min, out var area))
							currentEditor.RebuildMeshInArea(area);
					}
				}

				if (GUILayout.Button("Undo"))
				{
					if (currentData.UndoChange(out var changed))
					{
						currentEditor.RebuildMeshInArea(changed.ExpandRect(1));
						currentBrush.Repaint();
					}
				}

				EditorGUILayout.EndHorizontal();

				if (!currentData.IsWalkTable)
				{
					if (GUILayout.Button("Leave Edit Mode"))
					{
						currentEditor.MakeStatic();
						currentEditor.LeaveEditMode();
						return;
					}
				}
			}

			EditorGuiLayoutUtility.HorizontalLine();
			scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);

			if (!hasMap)
			{
				if (currentBrush != null && currentBrush.IsEnabled())
					currentBrush.OnDisable();

				if (EditMode == MapEditMode.Texture && currentData != null && currentEditor != null)
					EditModeTexture();
				else
					UnselectedGUI();

				GUILayout.EndScrollView();
				return;
			}


			currentEditor.EnterEditMode();
			EnsureBrushEnabled();

			switch (EditMode)
			{
				case MapEditMode.Mesh:
					EditModeMesh();
					break;
				case MapEditMode.Texture:
					EditModeTexture();
					break;
				case MapEditMode.Settings:
					EditModeSettings();
					break;
			}

			GUILayout.EndScrollView();
		}

		public void OnBecameInvisible()
		{
			currentBrush?.OnDisable();
			currentBrush = null;

			LeaveEditorMode();
		}

		public void OnInspectorUpdate()
		{
			Repaint();
		}

	}
}
