using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Drawing;

namespace PlaylistDownloader
{
    class Program
    {
        static string pathFFMPEG, ytDLPath, exePath, outputDir, path, playlistString, playlistName, playlist;
        static int ffmpegCount = 0;
        static int ytCount = 0;

        static void Main(string[] args)
        {
            exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            pathFFMPEG = exePath + "\\ffmpeg.exe";
            ytDLPath = exePath + "\\youtube-dl.exe";

            Console.Write("Enter Playlist URL: ");
            playlist = Console.ReadLine();

            Console.Write("Enter Output Directory: ");
            outputDir = Console.ReadLine();
            path = outputDir + "\\Incomplete";

            //Console.Write("Enter Playlist Name: ");
            //playlistName = Console.ReadLine();

            Console.WriteLine("\nDownloading from Youtube:\n=========================\n");

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            ProcessStartInfo startInfoYoutube = new ProcessStartInfo()
            {
                FileName = ytDLPath,
                WorkingDirectory = path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Arguments = "-f bestaudio[ext=m4a] --no-continue --write-thumbnail --write-info-json -i " + playlist,
                //Arguments = "--extract-audio --audio-format m4a --no-continue --write-thumbnail --write-info-json -i " + playlist,
            };

            var processYT = new Process();

            processYT.StartInfo = startInfoYoutube;
            processYT.ErrorDataReceived += ProcessYT_ErrorDataReceived;
            processYT.OutputDataReceived += ProcessYT_OutputDataReceived;
            processYT.Start();
            processYT.BeginErrorReadLine();
            processYT.BeginOutputReadLine();
            processYT.WaitForExit();

            ProcessStartInfo startInfoFFMPEG = new ProcessStartInfo()
            {
                FileName = pathFFMPEG,
                WorkingDirectory = path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var m4aFiles = new DirectoryInfo(path).GetFiles("*.m4a");

            Console.WriteLine("\n\nConverting Tracks:\n==================\n");
            for (int i = 0; i < m4aFiles.Length; i++)
            {
                // Get file names
                Console.Write("\nConverting track " + (i + 1) + "");
                string m4aFile = m4aFiles[i].FullName;
                string nameWithoutExt = Path.GetDirectoryName(m4aFile) + "\\" + Path.GetFileNameWithoutExtension(m4aFile);
                string jsonFile = nameWithoutExt + ".info.json";
                string jpgFile = nameWithoutExt + ".jpg";
                string jpgSquareFile = nameWithoutExt + "_square.jpg";

                if (File.Exists(jsonFile) && File.Exists(jpgFile))
                {
                    // Square Image
                    var width = Image.FromFile(jpgFile).Width;
                    var height = Image.FromFile(jpgFile).Height;

                    startInfoFFMPEG.Arguments = "-i \"" + jpgFile + "\" -filter:v \"crop = " + height + ":" + height + ":" + (int)((width - height) / 2) + ":0\"  \"" + jpgSquareFile + "\" -y ";

                    bool success1 = false;
                    int retryCount = 0;
                    while(!success1 && retryCount < 3)
                    {
                        var process3 = new Process();
                        process3.ErrorDataReceived += FFMPEGError2;
                        process3.OutputDataReceived += FFMPEGOutput;

                        process3.StartInfo = startInfoFFMPEG;
                        process3.Start();
                        process3.BeginErrorReadLine();
                        process3.BeginOutputReadLine();
                        success1 = process3.WaitForExit(20000);
                        retryCount++;
                    }

                    // Get json info
                    var jsonText = File.ReadAllText(jsonFile);
                    dynamic json = Newtonsoft.Json.Linq.JObject.Parse(jsonText);
                    string artist = json.artist;
                    string title = json.title;
                    string album = json.album;

                    if (artist == null || title == null || album == null)
                    {
                        Console.WriteLine("\nUnable to get song info for: " + Path.GetFileName(m4aFile) + " - skipping...");
                        continue;
                    }
                    else
                    {
                        artist = artist.Replace("\"", "");
                        title = title.Replace("\"", "");
                        album = album.Replace("\"", "");
                    }

                    var mp3Out = outputDir + "\\" + (i + 1).ToString("000") + " - " + artist + " - " + title + ".mp3";

                    startInfoFFMPEG.Arguments = "-i \"" + m4aFile + "\" -i  \"" + jpgSquareFile + "\" -map 0:0 -map 1:0 -c copy -id3v2_version 3 -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover(front)\"";
                    startInfoFFMPEG.Arguments += " -metadata title=\"" + title + "\" -metadata artist=\"" + artist + "\" -metadata album=\"" + album + "\" ";
                    startInfoFFMPEG.Arguments += " -codec:v copy -codec:a libmp3lame -q:a 1 \"" + mp3Out + "\" -y";
                    var process4 = new Process();

                    process4.StartInfo = startInfoFFMPEG;
                    process4.ErrorDataReceived += FFMPEGError;
                    process4.OutputDataReceived += FFMPEGOutput;
                    process4.Start();
                    process4.BeginErrorReadLine();
                    process4.BeginOutputReadLine();

                    process4.WaitForExit();
                    //playlistString += "0:/MUSIC/" + (i + 1).ToString("000") + " - " + artist + " - " + title + ".mp3";

                    //if (i < m4aFiles.Length - 1)
                    //{
                    //    playlistString += "\n";
                    //}
                }
                else
                {
                    Console.WriteLine("Could not find info files for track " + i + " - skipping...");
                }
            }
            Console.WriteLine("\n\n\nCompleted Successfully!");
            Console.WriteLine("Press Enter Key to exit...");
            var tmp = Console.ReadLine();
            //File.WriteAllText(outputDir + "\\" + playlistName + ".m3u", playlistString);
        }

        private static void ProcessYT_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            ytCount++;
            if (e.Data!= null && e.Data.Contains("[download] Downloading video"))
            {
                var pos1 = e.Data.IndexOf("video ") + 6;
                var pos2 = e.Data.IndexOf(" of ");

                Console.Write("\nDownoading video " + e.Data.Substring(pos1, (pos2 - pos1)) + "...");
            }
            else
            {
                ytCount++;
                if (ytCount % 5 == 0)
                {
                    Console.Write('.');
                }
                
            }
        }

        private static void ProcessYT_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            //Console.WriteLine("ERROR: " + e.Data);
        }

        private static void FFMPEGOutput(object sender, DataReceivedEventArgs e)
        {
            //Console.WriteLine("DATA: " + e.Data);
        }

        private static void FFMPEGError(object sender, DataReceivedEventArgs e)
        {
            //Console.WriteLine("ERROR: " + e.Data);
            ffmpegCount++;
            if (ffmpegCount % 5 == 0)
            {
                Console.Write('.');
            }
        }

        private static void FFMPEGError2(object sender, DataReceivedEventArgs e)
        {
            //Console.WriteLine("ERROR: " + e.Data);
        }

    }
}
