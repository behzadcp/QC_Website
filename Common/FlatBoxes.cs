using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GC_Website.Common
{
    public class FlatBoxes
    {
        public string FileName { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public int Right { get; set; }
    }

    public class FlatBoxesSame{
        public string FileName { get; set; }
        public int X { get; set; } = 0;
        public int XSame { get; set; } = 0;
        public int Y { get; set; } = 0;
        public int YSame { get; set; } = 0;
            
        public int Height { get; set; } = 0;
        public int HeightSame { get; set; } = 0;

        public int Width { get; set; } = 0;
        public int WidthSame { get; set; } = 0;

        public int Right { get; set; } = 0;
        public int RightSame { get; set; } = 0;

    }
}
