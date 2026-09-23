using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

// How the map is framed on a canvas: the isometric projection with a zoom and an origin. The default frame is the standard
// one; a map with the DLC01 area is zoomed out so that its whole enlarged rectangle is visible.
internal sealed record MapView(double Scale,double OriginX,double OriginY)
{
 public static readonly MapView Default=new(1,MapLayoutPositions.WorldOriginX,MapLayoutPositions.WorldOriginY);
 public Point Project(double x,double y)=>new(OriginX+(x-y)*MapLayoutPositions.ScaleX*Scale,OriginY-(x+y)*MapLayoutPositions.ScaleY*Scale);

 // Zoom and origin that centre the given map squares (origin x, origin y, side) on a canvas of the given size.
 public static MapView Fit(IEnumerable<(int X,int Y,int Size)> squares,double width,double height,double margin)
 {
  double minX=double.MaxValue,maxX=double.MinValue,minY=double.MaxValue,maxY=double.MinValue;
  foreach(var square in squares)
   foreach(var corner in new[]{(square.X,square.Y),(square.X+square.Size,square.Y),(square.X+square.Size,square.Y+square.Size),(square.X,square.Y+square.Size)})
   {
    var px=(corner.Item1-corner.Item2)*MapLayoutPositions.ScaleX;var py=-(corner.Item1+corner.Item2)*MapLayoutPositions.ScaleY;
    minX=Math.Min(minX,px);maxX=Math.Max(maxX,px);minY=Math.Min(minY,py);maxY=Math.Max(maxY,py);
   }
  var scale=Math.Min((width-2*margin)/(maxX-minX),(height-2*margin)/(maxY-minY));
  return new(scale,width/2-scale*(minX+maxX)/2,height/2-scale*(minY+maxY)/2);
 }
}

internal static class MapSchematic
{
 internal const double Width=700;
 internal const double Height=920;
 internal const int BaseWorldSize=2048;

 internal static Canvas CreateCanvas(RegionKind region,MapProfile profile,MapLayout? layout=null)
 {
  var canvas=new Canvas{Width=Width,Height=Height,ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Center};
  // A map with the DLC01 area (or DLC01 activated later) is zoomed out so that its whole enlarged rectangle fits the window.
  var view=layout is not null&&layout.BaseSize>0?MapView.Fit([(layout.FullX,layout.FullY,layout.FullSize),(0,0,layout.Width)],Width,Height,14):MapView.Default;
  canvas.Tag=view;
  canvas.Children.Add(new Rectangle{Width=Width,Height=Height,Fill=new SolidColorBrush(Color.FromRgb(24,62,77)),IsHitTestVisible=false});
  // The DLC01 area and the regular map are real rectangles in map coordinates (seen from the isometric side they are
  // diamonds). Without a layout (position picker) they are the template's: regular map 2048 units, DLC01 area 2688, both
  // anchored at the near corner (0,0). With a layout they are the generated map's own rectangles.
  var expansionFill=Color.FromRgb(38,88,105);var expansionStroke=Color.FromRgb(84,139,153);var baseFill=Color.FromRgb(31,72,86);var baseStroke=Color.FromRgb(95,149,160);
  if(layout is null)
  {
   if(region==RegionKind.Latium&&profile.Dlc01)canvas.Children.Add(WorldRectangle(view,0,0,profile.LatiumTemplateSize,expansionFill,expansionStroke));
   canvas.Children.Add(WorldRectangle(view,0,0,BaseWorldSize,baseFill,baseStroke));
   AddThirdParties(canvas,region,profile);
  }
  else if(layout.BaseSize>0)
  {
   canvas.Children.Add(WorldRectangle(view,layout.FullX,layout.FullY,layout.FullSize,expansionFill,expansionStroke));
   canvas.Children.Add(WorldRectangle(view,layout.BaseX,layout.BaseY,layout.BaseSize,baseFill,baseStroke));
  }
  else canvas.Children.Add(WorldRectangle(view,layout.FullX,layout.FullY,layout.FullSize,baseFill,baseStroke));
  return canvas;
 }

