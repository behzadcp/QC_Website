using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;


namespace GC_Website.Detect
{
    public sealed class DetectedBorder
    {
        public string ImagePath { get; set; } = string.Empty;

        public int X { get; set; }
        public int Y { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }

        public int Right => X + Width;
        public int Bottom => Y + Height;

        public double Area => (double)Width * Height;

        public override string ToString()
        {
            return $"X={X}, Y={Y}, Width={Width}, Height={Height}, " +
                   $"Right={Right}, Bottom={Bottom}, Area={Area}";
        }
    }

    public sealed class HtmlImageBorderDetector
    {
        private readonly int _minWidth;
        private readonly int _minHeight;
        private readonly double _minArea;

        public HtmlImageBorderDetector(
            int minWidth = 10,
            int minHeight = 10,
            double minArea = 100)
        {
            _minWidth = minWidth;
            _minHeight = minHeight;
            _minArea = minArea;
        }

        public Scalar GetBorderAverageColor(string imagePath)
        {
            var results = new List<DetectedBorder>();

            using var image = Cv2.ImRead(
                imagePath,
                ImreadModes.Color);


            Rect topRect = new Rect(0, 0, image.Width, 5);
            Rect bottomRect = new Rect(0, image.Height - 5, image.Width, 5);

            using Mat topSlice = new Mat(image, topRect);
            using Mat bottomSlice = new Mat(image, bottomRect);

            Scalar topMean = Cv2.Mean(topSlice);
            Scalar bottomMean = Cv2.Mean(bottomSlice);

            // Average them out for a rough estimate
            return new Scalar(
                (topMean.Val0 + bottomMean.Val0) / 2,
                (topMean.Val1 + bottomMean.Val1) / 2,
                (topMean.Val2 + bottomMean.Val2) / 2
            );
        }

        public List<DetectedBorder> DetectBorders(string imagePath)
        {
            var results = new List<DetectedBorder>();

            using var image = Cv2.ImRead(
                imagePath,
                ImreadModes.Color);

            if (image.Empty())
            {
                Console.WriteLine($"Cannot read image: {imagePath}");
                return results;
            }

            using var gray = new Mat();
            using var blurred = new Mat();
            using var edges = new Mat();

            // Convert screenshot to grayscale.
            Cv2.CvtColor(
                image,
                gray,
                ColorConversionCodes.BGR2GRAY);

            // Reduce noise before edge detection.
            Cv2.GaussianBlur(
                gray,
                blurred,
                new Size(5, 5),
                0);

            // Detect visible edges.
            Cv2.Canny(
                blurred,
                edges,
                50,
                150);

            // Find contours around detected edges.
            Cv2.FindContours(
                edges,
                out Point[][] contours,
                out HierarchyIndex[] hierarchy,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

            foreach (Point[] contour in contours)
            {
                if (contour.Length < 4)
                    continue;

                // Approximate the contour to identify rectangular shapes.
                double perimeter = Cv2.ArcLength(contour, true);

                Point[] polygon = Cv2.ApproxPolyDP(
                    contour,
                    0.02 * perimeter,
                    true);

                // Focus on rectangles and quadrilaterals.
                if (polygon.Length != 4 ||
                    !Cv2.IsContourConvex(polygon))
                {
                    continue;
                }

                Rect rect = Cv2.BoundingRect(polygon);

                if (rect.Width < _minWidth ||
                    rect.Height < _minHeight)
                {
                    continue;
                }

                double area = (double)rect.Width * rect.Height;

                if (area < _minArea)
                    continue;

                results.Add(new DetectedBorder
                {
                    ImagePath = imagePath,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height
                });
            }

            // Sort rectangles from top to bottom, then left to right.
            return results
                .OrderBy(r => r.Y)
                .ThenBy(r => r.X)
                .ToList();
        }

        public void SaveImageWithBorders(string imagePath)
        {
            using var image = Cv2.ImRead(imagePath, ImreadModes.Color);

            if (image.Empty())
            {
                Console.WriteLine($"Cannot read image: {imagePath}");
                return;
            }

            List<DetectedBorder> borders = DetectBorders(imagePath);

            foreach (var border in borders)
            {
                // Draw the detected rectangle.
                Cv2.Rectangle(
                    image,
                    new Point(border.X, border.Y),
                    new Point(
                        border.X + border.Width - 1,
                        border.Y + border.Height - 1),
                    Scalar.Red,
                    2);

                // Optional: display coordinates and dimensions.
                string label =
                    $"X:{border.X} Y:{border.Y} " +
                    $"{border.Width}x{border.Height}";

                Cv2.PutText(
                    image,
                    label,
                    new Point(border.X, Math.Max(15, border.Y - 5)),
                    HersheyFonts.HersheySimplex,
                    0.45,
                    Scalar.Lime,
                    1);
            }

            string directory = Path.GetDirectoryName(imagePath)!;
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string extension = Path.GetExtension(imagePath);

            string outputPath = Path.Combine(
                directory,
                $"{fileName}_border{extension}");

            if (Cv2.ImWrite(outputPath, image))
            {
                Console.WriteLine(
                    $"Saved {borders.Count} borders: {outputPath}");
            }
        }
        public List<DetectedBorder> DetectDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException(
                    $"Directory not found: {directoryPath}");
            }

