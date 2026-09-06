#nullable enable
using System.Threading.Tasks;
using BAModAPI;
using UnityEngine;

[assembly: RegisterModClass(typeof(CherryQuickRid.QuickRidCityMod))]

namespace CherryQuickRid
{
    /// <summary>
    /// Einstiegspunkt beim Laden der Stadt: legt das Controller-GameObject an.
    /// </summary>
    [ModEntryOnCityLoad]
    public sealed class QuickRidCityMod : IModBigAmbitions
    {
        private static GameObject? _controllerObject;

        public string[] RelativeAssetBundlePaths => new[] { QuickRidAssets.BundleKey };

        public Task OnLoadAsync(ModContext context)
        {
            if (_controllerObject == null)
            {
                // Das Kartensymbol kommt aus dem AssetBundle; fehlt es, nimmt der Filter das
                // Symbol des Vanilla-Lieferjobs.
                Sprite? mapIcon = QuickRidAssets.LoadMapIcon(context);

                _controllerObject = new GameObject("QuickRid - Controller");
                UnityEngine.Object.DontDestroyOnLoad(_controllerObject);
                var controller = _controllerObject.AddComponent<QuickRidController>();
                controller.Initialize(context, mapIcon);
            }

            context.Logger.Info("QuickRid city mod loaded.");
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            if (_controllerObject != null)
            {
                UnityEngine.Object.Destroy(_controllerObject);
                _controllerObject = null;
            }
            return Task.CompletedTask;
        }
    }
}
