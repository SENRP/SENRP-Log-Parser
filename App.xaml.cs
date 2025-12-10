using SenoraRP_Chatlog_Assistant.Controllers;
using SenoraRP_Chatlog_Assistant.Properties;
using SenoraRP_Chatlog_Assistant.UI;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SenoraRP_Chatlog_Assistant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private static bool startMinimized;
        private static bool isRestarted;

        /// <summary>
        /// Initializes the "follow system eligibility"
        /// for the app mode and system accent color
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            Settings.Default.Save();

            base.OnStartup(e);
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Get the command line arguments and check
            // if the current session is a restart or
            // a minimized start
            string[] args = Environment.GetCommandLineArgs();
            if (args.Any(arg => arg == $"{AppController.ParameterPrefix}restart"))
                isRestarted = true;

            if (args.Any(arg => arg == $"{AppController.ParameterPrefix}minimized"))
                startMinimized = true;

            // Make sure only one instance is running
            // if the application is not currently restarting
            Mutex mutex = new Mutex(true, "SenoraRPChatLogAssistant", out bool isUnique);
            if (!isUnique && !isRestarted)
            {
                MessageBox.Show(Localization.Strings.OtherInstanceRunning, Localization.Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }

            // Initialize the controllers and
            // display the server picker on the
            // first start, or the main window
            // on subsequent starts
            AppController.InitializeServerIp();
            Settings.Default.Save();

            MainWindow mainWindow = new MainWindow(startMinimized);
            if (!startMinimized)
                mainWindow.Show();

            // Don't let the garbage
            // collector touch the Mutex
            GC.KeepAlive(mutex);
        }

        /// <summary>
        /// Stops the running threads when
        /// quitting the application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            BackupController.Quitting = true;
        }
    }
}
