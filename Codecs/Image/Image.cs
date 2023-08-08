﻿using Media.Codec;
using Media.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Media.Codecs.Image
{
    public class Image : Media.Codec.MediaBuffer
    {
        const float DefaultDpi = 96.0f;

        #region Statics

        //static Image Crop(Image source)

        internal static int CalculateSize(ImageFormat format, int width, int height)
        {
            //The total size in bytes
            int totalSize = 0;

            //Iterate each component in the ColorSpace
            for (int i = 0, ie = format.Components.Length; i <ie ; ++i)
            {
                //Increment the total size in bytes by calculating the size in bytes of that plane using the ColorSpace information
                totalSize += (width >> format.Widths[i]) * (height >> format.Heights[i]);
            }

            //Return the amount of bytes
            return totalSize;
        }

        #endregion

        #region Fields

        public readonly int Width;

        public readonly int Height;
        
        #endregion

        #region Constructor

        //Needs a subsampling...

        public Image(ImageFormat format, int width, int height)
            : base(format, CalculateSize(format, width, height))
        {
            Width = width;

            Height = height;
        }

        public Image(ImageFormat format, int width, int height, byte[] data)
            : base(format, new Common.MemorySegment(data))
        {
            Width = width;

            Height = height;
        }

        #endregion

        #region Properties

        public IEnumerable<Common.MemorySegment> this[int x, int y]
        {
            get
            {
                if (x < 0 || y < 0 || x > Width || y > Height)
                    yield break;

                //Loop each component and return the segment which corresponds to the data at the offset for that component
                for (int c = 0, ce = ImageFormat.Components.Length; c < ce; ++c)
                    yield return new Common.MemorySegment(Data.Array, Data.Offset + CalculateComponentDataOffset(x, y, c));
            }
            set
            {
                if (x < 0 || y < 0 || x > Width || y > Height)
                    return;

                int componentIndex = 0;

                foreach(var componentData in value)
                    SetComponentData(x, y, componentIndex++, componentData);
            }
        }

        public ImageFormat ImageFormat { get { return MediaFormat as ImageFormat; } }

        public int Planes { get { return MediaFormat.Components.Length; } }

        #endregion

        #region Methods

        public void SaveBitmap(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            int width = Width;
            int height = Height;

            //Should be a 4CC indicating the image but...
            var compressionFormat = 0;// Binary.Read32(ImageFormat.Components.Select(c => (byte)char.ToUpper((char)c.Id)).ToArray(), 0, Binary.IsBigEndian);

            // Convert pixels to meters: 1 inch = 0.0254 meters
            float horizontalResolutionMeters = width / DefaultDpi * 0.0254f;
            float verticalResolutionMeters = height / DefaultDpi * 0.0254f;

            // Convert meters to pixels per meter
            int xpelsPerMeter = (int)Math.Round(1.0f / horizontalResolutionMeters);
            int ypelsPerMeter = (int)Math.Round(1.0f / verticalResolutionMeters);

            // Create a new BitmapInfoHeader based on the ImageFormat
            BitmapInfoHeader header = new BitmapInfoHeader(width, height, (short)ImageFormat.Components.Length, (short)ImageFormat.Size, compressionFormat, Data.Array.Length, xpelsPerMeter, ypelsPerMeter, 0, 0);
            SaveBitmap(header, stream);
        }

        public void SaveBitmap(BitmapInfoHeader header, Stream stream)
        {
            int fileSize = 54 + header.Count + Data.Array.Length;

            // BMP file header
            byte[] fileHeader = new byte[14]
            {
                0x42, 0x4D,                       // "BM" - BMP file identifier
                (byte)(fileSize & 0xFF),          // File size (low byte)
                (byte)((fileSize >> 8) & 0xFF),   // File size
                (byte)((fileSize >> 16) & 0xFF),  // File size
                (byte)((fileSize >> 24) & 0xFF),  // File size (high byte)
                0x00, 0x00,                       // Reserved
                0x00, 0x00,                       // Reserved
                0x36, 0x00, 0x00, 0x00            // Offset of the image data (54 bytes)
            };

            // Write the BMP file header to the stream
            stream.Write(fileHeader, 0, fileHeader.Length);
            stream.Write(header.Array, header.Offset, header.Count);
            stream.Write(Data.Array, Data.Offset, Data.Count);
        }

        //private int GetComponentDataOffset(int x, int y, int componentIndex)
        //{
        //    if (x < 0 || y < 0 || x >= Width || y >= Height || componentIndex >= ImageFormat.Components.Length)
        //        return -1;

        //    switch (DataLayout)
        //    {
        //        default:
        //        case Media.Codec.DataLayout.Unknown:
        //            throw new System.ArgumentException("Invalid DataLayout");

        //        case Media.Codec.DataLayout.Packed:
        //            return (y * Width + x) * ImageFormat.Size;

        //        case Media.Codec.DataLayout.Planar:
        //            int planeOffset = 0;
        //            for (int c = 0; c < componentIndex; c++)
        //            {
        //                if (ImageFormat.Widths[c] < 0 || ImageFormat.Heights[c] < 0)
        //                    continue;

        //                planeOffset += PlaneLength(c);
        //            }

        //            int xOffset = x >> ImageFormat.Widths[componentIndex];
        //            int yOffset = y >> ImageFormat.Heights[componentIndex];
        //            return planeOffset + yOffset * PlaneWidth(componentIndex) + xOffset;

        //        case Media.Codec.DataLayout.SemiPlanar:
        //            // SemiPlanar is similar to Planar, but all planes are packed together in the order of YUV.
        //            int semiPlanarOffset = 0;
        //            for (int c = 0; c < componentIndex; c++)
        //            {
        //                int componentSize = Common.Binary.BitsToBytes(ImageFormat.Components[c].Size);
        //                semiPlanarOffset += componentSize * Width * Height;
        //            }

        //            int semiPlanarXOffset = x >> ImageFormat.Widths[componentIndex];
        //            int semiPlanarYOffset = y >> ImageFormat.Heights[componentIndex];
        //            return semiPlanarOffset + semiPlanarYOffset * PlaneWidth(componentIndex) + semiPlanarXOffset;
        //    }
        //}

        public int CalculateComponentDataOffset(int x, int y, int componentIndex)
        {
            if (componentIndex < 0 || componentIndex >= ImageFormat.Components.Length)
                return -1;

            int offset = 0;

            for (int c = 0; c < componentIndex; c++)
            {
                var component = ImageFormat.Components[c];

                offset += component.Length;

                int widthSampling = ImageFormat.Widths[c];
                int heightSampling = ImageFormat.Heights[c];

                if (DataLayout == Media.Codec.DataLayout.Planar && widthSampling > 0 && heightSampling > 0)
                {
                    int planeWidth = Width >> widthSampling;
                    int planeX = x >> widthSampling;
                    int planeY = y >> heightSampling;

                    offset += (planeY * planeWidth + planeX) * ImageFormat.Components[c].Length;
                }
            }

            // Check for sub-sampling.
            if (DataLayout == Media.Codec.DataLayout.Planar && ImageFormat.Widths[componentIndex] > 0 && ImageFormat.Heights[componentIndex] > 0)
            {
                int planeWidth = Width >> ImageFormat.Widths[componentIndex];
                int planeX = x >> ImageFormat.Widths[componentIndex];
                int planeY = y >> ImageFormat.Heights[componentIndex];

                offset += (planeY * planeWidth + planeX) * ImageFormat.Components[componentIndex].Length;
            }
            else
            {
                offset += (y * Width + x) * ImageFormat.Components[componentIndex].Length;
            }

            return offset;
        }

        public SegmentStream GetSampleData(int x, int y)
        {
            var result = new SegmentStream();
            foreach(var component in ImageFormat.Components)
                result.AddMemory(GetComponentData(x, y, component));
            return result;
        }

        public MemorySegment GetComponentData(int x, int y, MediaComponent component)
        {
            int componentIndex = GetComponentIndex(component);

            if (componentIndex < 0) return MemorySegment.Empty;
                //throw new ArgumentException("Invalid component specified.");

            int offset = CalculateComponentDataOffset(x, y, componentIndex);

            if (offset < 0) return MemorySegment.Empty;
                //throw new IndexOutOfRangeException("Coordinates are outside the bounds of the image.");

            return new MemorySegment(Data.Array, offset, component.Length);
        }

        public int PlaneWidth(int plane)
        {
            if (plane >= MediaFormat.Components.Length) return -1;

            return Width >> ImageFormat.Widths[plane];
        }

        public int PlaneHeight(int plane)
        {
            if (plane >= MediaFormat.Components.Length) return -1;

            return Height >> ImageFormat.Heights[plane];
        }

        /// <summary>
        /// Gets the amount of bits in the given plane
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        public int PlaneSize(int plane)
        {
            if (plane >= MediaFormat.Components.Length) return -1;

            return (PlaneWidth(plane) + PlaneHeight(plane)) * ImageFormat.Size;
        }

        /// <summary>
        /// Gets the amount of bytes in the given plane.
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        public int PlaneLength(int plane)
        {
            if (plane >= MediaFormat.Components.Length) return -1;

            return Common.Binary.BitsToBytes(PlaneSize(plane));
        }

        public void SetComponentData(int x, int y, int componentIndex, MemorySegment data)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return; // or throw an exception

            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            if (offset < 0)
                return; // or throw an exception

            Buffer.BlockCopy(data.Array, data.Offset, Data.Array, Data.Offset + offset, data.Count);
        }

        public void SetComponentData(int x, int y, int componentIndex, Vector<byte> componentData)
        {
            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            int vectorSize = Vector<byte>.Count;

            if (offset < 0 || offset + vectorSize > Data.Count)
            {
                throw new ArgumentOutOfRangeException("componentData", "ComponentData doesn't fit in the image buffer.");
            }

            componentData.CopyTo(new Span<byte>(Data.Array, offset, vectorSize));
        }


        // Set the value of a specific component at the given (x, y) coordinates
        public void SetComponentData(int x, int y, byte componentId, MemorySegment data)
        {
            int componentIndex = GetComponentIndex(componentId);
            if (componentIndex == -1)
                return;

            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            if (offset == -1)
            {
                // Component doesn't exist in this format, return without setting anything.
                return;
            }

            // Make sure the data has the correct length for the component.
            int componentLength = ImageFormat.Components[componentIndex].Length;
            if (data.Count != componentLength)
            {
                throw new ArgumentException($"Invalid data length for component {componentId}. Expected length: {componentLength} bytes.", nameof(data));
            }

            // Copy the data into the image's memory buffer at the correct offset.
            Buffer.BlockCopy(data.Array, data.Offset, Data.Array, Data.Offset + offset, data.Count);
        }

        public MemorySegment GetComponentData(int x, int y, byte componentId)
        {
            int componentIndex = GetComponentIndex(componentId);
            if (componentIndex == -1)
            {
                throw new ArgumentException("Invalid component ID.", nameof(componentId));
            }

            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            if (offset == -1)
            {
                // Component doesn't exist in this format, return an empty MemorySegment.
                return MemorySegment.Empty;
            }

            // Now, you have the offset, so you can return a MemorySegment representing the component data.
            return new MemorySegment(Data.Array, Data.Offset + offset, ImageFormat.Components[componentIndex].Length);
        }

        public Vector<byte> GetComponentData(int x, int y, int componentIndex)
        {
            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            int vectorSize = Vector<byte>.Count;

            if (offset < 0 || offset + vectorSize > Data.Count)
            {
                throw new ArgumentOutOfRangeException("componentIndex", "Invalid component index or component data does not fit in the image buffer.");
            }

            return new Vector<byte>(Data.Array, offset);
        }


        #endregion
    }

    //Drawing?

    //Will eventually need Font support...
}


