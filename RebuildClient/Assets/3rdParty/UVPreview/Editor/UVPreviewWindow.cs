// UV Preview Window
// by Receptor /2012/
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class UVPreviewWindow : EditorWindow {
	
	protected static UVPreviewWindow uvPreviewWindow;
	
	private int windowDefaultSize = 562; // 512 + (sideSpace*2)
	private int ySpace = 75;
	private int sideSpace = 25;
	private Rect uvPreviewRect;
	
	private float scale = 1;

	private GameObject selectedObject = null;
	private Mesh m = null;
	private int[] tris;
    private Vector2[] uvs;
	private Rect screenCenter;
	private bool isStarted;
	
	private Texture2D fillTextureGray;
	private Texture2D fillTextureDark;

	private float xPanShift;
	private float yPanShift;

	private int gridStep = 16;
	
	private bool canDrawView;
	private bool mousePositionInsidePreview;
	
	private int selectedUV = 0;
	private string[] selectedUVStrings = new string[2];
	
	private Material lineMaterial;
	
	[MenuItem ("Window/UV Preview")]

	protected static void Start () {
		
		uvPreviewWindow = (UVPreviewWindow)EditorWindow.GetWindow(typeof(UVPreviewWindow));
		uvPreviewWindow.title = "UV Preview";
		uvPreviewWindow.autoRepaintOnSceneChange = true;
		uvPreviewWindow.minSize = new Vector2(256,256);
		
	}
	

	void Update () {
		
		if(!isStarted) {
			
			screenCenter = new Rect(Screen.width/2, Screen.height/2, 1, 1);
			uvPreviewWindow.position = new Rect(screenCenter.x, screenCenter.y, windowDefaultSize, windowDefaultSize + ySpace);
			fillTextureGray = CreateFillTexture(1,1, new Color(0,0,0,0.1f));
			fillTextureDark = CreateFillTexture(1,1, new Color(0,0,0,0.5f));
			
			xPanShift = 0;
			yPanShift = 0;
			
			isStarted = true;
			
			selectedUVStrings[0] = "UV";
			selectedUVStrings[1] = "UV 2";

		}

	}
	
	void OnSelectionChange(){

	}
	
	void OnGUI () {
		
		Event e = Event.current;
		
		selectedObject = Selection.activeGameObject;
	
		if(selectedObject == null){
			
			GUI.color = Color.gray;
			EditorGUILayout.HelpBox("Select the object...", MessageType.Warning);
			canDrawView = false;
			
		}else{
			
			if(selectedObject.GetComponent<MeshFilter>() != null | selectedObject.GetComponent<SkinnedMeshRenderer>() != null){
				
				GUI.color = Color.green;
				EditorGUILayout.HelpBox("Selected object: " + selectedObject.name, MessageType.None);
				GUI.color = Color.white;
				canDrawView = true;
				
				if(selectedObject.GetComponent<SkinnedMeshRenderer>() == null){
					m = selectedObject.GetComponent<MeshFilter>().sharedMesh;
				}else{
					m = selectedObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
				}
				
				if(m != null){
					
				if(m.uv2.Length > 0){
				
					selectedUV = GUILayout.Toolbar(selectedUV, selectedUVStrings);
				
				}else{
				
					selectedUV = 0;
				
					GUILayout.BeginHorizontal();
				
					EditorGUILayout.HelpBox("Mesh is not have UV 2. You can generate it", MessageType.None);
				
					if(GUILayout.Button("Generate UV2")){
						Unwrapping.GenerateSecondaryUVSet(m);
						EditorApplication.Beep();
						EditorUtility.DisplayDialog("Done", "Process is done!", "OK");
					}
				
						GUILayout.EndHorizontal();
				}
					
					tris = m.triangles;
					
					if(selectedUV == 0){
						uvs = m.uv;
					}else{
						uvs = m.uv2;
					}
				}
				
			}else{
				
				GUI.color = Color.gray;
				EditorGUILayout.HelpBox("Object must have a Mesh Filter or Skinned Mesh Renderer", MessageType.Warning);
				canDrawView = false;
				
			}
			
		}
		
		if( e.mousePosition.x > uvPreviewRect.x & e.mousePosition.x < uvPreviewRect.width+sideSpace & e.mousePosition.y > uvPreviewRect.y & e.mousePosition.y < uvPreviewRect.height+sideSpace+ySpace){
			mousePositionInsidePreview = true;
		}else{
			mousePositionInsidePreview = false;
		}
		
		if(mousePositionInsidePreview){
			
			if(e.type == EventType.MouseDrag){
				xPanShift += e.delta.x;
				yPanShift += e.delta.y;
			}
			
			if(e.type == EventType.ScrollWheel){
				scale += -(e.delta.y*0.02f);
			}
			
		}
		
		

		uvPreviewRect = new Rect(new Rect(sideSpace, ySpace+sideSpace, uvPreviewWindow.position.width-(sideSpace*2), uvPreviewWindow.position.height-ySpace-(sideSpace*2)));

		GUI.DrawTexture(new Rect(0,0, uvPreviewWindow.position.width, ySpace), fillTextureGray);
		
		
		if(canDrawView){
		
			GUI.DrawTexture(uvPreviewRect, fillTextureDark);
			
			//GRID
			for(int i = 1; i < 4096; i+= (int)(gridStep)){
				
				int x1h = (int)(uvPreviewRect.x-1);
				int x2h = (int)(uvPreviewRect.width+sideSpace);
				int yh = i+(ySpace+sideSpace)-1;

				int y1v = ySpace+sideSpace;
				int y2v = (int)(uvPreviewRect.height+ySpace+sideSpace);
				int xv = i+sideSpace-1;
				
				if(yh < uvPreviewRect.height+ySpace+sideSpace){
					DrawLine(x1h,yh,x2h,yh,new Color(1,1,1,0.15f));
				}
				
				if(xv < uvPreviewRect.width+sideSpace){
					DrawLine(xv,y1v,xv,y2v,new Color(1,1,1,0.15f));
				}

			}
			
			//UV
			for (int i = 0; i < tris.Length; i+=3){
				
				
				int line1x1 = (int)(uvs[tris[i]].x*(scale*windowDefaultSize)+sideSpace+xPanShift);
				int line1y1 = (int)(-uvs[tris[i]].y*(scale*windowDefaultSize)+ySpace+sideSpace+yPanShift)+windowDefaultSize;
				int line1x2 = (int)(uvs[tris[i+1]].x*(scale*windowDefaultSize)+sideSpace+xPanShift);
				int line1y2 = (int)(-uvs[tris[i+1]].y*(scale*windowDefaultSize)+sideSpace+ySpace+yPanShift+windowDefaultSize);
				
				int line2x1 = (int)(uvs[tris[i+1]].x*(scale*windowDefaultSize)+sideSpace+xPanShift);
				int line2y1 = (int)(-uvs[tris[i+1]].y*(scale*windowDefaultSize)+ySpace+sideSpace+yPanShift)+windowDefaultSize;
				int line2x2 = (int)(uvs[tris[i+2]].x*(scale*windowDefaultSize)+sideSpace+xPanShift);
				int line2y2 = (int)(-uvs[tris[i+2]].y*(scale*windowDefaultSize)+sideSpace+ySpace+yPanShift)+windowDefaultSize;
				
				int line3x1 = (int)(uvs[tris[i+2]].x*(scale*windowDefaultSize)+sideSpace+xPanShift);
				int line3y1 = (int)(-uvs[tris[i+2]].y*(scale*windowDefaultSize)+ySpace+sideSpace+yPanShift)+windowDefaultSize;
				int line3x2 = (int)(uvs[tris[i]].x*(scale*windowDefaultSize)+sideSpace+xPanShift);
				int line3y2 = (int)(-uvs[tris[i]].y*(scale*windowDefaultSize)+sideSpace+ySpace+yPanShift)+windowDefaultSize;
				
				Rect cropRect = new Rect(uvPreviewRect.x, uvPreviewRect.y, uvPreviewRect.width+sideSpace, uvPreviewRect.height+ySpace+sideSpace);
				
				DrawLine(line1x1, line1y1, line1x2 , line1y2,  new Color(0,1,1,1), true, cropRect);
				DrawLine(line2x1, line2y1, line2x2 , line2y2,  new Color(0,1,1,1), true, cropRect);
				DrawLine(line3x1, line3y1, line3x2 , line3y2,  new Color(0,1,1,1), true, cropRect);
				
				
			}
			
			DrawLine(0,ySpace-1, (int)uvPreviewWindow.position.width,ySpace-1, Color.gray);
			
			DrawHollowRectangle((int)uvPreviewRect.x, (int)uvPreviewRect.y, (int)uvPreviewRect.width+sideSpace, (int)uvPreviewRect.height+ySpace+sideSpace, Color.gray);
			DrawHollowRectangle((int)uvPreviewRect.x, (int)uvPreviewRect.y, (int)uvPreviewRect.width+sideSpace, (int)uvPreviewRect.height+ySpace+sideSpace, Color.gray, 1);
			DrawHollowRectangle((int)uvPreviewRect.x, (int)uvPreviewRect.y, (int)uvPreviewRect.width+sideSpace, (int)uvPreviewRect.height+ySpace+sideSpace, Color.gray, 2);

			EditorGUIUtility.AddCursorRect(uvPreviewRect, MouseCursor.Pan);

			if(GUILayout.Button("Save To PNG")){

				UVSaveWindow uvSaveWindow = (UVSaveWindow)EditorWindow.GetWindow (typeof (UVSaveWindow));
				uvSaveWindow.title = "Save to PNG";
				uvSaveWindow.maxSize = new Vector2(256,125);
				uvSaveWindow.minSize = new Vector2(256,124);
				uvSaveWindow.uvsToRender = uvs;
				uvSaveWindow.trianglesToRender = tris;

			}

			
		}

		Repaint();
		
	}
	
	private Texture2D CreateFillTexture(int width, int height, Color fillColor) {
		
		Texture2D texture = new Texture2D(width, height);
		Color[] pixels = new Color[width*height];
		
		for (int i = 0; i < pixels.Length; i++) {
			pixels[i] = fillColor;
		}
		
		texture.SetPixels(pixels);
		texture.Apply();
		
		return texture;
	}
	
	
	
	private void DrawLine(int x1, int y1, int x2, int y2, Color lineColor, bool isCrop = false, Rect crop = default(Rect)) {
	
		
		if (!lineMaterial) {
        	lineMaterial = new Material( "Shader \"Lines/Colored Blended\" {" +
           	"SubShader { Pass { BindChannels { Bind \"Color\",color } " +
           	"Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Fog { Mode Off } } } }"
			);
			
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
        }
		
		lineMaterial.SetPass(0);
		
		if(isCrop){
			
			if(x1 < crop.x) x1 = (int)crop.x;
			if(x1 > crop.width) x1 = (int)crop.width;
			if(y1 < crop.y) y1 = (int)crop.y;
			if(y1 > crop.height) y1 = (int)crop.height;
			
			if(x2 < crop.x) x2 = (int)crop.x;
			if(x2 > crop.width) x2 = (int)crop.width;
			if(y2 < crop.y) y2 = (int)crop.y;
			if(y2 > crop.height) y2 = (int)crop.height;
			
		}
		
        GL.Begin(GL.LINES);
        GL.Color(lineColor);
        GL.Vertex3(x1,y1,0);
        GL.Vertex3(x2,y2,0);
        GL.End();
	}
	
	private void DrawHollowRectangle(int x, int y, int width, int height, Color rectangleColor, int expand = 0){
		
			DrawLine(x-expand, y-expand, width+expand, y-expand, rectangleColor);
			DrawLine(x-expand, y-expand, x-expand, height+expand, rectangleColor);
			DrawLine(width+expand, y-expand, width+expand, height+expand, rectangleColor);
			DrawLine(x-expand, height+expand, width+expand, height+expand, rectangleColor);
		
	}

}
#endif