 static Polygon WorldRectangle(MapView view,int x,int y,int size,Color fill,Color stroke)
 {
  var polygon=new Polygon{Fill=new SolidColorBrush(fill),Stroke=new SolidColorBrush(stroke),StrokeThickness=2,IsHitTestVisible=false};
  foreach(var corner in new[]{(x,y),(x+size,y),(x+size,y+size),(x,y+size)}){var point=view.Project(corner.Item1,corner.Item2);polygon.Points.Add(point);}
  return polygon;
 }

 // A placed island, third-party island or decoration: the island image, rotated to the island's rotation and fitted into its
 // diamond (the island's outline box seen from the isometric side), over a tinted plate. Islands without an image get the
 // plate alone. Returns the diamond (for hover and tooltip) and its centre.
 internal static (Polygon Diamond,Point Centre) AddPlacedTile(Canvas canvas,LayoutItem item,Brush fill,Brush stroke,double strokeThickness)
 {
  var view=canvas.Tag as MapView??MapView.Default;
  var asset=IslandMasks.Asset(item.Name);var min=asset.Min(item.Rotation);var size=asset.Size(item.Rotation);
  // Centre of the island's outline box in map units.
  double cx=item.X+min.X+size.X/2d,cy=item.Y+min.Y+size.Y/2d;
  var image=IslandImages.Get(item.Name);
  double boxW=size.X,boxH=size.Y;
  if(image is not null)
  {
   var quarter=item.Rotation%2==1;
   boxW=(quarter?image.Height:image.Width)/IslandImages.PixelsPerUnit;boxH=(quarter?image.Width:image.Height)/IslandImages.PixelsPerUnit;
  }
  var diamond=new Polygon{Fill=fill,Stroke=stroke,StrokeThickness=strokeThickness,StrokeLineJoin=PenLineJoin.Round,Cursor=System.Windows.Input.Cursors.Hand};
  foreach(var corner in new[]{(cx-boxW/2,cy-boxH/2),(cx+boxW/2,cy-boxH/2),(cx+boxW/2,cy+boxH/2),(cx-boxW/2,cy+boxH/2)}){diamond.Points.Add(view.Project(corner.Item1,corner.Item2));}
  canvas.Children.Add(diamond);
  if(image is not null)
  {
   var picture=new Image{Source=image.Source,Width=image.Width,Height=image.Height,Stretch=Stretch.Fill,IsHitTestVisible=false,RenderTransform=ImageTransform(view,image,item.Rotation,cx,cy)};
   RenderOptions.SetBitmapScalingMode(picture,BitmapScalingMode.HighQuality);
   canvas.Children.Add(picture);
  }
  return(diamond,view.Project(cx,cy));
 }

 // Maps the image's own pixel grid onto the canvas. The image is drawn north-up: its top edge points towards +y of the map, its
 // right edge towards +x. A rotation r turns the island by r quarter turns from +x towards +y, which on the image (viewed north-up)
 // is counter-clockwise. The map's isometric projection then places +x up-right and +y up-left.
 static Transform ImageTransform(MapView view,IslandImage image,byte rotation,double centreX,double centreY)
 {
  var origin=view.Project(centreX,centreY);
  (double X,double Y) Map(double column,double row)
  {
   var east=(column-image.Width/2d)/IslandImages.PixelsPerUnit;var north=-(row-image.Height/2d)/IslandImages.PixelsPerUnit;
   var(x,y)=rotation switch{0=>(east,north),1=>(-north,east),2=>(-east,-north),_=>(north,-east)};
   return(origin.X+(x-y)*MapLayoutPositions.ScaleX*view.Scale,origin.Y-(x+y)*MapLayoutPositions.ScaleY*view.Scale);
  }
  var zero=Map(0,0);var right=Map(1,0);var down=Map(0,1);
  return new MatrixTransform(right.X-zero.X,right.Y-zero.Y,down.X-zero.X,down.Y-zero.Y,zero.X,zero.Y);
 }

