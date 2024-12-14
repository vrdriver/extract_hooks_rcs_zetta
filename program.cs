using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NAudio.Wave;  

class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Enter the directory containing WAV files:");
        string directoryPath = Console.ReadLine();



        // Edit this if you want a permantent extraction.
        // directoryPath = "c:\\temp\\wave_files";


 



        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine("Invalid directory path. Exiting...");
            return;
        }

        string hooksDirectory = Path.Combine(directoryPath, "hooks");
        Directory.CreateDirectory(hooksDirectory);

        string batchFilePath = Path.Combine(directoryPath, "process_hooks.bat");

        using (StreamWriter batchFileWriter = new StreamWriter(batchFilePath))
        {
            foreach (string wavFilePath in Directory.GetFiles(directoryPath, "*.wav"))
            {
                Console.WriteLine($"Processing file: {wavFilePath}");
                var markers = ReadZettaMetadata(wavFilePath);

                var marker1 = markers.Find(m => m.Name == "MRK1");
                var marker2 = markers.Find(m => m.Name == "MRK2");

                if (marker1 != null && marker2 != null)
                {
                    double audioDuration = GetAudioDuration(wavFilePath);

                    if (ValidateMarkers(marker1, marker2, audioDuration))
                    {
                        string outputFileName = Path.GetFileNameWithoutExtension(wavFilePath) + "_hook.mp3";
                        string outputFilePath = Path.Combine(hooksDirectory, outputFileName);

//                        batchFileWriter.WriteLine($"ffmpeg -i \"{wavFilePath}\" -ss {marker1.ConvertedTime} -to {marker2.ConvertedTime} -c copy \"{outputFilePath}\"");
                        batchFileWriter.WriteLine($"ffmpeg -i \"{wavFilePath}\" -ss {marker1.ConvertedTime} -to {marker2.ConvertedTime} -c:a libmp3lame -b:a 192k \"{outputFilePath}\"");

                    }
                    else
                    {
                        Console.WriteLine($"Invalid markers in file {wavFilePath}. Skipping...");
                    }
                }
                else
                {
                    Console.WriteLine($"Markers MRK1 and MRK2 not found in {wavFilePath}. Skipping...");
                }
            }
        }

        Console.WriteLine($"Batch file generated at: {batchFilePath}");
    }

    public static List<MarkerInfo> ReadZettaMetadata(string wavFilePath)
    {
        byte[] fileBytes = File.ReadAllBytes(wavFilePath);

        if (!IsValidWavFile(fileBytes))
        {
            Console.WriteLine("The file is not a valid WAV/RIFF file.");
            return new List<MarkerInfo>();
        }

        var markers = new (string Name, byte[] HexValue)[]
        {
            ("MRK1", new byte[] { 0x4D, 0x52, 0x4B, 0x31 }),
            ("MRK2", new byte[] { 0x4D, 0x52, 0x4B, 0x32 })
        };

        var foundMarkers = new List<MarkerInfo>();

        foreach (var (name, hexValue) in markers)
        {
            int position = FindHexPosition(fileBytes, hexValue);
            if (position >= 0)
            {
                byte[] extractedData = ReadDataAfterMarker(fileBytes, position);
                string convertedTime = ConvertHexDataToTimeFormatted(extractedData, 44100); // Sample rate assumed
                foundMarkers.Add(new MarkerInfo { Name = name, ConvertedTime = convertedTime });
            }
        }

        return foundMarkers;
    }

    private static double GetAudioDuration(string wavFilePath)
    {
        using (var reader = new WaveFileReader(wavFilePath))
        {
            return reader.TotalTime.TotalSeconds;
        }
    }

    private static bool ValidateMarkers(MarkerInfo marker1, MarkerInfo marker2, double audioDuration)
    {
        double marker1Time = TimeStringToSeconds(marker1.ConvertedTime);
        double marker2Time = TimeStringToSeconds(marker2.ConvertedTime);

        if (marker1Time < 0 || marker2Time < 0)
            return false;

        if (marker1Time >= marker2Time)
            return false;

        if (marker2Time > audioDuration)
            return false;

        return true;
    }

    private static double TimeStringToSeconds(string timeString)
    {
        var parts = timeString.Split(':', '.');
        if (parts.Length < 3) return -1;

        int hours = int.Parse(parts[0]);
        int minutes = int.Parse(parts[1]);
        int seconds = int.Parse(parts[2]);
        int milliseconds = parts.Length == 4 ? int.Parse(parts[3]) : 0;

        return hours * 3600 + minutes * 60 + seconds + milliseconds / 1000.0;
    }

    private static bool IsValidWavFile(byte[] fileBytes)
    {
        return fileBytes.Length >= 12 &&
               Encoding.ASCII.GetString(fileBytes, 0, 4) == "RIFF" &&
               Encoding.ASCII.GetString(fileBytes, 8, 4) == "WAVE";
    }

    private static int FindHexPosition(byte[] fileBytes, byte[] hexValue)
    {
        for (int i = 0; i <= fileBytes.Length - hexValue.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < hexValue.Length; j++)
            {
                if (fileBytes[i + j] != hexValue[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private static byte[] ReadDataAfterMarker(byte[] fileBytes, int markerPosition)
    {
        int dataLength = 4;
        int startPosition = markerPosition + 4;

        if (startPosition + dataLength > fileBytes.Length)
            dataLength = fileBytes.Length - startPosition;

        byte[] data = new byte[dataLength];
        Array.Copy(fileBytes, startPosition, data, 0, dataLength);
        return data;
    }

    private static string ConvertHexDataToTimeFormatted(byte[] extractedData, int sampleRate)
    {
        double totalSeconds = ConvertHexDataToTime(extractedData, sampleRate);
        long totalMilliseconds = (long)(totalSeconds * 1000);
        long hours = totalMilliseconds / 3600000;
        long minutes = (totalMilliseconds % 3600000) / 60000;
        long seconds = (totalMilliseconds % 60000) / 1000;
        long milliseconds = totalMilliseconds % 1000;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }

    private static double ConvertHexDataToTime(byte[] hexData, int sampleRate)
    {
        int samplePosition = BitConverter.ToInt32(hexData, 0);
        return (double)samplePosition / sampleRate;
    }

    public class MarkerInfo
    {
        public string Name { get; set; }
        public string ConvertedTime { get; set; }
    }
}
