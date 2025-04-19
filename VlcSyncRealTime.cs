using SharpHook.Reactive;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Reactive.Linq;

class VlcLauncher
{
    private static readonly SimpleReactiveGlobalHook hook = new();
    private static readonly FirebaseClient firebase = new("https://your-project.firebaseio.com/");
    private static TcpClient vlcClient;
    private static NetworkStream stream;

    private static bool isPlaying;

    static async Task Main(string[] args)
    {
        if (!await LaunchVlcWithRCReference(args))
            return;

        SetupGlobalHook();
        SetupFirebaseObserver();
        ResetFirebaseVlcState();

        Console.WriteLine("Press Enter to exit.");
        Console.ReadLine();
    }

    private static async Task<bool> LaunchVlcWithRCReference(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("Please drag and drop a video file onto this executable.");
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
            return false;
        }

        string videoPath = args[0];
        string vlcPath = @"C:\Program Files\VideoLAN\VLC\vlc.exe";
        int rcPort = 4212;

        // Launch VLC with RC interface
        var startInfo = new ProcessStartInfo
        {
            FileName = vlcPath,
            Arguments = $"\"{videoPath}\" --intf rc --rc-host=localhost:{rcPort}",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Console.WriteLine("Launching VLC...");
        Process.Start(startInfo);

        // Wait for VLC to start and open port
        Console.WriteLine("Waiting for VLC RC interface...");
        await Task.Delay(1500); // May need more for slower machines

        // Connect to VLC via TCP
        try
        {
            vlcClient = new TcpClient("localhost", rcPort);
            stream = vlcClient.GetStream();

            // Read VLC banner
            var buffer = new byte[1024];
            var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
            await Task.WhenAny(readTask, Task.Delay(1000));
            Console.WriteLine("Connected to VLC.");

            await SendVlcCommand("seek 0");
            await SendVlcCommand("pause");

            Console.WriteLine("Seek to 0, playback paused");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error connecting to VLC RC interface: " + ex.Message);
            return false;
        }
        return true;
    }

    private static void SetupGlobalHook()
    {
        hook.KeyPressed
            .Where(e => e.Data.KeyCode == SharpHook.Native.KeyCode.VcY)
            .Subscribe(async e =>
            {
                Console.WriteLine("Y was pressed");

                // Send VLC play/pause toggle or trigger sync logic
                VlcState currentState = await GetCurrentVlcState();
                await SendFirebaseState(new() { currentTime = currentState.currentTime, isPlaying = !isPlaying });
            });

        _ = hook.RunAsync();
    }

    private static void SetupFirebaseObserver()
    {
        firebase
           .Child("/")
           .AsObservable<VlcState>()
           .Subscribe(async ev =>
           {
               Console.WriteLine($"Received state {ev.Object}");
               if (ev.Object != null && ev.EventType == FirebaseEventType.InsertOrUpdate)
                   await ApplySyncState(ev.Object);
           });

        Console.WriteLine("Listening to Firebase changes");
    }

    private static void ResetFirebaseVlcState()
    {
        _ = SendFirebaseState(new() { isPlaying = isPlaying, currentTime = 0 });
    }

    private static async Task SendFirebaseState(VlcState state)
    {
        Console.WriteLine($"Sending state {state}");
        await firebase
            .Child("vlcState")
            .PutAsync(new VlcState
            {
                isPlaying = state.isPlaying,
                currentTime = state.currentTime
            });
    }

    private static async Task<VlcState> GetCurrentVlcState()
    {
        // Called many timess because VLC is an idiot: get_time first returns the changes and not the current time
        string timeStr;
        do
        {
            timeStr = await SendVlcCommand("get_time");
        } while (!int.TryParse(timeStr.Trim(), out int _));

        int.TryParse(timeStr.Trim(), out int currentTime);
        return new() { isPlaying = isPlaying, currentTime = currentTime };
    }

    static async Task<string> SendVlcCommand(string cmd)
    {
        byte[] command = Encoding.UTF8.GetBytes(cmd + "\n");
        await stream.WriteAsync(command, 0, command.Length);

        var buffer = new byte[512];
        int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, bytes);
    }

    static async Task ApplySyncState(VlcState state)
    {
        try
        {
            if (state.isPlaying && !isPlaying)
            {
                Console.WriteLine("Playing");

                await SendVlcCommand("pause"); // toggles play/pause state

                double expectedTime = state.currentTime;
                Console.WriteLine($"Seeking to {expectedTime}");
                await SendVlcCommand($"seek {Math.Floor(expectedTime)}");
                isPlaying = state.isPlaying;
            }
            else if (!state.isPlaying && isPlaying)
            {
                Console.WriteLine("Pausing");
                await SendVlcCommand("pause");
                isPlaying = state.isPlaying;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Sync error: " + ex.Message);
        }
    }

    public class VlcState
    {
        public bool isPlaying { get; set; }
        public double currentTime { get; set; }

        public override string ToString()
        {
            return $"isPlaying: {isPlaying}, currentTime: {currentTime}";
        }
    }
}
