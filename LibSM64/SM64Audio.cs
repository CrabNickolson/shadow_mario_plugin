using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace LibSM64
{
    public class SM64Audio
    {
        public float m_volume = 1;

        private AudioSource m_audioSource;
        private Thread m_audioThread;
        private float[] m_audioBuffer;
        private float[] m_audioBufferTemp;
        private int m_bufferLength;
        private bool m_stopThread;

        public const int c_frequency = 32000;
        private const int c_sampleArrayFactor = 2 * 2;
        private const int c_bufferWindowSize = 544 * c_sampleArrayFactor;

        internal static SM64Audio Create(GameObject _go)
        {
            if (AudioSettings.outputSampleRate != c_frequency)
            {
                ShadowMario.Plugin.PluginLog.LogWarning($"Project outputSampleRate is not set to {c_frequency}. Mario audio will not work");
                return null;
            }

            return new SM64Audio(_go);
        }

        private SM64Audio(GameObject _go)
        {
            m_audioBuffer = new float[c_bufferWindowSize * 4];
            m_audioBufferTemp = new float[c_bufferWindowSize];
            m_bufferLength = c_bufferWindowSize * 2;

            SM64Context.AudioInit();

            m_audioThread = new Thread(audioLoop);
            m_audioThread.Start();

            m_audioSource = _go.AddComponent<AudioSource>();
            m_audioSource.Play();
        }

        internal void Dispose()
        {
            m_stopThread = true;
            m_audioThread?.Join();
            m_audioThread = null;
        }

        internal void OnAudioFilterRead(Il2CppStructArray<float> _data, int _channels)
        {
            if (_channels != 2)
                return;

            if (!SM64Context.UpdateActive)
                return;

            lock (m_audioBuffer)
            {
                float volumeSqr = m_volume * m_volume;

                int copyLength = Mathf.Min(_data.Length, m_bufferLength);
                for (int i = 0; i < copyLength; i++)
                {
                    _data[i] = _data[i] + (m_audioBuffer[i] * volumeSqr);
                }
                m_bufferLength -= copyLength;

                if (copyLength < _data.Length)
                    ShadowMario.Plugin.PluginLog.LogWarning($"Too few audio samples! {copyLength} < {_data.Length}");

                System.Array.Copy(m_audioBuffer, copyLength, m_audioBuffer, 0, m_audioBuffer.Length - copyLength);
            }
        }

        private void audioLoop()
        {
            PirateBase.ModThreadUtility.AttachThread();

            while (true)
            {
                if (m_stopThread)
                {
                    PirateBase.ModThreadUtility.DetachThread();
                    return;
                }

                audioTick();
                Thread.Sleep(33);
            }
        }

        private void audioTick()
        {
            lock (m_audioBuffer)
            {
                if (m_bufferLength + c_bufferWindowSize < m_audioBuffer.Length)
                {
                    uint numSamples;
                    numSamples = SM64Context.AudioTick((uint)m_bufferLength / 2, 1100, m_audioBufferTemp);
                    int copyLength = (int)numSamples * c_sampleArrayFactor;
                    System.Array.Copy(m_audioBufferTemp, 0, m_audioBuffer, m_bufferLength, copyLength);
                    m_bufferLength += copyLength;
                }
            }
        }
    }
}
