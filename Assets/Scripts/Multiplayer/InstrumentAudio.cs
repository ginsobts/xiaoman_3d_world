using System.Collections.Generic;
using UnityEngine;

namespace VN.Multiplayer
{
    /// <summary>无需外部音频文件的柔和木琴音色，所有客户端可确定性生成同一个音符。</summary>
    public static class InstrumentAudio
    {
        private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
        private static readonly float[] Frequencies =
        {
            261.63f, 293.66f, 329.63f, 349.23f,
            392.00f, 440.00f, 493.88f, 523.25f
        };

        public static AudioClip GetNote(int note)
        {
            note = Mathf.Abs(note) % Frequencies.Length;
            if (Clips.TryGetValue(note, out var cached)) return cached;

            const int sampleRate = 44100;
            const float duration = 1.35f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            float frequency = Frequencies[note];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Clamp01(t / 0.012f);
                float decay = Mathf.Exp(-t * 3.8f);
                float fundamental = Mathf.Sin(Mathf.PI * 2f * frequency * t);
                float overtone = Mathf.Sin(Mathf.PI * 2f * frequency * 2.01f * t) * 0.25f;
                float wooden = Mathf.Sin(Mathf.PI * 2f * frequency * 3.98f * t) * 0.08f;
                samples[i] = (fundamental + overtone + wooden) * attack * decay * 0.42f;
            }

            var clip = AudioClip.Create("CrayonXylophone_" + note, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            Clips[note] = clip;
            return clip;
        }
    }
}
