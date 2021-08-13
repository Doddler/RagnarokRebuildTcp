using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor
{
	public static class SetSceneLightingSettings
	{
		[MenuItem("Ragnarok/Set Lighting Settings")]
		public static void SetLightingSettings()
		{
			LightmapEditorSettings.mixedBakeMode = MixedLightingMode.IndirectOnly;
			LightmapEditorSettings.directSampleCount = 32;
			LightmapEditorSettings.indirectSampleCount = 256;
			LightmapEditorSettings.environmentSampleCount = 256;
			LightmapEditorSettings.bounces = 1;
			LightmapEditorSettings.bakeResolution = 4;
			LightmapEditorSettings.padding = 2;
			LightmapEditorSettings.maxAtlasSize = 1024;
			LightmapEditorSettings.enableAmbientOcclusion = true;
			LightmapEditorSettings.aoMaxDistance = 12;
			LightmapEditorSettings.lightmapsMode = LightmapsMode.NonDirectional;
			LightmapEditorSettings.lightmapper = LightmapEditorSettings.Lightmapper.ProgressiveGPU;
		}
	}
}
