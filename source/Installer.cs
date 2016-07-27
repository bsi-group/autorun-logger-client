using System.ComponentModel;
using System.ServiceProcess;

namespace AutoRunLogger
{
    /// <summary>
    /// 
    /// </summary>
    [RunInstaller(true)]
    public sealed class ArlServiceInstallerProcess : ServiceProcessInstaller
    {
        public ArlServiceInstallerProcess()
        {
            this.Account = ServiceAccount.LocalSystem;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [RunInstaller(true)]
    public sealed class AutoRunLoggerServiceInstaller : ServiceInstaller
    {
        public AutoRunLoggerServiceInstaller()
        {
            this.Description = Global.DESCRIPTION;
            this.DisplayName = Global.DISPLAY_NAME;
            this.ServiceName = Global.SERVICE_NAME;
            this.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
        }
    }
}