namespace Media.UnitTests
{
    /// <summary>
    /// Provides tests which ensure the logic of the Image class is correct
    /// </summary>
    internal class ImageUnitTests
    {
        public static void TestConstructor()
        {
            using (Media.Codecs.Image.Image image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 1, 1))
            {
                if (image.SampleCount != 1) throw new System.InvalidOperationException();

                if (image.Data.Count != image.Width * image.Height * image.MediaFormat.Length) throw new System.InvalidOperationException();
            }
        }

        public static void TestGetComponentData()
        {
            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 100, 100))
            {
                for(int x = 0; x < image.Width; x++)
                {
                    for(int y = 0; y < image.Height; y++)
                    {
                        for (int c = 0; c < image.ImageFormat.Length; c++)
                        {
                            var component = image.ImageFormat[c];

                            var data = image.GetComponentData(x, y, component);

                            if (data.Count != component.Length) throw new InvalidOperationException();

                            Array.Fill(data.Array, byte.MaxValue);

                            image.SetComponentData(x, y, c, data);
                            
                            data = image.GetComponentData(x, y, component);

                            if (data.Array.Any(b => b != byte.MaxValue)) throw new Exception("Did not set Component data");
                        }
                    }
                }

                if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new Exception("Did not set Component data");
            }
        }

        public static void TestSave()
        {
            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 696, 564))
            {
                Array.Fill(image.Data.Array, byte.MaxValue);

                using (var outputBmpStream = new System.IO.FileStream("output_rgb.bmp", FileMode.OpenOrCreate))
                {
                    image.SaveBitmap(outputBmpStream);
                }
            }


            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGBA(8), 696, 564))
            {
                Array.Fill(image.Data.Array, byte.MaxValue);

                using (var outputBmpStream = new System.IO.FileStream("output_rgba.bmp", FileMode.OpenOrCreate))
                {
                    image.SaveBitmap(outputBmpStream);
                }
            }
        }

        //Averages around 1000 msec or 1 sec, way to slow
        //Even with Parallel its about 200 msec or .24 sec
        public static void TestConversionRGB()
        {
            int testHeight = 1920, testWidth = 1080;

            System.DateTime start = System.DateTime.UtcNow, end;

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), testWidth, testHeight))
            {
                if (rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, testWidth, testHeight))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RGB(rgbImage, yuvImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YUV(yuvImage, rgbImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        //Averages around 100 msec, 0.1 sec, still 60 times would be 600 msec or .6 seconds, just fast enough
        public static void TestUnsafeConversionRGB()
        {
            int testHeight = 1920, testWidth = 1080;

            System.DateTime start = System.DateTime.UtcNow, end;

            Codecs.Image.Image yuvImage;

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), testWidth, testHeight))
            {
                if (rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //ImageFormat could be given directly to constructor here

                //Create the destination image

                //Cache the data of the source before transformation
                byte[] left = rgbImage.Data.ToArray(), right;

                //Transform RGB to YUV

                start = System.DateTime.UtcNow;

                unsafe
                {
                    fixed (byte* ptr = rgbImage.Data.Array)
                    {
                        yuvImage = new Codecs.Image.Image(Yuv420P, testWidth, testHeight, Media.Codecs.Image.ColorConversions.RGBToYUV420Managed(rgbImage.Width, rgbImage.Height, (System.IntPtr)ptr));
                    }

                }

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                //Transform YUV to RGB

                start = System.DateTime.UtcNow;

                //it.Transform();

                Media.Codecs.Image.ColorConversions.YUV2RGBManaged(yuvImage.Data.Array, rgbImage.Data.Array, rgbImage.Width >> 1, rgbImage.Height >> 1);

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                //Rgb Data
                right = rgbImage.Data.ToArray();

                //Compare the two sequences
                //if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionARGB()
        {
            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.ARGB(8), 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                //ImageFormat be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionRGBA()
        {
            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGBA(8), 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionRGAB()
        {

            Codecs.Image.ImageFormat weird = new Codecs.Image.ImageFormat(Common.Binary.ByteOrder.Little, Codec.DataLayout.Packed, new Codec.MediaComponent[]
            {
                new Codec.MediaComponent((byte)'r', 10),
                new Codec.MediaComponent((byte)'g', 10),
                new Codec.MediaComponent((byte)'a', 2),
                new Codec.MediaComponent((byte)'b', 10),
                
            });

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new Codecs.Image.Image(weird, 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                if (rgbImage.ImageFormat.AlphaComponent.Size != 2) throw new System.Exception("AlphaComponent.Size should be 2 (bits)");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new Codecs.Image.Image(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }
    }
}