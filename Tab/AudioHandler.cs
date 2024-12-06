using System.Net.WebSockets;
using NAudio.Wave;
using FFMpegCore;
using SileroVad;
using NAudio.Wave.SampleProviders;
using System.Text;
using Newtonsoft.Json;

internal class AudioHandler
{
    private HttpContext context;
    private Coflnet.Whisper.Api.EndpointsApi apiClient;
    private ILogger<AudioHandler> logger;
    const int SAMPLE_RATE = 16000;

    public AudioHandler(HttpContext context)
    {
        this.context = context;
        this.apiClient = context.RequestServices.GetRequiredService<Coflnet.Whisper.Api.EndpointsApi>();
        this.logger = context.RequestServices.GetRequiredService<ILogger<AudioHandler>>();
    }

    private class NameHandler
    {
        int NextState = 0;
        int max = 2;
        private string tempFolder;

        public NameHandler(string tempFolder)
        {
            this.tempFolder = tempFolder;
        }

        public string GetCurrent()
        {
            return Path.Combine(tempFolder, NextState.ToString());
        }

        public void Forward()
        {
            NextState++;
            if (NextState > max)
                NextState = 0;
        }

        public string Last()
        {
            if (NextState == 0)
                return Path.Combine(tempFolder, max.ToString());
            return Path.Combine(tempFolder, (NextState - 1).ToString());
        }
    }

    internal async Task Handle()
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine("WebSocket connection established");

        byte[] buffer = new byte[1024 * 35];
        // var fileNameA = "a.webm";
        //  var fileNameB = "b.webm";
        var remoteIpProxiedHeader = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress.ToString();
        var tempFolder = Path.Combine(Path.GetTempPath(), "audio" + remoteIpProxiedHeader);
        Directory.Delete(tempFolder, true);
        Directory.CreateDirectory(tempFolder);
        var names = new NameHandler(tempFolder);
        var fileName = names.GetCurrent();
        var fileStream = new FileStream(fileName, FileMode.Create);
        if (!Directory.Exists(tempFolder))
        {
            Directory.CreateDirectory(tempFolder);
        }

        WebSocketReceiveResult result;
        var vad = new Vad();
        var batchcount = 0;
        var lastVadFound = 0;
        var indexInStream = 0;
        var processedAlready = TimeSpan.Zero;
        do
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var isTextFrame = result.MessageType == WebSocketMessageType.Text;
            if (isTextFrame)
            {
                logger.LogDebug("Text frame received");
                fileStream.Flush();
                fileStream.Dispose();

                names.Forward();
                fileName = names.GetCurrent();
                fileStream = new FileStream(fileName, FileMode.Create);
                processedAlready -= TimeSpan.FromSeconds(10);
                if (processedAlready < TimeSpan.Zero)
                    processedAlready = TimeSpan.Zero;
                continue;
            }
            fileStream.Write(buffer, 0, result.Count);
            if (batchcount++ % 3 == 2)
            {
                fileStream.Flush();
                var otherFile = names.Last();
                List<string> files = [fileName];
                if (File.Exists(otherFile))
                {
                    files.Insert(0, otherFile);
                }
                FFMpegArguments.FromConcatInput(files, opt => opt.Seek(processedAlready))
                    .OutputToFile("recording.wav")
                    .ProcessSynchronously();
                var reader = new WaveFileReader("recording.wav");
                var TotalTime = reader.TotalTime;
                logger.LogDebug($"Previous file is {otherFile} current file is {fileName} total Length {TotalTime} {processedAlready}");
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
                if (speachResult.Count > 0 && IsNotTalkingActively(array, speachResult))
                {
                    lastVadFound = speachResult.Count;
                    processedAlready = TimeSpan.FromSeconds((float)speachResult.Last().End / SAMPLE_RATE);
                    Console.WriteLine($"Speech detected: {speachResult.Count} {JsonConvert.SerializeObject(speachResult)} {fileStream.Length}");
                    await SendBack(webSocket, "speaking", "true");

                    Console.WriteLine("Speech over");
                    // reset stream
                    var audioSpeech = VadHelper.GetSpeechSamples(array, speachResult);

                    var batchfile = Path.Combine(tempFolder, $"batch_{indexInStream++}.wav");

                    using var fileWriter = new WaveFileWriter(batchfile, new WaveFormat(16000, 1));
                    foreach (var sample in audioSpeech)
                    {
                        fileWriter.WriteSample(sample);
                    }
                    fileWriter.Flush();
                    Console.WriteLine("Processing audio total time: " + TotalTime);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using FileStream reduced = await ProcessAudio(webSocket, batchfile);
                        }
                        catch (System.Exception e)
                        {
                            logger.LogError(e, "Error while processing audio");
                        }

                    });
                }
                if (speachResult.Count == 0)
                {
                    await SendBack(webSocket, "speaking", "false");
                }
            }
        } while (!result.CloseStatus.HasValue);
        fileStream.Dispose();
        Console.WriteLine("Audio recording saved");
    }

    private static async Task SendBack(WebSocket webSocket, string type, string content)
    {
        var command = new Command
        {
            Type = type,
            Content = content
        };
        var json = JsonConvert.SerializeObject(command);
        var byteSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        await webSocket.SendAsync(byteSegment, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public class Command
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("content")]
        public string Content { get; set; }
    }

    private static bool IsNotTalkingActively(float[] array, List<VadSpeech> speachResult)
    {
        var isNotActive = speachResult.Last().End < array.Length - SAMPLE_RATE / 2;
        if (!isNotActive)
            Console.WriteLine("talking actively");
        return isNotActive;
    }

    private async Task<FileStream> ProcessAudio(WebSocket webSocket, string batchfile)
    {
        var mp3 = batchfile.Replace(".wav", ".mp3");
        FFMpegArguments.FromFileInput(batchfile)
            .OutputToFile(mp3)
            .ProcessSynchronously();
        using var reduced = File.OpenRead(mp3);
        logger.LogInformation($"Reduced file size: {reduced.Length} sending to recognition");
        var speachText = await apiClient.AsrAsrPostWithHttpInfoAsync(reduced, true, "transcribe", null, null, false, false, "txt");
        var content = speachText.RawContent;
        Console.WriteLine($"Speech to text: {content}");
        if (!string.IsNullOrWhiteSpace(content))
            await SendBack(webSocket, "transcript", content);
        return reduced;
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
