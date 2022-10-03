using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Image_Processing
{
    public struct SeamPixel
    {
        public int x;
        public int y;
        public float energy;
        public SeamPixel(int _x, int _y, float _energy)
        {
            x = _x;
            y = _y;
            energy = _energy;
        }
    }
    public partial class Form1 : Form
    {
        private int[,] prewittx = new int[3, 3]
            {
                { 5,  5,  5 },
                {-3,  0, -3 },
                {-3, -3, -3 }
            };
        private int[,] prewitty = new int[3, 3]
            {
                { 5, -3, -3 },
                { 5,  0, -3 },
                { 5, -3, -3 }
            };
        private int[,] sobelx = new int[3, 3]
            {
                { 1,  0, -1 },
                { 2,  0, -2 },
                { 1,  0, -1 }
            };
        private int[,] sobely = new int[3, 3]
            {
                { 1,  2,  1 },
                { 0,  0,  0 },
                {-1, -2, -1 }
            };
        private int[,] laplacian3x3 = new int[3, 3]
            {
                {-1, -1, -1 },
                {-1,  8, -1 },
                {-1, -1, -1 }
            };
        private int[,] gaussian3x3 = new int[3, 3]
            {
                { 0, -1,  0 },
                {-1,  4, -1 },
                { 0, -1,  0 }
            };

        public static Image<Rgba32> outputImage;
        public static Image<Rgba32> energyImage;
        public static Image<Rgba32> pathImage;

        public int xClipAmount = 10;
        public int yClipAmount = 1;

        bool isOperationAborted = false;
        private Image<Rgba32> Gradient1(Image<Rgba32> image)
        {
            for (int i = 0; i < image.Width; i++)
            {
                for (int y = 0; y < image.Height; y++)
                {

                    image[i, y] = Color.FromRgb(byte.Parse(Clamp(i, 0, 255).ToString()), byte.Parse(Clamp(y, 0, 255).ToString()), 100);
                }
            }
            return image;
        }
        private float Min(params float[] numbers)
        {
            float min = numbers[0];

            for (int i = 0; i < numbers.Length - 1; i++)
            {
                if (min > numbers[i])
                {
                    min = numbers[i];
                }
            }

            return min;
        }
        private Image<Rgba32> AddImages(Image<Rgba32> image1, Image<Rgba32> image2)
        {
            int minX = (int)Min(image1.Width, image2.Width);
            int minY = (int)Min(image1.Height, image2.Height);

            Image<Rgba32> resultImage = new Image<Rgba32>(minX,minY);

            for (int x = 0; x < minX; x++)
            {
                for (int y = 0; y < minY; y++)
                {
                    float r = image1[x, y].R + image2[x, y].R;
                    float g = image1[x, y].G + image2[x, y].G;
                    float b = image1[x, y].B + image2[x, y].B;
                    resultImage[x, y] = new Rgba32(r / 512, g / 512, b / 512);
                }
            }

            return resultImage;
        }
        private Image<Rgba32> ScaleImage(Image<Rgba32> image, float multiplier)
        {
            image.Mutate(c => c.Resize((int)(image.Width * multiplier),(int)(image.Height * multiplier)));
            return image;
        }
        private Image<Rgba32> ToGreyScale(Image<Rgba32> sourceImage)
        {
            Image<Rgba32> image = sourceImage.Clone();
            for (int i = 0; i < image.Width; i++)
            {
                for (int y = 0; y < image.Height; y++)
                {          
                    byte r = image[i, y].R;
                    byte g = image[i, y].G;
                    byte b = image[i, y].B;
                    byte gray = (byte)((r + g + b) / 3);
                    image[i, y] = new Rgba32(gray, gray, gray);
                }
            }
            return image;
        }
        private int Clamp(int value, int min, int max)
        {
            if (value > max)
            {
                value = max;
            }
            if (value < min)
            {
                value = min;
            }
            return value;
        }
        private int Scale(float value, int a, int b)
        {
            float percent = b / a;
            value /= percent;
            int v = ((int)value);
            return v;
        }
        private Image<Rgba32> DetectEdges(Image<Rgba32> sourceImage, int[,] kernel)
        {           
            Image<Rgba32> image = sourceImage.Clone();
            Image<Rgba32> resultImage = new Image<Rgba32>(image.Width, image.Height);

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    int[,] projectionKernel = new int[3, 3];

                    projectionKernel[1, 1] = image[x, y].R;
                    if (x == 0 && y == 0)
                    {
                        projectionKernel[0, 0] = image[x, y].R;
                        projectionKernel[1, 0] = image[x, y].R;
                        projectionKernel[2, 0] = image[x + 1, y].R;

                        projectionKernel[0, 1] = image[x, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x + 1, y].R;

                        projectionKernel[0, 2] = image[x, y + 1].R;
                        projectionKernel[1, 2] = image[x, y + 1].R;
                        projectionKernel[2, 2] = image[x + 1, y + 1].R;
                    }
                    else if (x == image.Width - 1 && y == 0)
                    {
                        projectionKernel[0, 0] = image[x - 1, y].R;
                        projectionKernel[1, 0] = image[x, y].R;
                        projectionKernel[2, 0] = image[x, y].R;

                        projectionKernel[0, 1] = image[x - 1, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x, y].R;

                        projectionKernel[0, 2] = image[x - 1, y + 1].R;
                        projectionKernel[1, 2] = image[x, y + 1].R;
                        projectionKernel[2, 2] = image[x, y + 1].R;
                    }
                    else if (x == image.Width - 1 && y == image.Height - 1)
                    {
                        projectionKernel[0, 0] = image[x - 1, y - 1].R;
                        projectionKernel[1, 0] = image[x, y - 1].R;
                        projectionKernel[2, 0] = image[x, y - 1].R;

                        projectionKernel[0, 1] = image[x - 1, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x, y].R;

                        projectionKernel[0, 2] = image[x - 1, y].R;
                        projectionKernel[1, 2] = image[x, y].R;
                        projectionKernel[2, 2] = image[x, y].R;
                    }
                    else if (x == 0 && y == image.Height - 1)
                    {
                        projectionKernel[0, 0] = image[x, y - 1].R;
                        projectionKernel[1, 0] = image[x, y - 1].R;
                        projectionKernel[2, 0] = image[x + 1, y - 1].R;

                        projectionKernel[0, 1] = image[x, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x + 1, y].R;

                        projectionKernel[0, 2] = image[x, y].R;
                        projectionKernel[1, 2] = image[x, y].R;
                        projectionKernel[2, 2] = image[x + 1, y].R;
                    }
                    else if (y == 0)
                    {
                        projectionKernel[0, 0] = image[x - 1, y].R;
                        projectionKernel[1, 0] = image[x, y].R;
                        projectionKernel[2, 0] = image[x + 1, y].R;

                        projectionKernel[0, 1] = image[x - 1, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x + 1, y].R;

                        projectionKernel[0, 2] = image[x - 1, y + 1].R;
                        projectionKernel[1, 2] = image[x, y + 1].R;
                        projectionKernel[2, 2] = image[x + 1, y + 1].R;
                    }
                    else if (y == image.Height - 1)
                    {
                        projectionKernel[0, 0] = image[x - 1, y - 1].R;
                        projectionKernel[1, 0] = image[x, y - 1].R;
                        projectionKernel[2, 0] = image[x + 1, y - 1].R;

                        projectionKernel[0, 1] = image[x - 1, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x + 1, y].R;

                        projectionKernel[0, 2] = image[x - 1, y].R;
                        projectionKernel[1, 2] = image[x, y].R;
                        projectionKernel[2, 2] = image[x + 1, y].R;
                    }
                    else if (x == 0)
                    {
                        projectionKernel[0, 0] = image[x, y - 1].R;
                        projectionKernel[1, 0] = image[x, y - 1].R;
                        projectionKernel[2, 0] = image[x + 1, y - 1].R;

                        projectionKernel[0, 1] = image[x, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x + 1, y].R;

                        projectionKernel[0, 2] = image[x, y + 1].R;
                        projectionKernel[1, 2] = image[x, y + 1].R;
                        projectionKernel[2, 2] = image[x + 1, y + 1].R;
                    }
                    else if (x == image.Width - 1)
                    {
                        projectionKernel[0, 0] = image[x - 1, y - 1].R;
                        projectionKernel[1, 0] = image[x, y - 1].R;
                        projectionKernel[2, 0] = image[x, y - 1].R;

                        projectionKernel[0, 1] = image[x - 1, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x, y].R;

                        projectionKernel[0, 2] = image[x - 1, y + 1].R;
                        projectionKernel[1, 2] = image[x, y + 1].R;
                        projectionKernel[2, 2] = image[x, y + 1].R;
                    }
                    else
                    {
                        projectionKernel[0, 0] = image[x - 1, y - 1].R;
                        projectionKernel[1, 0] = image[x, y - 1].R;
                        projectionKernel[2, 0] = image[x + 1, y - 1].R;

                        projectionKernel[0, 1] = image[x - 1, y].R;
                        projectionKernel[1, 1] = image[x, y].R;
                        projectionKernel[2, 1] = image[x + 1, y].R;

                        projectionKernel[0, 2] = image[x - 1, y + 1].R;
                        projectionKernel[1, 2] = image[x, y + 1].R;
                        projectionKernel[2, 2] = image[x + 1, y + 1].R;
                    }

                    float brightness = ConvoluteKernels3x3(projectionKernel, kernel);
                    resultImage[x, y] = new Rgba32(brightness,brightness,brightness);
                }
            }
            image.Dispose();
            return resultImage;
        }
        private Image<Rgba32> DetectEdges(Image<Rgba32> sourceImage, int[,] kernelX, int[,] kernelY)
        {
            Image<Rgba32> imageX = DetectEdges(ToGreyScale(sourceImage), kernelX);
            Image<Rgba32> imageY = DetectEdges(ToGreyScale(sourceImage), kernelY);
            return AddImages(imageX, imageY);
        }
        private Image<Rgba32> CreateSeamPathMapVertical(Image<Rgba32> sourceImage)
        {
            //Image<Rgba32> copy = sourceImage.Clone();
            Image<Rgba32> resultImage = sourceImage.Clone();

            for (int x = 0; x < resultImage.Width; x++)
            {
                for (int y = resultImage.Height - 1; y >= 0; y--)
                {
                    float min = 0;
                    if (y != resultImage.Height - 1)
                    {
                        if (x == 0)
                        {
                            min = Min(resultImage[x, y + 1].R, resultImage[x + 1, y + 1].R);                            
                        }
                        else if (x == resultImage.Width - 1)
                        {
                            min = Min(resultImage[x - 1,y + 1].R, resultImage[x, y + 1].R);
                        }
                        else
                        {
                            min = Min(resultImage[x - 1, y + 1].R, resultImage[x, y + 1].R, resultImage[x + 1, y + 1].R);
                        }
                        float brightness = min + resultImage[x, y].R;
                        resultImage[x, y] = new Rgba32(brightness / 256, brightness / 256, brightness / 256);
                    }
                }
            }

            return resultImage;
        }
        private Image<Rgba32> CreateSeamPathMapHorizontal(Image<Rgba32> sourceImage)
        {
            //Image<Rgba32> copy = sourceImage.Clone();
            Image<Rgba32> resultImage = sourceImage.Clone();

            for (int x = resultImage.Width - 1; x >= 0; x--)
            {
                for (int y = 0; y < resultImage.Height; y++)
                {
                    float min = 0;
                    if (x != resultImage.Width - 1)
                    {
                        if (y == 0)
                        {
                            min = Min(resultImage[x + 1, y].R, resultImage[x + 1, y + 1].R);
                        }
                        else if (y == resultImage.Height - 1)
                        {
                            min = Min(resultImage[x + 1, y].R, resultImage[x + 1, y - 1].R);
                        }
                        else
                        {
                            min = Min(resultImage[x + 1, y].R, resultImage[x + 1, y - 1 ].R, resultImage[x + 1, y + 1].R);
                        }
                        float brightness = min + resultImage[x, y].R;
                        resultImage[x, y] = new Rgba32(brightness / 256, brightness / 256, brightness / 256);
                    }
                }
            }

            return resultImage;
        }
        float ConvoluteKernels3x3(int[,] kernel1, int[,] kernel2)
        {
            float result = 0;

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    result += kernel1[x, y] * kernel2[x, y];
                }
            }
            result /= 256f;
            return result;
        }
        
        private Image<Rgba32> DeleteVerticalSeam(Image<Rgba32> sourceEnergy, Image<Rgba32> sourceImage)
        {
            Image<Rgba32> pathMap = CreateSeamPathMapVertical(sourceEnergy);
            SeamPixel[] seam = new SeamPixel[pathMap.Height];
            float min = 0;
            for (int i = 0; i < pathMap.Width; i++)
            {
                if (i == 0)
                {
                    min = pathMap[i, 0].R;
                    seam[0] = new SeamPixel(i, 0, min);
                    
                }
                if (min > pathMap[i, 0].R)
                {
                    min = pathMap[i, 0].R;
                    seam[0] = new SeamPixel(i, 0, min);
                }
            }
            for (int y = 0; y < pathMap.Height - 1; y++)
            {
                float center = pathMap[seam[y].x, seam[y].y + 1].R;
                float left = 256;
                float right = 256;
                if (seam[y].x == 0)
                {
                    right = pathMap[seam[y].x + 1, seam[y].y + 1].R;
                }
                else if (seam[y].x == pathMap.Width - 1)
                {                   
                    left = pathMap[seam[y].x - 1, seam[y].y + 1].R;
                }
                else
                {
                    left = pathMap[seam[y].x - 1, seam[y].y + 1].R;
                    right = pathMap[seam[y].x + 1, seam[y].y + 1].R;
                }
                min = Min(left, center, right);

                if (min == center)
                {
                    seam[y + 1] = new SeamPixel(seam[y].x, seam[y].y + 1, min);
                }
                else if (min == left)
                {
                    seam[y + 1] = new SeamPixel(seam[y].x - 1, seam[y].y + 1, min);
                }
                else
                {
                    seam[y + 1] = new SeamPixel(seam[y].x + 1, seam[y].y + 1, min);
                }
            }
            pathMap.Dispose();
            energyImage = DeleteVerticalSeam(seam, energyImage);
            return DeleteVerticalSeam(seam, sourceImage);
        }
        private Image<Rgba32> DeleteHorizontalSeam(Image<Rgba32> sourceEnergy, Image<Rgba32> sourceImage)
        {
            Image<Rgba32> pathMap = CreateSeamPathMapHorizontal(sourceEnergy);
            SeamPixel[] seam = new SeamPixel[pathMap.Width];
            float min = 0;
            for (int i = 0; i < pathMap.Height; i++)
            {
                if (i == 0)
                {
                    min = pathMap[0, i].R;
                    seam[0] = new SeamPixel(0, i, min);

                }
                if (min > pathMap[0, i].R)
                {
                    min = pathMap[0, i].R;
                    seam[0] = new SeamPixel(0, i, min);
                }
            }
            for (int x = 0; x < pathMap.Width - 1; x++)
            {
                float center = pathMap[seam[x].x + 1, seam[x].y].R;
                float left = 256;
                float right = 256;
                if (seam[x].y == 0)
                {
                    right = pathMap[seam[x].x + 1, seam[x].y + 1].R;
                }
                else if (seam[x].y == pathMap.Height - 1)
                {
                    left = pathMap[seam[x].x + 1, seam[x].y - 1].R;
                }
                else
                {
                    left = pathMap[seam[x].x + 1, seam[x].y - 1].R;
                    right = pathMap[seam[x].x + 1, seam[x].y + 1].R;
                }
                min = Min(left, center, right);

                if (min == center)
                {
                    seam[x + 1] = new SeamPixel(seam[x].x + 1, seam[x].y, min);
                }
                else if (min == left)
                {
                    seam[x + 1] = new SeamPixel(seam[x].x + 1, seam[x].y - 1, min);
                }
                else
                {
                    seam[x + 1] = new SeamPixel(seam[x].x + 1, seam[x].y + 1, min);
                }
            }
            pathMap.Dispose();
            energyImage = DeleteHorizontalSeam(seam, energyImage);
            return DeleteHorizontalSeam(seam, sourceImage);
        }
        Image<Rgba32> DeleteVerticalSeam(SeamPixel[] seam, Image<Rgba32> sourceImage)
        {
            Image<Rgba32> copy = sourceImage.Clone();
            Image<Rgba32> clippedImage = new Image<Rgba32>(sourceImage.Width - 1, sourceImage.Height);
            for (int i = 0; i < seam.Length; i++)
            {
                for (int y = 0; y < copy.Width - seam[i].x - 1; y++)
                {
                    copy[seam[i].x + y, seam[i].y] = copy[seam[i].x + y + 1, seam[i].y];
                }
            }
            for (int x = 0; x < clippedImage.Width; x++)
            {
                for (int y = 0; y < clippedImage.Height; y++)
                {
                    clippedImage[x, y] = copy[x, y];
                }
            }
            
            copy.Dispose();
            return clippedImage.Clone();
        }
        Image<Rgba32> DeleteHorizontalSeam(SeamPixel[] seam, Image<Rgba32> sourceImage)
        {
            Image<Rgba32> copy = sourceImage.Clone();
            Image<Rgba32> clippedImage = new Image<Rgba32>(sourceImage.Width, sourceImage.Height - 1);
            for (int i = 0; i < seam.Length; i++)
            {
                for (int y = 0; y < copy.Height - seam[i].y - 1; y++)
                {
                    copy[seam[i].x, seam[i].y + y] = copy[seam[i].x, seam[i].y + y + 1];
                }
            }
            for (int x = 0; x < clippedImage.Width; x++)
            {
                for (int y = 0; y < clippedImage.Height; y++)
                {
                    clippedImage[x, y] = copy[x, y];
                }
            }

            copy.Dispose();
            return clippedImage.Clone();
        }
        public Form1()
        {
            InitializeComponent();
            
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            
            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 0;
        }

        private void buttonOpenFD_Click(object sender, EventArgs e)
        {
            if (openFD.ShowDialog() == DialogResult.OK)
            {
                textBoxPath.Text = openFD.FileName;
                pictureBoxOutput.Image = ImageSharpExtensions.ToBitmap((Image<Rgba32>)Image.Load(textBoxPath.Text));
            }
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            int parseResult = 0;
            if (int.TryParse(textBox2.Text, out parseResult))
            {
                xClipAmount = parseResult;
            }
            else
            {
                xClipAmount = 0;
            }
            if (int.TryParse(textBox1.Text, out parseResult))
            {
                yClipAmount = parseResult;
            }
            else
            {
                yClipAmount = 0;
            }
            if ((xClipAmount <= 0 || yClipAmount <= 0) && comboBox1.SelectedItem.ToString() == "Обрезать до")
            {
                DialogResult result = MessageBox.Show("Размер изображения не может быть меньше или равен 0", "Неправильный размер изображения", MessageBoxButtons.OK);
                if (result == DialogResult.OK)
                {
                    isOperationAborted = true;
                }
            }
            if (textBoxPath.Text != null && textBoxPath.Text != "" && !isOperationAborted)
            {
                Image<Rgba32> sourceImage = (Image<Rgba32>)Image.Load(textBoxPath.Text);
                using (Image<Rgba32> image = DetectEdges(ToGreyScale(sourceImage), gaussian3x3))
                {
                    Image<Rgba32> copy = sourceImage.Clone();
                    
                    if (!isOperationAborted)
                    {
                        energyImage = image.Clone();
                        if (comboBox1.SelectedItem.ToString() == "Обрезать до")
                        {
                            if (xClipAmount < sourceImage.Width)
                            {
                                xClipAmount = sourceImage.Width - xClipAmount;
                            }
                            if (yClipAmount < sourceImage.Height)
                            {
                                yClipAmount = sourceImage.Height - yClipAmount;
                            }
                        }
                        for (int i = 0; i < xClipAmount; i++)
                        {
                            copy = DeleteVerticalSeam(energyImage, copy);
                        }
                        for (int i = 0; i < yClipAmount; i++)
                        {
                            copy = DeleteHorizontalSeam(energyImage, copy);
                        }
                        outputImage = copy;

                        pictureBoxOutput.Image = ImageSharpExtensions.ToBitmap(outputImage);
                    }                   
                }
            }
            isOperationAborted = false;
        }

        private void buttonSaveFD_Click(object sender, EventArgs e)
        {
            if (outputImage != null)
            {
                saveFD.Filter = "Png Image|*.png|Jpeg Image|*.jpg";
                saveFD.Title = "Сохранить изображение";

                if (saveFD.ShowDialog() == DialogResult.OK)
                {
                    outputImage.Save(saveFD.FileName);
                }
            }
            
        }
    }
}
