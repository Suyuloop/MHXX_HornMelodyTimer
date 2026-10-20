using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace HornMelodyTimer.Services;

public sealed class NoteRecognizer
{
    private const int MaxNotesPerRow = 4;
    private const int MaxRows = 4;
    private const int ScanStep = 1;
    private const int MaxCandidatesPerVariant = 32;
    private const double MaximumMeanChannelError = 36;
    private const double MaximumSkyMeanChannelError = 28;
    private const double MaximumWhiteBackgroundMeanError = 40;
    private const int MaximumWhiteBackgroundColorSpread = 60;
    private const double MaximumPromptBackgroundLuminance = 145;
    private static readonly char[] NoteCodes = ['B', 'G', 'O', 'P', 'R', 'S', 'W', 'Y'];
    private readonly IReadOnlyList<NoteTemplate> _templates = LoadTemplates();

    public IReadOnlyList<string> ReadRows(
        Bitmap bitmap,
        string acceptedColors,
        IReadOnlyCollection<string> acceptedPatterns,
        out IReadOnlyList<string> scannedSequences)
    {
        var sourcePixels = GetPixels(bitmap);
        var candidates = new List<NoteMatch>();
        foreach (var template in _templates)
        {
            if (!acceptedColors.Contains(template.Note))
                continue;

            foreach (var variant in template.Variants)
                FindCandidates(sourcePixels, bitmap.Width, bitmap.Height, variant, candidates);
        }

        var matches = SuppressOverlappingMatches(candidates);
        var sequences = GroupIntoRows(matches)
            .Select(row => string.Concat(row.OrderBy(match => match.Bounds.Left)
                .Take(MaxNotesPerRow)
                .Select(match => match.Note)))
            .Where(sequence => sequence.Length > 0)
            .Take(MaxRows)
            .ToList();

        scannedSequences = sequences;
        var accepted = acceptedPatterns.ToHashSet(StringComparer.Ordinal);
        return sequences.Where(accepted.Contains).ToList();
    }

    private static void FindCandidates(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        NoteTemplateVariant template,
        List<NoteMatch> matches)
    {
        if (template.Width > sourceWidth || template.Height > sourceHeight)
            return;

        var stride = sourceWidth * 4;
        var right = sourceWidth - template.Width;
        var bottom = sourceHeight - template.Height;
        var meanChannelError = template.Note == 'S'
            ? MaximumSkyMeanChannelError
            : MaximumMeanChannelError;
        var maximumError = meanChannelError * template.TotalWeight * 3;
        var maximumQuickError = meanChannelError * template.QuickTotalWeight * 3;
        var variantMatches = new List<NoteMatch>();

        if (template.Note == 'W' && (template.Height < 20 || template.Height > 40))
            return;

        for (var y = 0; y <= bottom; y += ScanStep)
        {
            for (var x = 0; x <= right; x += ScanStep)
            {
                double quickError = 0;
                foreach (var sample in template.QuickSamples)
                {
                    var sourceIndex = (y + sample.Y) * stride + (x + sample.X) * 4;
                    var difference =
                        Math.Abs(source[sourceIndex + 2] - sample.Red) +
                        Math.Abs(source[sourceIndex + 1] - sample.Green) +
                        Math.Abs(source[sourceIndex] - sample.Blue);
                    quickError += difference * sample.Weight;
                    if (quickError > maximumQuickError)
                        break;
                }

                if (quickError > maximumQuickError)
                    continue;

                double error = 0;
                foreach (var sample in template.Samples)
                {
                    var sourceIndex = (y + sample.Y) * stride + (x + sample.X) * 4;
                    var difference =
                        Math.Abs(source[sourceIndex + 2] - sample.Red) +
                        Math.Abs(source[sourceIndex + 1] - sample.Green) +
                        Math.Abs(source[sourceIndex] - sample.Blue);
                    error += difference * sample.Weight;
                    if (error > maximumError)
                        break;
                }

                if (error <= maximumError)
                {
                    if (!TryGetLocalBackground(
                            source,
                            sourceWidth,
                            sourceHeight,
                            x,
                            y,
                            template.Width,
                            template.Height,
                            out var backgroundRed,
                            out var backgroundGreen,
                            out var backgroundBlue) ||
                        GetLuminance(backgroundRed, backgroundGreen, backgroundBlue) >
                                MaximumPromptBackgroundLuminance ||
                        (template.Note == 'W' &&
                         template.BackgroundSamples.Length > 0 &&
                         GetWhiteBackgroundMeanError(
                             source,
                             stride,
                             x,
                             y,
                             template.BackgroundSamples,
                             backgroundRed,
                             backgroundGreen,
                             backgroundBlue) > MaximumWhiteBackgroundMeanError))
                    {
                        continue;
                    }

                    variantMatches.Add(new NoteMatch(
                        template.Note,
                        new Rectangle(x, y, template.Width, template.Height),
                        error / (template.TotalWeight * 3)));
                }
            }
        }

        variantMatches.Sort((left, rightMatch) => left.Error.CompareTo(rightMatch.Error));
        matches.AddRange(variantMatches.Take(MaxCandidatesPerVariant));
    }

