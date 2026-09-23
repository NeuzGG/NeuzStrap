using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.UI.Pages;

namespace NeuzStrap.UI
{
    /// <summary>
    /// "NeuzStrap.exe -screenshot &lt;folder&gt;" renders every page to PNG (for the README and for checking
    /// the UI without clicking around). Nothing is launched or changed.
    /// </summary>
    public static class ScreenshotTool
    {
        static readonly string[] Pages = { "home", "performance", "booster", "fastflags", "mods", "overlay", "activity", "tools", "settings", "about" };

        /// <summary>Demo screenshots always look the same (e.g. "Play" even if Roblox happens to be open).</summary>
        internal static bool DemoMode { get; private set; }

        public static void Run(string folder, bool demo = false)
        {
            folder = string.IsNullOrEmpty(folder) ? Path.Combine(Paths.Base, "Screenshots") : Path.GetFullPath(folder);
            Directory.CreateDirectory(folder);
            DemoMode = demo;
            if (demo) LoadDemoData();

            // Run inside a real message loop: Application.DoEvents() alone uninstalls the WinForms
            // SynchronizationContext, which would make awaits resume on background threads.
            var context = new ApplicationContext();
            var start = new Timer { Interval = 1 };
            start.Tick += async (_, __) =>
            {
                start.Stop();
                try { await CaptureAllAsync(folder); }
                catch (Exception ex) { Logger.Error("Screenshot", ex); }
                finally { context.ExitThread(); }
            };
            start.Start();
            Application.Run(context);
            start.Dispose();
        }

        static async Task CaptureAllAsync(string folder)
        {
            using (var form = new MainForm())
            {
                PlaceOffscreen(form);
                form.Show();
                await Task.Delay(1500); // let async page loads (PC scan etc.) finish

                foreach (var key in Pages)
                {
                    form.ClientSize = Theme.S(1040, 690);
                    form.Navigate(key);
                    await Task.Delay(key == "home" || key == "tools" ? 3000 : 400);

                    Save(form.Root, Path.Combine(folder, key + ".png"));

                    // a second, full-length capture of the page
                    var page = form.GetPage(key);
                    int full = Theme.S(92) + page.ContentHeight + Theme.S(8);
                    if (full > form.ClientSize.Height)
                    {
                        form.ClientSize = new Size(Theme.S(1040), full);
                        await Task.Delay(300);
                        Save(form.Root, Path.Combine(folder, key + "-full.png"));
                    }
                }
                form.Close();
            }

            using (var boot = BootstrapperForm.CreatePreview())
            {
                PlaceOffscreen(boot);
                boot.Show();
                await Task.Delay(900);
                Save(boot, Path.Combine(folder, "bootstrapper.png"));
                boot.Close();
            }

            using (var installer = new InstallerForm(forceFreshInstall: true))
            {
                PlaceOffscreen(installer);
                installer.Show();
                await Task.Delay(1500);
                Save(installer, Path.Combine(folder, "installer.png"), clientOnly: true);
                installer.Close();
            }

            Logger.Info("Screenshot", "Saved screenshots to " + folder);
        }

        static void PlaceOffscreen(Form f)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-30000, -30000);
            f.ShowInTaskbar = false;
        }

        /// <summary>Sample data for README screenshots, so nobody's real PC or game history ends up in the docs.</summary>
        static void LoadDemoData()
        {
            Boost.SystemInfo.UseDemo();
            var s = Settings.Current;
            Boost.Profiles.Apply(PerformanceProfile.Potato, s, Boost.SystemInfo.Get());
            s.CustomFastFlags["FIntDebugForceMSAASamples"] = "1";
            s.CustomFastFlags["FFlagSomeOldTweak"] = "True";
            s.CursorStyle = CursorStyle.Sakura;
            s.Overlay = true;
            s.OverlayRainbow = true;
            s.OverlayKps = true;
            s.RobloxFpsCounter = true;

            var st = State.Current;
            st.RobloxVersionName = "0.739.0.7390687";
            st.RobloxVersionGuid = "version-4310300497aa4917";
            st.History.Clear();
            var now = DateTime.UtcNow;
            st.History.Add(new GameHistoryEntry { PlaceId = 1, Name = "Mega Obby Tower", Creator = "ObbyMakers", LastPlayedUtc = now.AddMinutes(-25), ServerLocation = "Singapore, SG", TimesPlayed = 12 });
            st.History.Add(new GameHistoryEntry { PlaceId = 2, Name = "Potato Farm Tycoon", Creator = "SpudStudios", LastPlayedUtc = now.AddHours(-5), ServerLocation = "Hong Kong, HK", TimesPlayed = 4 });
            st.History.Add(new GameHistoryEntry { PlaceId = 3, Name = "Island Roleplay", Creator = "SunnyDevs", LastPlayedUtc = now.AddDays(-1).AddHours(-2), ServerLocation = "Tokyo, JP", TimesPlayed = 7 });

            // pages re-read these files, and the screenshot runner is a throwaway portable folder
            Settings.Save();
            State.Save();
        }

        static void Save(Control c, string path, bool clientOnly = false)
        {
            Size size = clientOnly && c is Form f ? f.ClientSize : c.Size;
            using (var bmp = new Bitmap(size.Width, size.Height))
            {
                if (clientOnly && c is Form form)
                {
                    // draw each top-level child where it sits in the client area
                    using (var g = Graphics.FromImage(bmp)) g.Clear(form.BackColor);
                    foreach (Control child in form.Controls)
                    {
                        if (!child.Visible) continue;
                        using (var part = new Bitmap(child.Width, child.Height))
                        {
                            child.DrawToBitmap(part, new Rectangle(0, 0, child.Width, child.Height));
                            using (var g = Graphics.FromImage(bmp)) g.DrawImage(part, child.Left, child.Top);
                        }
                    }
                }
                else
                {
                    c.DrawToBitmap(bmp, new Rectangle(0, 0, size.Width, size.Height));
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
