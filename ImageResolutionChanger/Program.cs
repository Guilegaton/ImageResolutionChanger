using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ImageResolutionChanger
{
    class Program
    {
        private const decimal MAX_FILE_SIZE_PNG = 8388608;

        private static ulong filesCount = 0;

        private static float STEP_OF_COMPRESSION;
        private static int BATCH_SIZE;
        private static int MAX_DEGREE_OF_PARALLELISM;
        private static string DESTINATION_FOLDER_NAME;

        static void Main(string[] args)
        {
            var batchSizeConversion = int.TryParse(ConfigurationManager.AppSettings["BATCH_SIZE"], out int batchSizeConversionResult);
            BATCH_SIZE = batchSizeConversion ? batchSizeConversionResult : 5;
            var stepOfCompressionConversion = float.TryParse(ConfigurationManager.AppSettings["STEP_OF_COMPRESSION"], out float stepOfCompressionConversionResult);
            STEP_OF_COMPRESSION = stepOfCompressionConversion ? stepOfCompressionConversionResult : 0.1f;
            var destinationFolder = ConfigurationManager.AppSettings["DESTINATION_FOLDER_NAME"];
            DESTINATION_FOLDER_NAME = !string.IsNullOrWhiteSpace(destinationFolder) ? destinationFolder : "Compressed";
            var maxDegreeOfParallelismConversion = int.TryParse(ConfigurationManager.AppSettings["MAX_DEGREE_OF_PARALLELISM"], out int maxDegreeOfParallelismConversionResult);
            MAX_DEGREE_OF_PARALLELISM = maxDegreeOfParallelismConversion ? maxDegreeOfParallelismConversionResult : 1;

            Console.WriteLine("Please enter the path to folder with images:");
            var path = Console.ReadLine();

            var files = GetFilesFromDirectory(path);
            var folder = string.Empty;
            if (files.Any())
            {
                folder = CreateFolder(path, DESTINATION_FOLDER_NAME);
            }

            Dictionary<string, Bitmap> images = null;
            for (int i = 0; files.Count() > BATCH_SIZE * i; i++)
            {
                images = ChangeImageResolution(files.Skip(i*BATCH_SIZE).Take(BATCH_SIZE), MAX_FILE_SIZE_PNG);
                SaveImages(images, folder);
                GC.SuppressFinalize(images);
            }
        }

        private static IEnumerable<string> GetFilesFromDirectory(string path)
        {
            var result = default(IEnumerable<string>);
            if (Directory.Exists(path))
            {
                result = Directory.GetFiles(path);
            }

            return result;
        }

        private static string CreateFolder(string path, string folderName)
        {

            var str = "";
            var folder = string.Empty;
            for (int i = 0; ; i++)
            {
                folder = $"{path}\\{folderName}{str}";
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    break;
                }
                str = $"({i})";
            }

            return folder;
        }

        private static Dictionary<string, Bitmap> ChangeImageResolution(IEnumerable<string> files, decimal fileSizeInBytes)
        {
            var result = new Dictionary<string, Bitmap>();

            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = MAX_DEGREE_OF_PARALLELISM }, (file) =>
            {
                try
                {
                    var image = Image.FromFile(file);
                    var levelOfCompression = 0;

                    while (true)
                    {
                        var compression = levelOfCompression * STEP_OF_COMPRESSION + 1;
                        var width = (int)Math.Abs(image.Width / compression);
                        var hieght = (int)Math.Abs(image.Height / compression);
                        var destRect = new Rectangle(0, 0, width, hieght);
                        var destImage = new Bitmap(width, hieght);

                        destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                        using (var graphics = Graphics.FromImage(destImage))
                        {
                            graphics.CompositingMode = CompositingMode.SourceOver;
                            graphics.CompositingQuality = CompositingQuality.Default;
                            graphics.InterpolationMode = InterpolationMode.Default;
                            graphics.SmoothingMode = SmoothingMode.Default;
                            graphics.PixelOffsetMode = PixelOffsetMode.Default;

                            using (var wrapMode = new ImageAttributes())
                            {
                                wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                                graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                            }
                        }

                        long fileSize = 0;
                        using (var stream = new MemoryStream())
                        {
                            destImage.Save(stream, ImageFormat.Png);
                            fileSize = stream.Length;
                        }

                        if (fileSize < MAX_FILE_SIZE_PNG)
                        {
                            var fileName = file.Split('\\').Last();
                            result.Add(fileName, destImage);
                            Console.WriteLine($"{fileName} processed. Total processed files count: {++filesCount}");
                            break;
                        }
                        else
                        {
                            levelOfCompression++;
                            continue;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine($"{file} was skipped");
                }
            });

            return result;
        }

        private static void SaveImages(Dictionary<string, Bitmap> images, string folder)
        {
            Parallel.ForEach(images, (image) =>
            {
                image.Value.Save($"{folder}\\{image.Key}");
            });
        }
    }
}
