using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VetTVDirectStream;

/// <summary>Captures game/desktop audio (WASAPI loopback) + microphone, mixes to 48kHz stereo s16le and feeds a stream.</summary>
public sealed class AudioMixer : IDisposable
{
    readonly WasapiLoopbackCapture _game = new();
    WasapiCapture? _mic;
    readonly BufferedWaveProvider _gameBuf, _micBuf;
    readonly VolumeSampleProvider _gameVol, _micVol;
    readonly MixingSampleProvider _mix;
    Thread? _pump;
    volatile bool _run;

    bool _gameOn = true, _micOn = true; float _gv = 1, _mv = 1;
    public bool GameOn { get => _gameOn; set { _gameOn = value; Apply(); } }
    public bool MicOn { get => _micOn; set { _micOn = value; Apply(); } }
    public float GameVolume { get => _gv; set { _gv = value; Apply(); } }
    public float MicVolume { get => _mv; set { _mv = value; Apply(); } }
    void Apply() { _gameVol.Volume = _gameOn ? _gv : 0; _micVol.Volume = _micOn ? _mv : 0; }
    public float GameLevel, MicLevel;

    public static List<(string Id, string Name)> MicDevices()
    {
        using var en = new MMDeviceEnumerator();
        return en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Select(d => (d.ID, d.FriendlyName)).ToList();
    }

    public AudioMixer(string? micId)
    {
        _gameBuf = new BufferedWaveProvider(_game.WaveFormat) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(2) };
        _game.DataAvailable += (_, e) => { _gameBuf.AddSamples(e.Buffer, 0, e.BytesRecorded); GameLevel = Peak(e.Buffer, e.BytesRecorded, _game.WaveFormat); };

        MMDevice? micDev = null;
        using (var en = new MMDeviceEnumerator())
        {
            try { micDev = micId != null ? en.GetDevice(micId) : en.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications); } catch { }
        }
        ISampleProvider micSp;
        if (micDev != null)
        {
            _mic = new WasapiCapture(micDev);
            _micBuf = new BufferedWaveProvider(_mic.WaveFormat) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(2) };
            _mic.DataAvailable += (_, e) => { _micBuf.AddSamples(e.Buffer, 0, e.BytesRecorded); MicLevel = Peak(e.Buffer, e.BytesRecorded, _mic.WaveFormat); };
            micSp = Normalize(_micBuf.ToSampleProvider());
        }
        else
        {
            _micBuf = new BufferedWaveProvider(new WaveFormat(48000, 16, 2));
            micSp = Normalize(_micBuf.ToSampleProvider());
        }
        _gameVol = new VolumeSampleProvider(Normalize(_gameBuf.ToSampleProvider()));
        _micVol = new VolumeSampleProvider(micSp);
        _mix = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) { ReadFully = true };
        _mix.AddMixerInput(_gameVol);
        _mix.AddMixerInput(_micVol);
    }

    static ISampleProvider Normalize(ISampleProvider sp)
    {
        if (sp.WaveFormat.Channels == 1) sp = new MonoToStereoSampleProvider(sp);
        else if (sp.WaveFormat.Channels > 2)
        {
            var mux = new MultiplexingSampleProvider(new[] { sp }, 2);
            mux.ConnectInputToOutput(0, 0); mux.ConnectInputToOutput(1, 1);
            sp = mux;
        }
        if (sp.WaveFormat.SampleRate != 48000) sp = new WdlResamplingSampleProvider(sp, 48000);
        return sp;
    }

    static float Peak(byte[] buf, int n, WaveFormat f)
    {
        float max = 0;
        if (f.Encoding == WaveFormatEncoding.IeeeFloat || f.BitsPerSample == 32 && f.Encoding == WaveFormatEncoding.Extensible)
            for (int i = 0; i + 4 <= n; i += 16) max = Math.Max(max, Math.Abs(BitConverter.ToSingle(buf, i)));
        else if (f.BitsPerSample == 16)
            for (int i = 0; i + 2 <= n; i += 8) max = Math.Max(max, Math.Abs(BitConverter.ToInt16(buf, i)) / 32768f);
        return Math.Min(1f, max);
    }

    public void Start(Stream output)
    {
        _game.StartRecording();
        _mic?.StartRecording();
        _run = true;
        _pump = new Thread(() =>
        {
            const int ms = 20;
            int frames = 48 * ms;
            var f = new float[frames * 2];
            var bytes = new byte[frames * 2 * 2];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long tick = 0;
            while (_run)
            {
                int read = _mix.Read(f, 0, f.Length);
                for (int i = 0; i < read; i++)
                {
                    float s = Math.Clamp(f[i], -1f, 1f);
                    short v = (short)(s * 32767);
                    bytes[i * 2] = (byte)(v & 0xFF); bytes[i * 2 + 1] = (byte)(v >> 8);
                }
                try { output.Write(bytes, 0, read * 2); output.Flush(); } catch { break; }
                tick++;
                long wait = tick * ms - sw.ElapsedMilliseconds;
                if (wait > 0) Thread.Sleep((int)wait);
            }
        }) { IsBackground = true, Name = "AudioPump" };
        _pump.Start();
    }

    public void Dispose()
    {
        _run = false;
        try { _game.StopRecording(); } catch { }
        try { _mic?.StopRecording(); } catch { }
        _pump?.Join(500);
        _game.Dispose(); _mic?.Dispose();
    }
}
