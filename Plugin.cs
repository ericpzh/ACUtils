using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.UIElements;

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
            // __instance.callSign = UnityEngine.Random.Range(0, 10) > 5 ? "CA3115" : "ES1111";
        }

        [HarmonyPatch(typeof(CameraSystem.BuilderCameraSystem), "ProcessDragMove")]
        [HarmonyPrefix]
        public static bool EdgeScroller(ref CameraSystem.BuilderCameraSystem __instance, ref Vector2 ___cameraMoveSpeed, ref Cinemachine.CinemachineFramingTransposer ___topdownFramingTransposer)
        {
            const float thres = 0.05f;
            const float maxSpeed = 0.3f;
            if (!__instance.LockCamera && (__instance.mode != CameraSystem.BuilderCameraSystem.Mode.Follow || !((UnityEngine.Object)(object)__instance.currentFollowTarget != (UnityEngine.Object)null)))
            {
                float xdir = 0;
                float ydir = 0;
                Vector2 pos = Camera.main.ScreenToViewportPoint(Input.mousePosition);
                float x = Math.Max(Math.Min(1, pos.x), 0);
                float y = Math.Max(Math.Min(1, pos.y), 0);
                if (x < thres)
                {
                    float speed = (thres - x) / thres * maxSpeed;
                    xdir = speed;
                }
                else if (x > 1 - thres)
                {
                    float speed = (x - (1 - thres)) / thres * maxSpeed;
                    xdir = -speed;
                }
                if (y < thres)
                {
                    float speed = (thres - y) / thres * maxSpeed;
                    ydir = speed;
                }
                else if (y > 1 - thres)
                {
                    float speed = (y - (1 - thres)) / thres * maxSpeed;
                    ydir = -speed;
                }
                if (xdir == 0 && ydir == 0)
                {
                    return true;
                }
                float cameraDistance = ___topdownFramingTransposer.m_CameraDistance;
                float num1 = Mathf.InverseLerp(__instance.minOffsetLength, __instance.maxOffsetLength, cameraDistance);
                float CameraMoveSpeed = Mathf.Lerp(___cameraMoveSpeed.x, ___cameraMoveSpeed.y, num1);
                float num = xdir * CameraMoveSpeed * Time.unscaledDeltaTime;
                float num2 = ydir * CameraMoveSpeed * Time.unscaledDeltaTime;
                Vector3 right = ((Component)__instance.topdownCam).transform.right;
                ((Vector3)(right)).Normalize();
                Vector3 up = ((Component)__instance.topdownCam).transform.up;
                ((Vector3)(up)).Normalize();
                Vector3 val = (0f - num) * right + (0f - num2) * up;
                val.y = 0f;
                if (((Vector3)(val)).magnitude > 5f)
                {
                    // __instance.UnFollow();
                    __instance.SwitchToTopDownCam();
                }
                if ((UnityEngine.Object)(object)((Cinemachine.CinemachineVirtualCameraBase)__instance.topdownCam).Follow != (UnityEngine.Object)null)
                {
                    Transform follow = ((Cinemachine.CinemachineVirtualCameraBase)__instance.topdownCam).Follow;
                    follow.position += val;
                }
                return false;
            }
            return true;
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
            if (audioDisabled)
            {
                ___audioEnd = true;
                Plugin.Log.LogInfo("Audio Skipped.");
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
            AudioDisabler();
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
                for (int i = 0; i < timeScalerKeys.Length; i++)
                {
                    if (Input.GetKeyDown(timeScalerKeys[i]))
                    {
                        Time.timeScale = 1 << i;
                        audioDisabled = i > 0;
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
                for (int i = 0; i < spwanKeys.Length; i++)
                {
                    if (Input.GetKeyDown(spwanKeys[i]))
                    {
                        if (i == 0)
                        {
                            departureDurationMin = -1;
                            departureDurationMax = -1;
                            Plugin.Log.LogInfo("Reset departure spwan.");
                        }
                        else
                        {
                            int scale = i + 1;
                            departureDurationMin = 75 / scale;
                            departureDurationMax = 200 / scale;
                            Plugin.Log.LogInfo("Departure spwan " + scale + "x.");
                        }

                    }
                }
            }
            else if (Input.GetKey(KeyCode.LeftAlt))
            {
                for (int i = 1; i < spwanKeys.Length; i++)
                {
                    if (Input.GetKeyDown(spwanKeys[i]))
                    {
                        if (i == 0)
                        {
                            landingDurationMin = -1;
                            landingDurationMax = -1;
                            Plugin.Log.LogInfo("Reset arrival spwan.");
                        }
                        else
                        {
                            int scale = i + 1;
                            landingDurationMin = 75 / scale;
                            landingDurationMax = 200 / scale;
                            Plugin.Log.LogInfo("Arrival spwan " + scale + "x.");
                        }

                    }
                }
            }
        }

        static private void AudioDisabler() {
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (Time.timeScale != 1)
                {
                    return;
                }
                audioDisabled = !audioDisabled;
                Plugin.Log.LogInfo("Audio Disable? " + audioDisabled);
            }
        }

        static KeyCode[] timeScalerKeys = {KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B};
        static KeyCode[] spwanKeys = {KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0};
        static float prevTimeScale = 1f;
        static int runwayIndex = -1;
        static int departureDurationMin = -1;
        static int departureDurationMax = -1;
        static int landingDurationMin = -1;
        static int landingDurationMax = -1;
        static bool audioDisabled = false;
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
