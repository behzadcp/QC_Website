using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace GC_Website.Detect
{



    public sealed class RectangleDetector
    {
        public sealed class DetectedRectangle
        {
            public Point[] Corners { get; init; } = Array.Empty<Point>();
            public Rect Bounds { get; init; }
            public double Area { get; init; }
        }

        public List<DetectedRectangle> DetectRectangles(
            string directory,
            string outputPath,
            double minArea = 300,
            double epsilonFactor = 0.01,
            double minSideLength = 20,
            double angleToleranceDegrees = 20,
            double minDiagonal = 20)
        {
            var results = new List<DetectedRectangle>();

            var files = Directory.GetFiles(directory);
            foreach (var imagePath in files)
            {
                results = new List<DetectedRectangle>();
                using var source = Cv2.ImRead(imagePath, ImreadModes.Color);

                if (source.Empty())
                    throw new ArgumentException($"Cannot load image: {imagePath}");

                using var gray = new Mat();
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

                // 1. Enhance local contrast.
                using var clahe = Cv2.CreateCLAHE(
                    clipLimit: 3.0,
                    tileGridSize: new Size(8, 8));

                using var enhanced = new Mat();
                clahe.Apply(gray, enhanced);

                // 2. Reduce noise while preserving edges.
                using var blurred = new Mat();
                Cv2.GaussianBlur(
                    enhanced,
                    blurred,
                    new Size(3, 3),
                    0.8);

                // 3. Detect edges directly from grayscale.
                // This avoids losing weak borders through global thresholding.
                using var edgesCanny = new Mat();
                Cv2.Canny(
                    blurred,
                    edgesCanny,
                    15,
                    60,
                    3,
                    true);

                // 4. Adaptive thresholding as a second detection path.
                using var adaptive = new Mat();
                Cv2.AdaptiveThreshold(
                    blurred,
                    adaptive,
                    255,
                    AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.Binary,
                    21,
                    3);

                // Find edges in the adaptive result.
                using var edgesAdaptive = new Mat();
                Cv2.Canny(
                    adaptive,
                    edgesAdaptive,
                    20,
                    80);

                // 5. Combine both detection paths.
                using var edges = new Mat();
                Cv2.BitwiseOr(
                    edgesCanny,
                    edgesAdaptive,
                    edges);

                // 6. Close small gaps in borders.
                using var kernel = Cv2.GetStructuringElement(
                    MorphShapes.Rect,
                    new Size(3, 3));

                Cv2.MorphologyEx(
                    edges,
                    edges,
                    MorphTypes.Close,
                    kernel);

                // 5. Find contours.
                Cv2.FindContours(
                    edges,
                    out Point[][] contours,
                    out _,
                    RetrievalModes.List,
                    ContourApproximationModes.ApproxSimple);

                foreach (Point[] contour in contours)
                {
                    if (contour.Length < 4)
                        continue;

                    double area = Math.Abs(Cv2.ContourArea(contour));

                    // Reject small shapes.
                    if (area < minArea)
                        continue;

                    // 6. Approximate contours with polygonal features.
                    double perimeter = Cv2.ArcLength(contour, true);

                    Point[] polygon = Cv2.ApproxPolyDP(
                        contour,
                        epsilonFactor * perimeter,
                        true);

                    // 7. Accept only four-corner polygons.
                    if (polygon.Length != 4)
                        continue;

                    if (!Cv2.IsContourConvex(polygon))
                        continue;

                    // Check side lengths.
                    double[] sides = new double[4];

                    for (int i = 0; i < 4; i++)
                    {
                        Point a = polygon[i];
                        Point b = polygon[(i + 1) % 4];

                        sides[i] = Distance(a, b);
                    }

                    if (sides.Any(s => s < minSideLength))
                        continue;

                    // Check that adjacent edges are approximately perpendicular.
                    bool validAngles = true;

                    for (int i = 0; i < 4; i++)
                    {
                        Point previous = polygon[(i + 3) % 4];
                        Point current = polygon[i];
                        Point next = polygon[(i + 1) % 4];

                        double angle = CalculateAngle(
                            previous,
                            current,
                            next);

                        if (Math.Abs(angle - 90.0) >
                            angleToleranceDegrees)
                        {
                            validAngles = false;
                            break;
                        }
                    }

                    if (!validAngles)
                        continue;

                    // Check opposite-vertex distances (diagonals).
                    double diagonal1 = Distance(polygon[0], polygon[2]);
                    double diagonal2 = Distance(polygon[1], polygon[3]);

                    if (diagonal1 < minDiagonal ||
                        diagonal2 < minDiagonal)
                    {
                        continue;
                    }

                    // Rectangle detected.
                    results.Add(new DetectedRectangle
                    {
                        Corners = polygon,
                        Bounds = Cv2.BoundingRect(polygon),
                        Area = area
                    });
                }

                // Draw all detected rectangles.
                foreach (var rectangle in results)
                {
                    Cv2.Polylines(
                        source,
                        new[] { rectangle.Corners },
                        true,
                        Scalar.LimeGreen,
                        2,
                        LineTypes.AntiAlias);

                    Rect r = rectangle.Bounds;

                    Cv2.PutText(
                        source,
                        $"Area: {rectangle.Area:F0}",
                        new Point(r.X, Math.Max(15, r.Y - 5)),
                        HersheyFonts.HersheySimplex,
                        0.45,
                        Scalar.Red,
                        1,
                        LineTypes.AntiAlias);
                }

                // Save the annotated image.
                Cv2.ImWrite(Path.Combine(Path.GetDirectoryName(imagePath), Path.GetFileNameWithoutExtension(imagePath)+"boxes"+Path.GetExtension(imagePath)), source);

            }
            return results;
        }

        private static double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double CalculateAngle(
            Point a,
            Point vertex,
            Point c)
        {
            double ax = a.X - vertex.X;
            double ay = a.Y - vertex.Y;

            double cx = c.X - vertex.X;
            double cy = c.Y - vertex.Y;

            double dot = ax * cx + ay * cy;

            double magnitudeA = Math.Sqrt(ax * ax + ay * ay);
            double magnitudeC = Math.Sqrt(cx * cx + cy * cy);

            if (magnitudeA == 0 || magnitudeC == 0)
                return 0;

            double cosine = dot / (magnitudeA * magnitudeC);

            // Protect against floating-point rounding errors.
            cosine = Math.Clamp(cosine, -1.0, 1.0);

            return Math.Acos(cosine) * 180.0 / Math.PI;
        }



        public void DetectLines(
          string inputPath,
          string outputPath,
          int minLineLength = 30)
        {
           

            using Mat src = Cv2.ImRead(inputPath, ImreadModes.Grayscale);
            using Mat bw = new Mat();

            // Invert to get white lines on a black background
            Cv2.AdaptiveThreshold(src, bw, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.BinaryInv, 15, 2);

            // 2. Extract Horizontal Lines
            using Mat horizontal = bw.Clone();
            // Specify structure size (e.g., width of the image / 30)
            int horizontalSize = horizontal.Cols / 30;
            using Mat horizontalStructure = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(horizontalSize, 1));

            // Erode and dilate to remove vertical elements and preserve horizontal ones
            Cv2.Erode(horizontal, horizontal, horizontalStructure);
            Cv2.Dilate(horizontal, horizontal, horizontalStructure);
            Cv2.ImWrite("horizontal_lines.jpg", horizontal);

            // 3. Extract Vertical Lines
            using Mat vertical = bw.Clone();
            // Specify structure size (e.g., height of the image / 30)
            int verticalSize = vertical.Rows / 30;
            using Mat verticalStructure = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, verticalSize));

            // Erode and dilate to remove horizontal elements and preserve vertical ones
            Cv2.Erode(vertical, vertical, verticalStructure);
            Cv2.Dilate(vertical, vertical, verticalStructure);

        }


    }

}


