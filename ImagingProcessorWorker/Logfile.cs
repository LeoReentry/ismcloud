using IsmIoTSettings;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logging
{
    enum Fontcolors
    {
        BLACK,
        RED,
        GREEN,
        BLUE,
        PURPLE,
        YELLOW
    };

    class Logfile
    {
        //
        private static Logfile s_logfile = null;

        //
        public string LogfileName { get; set; }

        // Stream / Streamwriter
        private MemoryStream memoryLogfile;
        private StreamWriter sw;

        // Azure Storage 
        //private string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=storageacclogs;AccountKey=AlTunZcRR9c1TP8PR5Tko35zWQ3K5X3PTYfl+uhKzncvCY6N6ZbmkesoGIg5ka65sNe04Qhfvh8Zk6sRbpLTqg==";
        private string storageConnectionString = Settings.storageConnection; //System.Configuration.ConfigurationSettings.AppSettings.Get("ismiotstorage");
        private CloudStorageAccount storageAccount;
        private CloudBlobClient blobClient;
        private CloudBlobContainer container;
        private CloudBlockBlob blob;

        //
        private void ResetMemoryLogfile()
        {
            memoryLogfile = new MemoryStream();
            sw = new StreamWriter(memoryLogfile);
        }


        private Logfile(string name)
        {
            LogfileName = name;
            storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            blobClient = storageAccount.CreateCloudBlobClient();
            container = blobClient.GetContainerReference(Settings.containerLogs);
            blob = container.GetBlockBlobReference(LogfileName);

            // Erzeugt initiales Logfile (BlockBlob) bestehend aus 2 Blocks
            // nämlich der Überschrift und dem End of Logfile Teil
            // Zusammen ergibt das ein wohl geformtes HTML File
            CreateInitialLogfile();
        }

        // Singleton Pattern (privater Konstruktor)
        public static Logfile Get(string name)
        {
            if (s_logfile != null)
                return s_logfile;
            else
            {
                s_logfile = new Logfile(name);
                return s_logfile;
            }
        }


        // Erzeugt ein initiales Logfile mit Überschrift und End of Logfile Zeile. Wobei beide Teile einen eigenen Block darstellen.
        // --> AppendBlock Überschrift + AppendBlock End of Logfile
        // Der End of Logfile Block wird beim Update des Logfiles entfernt, neue Einträge angehängt und dann wieder End of Logfile angehängt.
        private void CreateInitialLogfile()
        {
            // Schreibe ersten Block mit Überschrift

            ResetMemoryLogfile();

            Textout("<html><head><title>Logfile</title></head>");
            Textout("<body><font face='courier new'>");
            WriteTopic("Logfile", 3);

#if DEBUG
            Textout("BUILD: DEBUG<br><br>");
#else
                        Textout("BUILD: RELEASE<br><br>");
#endif

            AppendBlock();
            sw.Close();


            // Schreibe zweiten Block mit End of Logfile
            memoryLogfile = new MemoryStream();
            sw = new StreamWriter(memoryLogfile);
            Textout("<br><br>End of Logfile</font></body></html>");
            AppendBlock();
            sw.Close();
            ResetMemoryLogfile();
        }


        // Appends a new block to a blob
        private void AppendBlock()
        {
            try
            {
                List<string> blockIds = new List<string>();
                blockIds.AddRange(blob.DownloadBlockList(BlockListingFilter.Committed).Select(b => b.Name));
                // Achtung: BlockId's eines BlockBlobs müssen die gleiche Länge haben! --> PadLeft()
                var newId = Convert.ToBase64String(Encoding.Default.GetBytes(blockIds.Count.ToString().PadLeft(32, '0')));
                // TODO: der MemoryStream müsste eigentlich passend hier reinkommen, damit hier kein neuer erzeugt werden muss aus dem vorhandenen
                blob.PutBlock(newId, new MemoryStream(memoryLogfile.GetBuffer(), true), null);
                blockIds.Add(newId);
                blob.PutBlockList(blockIds);
            }
            catch (Exception ex)
            {
                // Logfile ist zu verbuggt im Moment. Hier kann ab und an eine Exception auftreten. Reicht aber fürs Erste. 
            }
        }



        public void Update()
        {
            lock (memoryLogfile)
            {
                if (this.memoryLogfile.Length != 0) // Nur wenn auch neue Einträge gibt
                {
                    RemoveLastBlock(this.blob);
                    AppendBlock();

                    // Hänge am Ende wieder einen "End of Logfile Block" an
                    ResetMemoryLogfile();
                    Textout("<br><br>End of Logfile</font></body></html>");
                    AppendBlock();

                    sw.Close();
                    ResetMemoryLogfile();
                }
            }
        }

        // Get all Commited Blocks and Commit them again excluding the last block (so it will be garbage collected and deleted by the storage service)
        private void RemoveLastBlock(CloudBlockBlob blob)
        {
            try
            {
                List<string> blockIds = new List<string>();
                blockIds.AddRange(blob.DownloadBlockList(BlockListingFilter.Committed).Select(b => b.Name));
                blockIds.RemoveAt(blockIds.Count - 1);
                blob.PutBlockList(blockIds);
            }
            catch (Exception ex)
            {
                // Logfile ist zu verbuggt im Moment. Hier kann ab und an eine Exception auftreten. Reicht aber fürs Erste. 
            }
        }

        public void ClearLogfile()
        {
            // Remove All Blocks
            List<string> blockIds = new List<string>();
            blob.PutBlockList(blockIds);

            lock (memoryLogfile)
            {
                CreateInitialLogfile();
            }
        }


        public void WriteTopic(string Topic, int Size)
        {
            Textout("<table cellspacing='0' cellpadding='0' width='100%%' ");
            Textout("bgcolor='#DFDFE5'>\n<tr>\n<td>\n<font face='arial' ");
            fTextout("size='+{0}'>\n", Size);
            Textout(Topic);
            Textout("</font>\n</td>\n</tr>\n</table>\n<br>");
        }


        // Überladene Textout Methoden
        //
        public void Textout(string Text)
        {
            lock (memoryLogfile)
            {
                sw.Write(Text);
                sw.Flush();
            }
        }

        public void Textout(Fontcolors Color, string Text)
        {
            Textout(Color, false, Text);
        }

        public void Textout(Fontcolors Color, bool List, string Text)
        {
            // Listen-Tag schreiben
            if (List == true)
                Textout("<li>");

            // Farbtag schreiben
            switch (Color)
            {
                case Fontcolors.BLACK:
                    Textout("<font color=black>");
                    break;
                case Fontcolors.RED:
                    Textout("<font color=red>");
                    break;
                case Fontcolors.GREEN:
                    Textout("<font color=green>");
                    break;
                case Fontcolors.BLUE:
                    Textout("<font color=blue>");
                    break;
                case Fontcolors.PURPLE:
                    Textout("<font color=purple>");
                    break;
                case Fontcolors.YELLOW:
                    Textout("<font color=gold>");
                    break;
            }

            // Text schreiben
            Textout(Text);
            Textout("</font>");

            if (List == false)
                Textout("<br>");
            else
                Textout("</li>");
        }

        public void fTextout(string Text, params object[] Args)
        {
            Textout(String.Format(Text, Args));
        }

        public void fTextout(Fontcolors Color, string Text, params object[] Args)
        {
            Textout(Color, String.Format(Text, Args));
        }

        public void fTextout(Fontcolors Color, bool List, string Text, params object[] Args)
        {
            Textout(Color, List, String.Format(Text, Args));
        }

        public void FunctionResult(string Name, bool Result)
        {
            if (true == Result)
            {
                Textout("<table width='100%%' cellspacing='1' cellpadding='5' ");
                Textout("border='0' bgcolor='#C0C0C0'><tr><td bgcolor=");
                fTextout("'#FFFFFF' width='35%%'>{0}</td>", Name);
                Textout("<td bgcolor='FFFFFF' width='30%%'><font color=");
                Textout("'green'>OK</font></td><td bgcolor='FFFFFF' ");
                // TODO: Die 3. Spalte (im Moment mit -/- gefüllt) kann z.B. für Fehlermeldungen benutzt werden
                Textout("width='35%%'>-/-</td></tr></table>");
            }
            else
            {
                Textout("<table width='100%%' cellspacing='1' cellpadding='5' ");
                Textout("border='0' bgcolor='#C0C0C0'><tr><td bgcolor=");
                fTextout("'#FFFFFF' width='35%%'>{0}</td>", Name);
                Textout("<td bgcolor='FFFFFF' width='30%%'><font color=");
                Textout("'red'>ERROR</font></td><td bgcolor='FFFFFF' ");
                // TODO: Die 3. Spalte (im Moment mit -/- gefüllt) kann z.B. für Fehlermeldungen benutzt werden
                Textout("width='35%%'>-/-</td></tr></table>");
            }
        } // Überladene Textout Methoden
    }
}
