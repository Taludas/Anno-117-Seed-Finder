using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

internal sealed class SeedPreviewWindow:Window
{
 const double DesiredWidth=1540;
 const double DesiredHeight=1160;
 static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(25,36,52));
 static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(102,112,133));
 static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(138,91,36));
 readonly List<(FrameworkElement Target,ToolTip ToolTip)> tooltipTargets=[];
 readonly MapProfile profile;

 public SeedPreviewWindow(uint seed,MapProfile? profile=null,FertilitySetting fertilitySetting=FertilitySetting.Abundant,SlotSetting slotSetting=SlotSetting.Abundant)
 {
  this.profile=profile??MapProfiles.Default;
  Title=Localization.Instance.Format("PreviewTitleFormat",seed);Width=DesiredWidth;Height=DesiredHeight;MinWidth=1120;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(243,245,248));
  SourceInitialized+=(_,_)=>FitToMonitor();
  // The layouts are the real placement of the map (island positions, rotations, decorations and third-party islands).
  var latiumLayout=new MapLayout();var albionLayout=new MapLayout();
  var latium=Generator.GenerateLatium(seed,new GeneratorScratch(),this.profile,fertilitySetting:fertilitySetting,slotSetting:slotSetting,layout:latiumLayout);
  var albion=AlbionGenerator.Generate(seed,this.profile,fertilitySetting:fertilitySetting,slotSetting:slotSetting,layout:albionLayout);

  var root=new DockPanel{Margin=new Thickness(22)};Content=root;
  var header=new StackPanel{Margin=new Thickness(4,0,4,14)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  header.Children.Add(new TextBlock{Text=$"Seed {seed:N0}",FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=Ink});
  header.Children.Add(new TextBlock{Text=this.profile.Dlc01?Localization.Instance.Format("PreviewSubtitleDlcFormat",this.profile.Template,this.profile.Size):Localization.Instance.Format("PreviewSubtitleNoDlcFormat",this.profile.Template,this.profile.Size),Foreground=Muted,Margin=new Thickness(0,4,0,0)});

  var scroll=new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(scroll);
  var maps=new Grid{MinWidth=1300};maps.ColumnDefinitions.Add(new ColumnDefinition());maps.ColumnDefinitions.Add(new ColumnDefinition());scroll.Content=maps;
  maps.Children.Add(BuildMap(this.profile.Dlc01?"Latium + Prophecies of Ash":"Latium",RegionKind.Latium,latium.Islands,latiumLayout));
  var albionMap=BuildMap("Albion",RegionKind.Albion,albion,albionLayout);Grid.SetColumn(albionMap,1);maps.Children.Add(albionMap);
 }

 void FitToMonitor()
 {
  var ownHandle=new WindowInteropHelper(this).Handle;var ownerHandle=Owner is null?IntPtr.Zero:new WindowInteropHelper(Owner).Handle;var reference=ownerHandle!=IntPtr.Zero?ownerHandle:ownHandle;
  var monitor=MonitorFromWindow(reference,2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return;
  var dpi=GetDpiForWindow(reference);if(dpi==0)dpi=96;const double margin=16;var availableWidth=Math.Max(640,(info.Work.Right-info.Work.Left)*96d/dpi-margin);var availableHeight=Math.Max(480,(info.Work.Bottom-info.Work.Top)*96d/dpi-margin);
  MinWidth=Math.Min(MinWidth,availableWidth);MinHeight=Math.Min(MinHeight,availableHeight);MaxWidth=availableWidth;MaxHeight=availableHeight;Width=Math.Min(DesiredWidth,availableWidth);Height=Math.Min(DesiredHeight,availableHeight);
 }

 Border BuildMap(string title,RegionKind region,IReadOnlyList<GeneratedIsland> islands,MapLayout layout)
 {
  var card=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(10),Margin=new Thickness(5),Padding=new Thickness(12),BorderBrush=new SolidColorBrush(Color.FromRgb(228,231,236)),BorderThickness=new Thickness(1)};
  var body=new StackPanel();card.Child=body;
  body.Children.Add(new TextBlock{Text=title,FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=Ink,Margin=new Thickness(3,0,0,8)});
  var canvas=MapSchematic.CreateCanvas(region,profile,layout);body.Children.Add(canvas);
  var bySlot=islands.ToDictionary(island=>island.SlotIndex);
  // Decorations and third-party islands first (underneath), then the islands with their tooltips.
  foreach(var item in layout.Items.Where(x=>x.Kind!=MapLayout.Island))AddBackdropTile(canvas,item);
  foreach(var item in layout.Items.Where(x=>x.Kind==MapLayout.Island))
  {
   if(!bySlot.TryGetValue(item.Slot,out var island))continue;
   AddIslandTile(canvas,item,island,region);
  }
  return card;
 }

 static void AddBackdropTile(Canvas canvas,LayoutItem item)
 {
  var special=item.Kind==MapLayout.Special;var raider=item.Name.Contains("pirate");
  var stroke=special?new SolidColorBrush(raider?Color.FromRgb(166,33,54):Color.FromRgb(192,112,180)):new SolidColorBrush(Color.FromRgb(95,149,160));
  // Third-party islands get a plum plate (trader) or a dark red one (raider) to match their outline; decorations a dark, translucent one.
  Brush fill=special?new SolidColorBrush(raider?Color.FromRgb(86,24,36):Color.FromRgb(74,40,72)):new SolidColorBrush(Color.FromRgb(20,50,62)){Opacity=.55};
  var(diamond,centre)=MapSchematic.AddPlacedTile(canvas,item,fill,stroke,special?1.6:1);
  diamond.IsHitTestVisible=false;
  if(special)AddLabel(canvas,centre,raider?"RAIDER":"3RD",10);
 }

 // A tag centred on a tile: white Georgia bold with a dark halo, so it reads on the plate and on any terrain.
 static void AddLabel(Canvas canvas,Point centre,string text,double fontSize)
 {
  var label=new TextBlock{Text=text,TextAlignment=TextAlignment.Center,Foreground=Brushes.White,FontFamily=new FontFamily("Georgia"),FontSize=fontSize,FontWeight=FontWeights.Bold,IsHitTestVisible=false,Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=4,ShadowDepth=0,Opacity=1}};
  label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
  Canvas.SetLeft(label,centre.X-label.DesiredSize.Width/2);Canvas.SetTop(label,centre.Y-label.DesiredSize.Height/2);canvas.Children.Add(label);
 }

 void AddIslandTile(Canvas canvas,LayoutItem item,GeneratedIsland island,RegionKind region)
 {
  var sizeClass=MapSchematic.SizeClass(item.Name);
  // Opaque plate under the island image: the schematic's burgundy (XL, Cinis) and near-black (large), and darks from the app's own
  // palette for the light size classes, slate blue (medium) and a lighter shade of it (small), so the greens and rocks of the images stand out.
  var(schematicFill,stroke,_)=MapSchematic.TileColors(sizeClass);
  Brush fill=sizeClass switch{"M"=>new SolidColorBrush(Color.FromRgb(40,62,92)),"S"=>new SolidColorBrush(Color.FromRgb(74,104,145)),_=>schematicFill};
  var roleColor=RoleColor(island.FertilitySet);
  var(diamond,centre)=MapSchematic.AddPlacedTile(canvas,item,fill,stroke,2);
  var position=new MapPositionDefinition(item.Slot,item.Slot<0?"Cinis":$"#{item.Slot} · {sizeClass}",sizeClass,centre.X,centre.Y,0,0);
  var tooltip=IslandTooltip(position,island,region,island.Sites);
  diamond.ToolTip=tooltip;ToolTipService.SetInitialShowDelay(diamond,0);ToolTipService.SetBetweenShowDelay(diamond,0);ToolTipService.SetShowDuration(diamond,30000);
  tooltipTargets.Add((diamond,tooltip));
  diamond.MouseEnter+=(_,_)=>{diamond.Stroke=roleColor;diamond.StrokeThickness=4;};
  diamond.MouseLeave+=(_,_)=>{diamond.Stroke=stroke;diamond.StrokeThickness=2;};
  // Over an image the label is small and one line; on a plain plate (no image) it is the large two-line label.
  var hasImage=IslandImages.Get(item.Name) is not null;
  var caption=item.Slot<0?"CINIS":hasImage?$"{sizeClass} #{item.Slot}":$"{sizeClass}\n#{item.Slot}";
  AddLabel(canvas,centre,caption,hasImage?(sizeClass=="C"?20:sizeClass is "XL" or "L"?13:11):(sizeClass is "XL" or "C"?20:17));
 }

 static ToolTip IslandTooltip(MapPositionDefinition position,GeneratedIsland island,RegionKind region,SiteCounts? sites)
 {
  var panel=new StackPanel{Margin=new Thickness(5),MinWidth=220};
  panel.Children.Add(new TextBlock{Text=$"{position.Label} · {Role(island.FertilitySet)}",FontWeight=FontWeights.SemiBold,FontSize=14,Foreground=Brushes.White});
  panel.Children.Add(new TextBlock{Text=island.Name,Foreground=new SolidColorBrush(Color.FromRgb(203,213,225)),FontSize=11,Margin=new Thickness(0,2,0,8)});
  var fertilityRow=new WrapPanel();panel.Children.Add(fertilityRow);
  foreach(var fertility in island.Fertilities)
  {
   var icon=new Image{Source=FertilityIcons.Get(fertility),Width=28,Height=28,Margin=new Thickness(0,0,5,0)};fertilityRow.Children.Add(icon);
  }
  var siteRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,9,0,0)};panel.Children.Add(siteRow);
  AddSite(siteRow,"site_mountain",sites?.Mountain);
  if(region==RegionKind.Latium)AddSite(siteRow,"site_river",sites?.River);else AddSiteLabel(siteRow,Localization.Instance["SwampLabel"],sites?.Marsh);
  return new ToolTip{Content=panel,Placement=System.Windows.Controls.Primitives.PlacementMode.Mouse,Background=new SolidColorBrush(Color.FromRgb(10,23,34)),BorderBrush=new SolidColorBrush(Color.FromRgb(70,102,119)),BorderThickness=new Thickness(2),Padding=new Thickness(8),Foreground=Brushes.White};
 }

 internal void SmokeTooltips()
 {
  if(tooltipTargets.Count==0)throw new InvalidOperationException("Die Vorschau enthält keine Insel-Tooltips.");
  foreach(var pair in tooltipTargets)
  {
   pair.ToolTip.PlacementTarget=pair.Target;pair.ToolTip.IsOpen=true;UpdateLayout();pair.ToolTip.IsOpen=false;
  }
 }

 static void AddSite(Panel panel,string icon,int? count)
 {
  panel.Children.Add(new Image{Source=new BitmapImage(new Uri($"pack://application:,,,/icons/{icon}.png")),Width=22,Height=22,Margin=new Thickness(0,0,4,0)});
  panel.Children.Add(new TextBlock{Text=count?.ToString()??"–",FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,18,0)});
 }

 static void AddSiteLabel(Panel panel,string label,int? count)
 {
  panel.Children.Add(new TextBlock{Text=label,FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(203,213,225)),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)});
  panel.Children.Add(new TextBlock{Text=count?.ToString()??"–",FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,18,0)});
 }

 static Brush RoleColor(uint set)=>set switch{31312 or 8174=>new SolidColorBrush(Color.FromRgb(214,179,122)),3656 or 8179=>new SolidColorBrush(Color.FromRgb(24,178,161)),14198 or 8181=>new SolidColorBrush(Color.FromRgb(194,112,180)),144793=>Accent,_=>Brushes.White};
 static string Role(uint set)=>set switch{31312 or 8174=>"Starter",3656 or 8179=>"Secondary",14198 or 8181=>"Tertiary",144793=>"Continental",_=>set.ToString()};

 [DllImport("user32.dll")]
 static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Auto)]
 static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll")]
 static extern uint GetDpiForWindow(IntPtr window);
 [StructLayout(LayoutKind.Sequential)]
 struct MonitorInfo{public int Size;public Rect Monitor;public Rect Work;public uint Flags;}
 [StructLayout(LayoutKind.Sequential)]
 struct Rect{public int Left;public int Top;public int Right;public int Bottom;}
}