 // Size class of an asset for the tile colours: XL, L, M, S (and C for the continental island).
 internal static string SizeClass(string assetName)=>assetName.Contains("continental")?"C":assetName.Contains("extralarge")?"XL":assetName.Contains("_large_")?"L":assetName.Contains("_medium_")?"M":"S";

 internal static void AddTile(Canvas canvas,MapPositionDefinition position,FrameworkElement tile)
 {
  var hitSize=MarkerSize(position.Size);Canvas.SetLeft(tile,position.X-hitSize/2);Canvas.SetTop(tile,position.Y-hitSize/2);canvas.Children.Add(tile);
 }

 internal static FrameworkElement CreateLayoutTile(MapPositionDefinition position)
 {
  var side=TileSize(position.Size);var (fill,stroke,text)=TileColors(position.Size);
  var tile=new Border{Width=side,Height=side,Background=fill,BorderBrush=stroke,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(3),IsHitTestVisible=false,RenderTransform=new RotateTransform(45),RenderTransformOrigin=new Point(.5,.5),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
  var label=position.SlotIndex<0?"CINIS":$"{position.Size}\n#{position.SlotIndex}";
  tile.Child=new TextBlock{Text=label,TextAlignment=TextAlignment.Center,Foreground=text,FontFamily=new FontFamily("Georgia"),FontSize=position.Size is "XL" or "C"?25:position.Size=="L"?21:18,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,RenderTransform=new RotateTransform(-45),RenderTransformOrigin=new Point(.5,.5)};
  var host=new Grid{Width=MarkerSize(position.Size),Height=MarkerSize(position.Size),IsHitTestVisible=false};host.Children.Add(tile);return host;
 }

 internal static double TileSize(string size)=>size switch{"XL"=>94,"L"=>78,"M"=>60,"S"=>46,"C"=>100,_=>64};
 internal static double MarkerSize(string size)=>Math.Ceiling(TileSize(size)*Math.Sqrt(2))+10;
 internal static (Brush Fill,Brush Stroke,Brush Text) TileColors(string size)=>size switch
 {
  "XL" or "C"=>(new SolidColorBrush(Color.FromRgb(91,0,43)),new SolidColorBrush(Color.FromRgb(158,173,34)),new SolidColorBrush(Color.FromRgb(242,218,182))),
  "L"=>(new SolidColorBrush(Color.FromRgb(26,3,22)),new SolidColorBrush(Color.FromRgb(24,178,161)),new SolidColorBrush(Color.FromRgb(242,218,182))),
  _=>(new SolidColorBrush(Color.FromRgb(211,183,151)),new SolidColorBrush(Color.FromRgb(24,178,161)),new SolidColorBrush(Color.FromRgb(86,26,41)))
 };

 static void AddThirdParties(Canvas canvas,RegionKind region,MapProfile profile)
 {
  foreach(var special in MapLayoutPositions.SpecialPoints(profile,region))
  {
   var raider=special.Kind=="Raider";
   AddStaticTile(canvas,(special.X,special.Y),raider?"RAIDER":"3RD",raider?52:38,new SolidColorBrush(Color.FromRgb(211,183,151)),new SolidColorBrush(raider?Color.FromRgb(166,33,54):Color.FromRgb(192,112,180)),raider?13:11);
  }
 }

 static void AddStaticTile(Canvas canvas,(double X,double Y) point,string text,double side,Brush fill,Brush stroke,double fontSize)
 {
  var tile=new Border{Width=side,Height=side,Background=fill,BorderBrush=stroke,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(3),IsHitTestVisible=false,RenderTransform=new RotateTransform(45),RenderTransformOrigin=new Point(.5,.5)};
  tile.Child=new TextBlock{Text=text,Foreground=new SolidColorBrush(Color.FromRgb(86,26,41)),FontFamily=new FontFamily("Georgia"),FontWeight=FontWeights.SemiBold,FontSize=fontSize,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,RenderTransform=new RotateTransform(-45),RenderTransformOrigin=new Point(.5,.5)};
  Canvas.SetLeft(tile,point.X-side/2);Canvas.SetTop(tile,point.Y-side/2);canvas.Children.Add(tile);
 }
}
