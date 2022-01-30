using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AVICA.Capture.Types
{
    public class CaptureSessionData
    {
        public string PartnerId;
        public string PlatformId;
        public System.DateTime StartTimestamp;
        public System.DateTime EndTimestamp;
        public float Length;
        public int GameFrames;
        public string UserId;
        public string SessionId;

        public List<CaptureEventData> Events = new List<CaptureEventData>();
    }

    public class CaptureEventData
    {
        public string Id;
        public int Index;
        public CaptureEventType EventType;
        [JsonConverter(typeof(StringEnumConverter))]
        public CaptureEventType EventTypeString { get { return EventType; } }

        public float SessionStartTime;
        public float SessionEndTime;
        public System.DateTime StartTimestamp;
        public System.DateTime EndTimestamp;

        public List<CaptureEventCameraData> FootageData = new List<CaptureEventCameraData>();
    }

    public class CaptureEventCameraData
    {
        public string VirtualCameraID;
        public float VideoStartTime;
        public float VideoEndTime;
        public int VideoStartFrame;
        public int VideoEndFrame;

        public string[] UserIds;
    }

    public class CaptureCameraHeader
    {
        public string PartnerID;
        public string PlatformID;
        public string VideoFilename;
        public string CameraID;
        public string SessionID;
        public System.DateTime StartTimestamp;
        public System.DateTime EndTimestamp;
        public int TotalFrames;

        [JsonConverter(typeof(StringEnumConverter))]
        public AVICACamera.CaptureVideoFormat VideoFormat = AVICACamera.CaptureVideoFormat.MPEG4;

        [JsonConverter(typeof(StringEnumConverter))]
        public AVICACamera.CaptureCameraType CameraType = AVICACamera.CaptureCameraType.Fixed;
    }
}