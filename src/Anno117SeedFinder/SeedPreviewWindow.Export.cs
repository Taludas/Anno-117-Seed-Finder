using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

// Seed Atlas export (SeedRecommendation.cs): the playstyle ratings in the header and the export of annotated map images plus the
// Elementor template (English and German) for anno-companion.com.
internal sealed partial class SeedPreviewWindow
{
 SeedAnalysis? analysis;
 readonly WrapPanel playstyles=new(){Margin=new Thickness(0,8,0,0)};
 static bool German=>Localization.Instance.Language==AppLanguage.German;

 // One chip per playstyle: its rating and whether the seed meets the playstyle's minimums; the tooltip lists every rated property.
 // Shown only with the settling guide on (the Casual rating rates the guide's plans).
 void UpdatePlaystyles()
 {
  playstyles.Children.Clear();playstyles.Visibility=ShowGuide?Visibility.Visible:Visibility.Collapsed;if(analysis is null||!ShowGuide)return;
  playstyles.Children.Add(new TextBlock{Text=Localization.Instance["PlaystyleLabel"],Foreground=Muted,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
  foreach(var style in Enum.GetValues<Playstyle>())
  {
   var score=SeedRecommendation.Score(analysis,style);
   var tip=new StackPanel{MaxWidth=460};
   tip.Children.Add(new TextBlock{Text=$"{SeedRecommendation.StyleName(style,German)} · {score.Score:F0}/100",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,4)});
   foreach(var part in score.Parts.OrderByDescending(part=>part.Weight))
    tip.Children.Add(new TextBlock{Text=Localization.Instance.Format("PlaystylePartFormat",SeedRecommendation.PartName(part.Key,German),SeedRecommendation.PartValue(part,German),(part.Normalized*100).ToString("F0"),part.Weight),TextWrapping=TextWrapping.Wrap});
   tip.Children.Add(new TextBlock{Text=score.Passes?Localization.Instance["PlaystyleMinimumsMet"]:Localization.Instance.Format("PlaystyleMinimumsMissed",string.Join("; ",score.Failed)),Margin=new Thickness(0,6,0,0),FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
   var color=score.Passes?Color.FromRgb(21,128,61):Color.FromRgb(185,28,28);
   var chip=new Border{Background=new SolidColorBrush(Color.FromArgb(24,color.R,color.G,color.B)),BorderBrush=new SolidColorBrush(color),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(9,2,9,2),Margin=new Thickness(0,0,8,0),ToolTip=tip,
    Child=new TextBlock{Text=$"{SeedRecommendation.StyleName(style,German)} {score.Score:F0}{(score.Passes?" ✓":" ✗")}",Foreground=new SolidColorBrush(color),FontWeight=FontWeights.SemiBold}};
   ToolTipService.SetShowDuration(chip,60000);playstyles.Children.Add(chip);
  }
 }

 // ---- Own selection ----------------------------------------------------------------------------------------------------------
 // With "Pick islands" ticked, a click on an island adds it to the author's own selection for the export (in click order); the
 // selection then replaces the settling guide on the map and in the export. Two-population regions cycle through the populations:
 // first click population A (Romans, Kumatic), second population B (Celts, Greco-Romans), third both, fourth none.
 sealed class AtlasPick{public readonly List<int> A=[],B=[];public bool AFirst=true;public bool Any=>A.Count+B.Count>0;}
 const int PickLatium=0,PickAlbion=1,PickThird=2;
 readonly AtlasPick[] picks=[new(),new(),new()];
 bool PickMode;

 void TogglePick(int region,int slot)
 {
  var pick=picks[region];var wasEmpty=!pick.Any;
  if(region==PickLatium){if(!pick.A.Remove(slot))pick.A.Add(slot);}
  else
  {
   bool a=pick.A.Contains(slot),b=pick.B.Contains(slot);
   if(!a&&!b)pick.A.Add(slot);else if(a&&!b){pick.A.Remove(slot);pick.B.Add(slot);}else if(!a)pick.A.Add(slot);else{pick.A.Remove(slot);pick.B.Remove(slot);}
  }
  if(wasEmpty&&pick.Any)pick.AFirst=pick.A.Count>0;
  BuildMaps(false);
 }

 // Test hook: "0=21,5;1=a3,b5,ab12" picks islands by region index and slot (two-population regions: a, b or ab before the slot).
 internal void SmokePicks(string spec)
 {
  foreach(var part in spec.Split(';',StringSplitOptions.RemoveEmptyEntries))
  {
   var(region,list)=(int.Parse(part.Split('=')[0]),part.Split('=')[1]);
   foreach(var entry in list.Split(','))
   {
    var digits=entry.TrimStart('a','b');var clicks=entry[..^digits.Length] switch{"b"=>2,"ab"=>3,_=>1};
    for(var click=0;click<clicks;click++)TogglePick(region,int.Parse(digits));
   }
  }
 }

 // A plan of the picked islands in order; each step adds the fertilities (of the population, if given) no earlier island brought.
 static GuidePlan PickPlan(IEnumerable<int> slots,IReadOnlyDictionary<int,GuideIsland> islands,IReadOnlyCollection<uint>? population)
 {
  var open=population is null?islands.Values.SelectMany(island=>island.Fertilities).ToHashSet():population.ToHashSet();var steps=new List<GuideStep>();
  foreach(var slot in slots){if(!islands.TryGetValue(slot,out var island))continue;steps.Add(new(island,[..island.Fertilities.Where(open.Contains)]));open.ExceptWith(island.Fertilities);}
  return new(steps,0);
 }
 static PopulationGuide? PickGuide(AtlasPick pick,IReadOnlyList<GuideIsland>? islands,uint[] a,uint[] b)
 {
  if(!pick.Any||islands is null)return null;var bySlot=islands.ToDictionary(island=>island.Slot);var planA=PickPlan(pick.A,bySlot,a);var planB=PickPlan(pick.B,bySlot,b);
  var aFirst=pick.AFirst&&pick.A.Count>0||pick.B.Count==0;return new(aFirst,aFirst?planA:planB,aFirst?planB:planA);
 }
 void ApplyPicks()
 {
  if(analysis is null)return;
  analysis.LatiumChoice=picks[PickLatium].Any&&analysis.LatiumIslands is {} latium?PickPlan(picks[PickLatium].A,latium.ToDictionary(island=>island.Slot),null):null;
  analysis.AlbionChoice=PickGuide(picks[PickAlbion],analysis.AlbionIslands,SettlingGuide.Roman,SettlingGuide.Celtic);
 }

 // The marks of a region's map: the own selection when there is one (its panel above the guide's), else the guide (when shown).
 GuideMarks? RegionMarks(int region,Func<GuideMarks> guide)
 {
  if(!picks[region].Any||analysis is null)return ShowGuide?guide():null;
  var outer=new StackPanel();var marks=new GuideMarks([],[],outer);
  var content=new StackPanel();
  var head=new DockPanel{Margin=new Thickness(0,0,0,4)};content.Children.Add(head);
  var clear=new Button{Content=Localization.Instance["AtlasPickClear"],Padding=new Thickness(8,0,8,0),FontSize=11};DockPanel.SetDock(clear,Dock.Right);head.Children.Add(clear);
  clear.Click+=(_,_)=>{picks[region].A.Clear();picks[region].B.Clear();BuildMaps(false);};
  head.Children.Add(new TextBlock{Text=Localization.Instance["AtlasPickHeader"],FontWeight=FontWeights.SemiBold,FontSize=13,Foreground=Ink,VerticalAlignment=VerticalAlignment.Center});
  content.Children.Add(new TextBlock{Text=Localization.Instance[region==PickLatium?"AtlasPickHintLatium":"AtlasPickHintPopulations"],Foreground=Muted,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
  string Name(uint fertility)=>SeedRecommendation.FertilityName(fertility,German);
  if(region==PickLatium){if(analysis.LatiumChoice is {} plan)AddPlan(marks,content,plan,"",null,LatiumGuideColor,Name);}
  else
  {
   var chosen=region==PickAlbion?analysis.AlbionChoice:null;
   ((string Letter,string Name,Brush Color) A,(string Letter,string Name,Brush Color) B) pops=(("GuideRomanLetter","GuideRomans",RomanGuideColor),("GuideCelticLetter","GuideCelts",CelticGuideColor));
   if(chosen is not null)
    foreach(var(plan,isA) in new[]{(chosen.First,chosen.PopulationAFirst),(chosen.Second,!chosen.PopulationAFirst)})
    {
     if(plan.Steps.Count==0)continue;var pop=isA?pops.A:pops.B;
     AddPlan(marks,content,plan,Localization.Instance[pop.Letter],Localization.Instance[pop.Name],pop.Color,Name);
    }
  }
  outer.Children.Add(new Border{Child=content,Background=new SolidColorBrush(Color.FromRgb(236,246,247)),BorderBrush=new SolidColorBrush(Color.FromRgb(14,107,116)),BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(8),Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,10,0,0),MaxWidth=MapSchematic.Width});
  if(ShowGuide)outer.Children.Add(guide().Panel);
  return marks;
 }

 // ---- Export ------------------------------------------------------------------------------------------------------------------
 static readonly string SettingsPath=System.IO.Path.Combine(AppContext.BaseDirectory,"seed-atlas.txt");
 static Dictionary<string,string> LoadSettings()
 {
  try{return File.Exists(SettingsPath)?File.ReadAllLines(SettingsPath).Select(line=>line.Split('=',2)).Where(x=>x.Length==2).ToDictionary(x=>x[0],x=>x[1]):[];}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return[];}
 }
 static void SaveSettings(Dictionary<string,string> values){try{File.WriteAllLines(SettingsPath,values.Select(x=>$"{x.Key}={x.Value}"));}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}}

 void ExportAtlas()
 {
  if(analysis is null)return;
  var settings=LoadSettings();
  var best=Enum.GetValues<Playstyle>().MaxBy(style=>SeedRecommendation.Score(analysis,style).Score);
  var dialog=new Window{Title=Localization.Instance["AtlasDialogTitle"],Owner=this,SizeToContent=SizeToContent.WidthAndHeight,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background};
  AppCulture.Apply(dialog);
  var grid=new Grid{Margin=new Thickness(18),Width=560};dialog.Content=grid;
  grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  void Add(UIElement element,int row,int column,int span=1){while(grid.RowDefinitions.Count<=row)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(element,row);Grid.SetColumn(element,column);Grid.SetColumnSpan(element,span);grid.Children.Add(element);}
  TextBlock Label(string key)=>new(){Text=Localization.Instance[key],VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,8)};
  Add(new TextBlock{Text=Localization.Instance["AtlasHint"],TextWrapping=TextWrapping.Wrap,Foreground=Muted,Margin=new Thickness(0,0,0,12)},0,0,3);
  var style=new ComboBox{Margin=new Thickness(0,0,0,8)};style.Items.Add(Localization.Instance["AtlasPlaystyleNone"]);
  foreach(var value in Enum.GetValues<Playstyle>())style.Items.Add($"{SeedRecommendation.StyleName(value,German)} ({SeedRecommendation.Score(analysis,value).Score:F0})");
  style.SelectedIndex=(int)best+1;Add(Label("AtlasPlaystyle"),1,0);Add(style,1,1,2);
  var author=new TextBox{Text=settings.GetValueOrDefault("author",""),Margin=new Thickness(0,0,0,8)};Add(Label("AtlasAuthor"),2,0);Add(author,2,1,2);
  var version=new TextBox{Text=settings.GetValueOrDefault("version","v1.4"),Margin=new Thickness(0,0,0,8)};Add(Label("AtlasVersion"),3,0);Add(version,3,1,2);
  var url=new TextBox{Text=settings.GetValueOrDefault("url",$"https://anno-companion.com/117/wp-content/uploads/sites/2/{DateTime.Now:yyyy}/{DateTime.Now:MM}/"),Margin=new Thickness(0,0,0,8),ToolTip=Localization.Instance["AtlasImageUrlTooltip"]};
  Add(Label("AtlasImageUrl"),4,0);Add(url,4,1,2);
  var folder=new TextBox{Text=settings.GetValueOrDefault("folder",Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),Margin=new Thickness(0,0,6,8)};
  var browse=new Button{Content=Localization.Instance["AtlasBrowse"],Padding=new Thickness(10,2,10,2),Margin=new Thickness(0,0,0,8)};
  browse.Click+=(_,_)=>{var picker=new Microsoft.Win32.OpenFolderDialog{InitialDirectory=Directory.Exists(folder.Text)?folder.Text:null};if(picker.ShowDialog(dialog)==true)folder.Text=picker.FolderName;};
  Add(Label("AtlasFolder"),5,0);Add(folder,5,1);Add(browse,5,2);
  var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0)};Add(buttons,6,0,3);
  var ok=new Button{Content=Localization.Instance["AtlasExportButton"],Padding=new Thickness(14,4,14,4),IsDefault=true,FontWeight=FontWeights.SemiBold};ok.Click+=(_,_)=>dialog.DialogResult=true;buttons.Children.Add(ok);
  var cancel=new Button{Content=Localization.Instance["AtlasCancel"],Padding=new Thickness(14,4,14,4),Margin=new Thickness(8,0,0,0),IsCancel=true};buttons.Children.Add(cancel);
  if(dialog.ShowDialog()!=true)return;
  settings["author"]=author.Text.Trim();settings["version"]=version.Text.Trim();settings["url"]=url.Text.Trim();settings["folder"]=folder.Text.Trim();SaveSettings(settings);
  try
  {
   var written=ExportAtlas(folder.Text.Trim(),style.SelectedIndex<=0?null:(Playstyle)(style.SelectedIndex-1),author.Text.Trim(),version.Text.Trim(),url.Text.Trim());
   MessageBox.Show(this,Localization.Instance.Format("AtlasDoneFormat",folder.Text.Trim(),string.Join("\n",written.Select(System.IO.Path.GetFileName))),Localization.Instance["AtlasDialogTitle"],MessageBoxButton.OK,MessageBoxImage.Information);
  }
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
  {
   MessageBox.Show(this,ex.Message,Localization.Instance["AtlasDialogTitle"],MessageBoxButton.OK,MessageBoxImage.Error);
  }
 }

 // Writes the annotated map images (JPEG) and the English and German template into the folder; returns the files written.
 // The maps show the orders currently chosen in the guide panels (else the better order), and the descriptions follow them.
 internal List<string> ExportAtlas(string folder,Playstyle? style,string author,string version,string urlBase)
 {
  if(analysis is null)throw new InvalidOperationException("No seed loaded.");
  Directory.CreateDirectory(folder);var stem=SeedRecommendation.FileStem(analysis);var written=new List<string>();var urls=new List<string>();
  if(urlBase.Length>0&&!urlBase.EndsWith('/'))urlBase+="/";
  var previousStyle=IslandImages.Style;IslandImages.Style=IslandImageStyle.Tiles;
  try
  {
   var regions=new List<(string Name,FrameworkElement Image)>{("latium",AtlasImage(RegionKind.Latium)),("albion",AtlasImage(RegionKind.Albion))};
   foreach(var(name,image) in regions)
   {
    var file=System.IO.Path.Combine(folder,$"{stem}_{name}_annotated.jpg");SaveJpeg(image,file,1.5);written.Add(file);urls.Add(urlBase+System.IO.Path.GetFileName(file));
   }
  }
  finally{IslandImages.Style=previousStyle;}
  foreach(var de in new[]{false,true})
  {
   var file=System.IO.Path.Combine(folder,$"{stem}{(de?"_de":"_en")}.json");
   File.WriteAllText(file,SeedRecommendation.ElementorJson(analysis,de,style,author,version,urls,albionRomanFirstOnMap));
   written.Add(file);
  }
  return written;
 }

 static void SaveJpeg(FrameworkElement element,string path,double scale)
 {
  element.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));element.Arrange(new System.Windows.Rect(element.DesiredSize));element.UpdateLayout();
  var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*scale),(int)Math.Ceiling(element.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(element);
  var encoder=new JpegBitmapEncoder{QualityLevel=90};encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
 }

 // ---- Annotated map image -----------------------------------------------------------------------------------------------------
 // The images serve the English and the German page alike, so their text is English.
 static readonly System.Globalization.CultureInfo English=new("en-US");
 const double AtlasMap=960,AtlasSide=340,AtlasTop=54;
 static readonly Brush AtlasBackground=new SolidColorBrush(Color.FromRgb(10,23,34)),AtlasCard=new SolidColorBrush(Color.FromRgb(17,34,48)),AtlasBorder=new SolidColorBrush(Color.FromRgb(70,102,119)),AtlasMuted=new SolidColorBrush(Color.FromRgb(203,213,225));

 // The plans of the order shown on the map (Latium one, the other regions both populations') and their marks, without the panel.
 (GuideMarks Marks,List<GuidePlan> Plans) AtlasMarks(RegionKind? region)
 {
  var marks=new GuideMarks([],[],new Border());var sink=new StackPanel();var plans=new List<GuidePlan>();
  void Add(GuidePlan plan,string prefix,Brush color){plans.Add(plan);AddPlan(marks,sink,plan,prefix,null,color,fertility=>SeedRecommendation.FertilityName(fertility,false));}
  if(region==RegionKind.Latium){if(analysis!.ShownLatium is {} plan)Add(plan,"",LatiumGuideColor);return(marks,plans);}
  if(region==RegionKind.Albion)
  {
   if(analysis!.ShownAlbion(albionRomanFirstOnMap) is {} guide)
   {
    var(first,second)=guide.PopulationAFirst?(("R",RomanGuideColor),("C",CelticGuideColor)):(("C",CelticGuideColor),("R",RomanGuideColor));
    Add(guide.First,first.Item1,first.Item2);Add(guide.Second,second.Item1,second.Item2);
   }
   return(marks,plans);
  }
  return(marks,plans);
 }

 FrameworkElement AtlasImage(RegionKind region)
 {
  var layout=region==RegionKind.Latium?analysis!.LatiumLayout!:analysis!.AlbionLayout!;
  var canvas=MapSchematic.CreateCanvas(region,profile,layout,AtlasMap,AtlasMap,true);var(marks,plans)=AtlasMarks(region);
  var centres=DrawRegion(canvas,region,region==RegionKind.Latium?analysis.Latium.Islands:analysis.Albion,layout,marks);
  var islands=(region==RegionKind.Latium?analysis.LatiumIslands:analysis.AlbionIslands)!.ToDictionary(island=>island.Slot);
  var generated=(region==RegionKind.Latium?analysis.Latium.Islands:analysis.Albion).ToDictionary(island=>island.SlotIndex);
  var title=region==RegionKind.Latium?(profile.Dlc01?"Latium · Prophecies of Ash":"Latium"):"Albion";
  return AtlasFrame(title,canvas,marks,plans,centres,islands,slot=>
  {
   var island=islands[slot];var sites=new List<(string Icon,string Text)>{("site_mountain",island.Mountain.ToString())};
   if(region==RegionKind.Latium)sites.Add(("site_river",island.River.ToString()));else sites.Add(("",$"Marsh {island.Marsh}"));
   if(generated.TryGetValue(slot,out var source))sites.Add(("",$"{IslandAreas.Get(source.Name).Total.ToString("N0",English)} tiles"));
   return sites;
  });
 }

 // The map in the middle, a title bar, and a card per island of the plan on the left or right (the side the island lies on), each
 // joined to its island by a line: badges and label, every fertility of the island (the ones it adds for the plan in full colour)
 // and its slots.
 FrameworkElement AtlasFrame(string title,Canvas map,GuideMarks marks,List<GuidePlan> plans,Dictionary<int,Point> centres,Dictionary<int,GuideIsland> islands,Func<int,List<(string Icon,string Text)>> sites)
 {
  var width=AtlasSide*2+AtlasMap;var height=AtlasTop+AtlasMap;
  var root=new Canvas{Width=width,Height=height,Background=AtlasBackground,ClipToBounds=true};
  Canvas.SetLeft(map,AtlasSide);Canvas.SetTop(map,AtlasTop);root.Children.Add(map);
  root.Children.Add(new TextBlock{Text=title,Foreground=Brushes.White,FontFamily=new FontFamily("Georgia"),FontSize=24,FontWeight=FontWeights.Bold,Margin=new Thickness(20,12,0,0)});
  var subtitle=new TextBlock{Text=$"Seed {analysis!.Seed} · {SeedRecommendation.TemplateName(profile.Template)} · {SeedRecommendation.SizeName(profile.Size,false)} · {fertilitySetting}",Foreground=AtlasMuted,FontSize=15};
  subtitle.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));Canvas.SetLeft(subtitle,width-subtitle.DesiredSize.Width-20);Canvas.SetTop(subtitle,20);root.Children.Add(subtitle);
  var credit=new TextBlock{Text="Anno 117 Seed Finder",Foreground=AtlasMuted,FontSize=12,Opacity=.7};credit.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));Canvas.SetLeft(credit,width-credit.DesiredSize.Width-14);Canvas.SetTop(credit,height-credit.DesiredSize.Height-8);root.Children.Add(credit);
  var cards=new List<(int Slot,Border Card,Point Target,bool Left)>();
  foreach(var(slot,badges) in marks.Badges)
  {
   if(!centres.TryGetValue(slot,out var centre)||!islands.TryGetValue(slot,out var island))continue;
   var news=plans.SelectMany(plan=>plan.Steps).Where(step=>step.Island.Slot==slot).SelectMany(step=>step.NewFertilities).ToHashSet();
   var body=new StackPanel();
   var head=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,6)};foreach(var badge in badges)head.Children.Add(Badge(badge.Text,badge.Color,13));
   head.Children.Add(new TextBlock{Text=island.Label,Foreground=Brushes.White,FontFamily=new FontFamily("Georgia"),FontSize=17,FontWeight=FontWeights.Bold,Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center});
   body.Children.Add(head);
   var icons=new WrapPanel();body.Children.Add(icons);
   foreach(var fertility in island.Fertilities.OrderByDescending(news.Contains))icons.Children.Add(new Image{Source=FertilityIcons.Get(fertility),Width=30,Height=30,Margin=new Thickness(0,0,4,4),Opacity=news.Contains(fertility)?1:.38});
   var siteRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,2,0,0)};body.Children.Add(siteRow);
   foreach(var(icon,text) in sites(slot))
   {
    if(icon.Length>0)siteRow.Children.Add(new Image{Source=new BitmapImage(new Uri($"pack://application:,,,/icons/{icon}.png")),Width=20,Height=20,Margin=new Thickness(0,0,3,0)});
    siteRow.Children.Add(new TextBlock{Text=text,Foreground=AtlasMuted,FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)});
   }
   var card=new Border{Child=body,Background=AtlasCard,BorderBrush=badges[0].Color,BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(6),Padding=new Thickness(10,8,10,6),Width=AtlasSide-28,
    Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=8,ShadowDepth=0,Opacity=.6}};
   card.Measure(new Size(AtlasSide-28,double.PositiveInfinity));
   var target=new Point(centre.X+AtlasSide,centre.Y+AtlasTop);cards.Add((slot,card,target,centre.X<AtlasMap/2));
  }
  // Each column: cards in the order of their islands, as close to their island's height as the cards above and below allow.
  foreach(var left in new[]{true,false})
  {
   var column=cards.Where(card=>card.Left==left).OrderBy(card=>card.Target.Y).ToList();if(column.Count==0)continue;
   var tops=new double[column.Count];var gap=10d;
   for(var index=0;index<column.Count;index++){var h=column[index].Card.DesiredSize.Height;tops[index]=Math.Max(index==0?AtlasTop+8:tops[index-1]+column[index-1].Card.DesiredSize.Height+gap,column[index].Target.Y-h/2);}
   var bottom=height-28d;
   for(var index=column.Count-1;index>=0;index--){var h=column[index].Card.DesiredSize.Height;if(tops[index]+h>bottom)tops[index]=bottom-h;bottom=tops[index]-gap;}
   for(var index=0;index<column.Count;index++)
   {
    var(slot,card,target,_)=column[index];var x=left?14:width-AtlasSide+14;var h=card.DesiredSize.Height;
    var anchor=new Point(left?x+card.Width:x,Math.Clamp(target.Y,tops[index]+12,tops[index]+h-12));
    var color=((SolidColorBrush)card.BorderBrush).Color;
    root.Children.Add(new Line{X1=anchor.X,Y1=anchor.Y,X2=target.X,Y2=target.Y,Stroke=new SolidColorBrush(color),StrokeThickness=2,Opacity=.9,Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=3,ShadowDepth=0,Opacity=.8}});
    root.Children.Add(new Ellipse{Width=8,Height=8,Fill=new SolidColorBrush(color),Margin=new Thickness(target.X-4,target.Y-4,0,0)});
    Canvas.SetLeft(card,x);Canvas.SetTop(card,tops[index]);root.Children.Add(card);
   }
  }
  return root;
 }
}
