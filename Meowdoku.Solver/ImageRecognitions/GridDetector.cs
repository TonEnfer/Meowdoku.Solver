using System.Drawing;
using Microsoft.Extensions.Localization;
using OpenCvSharp;
using TreeStructures.Linq;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Meowdoku.Solver.ImageRecognitions;

internal class GridDetector(IStringLocalizer<Localization> localizer)
{
    public IStringLocalizer<Localization> Localizer { get; } = localizer;

    public class GridCell
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public Color Color { get; set; }
        public Rect BoundingBox { get; set; }
    }

    public class GridResult
    {
        public int Rows { get; set; }
        public int Cols { get; set; }
        public List<GridCell> Cells { get; init; } = [];
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
    }

    /// <summary>
    /// Основной метод для распознавания сетки из массива байтов
    /// </summary>
    public GridResult DetectGrid(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0)
            throw new ArgumentException(Localizer[nameof(Localization.FileSizeTooSmallText)].Value);

        using var image = LoadImageFromBytes(imageData);
        return DetectGrid(image);
    }

    /// <summary>
    /// Загрузка изображения из массива байтов
    /// </summary>
    private static Mat LoadImageFromBytes(byte[] imageData)
    {
        // Способ 1: Используем ImDecode (самый правильный способ)
        var mat = Cv2.ImDecode(imageData, ImreadModes.AnyColor | ImreadModes.AnyDepth);
        // return mat;
        if (mat != null && !mat.Empty())
            return mat;
        throw new Exception();
        // Способ 2: Используем MemoryStream и Bitmap (если ImDecode не сработал)
        // using var ms = new MemoryStream(imageData);
        // using var bitmap = new Bitmap(ms);
        // return BitmapConverter.ToMat(bitmap);
    }

    /// <summary>
    /// Основной алгоритм распознавания сетки
    /// </summary>
    public GridResult DetectGrid(Mat image)
    {
        var result = new GridResult
        {
            ImageWidth = image.Width,
            ImageHeight = image.Height
        };

        // 1. Преобразуем в оттенки серого
        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY, hint: AlgorithmHint.Accurate);

        // 2. Размытие для уменьшения шума
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);


        // 3. Детектор границ Canny
        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 50, 250, 5);

        // 4. Находим контуры
        Cv2.FindContours(
            edges,
            out var contours,
            out var hierarchy,
            RetrievalModes.Tree,
            ContourApproximationModes.ApproxTC89KCOS
        );

        var allContours = contours
            .Select((x, i) => new
            {
                Id = i,
                Contour = x,
                Area = Cv2.ContourArea(x),
                Hierarchy = hierarchy[i]
            })
            .ToList();

        var roots = allContours
            .Where(x => x.Hierarchy.Parent == -1)
            .OrderBy(x => x.Area)
            .Select(x =>
                x.AsValuedTreeNode(node => allContours.Where(child => child.Hierarchy.Parent == node.Id), node => node))
            .ToList();

        // 5. Фильтруем контуры, которые похожи на квадраты
        var boundingBoxes = new List<(Rect rect, Scalar color)>();

        var totalArea = image.Width * image.Height;

        var gameField = roots.MaxBy(x => x.Value.Area);
        if (gameField?.Children.Count() == 1)
        {
            gameField = gameField.Children.FirstOrDefault();
        }

        foreach (var rootNode in gameField?.Children
                     .Where(x => x.Value.Area >= 100 && x.Value.Area <= totalArea * 0.5) ?? [])
        {
            var epsilon = 0.02 * Cv2.ArcLength(rootNode.Value.Contour, true);
            // Аппроксимируем контур полигоном
            var approx = Cv2.ApproxPolyDP(rootNode.Value.Contour, epsilon, true);

            // Квадрат должен иметь 4 вершины
            if (approx.Length != 4)
                continue;

            // Проверяем, что это выпуклый четырёхугольник
            if (!Cv2.IsContourConvex(approx))
                continue;

            // Получаем ограничивающий прямоугольник
            var rect = Cv2.BoundingRect(approx);

            // Проверяем соотношение сторон (должно быть близко к квадрату)
            var aspectRatio = (double)rect.Width / rect.Height;
            if (aspectRatio is < 0.9 or > 1.1)
                continue;

            if (rootNode.Children.Any(x => x.Children.Any()))
            {
                Console.WriteLine(rootNode.Value.Id);
            }

            var descend = rootNode.DescendFirstMatches(x =>
                    Cv2.ApproxPolyDP(x.Value.Contour, 0.05 * Cv2.ArcLength(x.Value.Contour, true), true).Length != 4
                ).Select(x => x.Value.Contour)
                .ToList();

            using var mask = new Mat([rect.Height, rect.Width], MatType.CV_8U);
            mask.SetTo(new Scalar(0, 0, 0));

            var moments = Cv2.Moments(rootNode.Value.Contour);
            var center = new Point(moments.M10 / moments.M00, moments.M01 / moments.M00);

            var resized = rootNode.Value.Contour.Select(x => ((x - center) * 0.97) + center).ToList();

            Cv2.FillPoly(
                mask,
                [resized],
                new Scalar(255, 255, 255),
                offset: new Point(-rect.TopLeft.X, -rect.TopLeft.Y)
            );

            if (descend.Count != 0)
            {
                Cv2.DrawContours(
                    mask,
                    descend,
                    -1,
                    new Scalar(0, 0, 0),
                    2,
                    offset: new Point(-rect.TopLeft.X, -rect.TopLeft.Y)
                );

                Cv2.FillPoly(
                    mask,
                    descend,
                    new Scalar(0, 0, 0),
                    offset: new Point(-rect.TopLeft.X, -rect.TopLeft.Y)
                );
            }

            // Проверяем заполненность квадрата (цветные квадраты, а не рамки)
            using var roi = new Mat(image, rect);
            var meanColor = Cv2.Mean(roi, mask.Empty() ? null! : mask);
            var grayMean = (meanColor[0] + meanColor[1] + meanColor[2]) / 3;

            // Пропускаем белые/очень светлые квадраты (фон)
            if (grayMean > 230)
                continue;

            boundingBoxes.Add(new ValueTuple<Rect, Scalar>(rect, meanColor));
        }

        if (boundingBoxes.Count < 4)
            throw new Exception(
                Localizer[nameof(Localization.TooLittleSquaresErrorText), boundingBoxes.Count].Value
            );

        // 6. Сортируем по строке и столбцу
        var maxHeight = boundingBoxes.Max(rect => rect.rect.Height);
        var maxWidth = boundingBoxes.Max(rect => rect.rect.Width);

        var filtered = boundingBoxes
            .Where(rect => rect.rect.Height / (double)maxHeight > 0.95 && rect.rect.Width / (double)maxWidth > 0.95)
            .ToList();

        var sorted = SortGrid(filtered);

        // 7. Строим результат
        result.Rows = sorted.GetLength(0);
        result.Cols = sorted.GetLength(1);

        for (var row = 0; row < sorted.GetLength(0); row++)
        {
            for (var col = 0; col < sorted.GetLength(1); col++)
            {
                var rect = sorted[row, col];
                // using var roi = new Mat(image, rect.rect);
                // var avg = Cv2.Mean(roi);

                result.Cells.Add(new GridCell
                {
                    Row = row,
                    Col = col,
                    Color = Color.FromArgb(
                        (int)Math.Round(rect.color[2]), // R
                        (int)Math.Round(rect.color[1]), // G
                        (int)Math.Round(rect.color[0]) // B
                    ),
                    BoundingBox = rect.rect
                });
            }
        }

        // 4. Группируем ячейки по цветам (кластеризация)
        var clusteredResult = ClusterColorsByGroups(result, result.Cols);

        return clusteredResult;
    }

    /// <summary>
    /// Группирует цвета с помощью кластеризации (K-Means)
    /// </summary>
    private GridResult ClusterColorsByGroups(GridResult result, int nClusters)
    {
        // 1. Собираем все цвета
        var allColors = result.Cells.Select(c => c.Color).ToList();

        // 2. Применяем кластеризацию для группировки цветов
        var clusters = ClusterColors(allColors, nClusters);

        // 3. Для каждого кластера вычисляем средний цвет и сортируем по яркости
        var clusterColors = clusters.Select(AverageColors).OrderBy(GetBrightness).ToList();

        foreach (var resultCell in result.Cells)
        {
            var groupId = FindClosestCluster(resultCell.Color, clusterColors);
            resultCell.Color = clusterColors[groupId];
        }

        // 6. Обновляем результат
        var clusteredResult = new GridResult
        {
            Rows = result.Rows,
            Cols = result.Cols,
            ImageWidth = result.ImageWidth,
            ImageHeight = result.ImageHeight,
            Cells = result.Cells,
        };

        return clusteredResult;
    }

    /// <summary>
    /// Кластеризация цветов методом K-Means
    /// </summary>
    private List<List<Color>> ClusterColors(List<Color> colors, int k)
    {
        if (colors.Count == 0)
            return [];

        if (k <= 0)
            throw new ArgumentException(
                Localizer[nameof(Localization.GridDetector_ClusterColors_Clusters_count_must_be_positive)].Value,
                nameof(k));

        if (colors.Count <= k)
        {
            // Если цветов меньше или равно k, каждый цвет - отдельный кластер
            return colors.Select(c => new List<Color> { c }).ToList();
        }

        // Инициализация центров кластеров (выбираем k случайных цветов)
        var random = new Random();
        var centers = colors.OrderBy(x => random.Next()).Take(k).ToList();
        var clusters = new List<List<Color>>();
        var maxIterations = 200;
        var changed = true;
        var iteration = 0;

        while (changed && iteration < maxIterations)
        {
            iteration++;
            changed = false;

            // Очищаем кластеры
            clusters = [];
            for (var i = 0; i < k; i++)
                clusters.Add([]);

            // Распределяем точки по кластерам
            foreach (var color in colors)
            {
                var nearestIndex = FindClosestCluster(color, centers);
                clusters[nearestIndex].Add(color);
            }

            // Пересчитываем центры
            var newCenters = new List<Color>();
            for (var i = 0; i < k; i++)
            {
                if (clusters[i].Count > 0)
                {
                    var newCenter = AverageColors(clusters[i]);
                    newCenters.Add(newCenter);

                    // Проверяем, изменился ли центр
                    if (!ColorsAreSimilar(centers[i], newCenter, 3))
                        changed = true;
                }
                else
                {
                    // Если кластер пуст, создаем случайный центр
                    newCenters.Add(colors[random.Next(colors.Count)]);
                    changed = true;
                }
            }

            centers = newCenters;
        }

        // Удаляем пустые кластеры
        clusters = clusters.Where(c => c.Count > 0).ToList();

        return clusters;
    }

    /// <summary>
    /// Находит ближайший кластер для цвета
    /// </summary>
    private static int FindClosestCluster(Color color, List<Color> cluster)
    {
        var minIndex = 0;
        var minDistance = double.MaxValue;

        for (var i = 0; i < cluster.Count; i++)
        {
            var distance = ColorDistance(color, cluster[i]);
            if (!(distance < minDistance)) continue;

            minDistance = distance;
            minIndex = i;
        }

        return minIndex;
    }

    /// <summary>
    /// Вычисляет расстояние между двумя цветами (в пространстве RGB)
    /// </summary>
    private static double ColorDistance(Color c1, Color c2)
    {
        return Math.Sqrt(
            Math.Pow(c1.R - c2.R, 2) +
            Math.Pow(c1.G - c2.G, 2) +
            Math.Pow(c1.B - c2.B, 2)
        );
    }

    /// <summary>
    /// Вычисляет яркость цвета
    /// </summary>
    private static double GetBrightness(Color color)
    {
        return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
    }

    /// <summary>
    /// Усредняет список цветов
    /// </summary>
    private static Color AverageColors(List<Color> colors)
    {
        if (colors == null || colors.Count == 0)
            return Color.Black;
        var result = Color.FromArgb(
            (int)Math.Round(colors.Average(x => x.R)),
            (int)Math.Round(colors.Average(x => x.G)),
            (int)Math.Round(colors.Average(x => x.B))
        );

        return result;
    }

    private static bool ColorsAreSimilar(Color c1, Color c2, int tolerance)
    {
        return Math.Abs(c1.R - c2.R) <= tolerance &&
               Math.Abs(c1.G - c2.G) <= tolerance &&
               Math.Abs(c1.B - c2.B) <= tolerance;
    }

    private (Rect rect, Scalar color)[,] SortGrid(List<(Rect rect, Scalar color)> rectangles)
    {
        // Сортируем по Y (строкам)
        var sortedByY = rectangles.OrderBy(r => r.rect.Y).ToList();

        // Определяем строки (группируем по Y с учётом высоты)
        var rows = new List<List<(Rect rect, Scalar color)>>();
        var tolerance = sortedByY.Average(r => r.rect.Height) * 0.5;

        foreach (var rect in sortedByY)
        {
            var added = false;
            foreach (var row in rows.Where(row => Math.Abs(row[0].rect.Y - rect.rect.Y) < tolerance))
            {
                row.Add(rect);
                added = true;
                break;
            }

            if (!added)
            {
                rows.Add([rect]);
            }
        }

        // Сортируем в каждой строке по X
        foreach (var row in rows)
        {
            row.Sort((a, b) => a.rect.X.CompareTo(b.rect.X));
        }

        // Проверяем, что все строки имеют одинаковое количество столбцов
        var cols = rows[0].Count;
        foreach (var row in rows)
        {
            if (row.Count != cols)
                throw new Exception(
                    Localizer[nameof(Localization.ColumnsCountMismatchErrorText), cols, rows.Count]
                        .Value);
        }

        // Преобразуем в двумерный массив
        var result = new (Rect rect, Scalar color)[rows.Count, cols];
        for (var row = 0; row < rows.Count; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                result[row, col] = rows[row][col];
            }
        }

        return result;
    }
}