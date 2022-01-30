using AVICA.Capture.Types;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AVICA.Capture
{
    public class AVICAManager : MonoBehaviour
    {
        internal List<AVICAUser> RegisteredUsers { get; private set; } = new List<AVICAUser>();
        internal List<AVICACamera> RegisteredCameras { get; private set; } = new List<AVICACamera>();

        Dictionary<string, CaptureEventData> activeEvents = new Dictionary<string, CaptureEventData>();

        [Header("Partner Info")]
        public string platformId;
        public string partnerId;
        public string userId;

        CaptureSessionData sessionData = new CaptureSessionData();

        float gameTimeSessionStart = -1f;

        bool sessionIsRunning = false;

        static AVICAManager _instance;

        CaptureAudioRecorder sessionAudioRecorder;

        [Tooltip("Use NVENC when available, however this will be disabled if more than 3 cameras are present")]
        public bool allowNvidiaEncoding = true;

        public static AVICAManager Instance
        {
            get
            {
                if(_instance == null)
                {
                    return _instance = GameObject.FindObjectOfType<AVICAManager>();
                } else
                {
                    return _instance;
                }
            }
        }

        CaptureAudioRecorder _audioRecorder;

        CaptureAudioRecorder AudioRecorder
        {
            get
            {
                if(_audioRecorder == null)
                {
                    AudioListener listener = FindObjectOfType<AudioListener>();

                    if(listener != null)
                        _audioRecorder = listener.gameObject.AddComponent<CaptureAudioRecorder>();
                }

                return _audioRecorder;
            }
        }

        private void OnEnable()
        {
            if(_instance != null && _instance.gameObject != null && _instance != this)
            {
                Debug.LogError("[AVICA] You cannot have two AVICAManagers in a scene!");
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        public static string GetSessionRoot()
        {
            return System.IO.Path.Combine(System.Environment.CurrentDirectory, "recordings");
        }

        public static string GetSessionPath(string sessionId)
        {
            return System.IO.Path.Combine(GetSessionRoot(), "Session_" + sessionId);
        }

        public static string StartSession()
        {
            if (Instance.sessionIsRunning)
                return Instance.sessionData.SessionId;

            if(Instance.RegisteredCameras.Count > 3)
            {
                Debug.Log("[AVICA] Disabling NVENC Encoding as it is not compatible with > 3 streams (less for some GPUs)");
                Instance.allowNvidiaEncoding = false;
            }

            Instance.sessionData = new CaptureSessionData()
            {
                SessionId = System.Guid.NewGuid().ToString(),
                PartnerId = Instance.partnerId,
                PlatformId = Instance.platformId,
                UserId = Instance.userId,
                StartTimestamp = System.DateTime.UtcNow,
            };

            System.IO.Directory.CreateDirectory(GetSessionPath(Instance.sessionData.SessionId));
            // System.IO.Directory.CreateDirectory(System.IO.Path.Combine(GetSessionPath(Instance.sessionData.SessionId), "Footage"));

            Instance.sessionIsRunning = true;

            foreach(AVICACamera cam in Instance.RegisteredCameras)
            {
                cam.OnSessionStart(Instance.sessionData);
            }

            CaptureAudioRecorder recorder = Instance.AudioRecorder;

            if (recorder != null)
                recorder.StartRecording(Instance.sessionData);
            else
                Debug.Log("[AVICA] No Audio Listener found, so no audio will be recorded");

            Debug.Log("[AVICA] Started Session: " + Instance.sessionData.SessionId);

            return Instance.sessionData.SessionId;
        }

        public static void StopSession(float delay)
        {
            Instance.StartCoroutine(Instance.StopSessionAsync(delay));
        }

        IEnumerator StopSessionAsync(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            Instance.sessionIsRunning = false;

            foreach (CaptureEventData ev in Instance.activeEvents.Values.ToArray())
            {
                Debug.Log("[AVICA] Halting Event " + ev.Id + " as the session is sending");
                StopEvent(ev.Id);
            }

            foreach (AVICACamera cam in RegisteredCameras)
            {
                cam.OnSessionStop(sessionData);
            }

            sessionData.EndTimestamp = System.DateTime.UtcNow;

            if(_audioRecorder != null)
                Instance.AudioRecorder.StopRecording();

            Debug.Log("[AVICA] Stopped Session: " + Instance.sessionData.SessionId);
            CommitSessionData();
            Debug.Log("[AVICA] Committed session data to file");
        }

        void CommitSessionData()
        {
            string json = JsonConvert.SerializeObject(Instance.sessionData, Formatting.Indented);
            System.IO.File.WriteAllText(System.IO.Path.Combine(GetSessionPath(sessionData.SessionId), "events.json"), json);
        }

        public void Update()
        {
            if (sessionIsRunning && sessionData != null)
            {
                sessionData.Length += Time.deltaTime;
                sessionData.GameFrames++;
            }
        }

        public static void SetLocalUserId(string id)
        {
            Instance.userId = id;
        }

        float GetCurrentSessionTime()
        {
            return Time.time - gameTimeSessionStart;
        }

        public static string StartEvent(CaptureEventType type, AVICACamera[] cameraOverride = null, string idOverride = null)
        {
            string eventId = idOverride != null ? idOverride : System.Guid.NewGuid().ToString();

            foreach(AVICACamera cam in (cameraOverride != null ? cameraOverride : Instance.RegisteredCameras.Where(c => c.watchedEvents.Contains(type))))
            {
                cam.OnStartEvent(eventId);
            }

            Instance.activeEvents[eventId] = new CaptureEventData()
            {
                Id = eventId,
                EventType = type,
                Index = Instance.sessionData != null ? Instance.sessionData.Events.Count : 0,
                SessionStartTime = Instance.GetCurrentSessionTime(),
                StartTimestamp = System.DateTime.UtcNow
            };

            Debug.Log("[AVICA] Event " + type.ToString() + " (" + eventId + ") started");

            return eventId;
        }

        public static void StopEvent(string eventId)
        {
            foreach (AVICACamera cam in Instance.RegisteredCameras.Where(x => x.ActiveEvents.ContainsKey(eventId)))
            {
                CaptureEventCameraData data = cam.OnStopEvent(eventId);

                if(data != null)
                {
                    Instance.activeEvents[eventId].FootageData.Add(data);
                }
            }

            Instance.activeEvents[eventId].SessionEndTime = Instance.GetCurrentSessionTime();
            Instance.activeEvents[eventId].EndTimestamp = System.DateTime.UtcNow;

            Instance.sessionData.Events.Add(Instance.activeEvents[eventId]);
            Instance.activeEvents.Remove(eventId);

            Debug.Log("[AVICA] Event " + eventId + " ended");
        }

        public static string CaptureEvent(CaptureEventType type, float secondsBefore, float secondsAfter, AVICACamera[] cameraOverride = null, string idOverride = null)
        {
            string eventId = idOverride != null ? idOverride : System.Guid.NewGuid().ToString();

            Instance.StartCoroutine(Instance.CaptureEventFire(type, secondsBefore, secondsAfter, new CaptureEventData()
            {
                Id = eventId,
                EventType = type,
                SessionStartTime = Instance.GetCurrentSessionTime() - secondsBefore,
                SessionEndTime = Instance.GetCurrentSessionTime() + secondsAfter,
                StartTimestamp = System.DateTime.UtcNow.AddSeconds(-secondsBefore),
                EndTimestamp = System.DateTime.UtcNow.AddSeconds(secondsAfter),
                Index = Instance.sessionData != null ? Instance.sessionData.Events.Count : 0,
            }, cameraOverride));

            return eventId;
        }

        IEnumerator CaptureEventFire(CaptureEventType type, float secondsBefore, float secondsAfter, CaptureEventData ev, AVICACamera[] cameraOverride)
        {
            yield return new WaitForSeconds(secondsAfter);

            float rollback = secondsBefore + secondsAfter;

            foreach(AVICACamera cam in (cameraOverride != null ? cameraOverride : RegisteredCameras.Where(c => c.watchedEvents.Contains(type))))
            {
                CaptureEventCameraData data = cam.OnCaptureEvent(rollback);

                if(data != null)
                {
                    ev.FootageData.Add(data);
                }
            }

            sessionData.Events.Add(ev);

            Debug.Log("[AVICA] Instant Capture Event " + type.ToString() + " captured with a " + secondsBefore + "s rollback");
        }

        internal void RegisterUser(AVICAUser user)
        {
            if (!RegisteredUsers.Contains(user))
                RegisteredUsers.Add(user);
        }

        internal void UnregisterUser(AVICAUser user)
        {
            if (RegisteredUsers.Contains(user))
                RegisteredUsers.Remove(user);
        }

        internal void RegisterCamera(AVICACamera camera)
        {
            if (!RegisteredCameras.Contains(camera))
                RegisteredCameras.Add(camera);
        }

        internal void UnregisterCamera(AVICACamera camera)
        {
            if (RegisteredCameras.Contains(camera))
                RegisteredCameras.Remove(camera);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("GameObject/AVICA/Capture Manager", false, 10)]
        static void CreateAVICAManagerObject(MenuCommand menuCommand)
        {
            // Create a custom game object
            GameObject go = new GameObject("AVICAManager");
            go.AddComponent<AVICAManager>();
            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }
#endif
    }
}