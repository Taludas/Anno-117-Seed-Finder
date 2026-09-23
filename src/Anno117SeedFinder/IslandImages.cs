using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Top-down images of the islands (island_images\latium, island_images\albion; embedded resources). Every image is the island
// cropped to its outline at 2 pixels per map unit, drawn north-up, i.e. rotation 0 with the image's top edge towards +y.
// The files are 256-colour PNGs with transparency.
internal sealed record IslandImage(BitmapSource Source,int Width,int Height);

internal static class IslandImages
{
 // Pixels per map unit of every island image.
 internal const double PixelsPerUnit=2;
 static readonly Dictionary<string,IslandImage?> Cache=new(StringComparer.OrdinalIgnoreCase);

 // File name of an asset's image; the atlas names some third-party and DLC01 islands differently than the game data.
 internal static string? Path(string assetName)
 {
  if(assetName.StartsWith("celtic_island_",StringComparison.OrdinalIgnoreCase))return $"albion/{assetName}.png";
  var file=assetName
   .Replace("roman_island_3rdparty_","roman_island_3rd_")
   .Replace("roman_dlc01_island_","roman_island_dlc01_")
   .Replace("roman_dlc_01_island_","roman_island_dlc01_");
  return file.StartsWith("roman_island_",StringComparison.OrdinalIgnoreCase)?$"latium/{file}.png":null;
 }

 // null when there is no image for this island (the preview then draws the plain coloured tile).
 internal static IslandImage? Get(string assetName)
 {
  lock(Cache)
  {
   if(Cache.TryGetValue(assetName,out var cached))return cached;
   IslandImage? result=null;
   try
   {
    var path=Path(assetName);
    if(path is not null)
    {
     var uri=new Uri($"pack://application:,,,/island_images/{path}",UriKind.Absolute);
     var natural=BitmapFrame.Create(uri,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
     int width=natural.PixelWidth,height=natural.PixelHeight;
     var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=uri;bitmap.CacheOption=BitmapCacheOption.OnLoad;
     // The largest image (Cinis, 1538 px) is shown at about 200 px: decode the big ones smaller.
     if(width>700)bitmap.DecodePixelWidth=width/2;
     bitmap.EndInit();bitmap.Freeze();
     result=new IslandImage(bitmap,width,height);
    }
   }
   catch(Exception ex) when(ex is IOException or UriFormatException or NotSupportedException or FileFormatException or InvalidOperationException){result=null;}
   Cache[assetName]=result;return result;
  }
 }
}
