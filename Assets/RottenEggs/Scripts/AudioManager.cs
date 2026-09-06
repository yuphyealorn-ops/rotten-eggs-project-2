using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// Small sound player for the prototype, ported from the Java original.
    ///
    /// <para>Every operation is deliberately fail-safe: a missing file or an
    /// unreadable header turns audio into a no-op rather than interrupting the
    /// game. The bundled WAVs are plain 16-bit mono PCM at 22050 Hz, which is
    /// parsed here directly so the clips need no Unity import settings.</para>
    /// </summary>
    public sealed class AudioManager
    {
        private const float DefaultVolume = 0.70f;
        private const float MusicLevel = 0.40f;
        private const float SfxLevel = 0.85f;

        public enum Sfx
        {
            UiMove,
            UiConfirm,
            Throw,
            Catch,
            Boost,
            Crack,
            Hit,
            ChickenDown,
            Win,
            Lose
        }

        public enum Music
        {
            Menu,
            Game
        }

        private static readonly Dictionary<Sfx, string> SfxFiles = new Dictionary<Sfx, string>
        {
            { Sfx.UiMove, "ui-move.wav" },
            { Sfx.UiConfirm, "ui-confirm.wav" },
            { Sfx.Throw, "throw.wav" },
            { Sfx.Catch, "catch.wav" },
            { Sfx.Boost, "boost.wav" },
            { Sfx.Crack, "crack.wav" },
            { Sfx.Hit, "hit.wav" },
            { Sfx.ChickenDown, "chicken-down.wav" },
            { Sfx.Win, "win.wav" },
            { Sfx.Lose, "lose.wav" }
        };

        private static readonly Dictionary<Music, string> MusicFiles = new Dictionary<Music, string>
        {
            { Music.Menu, "menu-loop.wav" },
            { Music.Game, "game-loop.wav" }
        };

        private readonly Dictionary<Sfx, AudioSource> sfxSources = new Dictionary<Sfx, AudioSource>();
        private readonly Dictionary<Music, AudioClip> musicClips = new Dictionary<Music, AudioClip>();
        private AudioSource musicSource;
        private float volume = DefaultVolume;
        private bool muted;
        private bool playbackAvailable;
        private bool closed;
        private bool warningPrinted;
        private Music? currentMusicTrack;

        private AudioManager()
        {
        }

        public static string AudioDirectory
        {
            get { return Path.Combine(Application.streamingAssetsPath, "RottenEggs", "Audio"); }
        }

        /// <summary>Creates a manager that hosts its audio sources on the given object.</summary>
        public static AudioManager Create(GameObject host)
        {
            AudioManager manager = new AudioManager();
            manager.LoadPlaybackClips(host);
            return manager;
        }

        /// <summary>
        /// Returns a fully functional no-op manager for previews and tests.
        /// Volume and mute state still behave normally.
        /// </summary>
        public static AudioManager Silent()
        {
            return new AudioManager();
        }

        public void Play(Sfx effect)
        {
            if (!CanPlay() || muted || volume <= 0.0f)
            {
                return;
            }

            AudioSource source;
            if (!sfxSources.TryGetValue(effect, out source) || source == null || source.clip == null)
            {
                return;
            }

            source.Stop();
            source.volume = volume * SfxLevel;
            source.Play();
        }

        public void PlayMusic(Music track)
        {
            if (closed)
            {
                return;
            }

            if (currentMusicTrack.HasValue
                && currentMusicTrack.Value == track
                && musicSource != null
                && musicSource.isPlaying)
            {
                return;
            }

            if (musicSource != null)
            {
                musicSource.Stop();
            }

            currentMusicTrack = track;
            AudioClip clip;
            if (!CanPlay() || musicSource == null || !musicClips.TryGetValue(track, out clip) || clip == null)
            {
                return;
            }

            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.time = 0f;
            musicSource.volume = volume * MusicLevel;
            if (!muted && volume > 0.0f)
            {
                musicSource.Play();
            }
        }

        public void StopMusic()
        {
            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.time = 0f;
            }

            currentMusicTrack = null;
        }

        public bool ToggleMute()
        {
            SetMuted(!muted);
            return muted;
        }

        public void SetMuted(bool shouldMute)
        {
            if (muted == shouldMute)
            {
                return;
            }

            muted = shouldMute;
            ApplyAllVolumes();
            if (musicSource == null || musicSource.clip == null)
            {
                return;
            }

            if (muted || volume <= 0.0f)
            {
                musicSource.Stop();
            }
            else if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        public bool IsMuted()
        {
            return muted;
        }

        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            ApplyAllVolumes();

            if (musicSource == null || musicSource.clip == null)
            {
                return;
            }

            if (volume <= 0.0f)
            {
                musicSource.Stop();
            }
            else if (!muted && !musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        public float GetVolume()
        {
            return volume;
        }

        public bool IsPlaybackAvailable()
        {
            return playbackAvailable && !closed;
        }

        public void Close()
        {
            if (closed)
            {
                return;
            }

            closed = true;
            playbackAvailable = false;
            if (musicSource != null)
            {
                musicSource.Stop();
            }

            foreach (AudioSource source in sfxSources.Values)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }

            sfxSources.Clear();
            musicClips.Clear();
            musicSource = null;
            currentMusicTrack = null;
        }

        private bool CanPlay()
        {
            return playbackAvailable && !closed;
        }

        private void LoadPlaybackClips(GameObject host)
        {
            if (host == null)
            {
                return;
            }

            musicSource = host.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            foreach (KeyValuePair<Music, string> entry in MusicFiles)
            {
                AudioClip clip = LoadClip(entry.Value);
                if (clip != null)
                {
                    musicClips[entry.Key] = clip;
                }
            }

            foreach (KeyValuePair<Sfx, string> entry in SfxFiles)
            {
                AudioClip clip = LoadClip(entry.Value);
                if (clip == null)
                {
                    continue;
                }

                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.clip = clip;
                sfxSources[entry.Key] = source;
            }

            playbackAvailable = musicClips.Count > 0 || sfxSources.Count > 0;
            ApplyAllVolumes();
        }

        private AudioClip LoadClip(string filename)
        {
            string path = Path.Combine(AudioDirectory, filename);
            try
            {
                if (!File.Exists(path))
                {
                    WarnOnce("a bundled sound is missing: " + filename, null);
                    return null;
                }

                return DecodeWav(File.ReadAllBytes(path), Path.GetFileNameWithoutExtension(filename));
            }
            catch (Exception exception)
            {
                WarnOnce("a bundled sound could not be decoded: " + filename, exception);
                return null;
            }
        }

        private void ApplyAllVolumes()
        {
            bool effectivelyMuted = muted || volume <= 0.0f;
            foreach (AudioSource source in sfxSources.Values)
            {
                if (source != null)
                {
                    source.volume = effectivelyMuted ? 0f : volume * SfxLevel;
                }
            }

            if (musicSource != null)
            {
                musicSource.volume = effectivelyMuted ? 0f : volume * MusicLevel;
            }
        }

        private void WarnOnce(string context, Exception exception)
        {
            if (warningPrinted)
            {
                return;
            }

            warningPrinted = true;
            string detail = exception == null ? "" : ": " + exception.Message;
            Debug.LogWarning("Audio warning - " + context + detail + ". Continuing silently.");
        }

        /// <summary>
        /// Turns a RIFF/WAVE byte stream into an AudioClip. Uncompressed PCM at
        /// 8, 16, 24 or 32 bits and 32-bit float are all accepted, so extra
        /// sounds dropped into the folder later work without conversion.
        /// </summary>
        public static AudioClip DecodeWav(byte[] bytes, string clipName)
        {
            if (bytes == null || bytes.Length < 44)
            {
                throw new IOException("is too short to be a WAV file");
            }

            if (ReadTag(bytes, 0) != "RIFF" || ReadTag(bytes, 8) != "WAVE")
            {
                throw new IOException("is not a RIFF/WAVE file");
            }

            int formatTag = 0;
            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            int dataOffset = -1;
            int dataLength = 0;

            int cursor = 12;
            while (cursor + 8 <= bytes.Length)
            {
                string chunkId = ReadTag(bytes, cursor);
                int chunkSize = BitConverter.ToInt32(bytes, cursor + 4);
                int body = cursor + 8;
                if (chunkSize < 0 || body + chunkSize > bytes.Length)
                {
                    chunkSize = bytes.Length - body;
                }

                if (chunkId == "fmt ")
                {
                    formatTag = BitConverter.ToUInt16(bytes, body);
                    channels = BitConverter.ToUInt16(bytes, body + 2);
                    sampleRate = BitConverter.ToInt32(bytes, body + 4);
                    bitsPerSample = BitConverter.ToUInt16(bytes, body + 14);
                }
                else if (chunkId == "data")
                {
                    dataOffset = body;
                    dataLength = chunkSize;
                }

                cursor = body + chunkSize + (chunkSize % 2);
            }

            if (dataOffset < 0 || channels <= 0 || sampleRate <= 0)
            {
                throw new IOException("is missing its fmt or data chunk");
            }

            const int PcmInteger = 1;
            const int PcmFloat = 3;
            const int Extensible = 0xFFFE;
            if (formatTag != PcmInteger && formatTag != PcmFloat && formatTag != Extensible)
            {
                throw new IOException("must be uncompressed PCM");
            }

            int bytesPerSample = bitsPerSample / 8;
            if (bytesPerSample <= 0)
            {
                throw new IOException("has an unreadable sample size");
            }

            int totalSamples = dataLength / bytesPerSample;
            if (totalSamples <= 0)
            {
                throw new IOException("contains no audio frames");
            }

            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                int at = dataOffset + i * bytesPerSample;
                switch (bitsPerSample)
                {
                    case 8:
                        samples[i] = (bytes[at] - 128) / 128f;
                        break;
                    case 16:
                        samples[i] = BitConverter.ToInt16(bytes, at) / 32768f;
                        break;
                    case 24:
                        int packed = (bytes[at + 2] << 24) | (bytes[at + 1] << 16) | (bytes[at] << 8);
                        samples[i] = (packed >> 8) / 8388608f;
                        break;
                    case 32:
                        samples[i] = formatTag == PcmFloat
                            ? BitConverter.ToSingle(bytes, at)
                            : BitConverter.ToInt32(bytes, at) / 2147483648f;
                        break;
                    default:
                        throw new IOException("uses an unsupported sample size of " + bitsPerSample + " bits");
                }
            }

            AudioClip clip = AudioClip.Create(clipName, totalSamples / channels, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static string ReadTag(byte[] bytes, int offset)
        {
            return "" + (char)bytes[offset] + (char)bytes[offset + 1]
                   + (char)bytes[offset + 2] + (char)bytes[offset + 3];
        }

        public static IEnumerable<string> BundledFilenames()
        {
            foreach (string filename in MusicFiles.Values)
            {
                yield return filename;
            }

            foreach (string filename in SfxFiles.Values)
            {
                yield return filename;
            }
        }

        /// <summary>Checks every bundled WAV parses, without opening an output device.</summary>
        public static string VerifyBundledAssets()
        {
            List<string> failures = new List<string>();
            int verified = 0;

            foreach (string filename in BundledFilenames())
            {
                string path = Path.Combine(AudioDirectory, filename);
                try
                {
                    if (!File.Exists(path))
                    {
                        failures.Add(filename + " is missing");
                        continue;
                    }

                    AudioClip clip = DecodeWav(File.ReadAllBytes(path), filename);
                    if (clip == null || clip.samples <= 0)
                    {
                        failures.Add(filename + " contains no audio frames");
                        continue;
                    }

                    verified++;
                }
                catch (Exception exception)
                {
                    failures.Add(filename + " cannot be decoded: " + exception.Message);
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("Audio asset verification failed: " + string.Join("; ", failures));
            }

            return "AUDIO ASSETS VERIFIED: " + verified + " PCM WAV files";
        }
    }
}
