using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.RightsManagement;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
            Globals.localApp = JsonSerializer.Deserialize<appIntegrity>(File.ReadAllText(Globals.RootFolder + "\\application\\config\\appIntegrity.json"));

            appTitle.Content = Globals.localApp.appName;
            appRelease.Content = Globals.localApp.appRelease;

            Loaded += Startup;
        }

        private void Startup(object sender, EventArgs e)
        {
            // STABLE VERSION
            Globals.stableApp.appVersion = String.Empty;
            Globals.stableApp.appRelease = String.Empty;

            try
            {
                using (WebClient wc = new WebClient())
                {
                    var publicLinksObj   = JsonDocument.Parse(File.ReadAllText(Globals.RootFolder + "\\application\\config\\PublicLinks.json"));
                    var versionFileObj   = JsonDocument.Parse(wc.DownloadString(publicLinksObj.RootElement.GetProperty("VersionFile").ToString()));
                    var stableVersionObj = JsonDocument.Parse(versionFileObj.RootElement.GetProperty(Globals.localApp.appName).ToString());

                    Globals.stableApp.appVersion = stableVersionObj.RootElement.GetProperty("Version").ToString();
                    Globals.stableApp.appRelease = stableVersionObj.RootElement.GetProperty("ReleaseDate").ToString();
                }
            }
            catch (Exception) { }


            // LOCAL VERSION
            if ((Globals.stableApp.appVersion != "") && (Globals.stableApp.appVersion != Globals.localApp.appVersion))
            {
                var msgBox = new MessageWindow(this, Globals.localApp.appName, "Update", String.Format("A versão local do {0} (v. {1}) difere da versão estável (v. {2}), a qual foi disponibilizada no dia {3}.\n\nDeseja instalar a versão estável do app?", Globals.localApp.appName, Globals.localApp.appVersion, Globals.stableApp.appVersion, Globals.stableApp.appRelease));
                msgBox.ShowDialog();
            }
            else
                Fcn_IntegrityCheck();
        }

        public void Fcn_IntegrityCheck()
        {
            var fPath = String.Format("{0}\\application\\{1}", Globals.RootFolder, Globals.localApp.fileName);
            var fInfo = new FileInfo(fPath);

            Globals.appHandle.StartInfo.UseShellExecute = false;
            Globals.appHandle.StartInfo.FileName = fPath;

            if ((Globals.localApp.fileSize == fInfo.Length) && (Globals.localApp.fileHash == Fcn_ComputeHash(fInfo)))
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

                var msgBox = new MessageWindow(this, Globals.localApp.appName, "Error", String.Format("O arquivo {0} não passou no teste de integridade.", fPath));
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
            var appName = Globals.localApp.appName;

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

            var zipDir = String.Format(@"{0}\Downloads\{1}_Matlab.zip", Environment.GetEnvironmentVariable("USERPROFILE"), Globals.localApp.appName);
            var oldDir = new DirectoryInfo(Globals.RootFolder + "\\application_old");
            var newDir = new DirectoryInfo(Globals.RootFolder + "\\application");
            bool Flag = false;

            // Tentativa de apagar o diretório antigo, caso existente.
            try
            {
                if (oldDir.Exists)
                    Directory.Delete(oldDir.FullName, true);
            }
            catch (Exception exc) 
            {
                var msgBox = new MessageWindow(this, Globals.localApp.fileName, "Error", String.Format("O diretório {0} precisará ser apagado manualmente, uma vez que o Windows retornou o seguinte erro:\n{1}", oldDir.FullName, exc.Message));
                msgBox.ShowDialog();

                return;
            }

            try
            {
                using (WebClient wc = new WebClient())
                    wc.DownloadFile(String.Format(@"{0}/releases/download/{1}/{2}_Matlab.zip", Globals.localApp.codeRepo, Globals.stableApp.appVersion, Globals.localApp.appName), zipDir);

                Directory.Move(newDir.FullName, oldDir.FullName);
                Flag = true;

                ZipFile.ExtractToDirectory(zipDir, newDir.FullName);
                Directory.CreateDirectory(Path.Combine(newDir.FullName, "config", "Default"));

                foreach (string customFile in Globals.localApp.customFiles)
                {
                    string oldDirFullPath = Path.Combine(oldDir.FullName, "config", customFile);
                    string newDirFullPath = Path.Combine(newDir.FullName, "config", customFile);
                    string defaultDirFullPath = Path.Combine(newDir.FullName, "config", "Default", customFile);

                    File.Move(newDirFullPath, defaultDirFullPath);
                    File.Copy(oldDirFullPath, newDirFullPath, true);
                }

                Globals.localApp = JsonSerializer.Deserialize<appIntegrity>(File.ReadAllText(Globals.RootFolder + "\\application\\config\\appIntegrity.json"));
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

                var msgBox = new MessageWindow(this, Globals.localApp.fileName, "Error", String.Format("O processo de atualização do app não foi finalizado, uma vez que o Windows retornou o seguinte erro:\n{0}...", exc.Message));
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
        }

        public class appIntegrity
        {
            public string appName { get; set; }
            public string appRelease { get; set; }
            public string appVersion { get; set; }
            public string codeRepo { get; set; }
            public string[] customFiles { get; set; }
            public string fileName { get; set; }
            public string fileHash { get; set; }
            public double fileSize { get; set; }
        }

        public class Globals
        {
            public static string RootFolder = "";
            public static Process appHandle = new Process();
            public static appIntegrity localApp = new appIntegrity();
            public static appIntegrity stableApp = new appIntegrity();
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
