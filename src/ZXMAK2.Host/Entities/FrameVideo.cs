using System;
using System.Drawing;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Host.Entities
{
    public class FrameVideo : IFrameVideo
    {
        public FrameVideo(Size size, float ratio)
            : this(size, ratio, new Rectangle(Point.Empty, size))
        {
        }

        public FrameVideo(Size size, float ratio, Rectangle activeArea)
        {
            Buffer = new int[size.Width * size.Height];
            Size = size;
            Ratio = ratio;
            ActiveArea = Rectangle.Intersect(new Rectangle(Point.Empty, size), activeArea);
        }

        public FrameVideo(int width, int height, float ratio)
            : this (new Size(width, height), ratio)
        {
        }

        public FrameVideo(int width, int height, float ratio, Rectangle activeArea)
            : this(new Size(width, height), ratio, activeArea)
        {
        }
        
        public int[] Buffer { get; private set; }
        public Size Size { get; private set; }
        public float Ratio { get; private set; }
        public Rectangle ActiveArea { get; private set; }
    }
}
