// Inspired by https://github.com/Senior-S/SVD-Example-Use/blob/main/SVDLibrary/Helpers/WavReader.cs

namespace SVDAPI;

public class WavReader
{
    public static float[] ReadWavFile(string filePath)
    {
        using FileStream fs = File.OpenRead(filePath);
        using BinaryReader reader = new(fs);
        
        if (new string(reader.ReadChars(4)) != "RIFF")
            throw new Exception("Not a valid WAV file: RIFF header missing");

        reader.ReadInt32();
        
        if (new string(reader.ReadChars(4)) != "WAVE")
            throw new Exception("Not a valid WAV file: WAVE header missing");
        if (new string(reader.ReadChars(4)) != "fmt ")
            throw new Exception("Not a valid WAV file: fmt header missing");
        
        int fmtSize = reader.ReadInt32();
        
        short audioFormat = reader.ReadInt16();
        if (audioFormat != 1 && audioFormat != 3) // PCM or IEEE float
            throw new Exception($"Only PCM or IEEE float WAV files are supported. Found format: {audioFormat}");
        
        int channels = reader.ReadInt16();
        int sampleRate = reader.ReadInt32();
        
        reader.ReadInt32(); // Byte rate
        reader.ReadInt16(); // Block align
        
        short bitsPerSample = reader.ReadInt16();
        if (fmtSize > 16)
            reader.ReadBytes(fmtSize - 16);
        while (true)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();
                
            if (chunkId == "data")
            {
                List<float> samples = [];
                int bytesPerSample = bitsPerSample / 8;
                int numSamples = chunkSize / bytesPerSample;

                for (int i = 0; i < numSamples; i++)
                {
                    float sample;
                    
                    if (audioFormat == 1)
                    {
                        if (bitsPerSample == 16)
                        {
                            short value = reader.ReadInt16();
                            sample = value / 32768.0f;
                        }
                        else if (bitsPerSample == 24)
                        {
                            byte[] bytes = reader.ReadBytes(3);
                            int value = (bytes[0]) | (bytes[1] << 8) | (bytes[2] << 16);
                                
                            if ((value & 0x800000) != 0)
                                value |= -0x1000000;
                                
                            sample = value / 8388608.0f;
                        }
                        else if (bitsPerSample == 32)
                        {
                            int value = reader.ReadInt32();
                            sample = value / 2147483648.0f;
                        }
                        else if (bitsPerSample == 8)
                        {
                            byte value = reader.ReadByte();
                            sample = (value - 128) / 128.0f;
                        }
                        else
                        {
                            throw new Exception($"Unsupported bit depth: {bitsPerSample}");
                        }
                    }
                    else
                    {
                        sample = reader.ReadSingle();
                    }
                        
                    samples.Add(sample);
                }
                
                if (sampleRate != 24000 || channels != 1)
                {
                    return ResampleAndConvertChannels(samples.ToArray(), sampleRate, channels);
                }
                    
                return samples.ToArray();
            }
            else
            {
                reader.ReadBytes(chunkSize);
            }
            
            if (fs.Position >= fs.Length)
                throw new Exception("Data chunk not found in WAV file");
        }
    }
    
    private static float[] ResampleAndConvertChannels(float[] input, int srcSampleRate, int srcChannels)
    {
        float[] monoData;
        if (srcChannels != 1)
        {
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
        }
        else
        {
            monoData = input;
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
