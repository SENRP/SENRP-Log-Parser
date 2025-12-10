using System.Windows;
using System.Windows.Input;
using SenoraRP_Chatlog_Assistant.Controllers;

namespace SenoraRP_Chatlog_Assistant.UI
{
    /// <summary>
    /// Interaction logic for ProgramSettingsWindow.xaml
    /// </summary>
    public partial class ProgramSettingsWindow
    {
        private readonly MainWindow _mainWindow;

        /// <summary>
        /// Focuses back on this window if
        /// another window from this application
        /// gains focus (workaround for MahApps)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GainFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Focus();
        }

        /// <summary>
        /// Initializes the program settings window
        /// </summary>
        /// <param name="mainWindow"></param>
        public ProgramSettingsWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _mainWindow.GotKeyboardFocus += GainFocus;
            InitializeComponent();

            Left = _mainWindow.Left + (_mainWindow.Width / 2 - Width / 2);
            Top = _mainWindow.Top + (_mainWindow.Height / 2 - Height / 2) + 55;
            Focus();
            LoadSettings();
        }

        /// <summary>
        /// Saves the program settings
        /// </summary>
        private void SaveSettings()
        {   
            Properties.Settings.Default.DisableInformationPopups = DisableInformationPopups.IsChecked == true;
            Properties.Settings.Default.DisableWarningPopups = DisableWarningPopups.IsChecked == true;
            Properties.Settings.Default.DisableErrorPopups = DisableErrorPopups.IsChecked == true;
            Properties.Settings.Default.IgnoreBetaVersions = IgnoreBetaVersions.IsChecked == true;

            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Loads the program settings
        /// </summary>
        private void LoadSettings()
        {

            DisableInformationPopups.IsChecked = Properties.Settings.Default.DisableInformationPopups;
            DisableWarningPopups.IsChecked = Properties.Settings.Default.DisableWarningPopups;
            DisableErrorPopups.IsChecked = Properties.Settings.Default.DisableErrorPopups;
            IgnoreBetaVersions.IsChecked = Properties.Settings.Default.IgnoreBetaVersions;
        }

        /// <summary>
        /// Resets the backup settings
        /// </summary>
        private static void ResetSettings()
        {
            Properties.Settings.Default.UpdateCheckTimeout = 4;

            Properties.Settings.Default.DisableInformationPopups = false;
            Properties.Settings.Default.DisableWarningPopups = false;
            Properties.Settings.Default.DisableErrorPopups = false;
            Properties.Settings.Default.IgnoreBetaVersions = true;

            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Resets and reloads the program settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ResetSettings();
            LoadSettings();
        }
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Saves the settings before the program settings window closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProgramSettings_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            _mainWindow.GotKeyboardFocus -= GainFocus;
        }
    }
}
