using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.MapEditor.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Assets.Scripts.Editor
{

	class BuildAddressablesProcessor
	{
		/// <summary>
		/// Run a clean build before export.
		/// </summary>
		static public void PreExport()
		{
			Debug.Log("BuildAddressablesProcessor.PreExport start");
			AddressableAssetSettings.CleanPlayerContent(
				AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);
			AddressableAssetSettings.BuildPlayerContent();
			Debug.Log("BuildAddressablesProcessor.PreExport done");
		}

		[InitializeOnLoadMethod]
		private static void Initialize()
		{
			BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerHandler);
		}

		private static void BuildPlayerHandler(BuildPlayerOptions options)
		{
			if (EditorUtility.DisplayDialog("Build with Addressables",
				"Do you want to build a clean addressables before export?",
				"Perform Clean Addressables Build", "Normal Build"))
			{
				RagnarokMapImporterWindow.UpdateAddressables();
				PreExport();
			}
			//else
			//	AddressableAssetSettings.BuildPlayerContent();
			BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
		}

	}
}
