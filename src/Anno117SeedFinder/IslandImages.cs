using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Top-down images of the islands for the seed preview, drawn north-up (rotation 0 with the image's top edge towards +y) and cropped
// to the island's outline box. Two styles (embedded resources):
//  - Tiles (island_tiles): rendered from the island files' tile grids at 1 pixel per map unit, one flat colour per tile type
#if GAME21
//    (buildable, marsh, river, harbour, unbuildable). Every island has one.
#else
//    (buildable, marsh, fertile ground, riverbank, river, harbour, unbuildable). Every island of all three regions has one.
#endif
//  - Artwork (island_images\latium, island_images\albion): the game's island artwork at 2 pixels per map unit, 256-colour PNGs.
#if !GAME21
//    Aegyptus (island_images\egypt): the game's gamemapimage.png of every piece, cropped to its outline box at up to 1.25 pixels
//    per map unit (the scratch script build_egypt_artwork.py, after the map editor's Egypt image builder), plus the four quadrant
//    images, the ground of each Nile complex.
#endif
internal sealed record IslandImage(BitmapSource Source,int Width,int Height,double PixelsPerUnit,bool Pixelated);
internal enum IslandImageStyle{Tiles,Artwork}

internal static class IslandImages
{
 internal static IslandImageStyle Style{get;set;}=IslandImageStyle.Tiles;
 static readonly Dictionary<(IslandImageStyle,string),IslandImage?> Cache=[];

 // File name of an asset's artwork; the atlas names some third-party and DLC01 islands differently than the game data.
 internal static string? ArtworkPath(string assetName)
 {
  if(assetName.StartsWith("celtic_island_",StringComparison.OrdinalIgnoreCase))return $"island_images/albion/{assetName}.png";
#if !GAME21
  if(assetName.StartsWith("egypt_",StringComparison.OrdinalIgnoreCase))return $"island_images/egypt/{assetName}.png";
#endif
  var file=assetName
   .Replace("roman_island_3rdparty_","roman_island_3rd_")
   .Replace("roman_dlc01_island_","roman_island_dlc01_")
   .Replace("roman_dlc_01_island_","roman_island_dlc01_");
  return file.StartsWith("roman_island_",StringComparison.OrdinalIgnoreCase)?$"island_images/latium/{file}.png":null;
 }

 // null when there is no image for this island (the preview then draws the plain coloured tile).
 internal static IslandImage? Get(string assetName)
 {
  var style=Style;
  var key=(style,assetName.ToLowerInvariant());
  lock(Cache)
  {
   if(Cache.TryGetValue(key,out var cached))return cached;
   IslandImage? result=null;
   try
   {
    var path=style==IslandImageStyle.Tiles?$"island_tiles/{assetName}.png":ArtworkPath(assetName);
    if(path is not null)
    {
     var uri=new Uri($"pack://application:,,,/{path}",UriKind.Absolute);
     var natural=BitmapFrame.Create(uri,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
     int width=natural.PixelWidth,height=natural.PixelHeight;
     var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=uri;bitmap.CacheOption=BitmapCacheOption.OnLoad;
     // The largest artwork (Cinis, 1538 px) is shown at about 200 px: decode the big ones smaller. The tile images are small
     // (1 pixel per unit) and stay at full resolution so that zooming in shows every tile.
#if GAME21
     if(style==IslandImageStyle.Artwork&&width>700)bitmap.DecodePixelWidth=width/2;
#else
     var egypt=assetName.StartsWith("egypt_",StringComparison.OrdinalIgnoreCase);
     if(style==IslandImageStyle.Artwork&&width>700&&!egypt)bitmap.DecodePixelWidth=width/2;
#endif
     bitmap.EndInit();bitmap.Freeze();
#if GAME21
     result=style==IslandImageStyle.Tiles?new IslandImage(bitmap,width,height,1,true):new IslandImage(bitmap,width,height,2,false);
#else
     // The Egypt artwork has its own scale per piece: the image spans exactly the piece's outline box.
     double artworkScale=egypt?(double)width/(IslandMasks.Asset(assetName).X1-IslandMasks.Asset(assetName).X0):2;
     result=style==IslandImageStyle.Tiles?new IslandImage(bitmap,width,height,1,true):new IslandImage(bitmap,width,height,artworkScale,false);
#endif
    }
   }
   catch(Exception ex) when(ex is IOException or UriFormatException or NotSupportedException or FileFormatException or InvalidOperationException){result=null;}
   Cache[key]=result;return result;
  }
 }
}
