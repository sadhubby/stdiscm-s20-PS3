using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ConsumerGUI
{
    public static class VideoThumbnailer
    {
        public static Image GetThumbnail(string videoPath)
        {
            // Build ffmpeg path relative to the solution (portable)
            string ffmpeg = Path.Combine(
                Application.StartupPath,
                @"..\..\..\Shared\ffmpeg\ffmpeg-8.0.1-essentials_build\bin\ffmpeg.exe"
            );

            ffmpeg = Path.GetFullPath(ffmpeg);

            if (!File.Exists(ffmpeg))
            {
                MessageBox.Show("ERROR: ffmpeg.exe not found at:\n" + ffmpeg);
                return Properties.Resources.default_thumb;
            }

            string tempFile = Path.GetTempFileName() + ".jpg";

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-i \"{videoPath}\" -ss 00:00:02 -vframes 1 -y \"{tempFile}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                }

                if (!File.Exists(tempFile))
                    return Properties.Resources.default_thumb;

                using (var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read))
                {
                    Image img = Image.FromStream(fs);
                    return new Bitmap(img);
                }
            }
            catch
            {
                return Properties.Resources.default_thumb;
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }
}
