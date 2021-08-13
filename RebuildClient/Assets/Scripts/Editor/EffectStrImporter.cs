using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;

namespace Assets.Scripts.Editor
{

    public static class EffectStrImporter
    {
        [MenuItem("Ragnarok/Load Effects")]
        public static void Import()
        {
            var loader = new RagnarokEffectLoader();
            loader.Load(@"G:\Projects2\Ragnarok\Resources\data\texture\effect\pneuma1.str");
            loader.MakeAtlas(@"Assets/Effects/Atlas/");
        }
    }
}
