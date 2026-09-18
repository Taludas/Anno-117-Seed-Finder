using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

internal static class MapSchematic
{
 internal const double Width=700;
 internal const double Height=920;

 internal static Canvas CreateCanvas(RegionKind region,MapProfile profile)
 {
  var canvas=new Canvas{Width=Width,Height=Height,ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Center};
  canvas.Children.Add(new Rectangle{Width=Width,Height=Height,Fill=new SolidColorBrush(Color.FromRgb(24,62,77)),IsHitTestVisible=false});
  if(region==RegionKind.Latium)
  {
   var expansion=new Polygon{Fill=new SolidColorBrush(Color.FromRgb(38,88,105)),Stroke=new SolidColorBrush(Color.FromRgb(84,139,153)),StrokeThickness=2,IsHitTestVisible=false};
   foreach(var point in new[]{(0d,205d),(82d,165d),(175d,70d),(290d,15d),(460d,15d),(570d,76d),(655d,155d),(700d,235d),(700d,325d),(612d,316d),(532d,258d),(452d,175d),(350d,186d),(264d,170d),(185d,232d),(120d,305d),(48d,356d),(0d,358d)})expansion.Points.Add(new Point(point.Item1,point.Item2));
   canvas.Children.Add(expansion);
  }
  var baseWorld=new Polygon{Fill=new SolidColorBrush(Color.FromRgb(31,72,86)),Stroke=new SolidColorBrush(Color.FromRgb(95,149,160)),StrokeThickness=2,IsHitTestVisible=false};
  foreach(var point in new[]{(Width/2,90d),(Width-18d,Height/2),(Width/2,Height-32d),(18d,Height/2)})baseWorld.Points.Add(new Point(point.Item1,point.Item2));
  canvas.Children.Add(baseWorld);
  AddThirdParties(canvas,region,profile);
  return canvas;
 }

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
