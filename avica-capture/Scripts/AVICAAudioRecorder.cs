using AVICA.Capture.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AVICA.Capture
{
    public class CaptureAudioRecorder : MonoBehaviour
    {
        AudioListener audioListener;

        FileStream audioStream;

        System.Text.Encoding UTF8 = System.Text.Encoding.UTF8;

        int headerSize = 44;
        int bufferSize;
        int numBuffers;
        int rescaleFactor = 32767;

        bool isRecording = false;

        bool firstByte = true;
        int frame = 0;
        float time = 0f;

        internal void StartRecording(CaptureSessionData sessionData)
        {
            audioListener = GetComponent<AudioListener>();

            if(audioListener == null)
            {
                Debug.Log("[AVICA] No Audio Listener found, skipping audio recording");
                return;
            }

            audioStream = new FileStream(GetOutputPath(sessionData), FileMode.CreateNew);

            for(int i = 0; i < headerSize; i++)
                audioStream.WriteByte(new byte()); // Empty data for the header

            AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);
            isRecording = true;
            firstByte = true;
            Update();
        }

        private void Update()
        {
            time = Time.time;
            frame = Time.frameCount;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (isRecording && audioStream != null)
            {
                if(firstByte)
                {
                    firstByte = false;
                    Debug.Log("First Audio Byte at " + time + " (Frame " + frame + ")");
                }

                WriteAudioData(data);
            }
        }

        internal void StopRecording()
        {
            if (audioStream != null)
                WriteHeader();

            isRecording = false;
        }

        private void OnApplicationQuit()
        {
            StopRecording();
        }

        void WriteAudioData(float[] data)
        {
            Int16[] intData = new Int16[data.Length];
            byte[] bytesData = new byte[data.Length * 2];
            byte[] byteArr = new byte[2];

            for(int i = 0; i < data.Length; i++)
            {
                intData[i] = (Int16)(data[i] * rescaleFactor);
                byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }

            audioStream.Write(bytesData, 0, bytesData.Length);
        }

        void WriteHeader()
        {
            audioStream.Seek(0, SeekOrigin.Begin);

            audioStream.Write(UTF8.GetBytes("RIFF"), 0, 4);
            audioStream.Write(BitConverter.GetBytes(audioStream.Length - 8), 0, 4);
            audioStream.Write(UTF8.GetBytes("WAVE"), 0, 4);
            audioStream.Write(UTF8.GetBytes("fmt "), 0, 4);
            audioStream.Write(BitConverter.GetBytes(16), 0, 4);
            audioStream.Write(BitConverter.GetBytes((UInt16)1), 0, 2); // Audio Format
            audioStream.Write(BitConverter.GetBytes((UInt16)2), 0, 2); // Num Channels

            int sampleRate = AudioSettings.outputSampleRate;

            audioStream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
            audioStream.Write(BitConverter.GetBytes(sampleRate * 4), 0, 4);

            audioStream.Write(BitConverter.GetBytes((UInt16)4), 0, 2);
            audioStream.Write(BitConverter.GetBytes((UInt16)16), 0, 2);
            audioStream.Write(UTF8.GetBytes("data"), 0, 4);
            audioStream.Write(BitConverter.GetBytes(audioStream.Length - headerSize), 0, 4); // Header Size

            Debug.Log("[AVICA] Audio written to file with " + audioStream.Length + " bytes");

            audioStream.Close();
        }

        string GetOutputPath(CaptureSessionData sessionData)
        {
            return System.IO.Path.Combine(AVICAManager.GetSessionPath(sessionData.SessionId), "audio.wav");
        }
    }
}
