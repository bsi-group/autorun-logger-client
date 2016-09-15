using System;
using System.Collections;
using System.Configuration.Install;
using System.ServiceProcess;

namespace AutoRunLogger
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new AutoRunLogger()
                };
                ServiceBase.Run(ServicesToRun);
            }
            else if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "-install":
                        InstallService();
                        StartService();
                        break;
                    case "-uninstall":
                        StopService();
                        UninstallService();
                        break;
                    case "-run":
                        var s = new AutoRunLogger();
                        s.StartService();
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        private static void InstallService()
        {
            if (IsInstalled()) return;

            try
            {
                using (AssemblyInstaller installer = GetInstaller())
                {
                    IDictionary state = new Hashtable();
                    try
                    {
                        installer.Install(state);
                        installer.Commit(state);
                    }
                    catch
                    {
                        try
                        {
                            installer.Rollback(state);
                        }
                        catch { }
                        throw;
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private static void UninstallService()
        {
            if (!IsInstalled()) return;
            try
            {
                using (AssemblyInstaller installer = GetInstaller())
                {
                    IDictionary state = new Hashtable();
                    try
                    {
                        installer.Uninstall(state);
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static bool IsInstalled()
        {
            using (ServiceController controller = new ServiceController(Global.SERVICE_NAME))
            {
                try
                {
                    ServiceControllerStatus status = controller.Status;
                }
                catch
                {
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static bool IsRunning()
        {
            using (ServiceController controller = new ServiceController(Global.SERVICE_NAME))
            {
                if (!IsInstalled()) return false;
                return (controller.Status == ServiceControllerStatus.Running);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static AssemblyInstaller GetInstaller()
        {
            AssemblyInstaller installer = new AssemblyInstaller(typeof(AutoRunLogger).Assembly, null);
            installer.UseNewContext = true;
            return installer;
        }

        /// <summary>
        /// 
        /// </summary>
        private static void StartService()
        {
            if (!IsInstalled()) return;

            using (ServiceController controller = new ServiceController(Global.SERVICE_NAME))
            {
                try
                {
                    if (controller.Status != ServiceControllerStatus.Running)
                    {
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running,
                            TimeSpan.FromSeconds(10));
                    }
                }
                catch
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        internal static void StopService()
        {
            if (!IsInstalled()) return;
            using (ServiceController controller = new ServiceController(Global.SERVICE_NAME))
            {
                try
                {
                    if (controller.Status != ServiceControllerStatus.Stopped)
                    {
                        controller.Stop();
                        controller.WaitForStatus(ServiceControllerStatus.Stopped,
                             TimeSpan.FromSeconds(10));
                    }
                }
                catch
                {
                    throw;
                }
            }
        }
    }
}
