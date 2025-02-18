﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;






namespace cgshop
{
    public enum FilterEntryType
    {
        Graph,
        Function,
        Convolution,
    }

    public class FilterEntry
    {
        public FilterEntryType Type { get; }
        public string Name { get; set; }
        public IFilter Filter { get; }

        public FilterEntry(FilterEntryType type, string name, IFilter filter)
        {
            this.Type = type;
            this.Name = name;
            this.Filter = filter;
        }
    }



    public interface IFilter
    {
        unsafe BitmapImage Apply(BitmapImage original);
    }

    [Serializable]
    public class FunctionFilter : IFilter
    {
        public FunctionFormula Function { get; set; }

        public FunctionFilter(FunctionFormula function)
        {
            this.Function = function;
        }

        public unsafe BitmapImage Apply(BitmapImage original)
        {
            return Function.Apply(original);
        }
    }

    [Serializable]
    public class ConvolutionFilter : IFilter
    {
        public double[,] Kernel { get; }
        public int KernelWidth { get; } // Column count
        public int KernelHeight { get; } // Row count
        public double Divisor { get; }

        public ConvolutionFilter(double[,] kernel, double divisor)
        {
            this.Kernel = kernel;
            this.KernelWidth = kernel.GetLength(1);
            this.KernelHeight = kernel.GetLength(0);
            this.Divisor = divisor;
        }

        public unsafe BitmapImage Apply(BitmapImage original)
        {
            if (Divisor == 0)
                throw new ArgumentException("Convolution filter divisor cannot be equal to 0");

            var bitmap = new WriteableBitmap(original);

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int stride = bitmap.BackBufferStride;
            int bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;


            bitmap.Lock();

            byte[] newBuffer = new byte[width * height * bytesPerPixel];

            unsafe
            {
                byte* pBuffer = (byte*)bitmap.BackBuffer.ToPointer(); // Pointer to actual image data in buffer (BGRA32 format (1 byte for each channel))

                int kernelFirstRow = -(KernelHeight - 1) / 2;
                int kernelLastRow = (KernelHeight - 1) / 2;
                int kernelFirstColumn = -(KernelWidth - 1) / 2;
                int kernelLastColumn = (KernelWidth - 1) / 2;

                for (int y = 0; y < height; y++) // Row
                {
                    for (int x = 0; x < width; x++) // Column
                    {
                        double[] accumulatedValue = new double[4] { 0, 0, 0, 0 };

                        for (int i = 0; i < 4; i++) // Channels
                        {
                            if (i != 3) // Color channels
                            {

                                for (int yK = kernelFirstRow; yK <= kernelLastRow; yK++) // Row
                                {
                                    for (int xK = kernelFirstColumn; xK <= kernelLastColumn; xK++) // Column
                                    {
                                        int neighbourPixelX = x + xK;
                                        int neighbourPixelY = y + yK;

                                        int neighbourPixelIndex = (4 * neighbourPixelX + (neighbourPixelY * bitmap.BackBufferStride));

                                        var kernelValue = (int)Kernel[yK - kernelFirstRow, xK - kernelFirstColumn];
                                        if (neighbourPixelX < 0 || neighbourPixelX > width - 1 || neighbourPixelY < 0 || neighbourPixelY > height - 1) // Image edge case
                                        {
                                            accumulatedValue[i] += (int)pBuffer[4 * x + (y * bitmap.BackBufferStride) + i]; // Edge behaviour
                                        }
                                        else
                                        {
                                            accumulatedValue[i] += kernelValue * pBuffer[neighbourPixelIndex + i]; // Add value of current channel of current pixel
                                        }
                                    }
                                }
                            }
                            else // Alpha channel
                            {
                                int index = 4 * x + (y * bitmap.BackBufferStride) + i;
                                accumulatedValue[i] = pBuffer[index];
                            }
                        }

                        for (int i = 0; i < 4; i++) // Channels
                        {
                            int index = 4 * x + (y * bitmap.BackBufferStride) + i;

                            if (i != 3) // Color
                            {
                                newBuffer[index] = Convert.ToByte(Utils.Clamp((int)(accumulatedValue[i] / Divisor), 0, 255));
                            }
                            else  // Alpha
                            {
                                newBuffer[index] = Convert.ToByte(Utils.Clamp((int)accumulatedValue[i], 0, 255));
                            }
                        }
                    }
                }
            }

            bitmap.Unlock();

            // Create new bitmap from create buffer
            var newBitmap = BitmapImage.Create((int)width, (int)height, original.DpiX, original.DpiY, bitmap.Format, null, newBuffer, (int)bytesPerPixel * width);

            // Convert WritableBitmap to BitmapImage
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(newBitmap));
                encoder.Save(stream);
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            return bitmapImage;
        }

    }

    [Serializable]
    public class FunctionFormula
    {
        public FilterSettings.FunctionFormula_Formula Formula { get; }
        public object[] otherFunctionParams;

        public FunctionFormula(FilterSettings.FunctionFormula_Formula functionFormula, params object[] otherFunctionParams)
        {
            this.Formula = functionFormula;
            this.otherFunctionParams = otherFunctionParams;
        }

        public unsafe BitmapImage Apply(BitmapImage original)
        {
            var bitmap = new WriteableBitmap(original);

            //Console.WriteLine(bitmap.BackBufferStride);
            //Console.WriteLine(bitmap.Format.BitsPerPixel.ToString());
            //Console.WriteLine(bitmap.BackBufferStride);
            //Console.WriteLine(bitmap.DpiX);

            //32
            //3204
            //801

            bitmap.Lock();

            byte* pBuffer = (byte*)bitmap.BackBuffer; // Pointer to actual image data in buffer (BGRA32 format (1 byte for each channel))
            Formula(pBuffer, bitmap, otherFunctionParams);

            bitmap.Unlock();

            // Convert WritableBitmap to BitmapImage
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }

            return bitmapImage;
        }
    }

}
