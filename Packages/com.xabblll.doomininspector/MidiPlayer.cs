using System;
using System.IO;
using AudioSynthesis.Bank;
using AudioSynthesis.Synthesis;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Midi;
using UnityEngine;

namespace UnityMidi
{
    [RequireComponent(typeof(AudioSource))]
    public class MidiPlayer : MonoBehaviour
    {
        //TEST
        public bool isPlayTestMidiAndBank;
        PatchBank bank;
        MidiFile midi;
        
        private Synthesizer synthesizer;
        private AudioSource audioSource;
        private MidiFileSequencer sequencer;
        private int bufferHead;
        private float[] currentBuffer;

        public string midiFilePath;
        public string bankPath;

        public AudioSource AudioSource { get { return audioSource; } }
        public MidiFileSequencer Sequencer { get { return sequencer; } }
        public PatchBank Bank { get { return bank; } }
        public MidiFile MidiFile { get { return midi; } }

        private bool IsPlaying => sequencer != null && sequencer.IsPlaying;

        private void Start()
        {
            if (synthesizer == null)
            {
                Init();
            }
        }

        public void Init()
        {
            if (synthesizer != null)
                return;

            var sampleRate = AudioSettings.outputSampleRate;
            synthesizer = new Synthesizer(sampleRate, 2, 8, 8, 64);
            sequencer = new MidiFileSequencer(synthesizer);
            audioSource = GetComponent<AudioSource>();

            if (isPlayTestMidiAndBank)
            {
                var bankData = File.ReadAllBytes(bankPath);
                LoadBank(new PatchBank(new MemoryStream(bankData), Path.GetExtension(bankPath).ToLower().Remove(0, 1)));
                var midiFileData = File.ReadAllBytes(midiFilePath);
                LoadMidi(new MidiFile(midiFileData));
                SetVolume(1f);
                Play();
            }
        }

        public void SetLoopMode(bool value)
        {
            if(sequencer == null)
                return;
            sequencer.IsLoop = value;
        }

        public void SetVolume(float volume)
        {
            if (synthesizer != null)
            {
                synthesizer.MixGain = volume * 2.5f;
            }
        }

        public void LoadBank(PatchBank bank)
        {
            this.bank = bank;
            if (synthesizer != null)
            {
                synthesizer.UnloadBank();
                synthesizer.LoadBank(bank);
            }
        }

        public void LoadMidi(MidiFile midi)
        {
            Stop();
            this.midi = midi;
            if (sequencer != null)
            {
                sequencer.UnloadMidi();
                sequencer.LoadMidi(midi);
            }
        }

        public void Play()
        {
            gameObject.SetActive(true);
            if (audioSource != null)
            {
                audioSource.volume = 1f;
                audioSource.Play();
            }
            if (sequencer != null)
            {
                sequencer.Play();
            }
        }

        public void Stop()
        {
            gameObject.SetActive(false);
            if (audioSource != null)
            {
                audioSource.volume = 0f;
                audioSource.Stop();
            }
            if (synthesizer != null)
            {
                synthesizer.NoteOffAll(true);
            }
            if (sequencer != null)
            {
                sequencer.Stop();
            }
        }

        void OnAudioFilterRead(float[] data, int channel)
        {
            if (synthesizer == null || sequencer == null || bank == null || !IsPlaying)
            {
                Array.Clear(data, 0, data.Length);
                currentBuffer = null;
                bufferHead = 0;
                return;
            }

            int count = 0;
            while (count < data.Length)
            {
                if (currentBuffer == null || bufferHead >= currentBuffer.Length)
                {
                    sequencer.FillMidiEventQueue();
                    synthesizer.GetNext();
                    currentBuffer = synthesizer.WorkingBuffer;
                    bufferHead = 0;
                }
                var length = Mathf.Min(currentBuffer.Length - bufferHead, data.Length - count);
                Array.Copy(currentBuffer, bufferHead, data, count, length);
                bufferHead += length;
                count += length;
            }
        }
    }
}
