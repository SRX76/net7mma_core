﻿using Media.Common;
using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;
using static Media.Containers.Riff.RiffReader;

namespace Media.Containers.Riff;

#region Nested Types

public class Chunk : Node
{
    public FourCharacterCode ChunkId
    {
        get => (FourCharacterCode)Binary.Read32(Identifier, 0, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, 0, Binary.IsBigEndian, (int)value);
    }

    public bool HasSubType => Identifier.Length > RiffReader.TWODWORDSSIZE;

    public FourCharacterCode SubType
    {
        get => (FourCharacterCode)Binary.Read32(Identifier, RiffReader.IdentifierSize, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, RiffReader.IdentifierSize, Binary.IsBigEndian, (int)value);
    }

    public Chunk(RiffWriter writer, FourCharacterCode chunkId, long dataSize)
        : base(writer, Binary.GetBytes((long)chunkId, Binary.IsBigEndian), RiffReader.LengthSize, -1, dataSize, true)
    {
        ChunkId = chunkId;
    }

    public Chunk(RiffWriter writer, FourCharacterCode chunkId, byte[] data)
        : base(writer, Binary.GetBytes((long)chunkId, Binary.IsBigEndian), RiffReader.LengthSize, -1, data)
    {
        ChunkId = chunkId;
    }
}

public class DataChunk : Chunk
{
    public DataChunk(RiffWriter writer, byte[] data)
        : base(writer, FourCharacterCode.data, data)
    {
    }
}

public class RiffChunk : Chunk
{
    public RiffChunk(RiffWriter writer, FourCharacterCode type, FourCharacterCode subType, long dataSize)
        : base(writer, type, dataSize)
    {        
        SubType = subType;
    }
}

public class FmtChunk : Chunk
{
    public ushort AudioFormat
    {
        get => Binary.ReadU16(Data, 0, Binary.IsBigEndian);
        set => Binary.Write16(Data, 0, Binary.IsBigEndian, value);
    }

    public ushort NumChannels
    {
        get => Binary.ReadU16(Data, 2, Binary.IsBigEndian);
        set => Binary.Write16(Data, 2, Binary.IsBigEndian, value);
    }

