using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Utility
{
    //Numbers format and parse the same on every PC, whatever its language settings.
    public static class GameCulture
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void UseInvariantCulture()
        {
            var culture = CultureInfo.InvariantCulture;
            //threads started later, such as the network's
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}
