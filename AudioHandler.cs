using System.Net.WebSockets;
using NAudio.Wave;
using FFMpegCore;
using SileroVad;
using NAudio.Wave.SampleProviders;
using System.Text;

internal class AudioHandler
{
    private HttpContext context;

    public AudioHandler(HttpContext context)
    {
        this.context = context;
    }

    internal async Task Handle()
    {
        var SAMPLE_RATE = 16000;
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine("WebSocket connection established");

        byte[] buffer = new byte[1024 * 16];
        using var fileStream = new FileStream("recorded_audio.pcm", FileMode.Create);

        WebSocketReceiveResult result;
        var vad = new Vad();
        var batchcount = 0;
        var lastVadFound = 0;
        do
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            fileStream.Write(buffer, 0, result.Count);
            if (batchcount++ % 6 == 5)
            {
                fileStream.Flush();
                FFMpegArguments.FromFileInput("recorded_audio.pcm")
                    .OutputToFile("recording.wav")
                    .ProcessSynchronously();
                var reader = new WaveFileReader("recording.wav");
                var TotalTime = reader.TotalTime;
                ISampleProvider sampleProvider;

                if (reader.WaveFormat.SampleRate != SAMPLE_RATE)
                {
                    sampleProvider = new WdlResamplingSampleProvider(reader.ToSampleProvider(), SAMPLE_RATE).ToMono();
                }
                else
                {
                    sampleProvider = reader.ToSampleProvider();
                }
                var array = new float[CountSamples(TotalTime)];
                var read = sampleProvider.Read(array, 0, array.Length);
                var speachResult = vad.GetSpeechTimestamps(array, min_silence_duration_ms: 400, threshold: 0.3f, min_speech_duration_ms: 400);
                if (lastVadFound != speachResult.Count)
                {
                    lastVadFound = speachResult.Count;
                    Console.WriteLine($"Speech detected: {speachResult.Count}");
                    await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"Speech detected: {speachResult.Count}")), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else if (speachResult.Count > 0)
                {
                    Console.WriteLine("Speech over");
                    fileStream.Close();
                    var audioSpeech = VadHelper.GetSpeechSamples(array, speachResult);

                    var fileTrim = Path.ChangeExtension("recorded_audio.pcm", "speech") + ".wav";

                    using var fileWriter = new WaveFileWriter(fileTrim, new WaveFormat(16000, 1));
                    foreach (var sample in audioSpeech)
                    {
                        fileWriter.WriteSample(sample);
                    }
                    fileWriter.Flush();

                }
                Console.WriteLine($"Speech detected: {speachResult.Count}");
            }
        } while (!result.CloseStatus.HasValue);
        fileStream.Close();
        Console.WriteLine("Audio recording saved");
    }
    static int CountSamples(TimeSpan time)
    {
        WaveFormat waveFormat = new WaveFormat(16000, 1);

        return TimeSpanToSamples(time, waveFormat);
    }
    static int TimeSpanToSamples(TimeSpan time, WaveFormat waveFormat)
    {
        return (int)(time.TotalSeconds * (double)waveFormat.SampleRate) * waveFormat.Channels;
    }
}
