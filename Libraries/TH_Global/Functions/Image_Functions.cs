﻿using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TH_Global.Functions
{
    public static class Image_Functions
    {
        /// <summary>
        /// Returns a cropped image based on the 'cropArea' set
        /// </summary>
        /// <param name="img"></param>
        /// <param name="cropArea"></param>
        /// <returns></returns>
        public static Image CropImage(Image img, Rectangle cropArea)
        {
            Bitmap bmpImage = new Bitmap(img);
            return bmpImage.Clone(cropArea, bmpImage.PixelFormat);
        }

        /// <summary>
        /// Returns a Image that is automatically cropped to the center of the image
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public static Image CropImageToCenter(Image img)
        {
            int width = img.Width;
            int height = img.Height;

            if (width > height)
            {
                int sqWidth = height;
                int widthOffset = (width - sqWidth) / 2;

                Rectangle widthCropRect = new Rectangle(new Point(widthOffset, 0), new Size(height, height));

                return CropImage(img, widthCropRect);
            }
            else if (height > width)
            {
                int sqHeight = width;
                int heightOffset = (height - sqHeight) / 2;

                Rectangle heightCropRect = new Rectangle(new Point(0, heightOffset), new Size(width, width));

                return CropImage(img, heightCropRect);
            }
            else return img;
        }

        /// <summary>
        /// Returns a resized image based on the Width and Height given
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Bitmap SetImageSize(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }


    }
}