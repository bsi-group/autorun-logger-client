using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace AutoRunLogger
{
    /// <summary>
    /// 
    /// </summary>
    internal class ExtendedHttpClient
    {
        #region Delegates
        public delegate void ErrorEvent(string message);
        #endregion

        #region Events
        public event ErrorEvent Error;
        #endregion

        /// <summary>
        /// Member variables
        /// </summary>
        private HttpClient hc;
        public static X509Certificate x509Cert = null;

        /// <summary>
        /// 
        /// </summary>
        public ExtendedHttpClient()
        {
            this.hc = new HttpClient();

            // Validate cert by calling a function
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateRemoteCertificate);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        public async void Send(string url, byte[] data)
        {
            try
            {

                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                {
                    using (System.IO.Compression.GZipStream gzip = new System.IO.Compression.GZipStream(ms,
                        System.IO.Compression.CompressionMode.Compress, true))
                    {
                        gzip.Write(data, 0, data.Length);
                    }

                    ms.Position = 0;
                    byte[] compressed = new byte[ms.Length];
                    ms.Read(compressed, 0, compressed.Length);

                    using (MemoryStream outStream = new MemoryStream(compressed))
                    using (StreamContent streamContent = new StreamContent(outStream))
                    {
                        streamContent.Headers.Add("Content-Encoding", "gzip");
                        streamContent.Headers.ContentLength = outStream.Length;

                        using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(url)))
                        {
                            requestMessage.Content = streamContent;
                            using (HttpResponseMessage response = await this.hc.SendAsync(requestMessage))
                            {
                                var ret = await response.Content.ReadAsStringAsync();
                            }                                
                        }               
                    }                       
                }
            }
            catch (Exception ex)
            {
                var e = ex.GetInnerMostException();
                if (e != null)
                {
                    OnError("Error sending AutoRun data: " + e.Message);
                } 
                else
                {
                    OnError("Error sending AutoRun data: " + ex.Message);
                }             
            }
        }

        /// <summary>
        ///  Callback used to validate the certificate in an SSL conversation. The method 
        ///  checks the public key from the HTTPS request against the servers public
        ///  key. This provides an implementation of certificate pinning
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cert"></param>
        /// <param name="chain"></param>
        /// <param name="policyErrors"></param>
        /// <returns></returns>
        private static bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors policyErrors)
        {
            if (x509Cert == null)
            {
                return false;
            }

            if (null == cert)
            {
                return false;
            }

            if (x509Cert.GetPublicKeyString() == cert.GetPublicKeyString())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private void OnError(string message)
        {
            var handler = Error;
            if (handler != null)
            {
                handler(message);
            }
        }
    }
}
