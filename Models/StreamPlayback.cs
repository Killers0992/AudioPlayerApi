using PlayerRoles.FirstPersonControl;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class StreamPlayback
{
    private CancellationTokenSource _cts;
    private ConcurrentQueue<float> _pcmQueue = new ConcurrentQueue<float>();

    public string Url { get; }
    public string Name { get; }
    public bool IsActive { get; private set; } = true;

    public bool IsInitializing { get; set; } = true;

    public StreamPlayback(string url, string name)
    {
        Url = url;
        Name = name;

        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            await Ffmpeg.InitializeFfmpegAsync();

            try
            {
                await RunFfmpegPipeline(_cts.Token);
            }
            catch (Win32Exception win32ex)
            {
                switch (win32ex.NativeErrorCode)
                {
                    // Access denied
                    case 5:
                        ServerConsole.AddLog($"[AudioPlayer] FFmpeg {Ffmpeg.FfmpegPath} access denied, please check permissions on file!");
                        break;
                    case 2:
                        ServerConsole.AddLog($"[AudioPlayer] FFmpeg {Ffmpeg.FfmpegPath} not found, please ensure it is installed and the path is correct!");
                        break;
                    default:
                        ServerConsole.AddLog($"[AudioPlayer] Failed to run FFmpegPipeline, ffmpeg most likely is not installed! ( Code {win32ex.NativeErrorCode} )\n{win32ex}");
                        break;
                }
            }
            catch (Exception ex)
            {
                ServerConsole.AddLog($"[AudioPlayer] Error in live stream playback: {ex}");
            }
        }, _cts.Token);
    }

    async Task RunFfmpegPipeline(CancellationToken ct)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = Ffmpeg.FfmpegPath,
            Arguments = $"-hide_banner -loglevel error -i \"{Url}\" -vn -ac 1 -ar 48000 -c:a libvorbis -f ogg pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process ff = Process.Start(psi))
        {
            ServerConsole.AddLog($"[AudioPlayer] Running live stream for {Name} ({Url})");

            ct.Register(static state =>
            {
                try
                {
                    ServerConsole.AddLog("[AudioPlayer] Cancelling ffmpeg process...");
                    (state as Process)?.Close();
                }
                catch (Exception ex)
                {
                    ServerConsole.AddLog($"[AudioPlayer] Error closing ffmpeg process: {ex}");
                }
            }, ff);

            using (ProducerConsumerStream pipe = new ProducerConsumerStream())
            {
                _ = Task.Run(async () =>
                {
                    byte[] buffer = new byte[8192];
                    while (!ct.IsCancellationRequested)
                    {
                        int read = await ff.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length, ct);

                        if (read <= 0)
                            break;

                        pipe.Write(buffer, 0, read);
                    }
                    pipe.CompleteAdding();
                }, ct);

                using (var vorbis = new VorbisReader(pipe, false))
                {
                    float[] temp = new float[4096];
                    while (!ct.IsCancellationRequested)
                    {
                        int got = vorbis.ReadSamples(temp, 0, temp.Length);
                        if (got <= 0)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        IsInitializing = false;

                        for (int i = 0; i < got; i++)
                            _pcmQueue.Enqueue(temp[i]);
                    }
                }
            }
        }
        
    }

    /// <summary>
    /// Pulls next PCM block for AudioPlayer mixer.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int written = 0;
        while (written < count && _pcmQueue.TryDequeue(out float sample))
        {
            buffer[offset + written] = sample;
            written++;
        }
        return written;
    }
    
    /// <summary>
    /// Stops stream.
    /// </summary>
    public void Stop()
    {
        ServerConsole.AddLog($"[AudioPlayer] Stopping live stream playback for {Name} ({Url})");
        IsActive = false;
        _cts?.Cancel();
    }

    /// <summary>
    /// Simple non-seekable producer/consumer stream (ring buffer) for live pipelines.
    /// </summary>
    private sealed class ProducerConsumerStream : Stream
    {
        private readonly BlockingCollection<byte[]> _chunks;
        private readonly int _chunkSize;
        private byte[] _current;
        private int _offset;
        private bool _completed;

        public ProducerConsumerStream(int capacityBytes = 1 << 20, int chunkSize = 16 * 1024)
        {
            _chunkSize = chunkSize;
            _chunks = new BlockingCollection<byte[]>(boundedCapacity: Math.Max(1, capacityBytes / chunkSize));
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_completed && _current == null) return 0;

            int total = 0;

            while (count > 0)
            {
                if (_current == null)
                {
                    if (!_chunks.TryTake(out _current, Timeout.Infinite))
                    {
                        if (_completed) return total;
                        continue;
                    }
                    _offset = 0;
                }

                int copy = Math.Min(count, _current.Length - _offset);
                Array.Copy(_current, _offset, buffer, offset, copy);
                _offset += copy;
                offset += copy;
                count -= copy;
                total += copy;

                if (_offset >= _current.Length)
                {
                    _current = null;
                }

                if (total > 0) 
                    break;
            }

            return total;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            while (count > 0 && !_chunks.IsAddingCompleted)
            {
                int n = Math.Min(count, _chunkSize);
                var chunk = new byte[n];
                Array.Copy(buffer, offset, chunk, 0, n);
                _chunks.Add(chunk);
                offset += n;
                count -= n;
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public void CompleteAdding()
        {
            _completed = true;
            _chunks.CompleteAdding();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _chunks?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
