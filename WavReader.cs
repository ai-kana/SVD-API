// Inspired by https://github.com/Senior-S/SVD-Example-Use/blob/main/SVDLibrary/Helpers/WavReader.cs

using System.Runtime.InteropServices;
using System.Text;

namespace SVDAPI;

public class WavReader
{
    public static async Task<float[]> ReadWavFileAsync(string path)
    {
        using FileStream file = File.Open(path, FileMode.Open, FileAccess.Read);
        byte[] wavData = new byte[file.Length];
        await file.ReadAsync(wavData, 0, wavData.Length);

        unsafe
        {
            fixed (byte* ptr = wavData)
            {
                return ProcessWavFile(ptr, file.Length);
            }
        }
    }

    public unsafe static float[] ReadWavFile(string filePath)
    {
        using FileStream file = File.OpenRead(filePath);
        int length = (int)file.Length;
        byte* buffer = stackalloc byte[length];
        file.Read(new Span<byte>(buffer, length));
        
        return ProcessWavFile(buffer, file.Length);
    }

    private unsafe static bool CompareSectionHeader(byte* section, byte* sectionName)
    {
        return *(int*)section == *(int*)sectionName;
    }

    static unsafe WavReader()
    {
        byte* sections = (byte*)Marshal.AllocHGlobal((sizeof(byte) * 4) * 4);
        RIFF = sections + 0;
        WAVE = sections + 4;
        FMT = sections + 8;
        DATA = sections + 12;

        Copy(RIFF, Encoding.UTF8.GetBytes("RIFF"));
        Copy(WAVE, Encoding.UTF8.GetBytes("WAVE"));
        Copy(FMT, Encoding.UTF8.GetBytes("fmt "));
        Copy(DATA, Encoding.UTF8.GetBytes("data"));

        void Copy(byte* current, byte[] buffer)
        {
            for (int i = 0; i < 4; i++) current[i] = buffer[i];
        }
    }

    private unsafe static readonly byte* RIFF;
    private unsafe static readonly byte* WAVE;
    private unsafe static readonly byte* FMT;
    private unsafe static readonly byte* DATA;

    private unsafe static float[] ProcessWavFile(byte* file, long length)
    {
        if (!CompareSectionHeader(file, RIFF))
        {
            throw new("Not a valid WAV file: RIFF header missing");
        }
        file += sizeof(uint);

        file += sizeof(uint);
        if (!CompareSectionHeader(file, WAVE))
        {
            throw new("Not a valid WAV file: WAVE header missing");
        }
        file += sizeof(uint);

        if (!CompareSectionHeader(file, FMT))
        {
            throw new("Not a valid WAV file: fmt header missing");
        }
        file += sizeof(uint);
        
        int fmtSize = *file;
        file += sizeof(int);
        
        short audioFormat = *(short*)file;
        file += sizeof(short);
        if (audioFormat != 1 && audioFormat != 3) // PCM or IEEE float
        {
            throw new($"Only PCM or IEEE float WAV files are supported. Found format: {audioFormat}");
        }
        
        short channels = *(short*)file;
        file += sizeof(short);
        int sampleRate = *(int*)file;
        file += sizeof(int);
        
        file += sizeof(int); // Byte rate
        file += sizeof(short); // Block align
        
        short bitsPerSample = *(short*)file;
        file += sizeof(short);
        if (fmtSize > 16)
        {
            file += fmtSize - 16;
        }

        int chunkSize = *file;
        do
        {
            if (file >= file + length)
            {
                throw new("Data chunk not found in WAV file");
            }
            file += 4;
            chunkSize = *(int*)file;
            file += sizeof(int);
        }
        while (CompareSectionHeader(file, DATA));
        file += sizeof(int);

        int bytesPerSample = bitsPerSample >> 3;
        int numSamples = chunkSize / bytesPerSample;

        float[] samples = new float[numSamples];
        if (audioFormat != 1)
        {
            fixed (float* data = samples)
            {
                int size = sizeof(float) * numSamples;
                Buffer.MemoryCopy(file, data, size, size);
            }
            return ConvertAndResample(samples, sampleRate, channels);
        }

        if (bitsPerSample > 32 || (bitsPerSample & (8 - 1)) != 0)
        {
            throw new Exception($"Unsupported bit depth: {bitsPerSample}");
        }

        fixed (float* pSamples = samples)
        while (numSamples != 0)
        {
            switch (bytesPerSample)
            {
                case 4:
                    *pSamples = (*(int*)file / 2147483648.0f);
                    break;
                case 3:
                    int value = (file[0]) | (file[1] << 8) | (file[2] << 16);

                    if ((value & 0x800000) != 0)
                    {
                        value |= -0x1000000;
                    }

                    *pSamples = (value / 8388608.0f);
                    break;
                case 2:
                    *pSamples = (*(short*)file / 32768.0f);
                    break;
                case 1:
                    *pSamples = ((*file - 128) / 128.0f);
                    break;
            }

            file += bytesPerSample;
            numSamples--;
        }

        return ConvertAndResample(samples, sampleRate, channels);
    }

    private static float[] ConvertAndResample(float[] samples, int rate, int channels)
    {
        if (rate != 24000 || channels != 1)
        {
            return ResampleAndConvertChannels(samples, rate, channels);
        }

        return samples;
    }
    
    private static float[] ResampleAndConvertChannels(float[] input, int srcSampleRate, int srcChannels)
    {
        if (srcChannels == 1)
        {
            return input;
        }

        float[] monoData;
        monoData = new float[input.Length / srcChannels];
        for (int i = 0; i < monoData.Length; i++)
        {
            float sum = 0;
            for (int c = 0; c < srcChannels; c++)
            {
                sum += input[i * srcChannels + c];
            }
            monoData[i] = sum / srcChannels;
        }

        if (srcSampleRate == 24000)
        {
            return monoData;
        }
        
        double ratio = (double)srcSampleRate / 24000;
        int outputLength = (int)(monoData.Length / ratio);
        float[] resampled = new float[outputLength];
            
        for (int i = 0; i < outputLength; i++)
        {
            double pos = i * ratio;
            int pos0 = (int)pos;
            int pos1 = Math.Min(pos0 + 1, monoData.Length - 1);
            double frac = pos - pos0;
                
            resampled[i] = (float)((1 - frac) * monoData[pos0] + frac * monoData[pos1]);
        }
            
        return resampled;
    }
}