    private static bool TryGetLocalBackground(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        int x,
        int y,
        int width,
        int height,
        out byte red,
        out byte green,
        out byte blue)
    {
        Span<byte> reds = stackalloc byte[4];
        Span<byte> greens = stackalloc byte[4];
        Span<byte> blues = stackalloc byte[4];
        var stride = sourceWidth * 4;
        var count = 0;

        for (var corner = 0; corner < 4; corner++)
        {
            var sampleX = corner % 2 == 0 ? x - 2 : x + width + 1;
            var sampleY = corner < 2 ? y - 2 : y + height + 1;
            if (sampleX < 0 || sampleX >= sourceWidth || sampleY < 0 || sampleY >= sourceHeight)
                continue;

            var sourceIndex = sampleY * stride + sampleX * 4;
            reds[count] = source[sourceIndex + 2];
            greens[count] = source[sourceIndex + 1];
            blues[count] = source[sourceIndex];
            count++;
        }

        if (count < 3)
        {
            red = green = blue = 0;
            return false;
        }

        reds[..count].Sort();
        greens[..count].Sort();
        blues[..count].Sort();
        if (reds[count - 1] - reds[0] > MaximumWhiteBackgroundColorSpread ||
            greens[count - 1] - greens[0] > MaximumWhiteBackgroundColorSpread ||
            blues[count - 1] - blues[0] > MaximumWhiteBackgroundColorSpread)
        {
            red = green = blue = 0;
            return false;
        }

        red = (byte)((reds[(count - 1) / 2] + reds[count / 2]) / 2);
        green = (byte)((greens[(count - 1) / 2] + greens[count / 2]) / 2);
        blue = (byte)((blues[(count - 1) / 2] + blues[count / 2]) / 2);
        return true;
    }

    private static double GetWhiteBackgroundMeanError(
        byte[] source,
        int stride,
        int x,
        int y,
        PixelSample[] samples,
        byte backgroundRed,
        byte backgroundGreen,
        byte backgroundBlue)
    {
        double error = 0;
        foreach (var sample in samples)
        {
            var sourceIndex = (y + sample.Y) * stride + (x + sample.X) * 4;
            error +=
                Math.Abs(source[sourceIndex + 2] - backgroundRed) +
                Math.Abs(source[sourceIndex + 1] - backgroundGreen) +
                Math.Abs(source[sourceIndex] - backgroundBlue);
        }

        return error / (samples.Length * 3);
    }

    private static double GetLuminance(byte red, byte green, byte blue) =>
        0.2126 * red + 0.7152 * green + 0.0722 * blue;

    private static IReadOnlyList<NoteMatch> SuppressOverlappingMatches(List<NoteMatch> candidates)
    {
        candidates.Sort((left, right) => left.Error.CompareTo(right.Error));
        var selected = new List<NoteMatch>();
        foreach (var candidate in candidates)
        {
            if (selected.Any(match => IsDuplicate(match.Bounds, candidate.Bounds)))
                continue;
            selected.Add(candidate);
        }
        return selected;
    }

    private static bool IsDuplicate(Rectangle first, Rectangle second)
    {
        var intersection = Rectangle.Intersect(first, second);
        var overlap = intersection.Width * intersection.Height;
        if (overlap > Math.Min(first.Width * first.Height, second.Width * second.Height) * 0.25)
            return true;

        var firstCenterX = first.Left + first.Width / 2d;
        var firstCenterY = first.Top + first.Height / 2d;
        var secondCenterX = second.Left + second.Width / 2d;
        var secondCenterY = second.Top + second.Height / 2d;
        var distance = Math.Sqrt(
            Math.Pow(firstCenterX - secondCenterX, 2) +
            Math.Pow(firstCenterY - secondCenterY, 2));
        return distance < Math.Min(first.Height, second.Height) * 0.6;
    }

