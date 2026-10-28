using GC_Website.Common;
using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace GC_Website.Detect
{
    public class BackGroundColorDetect
    {
        public Scalar DetectBackgroundColor(string imagePath)
        {
            using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
            // Collect edge (border) pixels which are likely background
            var borderPixels = new List<Vec3b>();

            // Top and bottom rows
            for (int x = 0; x < image.Cols; x++)
            {
                borderPixels.Add(image.At<Vec3b>(0, x));
                borderPixels.Add(image.At<Vec3b>(image.Rows - 1, x));
            }

            // Left and right columns
            for (int y = 0; y < image.Rows; y++)
            {
                borderPixels.Add(image.At<Vec3b>(y, 0));
                borderPixels.Add(image.At<Vec3b>(y, image.Cols - 1));
            }

            // Calculate average BGR color of border pixels
            double avgB = borderPixels.Average(p => p.Item0);
            double avgG = borderPixels.Average(p => p.Item1);
            double avgR = borderPixels.Average(p => p.Item2);

            return new Scalar(avgB, avgG, avgR);
        }

        public void BackgroundDetect(string imagePath)
        {

            Mat src = Cv2.ImRead(imagePath);
            if (src.Empty())
            {
                Console.WriteLine("Could not open or find the image!");
                return;
            }

            // 2. Convert from BGR to HSV color space
            Mat hsv = new Mat();
            Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);

            // 3. Define lower and upper bounds for the background color you want to detect
            // Example: Detecting a white/light gray background
            // Adjust these Scalar values based on your specific background color
            Scalar lowerBound = new Scalar(0, 0, 200);   // Low saturation, high value (bright/white)
            Scalar upperBound = new Scalar(180, 50, 255);

            // For a Green Screen background, you would typically use:
            // Scalar lowerBound = new Scalar(35, 50, 50);
            // Scalar upperBound = new Scalar(85, 255, 255);

            // 4. Create the mask
            Mat mask = new Mat();
            Cv2.InRange(hsv, lowerBound, upperBound, mask);

            // 5. Mark the background on the original image (e.g., paint background pixels Red)

            // Define the color to mark the background with (Blue, Green, Red) -> Red here
            Scalar markerColor = new Scalar(0, 0, 255);

            // Apply color only where the mask is active

            // 6. Display the results
            Cv2.ImShow("Original Image", src);
            Cv2.ImShow("Background Mask", mask);

            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }
        public Scalar GetEdgeSamplingBackground(string imagePath)
        {

            Mat img = Cv2.ImRead(imagePath);
            List<Vec3b> borderPixels = new List<Vec3b>();
            int rows = img.Rows;
            int cols = img.Cols;

            // Sample top and bottom rows
            for (int c = 0; c < cols; c += 2) // Step by 2 for performance
            {
                borderPixels.Add(img.At<Vec3b>(0, c));
                borderPixels.Add(img.At<Vec3b>(rows - 1, c));
            }

            // Sample left and right columns
            for (int r = 1; r < rows - 1; r += 2)
            {
                borderPixels.Add(img.At<Vec3b>(r, 0));
                borderPixels.Add(img.At<Vec3b>(r, cols - 1));
            }

            // Calculate median for B, G, R channels separately
            byte b = (byte)borderPixels.Select(p => p.Item0).OrderBy(x => x).ElementAt(borderPixels.Count / 2);
            byte g = (byte)borderPixels.Select(p => p.Item1).OrderBy(x => x).ElementAt(borderPixels.Count / 2);
            byte red = (byte)borderPixels.Select(p => p.Item2).OrderBy(x => x).ElementAt(borderPixels.Count / 2);

            return new Scalar(b, g, red);
        }
        public Scalar GetHistogramDominantColor(string imagePath)
        {

            Mat img = Cv2.ImRead(imagePath);
            Dictionary<Vec3b, int> colorCounts = new Dictionary<Vec3b, int>();

            // Step across the image to sample pixels
            for (int y = 0; y < img.Rows; y += 4)
            {
                for (int x = 0; x < img.Cols; x += 4)
                {
                    Vec3b p = img.At<Vec3b>(y, x);

                    // Quantize to nearest 16 to cluster similar tones together
                    byte qb = (byte)((p.Item0 / 16) * 16);
                    byte qg = (byte)((p.Item1 / 16) * 16);
                    byte qr = (byte)((p.Item2 / 16) * 16);
                    Vec3b qp = new Vec3b(qb, qg, qr);

                    if (colorCounts.ContainsKey(qp))
                        colorCounts[qp]++;
                    else
                        colorCounts[qp] = 1;
                }
            }

            // Find the most frequent quantized color
            var dominant = colorCounts.OrderByDescending(kv => kv.Value).First().Key;
            return new Scalar(dominant.Item0, dominant.Item1, dominant.Item2);
        }

        public Scalar GetKMeansBackground(string imagePath, int k = 2)
        {

            Mat img = Cv2.ImRead(imagePath);
            // Reshape image to a 2D matrix with 3 columns (B, G, R) and convert to float32
            Mat data = new Mat();
            img.Reshape(1, img.Rows * img.Cols).ConvertTo(data, MatType.CV_32F);

            Mat labels = new Mat();
            Mat centers = new Mat();
            TermCriteria criteria = new TermCriteria(CriteriaTypes.Count | CriteriaTypes.Eps, 10, 1.0);

            // Run K-Means clustering
            Cv2.Kmeans(data, k, labels, criteria, 3, KMeansFlags.PpCenters, centers);

            // Count pixels in each cluster to find the largest group
            int[] clusterCounts = new int[k];
            for (int i = 0; i < labels.Rows; i++)
            {
                int label = labels.At<int>(i, 0);
                clusterCounts[label]++;
            }

            // Find the cluster with the highest pixel count
            int maxCluster = 0;
            int maxCount = clusterCounts[0];
            for (int i = 1; i < k; i++)
            {
                if (clusterCounts[i] > maxCount)
                {
                    maxCount = clusterCounts[i];
                    maxCluster = i;
                }
            }

            // Extract the center color of the largest cluster
            float b = centers.At<float>(maxCluster, 0);
            float g = centers.At<float>(maxCluster, 1);
            float r = centers.At<float>(maxCluster, 2);

            return new Scalar(b, g, r);
        }




        public List<Rect> FindMultipleNonBackgroundBoxes(string imagePath, Scalar backgroundColor, double threshold = 10.0, int minArea = 100)
        {
            Mat img = Cv2.ImRead(imagePath);

            // Step 1: Create a background matrix of the same size and type
            Mat bgMat = new Mat(img.Size(), img.Type(), backgroundColor);

            // Step 2: Compute absolute difference between the image and the background color
            Mat diff = new Mat();
            Cv2.Absdiff(img, bgMat, diff);

            // Step 3: Convert to grayscale and threshold to create a binary mask
            Mat gray = new Mat();
            Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);

            Mat mask = new Mat();
            Cv2.Threshold(gray, mask, threshold, 50, ThresholdTypes.Binary);

            // Step 4: Optional morphological operations to clean up small noise holes or specs
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

            // Step 5: Find contours of all non-background objects
            Cv2.FindContours(mask, out Point[][] contours, out HierarchyIndex[] hierarchy,
                             RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            List<Rect> boxes = new List<Rect>();

            // Step 6: Loop through each contour and create a bounding box
            foreach (var contour in contours)
            {
                double area = Cv2.ContourArea(contour);


                // Filter out tiny noise based on minimum area
                if (area > minArea)
                {
                    Rect box = Cv2.BoundingRect(contour);
                    if (box.Height > 100 && box.Width > 100)
                        boxes.Add(box);
                }
            }
            return boxes;

        }

        public Scalar GetBackgroundColorOfRectangle(
        Mat image,
        Rect rectangle,
        int margin = 5)
        {
            // Ensure the rectangle is within image boundaries
            rectangle = rectangle.Intersect(
                new Rect(0, 0, image.Width, image.Height));

            if (!(rectangle.Width <= margin * 2 ||
                rectangle.Height <= margin * 2))
            {



                // Extract the rectangle's interior, excluding its border
                Rect innerRect = new Rect(
                    rectangle.X + margin,
                    rectangle.Y + margin,
                    rectangle.Width - margin * 2,
                    rectangle.Height - margin * 2);

                using Mat roi = new Mat(image, innerRect);

                // Calculate average BGR color
                Scalar average = Cv2.Mean(roi);

                return average;
            }
            return default;
        }

        public static Rect GetInnerContentRectangle(Mat image, Rect searchRect, Scalar backgroundColor, double tolerance = 10)
        {
            // 1. Ensure the search rectangle stays within the image boundaries
            Rect safeRect = searchRect.Intersect(new Rect(0, 0, image.Cols, image.Rows));
            if (safeRect.Width <= 0 || safeRect.Height <= 0)
            {
                return new Rect(); // Return empty rect if out of bounds
            }

            // 2. Extract the Region of Interest (ROI)
            using Mat roi = new Mat(image, safeRect);

            // 3. Define lower and upper bounds to handle color variations/noise
            Scalar lowerBound = new Scalar(
                Math.Max(0, backgroundColor.Val0 - tolerance),
                Math.Max(0, backgroundColor.Val1 - tolerance),
                Math.Max(0, backgroundColor.Val2 - tolerance)
            );
            Scalar upperBound = new Scalar(
                Math.Min(255, backgroundColor.Val0 + tolerance),
                Math.Min(255, backgroundColor.Val1 + tolerance),
                Math.Min(255, backgroundColor.Val2 + tolerance)
            );

            // 4. Create a mask where the background color is white (255) and everything else is black (0)
            using Mat bgMask = new Mat();
            Cv2.InRange(roi, lowerBound, upperBound, bgMask);

            // 5. Invert the mask so the inner content/foreground is white (255)
            using Mat fgMask = new Mat();
            Cv2.BitwiseNot(bgMask, fgMask);


            using Mat nzPointsMat = new Mat();
            Cv2.FindNonZero(fgMask, nzPointsMat);
            // 6. Find all non-zero (foreground) points
            if (nzPointsMat.Total() == 0)
            {
                return new Rect(); // No inner content found
            }
            // 7. Calculate the bounding rectangle of the foreground points relative to the ROI
            Rect innerRoiRect = Cv2.BoundingRect(nzPointsMat);
            // 8. Translate the inner rectangle coordinates back to the original image coordinate space
            return new Rect(
                safeRect.X + innerRoiRect.X,
                safeRect.Y + innerRoiRect.Y,
                innerRoiRect.Width,
                innerRoiRect.Height
            );
        }


        public List<PageBoxModel> DetectBoxes(List<ScreenShotModel> screenShots, string directory)
        {
            var result = new List<PageBoxModel>();
            var files = Directory.GetFiles(directory);
            var pageStartY = 0;
            foreach (var item in screenShots)
            {
                var pageInfo = new PageBoxModel();

                var backColor = GetHistogramDominantColor(item.FilePath);
                var boxes = FindMultipleNonBackgroundBoxes(item.FilePath, backColor, 10, 500);

                Mat image = Cv2.ImRead(item.FilePath);
                pageInfo.Height = image.Height;
                pageInfo.Width = image.Width;
                pageInfo.FileName = item.FilePath;
                pageInfo.PageOffsetY = item.PageNumber * item.PageHeight;
                foreach (var box in boxes)
                {
                    var bgColor = GetBackgroundColorOfRectangle(image, box, 2);
                    //var rectangles = FindTextBlocksInsideRect(image, box);
                    //foreach (var rec in rectangles)
                    //{
                    //    Cv2.Rectangle(image, rec, new Scalar(0, 255, 0), 1);
                    //}
                    //var innerRectaangle = GetInnerContentRectangle(image, box, bgColor);
                    //Cv2.Rectangle(image, innerRectaangle, new Scalar(0, 255, 0), 2);

                    // Draw a green rectangle with thickness of 2
                    Cv2.Rectangle(image, box, new Scalar(0, 255, 0), 2);


                }

                // 5. Save or display the result
                Cv2.ImWrite(Path.Combine(Path.GetDirectoryName(item.FilePath), Path.GetFileNameWithoutExtension(item.FilePath) + "boxes" + Path.GetExtension(item.FilePath)), image);
                pageInfo.Recs.AddRange(boxes);

                result.Add(pageInfo);
            }

            return result;

        }
        public void FindWrongs(List<PageBoxModel> pages)
        {
            var leftAdses = new List<FlatBoxes>();
            var rightAdses = new List<FlatBoxes>();
            var pageContentBox = new List<FlatBoxes>();
            var pageContent = new List<FlatBoxes>();
            foreach (var page in pages)
            {
                var rects = new List<FlatBoxes>();
                foreach (var rec in page.Recs)
                {
                    if (rec.X != 0)
                        rects.Add(new FlatBoxes()
                        {
                            FileName = page.FileName,
                            X = rec.X,
                            Y = rec.Y,
                            Height = rec.Height,
                            Width = rec.Width,
                            Right = rec.X + rec.Width,
                        });
                }

                var lefts = rects.GroupBy(c => c.X).Select(g => new
                {
                    X = g.Key,
                    Count = g.Count(x => x.X == g.Key)

                }).ToList();

                var pageCountentLeft = lefts.OrderByDescending(x => x.Count).FirstOrDefault()?.X;

                var pageCountentRight = rects.GroupBy(c => c.Right).Select(g => new
                {
                    Right = g.Key,
                    Count = g.Count(x => x.X == g.Key)

                }).ToList();

                var pageRight = pageCountentRight.OrderByDescending(x => x.Right).FirstOrDefault()?.Right;

                var rightAds = rects.Where(c => c.X != 0).MaxBy(c => c.X);
                var leftAds = rects.Where(c => c.X != 0).MinBy(c => c.X);
                var headerMenu = rects.Where(c => c.X == 0).FirstOrDefault();
                leftAdses.Add(leftAds);
                rightAdses.Add(rightAds);
                pageContent.AddRange(rects.Except(new List<FlatBoxes>() { leftAds, rightAds, headerMenu }).ToList());


                //var heightGroupBy = rects.GroupBy(c => c.Height).Select(g => new
                //{
                //    Height = g.Key,
                //    x = g.FirstOrDefault().X,
                //    y = g.FirstOrDefault().Y,
                //    width = g.FirstOrDefault().Width,
                //    FileName = g.FirstOrDefault().FileName
                //}).ToList();

                //var height = heightGroupBy.Select(c => c.Height).ToImmutableList();
                //height = height.Sort();


            }
            var wrongLeftAds = GetMismatchedBoxes(leftAdses);

            var wrongWrightAds = GetMismatchedBoxes(leftAdses);

            var pageContentMissmatch = FindPageContentMissmatch(pageContent, 5);

            DrawRedBox(pageContentMissmatch);

        }

        public void DrawRedBox(List<FlatBoxesSame> boxes)
        {
            var files = boxes.GroupBy(g => g.FileName).Select(c => new
            {
                FileName = c.Key,
                Boxes = c.ToList()

            }).ToList();
            foreach (var file in files)
            {
                Mat image = Cv2.ImRead(file.FileName);

                foreach (var box in file.Boxes)
                {
                    var rectangle = new Rect(box.X, box.Y, box.Width, box.Height);
                    Cv2.Rectangle(image, rectangle, new Scalar(0, 255, 0), 2);
                }

                Cv2.ImWrite(Path.Combine(Path.GetDirectoryName(file.FileName), Path.GetFileNameWithoutExtension(file.FileName) + "wrongs" + Path.GetExtension(file.FileName)), image);

            }

        }

        public List<FlatBoxesSame> FindPageContentMissmatch(List<FlatBoxes> pageContent, int tolorance = 5)
        {

            var sameItems = new List<FlatBoxesSame>();


            var leftside = pageContent.OrderBy(c => c.X).ToList();

            var lefts = pageContent.GroupBy(c => c.X).Select(g => new
            {
                X = g.Key,
                Count = g.Count(x => x.X == g.Key)

            }).ToList().OrderBy(c => c.X).ToArray();

            var leftPoint = 0;
            var rightPoint = 0;
            foreach (var left in lefts)
            {
                if (left.Count > 1)
                {
                    leftPoint = left.X;
                    break;
                }
            }
            var rights = lefts.OrderByDescending(c => c.X).ToList();
            foreach (var right in rights)
            {
                if (right.Count > 1)
                {
                    rightPoint = right.X;
                    break;
                }
            }
            var wrongItems = new List<FlatBoxesSame>();
            wrongItems.AddRange(pageContent.Where(c => c.X < leftPoint || c.X > rightPoint).Select(g => new FlatBoxesSame
            {
                X = g.X,
                Y = g.Y,
                Width = g.Width,
                Height = g.Height,
                FileName= g.FileName,
            }).ToList());


            foreach (var item in pageContent)
            {                
                var sameList = pageContent.Where(c => Math.Abs(c.X - item.X) < tolorance
                    || Math.Abs(c.Width - item.Width) < tolorance
                    || Math.Abs(c.Height - item.Height) < tolorance
                    || Math.Abs(c.Right - item.Right) < tolorance).ToList();

                var sameItem = new FlatBoxesSame()
                {
                    FileName = item.FileName,
                    Height = item.Height,
                    Right = item.Right,
                    Width = item.Width,
                    X = item.X,
                    Y = item.Y,
                    XSame = sameList.Count(c => Math.Abs(c.X - item.X) < tolorance),
                    YSame = sameList.Count(c => Math.Abs(c.Y - item.Y) < tolorance),
                    HeightSame = sameList.Count(c => Math.Abs(c.Height - item.Height) < tolorance),
                    RightSame = sameList.Count(c => Math.Abs(c.Right - item.Right) < tolorance),

                };
                sameItems.Add(sameItem);
            }

            var corectItsms = sameItems.Where(c => (c.XSame >= 2 && c.HeightSame >= 2)
                || (c.XSame >= 2 && c.WidthSame >= 2)
                || (c.XSame >= 2 && c.RightSame >= 2)
                || (c.YSame >= 2 && c.HeightSame >= 2)
                || (c.YSame >= 2 || c.WidthSame >= 2))
            .ToList();

            wrongItems.AddRange( sameItems.Except(corectItsms).ToList());

            return wrongItems;
        }


        /// <summary>
        /// Returns the items whose X, Y, Height or Width differs from the
        /// most common combination in the list. Returns an empty list if all match.
        /// </summary>
        public List<FlatBoxes> GetMismatchedBoxes(IEnumerable<FlatBoxes> boxes)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));

            var list = boxes.ToList();
            if (list.Count <= 1) return new List<FlatBoxes>();

            // Group by the four values that must be identical
            var groups = list
                .GroupBy(b => new { b.X, b.Y, b.Height, b.Width })
                .OrderByDescending(g => g.Count())
                .ToList();

            // Everything is the same
            if (groups.Count == 1) return new List<FlatBoxes>();

            // The biggest group is the reference; everything else is a mismatch
            return groups.Skip(1).SelectMany(g => g).ToList();
        }
        /// <summary>
        /// Detects text blocks inside a specific rectangle region of an image.
        /// </summary>
        /// <param name="srcImage">The source image (Mat)</param>
        /// <param name="targetRect">The boundary rectangle to search within</param>
        /// <param name="dilationIterations">Number of times to dilate to join letters</param>
        /// <returns>A list of bounding rectangles for text blocks (in original image coordinates)</returns>
        public static List<Rect> FindTextBlocksInsideRect(Mat srcImage, Rect targetRect, int dilationIterations = 2)
        {
            List<Rect> textBlockRects = new List<Rect>();

            // 1. Ensure the target rectangle stays within image boundaries
            Rect safeRect = targetRect.Intersect(new Rect(0, 0, srcImage.Cols, srcImage.Rows));
            if (safeRect.Width <= 0 || safeRect.Height <= 0)
            {
                return textBlockRects;
            }

            // 2. Extract the Region of Interest (ROI) from the target rectangle
            using Mat roi = new Mat(srcImage, safeRect);

            // 3. Convert the ROI to Grayscale
            using Mat gray = new Mat();
            Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);

            // 4. Threshold into a binary image (Otsu's thresholding for dark text on light background)
            using Mat thresh = new Mat();
            Cv2.Threshold(gray, thresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            // 5. Create a structuring element (Kernel) and DILATE repeatedly to join letters and words
            using Mat kernel = Cv2.GetStructuringElement(OpenCvSharp.MorphShapes.Rect, new Size(15, 5));
            using Mat dilated = new Mat();
            Cv2.Dilate(thresh, dilated, kernel, iterations: dilationIterations);

            // 6. Use findContours() to extract the contour and its bounding box
            Cv2.FindContours(
                dilated,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple

            );

            // 7. Process contours and map coordinates back to the original image space
            foreach (var contour in contours)
            {
                Rect localRect = Cv2.BoundingRect(contour);

                // Filter out minor noise elements
                if (localRect.Width > 10 && localRect.Height > 5)
                {
                    // Translate local ROI coordinates back to original image coordinates
                    Rect globalRect = new Rect(
                        safeRect.X + localRect.X,
                        safeRect.Y + localRect.Y,
                        localRect.Width,
                        localRect.Height
                    );

                    textBlockRects.Add(globalRect);
                }
            }

            return textBlockRects;
        }
    }
}
