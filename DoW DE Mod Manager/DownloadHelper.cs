using IWshRuntimeLibrary;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

namespace DoW_DE_Nod_Manager
{
    static class DownloadHelper
    {
        class UpdateInfo
        {
            public string VersionUrl { get; }
            public string ChangelogUrl { get; }
            public Func<string, Version> ParseVersion { get; }
            public Func<Version, bool> IsNewerThanCurrent { get; }
            public string NoDataMessage { get; }
            public string BadVersionMessagePrefix { get; }
            public Func<DialogResult> ShowUpdatePrompt { get; }

            public UpdateInfo(
                string versionUrl,
                string changelogUrl,
                Func<string, Version> parseVersion,
                Func<Version, bool> isNewerThanCurrent,
                string noDataMessage,
                string badVersionMessagePrefix,
                Func<DialogResult> showUpdatePrompt)
            {
                VersionUrl = versionUrl;
                ChangelogUrl = changelogUrl;
                ParseVersion = parseVersion;
                IsNewerThanCurrent = isNewerThanCurrent;
                NoDataMessage = noDataMessage;
                BadVersionMessagePrefix = badVersionMessagePrefix;
                ShowUpdatePrompt = showUpdatePrompt;
            }
        }

        static readonly string currentDir = Directory.GetCurrentDirectory();
        static string latestStringVersion = "";
        static string latestChangelog = "";
        static bool closeAndDelete;

        static readonly UpdateInfo ExeUpdateInfo = new UpdateInfo(
            versionUrl: "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/refs/heads/DE/DoW%20DE%20Mod%20Manager/LatestStable/version",
            changelogUrl: "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/refs/heads/DE/DoW%20DE%20Mod%20Manager/LatestStable/latestchangelog",
            parseVersion: s => new Version(s),
            isNewerThanCurrent: latest => Assembly.GetExecutingAssembly().GetName().Version < latest,
            noDataMessage: "There is no data in \"version\" file on GitHub!",
            badVersionMessagePrefix: "There is something wrong with version number in \"version\" file on GitHub!\n",
            showUpdatePrompt: () => ThemedDialogueBox.Show(
                $"The new DoW Mod Manager v{latestStringVersion} is available. Do you wish to update now?\n{latestChangelog}",
                "New update available",
                exeORmods: "exe")
        );

        private static readonly UpdateInfo ModlistUpdateInfo = new UpdateInfo(
            versionUrl: "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/refs/heads/DE/DoW%20DE%20Mod%20Manager/ModList/version",
            changelogUrl: "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/refs/heads/DE/DoW%20DE%20Mod%20Manager/ModList/latestchangelog",
            parseVersion: s =>
            {
                // strip dots, parse int, wrap in Version for easy compare
                int numeric = Convert.ToInt32(s.Replace(".", ""));
                return new Version(numeric, 0);
            },
            isNewerThanCurrent: latest =>
            {
                string firstLine = System.IO.File.ReadLines(Path.Combine(currentDir, ModDownloaderForm.MODLIST_FILE)).FirstOrDefault() ?? "";
                int localNumeric = 0;
                if (!string.IsNullOrEmpty(firstLine))
                    localNumeric = Convert.ToInt32(firstLine.Replace(".", ""));
                return new Version(localNumeric, 0) < latest;
            },
            noDataMessage: "There is no data in \"version\" file on GitHub!",
            badVersionMessagePrefix: "There is something wrong with version number in \"version\" file on GitHub!\n",
            showUpdatePrompt: () => ThemedDialogueBox.Show(
                $"The new Modlist v{latestStringVersion} is available. Do you wish to update now?\n{latestChangelog}",
                "New update available",
                exeORmods: "mods")
        );

        /// <summary>
        /// Check for a new EXE version.
        /// </summary>
        public static DialogResult CheckForExeUpdate(bool silently) =>
            CheckForUpdateCore(ExeUpdateInfo, silently);

        /// <summary>
        /// Check for a new ModList version.
        /// </summary>
        public static DialogResult CheckForModlistUpdate(bool silently) =>
            CheckForUpdateCore(ModlistUpdateInfo, silently);