    private static IReadOnlyList<IReadOnlyList<NoteMatch>> GroupIntoRows(IReadOnlyList<NoteMatch> matches)
    {
        var rows = new List<List<NoteMatch>>();
        foreach (var match in matches.OrderBy(item => item.Bounds.Top + item.Bounds.Height / 2d))
        {
            var centerY = match.Bounds.Top + match.Bounds.Height / 2d;
            var row = rows.FirstOrDefault(items =>
            {
                var rowCenterY = items.Average(item => item.Bounds.Top + item.Bounds.Height / 2d);
                var rowHeight = items.Average(item => item.Bounds.Height);
                return Math.Abs(rowCenterY - centerY) <= Math.Max(4, Math.Min(rowHeight, match.Bounds.Height) * 0.45);
            });
            if (row is null)
                rows.Add([match]);
            else
                row.Add(match);
        }
        return rows;
    }

    private static byte[] GetPixels(Bitmap bitmap)
    {
        using var normalized = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(normalized))
            graphics.DrawImageUnscaled(bitmap, 0, 0);

        var bounds = new Rectangle(0, 0, normalized.Width, normalized.Height);
        var data = normalized.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[normalized.Width * normalized.Height * 4];
            var rowLength = normalized.Width * 4;
            for (var y = 0; y < normalized.Height; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * rowLength, rowLength);
            return pixels;
        }
        finally
        {
            normalized.UnlockBits(data);
        }
    }

    private static IReadOnlyList<NoteTemplate> LoadTemplates()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "NoteTemplates");
        var templates = new List<NoteTemplate>(NoteCodes.Length);
        foreach (var note in NoteCodes)
        {
            var path = Path.Combine(directory, $"{note}.png");
            if (!File.Exists(path))
                throw new FileNotFoundException($"找不到音符樣板：{path}", path);

            using var source = new Bitmap(path);
            var bounds = FindVisibleBounds(source);
            using var cropped = source.Clone(bounds, PixelFormat.Format32bppArgb);
            templates.Add(new NoteTemplate(note, CreateVariants(note, cropped)));
        }
        return templates;
    }

    private static Rectangle FindVisibleBounds(Bitmap bitmap)
    {
        var left = bitmap.Width;
        var top = bitmap.Height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 20)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left || bottom < top)
            throw new InvalidDataException($"音符樣板沒有可見像素：{bitmap.Width} × {bitmap.Height}");
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static IReadOnlyList<NoteTemplateVariant> CreateVariants(char note, Bitmap source)
    {
        var variants = new List<NoteTemplateVariant>();
        for (var targetHeight = 12; targetHeight <= 44; targetHeight += 4)
        {
            var scale = targetHeight / (double)source.Height;
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            using var resized = new Bitmap(width, targetHeight, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, width, targetHeight));
            }

            var samples = new List<PixelSample>();
            var backgroundSamples = new List<PixelSample>();
            for (var y = 0; y < targetHeight; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var color = resized.GetPixel(x, y);
                    if (color.A < 20 && (x + y) % 2 == 0)
                        backgroundSamples.Add(new PixelSample(x, y, 0, 0, 0, 1));
                    if (color.A < 200 || (x + y) % 2 != 0)
                        continue;
                    samples.Add(new PixelSample(x, y, color.R, color.G, color.B, color.A));
                }
            }

            if (samples.Count > 0)
            {
                var sampleArray = samples.ToArray();
                var sampleInterval = Math.Max(1, sampleArray.Length / 12);
                var quickSamples = sampleArray
                    .Where((_, index) => index % sampleInterval == 0)
                    .Take(12)
                    .ToArray();
                variants.Add(new NoteTemplateVariant(
                    note,
                    width,
                    targetHeight,
                    sampleArray,
                    quickSamples,
                    backgroundSamples.ToArray()));
            }
        }
        return variants;
    }

    private sealed record NoteTemplate(char Note, IReadOnlyList<NoteTemplateVariant> Variants);

    private sealed record NoteTemplateVariant(
        char Note,
        int Width,
        int Height,
        PixelSample[] Samples,
        PixelSample[] QuickSamples,
        PixelSample[] BackgroundSamples)
    {
        public int TotalWeight { get; } = Samples.Sum(sample => sample.Weight);
        public int QuickTotalWeight { get; } = QuickSamples.Sum(sample => sample.Weight);
    }

    private readonly record struct PixelSample(int X, int Y, byte Red, byte Green, byte Blue, byte Weight);
    private readonly record struct NoteMatch(char Note, Rectangle Bounds, double Error);
}
