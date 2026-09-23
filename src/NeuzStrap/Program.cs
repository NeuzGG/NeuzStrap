using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.Launch;
using NeuzStrap.Roblox;
using NeuzStrap.Setup;
using NeuzStrap.UI;

namespace NeuzStrap
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Paths.Init();
            var cmd = CommandLine.Parse(args);
            Logger.Init(cmd.ToString());

            try
            {
                Http.Init();
                Settings.Load();
                State.Load();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (_, e) => ReportCrash(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash(e.ExceptionObject as Exception);

                Theme.Init(Settings.Current.Accent);
                AppInstaller.CleanupOldExe();
                if (cmd.JustUpdated) AppInstaller.RefreshUninstallEntry();
                else AppInstaller.EnsureUninstallEntryCurrent();

                return Run(cmd);
            }
            catch (Exception ex)
            {
                ReportCrash(ex);
                return 1;
            }
        }

        static int Run(CommandLine cmd)
        {
            switch (cmd.Action)
            {
                case CommandAction.Uninstall:
                    return Uninstall(cmd.Quiet);

                case CommandAction.Screenshot:
                    ScreenshotTool.Run(cmd.Argument, cmd.Demo);
                    return 0;

                case CommandAction.Play:
                    Application.Run(new BootstrapperForm(LaunchMode.Play, cmd.Argument));
                    return 0;

                case CommandAction.Update:
                    Application.Run(new BootstrapperForm(LaunchMode.UpdateOnly, null));
                    return 0;

                case CommandAction.Repair:
                    Application.Run(new BootstrapperForm(LaunchMode.Repair, null));
                    return 0;

                case CommandAction.Install:
                    Application.Run(new InstallerForm());
                    return 0;

                default:
                    // A downloaded copy that isn't the installed one: offer to install / update.
                    if (!Paths.IsPortable && (!AppInstaller.IsInstalled || !Paths.IsRunningInstalledCopy))
                    {
                        Application.Run(new InstallerForm());
                        return 0;
                    }
                    return OpenMainWindow(cmd);
            }
        }

        static int OpenMainWindow(CommandLine cmd)
        {
            using (var single = new Mutex(true, @"Local\NeuzStrap_MainWindow", out bool first))
            {
                if (!first)
                {
                    BringExistingWindowToFront();
                    return 0;
                }

                var form = new MainForm(cmd.Page ?? "home");
                if (cmd.Welcome)
                    form.Shown += (_, __) => Toast.Show("NeuzStrap is installed!", "Press Play whenever you're ready. Roblox downloads on the first launch.", null, 8);
                else if (cmd.JustUpdated)
                    form.Shown += (_, __) => Toast.Show("NeuzStrap updated", $"You're on v{AppInfo.VersionString} now.", null, 5);
                Application.Run(form);
                return 0;
            }
        }

        static void BringExistingWindowToFront()
        {
            int self = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
            {
                using (p)
                {
                    if (p.Id == self || p.MainWindowHandle == IntPtr.Zero) continue;
                    if (!p.MainWindowTitle.Equals(AppInfo.Name, StringComparison.Ordinal)) continue;
                    Native.ShowWindow(p.MainWindowHandle, 9 /* SW_RESTORE */);
                    Native.SetForegroundWindow(p.MainWindowHandle);
                    return;
                }
            }
        }

        static int Uninstall(bool quiet)
        {
            if (!quiet && !Dialog.Confirm(null, "Uninstall NeuzStrap?",
                    "This removes NeuzStrap, its copy of Roblox and its settings. The website's Play button goes back to the official Roblox launcher " +
                    "(if it's installed). Your Roblox account and games aren't affected.", "Uninstall", danger: true))
                return 0;

            if (RobloxProcess.IsPlayerRunning())
            {
                if (!quiet) Dialog.Info(null, "Close Roblox first", "Roblox is still running. Close it, then uninstall again.");
                return 1;
            }

            try
            {
                AppInstaller.Uninstall();
            }
            catch (Exception ex)
            {
                Logger.Error("Uninstall", ex);
                if (!quiet) Dialog.Error(null, "Uninstall ran into a problem", ex.Message);
                return 1;
            }

            if (!quiet)
            {
                bool official = ProtocolHandler.FindOfficialPlayer() != null;
                Dialog.Success(null, "NeuzStrap is uninstalled", official
                    ? "Roblox links now open the official Roblox launcher again. Thanks for trying NeuzStrap!"
                    : "Thanks for trying NeuzStrap! To play again, download Roblox from roblox.com.");
            }
            return 0;
        }

        static int _crashShown;

        static void ReportCrash(Exception ex)
        {
            if (ex == null) return;
            Logger.Error("Crash", ex);
            if (Interlocked.Exchange(ref _crashShown, 1) == 1) return;
            try
            {
                MessageBox.Show(
                    "NeuzStrap hit an unexpected error:\n\n" + ex.Message +
                    "\n\nA log was saved to:\n" + (Logger.CurrentFile ?? Paths.Logs),
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }

    public enum CommandAction { Default, Play, Update, Repair, Settings, Install, Uninstall, Screenshot }

    /// <summary>
    /// NeuzStrap.exe [-player [uri]] [-update] [-repair] [-settings [-page name]] [-install] [-uninstall [-quiet]]
    /// A bare roblox-player: / roblox: link also works (that's how Windows calls protocol handlers).
    /// </summary>
    public sealed class CommandLine
    {
        public CommandAction Action { get; private set; } = CommandAction.Default;
        public string Argument { get; private set; }
        public string Page { get; private set; }
        public bool Quiet { get; private set; }
        public bool Welcome { get; private set; }
        public bool JustUpdated { get; private set; }
        public bool Demo { get; private set; }

        public static CommandLine Parse(string[] args)
        {
            var c = new CommandLine();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string next = i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal) ? args[i + 1] : null;

                if (IsRobloxUri(a)) { c.Action = CommandAction.Play; c.Argument = a; continue; }

                switch (a.ToLowerInvariant())
                {
                    case "-player":
                    case "--player":
                        c.Action = CommandAction.Play;
                        if (next != null) { c.Argument = next; i++; }
                        break;
                    case "-update": c.Action = CommandAction.Update; break;
                    case "-repair": c.Action = CommandAction.Repair; break;
                    case "-settings":
                    case "-menu": c.Action = CommandAction.Settings; break;
                    case "-page": if (next != null) { c.Page = next.ToLowerInvariant(); i++; } break;
                    case "-install": c.Action = CommandAction.Install; break;
                    case "-uninstall": c.Action = CommandAction.Uninstall; break;
                    case "-quiet": c.Quiet = true; break;
                    case "-welcome": c.Welcome = true; break;
                    case "-updated": c.JustUpdated = true; break;
                    case "-demo": c.Demo = true; break;
                    case "-screenshot":
                        c.Action = CommandAction.Screenshot;
                        if (next != null) { c.Argument = next; i++; }
                        break;
                }
            }

            // "-player" with an empty or bogus argument just opens the Roblox app
            if (c.Action == CommandAction.Play && c.Argument != null && !IsRobloxUri(c.Argument))
                c.Argument = null;
            return c;
        }

        static bool IsRobloxUri(string s) =>
            s.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase) || s.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase);

        public override string ToString()
        {
            string arg = Argument == null ? "" : " " + RobloxProcess.Redact(Argument);
            return Action + arg + (Page != null ? " page=" + Page : "");
        }
    }
}
