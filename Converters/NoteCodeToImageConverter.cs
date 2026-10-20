using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace HornMelodyTimer.Converters;

public sealed class NoteCodeToImageConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<char, BitmapImage> Images = LoadImages();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is char note && Images.TryGetValue(note, out var image))
            return image;
        return System.Windows.Data.Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;

    private static IReadOnlyDictionary<char, BitmapImage> LoadImages()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "NoteTemplates");
        var images = new Dictionary<char, BitmapImage>();
        foreach (var note in "BGOPRSWY")
        {
            var path = Path.Combine(directory, $"{note}.png");
            if (!File.Exists(path))
                throw new FileNotFoundException($"找不到音符圖示：{path}", path);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            images.Add(note, image);
        }

        return images;
    }
}