            string[] extensions =
            {
            ".png", ".jpg", ".jpeg", ".bmp",
            ".webp", ".tif", ".tiff"
        };

            var allResults = new List<DetectedBorder>();

            var files = Directory
                .EnumerateFiles(directoryPath)
                .Where(path => extensions.Contains(
                    Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path);

            foreach (string file in files)
            {
                Console.WriteLine($"\nImage: {Path.GetFileName(file)}");

                List<DetectedBorder> borders = DetectBorders(file);

                foreach (var border in borders)
                {
                    Console.WriteLine($"  {border}");
                }

                allResults.AddRange(borders);
            }

            return allResults;
        }

        public List<Rect> DetectGryRectangle(
        string imagePath,
        double minGray = 0.05,
        double maxGray = 0.10,
        int minWidth = 20,
        int minHeight = 20)
        {
            var outputPath = @"D:\website\www.takvim.com.tr\Custom Desktop\12.png";

            using var image = Cv2.ImRead(
                imagePath, ImreadModes.Color);

            if (image.Empty())
                throw new Exception("Cannot read image.");

            using var gray = new Mat();
            using var mask = new Mat();
            using var kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new Size(3, 3));

            Cv2.CvtColor(
                image, gray, ColorConversionCodes.BGR2GRAY);

            // Convert normalized gray values (0..1) to OpenCV values (0..255).
            int lower = (int)Math.Round(minGray * 255);
            int upper = (int)Math.Round(maxGray * 255);

            // Keep pixels within the specified gray range.
            Cv2.InRange(
                gray,
                new Scalar(lower),
                new Scalar(upper),
                mask);

            // Connect small gaps in the detected regions.
            Cv2.MorphologyEx(
                mask, mask, MorphTypes.Close, kernel);

            Cv2.FindContours(
                mask,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            var rectangles = new List<Rect>();

            foreach (var contour in contours)
            {
                Rect rect = Cv2.BoundingRect(contour);

                if (rect.Width < minWidth ||
                    rect.Height < minHeight)
                    continue;

                double area = Cv2.ContourArea(contour);

                // Require a reasonably filled rectangular region.
                double rectangularity =
                    area / ((double)rect.Width * rect.Height);

                if (rectangularity < 0.10)
                    continue;

                rectangles.Add(rect);
            }
            for (int i = 0; i < rectangles.Count; i++)
            {
                Rect rect = rectangles[i];

                Cv2.Rectangle(
                    image,
                    rect,
                    Scalar.Red,
                    2);

                Cv2.PutText(
                    image,
                    $"{i + 1}: {rect.Width}x{rect.Height}",
                    new Point(rect.X, Math.Max(15, rect.Y - 5)),
                    HersheyFonts.HersheySimplex,
                    0.45,
                    Scalar.Lime,
                    1);
            }

            outputPath ??= Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(imagePath))!,
                Path.GetFileNameWithoutExtension(imagePath) +
                "_rectangles" +
                Path.GetExtension(imagePath));

            if (!Cv2.ImWrite(outputPath, image))
                throw new IOException(
                    $"Could not save image: {outputPath}");

            Console.WriteLine(
                $"Detected {rectangles.Count} rectangles.");

