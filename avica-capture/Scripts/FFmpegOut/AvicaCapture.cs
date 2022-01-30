using FFmpegOut;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AVICA.FFmpegOut
{
    public class AvicaCapture : MonoBehaviour
    {
        #region Public properties

        public bool rendersToScreen = false;

        [SerializeField] int _width = 1920;

        public int width
        {
            get { return _width; }
            set { _width = value; }
        }

        [SerializeField] int _height = 1080;

        public int height
        {
            get { return _height; }
            set { _height = value; }
        }

        [SerializeField] FFmpegPreset _preset;

        public FFmpegPreset preset
        {
            get { return _preset; }
            set { _preset = value; }
        }

        [SerializeField] float _frameRate = 30;

        public float frameRate
        {
            get { return _frameRate; }
            set { _frameRate = value; }
        }

        #endregion

        #region Private members

        FFmpegSession _session;
        RenderTexture _tempRT;
        GameObject _blitter;

        Camera cam;

        public string outputPath;

        RenderTextureFormat GetTargetFormat()
        {
            return cam.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        }

        int GetAntiAliasingLevel()
        {
            return cam.allowMSAA ? (QualitySettings.antiAliasing > 0 ? QualitySettings.antiAliasing : 1) : 1;
        }

        #endregion

        #region Time-keeping variables

        int _frameCount;
        float _startTime;
        int _frameDropCount;

        float _gameTime = 0f;

        public float FrameTime
        {
            get { return _startTime + (_frameCount + 0.5f) / _frameRate; }
        }

        public int FrameCount
        {
            get
            {
                return _frameCount;
            }
        }

        float accruedGapTime = 0f;

        void WarnFrameDrop(float gap)
        {
            accruedGapTime += gap;

            Debug.LogWarning(
                "Significant frame droppping was detected (Gap of " + gap.ToString("N4") + "s). This may introduce " +
                "time instability into output video. Decreasing the recording " +
                "frame rate is recommended."
            );
        }

        #endregion

        #region MonoBehaviour implementation

        void OnValidate()
        {
            _width = Mathf.Max(8, _width);
            _height = Mathf.Max(8, _height);
        }

        void OnDisable()
        {
            if (_session != null)
            {
                // Close and dispose the FFmpeg session.
                _session.Close();
                _session.Dispose();
                _session = null;
            }

            if (_tempRT != null)
            {
                // Dispose the frame texture.
                cam.targetTexture = null;
                Destroy(_tempRT);
                _tempRT = null;
            }

            if (_blitter != null)
            {
                // Destroy the blitter game object.
                Destroy(_blitter);
                _blitter = null;
            }

            Debug.Log("[AVICA] Accrued gap time was " + accruedGapTime);
            
        }

        private void OnEnable()
        {
            if (cam == null) // If we're not setup, do nothing
                return;

            accruedGapTime = 0f;

            Debug.Log("[AVICA] Starting FFmpegSession on " + gameObject.name);

            // Start an FFmpeg session.
            _session = FFmpegSession.CreateWithOutputPath(
                outputPath,
                cam.targetTexture.width,
                cam.targetTexture.height,
                _frameRate, preset
            );

            _startTime = 0f;
            _frameCount = 0;
            _frameDropCount = 0;
        }

        IEnumerator Start()
        {
            // Sync with FFmpeg pipe thread at the end of every frame.
            for (var eof = new WaitForEndOfFrame(); ;)
            {
                yield return eof;
                _session?.CompletePushFrames();
            }
        }

        internal void Setup()
        {
            Debug.Log("[AVICA] Setting up Capture Component on " + gameObject.name);

            if (cam == null)
                cam = GetComponent<Camera>();

            // Give a newly created temporary render texture to the camera
            // if it's set to render to a screen. Also create a blitter
            // object to keep frames presented on the screen.
            if (cam.targetTexture == null)
            {
                _tempRT = new RenderTexture(_width, _height, 24, GetTargetFormat());
                _tempRT.antiAliasing = GetAntiAliasingLevel();
                cam.targetTexture = _tempRT;

                if(rendersToScreen)
                    _blitter = Blitter.CreateInstance(cam);
            }
        }

        bool firstByte = true;

        void Update()
        {
            var delta = 1 / _frameRate;

            _gameTime += Time.deltaTime;

            // Lazy initialization
            if (_session == null)
            {
                Setup();
            }

            var gap = _gameTime - FrameTime;

            if (gap < 0)
            {
                _session.PushFrame(null);
            }
            else if (gap < delta)
            {
                // Single-frame behind from the current time:
                // Push the current frame to FFmpeg.
                _session.PushFrame(cam.targetTexture);
                _frameCount++;

                if(accruedGapTime > delta) // Play catchup when possible
                {
                    accruedGapTime -= delta;
                    _session.PushFrame(cam.targetTexture);
                    _frameCount++;
                    Debug.Log("[AVICA] Caught up by one frame");
                }
            }
            else if (gap < delta * 2)
            {
                // Two-frame behind from the current time:
                // Push the current frame twice to FFmpeg. Actually this is not
                // an efficient way to catch up. We should think about
                // implementing frame duplication in a more proper way. #fixme
                _session.PushFrame(cam.targetTexture);
                _session.PushFrame(cam.targetTexture);
                _frameCount += 2;
            }
            else
            {
                // Show a warning message about the situation.
                WarnFrameDrop(gap);

                // Push the current frame to FFmpeg.
                _session.PushFrame(cam.targetTexture);

                // Compensate the time delay.
                //_frameCount += Mathf.FloorToInt(gap * _frameRate);

                // Don't do that, it breaks the frame counter, instead:
                _frameCount++;
            }

            if (firstByte && _frameCount > 0)
            {
                firstByte = false;
                Debug.Log("First Camera Byte for " + gameObject.name + " at " + Time.time + " (Frame " + Time.frameCount + ")");
            }
        }

        #endregion
    }
}
