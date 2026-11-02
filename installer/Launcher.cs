using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

// Starts Sekiro through me3 and keeps the game's PlayerNoEmblemsConsume debug byte set.
// This is the fallback for the upstream prebuilt co-op DLL; the fork's own DLL already
// sets the byte itself, so the launcher is harmless in both cases.
internal static class SekiroCoopLauncher
{
    private const long DebugFlagsRva = 0x3D7A366;
    private const long NoEmblemsOffset = 0x4;

    private const int ProcessVmOperation = 0x0008;
    private const int ProcessVmWrite = 0x0020;
    private const int ProcessVmRead = 0x0010;
    private const int ProcessQueryInformation = 0x0400;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, int size, out IntPtr written);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [STAThread]
    private static int Main(string[] args)
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string logPath = Path.Combine(dir, "launcher.log");
        try
        {
            bool unlimited = ReadUnlimited(Path.Combine(dir, "sekiro_coop_settings.ini"));
            string me3 = ReadMe3Path(dir);
            if (me3 == null)
            {
                me3 = FindMe3();
            }
            if (me3 == null)
            {
                Log(logPath, "me3.exe not found. Install me3: https://github.com/garyttierney/me3/releases");
                return 1;
            }

            string profile = Path.Combine(dir, "sekiro-coop.me3");
            var start = new ProcessStartInfo(me3, "launch --game sekiro --profile \"" + profile + "\"");
            start.WorkingDirectory = dir;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            // The GNU-built co-op DLL needs libstdc++/libgcc/libwinpthread from this folder.
            // me3's LoadLibraryExW does not search the DLL's own directory, so put it on PATH
            // for me3 and the game process it starts.
            string oldPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            start.EnvironmentVariables["PATH"] = dir.TrimEnd('\\') + ";" + oldPath;
            Process.Start(start);
            Log(logPath, "started me3: " + me3);

            if (!unlimited)
            {
                return 0;
            }
            return WatchAndPatch(logPath);
        }
        catch (Exception error)
        {
            Log(logPath, "error: " + error);
            return 1;
        }
    }

    private static int WatchAndPatch(string logPath)
    {
        int waitedSeconds = 0;
        while (true)
        {
            Process game = FindGame();
            if (game == null)
            {
                if (waitedSeconds >= 300)
                {
                    Log(logPath, "sekiro.exe did not start within 5 minutes");
                    return 1;
                }
                Thread.Sleep(1000);
                waitedSeconds++;
                continue;
            }

            using (game)
            {
                Log(logPath, "sekiro found, pid " + game.Id);
                PatchWhileRunning(game, logPath);
            }
            return 0;
        }
    }

    private static void PatchWhileRunning(Process game, string logPath)
    {
        IntPtr baseAddress;
        try
        {
            baseAddress = game.MainModule.BaseAddress;
        }
        catch (Exception error)
        {
            Log(logPath, "cannot read sekiro.exe base address: " + error.Message);
            return;
        }

        IntPtr handle = OpenProcess(
            ProcessVmOperation | ProcessVmWrite | ProcessVmRead | ProcessQueryInformation,
            false,
            game.Id);
        if (handle == IntPtr.Zero)
        {
            Log(logPath, "OpenProcess failed, error " + Marshal.GetLastWin32Error());
            return;
        }

        try
        {
            byte[] one = new byte[] { 1 };
            IntPtr address = new IntPtr(baseAddress.ToInt64() + DebugFlagsRva + NoEmblemsOffset);
            int lastErrorLog = 0;
            while (!game.HasExited)
            {
                IntPtr written;
                bool ok = WriteProcessMemory(handle, address, one, 1, out written);
                if (!ok)
                {
                    int now = Environment.TickCount;
                    if (now - lastErrorLog > 30000)
                    {
                        Log(logPath, "WriteProcessMemory failed, error " + Marshal.GetLastWin32Error());
                        lastErrorLog = now;
                    }
                }
                Thread.Sleep(1000);
            }
            Log(logPath, "sekiro exited");
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static Process FindGame()
    {
        Process[] processes = Process.GetProcessesByName("sekiro");
        if (processes.Length == 0)
        {
            return null;
        }
        Process found = processes[0];
        for (int i = 1; i < processes.Length; i++)
        {
            processes[i].Dispose();
        }
        return found;
    }

    private static bool ReadUnlimited(string iniPath)
    {
        try
        {
            if (!File.Exists(iniPath))
            {
                return true;
            }
            foreach (string line in File.ReadAllLines(iniPath))
            {
                Match match = Regex.Match(
                    line,
                    @"^\s*unlimited_spirit_emblems\s*=\s*(\S+)",
                    RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    continue;
                }
                string value = match.Groups[1].Value.Trim().ToLowerInvariant();
                return value == "1" || value == "true" || value == "yes" || value == "on";
            }
        }
        catch
        {
            // An unreadable file means the default (on).
        }
        return true;
    }

    private static string ReadMe3Path(string dir)
    {
        try
        {
            string pathFile = Path.Combine(dir, "me3_path.txt");
            if (File.Exists(pathFile))
            {
                string path = File.ReadAllText(pathFile).Trim().Trim('"');
                if (path.Length > 0 && File.Exists(path))
                {
                    return path;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static string FindMe3()
    {
        string[] candidates = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\garyttierney\me3\bin\me3.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\me3\bin\me3.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"me3\bin\me3.exe")
        };
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        try
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string part in path.Split(';'))
            {
                if (part.Trim().Length == 0)
                {
                    continue;
                }
                string candidate = Path.Combine(part.Trim(), "me3.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static void Log(string path, string message)
    {
        try
        {
            File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine);
        }
        catch
        {
        }
    }
}
