using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using SenoraRP_Chatlog_Assistant.Localization;

namespace SenoraRP_Chatlog_Assistant.Controllers
{
    public static class AppController
    {
        public const string AssemblyVersion = "1.0.0";
        public static readonly string Version = $"v{AssemblyVersion}";
        public const bool IsBetaVersion = false;

        public const string ParameterPrefix = "--";
        public static readonly string[] ProcessNames = { "GTA5", "GTA5_Enhanced" };
        public const string ProductHeader = "SenoraRP-Log-Parser";
        public static string ResourceDirectory;
        public static string LogLocation;

        public static readonly string ExecutablePath = Process.GetCurrentProcess().MainModule?.FileName;
        public static readonly string StartupPath = Path.GetDirectoryName(ExecutablePath);
        public static string PreviousLog = string.Empty;

        public static void InitializeServerIp()
        {
            try
            {
                ResourceDirectory = "Not Found";
                LogLocation = $"client_resources\\{@"play.senorarp.com_22005"}\\.storage";

                string directoryPath = Properties.Settings.Default.DirectoryPath;
                if (string.IsNullOrWhiteSpace(directoryPath)) return;

                string[] resourceDirectories = Directory.GetDirectories(directoryPath + @"\client_resources");

                List<string> potentialLogs = new List<string>();
                foreach (string resourceDirectory in resourceDirectories)
                {
                    if (!File.Exists(resourceDirectory + @"\.storage"))
                        continue;

                    string log;
                    using (StreamReader sr = new StreamReader(resourceDirectory + @"\.storage"))
                    {
                        log = sr.ReadToEnd();
                    }

                    potentialLogs.Add(resourceDirectory);
                }

                if (potentialLogs.Count == 0) return;

                // Compare the last write time on all .storage files in the List to find the latest one
                foreach (var file in potentialLogs.Select(log => new FileInfo(log + @"\.storage")))
                {
                    file.Refresh();
                }
                
                while (potentialLogs.Count > 1)
                {
                    potentialLogs.Remove(DateTime.Compare(File.GetLastWriteTimeUtc(potentialLogs[0] + @"\.storage"), File.GetLastWriteTimeUtc(potentialLogs[1] + @"\.storage")) > 0 ? potentialLogs[1] : potentialLogs[0]);
                }

                // Save the directory name that houses the latest .storage file
                int finalSeparator = potentialLogs[0].LastIndexOf(@"\", StringComparison.Ordinal);
                if (finalSeparator == -1) return;

                // Finally, set the log location
                ResourceDirectory = potentialLogs[0].Substring(finalSeparator + 1, potentialLogs[0].Length - finalSeparator - 1);
                LogLocation = $"client_resources\\{ResourceDirectory}\\.storage";
            }
            catch
            {
                // Silent exception
            }
        }

        public static string ParseChatLog(string directoryPath, bool removeTimestamps, bool showError = false)
        {
            try
            {
                // Read the chat log
                string log;
                using (StreamReader sr = new StreamReader(directoryPath + AppController.LogLocation))
                {
                    log = sr.ReadToEnd();
                }

                log = Regex.Match(log, "(?<=chat_log\\\":\\\")(.*?)(?=\\\\n\\\")").Value;

                if (string.IsNullOrWhiteSpace(log))
                    throw new IndexOutOfRangeException();


                log = System.Net.WebUtility.HtmlDecode(log);
                log = log.Replace("\\n", "\n");

                PreviousLog = log;
                if (removeTimestamps)
                    log = Regex.Replace(log, @"\[\d{1,2}:\d{1,2}:\d{1,2}\] ", string.Empty);

                log = Regex.Replace(log, "<[^>]*>", string.Empty);

                return log;
            }
            catch
            {
                if (showError)
                    MessageBox.Show(Strings.ParseError, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);

                return string.Empty;
            }
        }
    }
}
