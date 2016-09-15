using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace AutoRunLogger
{
    /// <summary>
    /// 
    /// </summary>
    public partial class AutoRunLogger : ServiceBase
    {
        #region Constants 
        private const string AUTORUNS_PARAMETERS = "-accepteula -a * -x -h -s -t *";
        #endregion

        #region Member Variables
        private Configuration config;
        private System.Timers.Timer timer;
        private ExtendedHttpClient ehc;
        private string autorunsPath;
        private string remoteUrl;
        #endregion

        #region Constructor
        /// <summary>
        /// 
        /// </summary>
        public AutoRunLogger()
        {
            InitializeComponent();

            this.config = new Configuration();            
            this.autorunsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autorunsc.exe");
            this.ehc = new ExtendedHttpClient();
            this.ehc.Error += OnEhc_Error;
            this.timer = new System.Timers.Timer();
            this.timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
            //this.timer.Interval = 120000;
            this.timer.Elapsed += Timer_Elapsed;
        }
        #endregion

        #region Service Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            StartService();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnStop()
        {
            EventLog.WriteEntry(Global.DISPLAY_NAME, "Service stopping", EventLogEntryType.Information);
            this.timer.Enabled = false;            
        }

        /// <summary>
        /// Method to start service functions. This provides a public interface so 
        /// that the functionality can be run without initialising a service e.g. debug
        /// </summary>
        public void StartService()
        {
            // To debug, load VS2015 as an admin user, and then uncomment the following line
            //Debugger.Launch();

            if (EventLog.SourceExists(Global.DISPLAY_NAME) == false)
            {
                EventLog.CreateEventSource(Global.DISPLAY_NAME, "Application");
            }

            EventLog.WriteEntry(Global.DISPLAY_NAME, "Service starting", EventLogEntryType.Information);

            if (File.Exists(this.autorunsPath) == false)
            {
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Autorunsc does not exist in the application directory: " + this.autorunsPath, EventLogEntryType.Error);
                this.ExitCode = -1;
                Program.StopService();
                return;
            }

            if (ValidateConfig() == false)
            {
                this.ExitCode = -1;
                Program.StopService();
                return;
            }

            if (LoadX509Certificate() == false)
            {
                this.ExitCode = -1;
                Program.StopService();
                return;
            }

            this.remoteUrl = "https://" + config.RemoteServer + "/" + Environment.UserDomainName + "/" + Environment.MachineName;
            this.timer.Enabled = true;

            Task.Run(() => { ProcessAutorunData(); });
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pemFile"></param>
        /// <returns></returns>
        private bool LoadX509Certificate()
        {
            try
            {
                var x509Cert = X509Certificate.CreateFromCertFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.CertificateFileName));
                ExtendedHttpClient.x509Cert = x509Cert;
                return true;
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Error loading certificate: " + ex.Message, EventLogEntryType.Error);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.timer.Enabled = false;
            ProcessAutorunData();
            this.timer.Enabled = true;
        }

        /// <summary>
        /// 
        /// </summary>
        private void ProcessAutorunData()
        {
            if (IsValidAutoRunsBinary() == false)
            {               
                return;
            }

            Process p = new Process();
            ProcessResult pr = Functions.ExecuteProcess(p, autorunsPath, AUTORUNS_PARAMETERS, "");

            //var temp = Encoding.UTF8.GetBytes(pr.Output);
            //string output = Encoding.Unicode.GetString(temp);

            Task.Run(() => { ehc.Send(remoteUrl, Encoding.ASCII.GetBytes(pr.Output)); });
        }

        /// <summary>
        /// Ensures that the autoruns binary is valid, correct and not tampered with
        /// </summary>
        /// <returns></returns>
        private bool IsValidAutoRunsBinary()
        {
            try
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(this.autorunsPath);
                if (fvi.InternalName.ToLower() != "sysinternals autoruns")
                {
                    EventLog.WriteEntry(Global.DISPLAY_NAME, "Error validating Autorun binary: Invalid internal name (" + fvi.InternalName + ")", EventLogEntryType.Error);
                    return false;
                }

                var cert = X509Certificate.CreateFromSignedFile(this.autorunsPath);
                if (cert == null)
                {
                    EventLog.WriteEntry(Global.DISPLAY_NAME, "Error validating Autorun binary: Unable to check certificate from signed file", EventLogEntryType.Error);
                    return false;
                }

                var cert2 = new X509Certificate2(cert.Handle);
                if (cert2 == null)
                {
                    EventLog.WriteEntry(Global.DISPLAY_NAME, "Error validating Autorun binary: Unable to create X509Certificate2 object", EventLogEntryType.Error);
                    return false;
                }

                // This method appears to be unreliable and therefore has been commented out
                //if (cert2.Verify() == false)
                //{
                //    EventLog.WriteEntry(Global.DISPLAY_NAME, "Error validating Autorun binary: Unable to verify file signature", EventLogEntryType.Error);
                //    return false;
                //}

                if (cert2.Issuer.ToLower() != "cn=microsoft code signing pca, o=microsoft corporation, l=redmond, s=washington, c=us")
                {
                    EventLog.WriteEntry(Global.DISPLAY_NAME, "Error validating Autorun binary: Invalid issuer (" + cert2.Issuer + ")", EventLogEntryType.Error);
                    return false;
                }

                if (AuthenticodeTools.IsTrusted(this.autorunsPath) == false)
                {
                    EventLog.WriteEntry(Global.DISPLAY_NAME, "Error validating Autorun binary: Untrusted binary", EventLogEntryType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Error validating Autorun binary: " + ex.Message, EventLogEntryType.Error);
                return false;
            }            

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        private bool ValidateConfig()
        {
            string err = this.config.Load();
            if (err.Length > 0)
            {
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Error loading config: " + err, EventLogEntryType.Error);
                return false;
            }

            if (config.RemoteServer.Length == 0)
            {
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Remote server not set in config", EventLogEntryType.Error);
                return false;
            }

            if (config.CertificateFileName.Length == 0)
            {
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Certificate file name not set in config", EventLogEntryType.Error);
                return false;
            }

            if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.CertificateFileName)) == false)
            {
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Certificate does not exist in application directory", EventLogEntryType.Error);
                return false;
            }

            return true;
        }

        #region ExtendedHttpClient Message Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private void OnEhc_Error(string message)
        {
            EventLog.WriteEntry(Global.DISPLAY_NAME, message, EventLogEntryType.Error);
        }
        #endregion
    }
}