            Console.WriteLine($"Saved: {outputPath}");
            return rectangles
                .OrderBy(r => r.Y)
                .ThenBy(r => r.X)
                .ToList();

        }

        public int MinWidth { get; set; } = 20;
        public int MinHeight { get; set; } = 20;
        public double MinArea { get; set; } = 400;

        // Rectangles with IoU above this threshold are considered duplicates.
        public double DuplicateIouThreshold { get; set; } = 0.85;

        public List<Rect> DetectRectangles(string imagePath)
        {
            var outputPath = @"D:\website\www.takvim.com.tr\Custom Desktop\11.png";
            using var image = Cv2.ImRead(imagePath, ImreadModes.Color);

            if (image.Empty())
                throw new InvalidOperationException(
                    $"Could not read image: {imagePath}");

            using var gray = new Mat();
            using var blurred = new Mat();
            using var edges = new Mat();

            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            Cv2.GaussianBlur(
                gray, blurred, new Size(5, 5), 0);

            Cv2.Canny(blurred, edges, 50, 150);

            // Connect small gaps in rectangular borders.
            using var kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new Size(5, 5));

            Cv2.MorphologyEx(
                edges, edges, MorphTypes.Close, kernel);

            Cv2.FindContours(
                edges,
                out Point[][] contours,
                out _,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

            var candidates = new List<Rect>();

            foreach (Point[] contour in contours)
            {
                if (contour.Length < 4)
                    continue;

                double perimeter = Cv2.ArcLength(contour, true);

                if (perimeter <= 0)
                    continue;

                Point[] polygon = Cv2.ApproxPolyDP(
                    contour,
                    0.02 * perimeter,
                    true);

                // A rectangle should have four corners.
                if (polygon.Length != 4)
                    continue;

                if (!Cv2.IsContourConvex(polygon))
                    continue;

                // Reject quadrilaterals with strongly non-right angles.
                if (!HasApproximatelyRightAngles(polygon))
                    continue;

                Rect rect = Cv2.BoundingRect(polygon);

                if (rect.Width < MinWidth ||
                    rect.Height < MinHeight)
                    continue;

                double area = (double)rect.Width * rect.Height;

                if (area < MinArea)
                    continue;

                candidates.Add(rect);
            }

            var rectangles = RemoveDuplicates(candidates);

            for (int i = 0; i < rectangles.Count; i++)
            {
                Rect rect = rectangles[i];

                Cv2.Rectangle(
                    image,
                    rect,
                    Scalar.Red,
                    2);

                Cv2.PutText(
                    image,
                    $"{i + 1}: {rect.Width}x{rect.Height}",
                    new Point(rect.X, Math.Max(15, rect.Y - 5)),
                    HersheyFonts.HersheySimplex,
                    0.45,
                    Scalar.Lime,
                    1);
            }

            outputPath ??= Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(imagePath))!,
                Path.GetFileNameWithoutExtension(imagePath) +
                "_rectangles" +
                Path.GetExtension(imagePath));

            if (!Cv2.ImWrite(outputPath, image))
                throw new IOException(
                    $"Could not save image: {outputPath}");

            Console.WriteLine(
                $"Detected {rectangles.Count} rectangles.");

            Console.WriteLine($"Saved: {outputPath}");
            return rectangles;
        }

        private static bool HasApproximatelyRightAngles(Point[] points)
        {
            const double minCosine = 0.30;

            for (int i = 0; i < 4; i++)
            {
                Point previous = points[(i + 3) % 4];
                Point current = points[i];
                Point next = points[(i + 1) % 4];

                double ax = previous.X - current.X;
                double ay = previous.Y - current.Y;

                double bx = next.X - current.X;
                double by = next.Y - current.Y;

                double denominator = Math.Sqrt(
                    (ax * ax + ay * ay) *
                    (bx * bx + by * by));

                if (denominator == 0)
                    return false;

                double cosine = Math.Abs(
                    (ax * bx + ay * by) / denominator);

                if (cosine > minCosine)
                    return false;
            }

            return true;
        }

        private List<Rect> RemoveDuplicates(List<Rect> rectangles)
        {
            var result = new List<Rect>();

            // Prefer larger contours when duplicates overlap.
            foreach (Rect candidate in rectangles
                         .OrderByDescending(r => (double)r.Width * r.Height))
            {
                bool duplicate = result.Any(existing =>
                    CalculateIoU(candidate, existing) >=
                    DuplicateIouThreshold);

                if (!duplicate)
                    result.Add(candidate);
            }

            return result
                .OrderBy(r => r.Y)
                .ThenBy(r => r.X)
                .ToList();
        }

        private static double CalculateIoU(Rect a, Rect b)
        {
            int left = Math.Max(a.X, b.X);
            int top = Math.Max(a.Y, b.Y);
            int right = Math.Min(a.Right, b.Right);
            int bottom = Math.Min(a.Bottom, b.Bottom);

            int intersectionWidth = Math.Max(0, right - left);
            int intersectionHeight = Math.Max(0, bottom - top);

            double intersection =
                (double)intersectionWidth * intersectionHeight;

            double union =
                (double)a.Width * a.Height +
                (double)b.Width * b.Height -
                intersection;

            return union <= 0 ? 0 : intersection / union;
        }

        public void SaveImageWithRectangles(
            string imagePath,
            string? outputPath = null)
        {
            using var image = Cv2.ImRead(
                imagePath, ImreadModes.Color);

            if (image.Empty())
                throw new InvalidOperationException(
                    $"Could not read image: {imagePath}");

            List<Rect> rectangles = DetectRectangles(imagePath);

            for (int i = 0; i < rectangles.Count; i++)
            {
                Rect rect = rectangles[i];

                Cv2.Rectangle(
                    image,
                    rect,
                    Scalar.Red,
                    2);

                Cv2.PutText(
                    image,
                    $"{i + 1}: {rect.Width}x{rect.Height}",
                    new Point(rect.X, Math.Max(15, rect.Y - 5)),
                    HersheyFonts.HersheySimplex,
                    0.45,
                    Scalar.Lime,
                    1);
            }

            outputPath ??= Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(imagePath))!,
                Path.GetFileNameWithoutExtension(imagePath) +
                "_rectangles" +
                Path.GetExtension(imagePath));

            if (!Cv2.ImWrite(outputPath, image))
                throw new IOException(
                    $"Could not save image: {outputPath}");

            Console.WriteLine(
                $"Detected {rectangles.Count} rectangles.");

            Console.WriteLine($"Saved: {outputPath}");
        }

    }
}