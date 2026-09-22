using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace NeuzStrap.Setup
{
    /// <summary>Creates .lnk shortcuts through the Windows shell (IShellLink), no extra DLLs.</summary>
    public static class Shortcut
    {
        public static void Create(string lnkPath, string target, string arguments, string description, string iconPath = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnkPath));
            var link = (IShellLinkW)new ShellLink();
            try
            {
                link.SetPath(target);
                link.SetArguments(arguments ?? "");
                link.SetWorkingDirectory(Path.GetDirectoryName(target));
                link.SetDescription(description ?? "");
                link.SetIconLocation(iconPath ?? target, 0);
                ((IPersistFile)link).Save(lnkPath, false);
            }
            finally
            {
                Marshal.ReleaseComObject(link);
            }
        }

        /// <summary>Reads a shortcut's target and arguments (null if it can't be read).</summary>
        public static (string Target, string Arguments)? Read(string lnkPath)
        {
            var link = (IShellLinkW)new ShellLink();
            try
            {
                ((IPersistFile)link).Load(lnkPath, 0 /* STGM_READ */);
                var target = new StringBuilder(1024);
                link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
                var args = new StringBuilder(2048);
                link.GetArguments(args, args.Capacity);
                return (target.ToString(), args.ToString());
            }
            catch
            {
                return null;
            }
            finally
            {
                Marshal.ReleaseComObject(link);
            }
        }

        public static void Delete(string lnkPath)
        {
            try { if (File.Exists(lnkPath)) File.Delete(lnkPath); } catch { }
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class ShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}
