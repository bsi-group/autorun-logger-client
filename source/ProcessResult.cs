using System;

namespace AutoRunLogger
{
    /// <summary>
    /// Stores data returned from executing an external process.
    /// Allows for more than one value to be returned from a methods
    /// </summary>
    public class ProcessResult
    {
        #region Properties/Member Variables
        public string Output { get; private set; }
        public string Error { get; private set; }
        public int ExitCode { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// 
        /// </summary>
        /// <param name="output"></param>
        /// <param name="error"></param>
        /// <param name="exitCode"></param>
        public ProcessResult(string output, string error, int exitCode)
        {
            this.Output = output;
            this.Error = error;
            this.ExitCode = exitCode;
        }
        #endregion

        #region Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.Output + Environment.NewLine + this.Error;
        }
        #endregion
    }
}
