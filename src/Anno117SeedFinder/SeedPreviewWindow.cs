using System.Windows;
using System.Windows.Controls;
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

 public SeedPreviewWindow(uint seed,MapProfile? profile=null,FertilitySetting fertilitySetting=FertilitySetting.Abundant)
 {
  this.profile=profile??MapProfiles.Default;
  Title=Localization.Instance.Format("PreviewTitleFormat",seed);Width=DesiredWidth;Height=DesiredHeight;MinWidth=1120;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(243,245,248));
  SourceInitialized+=(_,_)=>FitToMonitor();
  var latium=Generator.GenerateLatium(seed,new GeneratorScratch(),this.profile,fertilitySetting:fertilitySetting);
  var albion=AlbionGenerator.Generate(seed,this.profile,fertilitySetting:fertilitySetting);

  var root=new DockPanel{Margin=new Thickness(22)};Content=root;
  var header=new StackPanel{Margin=new Thickness(4,0,4,14)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  header.Children.Add(new TextBlock{Text=$"Seed {seed:N0}",FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=Ink});
  header.Children.Add(new TextBlock{Text=this.profile.Dlc01?Localization.Instance.Format("PreviewSubtitleDlcFormat",this.profile.Template,this.profile.Size):Localization.Instance.Format("PreviewSubtitleNoDlcFormat",this.profile.Template,this.profile.Size),Foreground=Muted,Margin=new Thickness(0,4,0,0)});
  if(Generator.HasUnverifiedArchipelagoMediumRisk(this.profile,latium.Islands))
   header.Children.Add(new Border{Background=new SolidColorBrush(Color.FromRgb(255,244,214)),BorderBrush=new SolidColorBrush(Color.FromRgb(214,164,53)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,10,0,0),Child=new TextBlock{Text=Localization.Instance["ArchipelagoMediumWarning"],TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(133,100,17)),FontSize=13}});

  var scroll=new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(scroll);
  var maps=new Grid{MinWidth=1300};maps.ColumnDefinitions.Add(new ColumnDefinition());maps.ColumnDefinitions.Add(new ColumnDefinition());scroll.Content=maps;
  maps.Children.Add(BuildMap(this.profile.Dlc01?"Latium + Prophecies of Ash":"Latium",RegionKind.Latium,latium.Islands,island=>island.Sites));
  var albionMap=BuildMap("Albion",RegionKind.Albion,albion,island=>island.Sites);Grid.SetColumn(albionMap,1);maps.Children.Add(albionMap);
 }

 void FitToMonitor()
 {
  var ownHandle=new WindowInteropHelper(this).Handle;var ownerHandle=Owner is null?IntPtr.Zero:new WindowInteropHelper(Owner).Handle;var reference=ownerHandle!=IntPtr.Zero?ownerHandle:ownHandle;
  var monitor=MonitorFromWindow(reference,2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return;
  var dpi=GetDpiForWindow(reference);if(dpi==0)dpi=96;const double margin=16;var availableWidth=Math.Max(640,(info.Work.Right-info.Work.Left)*96d/dpi-margin);var availableHeight=Math.Max(480,(info.Work.Bottom-info.Work.Top)*96d/dpi-margin);
  MinWidth=Math.Min(MinWidth,availableWidth);MinHeight=Math.Min(MinHeight,availableHeight);MaxWidth=availableWidth;MaxHeight=availableHeight;Width=Math.Min(DesiredWidth,availableWidth);Height=Math.Min(DesiredHeight,availableHeight);
 }

 Border BuildMap(string title,RegionKind region,IReadOnlyList<GeneratedIsland> islands,Func<GeneratedIsland,SiteCounts?> sites)
 {
  var card=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(10),Margin=new Thickness(5),Padding=new Thickness(12),BorderBrush=new SolidColorBrush(Color.FromRgb(228,231,236)),BorderThickness=new Thickness(1)};
  var body=new StackPanel();card.Child=body;
  body.Children.Add(new TextBlock{Text=title,FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=Ink,Margin=new Thickness(3,0,0,8)});
   var canvas=MapSchematic.CreateCanvas(region,profile);body.Children.Add(canvas);
  var bySlot=islands.ToDictionary(island=>island.SlotIndex);
  var positions=MapLayoutPositions.ForPreview(profile,region);
  foreach(var position in positions)
  {
   if(!bySlot.TryGetValue(position.SlotIndex,out var island))continue;
   var marker=Marker(position,island,region,sites(island));
    MapSchematic.AddTile(canvas,position,marker);
  }
  return card;
 }

 FrameworkElement Marker(MapPositionDefinition position,GeneratedIsland island,RegionKind region,SiteCounts? sites)
 {
  var size=MapSchematic.MarkerSize(position.Size);
  var side=MapSchematic.TileSize(position.Size);
  var roleColor=RoleColor(island.FertilitySet);
  var tooltip=IslandTooltip(position,island,region,sites);
  var hitArea=new Grid{Width=size,Height=size,Background=Brushes.Transparent,Cursor=System.Windows.Input.Cursors.Hand,ToolTip=tooltip};
  ToolTipService.SetInitialShowDelay(hitArea,0);ToolTipService.SetBetweenShowDelay(hitArea,0);ToolTipService.SetShowDuration(hitArea,30000);
  tooltipTargets.Add((hitArea,tooltip));
  var (fill,stroke,text)=MapSchematic.TileColors(position.Size);
  var rectangle=new Border{Width=side,Height=side,Background=fill,BorderBrush=stroke,BorderThickness=new Thickness(2),RenderTransform=new RotateTransform(45),RenderTransformOrigin=new Point(.5,.5),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
  var tileText=position.SlotIndex<0?"CINIS":$"{position.Size}\n#{position.SlotIndex}";
  var label=new TextBlock{Text=tileText,TextAlignment=TextAlignment.Center,Foreground=text,FontFamily=new FontFamily("Georgia"),FontSize=position.Size is "XL" or "C"?25:position.Size=="L"?21:18,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,RenderTransform=new RotateTransform(-45),RenderTransformOrigin=new Point(.5,.5)};
  rectangle.Child=label;hitArea.Children.Add(rectangle);
  hitArea.MouseEnter+=(_,_)=>{rectangle.BorderBrush=roleColor;rectangle.BorderThickness=new Thickness(4);};
  hitArea.MouseLeave+=(_,_)=>{rectangle.BorderBrush=stroke;rectangle.BorderThickness=new Thickness(2);};
  return hitArea;
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