    public uint SampleRate
    {
        get => Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public uint ByteRate
    {
        get => Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public ushort BlockAlign
    {
        get => Binary.ReadU16(Data, 12, Binary.IsBigEndian);
        set => Binary.Write16(Data, 12, Binary.IsBigEndian, value);
    }

    public ushort BitsPerSample
    {
        get => Binary.ReadU16(Data, 14, Binary.IsBigEndian);
        set => Binary.Write16(Data, 14, Binary.IsBigEndian, value);
    }

    public FmtChunk(RiffWriter writer, ushort audioFormat, ushort numChannels, uint sampleRate, ushort bitsPerSample)
        : base(writer, FourCharacterCode.fmt, new byte[16])
    {
        // Set the audio format
        AudioFormat = audioFormat;

        // Set the number of channels
        NumChannels = numChannels;

        // Set the sample rate
        SampleRate = sampleRate;

        // Calculate and set the byte rate
        uint byteRate = sampleRate * numChannels * (uint)(bitsPerSample / 8);
        ByteRate = byteRate;

        // Calculate and set the block align
        ushort blockAlign = (ushort)(numChannels * (bitsPerSample / 8));
        BlockAlign = blockAlign;

        // Set the bits per sample
        BitsPerSample = bitsPerSample;
    }
}

public class WaveFormat : MemorySegment
{
    const int Size = 16;

    // Fields specific to WaveFormat
    public short AudioFormat
    {
        get => Binary.Read16(Array, Offset, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset, Binary.IsBigEndian, value);
    }

    public short NumChannels
    {
        get => Binary.Read16(Array, Offset + 2, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 2, Binary.IsBigEndian, value);
    }

    public int SampleRate
    {
        get => Binary.Read32(Array, Offset + 4, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 4, Binary.IsBigEndian, value);
    }

    public int ByteRate
    {
        get => Binary.Read32(Array, Offset + 8, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 8, Binary.IsBigEndian, value);
    }

    public short BlockAlign
    {
        get => Binary.Read16(Array, Offset + 12, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 12, Binary.IsBigEndian, value);
    }

    public short BitsPerSample
    {
        get => Binary.Read16(Array, Offset + 14, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 14, Binary.IsBigEndian, value);
    }

    public WaveFormat(AudioEncoding audioFormat, int numChannels, int sampleRate, int bitsPerSample)
        : base(new byte[Size])
    {
        AudioFormat = (short)audioFormat;
        NumChannels = (short)numChannels;
        SampleRate = sampleRate;
        BitsPerSample = (short)bitsPerSample;

        // Calculate and set the other fields based on the given values
        BlockAlign = (short)(NumChannels * (BitsPerSample / 8));
        ByteRate = SampleRate * BlockAlign;
    }

    public WaveFormat(byte[] data, int offset)
        : base(data, offset)
    {
    }
}

public enum AudioEncoding : ushort
{
    PCM = 1, // Pulse Code Modulation (Linear PCM)
    IEEE_FLOAT = 3, // IEEE Float
    ALAW = 6, // 8-bit ITU-T G.711 A-law
    MULAW = 7, // 8-bit ITU-T G.711 µ-law
    EXTENSIBLE = 0xFFFE // Determined by SubFormat
                        // Add more encodings as needed
}

public class AviStreamHeader : Chunk
{
    public FourCharacterCode StreamType
    {
        get => (FourCharacterCode)Binary.Read32(Data, 0, Binary.IsBigEndian);
        set => Binary.Write32(Data, 0, Binary.IsBigEndian, (int)value);
    }

    public FourCharacterCode HandlerType
    {
        get => (FourCharacterCode)Binary.Read32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, (int)value);
    }

    public int SampleRate
    {
        get => Binary.Read32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public int Start
    {
        get => Binary.Read32(Data, 12, Binary.IsBigEndian);
        set => Binary.Write32(Data, 12, Binary.IsBigEndian, value);
    }

    public int Length
    {
        get => Binary.Read32(Data, 16, Binary.IsBigEndian);
        set => Binary.Write32(Data, 16, Binary.IsBigEndian, value);
    }

    public int SuggestedBufferSize
    {
        get => Binary.Read32(Data, 20, Binary.IsBigEndian);
        set => Binary.Write32(Data, 20, Binary.IsBigEndian, value);
    }

    public int Quality
    {
        get => Binary.Read32(Data, 24, Binary.IsBigEndian);
        set => Binary.Write32(Data, 24, Binary.IsBigEndian, value);
    }

    public int SampleSize
    {
        get => Binary.Read32(Data, 28, Binary.IsBigEndian);
        set => Binary.Write32(Data, 28, Binary.IsBigEndian, value);
    }

    public int FrameRate
    {
        get => Binary.Read32(Data, 32, Binary.IsBigEndian);
        set => Binary.Write32(Data, 32, Binary.IsBigEndian, value);
    }

    public int Scale
    {
        get => Binary.Read32(Data, 36, Binary.IsBigEndian);
        set => Binary.Write32(Data, 36, Binary.IsBigEndian, value);
    }

    public int Rate
    {
        get => Binary.Read32(Data, 40, Binary.IsBigEndian);
        set => Binary.Write32(Data, 40, Binary.IsBigEndian, value);
    }

    public int StartInitialFrames
    {
        get => Binary.Read32(Data, 44, Binary.IsBigEndian);
        set => Binary.Write32(Data, 44, Binary.IsBigEndian, value);
    }

    public int ExtraDataSize
    {
        get => Binary.Read32(Data, 48, Binary.IsBigEndian);
        set => Binary.Write32(Data, 48, Binary.IsBigEndian, value);
    }

    public AviStreamHeader(RiffWriter writer)
        : base(writer, FourCharacterCode.avih, 56)
    {
    }
}

#endregion

public class RiffWriter : MediaFileWriter
{
    private readonly FourCharacterCode Type;
    private readonly FourCharacterCode SubType;
    private readonly List<Chunk> chunks = new List<Chunk>();

    public override Node Root => chunks[0];

    public override Node TableOfContents => chunks[1];

    public RiffWriter(Uri filename, FourCharacterCode type, FourCharacterCode subType)
        : base(filename, FileAccess.ReadWrite)
    {
        Type = type;
        SubType = subType;

        AddChunk(new RiffChunk(this, Type, SubType, 0));
        //AddChunk(new Chunk(this, FourCharacterCode.LIST, 0));
    }

    internal protected void WriteFourCC(FourCharacterCode fourCC) => WriteInt32LittleEndian((int)fourCC);

    public void AddChunk(Chunk chunk)
    {
        if (chunk == null)
            throw new ArgumentNullException(nameof(chunk));

        chunks.Add(chunk);
        chunk.DataOffset = Position;

        Write(chunk);
    }

    public override IEnumerator<Node> GetEnumerator() => chunks.GetEnumerator();

    public override IEnumerable<Track> GetTracks() => Tracks;

    public override SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        throw new NotImplementedException();
    }
}

public class UnitTests
{
    // Function to generate a simple sine wave sound
    internal static short[] GenerateSineWave(int durationInSeconds, int sampleRate, double frequency)
    {
        int numSamples = durationInSeconds * sampleRate;
        double amplitude = 32760.0; // Max amplitude for 16-bit signed PCM
        double twoPiF = 2.0 * Math.PI * frequency;
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            samples[i] = (short)(amplitude * Math.Sin(twoPiF * t));
        }

        return samples;
    }

