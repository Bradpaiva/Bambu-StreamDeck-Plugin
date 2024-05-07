using StreamDeckLib;
using StreamDeckLib.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using System.Globalization;

namespace Bambulab
{
    [ActionUuid(Uuid = "bradpaiva.BambuLab.DefaultPluginAction")]
    public class BambuLabAction : BaseStreamDeckActionWithSettingsModel<Models.BambuSettingsModel>
    {
        public override async Task OnKeyUp(StreamDeckEventPayload args)
        {
            
            string output = ""; // output to save the command response

            try
            {
                string command = "bambu-cli status";
                string directory = null;
                using Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        //populate process settings
                        FileName = "cmd.exe",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        Arguments = "/c " + command,
                        CreateNoWindow = true,
                        WorkingDirectory = directory ?? string.Empty,
                    }
                };
                process.Start();
                process.WaitForExit();
                output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            //parse the output to get the information we need
            string[] outputParse = output.Split(' ', '\n');

            int i = 0;
            while (i < outputParse.Length)
            {
                //find the print percentage
                if (outputParse[i].ToLower().Contains('%'))
                {

                    SettingsModel.printerPercent = outputParse[i];
                    // the next entry is the print time
                    while (i < outputParse.Length)
                    {
                        if (outputParse[i].ToLower().Contains('m') || outputParse[i].ToLower().Contains('s') || outputParse[i].ToLower().Contains('h'))
                        {
                            SettingsModel.printerTime = outputParse[i];
                            break;
                        }
                        i++;
                    }
                    break;
                }
                i++;
            }

            //get the ams colors
            byte[,] amsArray = new byte[4, 3];
            int k = 0;
            while (i < outputParse.Length)
            {
                //color information is in hex #RRGGBB
                if (outputParse[i].ToLower().Contains('#'))
                {
                    char[] tempArray = outputParse[i].Replace("#", string.Empty).ToCharArray();

                    //RR
                    char[] char1 = { tempArray[0], tempArray[1] };
                    string s1 = new string(char1);

                    //GG
                    char[] char2 = { tempArray[2], tempArray[3] };
                    string s2 = new string(char2);

                    //BB
                    char[] char3 = { tempArray[4], tempArray[5] };
                    string s3 = new string(char3);

                    //compile color
                    int R = int.Parse(s1, NumberStyles.HexNumber);
                    int G = int.Parse(s2, NumberStyles.HexNumber);
                    int B = int.Parse(s3, NumberStyles.HexNumber);

                    int[] amsColor = new[] { R, G, B };

                    //add to the array of colors
                    amsArray[k, 0] = Convert.ToByte(amsColor[0]);
                    amsArray[k, 1] = Convert.ToByte(amsColor[1]);
                    amsArray[k, 2] = Convert.ToByte(amsColor[2]);
                    k++;
                }
                i++;
            }

            //define image locations
            string imagePath = "images/AMS.png"; // Path to the input image
            string outputPath = "images/AMSicon.png"; // Path to save the output image

            //populate the replacement colors matrix in Argb32 format
            Argb32[] replacementColors = new Argb32[]
            {
                    new Argb32(amsArray[0,0], amsArray[0,1], amsArray[0,2],255),   // Red replacement color (ARGB format)
                    new Argb32(amsArray[1,0], amsArray[1,1], amsArray[1,2],255),   // Blue replacement color (ARGB format)
                    new Argb32(amsArray[2,0], amsArray[2,1], amsArray[2,2],255),   // Yellow replacement color (ARGB format)
                    new Argb32(amsArray[3,0], amsArray[3,1], amsArray[3,2],255)    // Green replacement color (ARGB format)
            };

            //replace the colors in the template icon
            ReplaceColors(imagePath, outputPath, replacementColors);

            //add the title
            SettingsModel.titleString = SettingsModel.printerPercent + "\n" + SettingsModel.printerTime;
            await Manager.SetTitleAsync(args.context, SettingsModel.titleString);

            //add the icon
            await Manager.SetImageAsync(args.context, "images/AMSicon.png");

            //update settings
            await Manager.SetSettingsAsync(args.context, SettingsModel);
        
        }

        public override async Task OnDidReceiveSettings(StreamDeckEventPayload args)
        {
            await base.OnDidReceiveSettings(args);
            await Manager.SetTitleAsync(args.context, "");
        }

        public override async Task OnWillAppear(StreamDeckEventPayload args)
        {
            await base.OnWillAppear(args);
            await Manager.SetTitleAsync(args.context, "");
        }

        static void ReplaceColors(string inputImagePath, string outputImagePath, Argb32[] replacementColors)
        {
            using (Image<Argb32> image = Image.Load<Argb32>(inputImagePath))
            {
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        Argb32 pixelColor = image[x, y];
                        Argb32 newColor = pixelColor;

                        if (IsSimilarColor(pixelColor, new Argb32(255, 0, 0, 255)))
                            newColor = replacementColors[0]; // Red
                        else if (IsSimilarColor(pixelColor, new Argb32(0, 0, 255, 255)))
                            newColor = replacementColors[1]; // Blue
                        else if (IsSimilarColor(pixelColor, new Argb32(255, 255, 0, 255)))
                            newColor = replacementColors[2]; // Yellow
                        else if (IsSimilarColor(pixelColor, new Argb32(0, 255, 0, 255)))
                            newColor = replacementColors[3]; // Green

                        image[x, y] = newColor;
                    }
                }

                image.Save(outputImagePath, new PngEncoder()); // automatic encoder selected based on the extension
            }
        }

        static bool IsSimilarColor(Argb32 a, Argb32 b, int tolerance = 100)
        {
            return Math.Abs(a.R - b.R) < tolerance &&
                   Math.Abs(a.G - b.G) < tolerance &&
                   Math.Abs(a.B - b.B) < tolerance;
        }
    }
}


