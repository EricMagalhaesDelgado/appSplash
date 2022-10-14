using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace appSplash
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Globals.RootFolder = AppDomain.CurrentDomain.BaseDirectory;
            Globals.localApp = JsonSerializer.Deserialize<appIntegrityInfo>(File.ReadAllText(Globals.RootFolder + "\\application\\Settings\\appIntegrity.json"));

            appTitle.Content = Globals.localApp.name;
            appRelease.Content = Globals.localApp.release;

            Loaded += Startup;
        }

        private void Startup(object sender, EventArgs e)
        {
            var appName = "appAnalise";

            // STABLE VERSION
            Globals.stableApp.version = String.Empty;
            Globals.stableApp.release = String.Empty;

            try
            {
                using (WebClient wc = new WebClient())
                {
                    var webVersion = JsonDocument.Parse(wc.DownloadString(File.ReadAllText(Globals.RootFolder + "\\application\\Settings\\PublicLink.json")));
                    var appVersion = JsonDocument.Parse(webVersion.RootElement.GetProperty(appName).ToString());

                    Globals.stableApp.version = appVersion.RootElement.GetProperty("Version").ToString();
                    Globals.stableApp.release = appVersion.RootElement.GetProperty("ReleaseDate").ToString();
                }
            }
            catch (Exception) { }


            // LOCAL VERSION
            if ((Globals.stableApp.version != "") && (Globals.stableApp.version != Globals.localApp.version))
            {
                var msgBox = new MessageWindow(Globals.localApp.name, "Update", String.Format("A versão local do {0} (v. {1}) difere da versão estável (v. {2}), a qual foi disponibilizada no dia {3}.\n\nDeseja instalar a versão estável do app?", appName, Globals.localApp.version, Globals.stableApp.version, Globals.stableApp.release));
                msgBox.ShowDialog();
            }
            else
                Fcn_IntegrityCheck();
        }

        public void Fcn_IntegrityCheck()
        {
            var fPath = String.Format("{0}\\application\\{1}.exe", Globals.RootFolder, Globals.localApp.name);
            var fInfo = new FileInfo(fPath);

            Globals.appHandle.StartInfo.UseShellExecute = false;
            Globals.appHandle.StartInfo.FileName = fPath;

            if ((Globals.localApp.size == fInfo.Length) && (Globals.localApp.hash == Fcn_ComputeHash(fInfo)))
            {
                Globals.tmr1.Tick += new EventHandler(Fcn_StartProcess);
                Globals.tmr1.Interval = new TimeSpan(0, 0, 1);
                Globals.tmr1.Start();

                Globals.tmr2.Tick += new EventHandler(Fcn_CheckProcess);
                Globals.tmr2.Interval = new TimeSpan(0, 0, 1);
                Globals.tmr2.Start();
            }
            else
            {
                Fcn_Renderer("Processo de inicialização não finalizado.", false);

                var msgBox = new MessageWindow(Globals.localApp.name, "Error", String.Format("O arquivo {0} não passou no teste de integridade.", fPath));
                msgBox.ShowDialog();
            }
        }

        private string Fcn_ComputeHash(FileInfo fInfo)
        {
            string hashString = "";

            using (FileStream fStream = fInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SHA256 hashObj = SHA256.Create();
                fStream.Position = 0;
                byte[] hashBytes = hashObj.ComputeHash(fStream);

                hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }

            return hashString;
        }

        private void Fcn_StartProcess(object sender, EventArgs e)
        {
            Fcn_Renderer("Abrindo Matlab Runtime...", true);

            Globals.tmr1.Stop();
            Globals.appHandle.Start();
        }

        private void Fcn_CheckProcess(object sender, EventArgs e)
        {
            var appName = Globals.localApp.name + " " + Globals.localApp.release;

            foreach (Process CurrentProcess in Process.GetProcesses())
            {
                if ((CurrentProcess.ProcessName == "MATLABWindow") && (CurrentProcess.MainWindowTitle == appName) && (CurrentProcess.Responding))
                {
                    var diffTime = CurrentProcess.StartTime - Globals.appHandle.StartTime;
                    if (diffTime.Seconds > 0)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
            }
        }

        public void Fcn_UpdateProcess()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { appStatus.Content = "Tentando atualizar a versão do app..."; }), DispatcherPriority.Render);
            Thread.Sleep(100);

            var zipDir = String.Format(@"{0}\Downloads\{1}_Matlab.zip", Environment.GetEnvironmentVariable("USERPROFILE"), Globals.localApp.name);
            var oldDir = new DirectoryInfo(Globals.RootFolder + "\\application_old");
            var newDir = new DirectoryInfo(Globals.RootFolder + "\\application");
            bool Flag = false;

            try
            {
                using (WebClient wc = new WebClient())
                    wc.DownloadFile(String.Format(@"https://github.com/EricMagalhaesDelgado/{0}/releases/download/{1}/{2}_Matlab.zip", Globals.localApp.name, Globals.stableApp.version, Globals.localApp.name), zipDir);

                if (oldDir.Exists)
                    Directory.Delete(oldDir.FullName, true);
                Directory.Move(newDir.FullName, oldDir.FullName);
                Flag = true;

                ZipFile.ExtractToDirectory(zipDir, newDir.FullName);

                Fcn_IntegrityCheck();
            }
            catch (Exception exc)
            {
                if (Flag)
                {
                    oldDir.Refresh();
                    newDir.Refresh();

                    if (newDir.Exists)
                        Directory.Delete(newDir.FullName, true);
                    Directory.Move(oldDir.FullName, newDir.FullName);
                }

                Fcn_Renderer("Processo de atualização do app não finalizado.", false);

                var msgBox = new MessageWindow(Globals.localApp.name, "Error", String.Format("O processo de atualização do app não foi finalizado, tendo sido indicada a seguinte mensagem de erro:\n{0}...", exc.Message));
                msgBox.ShowDialog();
            }
        }

        private void Fcn_Renderer(string msgText, bool progressBarType)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                appStatus.Content = msgText;

                if (progressBarType)
                    progressBar1.IsIndeterminate = true;
                else
                {
                    progressBar1.Value = 100;
                    progressBar1.IsIndeterminate = false;
                }
            }), DispatcherPriority.Render);
            Thread.Sleep(1);
        }

        public class appIntegrityInfo
        {
            public string name { get; set; }
            public string release { get; set; }
            public string version { get; set; }
            public string hash { get; set; }
            public double size { get; set; }
        }

        public class Globals
        {
            public static string RootFolder = "";
            public static Process appHandle = new Process();
            public static appIntegrityInfo localApp = new appIntegrityInfo();
            public static appIntegrityInfo stableApp = new appIntegrityInfo();
            public static DispatcherTimer tmr1 = new DispatcherTimer();
            public static DispatcherTimer tmr2 = new DispatcherTimer();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Globals.appHandle.Kill();
            }
            catch (Exception) { }
            Application.Current.Shutdown();
        }
    }
}