    // Convert the short[] audio data to a byte[] for the DataChunk
    internal static byte[] ConvertAudioDataToBytes(short[] audioData)
    {
        byte[] bytes = new byte[audioData.Length * sizeof(short)];
        Buffer.BlockCopy(audioData, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static void TestChunks()
    {
        var riffChunk = new RiffChunk(null, FourCharacterCode.RIFF, FourCharacterCode.WAVE, 0);
        if (riffChunk.ChunkId != FourCharacterCode.RIFF) throw new InvalidOperationException();
        if (!riffChunk.HasSubType) throw new InvalidOperationException();
        if (riffChunk.SubType != FourCharacterCode.WAVE) throw new InvalidOperationException();
    }

    public static void WriteManaged()
    {
        int durationInSeconds = 5;
        int sampleRate = 44100;
        double frequency = 440.0; // A4 note frequency (440 Hz)

        // Generate the audio data (sine wave)
        short[] audioData = GenerateSineWave(durationInSeconds, sampleRate, frequency);

        // Put in Media/Audio/wav so we can read it.
        string localPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Media/Audio/wav/";

        // Replace with your desired output file path
        string outputFilePath = Path.GetFullPath(localPath + "twinkle_twinkle_little_star_managed.wav");

        System.IO.File.WriteAllBytes(outputFilePath, Common.MemorySegment.Empty.Array);

        // Create the RiffWriter with the appropriate type and subtype for Wave files.
        using (RiffWriter writer = new RiffWriter(new Uri("file://" + outputFilePath), FourCharacterCode.RIFF, FourCharacterCode.WAVE))
        {
            // Create the necessary chunks for the Wave file.
            // Note: We will use default values for FmtChunk since they are not important for this example.
            FmtChunk fmtChunk = new FmtChunk(writer, 1, 1, (uint)sampleRate, 16); // 1 channel, 16 bits per sample

            // Add the audio data (samples) to the DataChunk.
            using (DataChunk dataChunk = new DataChunk(writer, ConvertAudioDataToBytes(audioData)))
            {
                // Add the chunks to the RiffWriter.
                writer.AddChunk(fmtChunk);
                writer.AddChunk(dataChunk);
            }
        }

        Console.WriteLine("Wave file written successfully!");
    }

    // Sample method to generate audio data (Twinkle Twinkle Little Star).
    public static byte[] GenerateTwinkleTwinkleLittleStar()
    {
        // Sample audio data for Twinkle Twinkle Little Star
        byte[] audioData = new byte[]
        {
                52, 52, 59, 59, 66, 66, 59, 52,
                52, 59, 59, 66, 66, 59, 52,
                66, 66, 78, 78, 88, 88, 78, 66,
                78, 78, 88, 88, 78, 66, 66, 59,
                59, 52, 52, 59, 59, 66, 66, 59, 52,
                66, 66, 78, 78, 88, 88, 78, 66,
                78, 78, 88, 88, 78, 66, 59, 59,
                52, 52, 59, 59, 66, 66, 59, 52
        };

        return audioData;
    }

    public static void WriteRaw()
    {
        var audioData = GenerateTwinkleTwinkleLittleStar();

        // Put in Media/Audio/wav so we can read it.
        string localPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Media/Audio/wav/";

        // Replace with your desired output file path
        string outputFilePath = Path.GetFullPath(localPath + "twinkle_twinkle_little_star_raw.wav");

        System.IO.File.WriteAllBytes(outputFilePath, Common.MemorySegment.Empty.Array);

        // Create the RiffFileWriter and WaveFileHeader
        using (var writer = new RiffWriter(new Uri("file://" + outputFilePath), FourCharacterCode.RIFF, FourCharacterCode.WAVE))
        {
            // Create the necessary chunks for the Wave file
            FmtChunk fmtChunk = new FmtChunk(writer, 1, 1, 44100, 16); // 1 channel, 16 bits per sample
            DataChunk dataChunk = new DataChunk(writer, audioData);

            writer.AddChunk(fmtChunk);
            writer.AddChunk(dataChunk);
        }

        Console.WriteLine("Wave file written successfully!");
    }

    // Sample audio data for "Row, Row, Row Your Boat"
    public static short[] GenerateRowYourBoat()
    {
        double amplitude = 0.3; // Adjust the amplitude to control the volume
        int sampleRate = 44100;
        int durationMs = 500;
        int numSamples = (durationMs * sampleRate) / 1000;

        // The musical notes of the song (D, D, E, D, F, E)
        double[] frequencies = { 293.66, 293.66, 329.63, 293.66, 349.23, 329.63 };

        short[] audioData = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double time = i / (double)sampleRate;
            int noteIndex = (int)((time / durationMs) * frequencies.Length);
            double frequency = frequencies[noteIndex];

            double sineWave = amplitude * Math.Sin(2 * Math.PI * frequency * time);

            // Convert the double sample value to a 16-bit PCM value (-32768 to 32767)
            audioData[i] = (short)(sineWave * short.MaxValue);
        }

        return audioData;
    }
}