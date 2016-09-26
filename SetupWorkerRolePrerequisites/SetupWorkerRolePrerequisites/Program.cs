using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SetupWorkerRolePrerequisites
{
    class Program
    {
        //static string startupLog = "E:\\approot\\startupLog.txt";
        static string startupLog = System.Environment.GetEnvironmentVariable("RoleRoot").TrimEnd('\\') + @"\" + @"approot\startupLogfile.txt";

        static void Main(string[] args)
        {
            //string remoteUri = "http://de.mathworks.com/supportfiles/downloads/R2015a/deployment_files/R2015a/installers/win64/";
            //string fileName = "MCR_R2015a_win64_installer.exe";
            //string webResource = remoteUri + fileName;
            string webResource = "https://ismiotstorage.blob.core.windows.net/workersetup/MCR_R2015a_win64_installer.exe";
            string fileName = "MCR_R2015a_win64_installer.exe";

            // LocalStorage1Path begins with "<drive-letter>:" e.g. "C:" 
            string localStorageDrive = System.Environment.GetEnvironmentVariable("LocalStorage1Path").Substring(0, 2);
            string workingDirectory = string.Format("{0}\\MCRDownload\\", localStorageDrive);
            string matlabDllName = "MLFilamentDetection.dll";

            // Simple Test if this Role was started before.
            // Return in this case and do not do the startup tasks again         
            if (System.IO.File.Exists(startupLog))
            {
                //using (System.IO.StreamWriter file = new System.IO.StreamWriter(startupLog, true))
                //{
                //    file.WriteLine(DateTime.Now + "\t" + "This is a restart of the role. No Startup Tasks to do.\n");
                //}

                writeStartupLog("This is a restart of the role.No Startup Tasks to do.");

                return;
            }

            // 1.) Download
            //
            WebClient webclient = new WebClient();
            Console.WriteLine("Downloading File \"{0}\" from \"{1}\" .......\n\n", fileName, webResource);
            //System.IO.Directory.CreateDirectory("C:\\MCRDownload");
            System.IO.Directory.CreateDirectory(workingDirectory);

            // trust all certificates: http://stackoverflow.com/questions/11328061/c-sharp-webclient-download-string-https
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            webclient.DownloadFile(webResource, workingDirectory + fileName);
            Console.WriteLine("Successfully Downloaded File \"{0}\" from \"{1}\"", fileName, webResource);
            writeStartupLog(String.Format("Successfully Downloaded File \"{0}\" from \"{1}\"", fileName, webResource));

            // 2.) Rename to .zip
            //
            string fileNameZip = fileName.Substring(0, fileName.LastIndexOf(".exe")) + ".zip";
            System.IO.File.Move(workingDirectory + fileName, workingDirectory + fileNameZip);
            writeStartupLog(String.Format("Successfully Renamed \"{0}\" to \"{1}\"", fileName, fileNameZip));

            // 3.) Extract .zip file
            //
            string directoryNameExtracted = fileNameZip.Substring(0, fileNameZip.LastIndexOf(".zip"));
            System.IO.Compression.ZipFile.ExtractToDirectory(workingDirectory + fileNameZip, workingDirectory + directoryNameExtracted);
            writeStartupLog(String.Format("Successfully Extracted zip file to \"{0}\" directory", workingDirectory + directoryNameExtracted));

            // 4.) Install the Matlab dlls in the global assembly cache (GAC) with gacutil.exe   
            writeStartupLog(ExecuteCommandSync(string.Format("gacutil.exe /i {0}", matlabDllName)));
            writeStartupLog(string.Format("Successfully Installed {0} in the Global Assembly Cache with gacutil.exe", matlabDllName));

            /*
            writeStartupLog(ExecuteCommandSync("gacutil.exe /i MWArray.dll"));
            writeStartupLog("Successfully Installed MWArray.dll in the Global Assembly Cache with gacutil.exe");
            */

            // 5.) Set PATH Variable
            // http://nicoploner.blogspot.com.au/2011/11/setting-environment-variables-in.html
            string path = @"%SystemRoot%\system32;%SystemRoot%;%SystemRoot%\System32\Wbem;%SYSTEMROOT%\System32\WindowsPowerShell\v1.0\;%SystemRoot%\Program Files\MATLAB\MATLAB Runtime\v85\bin\win64";//%SystemRoot%\Program Files\MATLAB\MATLAB Runtime\v85\runtime\win64";
            writeStartupLog(ExecuteCommandSync(String.Format("setx PATH \"{0}\" /M", path)));

            // 6.) Set LocalRoot Variable
            writeStartupLog(ExecuteCommandSync(String.Format("setx LocalRoot \"{0}\" /M", localStorageDrive)));

            // 7.) Create User and execute the silent install in it's context
            //
            writeStartupLog(ExecuteCommandSync("net user scheduser Qwer123 /add"));
            writeStartupLog(ExecuteCommandSync("net localgroup Administrators scheduser /add"));

            // execute schtasks cmd in 1 minutes
            DateTime executionTime = DateTime.Now.Add(new TimeSpan(0, 1, 0));
            string date = string.Format("{0}/{1}/{2}", executionTime.Month.ToString("d2"), executionTime.Day.ToString("d2"), executionTime.Year);
            string time = string.Format("{0}:{1}", executionTime.Hour.ToString("d2"), executionTime.Minute.ToString("d2"));
            //string cmd = string.Format("schtasks /CREATE /TN InstallMCR /SC ONCE /SD {0} /ST {1} /RL HIGHEST /RU scheduser /RP Qwer123 /TR \"C:\\MCRDownload\\MCR_R2015a_win64_installer\\bin\\win64\\setup.exe -mode silent -agreeToLicense yes -outputFile C:\\MCRDownload\\InstallMcr.log\" /F", date, time);
            string cmd = string.Format("schtasks /CREATE /TN InstallMCR /SC ONCE /SD {0} /ST {1} /RL HIGHEST /RU scheduser /RP Qwer123 /TR \"{2}\\MCR_R2015a_win64_installer\\bin\\win64\\setup.exe -mode silent -agreeToLicense yes -outputFile {2}\\InstallMcr.log\" /F", date, time, workingDirectory.TrimEnd('\\'));
            writeStartupLog(ExecuteCommandSync("ECHO " + cmd));
            writeStartupLog(ExecuteCommandSync(cmd));
           
        }

        public static void writeStartupLog(string message)
        {
            if (!System.IO.File.Exists(startupLog))
            {
                using (System.IO.File.Create(startupLog))
                { }
            }
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(startupLog, true))
            {
                file.WriteLine(DateTime.Now + "\t" + message);
            }
        }

        // http://www.codeproject.com/Articles/25983/How-to-Execute-a-Command-in-C
        /// <span class="code-SummaryComment"><summary></span>
        /// Executes a shell command synchronously.
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="command">string command</param></span>
        /// <span class="code-SummaryComment"><returns>string, as output of the command.</returns></span>
        public static string ExecuteCommandSync(object command)
        {
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                System.Diagnostics.ProcessStartInfo procStartInfo =
                    new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;
                // Now we create a process, assign its ProcessStartInfo and start it
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                // Get the output into a string
                string result = proc.StandardOutput.ReadToEnd();
                // Display the command output.
                Console.WriteLine(result);
                return result;
            }
            catch (Exception objException)
            {
                // Log the exception
                return objException.Message;
            }
        }
    }
}
