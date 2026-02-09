using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TaintedGrailMinimap
{
    [BepInPlugin("com.minimap.taintedgrail", "Tainted Grail Minimap", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            MinimapConfig.Init(Config);
            var minimapObj = new GameObject("TaintedGrailMinimap");
            minimapObj.AddComponent<MinimapBehaviour>();
            DontDestroyOnLoad(minimapObj);
            Logger.LogInfo("TaintedGrailMinimap loaded!");
        }
    }
}
