using System.Net;
using System.Net.Sockets;
using EchoGate.Core;

namespace EchoGate.ClientLauncher;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--probe", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("ECHO_GATE_CLIENT_HELPER_OK x86-compatible");
            return 0;
        }

        LaunchOptions options;
        try
        {
            options = LaunchOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.LogPath)) ?? ".");

            File.AppendAllText(options.LogPath, $"helper_started_at={DateTimeOffset.Now:O}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"game={options.GamePath}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"working_directory={options.WorkingDirectory}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"server_host={options.ServerHost}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"observe_ms={options.ObservationTimeoutMilliseconds}{Environment.NewLine}");

            // TEMP diagnostic logging. Remove or redact before public release.
            File.AppendAllText(options.LogPath, $"session={options.SessionId}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"session_length={options.SessionId.Length}{Environment.NewLine}");

            string lobbyHost = ResolveLobbyHost(options.ServerHost);
            File.AppendAllText(options.LogPath, $"resolved_lobby_host={lobbyHost}{Environment.NewLine}");

            GameLaunchToken token = GameLaunchTokenGenerator.Generate(options.SessionId);

            File.AppendAllText(options.LogPath, $"token_tick={token.TickCount}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"token_length={token.Token.Length}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"launch_argument_length={token.LaunchArgument.Length}{Environment.NewLine}");

            ClientLaunchResult launchResult = ClientProcessLauncher.Launch(options, token, lobbyHost);

            File.AppendAllText(options.LogPath, $"process_id={launchResult.ProcessId}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"thread_id={launchResult.ThreadId}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"exited_during_observation={launchResult.ExitedDuringObservation}{Environment.NewLine}");

            if (launchResult.ExitCode is not null)
                File.AppendAllText(options.LogPath, $"exit_code={launchResult.ExitCode}{Environment.NewLine}");

            File.AppendAllText(options.LogPath, $"helper_completed_at={DateTimeOffset.Now:O}{Environment.NewLine}");
            return 0;
        }
        catch (Exception ex)
        {
            File.AppendAllText(options.LogPath, $"helper_error={ex}{Environment.NewLine}");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string ResolveLobbyHost(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
            return parsed.ToString();

        IPAddress? address = Dns.GetHostAddresses(host)
            .FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork);

        return address?.ToString() ?? host;
    }
}

internal sealed record LaunchOptions(
    string GamePath,
    string WorkingDirectory,
    string SessionId,
    string ServerHost,
    string LogPath,
    uint ObservationTimeoutMilliseconds)
{
    private const uint DefaultObservationTimeoutMilliseconds = 5000;

    public static LaunchOptions Parse(string[] args)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < args.Length; index++)
        {
            string key = args[index];

            if (!key.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument: {key}");

            if (index + 1 >= args.Length)
                throw new ArgumentException($"Missing value for {key}");

            values[key[2..]] = args[++index];
        }

        return new LaunchOptions(
            Required(values, "game"),
            Required(values, "working-directory"),
            Required(values, "session"),
            Required(values, "server-host"),
            Required(values, "log"),
            ParseObservationTimeout(values));
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing --{key}");

        return value;
    }

    private static uint ParseObservationTimeout(IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue("observe-seconds", out string? value) || string.IsNullOrWhiteSpace(value))
            return DefaultObservationTimeoutMilliseconds;

        if (!uint.TryParse(value, out uint seconds) || seconds > 300)
            throw new ArgumentException("Invalid --observe-seconds value.");

        return seconds * 1000;
    }
}