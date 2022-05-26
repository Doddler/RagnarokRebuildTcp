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
            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>("Assets/Textures/Ro Lighting.lighting");
            Lightmapping.lightingSettings = lighting;

			Lightmapping.lightingSettings.mixedBakeMode = MixedLightingMode.IndirectOnly;
			Lightmapping.lightingSettings.directSampleCount = 32;
			Lightmapping.lightingSettings.indirectSampleCount = 256;
			Lightmapping.lightingSettings.environmentSampleCount = 256;
			Lightmapping.lightingSettings.maxBounces = 2;
            Lightmapping.lightingSettings.lightmapResolution = 4;
            Lightmapping.lightingSettings.lightmapPadding = 2;
            Lightmapping.lightingSettings.lightmapMaxSize = 1024;
            Lightmapping.lightingSettings.ao = true;
            Lightmapping.lightingSettings.aoMaxDistance = 12;
            Lightmapping.lightingSettings.directionalityMode = LightmapsMode.NonDirectional;
            Lightmapping.lightingSettings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
		}
	}
}
