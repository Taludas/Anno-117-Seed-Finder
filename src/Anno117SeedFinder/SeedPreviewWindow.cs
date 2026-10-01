using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

internal sealed class SeedPreviewWindow:Window
{
 double DesiredWidth=1540;
 const double DesiredHeight=1160;
 static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(25,36,52));
 static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(102,112,133));
 static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(138,91,36));
 readonly List<(FrameworkElement Target,ToolTip ToolTip)> tooltipTargets=[];
 readonly MapProfile profile;
 readonly uint seed;readonly FertilitySetting fertilitySetting;readonly SlotSetting slotSetting;
 // Zoom: the maps are scaled to fit the window (zoom 1) and can be enlarged up to MaxZoom times; Ctrl+mouse wheel zooms around the
 // cursor, dragging with the left mouse button pans.
 const double MaxZoom=12;
 readonly ScrollViewer scroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Brushes.Transparent};
 readonly ScaleTransform scale=new(1,1);
 readonly Grid maps=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top};
 readonly Slider zoomSlider=new(){Minimum=1,Maximum=MaxZoom,Value=1,Width=180,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,6,0)};
 readonly TextBlock zoomLabel=new(){Width=48,VerticalAlignment=VerticalAlignment.Center};
 readonly TextBlock legend=new(){Foreground=new SolidColorBrush(Color.FromRgb(102,112,133)),FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};
 Point? dragStart;double dragH,dragV;
 // Settling guide (SettlingGuide.cs): shown while the toolbar box is ticked; the Albion map shows one of the two population orders.
 internal static bool ShowGuide{get;set;}
 // "Cinis off": the Latium plan leaves the Cinis out (only with DLC01, where the Cinis exists).
 internal static bool GuideWithoutCinis{get;set;}
 static bool? albionRomanFirstOnMap;
 readonly StartModeKind startMode;

 // The maps start scaled to fit the window and can be zoomed.
 public SeedPreviewWindow(uint seed,MapProfile? profile=null,FertilitySetting fertilitySetting=FertilitySetting.Abundant,SlotSetting slotSetting=SlotSetting.Abundant,StartModeKind startMode=StartModeKind.Flagship)
 {
  AppCulture.Apply(this);this.profile=profile??MapProfiles.Default;this.seed=seed;this.fertilitySetting=fertilitySetting;this.slotSetting=slotSetting;
  this.startMode=startMode;
  Title=Localization.Instance.Format("PreviewTitleFormat",seed);Width=DesiredWidth;Height=DesiredHeight;MinWidth=1120;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(243,245,248));
  SourceInitialized+=(_,_)=>FitToMonitor();

  var root=new DockPanel{Margin=new Thickness(22)};Content=root;
  var header=new StackPanel{Margin=new Thickness(4,0,4,10)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  header.Children.Add(new TextBlock{Text=$"Seed {seed:N0}",FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=Ink});
  header.Children.Add(new TextBlock{Text=this.profile.Dlc01?Localization.Instance.Format("PreviewSubtitleDlcFormat",this.profile.Template,this.profile.Size):Localization.Instance.Format("PreviewSubtitleNoDlcFormat",this.profile.Template,this.profile.Size),Foreground=Muted,Margin=new Thickness(0,4,0,0)});
  // Toolbar: island style and zoom.
  var toolbar=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0)};header.Children.Add(toolbar);
  toolbar.Children.Add(new TextBlock{Text=Localization.Instance["PreviewStyleLabel"],VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)});
  var style=new ComboBox{Width=170,VerticalAlignment=VerticalAlignment.Center};style.Items.Add(Localization.Instance["PreviewStyleTiles"]);style.Items.Add(Localization.Instance["PreviewStyleArtwork"]);
  style.SelectedIndex=IslandImages.Style==IslandImageStyle.Tiles?0:1;style.SelectionChanged+=(_,_)=>{IslandImages.Style=style.SelectedIndex==0?IslandImageStyle.Tiles:IslandImageStyle.Artwork;BuildMaps();};toolbar.Children.Add(style);
  toolbar.Children.Add(new TextBlock{Text=Localization.Instance["PreviewZoomLabel"],VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(24,0,0,0)});
  var zoomOut=new Button{Content="−",Width=28,Margin=new Thickness(6,0,0,0)};zoomOut.Click+=(_,_)=>SetZoom(zoomSlider.Value/1.5,null);toolbar.Children.Add(zoomOut);
  zoomSlider.ValueChanged+=(_,_)=>ApplyZoom();toolbar.Children.Add(zoomSlider);
  var zoomIn=new Button{Content="+",Width=28};zoomIn.Click+=(_,_)=>SetZoom(zoomSlider.Value*1.5,null);toolbar.Children.Add(zoomIn);
  toolbar.Children.Add(zoomLabel);
  var fit=new Button{Content=Localization.Instance["PreviewZoomFit"],Padding=new Thickness(10,2,10,2),Margin=new Thickness(6,0,0,0)};fit.Click+=(_,_)=>SetZoom(1,null);toolbar.Children.Add(fit);
  toolbar.Children.Add(new TextBlock{Text=Localization.Instance["PreviewZoomHint"],Foreground=Muted,FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,0,0)});
  var guide=new CheckBox{Content=Localization.Instance["GuideToggle"],IsChecked=ShowGuide,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(24,0,0,0),FontWeight=FontWeights.SemiBold,ToolTip=Localization.Instance["GuideToggleTooltip"]};
  guide.Checked+=(_,_)=>{ShowGuide=true;BuildMaps();};guide.Unchecked+=(_,_)=>{ShowGuide=false;BuildMaps();};toolbar.Children.Add(guide);
  if(this.profile.Dlc01)
  {
   var withoutCinis=new CheckBox{Content=Localization.Instance["GuideWithoutCinis"],IsChecked=GuideWithoutCinis,IsEnabled=ShowGuide,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,0,0),ToolTip=Localization.Instance["GuideWithoutCinisTooltip"]};
   withoutCinis.Checked+=(_,_)=>{GuideWithoutCinis=true;BuildMaps();};withoutCinis.Unchecked+=(_,_)=>{GuideWithoutCinis=false;BuildMaps();};toolbar.Children.Add(withoutCinis);
   guide.Checked+=(_,_)=>withoutCinis.IsEnabled=true;guide.Unchecked+=(_,_)=>withoutCinis.IsEnabled=false;
  }
  header.Children.Add(legend);

  maps.LayoutTransform=scale;scroll.Content=maps;root.Children.Add(scroll);
  scroll.SizeChanged+=(_,_)=>ApplyZoom();
  scroll.PreviewMouseWheel+=(_,e)=>{if((System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)==0)return;e.Handled=true;SetZoom(zoomSlider.Value*(e.Delta>0?1.25:0.8),e.GetPosition(scroll));};
  scroll.PreviewMouseLeftButtonDown+=(_,e)=>{if(IsInScrollBar(e.OriginalSource as DependencyObject))return;dragStart=e.GetPosition(scroll);dragH=scroll.HorizontalOffset;dragV=scroll.VerticalOffset;};
  scroll.PreviewMouseMove+=(_,e)=>
  {
   if(dragStart is not Point start||e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed){dragStart=null;return;}
   var now=e.GetPosition(scroll);if(!scroll.IsMouseCaptured&&(Math.Abs(now.X-start.X)>4||Math.Abs(now.Y-start.Y)>4))scroll.CaptureMouse();
   if(scroll.IsMouseCaptured){scroll.ScrollToHorizontalOffset(dragH-(now.X-start.X));scroll.ScrollToVerticalOffset(dragV-(now.Y-start.Y));scroll.Cursor=System.Windows.Input.Cursors.SizeAll;}
  };
  scroll.PreviewMouseLeftButtonUp+=(_,_)=>{dragStart=null;if(scroll.IsMouseCaptured)scroll.ReleaseMouseCapture();scroll.Cursor=null;};
  BuildMaps();
 }

 // (Re)builds the map cards, e.g. after the island style changed. The layouts are the real placement of the map (island positions,
 // rotations, decorations and third-party islands).
 void BuildMaps()
 {
  maps.Children.Clear();maps.ColumnDefinitions.Clear();tooltipTargets.Clear();
  var latiumLayout=new MapLayout();var albionLayout=new MapLayout();
  var latium=Generator.GenerateLatium(seed,new GeneratorScratch(),profile,fertilitySetting:fertilitySetting,slotSetting:slotSetting,layout:latiumLayout);
  var albion=AlbionGenerator.Generate(seed,profile,fertilitySetting:fertilitySetting,slotSetting:slotSetting,layout:albionLayout);
  for(var column=0;column<2;column++)maps.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  GuideMarks? latiumGuide=null,albionGuide=null;
  if(ShowGuide){latiumGuide=LatiumGuide(latium.Islands,latiumLayout);albionGuide=AlbionGuide(albion,albionLayout);}
  maps.Children.Add(BuildMap(profile.Dlc01?"Latium · Prophecies of Ash":"Latium",RegionKind.Latium,latium.Islands,latiumLayout,latiumGuide));
  var albionMap=BuildMap("Albion",RegionKind.Albion,albion,albionLayout,albionGuide);Grid.SetColumn(albionMap,1);maps.Children.Add(albionMap);
  legend.Text=IslandImages.Style==IslandImageStyle.Tiles?Localization.Instance["PreviewTileLegend"]:"";
  legend.Visibility=legend.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
 }

 static bool IsInScrollBar(DependencyObject? element)
 {
  for(;element is not null;element=element is Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(element):LogicalTreeHelper.GetParent(element))
   if(element is System.Windows.Controls.Primitives.ScrollBar)return true;
  return false;
 }

 // Zoom relative to "fit the window"; with a point (in viewport coordinates) the map spot under it stays in place.
 void SetZoom(double zoom,Point? anchor)
 {
  zoom=Math.Clamp(zoom,1,MaxZoom);
  var at=anchor??new Point(scroll.ViewportWidth/2,scroll.ViewportHeight/2);
  var content=new Point((scroll.HorizontalOffset+at.X)/scale.ScaleX,(scroll.VerticalOffset+at.Y)/scale.ScaleY);
  zoomSlider.Value=zoom;ApplyZoom();scroll.UpdateLayout();
  scroll.ScrollToHorizontalOffset(content.X*scale.ScaleX-at.X);scroll.ScrollToVerticalOffset(content.Y*scale.ScaleY-at.Y);
 }

 void ApplyZoom()
 {
  if(maps.ActualWidth<=0||scroll.ActualWidth<=0){zoomLabel.Text=$"{zoomSlider.Value*100:F0} %";return;}
  var fit=Math.Min(1,Math.Min((scroll.ActualWidth-4)/maps.ActualWidth,(scroll.ActualHeight-4)/maps.ActualHeight));
  scale.ScaleX=scale.ScaleY=fit*zoomSlider.Value;zoomLabel.Text=$"{zoomSlider.Value*100:F0} %";
 }

 // Test hook: zoom in around a point given as a fraction of the viewport.
 internal void SmokeZoom(double zoom,double fx,double fy){UpdateLayout();ApplyZoom();UpdateLayout();SetZoom(zoom,new Point(scroll.ViewportWidth*fx,scroll.ViewportHeight*fy));UpdateLayout();}


 // ---- Settling guide ---------------------------------------------------------------------------------------------------------
 // What the guide adds to one map: badges per island slot, a tooltip line per island slot and the panel under the map.
 sealed record GuideMarks(Dictionary<int,List<(string Text,Brush Color)>> Badges,Dictionary<int,string> Tooltips,FrameworkElement Panel){public List<(int From,int To,Brush Color)> Routes{get;}=[];}
 static readonly Brush LatiumGuideColor=new SolidColorBrush(Color.FromRgb(194,65,12)),RomanGuideColor=new SolidColorBrush(Color.FromRgb(185,28,28)),CelticGuideColor=new SolidColorBrush(Color.FromRgb(21,128,61));

 // The raider islands, the map area and the preferred edges of a region, from its layout (Latium: north-west and south-east,
 // Albion: south-west).
 internal static GuideMap GuideMapOf(MapLayout layout,RegionKind region)
 {
  var raiders=layout.Items.Where(x=>x.Kind==MapLayout.Special&&x.Name.Contains("pirate")).Select(x=>{var asset=IslandMasks.Asset(x.Name);var min=asset.Min(x.Rotation);var size=asset.Size(x.Rotation);return(x.X+min.X,x.Y+min.Y,x.X+min.X+size.X,x.Y+min.Y+size.Y);}).Distinct().ToList();
  var area=(layout.FullX,layout.FullY,layout.FullX+layout.FullSize,layout.FullY+layout.FullSize);
  return new(raiders,area,region==RegionKind.Latium?[MapEdge.NorthWest,MapEdge.SouthEast]:[MapEdge.SouthWest]);
 }

 internal static List<GuideIsland> GuideIslands(IReadOnlyList<GeneratedIsland> islands,MapLayout layout,RegionKind region)
 {
  var items=layout.Items.Where(x=>x.Kind==MapLayout.Island).GroupBy(x=>x.Slot).ToDictionary(x=>x.Key,x=>x.First());var result=new List<GuideIsland>();
  foreach(var island in islands)
  {
   if(!items.TryGetValue(island.SlotIndex,out var item))continue;
   var asset=IslandMasks.Asset(item.Name);var min=asset.Min(item.Rotation);var size=asset.Size(item.Rotation);var sizeClass=MapSchematic.SizeClass(item.Name);
   result.Add(new(island.SlotIndex,island.SlotIndex<0?"Cinis":$"{sizeClass} #{island.SlotIndex}",sizeClass,island.Fertilities,island.Sites.Mountain,island.Sites.River,island.Sites.Marsh,
    region==RegionKind.Latium&&island.FertilitySet==31312,(item.X+min.X,item.Y+min.Y,item.X+min.X+size.X,item.Y+min.Y+size.Y)));
  }
  return result;
 }

 static string FertilityName(RegionKind region,uint guid){try{return FertilityDefinitions.Choice(region,guid).Name;}catch(InvalidOperationException){return guid.ToString();}}

 GuideMarks LatiumGuide(IReadOnlyList<GeneratedIsland> islands,MapLayout layout)
 {
  var withoutCinis=GuideWithoutCinis&&profile.Dlc01;
  var plan=SettlingGuide.Latium(GuideIslands(islands,layout,RegionKind.Latium),startMode==StartModeKind.StartIsland,GuideMapOf(layout,RegionKind.Latium),withoutCinis);
  var marks=new GuideMarks([],[],GuidePanelFrame(out var content));
  content.Children.Add(new TextBlock{Text=Localization.Instance[plan is null?"GuideNone":"GuideLatiumHint"],Foreground=Muted,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
  if(startMode==StartModeKind.StartIsland)content.Children.Add(new TextBlock{Text=Localization.Instance["GuideStartIslandHint"],Foreground=Muted,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
  if(withoutCinis)content.Children.Add(new TextBlock{Text=Localization.Instance["GuideWithoutCinisHint"],Foreground=Muted,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
  if(plan is not null)AddPlan(marks,content,plan,"",null,LatiumGuideColor,fertility=>FertilityName(RegionKind.Latium,fertility));
  return marks;
 }

 GuideMarks AlbionGuide(IReadOnlyList<GeneratedIsland> islands,MapLayout layout)
 {
  var guideIslands=GuideIslands(islands,layout,RegionKind.Albion);
  var map=GuideMapOf(layout,RegionKind.Albion);
  return PopulationMarks(SettlingGuide.Albion(guideIslands,true,map),SettlingGuide.Albion(guideIslands,false,map),"GuideAlbionHint",
   ("GuideRomanLetter","GuideRomans","GuideRomansFirstFormat",RomanGuideColor),("GuideCelticLetter","GuideCelts","GuideCeltsFirstFormat",CelticGuideColor),
   albionRomanFirstOnMap,choice=>albionRomanFirstOnMap=choice,fertility=>FertilityName(RegionKind.Albion,fertility));
 }

 // Both orders of a two-population region, each with its score; the chosen one (or the better one) is drawn on the map.
 // Population A/B: (badge letter key, name key, "A first" format key, colour).
 GuideMarks PopulationMarks(PopulationGuide? aFirst,PopulationGuide? bFirst,string hintKey,(string Letter,string Name,string FirstFormat,Brush Color) a,(string Letter,string Name,string FirstFormat,Brush Color) b,bool? chosen,Action<bool> choose,Func<uint,string> fertilityName)
 {
  var marks=new GuideMarks([],[],GuidePanelFrame(out var content));
  if(aFirst is null&&bFirst is null){content.Children.Add(new TextBlock{Text=Localization.Instance["GuideNone"],Foreground=Muted,FontSize=11});return marks;}
  content.Children.Add(new TextBlock{Text=Localization.Instance[hintKey],Foreground=Muted,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
  var onMap=chosen??(bFirst is null||aFirst is not null&&aFirst.Score>=bFirst.Score);
  var group=hintKey;
  foreach(var guide in new[]{aFirst,bFirst})
  {
   if(guide is null)continue;
   var selected=guide.PopulationAFirst==onMap;var(first,second)=guide.PopulationAFirst?(a,b):(b,a);
   var choice=new RadioButton{Content=Localization.Instance.Format(first.FirstFormat,guide.Score.ToString("N0")),IsChecked=selected,GroupName=group,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,6,0,2),ToolTip=Localization.Instance["GuideShowOnMap"]};
   var value=guide.PopulationAFirst;choice.Checked+=(_,_)=>{if(onMap==value)return;choose(value);BuildMaps();};
   content.Children.Add(choice);
   var sub=new StackPanel{Margin=new Thickness(20,0,0,0)};content.Children.Add(sub);
   AddPlan(selected?marks:null,sub,guide.First,Localization.Instance[first.Letter],Localization.Instance[first.Name],first.Color,fertilityName);
   AddPlan(selected?marks:null,sub,guide.Second,Localization.Instance[second.Letter],Localization.Instance[second.Name],second.Color,fertilityName);
  }
  return marks;
 }

 static Border GuidePanelFrame(out StackPanel content)
 {
  content=new StackPanel();
  var outer=new StackPanel();outer.Children.Add(new TextBlock{Text=Localization.Instance["GuideHeader"],FontWeight=FontWeights.SemiBold,FontSize=13,Foreground=Ink,Margin=new Thickness(0,0,0,4)});outer.Children.Add(content);
  return new Border{Child=outer,Background=new SolidColorBrush(Color.FromRgb(248,244,238)),BorderBrush=new SolidColorBrush(Color.FromRgb(230,217,199)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,10,0,0),MaxWidth=MapSchematic.Width};
 }

 // One plan as a row of steps (badge, island, icons of the fertilities it adds); with marks, also the map badges and tooltip lines.
 static void AddPlan(GuideMarks? marks,Panel panel,GuidePlan plan,string prefix,string? population,Brush color,Func<uint,string> fertilityName)
 {
  var row=new WrapPanel{Margin=new Thickness(0,2,0,2)};panel.Children.Add(row);
  if(population is not null)row.Children.Add(new TextBlock{Text=population+":",Foreground=Ink,FontSize=12,MinWidth=58,Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center});
  for(var index=0;index<plan.Steps.Count;index++)
  {
   var step=plan.Steps[index];var text=$"{prefix}{index+1}";
   var item=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,2,12,2)};item.Children.Add(Badge(text,color,11));
   item.Children.Add(new TextBlock{Text=step.Island.Label,Foreground=Ink,FontSize=12,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,4,0)});
   foreach(var fertility in step.NewFertilities)item.Children.Add(new Image{Source=FertilityIcons.Get(fertility),Width=20,Height=20,Margin=new Thickness(0,0,1,0),ToolTip=fertilityName(fertility)});
   row.Children.Add(item);
   if(marks is null)continue;
   if(!marks.Badges.TryGetValue(step.Island.Slot,out var badges))marks.Badges[step.Island.Slot]=badges=[];badges.Add((text,color));
   if(index>0)marks.Routes.Add((plan.Steps[0].Island.Slot,step.Island.Slot,color));
   var line=Localization.Instance.Format("GuideTooltipFormat",text,string.Join(", ",step.NewFertilities.Select(fertilityName)));
   marks.Tooltips[step.Island.Slot]=marks.Tooltips.TryGetValue(step.Island.Slot,out var earlier)?earlier+"\n"+line:line;
  }
 }

 static Border Badge(string text,Brush color,double fontSize)=>new()
 {
  Background=color,BorderBrush=Brushes.White,BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(10),MinWidth=fontSize+9,Padding=new Thickness(5,0,5,0),Margin=new Thickness(1,0,1,0),
  Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=4,ShadowDepth=0,Opacity=.6},
  Child=new TextBlock{Text=text,Foreground=Brushes.White,FontWeight=FontWeights.Bold,FontSize=fontSize,HorizontalAlignment=HorizontalAlignment.Center}
 };

 void FitToMonitor()
 {
  var ownHandle=new WindowInteropHelper(this).Handle;var ownerHandle=Owner is null?IntPtr.Zero:new WindowInteropHelper(Owner).Handle;var reference=ownerHandle!=IntPtr.Zero?ownerHandle:ownHandle;
  var monitor=MonitorFromWindow(reference,2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return;
  var dpi=GetDpiForWindow(reference);if(dpi==0)dpi=96;const double margin=16;var availableWidth=Math.Max(640,(info.Work.Right-info.Work.Left)*96d/dpi-margin);var availableHeight=Math.Max(480,(info.Work.Bottom-info.Work.Top)*96d/dpi-margin);
  MinWidth=Math.Min(MinWidth,availableWidth);MinHeight=Math.Min(MinHeight,availableHeight);MaxWidth=availableWidth;MaxHeight=availableHeight;Width=Math.Min(DesiredWidth,availableWidth);Height=Math.Min(DesiredHeight,availableHeight);
 }

 Border BuildMap(string title,RegionKind region,IReadOnlyList<GeneratedIsland> islands,MapLayout layout,GuideMarks? guide=null)
 {
  var card=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(10),Margin=new Thickness(5),Padding=new Thickness(12),BorderBrush=new SolidColorBrush(Color.FromRgb(228,231,236)),BorderThickness=new Thickness(1)};
  var body=new StackPanel();card.Child=body;
  body.Children.Add(new TextBlock{Text=title,FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=Ink,Margin=new Thickness(3,0,0,8)});
  var canvas=MapSchematic.CreateCanvas(region,profile,layout);body.Children.Add(canvas);
  var bySlot=islands.ToDictionary(island=>island.SlotIndex);
  // Decorations and third-party islands first (underneath), then the islands with their tooltips.
  foreach(var item in layout.Items.Where(x=>x.Kind!=MapLayout.Island))AddBackdropTile(canvas,item);
  var centres=new Dictionary<int,Point>();
  foreach(var item in layout.Items.Where(x=>x.Kind==MapLayout.Island))
  {
   if(!bySlot.TryGetValue(item.Slot,out var island))continue;
   centres[item.Slot]=AddIslandTile(canvas,item,island,region,guide?.Tooltips.GetValueOrDefault(item.Slot));
  }
  if(guide is not null){DrawBadges(canvas,guide,centres);body.Children.Add(guide.Panel);}
  return card;
 }

 // The step badges go on top of everything, just above the island's label.
 static void DrawBadges(Canvas canvas,GuideMarks guide,Dictionary<int,Point> centres)
 {
  // Supply routes from each plan's main island, dashed, under the badges.
  foreach(var route in guide.Routes)
  {
   if(!centres.TryGetValue(route.From,out var from)||!centres.TryGetValue(route.To,out var to))continue;
   canvas.Children.Add(new System.Windows.Shapes.Line{X1=from.X,Y1=from.Y,X2=to.X,Y2=to.Y,Stroke=route.Color,StrokeThickness=2.5,StrokeDashArray=[3,2],Opacity=.85,IsHitTestVisible=false,Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=3,ShadowDepth=0,Opacity=.7}});
  }
  foreach(var(slot,badges) in guide.Badges)
  {
   if(!centres.TryGetValue(slot,out var centre))continue;
   var row=new StackPanel{Orientation=Orientation.Horizontal,IsHitTestVisible=false};foreach(var badge in badges)row.Children.Add(Badge(badge.Text,badge.Color,13));
   row.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));Canvas.SetLeft(row,centre.X-row.DesiredSize.Width/2);Canvas.SetTop(row,centre.Y-row.DesiredSize.Height-11);canvas.Children.Add(row);
  }
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

 Point AddIslandTile(Canvas canvas,LayoutItem item,GeneratedIsland island,RegionKind region,string? guideLine=null)
 {
  var sizeClass=MapSchematic.SizeClass(item.Name);
  // Opaque plate under the island image: the schematic's burgundy (XL, Cinis) and near-black (large), and darks from the app's own
  // palette for the light size classes, slate blue (medium) and a lighter shade of it (small), so the greens and rocks of the images stand out.
  var(schematicFill,stroke,_)=MapSchematic.TileColors(sizeClass);
  Brush fill=sizeClass switch{"M"=>new SolidColorBrush(Color.FromRgb(40,62,92)),"S"=>new SolidColorBrush(Color.FromRgb(74,104,145)),_=>schematicFill};
  var roleColor=RoleColor(island.FertilitySet);
  var(diamond,centre)=MapSchematic.AddPlacedTile(canvas,item,fill,stroke,2);
  var position=new MapPositionDefinition(item.Slot,item.Slot<0?"Cinis":$"#{item.Slot} · {sizeClass}",sizeClass,centre.X,centre.Y,0,0);
  var tooltip=IslandTooltip(position,island,region,island.Sites,guideLine);
  diamond.ToolTip=tooltip;ToolTipService.SetInitialShowDelay(diamond,0);ToolTipService.SetBetweenShowDelay(diamond,0);ToolTipService.SetShowDuration(diamond,30000);
  tooltipTargets.Add((diamond,tooltip));
  diamond.MouseEnter+=(_,_)=>{diamond.Stroke=roleColor;diamond.StrokeThickness=4;};
  diamond.MouseLeave+=(_,_)=>{diamond.Stroke=stroke;diamond.StrokeThickness=2;};
  // Over an image the label is small and one line; on a plain plate (no image) it is the large two-line label.
  var hasImage=IslandImages.Get(item.Name) is not null;
  var caption=item.Slot<0?"CINIS":hasImage?$"{sizeClass} #{item.Slot}":$"{sizeClass}\n#{item.Slot}";
  AddLabel(canvas,centre,caption,hasImage?(sizeClass=="C"?20:sizeClass is "XL" or "L"?13:11):(sizeClass is "XL" or "C"?20:17));
  return centre;
 }

 static ToolTip IslandTooltip(MapPositionDefinition position,GeneratedIsland island,RegionKind region,SiteCounts? sites,string? guideLine=null)
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
  // Tile breakdown: buildable land (Albion: of it marsh) and harbour of the island.
  var area=IslandAreas.Get(island.Name);
  var tiles=region==RegionKind.Latium?Localization.Instance.Format("LatiumTilesFormat",area.Total.ToString("N0"),area.Harbour.ToString("N0")):Localization.Instance.Format("AlbionTilesFormat",area.Total.ToString("N0"),area.Swamp.ToString("N0"),area.Harbour.ToString("N0"));
  panel.Children.Add(new TextBlock{Text=tiles,Foreground=new SolidColorBrush(Color.FromRgb(203,213,225)),FontSize=12,Margin=new Thickness(0,8,0,0)});
  if(guideLine is not null)panel.Children.Add(new TextBlock{Text=guideLine,Foreground=new SolidColorBrush(Color.FromRgb(253,186,116)),FontSize=12,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,0),TextWrapping=TextWrapping.Wrap,MaxWidth=320});
  return new ToolTip{Content=panel,Placement=System.Windows.Controls.Primitives.PlacementMode.Mouse,Background=new SolidColorBrush(Color.FromRgb(10,23,34)),BorderBrush=new SolidColorBrush(Color.FromRgb(70,102,119)),BorderThickness=new Thickness(2),Padding=new Thickness(8),Foreground=Brushes.White};
 }

 internal void SmokeTooltips()
 {
  if(tooltipTargets.Count==0)throw new InvalidOperationException("Die Vorschau enthält keine Insel-Tooltips.");
  foreach(var pair in tooltipTargets)
  {
   pair.ToolTip.PlacementTarget=pair.Target;pair.ToolTip.IsOpen=true;UpdateLayout();pair.ToolTip.IsOpen=false;
  }
  if(Localization.Instance.MissingKeys.Count>0)throw new InvalidOperationException("Fehlende Texte: "+string.Join(", ",Localization.Instance.MissingKeys));
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
