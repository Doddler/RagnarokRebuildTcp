using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEditor.SceneManagement;

// Enums for Baking Options
public enum BakeOptions
{
    Individual = 0, // Bake the scenes one by one
    Group = 1, // Load the scenes in the list altogether and bake them in one batch
}

[ExecuteInEditMode]
public class BakeLightmaps : EditorWindow
{
    //-------------------------------------------------------------------------------------------------------------------------
    // LOGGING OPTIONS
    //-------------------------------------------------------------------------------------------------------------------------
    public string LogFile = "log.txt"; //Name of file to store baking results
    public bool EchoToConsole = true; 
    public bool AddTimeStamp = true; // Add timing results to the file
    static public string textOutput = ""; 

    public static StringBuilder BakeLog = new StringBuilder(); // Useful for creating sheets, e.g. in a CSV file
    private StreamWriter OutputStream; // Output stream
    public string bakeFinal; // Text for final baking results 

	System.DateTime timeStamp;
    string titles = "Scene Names, Bake Time";

    //-------------------------------------------------------------------------------------------------------------------------
    // SINGLETON INSTANCE
    //-------------------------------------------------------------------------------------------------------------------------
    static BakeLightmaps Singleton = null;

    public static BakeLightmaps Instance
    {
        get { return Singleton; }
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // GUI Elements
    //-------------------------------------------------------------------------------------------------------------------------
    public BakeOptions display = BakeOptions.Individual;
    public Object[] scenes; // Object array to store the list of scenes
    public LightingConfiguration lightingPreset; // Lighting preset to be assigned

    private EditorBuildSettingsScene[] buildScenes; //Array to the fetch the list of scenes from Build Settings
    
    // Display button and status text
    string bakeButton = "Generate Lighting";
    string status = "Idling";
    string buildSettingScenes = "Load Build Setting Scenes";

    //-------------------------------------------------------------------------------------------------------------------------
    // SCENE MANAGEMENT
    //-------------------------------------------------------------------------------------------------------------------------
    List<string> sceneList = new List<string>(); // List to store scenes
    private int sceneIndex = 0; // Set a scene index value
    private string[] scenePath; // Directory paths of Scenes from EditorGUI
    private string[] buildScenePath; // Directory paths of Scenes from Build Settings

    private bool isGroupBakingDone = false; // Track the process of group baking

	//-------------------------------------------------------------------------------------------------------------------------
	// DEBUG OPTIONS
    //-------------------------------------------------------------------------------------------------------------------------
    public bool printTiming = false; // Print baking times?
    public bool overrideLightSettings = false; // Override lighting settings of a scene with lighting preset?
    public bool logPrintTimes = false; // Log baking times to console?


    //-------------------------------------------------------------------------------------------------------------------------
    void OnEnable()
    {
        buildScenes = EditorBuildSettings.scenes;
        BakeLog.AppendLine(titles);

        if (Singleton != null)
        {
            UnityEngine.Debug.LogError("Multiple Singletons exist!");
            return;
        }

        Singleton = this;

        #if !FINAL
        // Open the log file to append the new log to it
        OutputStream = new StreamWriter(LogFile, true);
        //System.IO.File.WriteAllText("CSVData.csv", LogFile);
        #endif
    }

    //-------------------------------------------------------------------------------------------------------------------------
    void OnDestroy()
    {
        #if !FINAL
        if (OutputStream != null)
        {
            OutputStream.Close();
            OutputStream = null;
        }
        #endif
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Dropdown menu entry
    [MenuItem("Tools/Lightmaps/Simple Baking Manager")]
    public static void ShowWindow()
    {
        EditorWindow window = GetWindow(typeof(BakeLightmaps), false, "Bake Manager");
        window.autoRepaintOnSceneChange = true;

  		Texture icon = AssetDatabase.LoadAssetAtPath<Texture>("Assets/BakeManager/Icon/BakeIcon.png");
  		GUIContent titleContent = new GUIContent("Bake Manager", icon);
  		window.titleContent = titleContent;
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Repaint the EditorGUI on focus
    void OnFocus()
    {
        status = "Idling";
        if (!Lightmapping.isRunning)
        {
            bakeButton = "Generate Lighting";
        }
    }

    //-------------------------------------------------------------------------------------------------------------------------
    void OnGUI()
    {
    	// Display options 
        display = (BakeOptions)EditorGUI.EnumPopup(
            new Rect(3, 3, position.width - 6, 15),
            "Bake Option:",
            display);

        GUILayout.Space(20);
        ScriptableObject target = this;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty scenesProperty = so.FindProperty("scenes");

        EditorGUILayout.PropertyField(scenesProperty, true); // True shows children
        so.ApplyModifiedProperties(); // Apply modified properties

        if (GUILayout.Button(bakeButton)) // Button to initiate baking
        {
            StartBake();
        }

        if (GUILayout.Button(buildSettingScenes)) // Button to fetch scenes from Build Settings
        {
            LoadBuildSettingScenes();
        }

        GUILayout.Space(20);
        overrideLightSettings = EditorGUILayout.Toggle("Override Lighting Settings", overrideLightSettings); // Toggle to assign Lighting Preset
     	ScriptableObject preset = this;
        SerializedObject scriptableObj = new SerializedObject(preset); 
        SerializedProperty lightingProperty = scriptableObj.FindProperty("lightingPreset");

        EditorGUILayout.PropertyField(lightingProperty); // True means show children
        scriptableObj.ApplyModifiedProperties(); // Remember to apply modified properties 

      	GUILayout.Space(20);
      	EditorGUILayout.LabelField("Debug: ");
        printTiming = EditorGUILayout.Toggle("Print Timing", printTiming); //Toggle to activate printing timing 
        logPrintTimes = EditorGUILayout.Toggle("Log Debug Values", logPrintTimes); // Toggle to print baking times to console log

      	GUILayout.Space(10);
        EditorGUILayout.LabelField("Status:  ", status); // Show baking status
        so.Update();
    }

    //-------------------------------------------------------------------------------------------------------------------------
    void StartBake()
    {
    	// If lightmapping is not running, first set scene list and set delegates
    	// Then start generating lighting
        if (!Lightmapping.isRunning)
        {
            Lightmapping.completed = null;
            Lightmapping.completed = SaveScene;
            Lightmapping.completed += BakeNewScene;
            if (display == BakeOptions.Individual)
            {
                SetScenes();
            }
            BakeNewScene();
        }
        else
        {
        	// While baking...
            Lightmapping.Cancel();
            UpdateLightmappingProcess();
        }
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Create an array of scenes for lightmapping
    private bool SetScenes()
    {
        // Reset
        sceneList.Clear();
        sceneIndex = 0;

        // Get directory paths of scenes
        if (scenes.Length == 0)
        {
            status = "No scenes found";
            bakeButton = "Generate Lighting";
            return false;
        }
        else
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                sceneList.Add(AssetDatabase.GetAssetPath(scenes[i]));
            }
            scenePath = sceneList.ToArray();
            return true;
        }
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Fetch the directory path of each scene from Build Settings
    private bool LoadBuildSettingScenes()
    {
        // Reset
        sceneList.Clear();
        sceneIndex = 0;
        
        if(logPrintTimes)
        	UnityEngine.Debug.Log((int)buildScenes.Length);

        scenes = new Object[buildScenes.Length];
        int countItem = (int) buildScenes.Length;

        for (int i = 0; i<buildScenes.Length; i++)
        {
        	if(logPrintTimes)
        		UnityEngine.Debug.Log(buildScenes[i].path);

            scenes[i] = AssetDatabase.LoadAssetAtPath(buildScenes[i].path, typeof(Object));
        }
      
       return true;
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Determine which baking option is used
    // Then iterate through scene list to generate lighting and keep track of process
    private void BakeNewScene()
    {
        if (display == BakeOptions.Individual && !overrideLightSettings)
        {
            if (sceneIndex < scenes.Length)
            {
                EditorSceneManager.OpenScene(scenePath[sceneIndex], OpenSceneMode.Single);
                timeStamp = System.DateTime.Now;
                Lightmapping.BakeAsync();
                UpdateLightmappingProcess();
                sceneIndex++;
            }
            else
            {
                DoneLightmapping("done");
                if(printTiming)
                {
					SaveOutputGrid(BakeLog);
                	BakeLightmaps.Trace(" ");
                }                
            }
        }
        else if(display == BakeOptions.Individual && overrideLightSettings && lightingPreset != null)
        {
            if (sceneIndex < scenes.Length)
            {
                EditorSceneManager.OpenScene(scenePath[sceneIndex], OpenSceneMode.Single);
                lightingPreset.Load();
                timeStamp = System.DateTime.Now;
                Lightmapping.BakeAsync();
                UpdateLightmappingProcess();
                sceneIndex++;
            }
            else
            {
                DoneLightmapping("done");
                if(printTiming)
                {
					SaveOutputGrid(BakeLog);
                	BakeLightmaps.Trace(" ");
                }    
            }
        }
        else if((display == BakeOptions.Individual || display == BakeOptions.Group) && overrideLightSettings && lightingPreset == null)
        {
        	UnityEngine.Debug.LogError("Please assign a Lighting Preset to override lighting settings for scenes");
        }

        if (display == BakeOptions.Group && !overrideLightSettings)
        {
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
            timeStamp = System.DateTime.Now;
            Lightmapping.BakeMultipleScenes(scenePath.ToArray());
            UpdateLightmappingProcess();

        }
        else if(display == BakeOptions.Group && overrideLightSettings && lightingPreset != null)
        {
        	EditorSceneManager.OpenScene(scenePath[0], OpenSceneMode.Single);
			EditorSceneManager.SetActiveScene(EditorSceneManager.GetSceneByName(EditorSceneManager.GetActiveScene().name));
        	lightingPreset.Load();
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            timeStamp = System.DateTime.Now;
            Lightmapping.BakeMultipleScenes(scenePath.ToArray());
            UpdateLightmappingProcess();
        }
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Update lightmapping process
    private void UpdateLightmappingProcess()
    {
        if (Lightmapping.isRunning)
        {
        	if(display == BakeOptions.Individual)
        	{
           		status = "Lightmapping " + (sceneIndex + 1).ToString() + " of " + scenes.Length.ToString();
            	bakeButton = "Cancel";
        	}
        	else if(display == BakeOptions.Group)
        	{
        		status = "Lightmapping scenes as a group";
        		bakeButton = "Cancel";
        	}
        }
        // This section only runs when baking stalls, not when baking is completed (API bug)
        // Baking has been stopped when this code path is executed
        else if (!Lightmapping.isRunning)
        {
        	if(display == BakeOptions.Individual)
        	{
        		DoneLightmapping("Generate Lighting");	
        	}
        	else if(display == BakeOptions.Group)
        	{
				bakeButton = "Generate Lighting";
        	}
            
        }

        // Handle printing results for group baking
        if(display == BakeOptions.Group && isGroupBakingDone)
        {
        	if(printTiming)
            {
				SaveOutputGrid(BakeLog);
            	BakeLightmaps.Trace(" ");
            }

            DoneLightmapping("done");
        	isGroupBakingDone = false; // revert bool status to idle state
        }
    }

    //-------------------------------------------------------------------------------------------------------------------------
    private void SaveScene()
    {
	    // If baking a scene individually, save it at the end of each lightmapping process before kickstarting a new one
    	if(display == BakeOptions.Individual)
    	{
    		System.TimeSpan lightmappingSpan = System.DateTime.Now - timeStamp;
	        string bakeTime = string.Format("{0:D2}:{1:D2}:{2:D2}", lightmappingSpan.Hours, lightmappingSpan.Minutes, lightmappingSpan.Seconds);

	        bakeFinal = "(" + EditorSceneManager.GetActiveScene().name + ")" + "," + "[" + bakeTime + "]";

	        if(logPrintTimes)
	        {
	        	UnityEngine.Debug.Log("[" + sceneIndex.ToString() + "/" + scenes.Length.ToString() + "] " + "Done baking: " +
	            EditorSceneManager.GetActiveScene().name + ".unity" + " after " + bakeTime);
	        }
	        
	        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

	        if (printTiming)
	            BakeLightmaps.Trace(bakeFinal);

    	}
	    // If baking a scenes as a group, save them altogether
    	else if(display == BakeOptions.Group)
    	{
    		System.TimeSpan lightmappingSpan = System.DateTime.Now - timeStamp;
	        string bakeTime = string.Format("{0:D2}:{1:D2}:{2:D2}", lightmappingSpan.Hours, lightmappingSpan.Minutes, lightmappingSpan.Seconds);

	        bakeFinal = "Group bake timing:" + "," +  "[" + bakeTime + "]";

			EditorSceneManager.MarkAllScenesDirty(); // Mark the scenes dirty so all the results are saved in the next step
	        EditorSceneManager.SaveOpenScenes();

	        isGroupBakingDone = true; // Group baking is done

			if(logPrintTimes)
				UnityEngine.Debug.Log(bakeFinal);
        	
	        if(printTiming)
	        	BakeLightmaps.Trace(bakeFinal);
	        
    	}        
    }

   	//-------------------------------------------------------------------------------------------------------------------------
    // When lightmapping is done, update EditorGUI
    private void DoneLightmapping(string state)
    {
        Lightmapping.completed = null;
        sceneList.Clear();
        sceneIndex = 0;

        if (state == "done")
        {
            status = "Lightmapping is done";
            bakeButton = "Generate Lighting";
        }
        else if (state == "cancel")
        {
            status = "Canceled";
            bakeButton = "Generate Lighting";
        }
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Write Time Stamp and Scene Name to log file
    public void Write(string message)
    {
   		#if !FINAL
        if (AddTimeStamp)
        {
            System.DateTime now = System.DateTime.Now;
        }

        if (OutputStream != null)
        {
            OutputStream.WriteLine(message);
            OutputStream.Flush();
        }
        #endif
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Write all data to CSV file
    static public void SaveOutputGrid(StringBuilder items)
    {
        System.IO.File.WriteAllText("CSVData.csv", items.ToString());
    }

    //-------------------------------------------------------------------------------------------------------------------------
    // Write all baking results to a single instance 
    [Conditional("DEBUG"), Conditional("PROFILE")]
    public static void Trace(string Message)
    {
        #if !FINAL
        if (BakeLightmaps.Instance != null)
        {
            AppendText(Message);
            BakeLightmaps.Instance.Write(Message);
        }
        else
        {
            // Fallback if the debugging system hasn't been initialized yet.
            UnityEngine.Debug.LogError(Message);
        }
        #endif
    }

    //-------------------------------------------------------------------------------------------------------------------------
    public static void AppendText(string messageItem)
    {
        BakeLog.AppendLine(messageItem);
    }

}