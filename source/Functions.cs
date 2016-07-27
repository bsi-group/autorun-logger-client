using System.Diagnostics;
using System.Threading.Tasks;

namespace AutoRunLogger
{
    /// <summary>
    /// 
    /// </summary>
    internal class Functions
    {
        /// <summary>
        /// Executes a process including specifying the working directory
        /// </summary>
        /// <param name="p"></param>
        /// <param name="fileName"></param>
        /// <param name="arguments"></param>
        /// <param name="workingDir"></param>
        /// <returns></returns>
        public static ProcessResult ExecuteProcess(Process p, string fileName, string arguments, string workingDir)
        {
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            // p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = fileName;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.WorkingDirectory = workingDir;
            p.Start();

            using (Task processWaiter = Task.Factory.StartNew(() => p.WaitForExit()))
            using (Task<string> outputReader = Task.Factory.StartNew(() => p.StandardOutput.ReadToEnd()))
            //using (Task<string> errorReader = Task.Factory.StartNew(() => p.StandardError.ReadToEnd()))
            {
                //Task.WaitAll(processWaiter, outputReader, errorReader);
                Task.WaitAll(processWaiter, outputReader);

                // ProcessResult pr = new ProcessResult(outputReader.Result, errorReader.Result, p.ExitCode);
                ProcessResult pr = new ProcessResult(outputReader.Result, "", p.ExitCode);
                return pr;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="message"></param>
        public static void LogToEventLog(string eventSource, string message)
        {
            EventLog.WriteEntry(eventSource, message);

        }
    }
}
