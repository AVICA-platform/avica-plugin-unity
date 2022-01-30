using AVICA.Capture.Types;
using AVICA.Capture.Utils;
using FFmpegOut;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using UnityEngine;

namespace AVICA.Capture
{
    public class AVICACamera : MonoBehaviour
    {
        public enum CaptureCameraType
        {
            Follow,
            Fixed,
            Panoramic,
            ThreeSixty
        }

        public enum CaptureVideoFormat
        {
            MPEG4
        }

        public List<CaptureEventType> watchedEvents;
        public CaptureCameraType cameraType = CaptureCameraType.Fixed;

        [Tooltip("When enabled, the camera will be enabled/disabled when a recording session starts/ends. You probably want this off for your Main Camera")]
        public bool autoEnableCamera = true;

        [Tooltip("Should this camera also render to the screen?")]
        public bool renderToScreen = false;

        [Tooltip("The size to render this camera at, this will be ignored if rendering to the screen")]
        public Vector2 renderSize = new Vector2(1280, 720);

        Camera cam;
        System.DateTime recordStartTime;

        string ownerUserId;

        [SerializeField]
        public string cameraUDID;

        float CurrentVideoTime
        {
            get
            {
                return capture.FrameTime;
            }
        }

        Dictionary<string, int> userLastSeenCache = new Dictionary<string, int>();

        internal Dictionary<string, CaptureEventCameraData> ActiveEvents { get; private set; } = new Dictionary<string, CaptureEventCameraData>();

        AVICA.FFmpegOut.AvicaCapture capture;

        private void Awake()
        {
            cam = GetComponent<Camera>();

            if (autoEnableCamera)
                cam.enabled = false;

            capture = gameObject.AddComponent<AVICA.FFmpegOut.AvicaCapture>();
            capture.enabled = false;

            if (renderToScreen)
            {
                capture.width = cam.pixelWidth;
                capture.height = cam.pixelHeight;
            } else
            {
                capture.width = (int)renderSize.x;
                capture.height = (int)renderSize.y;
            }

            capture.rendersToScreen = renderToScreen;

            Debug.Log("[AVICA] Graphics Vendor: " + SystemInfo.graphicsDeviceVendor);

            if (SystemInfo.graphicsDeviceVendor.ToLower().Contains("nvidia") && AVICAManager.Instance.allowNvidiaEncoding)
            {
                Debug.Log("[AVICA] We're using NVIDIA hardware, so can use the HevcNvidia preset!");
                capture.preset = FFmpegPreset.HevcNvidia;
            }
            else
            {
                Debug.Log("[AVICA] No NVIDIA hardware detected or NVENC disabled in plugin, using the H264Default preset.");
                capture.preset = FFmpegPreset.H264Default;
            }

            capture.Setup();
        }

        public void SetOwnerUserId(string id)
        {
            ownerUserId = id;
            Debug.Log("[AVICA] User " + id + " is now the owner of Camera " + gameObject.name + " (" + cameraUDID + ")");
        }

        private void OnEnable()
        {
            AVICAManager.Instance.RegisterCamera(this);
        }

        private void OnDisable()
        {
            try
            {
                AVICAManager.Instance.UnregisterCamera(this);
            }
            catch(System.NullReferenceException) { } // Strictly speaking... doesn't matter if it's destroyed anyway
        }

        string GetOutputPath(CaptureSessionData sessionData, string ext)
        {
            return System.IO.Path.Combine(AVICAManager.GetSessionPath(sessionData.SessionId), "Cam_" + cameraUDID + ext);
        }

        internal void OnSessionStart(CaptureSessionData sessionData)
        {
            if(autoEnableCamera)
                cam.enabled = true;

            capture.outputPath = GetOutputPath(sessionData, ".mp4");
            capture.preset = AVICAManager.Instance.allowNvidiaEncoding ? capture.preset : FFmpegPreset.H264Default; // Force H264 if we don't allow NVIDIA encoding
            capture.enabled = true;
            recordStartTime = GetCurrentTimestamp();
        }

