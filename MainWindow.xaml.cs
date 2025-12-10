using SenoraRP_Chatlog_Assistant.Localization;
using Microsoft.Win32;
using Octokit;
using SenoraRP_Chatlog_Assistant.Controllers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;
using Path = System.IO.Path;

namespace SenoraRP_Chatlog_Assistant.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private System.Windows.Forms.NotifyIcon _trayIcon; // Change to use System.Windows.Forms.NotifyIcon

        private GitHubClient _client;
        private bool _isUpdateCheckRunning;
        private bool _isUpdateCheckManual;
        private readonly bool _isLoading;

        private static bool isRestarting;
        public MainWindow(bool startMinimized)
        {
            _isLoading = true;
            _client = new GitHubClient(new ProductHeaderValue(AppController.ProductHeader));
            _client.SetRequestTimeout(new TimeSpan(0, 0, 0, Properties.Settings.Default.UpdateCheckTimeout));
            StartupController.InitializeShortcut();

            InitializeComponent();
            InitializeTrayIcon();

            if (startMinimized)
                _trayIcon.Visible = true;

            // Also checks for the RAGEMP directory on the first start
            LoadSettings();

            BackupController.Initialize();
            _isLoading = false;
        }

        /// <summary>
        /// Saves the main settings
        /// </summary>
        private void SaveSettings()
        {
            Properties.Settings.Default.DirectoryPath = DirectoryPath.Text;
            Properties.Settings.Default.RemoveTimestamps = RemoveTimestamps.IsChecked == true;
            Properties.Settings.Default.CheckForUpdatesAutomatically = CheckForUpdatesOnStartup.IsChecked == true;

            Properties.Settings.Default.Save();
            AppController.InitializeServerIp();
        }

        /// <summary>
        /// Loads the main settings
        /// </summary>
        private void LoadSettings()
        {

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable once UnreachableCode
#pragma warning disable 162
            Version.Text = string.Format(Strings.VersionInfo, AppController.Version, string.Empty);
#pragma warning restore 162
            StatusLabel.Content = string.Format(Strings.BackupStatus, Properties.Settings.Default.BackupChatLogAutomatically ? Strings.Enabled : Strings.Disabled);
            Counter.Text = string.Format(Strings.CharacterCounter, 0, 0);

            RemoveTimestamps.IsChecked = Properties.Settings.Default.RemoveTimestamps;
            CheckForUpdatesOnStartup.IsChecked = Properties.Settings.Default.CheckForUpdatesAutomatically;

            if (Properties.Settings.Default.FirstStart)
            {
                Properties.Settings.Default.FirstStart = false;
                Properties.Settings.Default.Save();

                LookForMainDirectory();
                SaveSettings();
            }
            else
                DirectoryPath.Text = Properties.Settings.Default.DirectoryPath;
        }

        /// <summary>
        /// Looks for the main RAGEMP directory
        /// path on the first start
        /// </summary>
        private void LookForMainDirectory()
        {
            try
            {
                var keyValue = Registry.GetValue(@"HKEY_CURRENT_USER\Software\RAGE-MP", "rage_path", null);
                if (keyValue != null)
                {
                    DirectoryPath.Text = keyValue + @"\";
                    MessageBox.Show(string.Format(Strings.DirectoryFinder, DirectoryPath.Text), Strings.DirectoryFinderTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    throw new IOException();
                }
            }
            catch
            {
                MessageBox.Show(Strings.DirectoryFinderNotFound, Strings.DirectoryFinderTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Saves the settings when the
        /// value of the text box changes
        /// and disables automatic backup
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DirectoryPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading)
                return;

            if (Properties.Settings.Default.BackupChatLogAutomatically)
            {
                SettingsWindow.ResetSettings();

                StatusLabel.Content = string.Format(Strings.BackupStatus, Strings.Disabled);
                MessageBox.Show(Strings.BackupTurnedOff, Strings.Information, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            SaveSettings();
        }

        /// <summary>
        /// Opens the directory picker
        /// when the text box is clicked on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DirectoryPath_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DirectoryPath.Text))
                Browse_Click(this, null);
        }

        /// <summary>
        /// Displays a directory picker until
        /// a non-root directory is selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog directoryBrowserDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = @"RAGEMP Directory Path",
                RootFolder = Environment.SpecialFolder.MyComputer,
                SelectedPath = string.IsNullOrWhiteSpace(DirectoryPath.Text) || !Directory.Exists(DirectoryPath.Text) ? Path.GetPathRoot(Environment.SystemDirectory) : DirectoryPath.Text,
                ShowNewFolderButton = false
            };

            bool validLocation = false;
            while (!validLocation)
            {
                if (directoryBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (directoryBrowserDialog.SelectedPath[directoryBrowserDialog.SelectedPath.Length - 1] != '\\')
                    {
                        DirectoryPath.Text = directoryBrowserDialog.SelectedPath + "\\";
                        validLocation = true;
                    }
                    else
                        MessageBox.Show(Strings.BadDirectoryPath, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                    validLocation = true;
            }
        }

        /// <summary>
        /// Parses the current chat log and sets
        /// the text of the main text box to it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Parse_Click(object sender, RoutedEventArgs e)
        {
            // The paths may have changed since the program has
            // started, we need to initialize the locations again
            AppController.InitializeServerIp();

            if (string.IsNullOrWhiteSpace(DirectoryPath.Text) || !Directory.Exists(DirectoryPath.Text + "client_resources\\"))
            {
                MessageBox.Show(Strings.InvalidDirectoryPath, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!File.Exists(DirectoryPath.Text + AppController.LogLocation))
            {
                MessageBox.Show(Strings.NoChatLog, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Parsed.Text = AppController.ParseChatLog(DirectoryPath.Text, RemoveTimestamps.IsChecked == true, true);
        }

        /// <summary>
        /// Displays a save file dialog to save the
        /// contents of the main text box to the disk
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Parsed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Counter == null)
                return;

            if (string.IsNullOrWhiteSpace(Parsed.Text))
            {
                Counter.Text = string.Format(Strings.CharacterCounter, 0, 0);
                return;
            }

            Counter.Text = string.Format(Strings.CharacterCounter, Parsed.Text.Length, Parsed.Text.Split('\n').Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveParsed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Parsed.Text))
                {
                    if (!Properties.Settings.Default.DisableErrorPopups)
                        MessageBox.Show(Strings.NothingParsed, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "chatlog.txt",
                    Filter = "Text File | *.txt"
                };

                if (dialog.ShowDialog() != true) return;
                using (StreamWriter sw = new StreamWriter(dialog.OpenFile()))
                {
                    sw.Write(Parsed.Text.Replace("\n", Environment.NewLine));
                }
            }
            catch
            {
                MessageBox.Show(Strings.SaveError, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Copies the contents of the
        /// main text box to the clipboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CopyParsedToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Parsed.Text) && !Properties.Settings.Default.DisableErrorPopups)
                MessageBox.Show(Strings.NothingParsed, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            else
                Clipboard.SetText(Parsed.Text.Replace("\n", Environment.NewLine));
        }

        /// <summary>
        /// Toggles the "Check For Updates On Startup" option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckForUpdatesOnStartup_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (CheckForUpdatesOnStartup.IsChecked == true)
                TryCheckingForUpdates();
        }

        /// <summary>
        /// Removes the timestamps from the parsed chat log
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveTimestamps_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Parsed.Text) || string.IsNullOrWhiteSpace(DirectoryPath.Text) || !Directory.Exists(DirectoryPath.Text + "client_resources\\") || !File.Exists(DirectoryPath.Text + AppController.LogLocation))
                return;

            if (RemoveTimestamps.IsChecked == true)
            {
                AppController.PreviousLog = Parsed.Text;
                Parsed.Text = Regex.Replace(AppController.PreviousLog, @"\[\d{1,2}:\d{1,2}:\d{1,2}\] ", string.Empty);
                Parsed.Text = Regex.Replace(Parsed.Text, "<[^>]*>", string.Empty);
            }
            else if (!string.IsNullOrWhiteSpace(AppController.PreviousLog))
            {
                Parsed.Text = Regex.Replace(AppController.PreviousLog, "<[^>]*>", string.Empty);
            }
        }

        /// <summary>
        /// Toggles the controls on the main window
        /// </summary>
        /// <param name="enable"></param>
        private void ToggleControls(bool enable = false)
        {
            Dispatcher?.Invoke(() =>
            {

                Parse.IsEnabled = enable;
                SaveParsed.IsEnabled = enable;
                CopyParsedToClipboard.IsEnabled = enable;
                DirectoryPath.IsEnabled = enable;
                Browse.IsEnabled = enable;
                Parsed.IsEnabled = enable;
                CheckForUpdatesOnStartup.IsEnabled = enable;
                RemoveTimestamps.IsEnabled = enable;
            });
        }

        /// <summary>
        /// Tries checking for updates
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckForUpdatesToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TryCheckingForUpdates(true);
        }

        /// <summary>
        /// Disables the controls on the main window
        /// and checks for updates
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private void TryCheckingForUpdates(bool manual = false)
        {
            if (!_isUpdateCheckRunning)
            {
                _isUpdateCheckRunning = true;
                _resetEvent.Reset();


                if (manual)
                    ToggleControls();

                _isUpdateCheckManual = manual;
                ThreadPool.QueueUserWorkItem(_ => CheckForUpdates(ref _isUpdateCheckManual));
                ThreadPool.QueueUserWorkItem(_ => FinishUpdateCheck());
            }
            else if (manual && !_isUpdateCheckManual)
            {
                _isUpdateCheckManual = true;
                ToggleControls();
            }
        }

        /// <summary>
        /// Enables the controls on the main window
        /// and disables the progress ring
        /// </summary>
        private void FinishUpdateCheck()
        {
            _resetEvent.WaitOne();

            ToggleControls(true);

            _isUpdateCheckRunning = false;
        }

        /// <summary>
        /// Displays a message box
        /// on the main UI thread
        /// </summary>
        /// <param name="text"></param>
        /// <param name="title"></param>
        /// <param name="buttons"></param>
        /// <param name="image"></param>
        private void DisplayUpdateMessage(string text, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            ToggleControls(true);

            Dispatcher?.Invoke(() =>
            {
                if (MessageBox.Show(text, title, buttons, image) == MessageBoxResult.Yes)
                    Process.Start(Strings.ReleasesLink);
            });
        }

        /// <summary>
        /// Checks for updates
        /// </summary>
        /// <param name="manual"></param>
#pragma warning disable 162
        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        [SuppressMessage("ReSharper", "UnreachableCode")]
        private void CheckForUpdates(ref bool manual)
        {
            try
            {
                string installedVersion = AppController.Version;
                IReadOnlyList<Release> releases = _client.Repository.Release.GetAll("SENRP", AppController.ProductHeader).Result;

                string newVersion = string.Empty;
                bool isNewVersionBeta = false;

                // Prereleases are a go
                if (!Properties.Settings.Default.IgnoreBetaVersions)
                {
                    newVersion = releases[0].TagName;
                    isNewVersionBeta = releases[0].Prerelease;
                }
                else
                {
                    // If the user does not want to
                    // look for prereleases during
                    // the update check, ignore them
                    foreach (Release release in releases)
                    {
                        if (release.Prerelease)
                            continue;

                        newVersion = release.TagName;
                        isNewVersionBeta = release.Prerelease;
                        break;
                    }
                }

                if (AppController.IsBetaVersion && !isNewVersionBeta && string.CompareOrdinal(installedVersion, newVersion) == 0 || string.CompareOrdinal(installedVersion, newVersion) < 0)
                { // Update available
                    if (Visibility != Visibility.Visible)
                        ResumeTrayStripMenuItem_Click(this, EventArgs.Empty);

                    DisplayUpdateMessage(string.Format(Strings.UpdateAvailable, installedVersion + (AppController.IsBetaVersion ? " Beta" : string.Empty), newVersion + (isNewVersionBeta ? " Beta" : string.Empty)), Strings.UpdateAvailableTitle, MessageBoxButton.YesNo, MessageBoxImage.Information);
                }
                else if (manual) // Latest version
                    DisplayUpdateMessage(string.Format(Strings.RunningLatest, installedVersion + (AppController.IsBetaVersion ? " Beta" : string.Empty)), Strings.Information, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch // No internet
            {
                if (manual)
                    DisplayUpdateMessage(string.Format(Strings.NoInternet, AppController.Version + (AppController.IsBetaVersion ? " Beta" : string.Empty)), Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _resetEvent.Set();
        }
#pragma warning restore 162

        /// <summary>
        /// Opens the backup settings window
        /// </summary>
        private static SettingsWindow backupSettings;
        private void BackupSettingsToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DirectoryPath.Text) || !Directory.Exists(DirectoryPath.Text + "client_resources\\"))
            {
                MessageBox.Show(Strings.InvalidDirectoryPathBackup, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Properties.Settings.Default.BackupChatLogAutomatically)
            {
                if (!Properties.Settings.Default.DisableWarningPopups && MessageBox.Show(Strings.BackupWillBeOff, Strings.Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;

                StatusLabel.Content = string.Format(Strings.BackupStatus, Strings.Disabled);
            }
            else
                if (!Properties.Settings.Default.DisableInformationPopups)
                MessageBox.Show(Strings.SettingsAfterClose, Strings.Information, MessageBoxButton.OK, MessageBoxImage.Information);

            BackupController.AbortAll();
            SaveSettings();

            if (backupSettings == null)
            {
                backupSettings = new SettingsWindow(this);
                backupSettings.IsVisibleChanged += (s, args) =>
                {
                    if ((bool)args.NewValue) return;
                    BackupController.Initialize();
                    StatusLabel.Content = string.Format(Strings.BackupStatus,
                        Properties.Settings.Default.BackupChatLogAutomatically ? Strings.Enabled : Strings.Disabled);
                };
                backupSettings.Closed += (s, args) =>
                {
                    backupSettings = null;
                };
            }

            backupSettings.ShowDialog();
        }

        /// <summary>
        /// Asks the user if they are sure they want to exit
        /// if automatic backup is enabled.
        /// Saves the settings before the main window closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Main_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isRestarting)
            {
                if (Properties.Settings.Default.BackupChatLogAutomatically && _trayIcon.Visible == false)
                {
                    MessageBoxResult result = MessageBoxResult.Yes;
                    if (!Properties.Settings.Default.AlwaysCloseToTray)
                        result = MessageBox.Show(Strings.MinimizeInsteadOfClose, Strings.Warning, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    // ReSharper disable once ConvertIfStatementToSwitchStatement
                    if (result == MessageBoxResult.Yes)
                    {
                        e.Cancel = true;

                        Hide();
                        _trayIcon.Visible = true;

                        return;
                    }

                    if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            BackupController.Quitting = true;
            SaveSettings();

            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Resumes and shows the main window by double clicking the tray icon
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TrayIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ResumeTrayStripMenuItem_Click(sender, EventArgs.Empty);
        }

        /// <summary>
        /// Resumes and shows the main window from the tray menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResumeTrayStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isRestarting)
                return;

            Show();
            _trayIcon.Visible = false;

            if (CheckForUpdatesOnStartup.IsChecked == true)
                TryCheckingForUpdates();
        }

        /// <summary>
        /// Quits the application from the tray
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExitTrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BackupController.Quitting = true;

            _trayIcon.Visible = false;
            isRestarting = true;
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Initializes the tray icon
        /// </summary>
        private void InitializeTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Visible = false,
                Icon = Properties.Resources.icon,
                Text = @"SenoraRP Chatlog Assistant"
            };

            _trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;

            _trayIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _trayIcon.ContextMenuStrip.Items.Add(@"Open", null, ResumeTrayStripMenuItem_Click);
            _trayIcon.ContextMenuStrip.Items.Add(@"Exit", null, ExitTrayToolStripMenuItem_Click);
        }
        // Can execute
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        // Minimize
        private void CommandBinding_Executed_Minimize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        // Close
        private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }
    }
}
