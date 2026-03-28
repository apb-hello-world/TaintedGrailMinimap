using BepInEx;
using UnityEngine;

namespace TaintedGrailMinimap
{
    [BepInPlugin("com.minimap.taintedgrail", "MiniMap", "1.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            MinimapConfig.Init(Config);
            MinimapBehaviour.Log = Logger;
            var minimapObj = new GameObject("TaintedGrailMinimap");
            minimapObj.AddComponent<MinimapBehaviour>();
            DontDestroyOnLoad(minimapObj);
            Logger.LogInfo("TaintedGrailMinimap loaded!");
        }
    }
}
