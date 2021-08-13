// UV Save Window
// by Receptor /2012/
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class UVSaveWindow : EditorWindow {
	
	protected static UVSaveWindow uvSaveWindow;

	private string PNGFileName = "UV";
	private int uvTextureWidth = 512;
	private int uvTextureHeight = 512;
	
	public Vector2[] uvsToRender;
	public int[] trianglesToRender;
	
	private bool saveAlphaChannel = true;

	protected static void Start () {
		
		uvSaveWindow = (UVSaveWindow)EditorWindow.GetWindow (typeof (UVSaveWindow));
		
	}
	
	void OnGUI() {

			PNGFileName = EditorGUILayout.TextField("File Name (*.PNG)",PNGFileName, GUILayout.Width(230));
			GUILayout.Space(4);
			uvTextureWidth = EditorGUILayout.IntField ( "Width", uvTextureWidth, GUILayout.Width(230));
			GUILayout.Space(4);
			uvTextureHeight = EditorGUILayout.IntField ( "Height", uvTextureHeight, GUILayout.Width(230));
			GUILayout.Space(4);
			saveAlphaChannel = EditorGUILayout.Toggle("Alpha Channel", saveAlphaChannel);
			
			GUILayout.Space(8);
		
			if(GUILayout.Button("Save", GUILayout.Width(128))){
						
				SaveToPNG(CreateUVTexture(uvTextureWidth,uvTextureHeight, Color.cyan, uvsToRender, trianglesToRender), PNGFileName);
				EditorApplication.Beep();
				EditorUtility.DisplayDialog("Done", "Is done!", "OK");
				this.Close();
			
			}

	}
	
	void OnLostFocus(){
		this.Close();
	}
	
	private void DrawLineToTexture(Vector2 posA, Vector2 posB, Texture2D texture, Color lineColor){
		
		int deltaX = Mathf.Abs((int)posB.x - (int)posA.x);
    	int deltaY = Mathf.Abs((int)posB.y - (int)posA.y);
    	int signX = (int)posA.x < (int)posB.x ? 1 : -1;
    	int signY = (int)posA.y < (int)posB.y ? 1 : -1;
		
		int error = deltaX - deltaY;
		
		texture.SetPixel((int)posB.x, (int)posB.y, lineColor);
		
		while((int)posA.x != (int)posB.x || (int)posA.y != (int)posB.y) {
			
	        texture.SetPixel((int)posA.x, (int)posA.y, lineColor);
	        int error2 = error * 2;
	        
	        if(error2 > -deltaY) {
	            error -= deltaY;
	            posA.x += signX;
	        }
			
	        if(error2 < deltaX) {
	            error += deltaX;
	            posA.y += signY;
	        }
    	}
	
	}
	
	private Texture2D CreateUVTexture(int width, int height, Color lineColor, Vector2[] uv, int[] triangles){
		
		Texture2D texture = new Texture2D(width,height);
			
		for (int i = 0; i < texture.width; i++){
						
			for(int r = 0; r < texture.height; r++){
					if(saveAlphaChannel){
						texture.SetPixel(i,r, Color.clear);
					}else{
						texture.SetPixel(i,r, Color.black);
					}
			}
		}
						
		for (int i = 0; i < triangles.Length; i+=3){
			DrawLineToTexture(uv[triangles[i+1]]*texture.width, uv[triangles[i]]*texture.width, texture, lineColor);
			DrawLineToTexture(uv[triangles[i+2]]*texture.width, uv[triangles[i+1]]*texture.width, texture, lineColor);
			DrawLineToTexture(uv[triangles[i+2]]*texture.width, uv[triangles[i]]*texture.width, texture, lineColor);
		}
						
		texture.Apply();
		
		
		return texture;
		
	}
	
	private void SaveToPNG(Texture2D uvTexture, string fileName){
		
    		byte[] bytes = uvTexture.EncodeToPNG();
    		Stream file = File.Open(Application.dataPath + "/"+fileName + ".PNG",FileMode.Create);
   		 	BinaryWriter binary = new BinaryWriter(file);
    		binary.Write(bytes);
    		file.Close();
			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		
 	}
}
#endif