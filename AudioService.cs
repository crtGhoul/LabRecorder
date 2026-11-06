using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;

namespace LabRecorder
{
    public class AudioDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public class AudioService : IDisposable
    {
        private Thread? _meterThread;
        private CancellationTokenSource? _meterCts;
        private readonly object _meterLock = new();

        public event Action<float>? OnMicLevel;

        public static List<AudioDeviceInfo> EnumerateMicrophones()
        {
            var list = new List<AudioDeviceInfo>();
            try
            {
                var enumerator = (NativeAudio.IMMDeviceEnumerator)new NativeAudio.MMDeviceEnumeratorCom();
                if (enumerator.EnumAudioEndpoints(NativeAudio.eCapture, NativeAudio.DEVICE_STATE_ACTIVE, out var coll) == 0 && coll != null)
                {
                    coll.GetCount(out int count);
                    var key = NativeAudio.PROPERTYKEY.PKEY_Device_FriendlyName;
                    for (int i = 0; i < count; i++)
                    {
                        if (coll.Item(i, out var dev) == 0 && dev != null)
                        {
                            dev.GetId(out string id);
                            dev.OpenPropertyStore(0, out var store);
                            store.GetValue(ref key, out var pv);
                            string name = Marshal.PtrToStringUni(pv.pwszVal) ?? $"Microphone {i + 1}";
                            list.Add(new AudioDeviceInfo { Id = id, Name = name });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error enumerating mics: " + ex.Message);
            }
            return list;
        }

        public void StartMicMeter(string? deviceId = null)
        {
            StopMicMeter();

            lock (_meterLock)
            {
                _meterCts = new CancellationTokenSource();
                var token = _meterCts.Token;

                _meterThread = new Thread(() =>
                {
                    NativeAudio.IAudioClient? client = null;
                    NativeAudio.IAudioMeterInformation? meter = null;
                    try
                    {
                        var enumerator = (NativeAudio.IMMDeviceEnumerator)new NativeAudio.MMDeviceEnumeratorCom();
                        NativeAudio.IMMDevice? dev;
                        if (string.IsNullOrEmpty(deviceId))
                        {
                            enumerator.GetDefaultAudioEndpoint(NativeAudio.eCapture, NativeAudio.eConsole, out dev);
                        }
                        else
                        {
                            enumerator.GetDevice(deviceId, out dev);
                        }

                        if (dev == null) return;

                        var iidClient = typeof(NativeAudio.IAudioClient).GUID;
                        dev.Activate(ref iidClient, 23, IntPtr.Zero, out object clientObj);
                        client = (NativeAudio.IAudioClient)clientObj;

                        client.GetMixFormat(out IntPtr pFormat);
                        var emptySession = Guid.Empty;
                        // Initialize shared mode capture stream so peak meter is continuously fed
                        client.Initialize(NativeAudio.AUDCLNT_SHAREMODE_SHARED, 0, 10000000, 0, pFormat, ref emptySession);
                        client.Start();

                        var iidMeter = typeof(NativeAudio.IAudioMeterInformation).GUID;
                        dev.Activate(ref iidMeter, 23, IntPtr.Zero, out object meterObj);
                        meter = (NativeAudio.IAudioMeterInformation)meterObj;

                        while (!token.IsCancellationRequested)
                        {
                            if (meter != null)
                            {
                                meter.GetPeakValue(out float peak);
                                OnMicLevel?.Invoke(peak);
                            }
                            Thread.Sleep(30); // ~33 Hz refresh
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Mic meter error: " + ex.Message);
                    }
                    finally
                    {
                        try { client?.Stop(); } catch {}
                        OnMicLevel?.Invoke(0f);
                    }
                })
                {
                    IsBackground = true,
                    Name = "MicMeterThread"
                };
                _meterThread.Start();
            }
        }

        public void StopMicMeter()
        {
            lock (_meterLock)
            {
                if (_meterCts != null)
                {
                    _meterCts.Cancel();
                    _meterThread?.Join(300);
                    _meterCts.Dispose();
                    _meterCts = null;
                    _meterThread = null;
                }
            }
            OnMicLevel?.Invoke(0f);
        }

        public static void RunSystemAudioLoopback(string pipeName, CancellationToken token)
        {
            // Background worker that connects to named pipe and streams 48000 Hz, 16-bit, Stereo PCM
            NamedPipeServerStream? server = null;
            NativeAudio.IAudioClient? client = null;
            try
            {
                server = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                
                var asyncResult = server.BeginWaitForConnection(null, null);
                while (!token.IsCancellationRequested && !asyncResult.IsCompleted)
                {
                    Thread.Sleep(20);
                }
                if (token.IsCancellationRequested)
                {
                    try { server.Close(); } catch {}
                    return;
                }
                server.EndWaitForConnection(asyncResult);

                var enumerator = (NativeAudio.IMMDeviceEnumerator)new NativeAudio.MMDeviceEnumeratorCom();
                if (enumerator.GetDefaultAudioEndpoint(NativeAudio.eRender, NativeAudio.eConsole, out var outDev) != 0 || outDev == null)
                    return;

                var iidClient = typeof(NativeAudio.IAudioClient).GUID;
                outDev.Activate(ref iidClient, 23, IntPtr.Zero, out object clientObj);
                client = (NativeAudio.IAudioClient)clientObj;

                client.GetMixFormat(out IntPtr pFormat);
                var emptySession = Guid.Empty;
                int hr = client.Initialize(NativeAudio.AUDCLNT_SHAREMODE_SHARED, NativeAudio.AUDCLNT_STREAMFLAGS_LOOPBACK, 10000000, 0, pFormat, ref emptySession);
                if (hr != 0) return;

                var iidCapture = typeof(NativeAudio.IAudioCaptureClient).GUID;
                client.GetService(ref iidCapture, out object capObj);
                var capture = (NativeAudio.IAudioCaptureClient)capObj;

                client.Start();

                // Standard PCM buffer: 48kHz, 16-bit, Stereo = 192 bytes/ms. 20ms = 3840 bytes.
                int sampleRate = 48000;
                int bytesPer20ms = (sampleRate * 2 * 2 * 20) / 1000;
                byte[] silence = new byte[bytesPer20ms];

                var sw = Stopwatch.StartNew();
                long nextExpectedMs = 0;

                while (!token.IsCancellationRequested && server.IsConnected)
                {
                    bool gotData = false;
                    while (capture.GetNextPacketSize(out uint packetSize) == 0 && packetSize > 0)
                    {
                        if (capture.GetBuffer(out IntPtr pData, out uint numFrames, out int flags, out ulong devPos, out ulong qpcPos) == 0)
                        {
                            if (numFrames > 0)
                            {
                                gotData = true;
                                int bytes = (int)(numFrames * 8); // Assuming 32-bit float stereo = 8 bytes/frame
                                byte[] floatBytes = new byte[bytes];
                                Marshal.Copy(pData, floatBytes, 0, bytes);

                                // Convert float samples to 16-bit PCM for universal FFmpeg compatibility
                                int sampleCount = bytes / 4;
                                byte[] pcm16 = new byte[sampleCount * 2];
                                for (int i = 0; i < sampleCount; i++)
                                {
                                    float f = BitConverter.ToSingle(floatBytes, i * 4);
                                    if (f > 1.0f) f = 1.0f;
                                    else if (f < -1.0f) f = -1.0f;
                                    short s = (short)(f * 32767);
                                    pcm16[i * 2] = (byte)(s & 0xFF);
                                    pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                                }

                                try { server.Write(pcm16, 0, pcm16.Length); }
                                catch { break; }
                            }
                            capture.ReleaseBuffer(numFrames);
                        }
                    }

                    if (!gotData)
                    {
                        // Output silence to keep clock synchronized
                        try { server.Write(silence, 0, silence.Length); }
                        catch { break; }
                    }

                    nextExpectedMs += 20;
                    long sleep = nextExpectedMs - sw.ElapsedMilliseconds;
                    if (sleep > 0 && sleep < 50) Thread.Sleep((int)sleep);
                    else if (sleep < -100) nextExpectedMs = sw.ElapsedMilliseconds;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("System audio loopback error: " + ex.Message);
            }
            finally
            {
                try { client?.Stop(); } catch {}
                try { server?.Close(); } catch {}
            }
        }

        public void Dispose()
        {
            StopMicMeter();
        }
    }
}