        internal void OnSessionStop(CaptureSessionData sessionData)
        {
            if(autoEnableCamera)
                cam.enabled = false;

            CommitCameraHeader(sessionData);

            capture.enabled = false;
        }

        System.DateTime GetCurrentTimestamp()
        {
            return System.DateTime.UtcNow;
        }

        void CommitCameraHeader(CaptureSessionData sessionData)
        {
            string path = GetOutputPath(sessionData, ".json");

            CaptureCameraHeader header = new CaptureCameraHeader()
            {
                CameraID = cameraUDID,
                CameraType = cameraType,
                PartnerID = sessionData.PartnerId,
                PlatformID = sessionData.PlatformId,
                SessionID = sessionData.SessionId,
                TotalFrames = capture.FrameCount,
                VideoFormat = CaptureVideoFormat.MPEG4,
                VideoFilename = System.IO.Path.GetFileName(GetOutputPath(sessionData, ".mp4")),
                StartTimestamp = recordStartTime,
                EndTimestamp = GetCurrentTimestamp(),
            };

            string json = JsonConvert.SerializeObject(header, Formatting.Indented);

            System.IO.File.WriteAllText(path, json);
        }

        internal void OnStartEvent(string eventId)
        {
            ActiveEvents[eventId] = new CaptureEventCameraData()
            {
                VideoStartTime = CurrentVideoTime,
                VideoStartFrame = capture.FrameCount,
                VirtualCameraID = cameraUDID,
                VideoEndTime = -1f,
            };
        }

        internal CaptureEventCameraData OnStopEvent(string eventId)
        {
            if(ActiveEvents.ContainsKey(eventId))
            {
                ActiveEvents[eventId].VideoEndTime = CurrentVideoTime;
                ActiveEvents[eventId].VideoEndFrame = capture.FrameCount;
                ActiveEvents[eventId].UserIds = GetUserIdsVisibleSince(ActiveEvents[eventId].VideoStartFrame);
                return ActiveEvents[eventId];
            } else
            {
                return null;
            }
        }

        public string[] GetUserIdsVisibleSince(int frame)
        {
            return userLastSeenCache.Where(u => u.Value >= frame).Select(u => u.Key).ToArray();
        }

        internal CaptureEventCameraData OnCaptureEvent(float rollback)
        {
            int startFrame = Mathf.Max(0, capture.FrameCount - Mathf.RoundToInt(rollback * capture.frameRate));
            return new CaptureEventCameraData()
            {
                VideoEndTime = CurrentVideoTime,
                VideoStartTime = CurrentVideoTime - rollback,
                VideoStartFrame = startFrame,
                VideoEndFrame = capture.FrameCount,
                VirtualCameraID = cameraUDID,
                UserIds = GetUserIdsVisibleSince(startFrame)
            };
        }

        void Update()
        {
            foreach(AVICAUser user in AVICAManager.Instance.RegisteredUsers)
            {
                if (user.UserId == null)
                    continue;

                if(user.UserId == ownerUserId || user.IsVisibleOnCamera(cam)) // We always "see" the owner
                {
                    userLastSeenCache[user.UserId] = capture.FrameCount;
                }
            }
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(cameraUDID))
            {
                cameraUDID = System.Guid.NewGuid().ToString();

#if UNITY_EDITOR
                EditorUtility.SetDirty(gameObject);

                var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

                if (prefabStage != null)
                    EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                else 
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("GameObject/AVICA/Capture Camera", false, 10)]
        static void CreateAVICACameraObject(MenuCommand menuCommand)
        {
            // Create a custom game object
            GameObject go = new GameObject("AVICACamera");
            go.AddComponent<AVICACamera>();
            go.AddComponent<Camera>();
            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }
#endif

        // FFmpeg stuff

        FFmpegSession _session;
        RenderTexture _tempRT;
        GameObject _blitter;


    }
}