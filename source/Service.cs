﻿using System;
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
    public partial class ArlService : ServiceBase
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
        public ArlService()
        {
            InitializeComponent();

            this.config = new Configuration();            
            this.autorunsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autorunsc.exe");
            this.ehc = new ExtendedHttpClient();
            this.ehc.Error += OnEhc_Error;
            this.timer = new System.Timers.Timer();
            //this.timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
            this.timer.Interval = 120000;
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
            if (EventLog.SourceExists(Global.DISPLAY_NAME) == false)
            {
                EventLog.CreateEventSource(Global.DISPLAY_NAME, "Application");
            }

            EventLog.WriteEntry(Global.DISPLAY_NAME, "Service starting", EventLogEntryType.Information);

            if (File.Exists(this.autorunsPath) == false)
            {
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Autorunsc does not exist in the application directory: " + this.autorunsPath, EventLogEntryType.Error);
                this.ExitCode = -1;
                this.Stop();
                return;
            }

            if (ValidateConfig() == false)
            {
                this.ExitCode = -1;
                this.Stop();
                return;
            }

            if (LoadX509Certificate() == false)
            {
                this.ExitCode = -1;
                this.Stop();
                return;
            }

            this.remoteUrl = "https://" + config.RemoteServer + "/" + Environment.UserDomainName + "/" + Environment.MachineName;
            this.timer.Enabled = true;

            // Get an initial set of data using a Task so the service OnStart 
            // method can complete without waiting for the process to finish            
            Task.Run(() => { ProcessAutorunData(); });
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnStop()
        {
            EventLog.WriteEntry(Global.DISPLAY_NAME, "Service stopping", EventLogEntryType.Information);
            this.timer.Enabled = false;            
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
                EventLog.WriteEntry(Global.DISPLAY_NAME, "Autorun binary is invalid", EventLogEntryType.Error);
                return;
            }

            Process p = new Process();
            ProcessResult pr = Functions.ExecuteProcess(p, autorunsPath, AUTORUNS_PARAMETERS, "");
            var temp = Encoding.UTF8.GetBytes(pr.Output);
            string output = Encoding.Unicode.GetString(temp);

            Task.Run(() => { ehc.Send(remoteUrl, Encoding.ASCII.GetBytes(output)); });
        }

        /// <summary>
        /// Ensures that the autoruns binary is valid, correct and not tampered with
        /// </summary>
        /// <returns></returns>
        private bool IsValidAutoRunsBinary()
        {
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(this.autorunsPath);
            if (fvi.InternalName.ToLower() != "sysinternals autoruns")
            {
                return false;
            }

            var cert = X509Certificate.CreateFromSignedFile(this.autorunsPath);
            var cert2 = new X509Certificate2(cert.Handle);
            if (cert2.Verify() == false)
            {
                return false;
            }

            if (cert2.Issuer.ToLower() != "cn=microsoft code signing pca, o=microsoft corporation, l=redmond, s=washington, c=us")
            {
                return false;
            }

            if (AuthenticodeTools.IsTrusted(this.autorunsPath) == false)
            {
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