using OpenCvSharp;

namespace GC_Website.Common
{
    public class PageBoxModel
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Index { get; set; }
        public string FileName { get; set; }
        public int  PageOffsetY { get; set; }
        public  List<Rect>  Recs{ get; set; }=new List<Rect>();
    }
}
