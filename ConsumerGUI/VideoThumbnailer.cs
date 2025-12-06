//using System;
//using System.Diagnostics;
//using System.Drawing;
//using System.IO;
//using System.Security.Cryptography;
//using System.Text;

//namespace ConsumerGUI
//{
//    public static class VideoThumbnailer
//    {
//        // Try locate ffmpeg inside repository under Shared\ffmpeg... fallback to PATH
//        private static string LocateFfmpeg()
//        {
//            // relative path from build output to repo Shared\ffmpeg\... (adjust if needed)
//            string candidate = Path.GetFullPath(
//                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Shared\ffmpeg\ffmpeg-8.0.1-essentials_build\bin\ffmpeg.exe")
//            );

//            if (File.Exists(candidate)) return candidate;

//            // alternative location inside repo root (older path you used)
//            candidate = Path.GetFullPath(
//                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Shared\ffmpeg\bin\ffmpeg.exe")
//            );
//            if (File.Exists(candidate)) return candidate;

//            // fallback: assume ffmpeg is on PATH
//            return "ffmpeg";
//        }

//        private static string FfmpegPath = LocateFfmpeg();

//        // cache directory for thumbnails
//        private static string ThumbCacheDir = Path.Combine(Path.GetTempPath(), "stdiscm_thumbs");

//        static VideoThumbnailer()
//        {
//            try { Directory.CreateDirectory(ThumbCacheDir); } catch { }
//        }

//        private static string HashToFileName(string s)
//        {
//            using (var sha = SHA256.Create())
//            {
//                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
//                var hex = BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
//                return hex + ".jpg";
//            }
//        }

//        /// <summary>
//        /// Get a thumbnail image for the given playbackUrl. If cached thumbnail exists, returns that.
//        /// If not, runs ffmpeg to capture 1 frame at 1s and returns the resulting image (and caches it).
//        /// playbackUrl can be an HTTP URL or a local file path (ffmpeg supports both).
//        /// </summary>
//        public static Image GetThumbnail(string playbackUrl, int width = 320, int height = 180)
//        {
//            try
//            {
//                if (string.IsNullOrEmpty(playbackUrl)) return Properties.Resources.default_thumb;

//                var fn = HashToFileName(playbackUrl);
//                var target = Path.Combine(ThumbCacheDir, fn);

//                if (File.Exists(target))
//                {
//                    // return loaded image clone (so callers can dispose)
//                    using var img = Image.FromFile(target);
//                    return new Bitmap(img);
//                }

//                // create temporary output file (unique)
//                string tmp = Path.Combine(ThumbCacheDir, Guid.NewGuid().ToString("N") + ".jpg");

//                // ffmpeg args: -ss 00:00:01 -i "<url>" -frames:v 1 -vf scale=width:height -y "<tmp>"
//                var args = $"-ss 00:00:01 -i \"{playbackUrl}\" -frames:v 1 -vf \"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2\" -y \"{tmp}\"";

//                var psi = new ProcessStartInfo
//                {
//                    FileName = FfmpegPath,
//                    Arguments = args,
//                    UseShellExecute = false,
//                    CreateNoWindow = true,
//                    RedirectStandardOutput = true,
//                    RedirectStandardError = true
//                };

//                using (var p = Process.Start(psi)!)
//                {
//                    // read stderr to avoid blocking if ffmpeg outputs a lot
//                    var err = p.StandardError.ReadToEnd();
//                    p.WaitForExit(5000); // wait up to 5s
//                    if (!p.HasExited) p.Kill();
//                }

//                if (File.Exists(tmp))
//                {
//                    // move tmp -> target (atomic-ish)
//                    if (File.Exists(target)) File.Delete(target);
//                    File.Move(tmp, target);
//                    using var img = Image.FromFile(target);
//                    return new Bitmap(img);
//                }

//                return Properties.Resources.default_thumb;
//            }
//            catch
//            {
//                return Properties.Resources.default_thumb;
//            }
//        }
//    }
//}
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
                @"C:\Users\Evan\Downloads\ffmpeg-8.0.1-essentials_build\ffmpeg-8.0.1-essentials_build\bin\ffmpeg.exe"
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