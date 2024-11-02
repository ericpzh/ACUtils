using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utils
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {   
        private void Awake()
        {
            Log = Logger;

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo($"Scene loaded: {scene.name}");
        }

        internal static ManualLogSource Log;
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(Aircraft.AircraftController), "Init")]
        [HarmonyPostfix]
        public static void AircraftModifier(Aircraft.AircraftController.State state, ref Aircraft.AircraftController __instance)
        {
            // __instance.callSign = "CA3115";
        }

        [HarmonyPatch(typeof(Schedule.FlightScheduler), "GetRandomRunway")]
        [HarmonyPostfix]
        public static void FlightSchedulerModifier(ref Schedule.FlightScheduler __instance, ref ScriptableObjects.Runways.Runway __result)
        {
            if (runwayIndex != -1)
            {
                ScriptableObjects.Runways.Runway[] data = Osm.AirportManager.Instance.metaData.runways.data;
                __result = data[runwayIndex];
            }

            if (landingDurationMin != -1 && landingDurationMax != -1)
            {
                __instance.landingDurationMin = landingDurationMin;
                __instance.landingDurationMax = landingDurationMax;
            }

            if (departureDurationMin != -1 && departureDurationMax != -1)
            {
                __instance.departureDurationMin = departureDurationMin;
                __instance.departureDurationMax = departureDurationMax;
            }
        }

        [HarmonyPatch(typeof(Aircraft.AircraftController), "RegisterAudio")]
        [HarmonyPrefix]
        public static bool DisableAudio(ref bool ___audioEnd, string message, string voiceName, Action OnAudioComplete)
        {
            if (Time.timeScale != 1)
            {
                ___audioEnd = true;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(CameraSystem.BuilderCameraSystem), "Update")]
        [HarmonyPrefix]
        public static bool RuntimeUpdatePatches()
        {
            TimeScaler();
            ArrivalRunwaySelector();
            SpwanIntervalSelector();
            return true;
        }

        static private void TimeScaler() {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (Time.timeScale == 0f)
                {
                    Time.timeScale = prevTimeScale;
                }
                else
                {
                    prevTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                Plugin.Log.LogInfo("Game speed changed to: " + Time.timeScale);
            }
            else
            {
                for (int i = 0; i < timeSclaerKeys.Length; i++)
                {
                    if (Input.GetKeyDown(timeSclaerKeys[i]))
                    {
                        Time.timeScale = 1 << i;
                        Plugin.Log.LogInfo("Game speed changed to: " + Time.timeScale);
                        break;
                    }
                }
            }
        }

        static private void ArrivalRunwaySelector()
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                if (Input.GetKeyDown(KeyCode.L))
                {
                    runwayIndex = 1;
                    Plugin.Log.LogInfo("All arrival to 36L");
                }
                else if (Input.GetKeyDown(KeyCode.R))
                {
                    runwayIndex = 0;
                    Plugin.Log.LogInfo("All arrival to 36R");
                }
                else if (Input.GetKeyDown(KeyCode.C))
                {
                    runwayIndex = -1;
                    Plugin.Log.LogInfo("All arrival to 36L/R");
                }
            }
        }

        static private void SpwanIntervalSelector()
        {
            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Alpha1))
                {
                    departureDurationMin = -1;
                    departureDurationMax = -1;
                    Plugin.Log.LogInfo("Reset departure spwan.");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    departureDurationMin = 75 / 2;
                    departureDurationMax = 150 / 2;
                    Plugin.Log.LogInfo("Departure spwan 2x.");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    departureDurationMin = 75 / 3;
                    departureDurationMax = 150 / 3;
                    Plugin.Log.LogInfo("Departure spwan 3x.");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha4))
                {
                    departureDurationMin = 75 / 4;
                    departureDurationMax = 150 / 4;
                    Plugin.Log.LogInfo("Departure spwan 4x.");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha5))
                {
                    departureDurationMin = 75 / 5;
                    departureDurationMax = 150 / 5;
                    Plugin.Log.LogInfo("Departure spwan 5x.");
                }
            }

            if (Input.GetKey(KeyCode.LeftAlt))
            {
                if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Alpha1))
                {
                    landingDurationMin = -1;
                    landingDurationMax = -1;
                    Plugin.Log.LogInfo("Reset arrival spwan.");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    landingDurationMin = 75 / 2;
                    landingDurationMax = 200 / 2;
                    Plugin.Log.LogInfo("Arrival spwan 2x.");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    landingDurationMin = 75 / 3;
                    landingDurationMax = 200 / 3;
                    Plugin.Log.LogInfo("Arrival spwan 3x.");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha4))
                {
                    landingDurationMin = 75 / 4;
                    landingDurationMax = 200 / 4;
                    Plugin.Log.LogInfo("Arrival spwan 4x.");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha5))
                {
                    landingDurationMin = 75 / 5;
                    landingDurationMax = 200 / 5;
                    Plugin.Log.LogInfo("Arrival spwan 5x.");
                }
            }
        }

        static KeyCode[] timeSclaerKeys = {KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B};
        static float prevTimeScale = 1f;
        static int runwayIndex = -1;
        static int departureDurationMin = -1;
        static int departureDurationMax = -1;
        static int landingDurationMin = -1;
        static int landingDurationMax = -1;
    }

    public static class ReflectionExtensions
    {
        public static T GetFieldValue<T>(this object obj, string name)
        {
            // Set the flags so that private and public fields from instances will be found
            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var field = obj.GetType().GetField(name, bindingFlags);
            return (T)field?.GetValue(obj);
        }

        public static void SetFieldValue<T>(this object obj, string name, T value)
        {
            // Set the flags so that private and public fields from instances will be found
            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var field = obj.GetType().GetField(name, bindingFlags);
            field.SetValue(obj, value);
        }
    }
}