        static DialogResult CheckForUpdateCore(UpdateInfo info, bool silently)
        {
            latestStringVersion = DownloadString(info.VersionUrl);
            latestChangelog = DownloadString(info.ChangelogUrl);

            bool showMessageBox;
            string message = null, title = null;
            DialogResult result = DialogResult.Cancel;

            if (string.IsNullOrEmpty(latestStringVersion))
            {
                showMessageBox = true;
                message = info.NoDataMessage;
                title = "Warning!";
                result = DialogResult.Abort;
                goto SHOW;
            }

            Version latestVersion;
            try
            {
                latestVersion = info.ParseVersion(latestStringVersion);
            }
            catch (Exception ex)
            {
                showMessageBox = true;
                message = info.BadVersionMessagePrefix + ex.Message;
                title = "Warning!";
                result = DialogResult.Abort;
                goto SHOW;
            }

            if (info.IsNewerThanCurrent(latestVersion))
                return info.ShowUpdatePrompt();
            else
            {
                showMessageBox = true;
                message = "You have the latest version!";
                title = "Good news!";
                result = DialogResult.Cancel;
            }

        SHOW:
            if (!silently && showMessageBox)
                ThemedMessageBox.Show(message, title);

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DownloadExe()
        {
            DownloadFile(
                "https://github.com/IgorTheLight/DoW-Mod-Manager/raw/refs/heads/DE/DoW%20DE%20Mod%20Manager/LatestStable/DoW%20Mod%20Manager.exe",
                Path.Combine(currentDir, $"DoW Mod Manager v{latestStringVersion}.exe"),
                closeAndDeleteApplication: true
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DownloadModlist()
        {
            DownloadFile(
                "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/refs/heads/DE/DoW%20DE%20Mod%20Manager/ModList/DoW%20Mod%20Manager%20Download%20Mods.list",
                Path.Combine(currentDir, ModDownloaderForm.MODLIST_FILE),
                closeAndDeleteApplication: false
            );
        }

        public static string DownloadString(string address)
        {
            try
            {
                using (var webClient = new WebClient())
                    return webClient.DownloadString(address);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(ex.Message, "Download Error:");
                return "";
            }
        }

        public static void DownloadFile(string address, string downloadPath, bool closeAndDeleteApplication)
        {
            new Thread(() =>
            {
                using (var webClient = new WebClient())
                {
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                    webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed);

                    try
                    {
                        webClient.DownloadFileAsync(new Uri(address), downloadPath);
                        while (webClient.IsBusy)
                            Application.DoEvents();

                        closeAndDelete = closeAndDeleteApplication;
                    }
                    catch (Exception ex)
                    {
                        ThemedMessageBox.Show(ex.Message, "Download Error:");
                    }
                }
            }).Start();
        }

        static void Completed(object sender, AsyncCompletedEventArgs e)
        {
            if (closeAndDelete)
            {
                ThemedMessageBox.Show("Download completed!\nApplication will restart to take effect", "Good news!");
                CleanupAndStartApp();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CleanupAndStartApp()
        {
            string oldExe = Path.Combine(currentDir, AppDomain.CurrentDomain.FriendlyName);
            string newExe = Path.Combine(currentDir, $"DoW Mod Manager v{latestStringVersion}.exe");

            Process.Start(newExe);
            Process.Start("cmd.exe", "/C choice /C Y /N /D Y /T 1 & Del \"" + oldExe + "\"");
            CreateShortcut($"DoW Mod Manager v{latestStringVersion}", newExe);
            Program.TerminateApp();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CreateShortcut(string shortcutName, string targetPath)
        {
            string shortcutLocation = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                shortcutName + ".lnk");
            var shell = new WshShell();
            var shortcut = (IWshShortcut)shell.CreateShortcut(shortcutLocation);
            shortcut.Description = $"The latest DoW Mod Manager v{latestStringVersion}";
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = currentDir;
            shortcut.Save();
        }
    }
}