using System.Collections.Generic;
using UnityEngine;

namespace VN.Multiplayer
{
    /// <summary>无需外部音频文件的程序化乐器音色，所有客户端可确定性生成同一个音符。</summary>
    public static class InstrumentAudio
    {
        public enum Timbre { Xylophone = 0, Piano = 1 }

        private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
        private static readonly float[] Frequencies =
        {
            261.63f, 293.66f, 329.63f, 349.23f,
            392.00f, 440.00f, 493.88f, 523.25f,
            587.33f, 659.25f, 698.46f, 783.99f, 880.00f
        };

        public static int NoteCount => Frequencies.Length;

        public static AudioClip GetNote(int note) => GetNote(note, Timbre.Xylophone);

        public static AudioClip GetNote(int note, Timbre timbre)
        {
            note = Mathf.Abs(note) % Frequencies.Length;
            int key = note + (int)timbre * 100;
            if (Clips.TryGetValue(key, out var cached)) return cached;

            const int sampleRate = 44100;
            float duration = timbre == Timbre.Piano ? 2.4f : 1.35f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            float frequency = Frequencies[note];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float value;
                if (timbre == Timbre.Piano)
                {
                    // 锤击起音 + 多泛音 + 轻微失谐的双弦，模拟老式立式钢琴
                    float attack = Mathf.Clamp01(t / 0.004f);
                    float body = Mathf.Exp(-t * 1.9f);
                    float bright = Mathf.Exp(-t * 7f);
                    float f1 = Mathf.Sin(Mathf.PI * 2f * frequency * t);
                    float f1b = Mathf.Sin(Mathf.PI * 2f * frequency * 1.003f * t);
                    float h2 = Mathf.Sin(Mathf.PI * 2f * frequency * 2f * t) * 0.5f;
                    float h3 = Mathf.Sin(Mathf.PI * 2f * frequency * 3.01f * t) * 0.22f * bright;
                    float h4 = Mathf.Sin(Mathf.PI * 2f * frequency * 4.02f * t) * 0.12f * bright;
                    float h5 = Mathf.Sin(Mathf.PI * 2f * frequency * 5.05f * t) * 0.06f * bright;
                    float hammer = Mathf.Sin(Mathf.PI * 2f * frequency * 6.4f * t) * 0.08f * Mathf.Exp(-t * 40f);
                    value = ((f1 + f1b) * 0.5f + h2 + h3 + h4 + h5 + hammer) * attack * body * 0.36f;
                }
                else
                {
                    float attack = Mathf.Clamp01(t / 0.012f);
                    float decay = Mathf.Exp(-t * 3.8f);
                    float fundamental = Mathf.Sin(Mathf.PI * 2f * frequency * t);
                    float overtone = Mathf.Sin(Mathf.PI * 2f * frequency * 2.01f * t) * 0.25f;
                    float wooden = Mathf.Sin(Mathf.PI * 2f * frequency * 3.98f * t) * 0.08f;
                    value = (fundamental + overtone + wooden) * attack * decay * 0.42f;
                }
                samples[i] = value;
            }

            var clip = AudioClip.Create(timbre + "_" + note, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            Clips[key] = clip;
            return clip;
        }
    }
}
