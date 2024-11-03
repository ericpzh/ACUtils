using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
            const float maxScrollSpeed = 0.3f;
            if (!__instance.LockCamera && (__instance.mode != CameraSystem.BuilderCameraSystem.Mode.Follow || !((UnityEngine.Object)(object)__instance.currentFollowTarget != (UnityEngine.Object)null)))
            {
                float xdir = 0;
                float ydir = 0;
                // Normalized mouse position.
                Vector2 pos = Camera.main.ScreenToViewportPoint(Input.mousePosition);
                float mouseX = Math.Max(Math.Min(1, pos.x), 0);
                float mouseY = Math.Max(Math.Min(1, pos.y), 0);
                // Linear scrolling speed.
                if (mouseX < thres)
                {
                    float speed = (thres - mouseX) / thres * maxScrollSpeed;
                    xdir = speed;
                }
                else if (mouseX > 1 - thres)
                {
                    float speed = (mouseX - (1 - thres)) / thres * maxScrollSpeed;
                    xdir = -speed;
                }
                if (mouseY < thres)
                {
                    float speed = (thres - mouseY) / thres * maxScrollSpeed;
                    ydir = speed;
                }
                else if (mouseY > 1 - thres)
                {
                    float speed = (mouseY - (1 - thres)) / thres * maxScrollSpeed;
                    ydir = -speed;
                }
                if (xdir == 0 && ydir == 0)
                {
                    return true;
                }
                // Mostly ProcessDragMove() except xdir & ydir.
                float cameraDistance = ___topdownFramingTransposer.m_CameraDistance;
                float num1 = Mathf.InverseLerp(__instance.minOffsetLength, __instance.maxOffsetLength, cameraDistance);
                float CameraMoveSpeed = Mathf.Lerp(___cameraMoveSpeed.x, ___cameraMoveSpeed.y, num1);
                float num = xdir * CameraMoveSpeed * Time.unscaledDeltaTime;
                float num2 = ydir * CameraMoveSpeed * Time.unscaledDeltaTime;
                Vector3 right = ((UnityEngine.Component)__instance.topdownCam).transform.right;
                ((Vector3)(right)).Normalize();
                Vector3 up = ((UnityEngine.Component)__instance.topdownCam).transform.up;
                ((Vector3)(up)).Normalize();
                Vector3 val = (0f - num) * right + (0f - num2) * up;
                val.y = 0f;
                if (((Vector3)(val)).magnitude > 5f)
                {
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

        [HarmonyPatch(typeof(StripView), "SetText")]
        [HarmonyPostfix]
        public static void StripText(ref StripView __instance, ref TMP_Text ___text)
        {
            bool isDeparture = __instance.aircraftController.isDeparture;
            StripLabel label = __instance.aircraftController.GetComponent<StripLabel>();
            if (label == null)
            {
                label = __instance.aircraftController.gameObject.AddComponent<StripLabel>();
                label.destination = RandomDestination(isDeparture);
            }
            string destination = label.destination;
            if (((Behaviour)__instance.aircraftController.landing).isActiveAndEnabled)
            {
                destination = __instance.aircraftController.landing.Runway.name;
            }
            string type = __instance.aircraftController.size == Aircraft.AircraftSize.H ? "B744" : "B738";
            string optHeavy = __instance.aircraftController.size == Aircraft.AircraftSize.H ? "H/" : "";
            ___text.text = " " + __instance.aircraftController.callSign + "\n";
            ___text.text += " " + destination  + " | " + optHeavy + type + "\n";
            ___text.fontSize *= 0.9f;
            // ___text.rectTransform.sizeDelta = new Vector2(3, 1);
            // string text = __instance.aircraftController.state.ToString();
            // if (text.StartsWith("Departure"))
            // {
            //     text = text.Replace("Departure", "");
            // }
            // else if (text.StartsWith("Arrival"))
            // {
            //     text = text.Replace("Arrival", "");
            // }
            // ___text.text += " " + TranslateState(text);
        }

        [HarmonyPatch(typeof(StripView), "OnProgressButtonClicked")]
        [HarmonyPostfix]
        public static void StripOnClick(ref StripView __instance, ref UnityEngine.UI.Image ___buttonImage)
        {
            bool isDeparture = __instance.aircraftController.isDeparture;
            Color color = isDeparture ? new Color(0.7f, 1f, 0.7f, 0.7f) : new Color(1f, 0.9f, 0.7f, 0.7f);
            if (focusedStrip != null)
            {
                ((Graphic)focusedButtonImage).color = focusedColor;
            }
            focusedStrip = __instance;
            focusedButtonImage = ___buttonImage;
            focusedColor = ((Graphic)___buttonImage).color;
            ((Graphic)___buttonImage).color = color;
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

        static private string TranslateState(string state)
        {
            Dictionary<string, string> stateMap = new Dictionary<string, string>
            {
                ["WaitPushbackClearance"] = "已放行",
                ["PushbackInProgress"] = "推出中",
                ["WaitTaxiClearance"] = "已推出",
                ["TaxiInProgress"] = "滑行中",
                ["HoldShort"] = "等待中",
                ["TaxiToLineUpAndWait"] = "跑道内等待中",
                ["LinedUpAndWaiting"] = "跑道内等待中",
                ["TaxiToTakeoff"] = "起飞中",
                ["TakeoffInProgress"] = "起飞中",
                ["HandoffComplete"] = "已离场",
                ["Holding"] = "等待中",
                ["Approaching"] = "着陆中",
                ["ClearedToLand"] = "着陆中",
                ["Landing"] = "着陆中",
                ["ExitedRunway"] = "已着陆",
                ["TaxiToApron"] = "滑行中",
                ["AtApron"] = "已到达",
            };
            if(!stateMap.ContainsKey(state))
            {
                return state;
            }
            return stateMap[state];
        }

        static private string RandomDestination(bool isDeparture)
        {
            if (isDeparture)
            {
                string[] cities = {
                    // "长春", "长沙", "重庆", "大连", "福州", "广州", "哈尔滨", "海口", "武汉", "乌鲁木齐", 
                    // "厦门", "西安"
                    "RJAA", "ZSSS", "ZGGG", "ZSAM", "ZUCK", "ZUTF", "RKSI", "ZHHH", "ZJSY", "ZYHB", 
                    "ZWWW", "ZLXY", "ZSNJ", "ZPPP", "ZYTL", "ZGNN", "ZGHA", "RJBB", "VVNB", "ZJHK",
                };
                return cities[UnityEngine.Random.Range(0, cities.Length)];
            }
            return UnityEngine.Random.Range(100, 300).ToString();
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
        static StripView focusedStrip = null;
        static UnityEngine.UI.Image focusedButtonImage = null;
        static Color focusedColor;
    }

    public class StripLabel : MonoBehaviour
    {
        public string destination;
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
