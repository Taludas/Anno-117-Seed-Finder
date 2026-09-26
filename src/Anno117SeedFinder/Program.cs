using System.Numerics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
 [STAThread]
 private static int Main(string[] args)
 {
  try{return Run(args);}
  catch(Exception error)
  {
   Console.Error.WriteLine($"{error.GetType().Name}: {error.Message}");
   return 1;
  }
 }

 private static int Run(string[] args)
 {
  if(args.Contains("--self-test",StringComparer.OrdinalIgnoreCase))
   return SeedSearcher.RunSelfTest().Success?0:1;
  if(args.Length==2&&args[0].Equals("--self-test-report",StringComparison.OrdinalIgnoreCase))
  {
   var test=SeedSearcher.RunSelfTest();File.WriteAllText(args[1],$"{test.Passed}/{test.Total}; failed={string.Join(',',test.Failed)}");return test.Success?0:1;
  }
  if(args.Contains("--smoke-test",StringComparer.OrdinalIgnoreCase))
  {
   var output=Path.Combine(Path.GetTempPath(),$"anno117-seed-finder-{Guid.NewGuid():N}.txt");
   try
   {
    var request=new SearchRequest(1,1000,Math.Max(1,Environment.ProcessorCount),3,output,2206,[2205,2208,8577,32027],true);
    var result=SeedSearcher.SearchAsync(request,null,CancellationToken.None).GetAwaiter().GetResult();
    var baseOk=result.Hits.Select(x=>x.Seed).SequenceEqual([191u,223u,778u])&&File.ReadAllLines(output).SequenceEqual(["191","223","778"]);
    var limited=SeedSearcher.SearchAsync(request with{MaxSeed=1_000_000,Limit=2},null,CancellationToken.None).GetAwaiter().GetResult();var limitedOk=limited.Hits.Select(x=>x.Seed).SequenceEqual([191u,223u])&&limited.Processed<1_000_000&&limited.FoundCount==2&&File.ReadAllLines(output).SequenceEqual(["191","223"]);
    var partial=SeedSearcher.SearchAsync(request with{CinisSlot1=0,CinisPool=[32027],CinisMaxSites=false,Limit=5},null,CancellationToken.None).GetAwaiter().GetResult();var partialOk=partial.Hits.Length==5&&partial.Hits.All(hit=>hit.Fertilities.Skip(3).Contains(32027u));
    var siteFiltered=SeedSearcher.SearchAsync(request with{CinisSlot1=0,CinisPool=[],CinisMaxSites=false,Limit=10,MinLatiumGoldSites=60,MinLatiumSturgeonSites=60,MinLatiumMountainSites=140,MinLatiumRiverSites=150,MinAlbionMountainSites=105,MinLatiumBuildableTiles=570_000,MinAlbionBuildableTiles=224_000,MinAlbionSwampTiles=82_000},null,CancellationToken.None).GetAwaiter().GetResult();
    var siteFilteredOk=siteFiltered.Hits.Length==10&&siteFiltered.Hits.All(hit=>hit.Latium.GoldSites>=60&&hit.Latium.SturgeonSites>=60&&hit.Latium.Sites.Mountain>=140&&hit.Latium.Sites.River>=150&&hit.Albion.Sites.Mountain>=105&&hit.Latium.BuildableTiles>=570_000&&hit.Albion.BuildableTiles>=224_000&&hit.Albion.SwampTiles>=82_000);
    var rule=new IslandCondition(RegionKind.Latium,FertilitySetKind.Tertiary,2,[new([2,3],[2208,8577])]);
    var filtered=SeedSearcher.SearchAsync(new(1,1000,Math.Max(1,Environment.ProcessorCount),0,output,2206,[2205,2208,8577,32027],false,[rule]),null,CancellationToken.None).GetAwaiter().GetResult();
    var conditions=new IslandCondition[]{new(RegionKind.Latium,FertilitySetKind.Starter,1,[new([0],[2206])],[18,21]),new(RegionKind.Latium,FertilitySetKind.AnyCombination,1,[new([0,1],[2202,4051])]),new(RegionKind.Albion,FertilitySetKind.Secondary,1,[new([1],[2217])],[8,9]),new(RegionKind.Albion,FertilitySetKind.Tertiary,1,[new([4,5],[2219])])};
   var compiledRequest=new SearchRequest(1,500,Math.Max(1,Environment.ProcessorCount),0,output,2206,[32027],true,conditions);var compiled=CompiledSearchPlan.Create(compiledRequest);var scratch=new GeneratorScratch();
    var compiledOk=Enumerable.Range(1,500).All(seed=>{var latium=Generator.GenerateLatium((uint)seed,scratch);var albion=AlbionGenerator.Generate((uint)seed);var optimized=compiled.MatchesLatium(latium)&&compiled.MatchesAlbion(albion);var legacy=SearchProfile.Matches(Generator.Complete(latium,albion),compiledRequest.CinisSlot1,compiledRequest.CinisPool,compiledRequest.CinisMaxSites,conditions);return optimized==legacy;});
    var filteredOk=filtered.Hits.Select(x=>x.Seed).SequenceEqual([290u,693u])&&File.ReadAllLines(output).SequenceEqual(["290","693"]);
    using var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(250));var canceled=SeedSearcher.SearchAsync(new(1,1_000_000,Math.Max(1,Environment.ProcessorCount),0,output,2206,[2205,2208,8577,32027],true),null,cancel.Token).GetAwaiter().GetResult();
    var cancellationOk=canceled.Canceled&&canceled.Processed<1_000_000&&canceled.Hits.Length>0&&canceled.FoundCount==canceled.Hits.Length&&File.ReadAllLines(output).Length==canceled.Hits.Length;
    // A search over a seed list looks at exactly those seeds and keeps the ones that pass the filters.
    var tableSearch=SeedSearcher.SearchAsync(request with{SeedList=[191u,223u,500u,700u,778u]},null,CancellationToken.None).GetAwaiter().GetResult();
    var tableSearchOk=tableSearch.Hits.Select(x=>x.Seed).SequenceEqual([191u,223u,778u])&&tableSearch.Processed==5&&File.ReadAllLines(output).SequenceEqual(["191","223","778"]);
    // ...and when none of them passes, the output file (possibly the loaded list) is left as it was.
    var emptyTableSearch=SeedSearcher.SearchAsync(request with{SeedList=[500u,700u]},null,CancellationToken.None).GetAwaiter().GetResult();
    var emptyTableSearchOk=emptyTableSearch.Hits.Length==0&&File.ReadAllLines(output).SequenceEqual(["191","223","778"]);
    // Advanced fertility filters: a search over a range keeps exactly the seeds whose Latium murex and Albion sea shell harbour tiles reach the minimums.
    var advancedMinimums=new int[AdvancedFilters.Count];advancedMinimums[(int)AdvancedFilter.LatiumHarbourMurex]=50_000;advancedMinimums[(int)AdvancedFilter.AlbionHarbourSeaShells]=17_500;
    var advancedSearch=SeedSearcher.SearchAsync(new SearchRequest(1,2000,Math.Max(1,Environment.ProcessorCount),0,output,0,[],false,AdvancedMinimums:advancedMinimums),null,CancellationToken.None).GetAwaiter().GetResult();
    var advancedExpected=Enumerable.Range(1,2000).Select(seed=>SeedSearcher.Describe((uint)seed,MapProfiles.Default,FertilitySetting.Abundant)).Where(hit=>hit.Latium.HarbourMurexTiles>=50_000&&hit.Albion.HarbourSeaShellTiles>=17_500).Select(hit=>hit.Seed).ToArray();
    var advancedOk=advancedExpected.Length>0&&advancedExpected.Length<2000&&advancedSearch.Hits.Select(hit=>hit.Seed).SequenceEqual(advancedExpected);
    var vanillaProfile=MapProfiles.Get(MapTemplateKind.Archipelago,MapSizeKind.Large,false);var vanilla=SeedSearcher.SearchAsync(request with{MaxSeed=20,Limit=3,Profile=vanillaProfile},null,CancellationToken.None).GetAwaiter().GetResult();var vanillaOk=vanilla.Hits.Select(hit=>hit.Seed).SequenceEqual([1u,2u,3u])&&vanilla.Hits.All(hit=>hit.Fertilities.Length==0);
    return baseOk&&limitedOk&&partialOk&&siteFilteredOk&&filteredOk&&compiledOk&&cancellationOk&&vanillaOk&&tableSearchOk&&emptyTableSearchOk&&advancedOk?0:1;
   }
   finally{if(File.Exists(output))File.Delete(output);}
  }
  if(args.Length>=2&&args[0].Equals("--render-ui",StringComparison.OrdinalIgnoreCase))
  {
   var previewApp=new Application();
   var window=new MainWindow{WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};window.AddPreviewConditions();
   // Optional extra arguments: "en" renders in English, "advanced" switches the advanced fertility filters on, "scoring" also
   // switches seed scoring on, "tall" makes the window taller, "scroll" scrolls to the advanced filters, "bottom" scrolls to the
   // end, "longlabel" fills the progress line with a message as long as a real one on a huge range.
   if(args.Contains("en",StringComparer.OrdinalIgnoreCase))window.CmbLanguage.SelectedIndex=1;
   if(args.Contains("advanced",StringComparer.OrdinalIgnoreCase))window.SmokeShowAdvanced();
   if(args.Contains("scoring",StringComparer.OrdinalIgnoreCase)){window.ChkScoring.IsChecked=true;window.SmokeShowAdvanced();}
   if(args.Contains("longlabel",StringComparer.OrdinalIgnoreCase))window.SmokeLongProgressLabel();
   if(args.Contains("tall",StringComparer.OrdinalIgnoreCase))window.Height=1800;
   // RENDER_SIZE="width;height" renders the window at that size (e.g. a maximised window on 1080p or 1440p).
   if(Environment.GetEnvironmentVariable("RENDER_SIZE")?.Split(';') is [var rw,var rh]){window.Width=double.Parse(rw);window.Height=double.Parse(rh);}
   var scrollFilters=args.Contains("scroll",StringComparer.OrdinalIgnoreCase);
   var scrollToEnd=args.Contains("bottom",StringComparer.OrdinalIgnoreCase);
   window.Show();window.UpdateLayout();if(scrollFilters)window.SmokeScrollFilters();if(scrollToEnd)window.SmokeScrollToEnd();
   var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(args[1]))encoder.Save(stream);
   window.Close();previewApp.Shutdown();return 0;
  }
  if((args.Length==3||args.Length==6)&&args[0].Equals("--render-preview",StringComparison.OrdinalIgnoreCase)&&uint.TryParse(args[1],out var previewSeed))
  {
   var previewApp=new Application();var previewProfile=args.Length==6?MapProfiles.Get(Enum.Parse<MapTemplateKind>(args[3],true),Enum.Parse<MapSizeKind>(args[4],true),args[5]!="0"):null;if(Environment.GetEnvironmentVariable("ARTWORK")=="1")IslandImages.Style=IslandImageStyle.Artwork;var window=new SeedPreviewWindow(previewSeed,previewProfile){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
   window.Show();window.UpdateLayout();if(Environment.GetEnvironmentVariable("PREVIEW_ZOOM")?.Split(';') is [var z,var zx,var zy])window.SmokeZoom(double.Parse(z,System.Globalization.CultureInfo.InvariantCulture),double.Parse(zx,System.Globalization.CultureInfo.InvariantCulture),double.Parse(zy,System.Globalization.CultureInfo.InvariantCulture));var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(args[2]))encoder.Save(stream);
   window.Close();previewApp.Shutdown();return 0;
  }
  // Temporary visual check for the RangeGauge DataTemplate: a ToolTip's Popup renders outside the normal visual
  // tree, so a screenshot of the window itself never shows it - render the template's content directly instead.
  if(args.Length==2&&args[0].Equals("--render-gauge",StringComparison.OrdinalIgnoreCase))
  {
   var gaugeApp=new Application();var host=new MainWindow{WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};host.Show();host.UpdateLayout();
   var range=new ScoreRange(20,90,58,60);var gauge=new RangeGauge("Rohmarmor-Steinbrüche · Latium",35,range);
   var content=new System.Windows.Controls.ContentControl{Content=gauge,ContentTemplate=(DataTemplate)host.FindResource(new DataTemplateKey(typeof(RangeGauge))),Margin=new Thickness(12),Background=System.Windows.Media.Brushes.White};
   var window=new Window{Content=content,SizeToContent=SizeToContent.WidthAndHeight,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
   window.Show();window.UpdateLayout();
   var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(args[1]))encoder.Save(stream);
   window.Close();host.Close();gaugeApp.Shutdown();return 0;
  }
  if(args.Length==3&&args[0].Equals("--render-position-picker",StringComparison.OrdinalIgnoreCase))
  {
   var region=args[1].Equals("albion",StringComparison.OrdinalIgnoreCase)?RegionKind.Albion:RegionKind.Latium;
   var previewApp=new Application();var window=new PositionPickerWindow(region,FertilitySetKind.Tertiary,[],9){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
   window.Show();window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(args[2]))encoder.Save(stream);
   window.Close();previewApp.Shutdown();return 0;
  }
  if(args.Contains("--preview-smoke-test",StringComparer.OrdinalIgnoreCase))
  {
   var previewApp=new Application();var window=new SeedPreviewWindow(29572){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
   window.Show();window.UpdateLayout();window.SmokeTooltips();window.Close();previewApp.Shutdown();return 0;
  }
  if(args.Contains("--settings-smoke-test",StringComparer.OrdinalIgnoreCase))
  {
   var profile=MapProfiles.Get(MapTemplateKind.Archipelago,MapSizeKind.Small);var output=Path.Combine(Path.GetTempPath(),$"anno117-settings-{Guid.NewGuid():N}.anno117settings.json");
   try
   {
    var preset=new FinderSettingsPreset{Template=MapTemplateKind.Archipelago,Size=MapSizeKind.Small,StartMode=StartModeKind.StartIsland,Dlc01=true,FirstSeed="28000",LastSeed="30000",Threads="8",MaximumHits="25",OutputPath="C:\\Temp\\treffer.txt",PreviewSeed="29572",MinimumGoldSites="60",MinimumSturgeonSites="60",MinimumLatiumMountainSites="132",MinimumLatiumRiverSites="92",MinimumAlbionMountainSites="89",LatiumMountainSitesEnabled=true,LatiumRiverSitesEnabled=false,AlbionMountainSitesEnabled=true,MinimumLatiumAreaK=390,MinimumAlbionAreaK=164,MinimumAlbionSwampAreaK=58,LatiumAreaEnabled=true,AlbionAreaEnabled=false,AlbionSwampAreaEnabled=true,ShowAdvancedFilters=true,AdvancedMinimums=new(){["LatiumHarbourMurex"]=45_000,["AlbionMarshBeaver"]=30_000},AdvancedEnabled=new(){["LatiumHarbourMurex"]=true},EnableScoring=true,ExtendScoringOutliers=false,ScoreWeights=new(){["LatiumArea"]=8,["MarbleSites"]=5},CinisSlot1=2206,CinisFertilities=[32027],CinisMaximumSites=true,Conditions=[new(){Region=RegionKind.Latium,Set=FertilitySetKind.Secondary,Minimum=1,RequiredByGroup=[[0],[0,0],[0],[0,0]],Positions=[MapLayoutPositions.For(profile,RegionKind.Latium,FertilitySetKind.Secondary).First().SlotIndex]},new(){Region=RegionKind.Albion,Set=FertilitySetKind.AnyCombination,Minimum=1,RequiredByGroup=[[2212,2214]],Positions=[MapLayoutPositions.For(profile,RegionKind.Albion,FertilitySetKind.AnyCombination).First().SlotIndex]}]};
    FinderSettingsStorage.Save(output,preset);var loaded=FinderSettingsStorage.Load(output);
    if(loaded.Template!=preset.Template||loaded.Size!=preset.Size||loaded.StartMode!=preset.StartMode||loaded.Dlc01!=preset.Dlc01||loaded.Conditions.Count!=2||!loaded.CinisFertilities.SequenceEqual(preset.CinisFertilities)||!loaded.ShowAdvancedFilters||loaded.AdvancedMinimums["LatiumHarbourMurex"]!=45_000||loaded.AdvancedEnabled.GetValueOrDefault("AlbionMarshBeaver"))return 1;
     var settingsApp=new Application();var window=new MainWindow{WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};typeof(MainWindow).GetMethod("ApplyPreset",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[loaded]);window.Show();window.UpdateLayout();var latium=(System.Windows.Controls.StackPanel)window.FindName("LatiumConditions");var albion=(System.Windows.Controls.StackPanel)window.FindName("AlbionConditions");var gold=(System.Windows.Controls.ComboBox)window.FindName("CmbMinGoldSites");var latiumMountain=(System.Windows.Controls.ComboBox)window.FindName("CmbMinLatiumMountainSites");var latiumRiverEnabled=(System.Windows.Controls.CheckBox)window.FindName("ChkMinLatiumRiverSites");var albionMountainEnabled=(System.Windows.Controls.CheckBox)window.FindName("ChkMinAlbionMountainSites");var latiumArea=(System.Windows.Controls.Slider)window.FindName("SldMinLatiumArea");var albionAreaEnabled=(System.Windows.Controls.CheckBox)window.FindName("ChkMinAlbionArea");var swampArea=(System.Windows.Controls.Slider)window.FindName("SldMinAlbionSwampArea");var areaMedianButton=(System.Windows.Controls.Button)window.FindName("BtnLatiumAreaMedian");var siteMedianButton=(System.Windows.Controls.Button)window.FindName("BtnGoldSitesMedian");var firstSeed=(System.Windows.Controls.TextBox)window.FindName("TxtFirstSeed");var lastSeed=(System.Windows.Controls.TextBox)window.FindName("TxtMaxSeed");var minSeedButton=(System.Windows.Controls.Button)window.FindName("BtnMinSeed");var maxSeedButton=(System.Windows.Controls.Button)window.FindName("BtnMaxSeed");var presetValuesOk=gold.SelectedItem?.ToString()=="60"&&latiumArea.Value==390;latiumArea.Value=380;firstSeed.Text="123456";lastSeed.Text="123456789";var seedFormattingOk=firstSeed.Text=="123.456"&&lastSeed.Text=="123.456.789";areaMedianButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));siteMedianButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));minSeedButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));maxSeedButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));var ok=latium.Children.Count==1&&albion.Children.Count==1&&presetValuesOk&&seedFormattingOk&&window.SmokePreparedProfileControls()&&gold.SelectedItem?.ToString()=="62"&&latiumMountain.SelectedItem?.ToString()=="132"&&latiumRiverEnabled.IsChecked==false&&albionMountainEnabled.IsChecked==true&&latiumArea.Value==390&&albionAreaEnabled.IsChecked==false&&swampArea.Value==58&&firstSeed.Text=="1"&&lastSeed.Text=="999.999.999"&&window.SmokeDlcToggle()&&window.SmokeAdvancedFilters()&&window.SmokeScoring()&&window.SmokeLanguageRefresh()&&window.SmokeTableSeeds()&&window.SmokeColumnStyles()&&window.SmokeGaugeTooltips()&&window.SmokeTimestampedOutput();window.Close();settingsApp.Shutdown();return ok?0:1;
   }
   finally{if(File.Exists(output))File.Delete(output);}
  }
  if(args.Contains("--all-profile-smoke-test",StringComparer.OrdinalIgnoreCase))
  {
   foreach(var profile in MapProfiles.All.Concat(MapProfiles.All.Where(x=>x.Dlc01).Select(x=>MapProfiles.Get(x.Template,x.Size,true,true))))foreach(var seed in new uint[]{1,2500})
   {
    LatiumGeneration latium;List<GeneratedIsland> albion;try{latium=Generator.GenerateLatium(seed,new GeneratorScratch(),profile);albion=AlbionGenerator.Generate(seed,profile);}catch(Exception error){Console.Error.WriteLine($"{profile.DisplayName} seed {seed}: {error.Message}");return 4;}
    var latiumCount=profile.LatiumSlots.Count+(profile.Dlc01?1:0);
    if(latium.Islands.Count!=latiumCount||albion.Count!=profile.AlbionSlots.Count||MapLayoutPositions.ForPreview(profile,RegionKind.Latium).Count!=latiumCount||MapLayoutPositions.ForPreview(profile,RegionKind.Albion).Count!=profile.AlbionSlots.Count)return 3;
   }
   return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-profile",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var dumpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var dumpSize)&&uint.TryParse(args[3],out var dumpSeed))
  {
   var profile=MapProfiles.Get(dumpTemplate,dumpSize);var generated=Generator.GenerateLatium(dumpSeed,new GeneratorScratch(),profile);
   File.WriteAllLines(args[4],generated.Islands.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  // Statistics run (--analyze-mines): min/max/average/median of the Latium mineral, marble and gold-mine sites and of the Albion
  // copper and silver mine sites over seeds 1..N per template and size, for AggregateSiteRanges.
  if(args.Length==5&&args[0].Equals("--analyze-mines",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapSizeKind>(args[1],true,out var amSize)&&bool.TryParse(args[2],out var amDlc)&&int.TryParse(args[3],out var amSeeds))
  {
   var rows=new List<string>();
   foreach(var template in Enum.GetValues<MapTemplateKind>())
   {
    var profile=MapProfiles.Get(template,amSize,amDlc);var mineral=new int[amSeeds];var copper=new int[amSeeds];var silver=new int[amSeeds];var marble=new int[amSeeds];var goldMine=new int[amSeeds];
    Parallel.For(0,amSeeds,()=>new GeneratorScratch(),(index,_,scratch)=>
    {
     var seed=(uint)(index+1);
     var latium=Generator.GenerateLatium(seed,scratch,profile).Metrics;mineral[index]=latium.MineralMineSites;marble[index]=latium.MarbleSites;goldMine[index]=latium.GoldMineSites;
     var albion=RegionMetrics.Calculate(AlbionGenerator.Generate(seed,profile));copper[index]=albion.CopperMineSites;silver[index]=albion.SilverMineSites;
     return scratch;
    },_=>{});
    rows.Add($"{template}|{amSize}|dlc={amDlc}|Mineral={MineStats(mineral)}|Copper={MineStats(copper)}|Silver={MineStats(silver)}|Marble={MineStats(marble)}|GoldMine={MineStats(goldMine)}");
   }
   File.WriteAllLines(args[4],rows);return 0;
  }
  // Statistics run (--analyze-tin): min/max/average/median of the Albion tin-mine sites over seeds 1..N per template and size, for
  // AggregateSiteRanges.TinRanges. Albion does not depend on DLC01.
  if(args.Length==4&&args[0].Equals("--analyze-tin",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapSizeKind>(args[1],true,out var atSize)&&int.TryParse(args[2],out var atSeeds))
  {
   var rows=new List<string>();
   foreach(var template in Enum.GetValues<MapTemplateKind>())
   {
    var profile=MapProfiles.Get(template,atSize,true);var tin=new int[atSeeds];
    Parallel.For(0,atSeeds,index=>{tin[index]=RegionMetrics.Calculate(AlbionGenerator.Generate((uint)(index+1),profile)).TinMineSites;});
    rows.Add($"{template}|{atSize}|Tin={MineStats(tin)}");
   }
   File.WriteAllLines(args[3],rows);return 0;
  }
  // Statistics run (--analyze-advanced): min/max/average/median of every advanced filter value over seeds 1..N per template, for one
  // size and DLC01 state; the rows are the input of AdvancedRanges.
  if(args.Length==5&&args[0].Equals("--analyze-advanced",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapSizeKind>(args[1],true,out var aaSize)&&bool.TryParse(args[2],out var aaDlc)&&int.TryParse(args[3],out var aaSeeds))
  {
   var rows=new List<string>();
   foreach(var template in Enum.GetValues<MapTemplateKind>())
   {
    var profile=MapProfiles.Get(template,aaSize,aaDlc);var values=Enumerable.Range(0,AdvancedFilters.Count).Select(_=>new int[aaSeeds]).ToArray();
    Parallel.For(0,aaSeeds,()=>new GeneratorScratch(),(index,_,scratch)=>
    {
     var seed=(uint)(index+1);
     var latium=Generator.GenerateLatium(seed,scratch,profile).Metrics;var albion=RegionMetrics.Calculate(AlbionGenerator.Generate(seed,profile));
     foreach(var definition in AdvancedFilters.All)values[(int)definition.Filter][index]=(definition.Region==RegionKind.Latium?latium:albion).Tiles(definition.Filter);
     return scratch;
    },_=>{});
    rows.Add($"{template}|{aaSize}|dlc={aaDlc}|{string.Join('|',AdvancedFilters.All.Select(definition=>$"{definition.Filter}={MineStats(values[(int)definition.Filter])}"))}");
   }
   File.WriteAllLines(args[4],rows);return 0;
  }
  // Throughput (--benchmark <seeds> <threads>): seeds per second for the generation of each region alone, and for a real
  // search over seeds 1..N on Corners Large with DLC01 whose Latium filter lets nothing through (every seed generated in Latium,
  // the usual first region). Written to the console as one line per measurement.
  if(args.Length==3&&args[0].Equals("--benchmark",StringComparison.OrdinalIgnoreCase)&&int.TryParse(args[1],out var bmSeeds)&&int.TryParse(args[2],out var bmThreads))
  {
   var bmProfile=MapProfiles.Get(MapTemplateKind.Corners,MapSizeKind.Large,true);var options=new ParallelOptions{MaxDegreeOfParallelism=bmThreads};
   double Rate(Action<uint> work){Parallel.For(1,Math.Min(bmSeeds,2000)+1,options,i=>work((uint)i));var watch=System.Diagnostics.Stopwatch.StartNew();Parallel.For(1,bmSeeds+1,options,i=>work((uint)i));return bmSeeds/watch.Elapsed.TotalSeconds;}
   var scratch=new ThreadLocal<GeneratorScratch>(()=>new GeneratorScratch());
   Console.WriteLine($"latium {Rate(seed=>Generator.GenerateLatium(seed,scratch.Value!,bmProfile)):F0}");
   Console.WriteLine($"albion {Rate(seed=>RegionMetrics.Calculate(AlbionGenerator.Generate(seed,bmProfile))):F0}");
   var output=Path.Combine(Path.GetTempPath(),$"anno117-benchmark-{Guid.NewGuid():N}.txt");
   try
   {
    var request=new SearchRequest(1,bmSeeds,bmThreads,0,output,0,[],false,Profile:bmProfile,MinLatiumGoldSites:999);
    var watch=System.Diagnostics.Stopwatch.StartNew();var summary=SeedSearcher.SearchAsync(request,null,CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"search {bmSeeds/watch.Elapsed.TotalSeconds:F0} ({summary.Strategy})");
   }
   finally{if(File.Exists(output))File.Delete(output);}
   return 0;
  }
  // Headless counterpart of the window's "Seedliste laden" + "CSV exportieren" pair:
  // reads one seed per line and writes the same table the UI would show.
  if(args.Length==5&&args[0].Equals("--describe-seeds",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var describeTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var describeSize))
  {
   var profile=MapProfiles.Get(describeTemplate,describeSize);
   var seeds=File.ReadLines(args[3]).Select(line=>new string(line.Trim().TakeWhile(char.IsDigit).ToArray())).Where(text=>text.Length>0&&uint.TryParse(text,out var value)&&value>=SeedLimits.Minimum&&value<=SeedLimits.Maximum).Select(uint.Parse).Distinct().ToArray();
   var rows=new string[seeds.Length];Parallel.For(0,seeds.Length,index=>rows[index]=SeedSearcher.CsvRow(SeedSearcher.Describe(seeds[index],profile,FertilitySetting.Abundant)));
   File.WriteAllLines(args[4],rows.Prepend(SeedSearcher.CsvHeader),new System.Text.UTF8Encoding(true));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-profile-nodlc",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var dumpNoDlcTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var dumpNoDlcSize)&&uint.TryParse(args[3],out var dumpNoDlcSeed))
  {
   var profile=MapProfiles.Get(dumpNoDlcTemplate,dumpNoDlcSize,false);var generated=Generator.GenerateLatium(dumpNoDlcSeed,new GeneratorScratch(),profile);
   File.WriteAllLines(args[4],generated.Islands.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-third-party-raw",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var tpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var tpSize)&&uint.TryParse(args[3],out var tpSeed))
  {
   var profile=MapProfiles.Get(tpTemplate,tpSize);var buf=new uint[5];Generator.GenerateLatium(tpSeed,new GeneratorScratch(),profile,debugThirdPartyRawOut:buf);
   File.WriteAllText(args[4],string.Join(',',buf.Select(x=>x.ToString("X8"))));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-retro-placements",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var retroPlaceTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var retroPlaceSize)&&uint.TryParse(args[3],out var retroPlaceSeed))
  {
   var rows=new List<string>();Generator.GenerateLatium(retroPlaceSeed,new GeneratorScratch(),MapProfiles.Get(retroPlaceTemplate,retroPlaceSize,true,true),debugMovedPlacements:rows);File.WriteAllLines(args[4],rows);return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-retro-info",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var riTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var riSize)&&uint.TryParse(args[3],out var riFirst))
  {
   var riCount=int.TryParse(args[4],out var parsedCount)?parsedCount:1;var riRows=new List<string>();
   for(var riSeed=riFirst;riSeed<riFirst+riCount;riSeed++)riRows.Add($"{riSeed}|{string.Join('|',Generator.DebugRetroInfo(riSeed,riTemplate,riSize))}");
   File.WriteAllLines("retro_info.txt",riRows);Console.Write(string.Join("; ",riRows));return 0;
  }
  if(args.Length>=5&&args[0].Equals("--dump-profile-sites",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var siteCheckTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var siteCheckSize)&&uint.TryParse(args[3],out var siteCheckSeed))
  {
   var siteSetting=args.Length>5&&Enum.TryParse<FertilitySetting>(args[5],true,out var parsedSiteSetting)?parsedSiteSetting:FertilitySetting.Abundant;
   var siteProfile=MapProfiles.Get(siteCheckTemplate,siteCheckSize,Environment.GetEnvironmentVariable("NODLC")!="1");
   var siteSlots=Enum.TryParse<SlotSetting>(Environment.GetEnvironmentVariable("SLOTS"),true,out var parsedSlots)?parsedSlots:SlotSetting.Abundant;
   var siteGenerated=Generator.GenerateLatium(siteCheckSeed,new GeneratorScratch(),siteProfile,fertilitySetting:siteSetting,slotSetting:siteSlots);
   File.WriteAllLines(args[4],siteGenerated.Islands.OrderBy(island=>island.Name,StringComparer.Ordinal).ThenBy(island=>island.SlotIndex).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}|{island.Sites.Mountain+island.Sites.River}"));return 0;
  }
  if(args.Length>=5&&args[0].Equals("--dump-profile-retro",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var retroTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var retroSize)&&uint.TryParse(args[3],out var retroSeed))
  {
   var retroSetting=args.Length>5&&Enum.TryParse<FertilitySetting>(args[5],true,out var parsedRetroSetting)?parsedRetroSetting:FertilitySetting.Abundant;
   var generated=Generator.GenerateLatium(retroSeed,new GeneratorScratch(),MapProfiles.Get(retroTemplate,retroSize,true,true),fertilitySetting:retroSetting);
   File.WriteAllLines(args[4],generated.Islands.OrderBy(island=>island.Name,StringComparer.Ordinal).ThenBy(island=>island.SlotIndex).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}|{island.Sites.Mountain+island.Sites.River}"));return 0;
  }
  if(args.Length==6&&args[0].Equals("--dump-profile-setting",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var settingDumpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var settingDumpSize)&&uint.TryParse(args[3],out var settingDumpSeed)&&Enum.TryParse<FertilitySetting>(args[4],true,out var dumpFertilitySetting))
  {
   var profile=MapProfiles.Get(settingDumpTemplate,settingDumpSize);var generated=Generator.GenerateLatium(settingDumpSeed,new GeneratorScratch(),profile,fertilitySetting:dumpFertilitySetting);
   File.WriteAllLines(args[5],generated.Islands.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  if(args.Length==6&&args[0].Equals("--dump-profile-nodlc-setting",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var nodlcSettingDumpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var nodlcSettingDumpSize)&&uint.TryParse(args[3],out var nodlcSettingDumpSeed)&&Enum.TryParse<FertilitySetting>(args[4],true,out var nodlcDumpFertilitySetting))
  {
   var profile=MapProfiles.Get(nodlcSettingDumpTemplate,nodlcSettingDumpSize,false);var generated=Generator.GenerateLatium(nodlcSettingDumpSeed,new GeneratorScratch(),profile,fertilitySetting:nodlcDumpFertilitySetting);
   File.WriteAllLines(args[5],generated.Islands.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  if(args.Length==6&&args[0].Equals("--dump-albion-profile-setting",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionSettingDumpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionSettingDumpSize)&&uint.TryParse(args[3],out var albionSettingDumpSeed)&&Enum.TryParse<FertilitySetting>(args[4],true,out var albionDumpFertilitySetting))
  {
   var generated=AlbionGenerator.Generate(albionSettingDumpSeed,MapProfiles.Get(albionSettingDumpTemplate,albionSettingDumpSize),fertilitySetting:albionDumpFertilitySetting);
   File.WriteAllLines(args[5],generated.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  if(args.Length==6&&args[0].Equals("--dump-albion-profile-nodlc-setting",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionNodlcSettingDumpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionNodlcSettingDumpSize)&&uint.TryParse(args[3],out var albionNodlcSettingDumpSeed)&&Enum.TryParse<FertilitySetting>(args[4],true,out var albionNodlcDumpFertilitySetting))
  {
   var generated=AlbionGenerator.Generate(albionNodlcSettingDumpSeed,MapProfiles.Get(albionNodlcSettingDumpTemplate,albionNodlcSettingDumpSize,false),fertilitySetting:albionNodlcDumpFertilitySetting);
   File.WriteAllLines(args[5],generated.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-vanilla-profile",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var vanillaDumpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var vanillaDumpSize)&&uint.TryParse(args[3],out var vanillaDumpSeed))
  {
   var profile=MapProfiles.Get(vanillaDumpTemplate,vanillaDumpSize,false);var generated=Generator.GenerateLatium(vanillaDumpSeed,new GeneratorScratch(),profile);
   File.WriteAllLines(args[4],generated.Islands.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-cinis-sites",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var siteTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var siteSize)&&uint.TryParse(args[3],out var siteSeed))
  {
   var sites=Generator.GenerateLatium(siteSeed,new GeneratorScratch(),MapProfiles.Get(siteTemplate,siteSize)).CinisSites;File.WriteAllText(args[4],$"{sites.Mountain},{sites.River}");return 0;
  }
  if(args.Length==7&&args[0].Equals("--fit-latium-wiggle-permutation",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var wiggleFitTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var wiggleFitSize)&&uint.TryParse(args[3],out var wiggleFitSeed)&&int.TryParse(args[5],out var wiggleFitLast))
  {
   var expected=File.ReadAllText(args[4]).Trim().Split(',').Select(int.Parse).ToArray();var profile=MapProfiles.Get(wiggleFitTemplate,wiggleFitSize);var matches=new List<int>();for(var advance=0;advance<=wiggleFitLast;advance++)if(Generator.DebugWigglePermutation(wiggleFitSeed,profile,advance).SequenceEqual(expected))matches.Add(advance);File.WriteAllText(args[6],string.Join(',',matches));return matches.Count>0?0:2;
  }
  if(args.Length==7&&args[0].Equals("--dump-wiggle-permutations",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var permutationTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var permutationSize)&&uint.TryParse(args[3],out var permutationSeed)&&int.TryParse(args[4],out var permutationAdvance)&&int.TryParse(args[5],out var permutationCount))
  {
   File.WriteAllLines(args[6],Generator.DebugWigglePermutations(permutationSeed,MapProfiles.Get(permutationTemplate,permutationSize),permutationAdvance,permutationCount));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-albion-profile-nodlc",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionOffTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionOffSize)&&uint.TryParse(args[3],out var albionOffSeed))
  {
   var generated=AlbionGenerator.Generate(albionOffSeed,MapProfiles.Get(albionOffTemplate,albionOffSize,false));File.WriteAllLines(args[4],generated.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  if(args.Length>=5&&args[0].Equals("--dump-albion-sites",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albSiteTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albSiteSize)&&uint.TryParse(args[3],out var albSiteSeed))
  {
   var albSiteSetting=args.Length>5&&Enum.TryParse<FertilitySetting>(args[5],true,out var parsedAlbSiteSetting)?parsedAlbSiteSetting:FertilitySetting.Abundant;
   var albSiteProfile=MapProfiles.Get(albSiteTemplate,albSiteSize,Environment.GetEnvironmentVariable("NODLC")!="1");
   var albSiteSlots=Enum.TryParse<SlotSetting>(Environment.GetEnvironmentVariable("SLOTS"),true,out var parsedAlbSlots)?parsedAlbSlots:SlotSetting.Abundant;
   var albSiteGenerated=AlbionGenerator.Generate(albSiteSeed,albSiteProfile,fertilitySetting:albSiteSetting,slotSetting:albSiteSlots);
   File.WriteAllLines(args[4],albSiteGenerated.OrderBy(island=>island.Name,StringComparer.Ordinal).ThenBy(island=>island.SlotIndex).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}|{island.Sites.Mountain+island.Sites.River+island.Sites.Marsh}"));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-albion-profile",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionDumpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionDumpSize)&&uint.TryParse(args[3],out var albionDumpSeed))
  {
   var generated=AlbionGenerator.Generate(albionDumpSeed,MapProfiles.Get(albionDumpTemplate,albionDumpSize));File.WriteAllLines(args[4],generated.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}"));return 0;
  }
  if(args.Length==6&&args[0].Equals("--probe-albion-advance",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionAdvanceTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionAdvanceSize)&&uint.TryParse(args[3],out var albionAdvanceSeed))
  {
   var profile=MapProfiles.Get(albionAdvanceTemplate,albionAdvanceSize);var expected=File.ReadAllLines(args[4]).OrderBy(line=>line,StringComparer.Ordinal).ToArray();var matches=new List<int>();
   for(var draws=10_000;draws<=50_000;draws++)
   {
    var rows=AlbionGenerator.Generate(albionAdvanceSeed,profile,draws).Select(island=>$"{island.Name}|{island.FertilitySet}|{string.Join(',',island.Fertilities)}").OrderBy(line=>line,StringComparer.Ordinal);
    if(rows.SequenceEqual(expected))matches.Add(draws);
   }
   File.WriteAllText(args[5],string.Join(',',matches));return matches.Count>0?0:2;
  }
  if(args.Length==5&&args[0].Equals("--dump-albion-width",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionWidthTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionWidthSize)&&uint.TryParse(args[3],out var albionWidthSeed))
  {
   File.WriteAllText(args[4],AlbionGenerator.DebugWidth(albionWidthSeed,MapProfiles.Get(albionWidthTemplate,albionWidthSize)).ToString());return 0;
  }
  if(args.Length==3&&args[0].Equals("--fit-albion-chain-wiggle",StringComparison.OrdinalIgnoreCase))
  {
   var samples=File.ReadAllLines(args[1]).Select(line=>line.Split('|')).Select(p=>(Size:Enum.Parse<MapSizeKind>(p[0]),Seed:uint.Parse(p[1]),Width:int.Parse(p[2]))).ToArray();var scores=new List<string>();
   for(var advance=0;advance<=2500;advance++){var differences=samples.Select(sample=>Math.Abs(AlbionGenerator.DebugChainWidth(sample.Seed,MapProfiles.Get(MapTemplateKind.IslandChains,sample.Size),advance)-sample.Width)).ToArray();scores.Add($"{advance}|{differences.Count(x=>x==0)}|{differences.Sum()}");}
   File.WriteAllLines(args[2],scores.OrderByDescending(line=>int.Parse(line.Split('|')[1])).ThenBy(line=>int.Parse(line.Split('|')[2])).Take(30));return 0;
  }
  if(args.Length==3&&args[0].Equals("--dump-albion-chain-widths",StringComparison.OrdinalIgnoreCase))
  {
   var rows=File.ReadAllLines(args[1]).Select(line=>line.Split('|')).Select(parts=>{var profile=MapProfiles.Get(MapTemplateKind.IslandChains,Enum.Parse<MapSizeKind>(parts[0]));var seed=uint.Parse(parts[1]);return $"{string.Join('|',parts)}|{AlbionGenerator.DebugInitialWidth(seed,profile)}|{AlbionGenerator.DebugWidth(seed,profile)}";});File.WriteAllLines(args[2],rows);return 0;
  }
  if(args.Length==7&&args[0].Equals("--fit-albion-chain-positions",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapSizeKind>(args[1],true,out var positionFitSize)&&uint.TryParse(args[2],out var positionFitSeed)&&int.TryParse(args[4],out var positionFitFirst)&&int.TryParse(args[5],out var positionFitLast))
  {
   var expected=File.ReadAllLines(args[3]).Where(line=>line.StartsWith("celtic_island_",StringComparison.Ordinal)&&!line.Contains("3rdparty",StringComparison.Ordinal)&&!line.Contains("deco",StringComparison.Ordinal)).Select(line=>line.Split('|')).ToArray();var profile=MapProfiles.Get(MapTemplateKind.IslandChains,positionFitSize);var scores=new List<string>();
   for(var advance=positionFitFirst;advance<=positionFitLast;advance++){var generated=AlbionGenerator.DebugMovedPlacements(positionFitSeed,profile,advance).Select(line=>line.Split('|')).ToArray();var exact=0;var distance=0;for(var index=0;index<Math.Min(expected.Length,generated.Length);index++){var dx=Math.Abs(int.Parse(expected[index][2])-int.Parse(generated[index][3]));var dy=Math.Abs(int.Parse(expected[index][3])-int.Parse(generated[index][4]));if(expected[index][0]==generated[index][0]&&expected[index][1]==generated[index][1]&&dx==0&&dy==0)exact++;distance+=dx+dy;}scores.Add($"{advance}|{exact}|{distance}");}
   File.WriteAllLines(args[6],scores.OrderByDescending(line=>int.Parse(line.Split('|')[1])).ThenBy(line=>int.Parse(line.Split('|')[2])).Take(50));return 0;
  }
  if(args.Length==6&&args[0].Equals("--dump-sites",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<RegionKind>(args[1],true,out var siteRegion)&&Enum.TryParse<MapTemplateKind>(args[2],true,out var siteDumpTemplate)&&Enum.TryParse<MapSizeKind>(args[3],true,out var siteDumpSize)&&uint.TryParse(args[4],out var siteDumpSeed))
  {
   var siteProfile=MapProfiles.Get(siteDumpTemplate,siteDumpSize);var islands=siteRegion==RegionKind.Latium?Generator.GenerateLatium(siteDumpSeed,new GeneratorScratch(),siteProfile).Islands:AlbionGenerator.Generate(siteDumpSeed,siteProfile);
   File.WriteAllLines(args[5],islands.OrderBy(island=>island.Name,StringComparer.Ordinal).Select(island=>$"{island.Name}|{island.Sites.Mountain}|{island.Sites.River}|{island.Sites.Marsh}"));return 0;
  }
  if(args.Length==4&&args[0].Equals("--analyze-site-ranges",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapSizeKind>(args[1],true,out var rangeSize)&&int.TryParse(args[2],out var rangeSeeds)&&rangeSeeds>0)
  {
   File.WriteAllLines(args[3],SiteRangeAnalyzer.Analyze(rangeSize,rangeSeeds,true));return 0;
  }
  if(args.Length==4&&args[0].Equals("--analyze-vanilla-site-ranges",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapSizeKind>(args[1],true,out var vanillaRangeSize)&&int.TryParse(args[2],out var vanillaRangeSeeds)&&vanillaRangeSeeds>0)
  {
   File.WriteAllLines(args[3],SiteRangeAnalyzer.Analyze(vanillaRangeSize,vanillaRangeSeeds,false));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-placements",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var placementTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var placementSize)&&uint.TryParse(args[3],out var placementSeed))
  {
   File.WriteAllLines(args[4],Generator.DebugPlacements(placementSeed,MapProfiles.Get(placementTemplate,placementSize)));return 0;
  }
  // Every placed element of one map in the final map frame (the data behind the preview): region|kind|name|slot|rotation|x|y.
  // Env NODLC=1 for a map without DLC01, RETRO=1 for DLC01 activated later.
  if(args.Length==5&&args[0].Equals("--dump-layout",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var layoutTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var layoutSize)&&uint.TryParse(args[3],out var layoutSeed))
  {
   var layoutProfile=MapProfiles.Get(layoutTemplate,layoutSize,Environment.GetEnvironmentVariable("NODLC")!="1",Environment.GetEnvironmentVariable("RETRO")=="1");
   var latiumLayout=new MapLayout();var albionLayout=new MapLayout();
   Generator.GenerateLatium(layoutSeed,new GeneratorScratch(),layoutProfile,layout:latiumLayout);AlbionGenerator.Generate(layoutSeed,layoutProfile,layout:albionLayout);
   var lines=new List<string>{$"Latium|map|{latiumLayout.Width}|{latiumLayout.FullX}|{latiumLayout.FullY}|{latiumLayout.FullSize}|{latiumLayout.BaseX}|{latiumLayout.BaseY}|{latiumLayout.BaseSize}",$"Albion|map|{albionLayout.Width}|{albionLayout.FullX}|{albionLayout.FullY}|{albionLayout.FullSize}|{albionLayout.BaseX}|{albionLayout.BaseY}|{albionLayout.BaseSize}"};
   foreach(var item in latiumLayout.Items)lines.Add($"Latium|{item.Kind}|{item.Name}|{item.Slot}|{item.Rotation}|{item.X}|{item.Y}");
   foreach(var item in albionLayout.Items)lines.Add($"Albion|{item.Kind}|{item.Name}|{item.Slot}|{item.Rotation}|{item.X}|{item.Y}");
   File.WriteAllLines(args[4],lines);return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-moved-placements",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var movedPlacementTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var movedPlacementSize)&&uint.TryParse(args[3],out var movedPlacementSeed))
  {
   var rows=new List<string>();Generator.GenerateLatium(movedPlacementSeed,new GeneratorScratch(),MapProfiles.Get(movedPlacementTemplate,movedPlacementSize,Environment.GetEnvironmentVariable("NODLC")!="1"),debugMovedPlacements:rows);File.WriteAllLines(args[4],rows);return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-albion-placements",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionPlacementTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionPlacementSize)&&uint.TryParse(args[3],out var albionPlacementSeed))
  {
   File.WriteAllLines(args[4],AlbionGenerator.DebugPlacements(albionPlacementSeed,MapProfiles.Get(albionPlacementTemplate,albionPlacementSize)));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-albion-moved-placements",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionMovedTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionMovedSize)&&uint.TryParse(args[3],out var albionMovedSeed))
  {
   File.WriteAllLines(args[4],AlbionGenerator.DebugMovedPlacements(albionMovedSeed,MapProfiles.Get(albionMovedTemplate,albionMovedSize)));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-albion-moved-specials",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionSpecialTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionSpecialSize)&&uint.TryParse(args[3],out var albionSpecialSeed))
  {
   File.WriteAllLines(args[4],AlbionGenerator.DebugMovedSpecials(albionSpecialSeed,MapProfiles.Get(albionSpecialTemplate,albionSpecialSize)));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-albion-decorations",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionDecorationTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionDecorationSize)&&uint.TryParse(args[3],out var albionDecorationSeed))
  {
   File.WriteAllLines(args[4],AlbionGenerator.DebugDecorations(albionDecorationSeed,MapProfiles.Get(albionDecorationTemplate,albionDecorationSize)));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-albion-wiggle-permutations",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionPermutationTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionPermutationSize)&&uint.TryParse(args[3],out var albionPermutationSeed))
  {
   File.WriteAllLines(args[4],AlbionGenerator.DebugWigglePermutations(albionPermutationSeed,MapProfiles.Get(albionPermutationTemplate,albionPermutationSize)));return 0;
  }
  if(args.Length==6&&args[0].Equals("--probe-grid",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var gridTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var gridSize)&&uint.TryParse(args[3],out var gridSeed))
  {
   var profile=MapProfiles.Get(gridTemplate,gridSize);var matches=(from value in Enumerable.Range(100,413) let width=value*16 from draws in Enumerable.Range(0,31) where Generator.DebugDecorationOrder(gridSeed,profile,width,draws)==args[4] select $"{width}:{draws}").ToArray();File.WriteAllText(args[5],string.Join(',',matches));return matches.Length>0?0:2;
  }
  if(args.Length==5&&args[0].Equals("--dump-width",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var widthTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var widthSize)&&uint.TryParse(args[3],out var widthSeed))
  {
   File.WriteAllText(args[4],Generator.GenerateLatium(widthSeed,new GeneratorScratch(),MapProfiles.Get(widthTemplate,widthSize)).Width.ToString());return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-post-core",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var postTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var postSize)&&uint.TryParse(args[3],out var postSeed))
  {
   File.WriteAllText(args[4],string.Join(',',Generator.DebugPostCore(postSeed,MapProfiles.Get(postTemplate,postSize))));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-decorations",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var decorationTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var decorationSize)&&uint.TryParse(args[3],out var decorationSeed))
  {
   var trace=new List<string>();Generator.GenerateLatium(decorationSeed,new GeneratorScratch(),MapProfiles.Get(decorationTemplate,decorationSize,Environment.GetEnvironmentVariable("NODLC")!="1"),debugDecorationTrace:trace);File.WriteAllLines(args[4],trace);return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-decorations-cardinal",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var cardinalDecorationTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var cardinalDecorationSize)&&uint.TryParse(args[3],out var cardinalDecorationSeed))
  {
   var trace=new List<string>();Generator.GenerateLatium(cardinalDecorationSeed,new GeneratorScratch(),MapProfiles.Get(cardinalDecorationTemplate,cardinalDecorationSize),debugDecorationTrace:trace,debugCardinalCollision:true);File.WriteAllLines(args[4],trace);return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-vanilla-placements",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var vpTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var vpSize)&&uint.TryParse(args[3],out var vpSeed))
  {
   File.WriteAllLines(args[4],Generator.DebugPlacements(vpSeed,MapProfiles.Get(vpTemplate,vpSize,false)));return 0;
  }
  if(args.Length==5&&args[0].Equals("--dump-vanilla-decorations",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var vanillaDecorationTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var vanillaDecorationSize)&&uint.TryParse(args[3],out var vanillaDecorationSeed))
  {
   var trace=new List<string>();Generator.GenerateLatium(vanillaDecorationSeed,new GeneratorScratch(),MapProfiles.Get(vanillaDecorationTemplate,vanillaDecorationSize,false),debugDecorationTrace:trace);File.WriteAllLines(args[4],trace);return 0;
  }
  if(args.Length==5&&args[0].Equals("--match-slots",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var matchTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var matchSize))
  {
   File.WriteAllLines(args[4],Generator.DebugMatchSlots(MapProfiles.Get(matchTemplate,matchSize),File.ReadAllLines(args[3])));return 0;
  }
  if(args.Length==3&&args[0].Equals("--infer-slots",StringComparison.OrdinalIgnoreCase))
  {
   File.WriteAllLines(args[2],Generator.DebugInferSlots(File.ReadAllLines(args[1])));return 0;
  }
  if(args.Length==4&&args[0].Equals("--dump-slots",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var slotsTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var slotsSize))
  {
   File.WriteAllLines(args[3],MapProfiles.Get(slotsTemplate,slotsSize).LatiumSlots.Select(slot=>$"{slot.Index}|{slot.Type}|{slot.Size}|{slot.X+Generator.DebugSlotHalf(slot.Size)}|{slot.Y+Generator.DebugSlotHalf(slot.Size)}"));return 0;
  }
  if(args.Length==5&&args[0].Equals("--match-albion-slots",StringComparison.OrdinalIgnoreCase)&&Enum.TryParse<MapTemplateKind>(args[1],true,out var albionMatchTemplate)&&Enum.TryParse<MapSizeKind>(args[2],true,out var albionMatchSize))
  {
   File.WriteAllLines(args[4],AlbionGenerator.DebugMatchSlots(MapProfiles.Get(albionMatchTemplate,albionMatchSize),File.ReadAllLines(args[3])));return 0;
  }
  var app=new Application();
  app.Run(new MainWindow());
  return 0;
 }

 static string MineStats(int[] values)
 {
  Array.Sort(values);var middle=values.Length/2;var median=values.Length%2==0?(values[middle-1]+values[middle])/2d:values[middle];
  return string.Create(System.Globalization.CultureInfo.InvariantCulture,$"{values[0]};{values[^1]};{values.Average():F2};{median:F0}");
 }

}

internal enum FertilitySetting{Abundant,Regular,Sparse}
internal static class Generator
{
 internal const string CinisName="roman_dlc01_island_continental_01";
 internal static readonly uint[] Pool6=[2205,2202,4051,2208,8577,32027];
 static readonly Dictionary<uint,uint[]> Pools=new(){[31314]=[2206,2209],[31330]=[2210,51212],[31349]=[4049,4062],[31354]=Pool6,[31352]=[4052,4053]};
 // Latium fertility sets per resource-amount setting, taken from the FertilitySet/FertilityPool assets in the game's assets.xml.
 // Abundant is the game's High tier; its Medium and Hard tiers are the Regular and Sparse settings.
 static readonly Dictionary<uint,uint[]> Sets=new()
 {
  [31312]=[31314,4049,31354,31354,31354,31354],[3656]=[31314,31330,31330,31349,31354,31354],[14198]=[31314,31349,31354,31354,31352,31352],[144793]=[31314,31349,31349,31354,31354,31354,31354],
  [41833]=[31314,31314,4049,31354,31354],[41835]=[31314,31349,31354,31352,31352],[41834]=[31330,31330,31349,31354,31354],[145110]=[31314,31349,31349,31354,31354,31354],
  [41837]=[31314,31349,31354,31352],[41839]=[31314,31354,31354,31352],[41838]=[31349,31330,31354,31354],[145109]=[31314,4049,31354,31354,31354],
 };
 // Maps each Abundant role GUID to the FertilitySet GUID of the same role in the Regular (the game's Medium) and Sparse (Hard) tier.
 // The setting changes only the pool a role draws from, not which island gets which role. Shared by both regions because role
 // GUIDs never collide between them.
 internal static readonly Dictionary<(uint Role,FertilitySetting Setting),uint> SetVariants=new()
 {
  [(31312,FertilitySetting.Regular)]=41833,[(31312,FertilitySetting.Sparse)]=41837,
  [(3656,FertilitySetting.Regular)]=41834,[(3656,FertilitySetting.Sparse)]=41838,
  [(14198,FertilitySetting.Regular)]=41835,[(14198,FertilitySetting.Sparse)]=41839,
  [(144793,FertilitySetting.Regular)]=145110,[(144793,FertilitySetting.Sparse)]=145109,
  [(8174,FertilitySetting.Regular)]=41852,[(8174,FertilitySetting.Sparse)]=41856,
  [(8179,FertilitySetting.Regular)]=41853,[(8179,FertilitySetting.Sparse)]=41857,
  [(8181,FertilitySetting.Regular)]=41854,[(8181,FertilitySetting.Sparse)]=41858,
 };
 internal static uint ResolveSet(uint role,FertilitySetting setting,MapProfile profile)
 {
  if(setting==FertilitySetting.Abundant)return role;
  return SetVariants[(role,setting)];
 }
 static readonly Dictionary<string,uint> Names=new(StringComparer.OrdinalIgnoreCase){{"Mackerel",2206},{"Lavender",2209},{"Grapes",2205},{"Flax",2202},{"Murex",4051},{"Sea Snails",4051},{"Oysters",2208},{"Sturgeon",8577},{"Gold",32027}};
 static readonly Dictionary<uint,string> Labels=new(){{2206,"Mackerel"},{2209,"Lavender"},{2210,"Olives"},{51212,"Resin"},{4049,"Iron"},{4062,"Marble"},{2205,"Grapes"},{2202,"Flax"},{4051,"Murex"},{2208,"Oysters"},{8577,"Sturgeon"},{32027,"Gold"},{4052,"Sandarac"},{4053,"Minerals"}};
 static readonly Dictionary<string,Asset> Assets=Asset.All.ToDictionary(x=>x.Name);
 // Game version 3.0 re-shipped roman_island_extralarge_04 and moved its start coast: 37 starter placements in the 3.0 savegames
 // allow only 5.428..5.468 (the 2.1 value 5.4925 rotates it wrongly on Rift slot 23 and Archipelago Medium slot 20).
 static double StartCoastDirection(string name)=>RomanStartCoastDirections[name];
 static readonly Dictionary<string,double> RomanStartCoastDirections=new()
 {
  ["roman_island_extralarge_01"]=4.567947,["roman_island_extralarge_02"]=4.950634,["roman_island_extralarge_03"]=4.128881,["roman_island_extralarge_04"]=5.4925,
  ["roman_island_large_01"]=3.990198,["roman_island_large_02"]=1.181638,["roman_island_large_03"]=4.058963,["roman_island_large_04"]=0.06,
  ["roman_island_large_05"]=4.204327,["roman_island_large_06"]=2.861056,["roman_island_large_07"]=3.646262,["roman_island_large_09"]=3.798982
 };
 static readonly (int X,int Y)[] ArchipelagoOffsets=BuildArchipelagoOffsets();
 public static uint Name(string s)=>Names.TryGetValue(s.Trim(),out var x)?x:throw new ArgumentException($"Unbekannte Fruchtbarkeit: {s}");public static string Format(IEnumerable<uint>x)=>string.Join(" | ",x.Select(v=>Labels.GetValueOrDefault(v,v.ToString())));
 internal static string Label(uint guid)=>Labels.GetValueOrDefault(guid,guid.ToString());
 public static uint[] Cinis(uint seed)=>Generate(seed).Fertilities[CinisName];
 public static World Generate(uint seed)
 {
  var latium=GenerateLatium(seed,new GeneratorScratch(),MapProfiles.Default);
  return Complete(latium,AlbionGenerator.Generate(seed,MapProfiles.Default));
 }
 internal static LatiumGeneration GenerateLatium(uint seed,GeneratorScratch scratch,MapProfile? profile=null,int? debugWidth=null,List<string>? debugDecorationTrace=null,bool debugCardinalCollision=false,List<string>? debugMovedPlacements=null,FertilitySetting fertilitySetting=FertilitySetting.Abundant,uint[]? debugThirdPartyRawOut=null,RetroBase? retroBase=null,SlotSetting slotSetting=SlotSetting.Abundant,MapLayout? layout=null)
 {
  profile??=MapProfiles.Default;
  if(profile.Retro)return GenerateLatiumRetro(seed,scratch,profile,fertilitySetting,debugMovedPlacements,slotSetting,layout);
  var r=new Rng(seed);r.Advance(9);var original=Slots(profile);var starters=original.Where(x=>x.Type==1).ToList();r.Shuffle(starters);var slots=original.Where(x=>x.Type!=1).Concat(starters).ToList();r.Shuffle(slots);slots=slots.OrderByDescending(x=>x.Type).ToList();
  var g=new Dictionary<(int,string),List<Asset>>{{(7,"Medium"),A("roman_dlc01_island_medium_01","roman_dlc01_island_medium_02","roman_dlc01_island_medium_03")},{(7,"Small"),A("roman_dlc01_island_small_02","roman_dlc_01_island_small_01")},{(1,"XL"),A("roman_island_extralarge_01","roman_island_extralarge_02","roman_island_extralarge_03","roman_island_extralarge_04")},{(0,"Large"),A("roman_island_large_01","roman_island_large_02","roman_island_large_03","roman_island_large_04","roman_island_large_05","roman_island_large_06","roman_island_large_07","roman_island_large_09")},{(0,"Medium"),A(Enumerable.Range(1,8).Select(i=>$"roman_island_medium_{i:00}").ToArray())},{(0,"Small"),A(Enumerable.Range(1,7).Select(i=>$"roman_island_small_{i:00}").ToArray())}};
  var cycled=new HashSet<(int,string)>();var p=new List<Placed>();foreach(var s in slots){var key=PoolKey(s.Type,s.Size);var l=g[key];var independent=cycled.Contains(key);if(l.Count==0){g[key]=l=LatiumPool(key);cycled.Add(key);independent=false;}var candidates=independent?LatiumPool(key):l;var j=candidates.Count==1?0:(int)r.Scaled((uint)candidates.Count);var a=candidates[j];if(!independent)candidates.RemoveAt(j);var raw=r.Next();var rotation=s.Type!=1?(byte)(raw>>30):LatiumStarterRotation(profile,s,a,starters);p.Add(new(a,s,rotation));}var thirdPartyRaw=Enumerable.Range(0,5).Select(_=>r.Next()).ToArray();
  if(debugThirdPartyRawOut is not null)thirdPartyRaw.CopyTo(debugThirdPartyRawOut,0);
  var rawPositions=p.Select(x=>{var half=SlotHalf(x.Slot.Size);return CorePosition(x.Asset,x.Rot,x.Slot.X+half,x.Slot.Y+half);}).ToArray();
  var mx=rawPositions.Min(x=>x.X);var my=rawPositions.Min(x=>x.Y);var templateWidth=!profile.Dlc01&&profile.Template==MapTemplateKind.Archipelago?2016:profile.LatiumTemplateSize;var w=debugWidth??(templateWidth+Math.Max(Math.Max(0,-mx),Math.Max(0,-my)));
  (int X,int Y)[]? finalPositions=null;var shiftX=Math.Max(0,-mx);var shiftY=Math.Max(0,-my);(int X0,int Y0,int X1,int Y1)? playable=null;
  // Island Chains use one EnlargementOffset for both axes: it derives from the axis that enlarged the square map and is applied
  // to every element and to the playable rectangle.
  if(profile.Template==MapTemplateKind.IslandChains)shiftX=shiftY=w-templateWidth;
  if(profile.Template==MapTemplateKind.Corners)
  {
   if(!profile.Dlc01)(w,shiftX,shiftY,playable)=FitOpenMap(profile,p,rawPositions,thirdPartyRaw);finalPositions=rawPositions;
   if(debugMovedPlacements is not null)for(var index=0;index<p.Count;index++)debugMovedPlacements.Add($"{p[index].Asset.Name}|{p[index].Rot}|{p[index].Slot.Index}|{rawPositions[index].X}|{rawPositions[index].Y}");
   PlaceDecorations(r,w,shiftX,shiftY,p,thirdPartyRaw,scratch,profile,debugDecorationTrace,cardinalCollision:debugCardinalCollision,playable:playable,layout:layout);
  }
  else if(profile.Template==MapTemplateKind.Archipelago)
  {
   var positions=WiggleArchipelago(r,profile.Dlc01?w:profile.LatiumTemplateSize,p,scratch,profile,thirdPartyRaw,debugDecorationTrace);finalPositions=positions;
   if(!profile.Dlc01)(w,shiftX,shiftY,playable)=FitOpenMap(profile,p,positions,thirdPartyRaw);
   if(debugMovedPlacements is not null)for(var index=0;index<p.Count;index++)debugMovedPlacements.Add($"{p[index].Asset.Name}|{p[index].Rot}|{p[index].Slot.Index}|{positions[index].X}|{positions[index].Y}");
   PlaceDecorations(r,w,shiftX,shiftY,p,thirdPartyRaw,scratch,profile,debugDecorationTrace,positions,cardinalCollision:debugCardinalCollision,playable:playable,layout:layout);
  }
  else if(profile.Template is MapTemplateKind.Atoll or MapTemplateKind.Rift)
  {
   var positions=WiggleArchipelago(r,profile.Dlc01?w:profile.LatiumTemplateSize,p,scratch,profile,thirdPartyRaw,debugDecorationTrace);finalPositions=positions;
   if(!profile.Dlc01)(w,shiftX,shiftY,playable)=FitOpenMap(profile,p,positions,thirdPartyRaw);
   if(debugMovedPlacements is not null)for(var index=0;index<p.Count;index++)debugMovedPlacements.Add($"{p[index].Asset.Name}|{p[index].Rot}|{p[index].Slot.Index}|{positions[index].X}|{positions[index].Y}");
   PlaceDecorations(r,w,shiftX,shiftY,p,thirdPartyRaw,scratch,profile,debugDecorationTrace,positions,cardinalCollision:debugCardinalCollision,playable:playable,layout:layout);
  }
  else if(profile.Template==MapTemplateKind.IslandChains)
  {
   var moved=WiggleIslandChain(r,profile.Dlc01?w:profile.LatiumTemplateSize,p,scratch,profile,thirdPartyRaw,debugDecorationTrace);finalPositions=moved.Core;if(!profile.Dlc01)(w,shiftX,shiftY,playable)=FitOpenMap(profile,p,moved.Core,thirdPartyRaw,moved.Specials);if(debugMovedPlacements is not null)for(var index=0;index<p.Count;index++)debugMovedPlacements.Add($"{p[index].Asset.Name}|{p[index].Rot}|{p[index].Slot.Index}|{moved.Core[index].X}|{moved.Core[index].Y}");
   PlaceDecorations(r,w,shiftX,shiftY,p,thirdPartyRaw,scratch,profile,debugDecorationTrace,moved.Core,moved.Specials,debugCardinalCollision,playable,layout);
  }
  else throw new InvalidOperationException($"Keine Dekorationsphase für {profile.Template} definiert.");
  if(layout is not null){layout.Width=w;if(profile.Dlc01){layout.FullX=shiftX;layout.FullY=shiftY;layout.FullSize=profile.LatiumTemplateSize;layout.BaseX=shiftX;layout.BaseY=shiftY;layout.BaseSize=2048;}else{layout.FullX=layout.FullY=0;layout.FullSize=w;layout.BaseSize=0;}}
  if(retroBase is not null&&playable is {} retroPlayable){retroBase.Used=p.Select(x=>x.Asset.Name).ToHashSet();if(finalPositions is not null)for(var index=0;index<p.Count;index++)retroBase.Placements.Add((p[index].Asset.Name,p[index].Rot,finalPositions[index].X+shiftX,finalPositions[index].Y+shiftY));retroBase.PlayableFarX=retroPlayable.X1;retroBase.PlayableFarY=retroPlayable.Y1;}
  var sites=profile.Dlc01?SiteActivation.GenerateLatium(r,Assets[CinisName],slotSetting):default;
  var sitesBySlot=new Dictionary<int,SiteCounts>(p.Count);foreach(var placed in p)sitesBySlot[placed.Slot.Index]=SiteActivation.GenerateLatium(r,placed.Asset,slotSetting);
  var perSetting=fertilitySetting!=FertilitySetting.Abundant;
  // The game shuffles the list of set roles with the same draws in every fertility setting, but the list holds a different
  // sequence of entries per setting: Abundant [Secondary, Tertiary, Starter, Continental], Regular/Sparse [Starter, Secondary,
  // Tertiary, Continental] (the Regular/Sparse variants of those sets).
  var rules=perSetting?SettingRuleList(fertilitySetting):new List<uint>{3656,14198,31312,144793};r.Shuffle(rules);
  var records=new List<Record>();if(profile.Dlc01)records.Add(new(Assets[CinisName],0,144793,-1,"Continental"));records.AddRange(p.Select(x=>new Record(x.Asset,x.Slot.Type,0,x.Slot.Index,x.Slot.Size)));r.Shuffle(records);records=records.OrderByDescending(x=>x.Priority).ToList();var bags=new Dictionary<uint,List<uint>>();var generated=new List<GeneratedIsland>();uint[]? cinis=null;
  foreach(var rec in records){uint set,resolved;if(rec.Fixed!=0){set=rec.Fixed;resolved=ResolveSet(set,fertilitySetting,profile);}else if(perSetting){resolved=RuleVariant(rules,rec.Priority,fertilitySetting);set=RoleOfVariant[resolved];}else{set=Rule(rules,rec.Priority);resolved=set;}var assigned=Assign(rec.Asset,Sets[resolved],bags,r);if(rec.Fixed==144793)cinis=assigned;generated.Add(new(rec.Asset.Name,rec.SlotIndex,rec.Size,set,assigned,rec.SlotIndex<0?sites:sitesBySlot[rec.SlotIndex]));}
  return new LatiumGeneration(seed,w,sites,cinis??[],generated,RegionMetrics.Calculate(generated));
 }
 // ---- DLC01 switched on after the map was created ("retroactive") ----------------------------------------------------------
 // The old part of the map is the no-DLC map, unchanged. The new islands (Cinis and the six slots that exist only in the DLC
 // template) come from a second generator run over just those slots: Rng(seed), Advance(9), a shuffle of the new slots and, per
 // slot, a pick from the island pool minus the islands the no-DLC map already uses (a used-up pool is refilled, so an island can
 // appear twice) and a rotation. The new islands are not wiggled; they sit at their slot position plus the EnlargementOffset.
 // Between the picks and the site phase the game consumes RetroGap draws.
 internal sealed class RetroBase{public List<(string Name,byte Rot,int X,int Y)> Placements=new();public HashSet<string> Used=new();public int PlayableFarX;public int PlayableFarY;}
 static int Ceil8(int value)=>((value+7)>>3)<<3;
 // EnlargementOffset per axis: the far edge of the old playable area against the far edge (2020) of the no-DLC template.
 internal static (int X,int Y) RetroEnlargement(int playableFarX,int playableFarY)=>(Ceil8(playableFarX-2020),Ceil8(playableFarY-2020));
 // Draws between the slot picks and the site phase: 2 * w * (n - w) with w = n / 6 rounded (two strips of a sixth of the enlarged
 // map). n follows the larger enlargement offset as 168 + offset / 16, except for the offsets listed in RetroGridByOffset, whose
 // values were measured. For any other offset the formula may be off by one or two rows, which changes the fertility of the new
 // islands.
 static readonly Dictionary<int,int> RetroGridByOffset=new(){{-24,167},{8,168},{16,169},{32,170},{40,170},{64,172},{80,173},{144,176},{152,178},{176,179}};
 internal static bool RetroOffsetMeasured(int offsetX,int offsetY)=>RetroGridByOffset.ContainsKey(Math.Max(offsetX,offsetY));
 internal static int RetroGap(int offsetX,int offsetY)
 {
  var largest=Math.Max(offsetX,offsetY);var n=RetroGridByOffset.TryGetValue(largest,out var measured)?measured:168+largest/16;var w=(int)(n/6.0+0.5);return 2*w*(n-w);
 }
 internal static string[] DebugRetroInfo(uint seed,MapTemplateKind template,MapSizeKind size)
 {
  var off=MapProfiles.Get(template,size,false);var info=new RetroBase();
  GenerateLatium(seed,new GeneratorScratch(),off,retroBase:info);
  var e=RetroEnlargement(info.PlayableFarX,info.PlayableFarY);
  return [$"{e.X}",$"{e.Y}",$"{Math.Max(e.X,e.Y)}",$"{RetroOffsetMeasured(e.X,e.Y)}"];
 }
 static LatiumGeneration GenerateLatiumRetro(uint seed,GeneratorScratch scratch,MapProfile profile,FertilitySetting fertilitySetting,List<string>? debugPlacements=null,SlotSetting slotSetting=SlotSetting.Abundant,MapLayout? layout=null)
 {
  var off=MapProfiles.Get(profile.Template,profile.Size,false);var baseInfo=new RetroBase();var baseLayout=layout is null?null:new MapLayout();
  var old=GenerateLatium(seed,scratch,off,fertilitySetting:fertilitySetting,retroBase:baseInfo,slotSetting:slotSetting,layout:baseLayout);
  var extraSlots=profile.LatiumSlots.Skip(off.LatiumSlots.Count).Select(x=>new Slot(x.Index,x.X,x.Y,x.Size,x.Type)).ToList();
  var r=new Rng(seed);r.Advance(9);r.Shuffle(extraSlots);var slots=extraSlots.OrderByDescending(x=>x.Type).ToList();
  var pools=new Dictionary<(int,string),List<Asset>>();var p=new List<Placed>();
  foreach(var s in slots)
  {
   var key=PoolKey(s.Type,s.Size);if(!pools.TryGetValue(key,out var pool)){pool=LatiumPool(key);pool.RemoveAll(a=>baseInfo.Used.Contains(a.Name));pools[key]=pool;}
   Asset asset;
   if(pool.Count==0){var full=LatiumPool(key);asset=full[(int)r.Scaled((uint)full.Count)];}
   else{var j=pool.Count==1?0:(int)r.Scaled((uint)pool.Count);asset=pool[j];pool.RemoveAt(j);}
   var raw=r.Next();p.Add(new(asset,s,(byte)(raw>>30)));
  }
  var offset=RetroEnlargement(baseInfo.PlayableFarX,baseInfo.PlayableFarY);
  if(debugPlacements is not null){foreach(var b in baseInfo.Placements)debugPlacements.Add($"{b.Name}|{b.Rot}|{b.X}|{b.Y}");debugPlacements.Add($"{CinisName}|0|{1920+offset.X}|{1920+offset.Y}");foreach(var q in p){var half=SlotHalf(q.Slot.Size);var pos=CorePosition(q.Asset,q.Rot,q.Slot.X+half,q.Slot.Y+half);debugPlacements.Add($"{q.Asset.Name}|{q.Rot}|{pos.X+offset.X}|{pos.Y+offset.Y}");}}r.Advance(RetroGap(offset.X,offset.Y));
  var sites=SiteActivation.GenerateLatium(r,Assets[CinisName],slotSetting);
  var sitesBySlot=new Dictionary<int,SiteCounts>(p.Count);foreach(var placed in p)sitesBySlot[placed.Slot.Index]=SiteActivation.GenerateLatium(r,placed.Asset,slotSetting);
  var perSetting=fertilitySetting!=FertilitySetting.Abundant;
  var rules=perSetting?SettingRuleList(fertilitySetting):new List<uint>{3656,14198,31312,144793};r.Shuffle(rules);
  var records=new List<Record>{new(Assets[CinisName],0,144793,-1,"Continental")};records.AddRange(p.Select(x=>new Record(x.Asset,x.Slot.Type,0,x.Slot.Index,x.Slot.Size)));r.Shuffle(records);records=records.OrderByDescending(x=>x.Priority).ToList();
  var bags=new Dictionary<uint,List<uint>>();var generated=new List<GeneratedIsland>(old.Islands);uint[]? cinis=null;
  foreach(var rec in records)
  {
   uint set,resolved;if(rec.Fixed!=0){set=rec.Fixed;resolved=ResolveSet(set,fertilitySetting,profile);}else if(perSetting){resolved=RuleVariant(rules,rec.Priority,fertilitySetting);set=RoleOfVariant[resolved];}else{set=Rule(rules,rec.Priority);resolved=set;}
   var assigned=Assign(rec.Asset,Sets[resolved],bags,r);if(rec.Fixed==144793)cinis=assigned;
   generated.Add(new(rec.Asset.Name,rec.SlotIndex,rec.Size,set,assigned,rec.SlotIndex<0?sites:sitesBySlot[rec.SlotIndex]));
  }
  var size=2688+Math.Max(offset.X,offset.Y);
  if(layout is not null&&baseLayout is not null)
  {
   // The old map keeps its frame; Cinis and the new islands sit at their slot position plus the enlargement offset (not wiggled).
   layout.Items.AddRange(baseLayout.Items);layout.Add(CinisName,MapLayout.Island,-1,0,1920+offset.X,1920+offset.Y);
   foreach(var q in p){var half=SlotHalf(q.Slot.Size);var pos=CorePosition(q.Asset,q.Rot,q.Slot.X+half,q.Slot.Y+half);layout.Add(q.Asset.Name,MapLayout.Island,q.Slot.Index,q.Rot,pos.X+offset.X,pos.Y+offset.Y);}
   layout.Width=size;layout.FullX=layout.FullY=0;layout.FullSize=size;layout.BaseX=layout.BaseY=0;layout.BaseSize=baseLayout.Width;
  }
  return new LatiumGeneration(seed,size,sites,cinis??[],generated,RegionMetrics.Calculate(generated));
 }
 internal static string[] DebugPlacements(uint seed,MapProfile profile)
 {
  var r=new Rng(seed);r.Advance(9);var original=Slots(profile);var starters=original.Where(x=>x.Type==1).ToList();r.Shuffle(starters);var slots=original.Where(x=>x.Type!=1).Concat(starters).ToList();r.Shuffle(slots);slots=slots.OrderByDescending(x=>x.Type).ToList();
  var groups=new Dictionary<(int,string),List<Asset>>{{(7,"Medium"),A("roman_dlc01_island_medium_01","roman_dlc01_island_medium_02","roman_dlc01_island_medium_03")},{(7,"Small"),A("roman_dlc01_island_small_02","roman_dlc_01_island_small_01")},{(1,"XL"),A("roman_island_extralarge_01","roman_island_extralarge_02","roman_island_extralarge_03","roman_island_extralarge_04")},{(0,"Large"),A("roman_island_large_01","roman_island_large_02","roman_island_large_03","roman_island_large_04","roman_island_large_05","roman_island_large_06","roman_island_large_07","roman_island_large_09")},{(0,"Medium"),A(Enumerable.Range(1,8).Select(i=>$"roman_island_medium_{i:00}").ToArray())},{(0,"Small"),A(Enumerable.Range(1,7).Select(i=>$"roman_island_small_{i:00}").ToArray())}};
  var cycled=new HashSet<(int,string)>();var result=new List<string>();foreach(var slot in slots){var key=PoolKey(slot.Type,slot.Size);var candidates=groups[key];var refilled=candidates.Count==0;var independent=cycled.Contains(key);if(refilled){groups[key]=candidates=LatiumPool(key);cycled.Add(key);independent=false;}if(independent)candidates=LatiumPool(key);uint choiceRaw=0;var choice=0;if(candidates.Count>1){choiceRaw=r.Next();choice=(int)(((ulong)choiceRaw*(uint)candidates.Count)>>32);}var asset=candidates[choice];if(!independent)candidates.RemoveAt(choice);var raw=r.Next();var rawRotation=(byte)(raw>>30);var rotation=slot.Type!=1?rawRotation:LatiumStarterRotation(profile,slot,asset,starters);var position=CorePosition(asset,rotation,slot.X+SlotHalf(slot.Size),slot.Y+SlotHalf(slot.Size));result.Add($"{asset.Name}|{rotation}|{slot.Index}|{slot.X}|{slot.Y}|{rawRotation}|{position.X}|{position.Y}|{raw}|choice={choice}|choiceRaw={choiceRaw}|refill={refilled}|independent={independent}|type={slot.Type}");}return[..result];
 }
 internal static string DebugDecorationOrder(uint seed,MapProfile profile,int width,int thirdPartyDraws)
 {
  var r=new Rng(seed);r.Advance(9);var original=Slots(profile);var starters=original.Where(x=>x.Type==1).ToList();r.Shuffle(starters);var slots=original.Where(x=>x.Type!=1).Concat(starters).ToList();r.Shuffle(slots);slots=slots.OrderByDescending(x=>x.Type).ToList();
  var capacities=new Dictionary<(int,string),int>{{(7,"Medium"),3},{(7,"Small"),2},{(1,"XL"),4},{(0,"Large"),8},{(0,"Medium"),8},{(0,"Small"),7}};var counts=new Dictionary<(int,string),int>(capacities);
  foreach(var slot in slots){var key=PoolKey(slot.Type,slot.Size);var count=counts[key];if(count==0)counts[key]=count=capacities[key];if(count>1)r.Scaled((uint)count);counts[key]=count-1;r.Next();}
  r.Advance(thirdPartyDraws);var grid=width/16;r.ShuffleCount(grid*grid);var decorations=Enumerable.Range(1,6).ToList();r.Shuffle(decorations);return string.Join(',',decorations);
 }
 internal static uint[] DebugPostCore(uint seed,MapProfile profile)
 {
  var r=DebugPostCoreRng(seed,profile);return Enumerable.Range(0,16).Select(_=>r.Next()).ToArray();
 }
 internal static int[] DebugWigglePermutation(uint seed,MapProfile profile,int advance)
 {
  var r=DebugPostCoreRng(seed,profile);r.Advance(advance);var result=Enumerable.Range(0,80).ToArray();r.Shuffle(result);return result;
 }
 internal static string[] DebugWigglePermutations(uint seed,MapProfile profile,int advance,int count)
 {
  var r=DebugPostCoreRng(seed,profile);r.Advance(advance);var permutation=Enumerable.Range(0,80).ToArray();var result=new string[count];for(var index=0;index<count;index++){r.Shuffle(permutation);result[index]=string.Join(',',permutation);}return result;
 }
 static Rng DebugPostCoreRng(uint seed,MapProfile profile)
 {
  var r=new Rng(seed);r.Advance(9);var original=Slots(profile);var starters=original.Where(x=>x.Type==1).ToList();r.Shuffle(starters);var slots=original.Where(x=>x.Type!=1).Concat(starters).ToList();r.Shuffle(slots);slots=slots.OrderByDescending(x=>x.Type).ToList();
  var capacities=new Dictionary<(int,string),int>{{(7,"Medium"),3},{(7,"Small"),2},{(1,"XL"),4},{(0,"Large"),8},{(0,"Medium"),8},{(0,"Small"),7}};var counts=new Dictionary<(int,string),int>(capacities);
  foreach(var slot in slots){var key=PoolKey(slot.Type,slot.Size);var count=counts[key];if(count==0)counts[key]=count=capacities[key];if(count>1)r.Scaled((uint)count);counts[key]=count-1;r.Next();}
  return r;
 }
 internal static string[] DebugMatchSlots(MapProfile profile,IEnumerable<string> rows)
 {
  return rows.Where(row=>Assets.ContainsKey(row.Split('|')[0])).Select(row=>
  {
   var parts=row.Split('|');var name=parts[0];var rotation=byte.Parse(parts[1]);var actualX=int.Parse(parts[2]);var actualY=int.Parse(parts[3]);var asset=Assets[name];
   var type=name.Contains("extralarge",StringComparison.Ordinal)?1:name.StartsWith("roman_dlc",StringComparison.Ordinal)?7:0;
   var size=name.Contains("extralarge",StringComparison.Ordinal)?"XL":name.Contains("_large_",StringComparison.Ordinal)?"Large":name.Contains("_medium_",StringComparison.Ordinal)?"Medium":"Small";
   var candidates=profile.LatiumSlots.Where(slot=>slot.Type==type&&slot.Size==size).Select(slot=>{var position=CorePosition(asset,rotation,slot.X+SlotHalf(slot.Size),slot.Y+SlotHalf(slot.Size));return(Slot:slot,Distance:Math.Abs(position.X-actualX)+Math.Abs(position.Y-actualY),Position:position);}).ToArray();
   if(candidates.Length==0)return $"-1|-1|{name}|{rotation}|{actualX}|{actualY}|unmatched|unmatched";
   var match=candidates.MinBy(candidate=>candidate.Distance);
   return $"{match.Slot.Index}|{match.Distance}|{name}|{rotation}|{actualX}|{actualY}|{match.Position.X}|{match.Position.Y}";
  }).ToArray();
 }
 internal static string[] DebugInferSlots(IEnumerable<string> rows)
 {
  return rows.Select(row=>
  {
   var parts=row.Split('|');var name=parts[0];var rotation=byte.Parse(parts[1]);var actualX=int.Parse(parts[2]);var actualY=int.Parse(parts[3]);var asset=Assets[name];
   var min=asset.Min(rotation);var size=asset.Size(rotation);var mapW=rotation is 0 or 2?asset.W:asset.H;var mapH=rotation is 0 or 2?asset.H:asset.W;
   var centerX=min.X+size.X/2;var centerY=min.Y+size.Y/2;centerX+=centerX>mapW/2?-(centerX%8):centerX%8;centerY+=centerY>mapH/2?-(centerY%8):centerY%8;
   var type=name.Contains("extralarge",StringComparison.Ordinal)?1:name.StartsWith("roman_dlc",StringComparison.Ordinal)?7:0;var islandSize=name.Contains("extralarge",StringComparison.Ordinal)?"XL":name.Contains("_large_",StringComparison.Ordinal)?"Large":name.Contains("_medium_",StringComparison.Ordinal)?"Medium":"Small";
   return $"{name}|{rotation}|{type}|{islandSize}|{actualX+centerX}|{actualY+centerY}";
  }).ToArray();
 }
 internal static int DebugSlotHalf(string size)=>SlotHalf(size);
 internal static World Complete(LatiumGeneration latium,List<GeneratedIsland> albion)
 {
  var generated=new List<GeneratedIsland>(latium.Islands.Count+albion.Count);generated.AddRange(latium.Islands);generated.AddRange(albion);
  var fertilities=generated.ToDictionary(x=>x.Name,x=>x.Fertilities);
  var sites=new Dictionary<string,SiteCounts>();if(latium.CinisFertilities.Length!=0)sites[CinisName]=latium.CinisSites;
  return new World(latium.Seed,latium.Width,generated.Where(x=>x.SlotIndex>=0).Select(x=>x.Name).ToArray(),sites,fertilities,generated);
 }
 static (int X,int Y)[] BuildArchipelagoOffsets()
 {
  var result=new (int X,int Y)[80];var count=0;for(var radius=24;radius<=96;radius+=24){for(var x=-radius;x<=radius;x+=24){result[count++]=(x,radius);result[count++]=(x,-radius);}for(var y=-radius+24;y<=radius-24;y+=24){result[count++]=(radius,y);result[count++]=(-radius,y);}}return result;
 }
 // The player start points (where ships spawn on the first map): the type-2 elements of the map template, identical for every
 // seed of a template and size. Wiggle (Rift) and decoration placement keep clear of them.
 static (int X,int Y)[] StartPoints(MapProfile profile)
 {
  // The template depends on the size (Large/Medium/Small). Values come from roman_province_*.a7tinfo (base game) and, with DLC01,
  // from the DLC01 templates where they differ (Corners and Island Chains).
  var size=profile.Size;
  if(profile.Dlc01)
    return profile.Template switch
    {
     MapTemplateKind.IslandChains=>[size switch{MapSizeKind.Large=>(1048,1040),MapSizeKind.Medium=>(1080,1048),_=>(1072,1024)}],
     MapTemplateKind.Atoll=>[(1040,1064)],
     MapTemplateKind.Rift=>[(472,1584),(1568,472)],
     MapTemplateKind.Archipelago=>[size switch{MapSizeKind.Large=>(1072,1064),MapSizeKind.Medium=>(1032,1056),_=>(1024,1072)}],
     _=>size switch
     {
      MapSizeKind.Large=>[(456,1584),(1576,1584),(456,456),(1576,456)],
      MapSizeKind.Medium=>[(536,1504),(1496,544),(536,544),(1496,1504)],
      _=>[(536,1584),(536,448),(1496,1584),(1496,440)]
     }
    };
  return profile.Template switch
  {
   MapTemplateKind.IslandChains=>[size switch{MapSizeKind.Large=>(1048,1040),MapSizeKind.Medium=>(1080,1048),_=>(1072,1024)}],
   MapTemplateKind.Atoll=>[(1040,1064)],
   MapTemplateKind.Rift=>[(472,1584),(1568,472)],
   MapTemplateKind.Archipelago=>[size switch{MapSizeKind.Large=>(1072,1064),MapSizeKind.Medium=>(1032,1056),_=>(1024,1072)}],
   _=>size switch
   {
    MapSizeKind.Large=>[(464,1576),(464,464),(1568,472),(1576,1576)],
    MapSizeKind.Medium=>[(536,1504),(1496,544),(1496,1504),(536,544)],
    // Small Corners uses the corner points of the DLC01 template without DLC as well; the base game file lists different ones.
    _=>[(536,1584),(536,448),(1496,1584),(1496,440)]
   }
  };
 }
 // Without DLC the map is shrink-wrapped around its content: width = (larger span of the islands' full images, third parties
 // included, + 111) rounded down to 16, and the content is centred with half the free space rounded up to 8 (the same rule as
 // Albion).
 static (int Width,int ShiftX,int ShiftY,(int X0,int Y0,int X1,int Y1) Playable) FitOpenMap(MapProfile profile,List<Placed> core,(int X,int Y)[] positions,uint[] thirdPartyRaw,(int X,int Y)[]? specialPositions=null)
 {
  int x0=int.MaxValue,y0=int.MaxValue,x1=int.MinValue,y1=int.MinValue;
  void Grow(Asset asset,byte rotation,(int X,int Y) position){var mapW=rotation is 0 or 2?asset.W:asset.H;var mapH=rotation is 0 or 2?asset.H:asset.W;x0=Math.Min(x0,position.X);y0=Math.Min(y0,position.Y);x1=Math.Max(x1,position.X+mapW);y1=Math.Max(y1,position.Y+mapH);}
  for(var index=0;index<core.Count;index++)Grow(core[index].Asset,core[index].Rot,positions[index]);
  var pirate=IslandMasks.Asset("roman_island_3rdparty_pirate_01");var raider=profile.LatiumSpecials.Single(special=>special.Kind=="Raider");var pirateRotation=(byte)(thirdPartyRaw[0]>>30);Grow(pirate,pirateRotation,specialPositions?[0]??CorePosition(pirate,pirateRotation,raider.X+160,raider.Y+160));
  var traders=new[]{IslandMasks.Asset("roman_island_3rdparty_trader_01"),IslandMasks.Asset("roman_island_3rdparty_trader_02")};var anchors=profile.LatiumSpecials.Where(special=>special.Kind=="Trader").Select(special=>(X:special.X+128,Y:special.Y+128)).ToArray();
  if((thirdPartyRaw[1]&1)==0)(traders[0],traders[1])=(traders[1],traders[0]);if((thirdPartyRaw[2]&1)==0)(anchors[0],anchors[1])=(anchors[1],anchors[0]);
  for(var index=0;index<2;index++){var rotation=(byte)(thirdPartyRaw[index+3]>>30);Grow(traders[index],rotation,specialPositions?[index+1]??CorePosition(traders[index],rotation,anchors[index].X,anchors[index].Y));}
  var spanX=x1-x0;var spanY=y1-y0;var width=(Math.Max(spanX,spanY)+111)/16*16;
  // The content box is centred and the shift rounded up to a multiple of 8. When the centring shift is negative and not already
  // a multiple of 8 (the box starts beyond the centre), the game lands one step (8) further out.
  static int Centre(int free,int min){var half=free/2;var shift=(half+7)/8*8-min;return half<min&&(half-min)%8!=0?shift+8:shift;}
  var fitShiftX=Centre(width-spanX,x0);var fitShiftY=Centre(width-spanY,y0);
  // The playable area is the content box grown by (free space - 40) / 2 on every side.
  var margins=((width-spanX-40)/2,(width-spanY-40)/2);
  return(width,fitShiftX,fitShiftY,(x0+fitShiftX-margins.Item1,y0+fitShiftY-margins.Item2,x1+fitShiftX+margins.Item1,y1+fitShiftY+margins.Item2));
 }
 static (int X,int Y)[] WiggleArchipelago(Rng r,int width,List<Placed> core,GeneratorScratch scratch,MapProfile profile,uint[] thirdPartyRaw,List<string>? trace)
 {
  var positions=scratch.GetPositions(core.Count);for(var index=0;index<core.Count;index++){var placed=core[index];var half=SlotHalf(placed.Slot.Size);positions[index]=CorePosition(placed.Asset,placed.Rot,placed.Slot.X+half,placed.Slot.Y+half);}
  // Only starting islands (and Cinis when present) attract the movable
  // islands. NPC islands participate in collision checks but are not targets;
  // the template centre is neither a target nor an artificial obstacle.
  var attractionPoints=scratch.GetAttractionPoints(core.Count+1);var attractionCount=0;if(profile.Dlc01)attractionPoints[attractionCount++]=(1920,1920);for(var index=0;index<core.Count;index++)if(core[index].Slot.Type==1)attractionPoints[attractionCount++]=positions[index];
  const int clearance=16;const int collisionBorder=2;var blockWiggleAtStartPoints=profile.Template==MapTemplateKind.Rift;const int startBlockBorder=24;var occupied=scratch.CountedOccupied;occupied.Reset(width/8);for(var index=0;index<core.Count;index++){var placed=core[index];var position=positions[index];occupied.Add(IslandMasks.Get(placed.Asset.Name),position.X/8,position.Y/8,placed.Rot);}if(profile.Dlc01)occupied.Add(IslandMasks.Get(CinisName),1920/8,1920/8,0);
  var traderAnchor0=(X:0,Y:0);var traderAnchor1=(X:0,Y:0);MapSpecial? raiderSpecial=null;var traderIndex=0;foreach(var special in profile.LatiumSpecials)if(special.Kind=="Raider")raiderSpecial=special;else if(traderIndex++==0)traderAnchor0=(special.X+128,special.Y+128);else traderAnchor1=(special.X+128,special.Y+128);
  var trader0="roman_island_3rdparty_trader_01";var trader1="roman_island_3rdparty_trader_02";if((thirdPartyRaw[1]&1)==0)(trader0,trader1)=(trader1,trader0);if((thirdPartyRaw[2]&1)==0)(traderAnchor0,traderAnchor1)=(traderAnchor1,traderAnchor0);
  void AddSpecial(string name,int anchorX,int anchorY,byte rotation){var mask=IslandMasks.Get(name);var position=CorePosition(mask.Asset,rotation,anchorX,anchorY);occupied.Add(mask,position.X/8,position.Y/8,rotation);}
  var raider=raiderSpecial??throw new InvalidOperationException("Raider-Position fehlt.");AddSpecial("roman_island_3rdparty_pirate_01",raider.X+160,raider.Y+160,(byte)(thirdPartyRaw[0]>>30));AddSpecial(trader0,traderAnchor0.X,traderAnchor0.Y,(byte)(thirdPartyRaw[3]>>30));AddSpecial(trader1,traderAnchor1.X,traderAnchor1.Y,(byte)(thirdPartyRaw[4]>>30));
  var offsets=scratch.GetWiggleOffsets(ArchipelagoOffsets.Length);
  for(var pass=0;pass<2;pass++)
  {
   ArchipelagoOffsets.AsSpan().CopyTo(offsets);
   for(var placedIndex=0;placedIndex<core.Count;placedIndex++)
   {
   var placed=core[placedIndex];if(placed.Slot.Type==1)continue;r.Shuffle(offsets.AsSpan(0,ArchipelagoOffsets.Length));var mask=IslandMasks.Get(placed.Asset.Name);var current=positions[placedIndex];occupied.Remove(mask,current.X/8,current.Y/8,placed.Rot);
   var min=placed.Asset.Min(placed.Rot);var size=placed.Asset.Size(placed.Rot);var cx=current.X+min.X+size.X/2;var cy=current.Y+min.Y+size.Y/2;var distance=int.MaxValue;for(var pointIndex=0;pointIndex<attractionCount;pointIndex++){var point=attractionPoints[pointIndex];distance=Math.Min(distance,Math.Abs(cx-point.X)+Math.Abs(cy-point.Y));}
   for(var offsetIndex=0;offsetIndex<ArchipelagoOffsets.Length;offsetIndex++)
   {
    var offset=offsets[offsetIndex];
    var candidate=(X:current.X+offset.X,Y:current.Y+offset.Y);var x0=candidate.X+min.X;var y0=candidate.Y+min.Y;var x1=x0+size.X;var y1=y0+size.Y;
    var candidateDistance=int.MaxValue;for(var pointIndex=0;pointIndex<attractionCount;pointIndex++){var point=attractionPoints[pointIndex];candidateDistance=Math.Min(candidateDistance,Math.Abs(cx+offset.X-point.X)+Math.Abs(cy+offset.Y-point.Y));}
    var mapWidth=placed.Rot is 0 or 2?placed.Asset.W:placed.Asset.H;var mapHeight=placed.Rot is 0 or 2?placed.Asset.H:placed.Asset.W;
    var playableMin=20-clearance;var playableMax=(profile.Dlc01?2440:2020)+clearance;
    if(candidateDistance>=distance||candidate.X<clearance||candidate.Y<clearance||candidate.X+mapWidth>width-clearance||candidate.Y+mapHeight>width-clearance||x0<playableMin||y0<playableMin||x1>playableMax||y1>playableMax)continue;
    if(blockWiggleAtStartPoints){var blocked=false;foreach(var start in StartPoints(profile))if(candidate.X-startBlockBorder<=start.X&&start.X<candidate.X+mapWidth+startBlockBorder&&candidate.Y-startBlockBorder<=start.Y&&start.Y<candidate.Y+mapHeight+startBlockBorder)blocked=true;if(blocked)continue;}
    if(occupied.Overlaps(mask,candidate.X/8,candidate.Y/8,placed.Rot,collisionBorder))continue;
    positions[placedIndex]=candidate;trace?.Add($"wiggle|{pass}|{placed.Asset.Name}|{offset.X}|{offset.Y}");break;
   }
   var final=positions[placedIndex];occupied.Add(mask,final.X/8,final.Y/8,placed.Rot);
   }
  }
  return positions;
 }
 static ((int X,int Y)[] Core,(int X,int Y)[] Specials) WiggleIslandChain(Rng r,int width,List<Placed> core,GeneratorScratch scratch,MapProfile profile,uint[] thirdPartyRaw,List<string>? trace)
 {
  var positions=scratch.GetPositions(core.Count);for(var index=0;index<core.Count;index++){var placed=core[index];var half=SlotHalf(placed.Slot.Size);positions[index]=CorePosition(placed.Asset,placed.Rot,placed.Slot.X+half,placed.Slot.Y+half);}
  var attractions=scratch.GetAttractionPoints(core.Count+1);var attractionCount=0;if(profile.Dlc01)attractions[attractionCount++]=(1920,1920);for(var index=0;index<core.Count;index++)if(core[index].Slot.Type==1)attractions[attractionCount++]=positions[index];
  var specialAssets=new Asset[3];var specialRotations=new byte[3];var specialPositions=new (int X,int Y)[3];
  var traderAssets=new[]{IslandMasks.Asset("roman_island_3rdparty_trader_01"),IslandMasks.Asset("roman_island_3rdparty_trader_02")};var traderAnchors=profile.LatiumSpecials.Where(special=>special.Kind=="Trader").Select(special=>(X:special.X+128,Y:special.Y+128)).ToArray();if((thirdPartyRaw[1]&1)==0)(traderAssets[0],traderAssets[1])=(traderAssets[1],traderAssets[0]);if((thirdPartyRaw[2]&1)==0)(traderAnchors[0],traderAnchors[1])=(traderAnchors[1],traderAnchors[0]);
  var raider=profile.LatiumSpecials.Single(special=>special.Kind=="Raider");specialAssets[0]=IslandMasks.Asset("roman_island_3rdparty_pirate_01");specialRotations[0]=(byte)(thirdPartyRaw[0]>>30);specialPositions[0]=CorePosition(specialAssets[0],specialRotations[0],raider.X+160,raider.Y+160);for(var index=0;index<2;index++){specialAssets[index+1]=traderAssets[index];specialRotations[index+1]=(byte)(thirdPartyRaw[index+3]>>30);specialPositions[index+1]=CorePosition(specialAssets[index+1],specialRotations[index+1],traderAnchors[index].X,traderAnchors[index].Y);}
  var occupied=scratch.CountedOccupied;occupied.Reset(width/8);for(var index=0;index<core.Count;index++)occupied.Add(IslandMasks.Get(core[index].Asset.Name),positions[index].X/8,positions[index].Y/8,core[index].Rot);for(var index=0;index<3;index++)occupied.Add(IslandMasks.Get(specialAssets[index].Name),specialPositions[index].X/8,specialPositions[index].Y/8,specialRotations[index]);if(profile.Dlc01)occupied.Add(IslandMasks.Get(CinisName),1920/8,1920/8,0);
  const int clearance=16;const int collisionBorder=24;var(fixedX,fixedY)=StartPoints(profile)[0];var playableMin=20-clearance;var playableMax=(profile.Dlc01?2440:2020)+clearance;var offsets=scratch.GetWiggleOffsets(ArchipelagoOffsets.Length);ArchipelagoOffsets.AsSpan().CopyTo(offsets);
  void Move(Asset asset,byte rotation,ref (int X,int Y) current,string name)
  {
   r.Shuffle(offsets.AsSpan(0,ArchipelagoOffsets.Length));var mask=IslandMasks.Get(asset.Name);occupied.Remove(mask,current.X/8,current.Y/8,rotation);var min=asset.Min(rotation);var size=asset.Size(rotation);var mapWidth=rotation is 0 or 2?asset.W:asset.H;var mapHeight=rotation is 0 or 2?asset.H:asset.W;var centerX=current.X+min.X+size.X/2;var centerY=current.Y+min.Y+size.Y/2;var currentDistance=int.MaxValue;for(var pointIndex=0;pointIndex<attractionCount;pointIndex++){var point=attractions[pointIndex];currentDistance=Math.Min(currentDistance,Math.Abs(centerX-point.X)+Math.Abs(centerY-point.Y));}
   for(var offsetIndex=0;offsetIndex<ArchipelagoOffsets.Length;offsetIndex++)
   {
    var offset=offsets[offsetIndex];var candidate=(X:current.X+offset.X,Y:current.Y+offset.Y);var candidateDistance=int.MaxValue;for(var pointIndex=0;pointIndex<attractionCount;pointIndex++){var point=attractions[pointIndex];candidateDistance=Math.Min(candidateDistance,Math.Abs(centerX+offset.X-point.X)+Math.Abs(centerY+offset.Y-point.Y));}if(candidateDistance>=currentDistance)continue;
    var fullX0=candidate.X;var fullY0=candidate.Y;var fullX1=fullX0+mapWidth;var fullY1=fullY0+mapHeight;if(fullX0<clearance||fullY0<clearance||fullX1>width-clearance||fullY1>width-clearance)continue;var activeX0=candidate.X+min.X;var activeY0=candidate.Y+min.Y;var activeX1=activeX0+size.X;var activeY1=activeY0+size.Y;if(activeX0<playableMin||activeY0<playableMin||activeX1>playableMax||activeY1>playableMax)continue;if(fullX0-collisionBorder<=fixedX&&fixedX<fullX1+collisionBorder&&fullY0-collisionBorder<=fixedY&&fixedY<fullY1+collisionBorder)continue;if(occupied.Overlaps(mask,candidate.X/8,candidate.Y/8,rotation,2))continue;current=candidate;trace?.Add($"wiggle|0|{name}|{offset.X}|{offset.Y}");break;
   }
   occupied.Add(mask,current.X/8,current.Y/8,rotation);
  }
  for(var index=0;index<core.Count;index++){if(core[index].Slot.Type==1)continue;var current=positions[index];Move(core[index].Asset,core[index].Rot,ref current,core[index].Asset.Name);positions[index]=current;}for(var index=0;index<3;index++){var current=specialPositions[index];Move(specialAssets[index],specialRotations[index],ref current,specialAssets[index].Name);specialPositions[index]=current;}
  return(positions,specialPositions);
 }
 static void PlaceDecorations(Rng r,int width,int shiftX,int shiftY,List<Placed> core,uint[] thirdPartyRaw,GeneratorScratch scratch,MapProfile profile,List<string>? trace=null,(int X,int Y)[]? corePositions=null,(int X,int Y)[]? movedSpecialPositions=null,bool cardinalCollision=false,(int X0,int Y0,int X1,int Y1)? playable=null,MapLayout? layout=null)
 {
  var occupied=scratch.Occupied;occupied.Reset(width/8);
  void Add(Asset asset,int x,int y,byte rotation)=>occupied.Add(IslandMasks.Get(asset.Name),x/8,y/8,rotation);
  if(profile.Dlc01){Add(IslandMasks.Asset(CinisName),1920+shiftX,1920+shiftY,0);layout?.Add(CinisName,MapLayout.Island,-1,0,1920+shiftX,1920+shiftY);}
  for(var index=0;index<core.Count;index++)
  {
   var placed=core[index];var half=SlotHalf(placed.Slot.Size);var position=corePositions is null?CorePosition(placed.Asset,placed.Rot,placed.Slot.X+half,placed.Slot.Y+half):corePositions[index];
   var x=position.X+shiftX;var y=position.Y+shiftY;
   Add(placed.Asset,x,y,placed.Rot);layout?.Add(placed.Asset.Name,MapLayout.Island,placed.Slot.Index,placed.Rot,x,y);
  }
  var fixedPoints=StartPoints(profile);
  // The player start points keep decorations away. With DLC01 (fixed map size) the engine uses their template position, without the
  // map shift that applies where islands stick out past the map edge (Corners Large seed 27028); without DLC01 (shrink-wrapped,
  // centred map) it uses the shifted position (1204 Archipelago Small without DLC).
  foreach(var point in fixedPoints)occupied.Set((point.Item1+(profile.Dlc01?0:shiftX))/8,(point.Item2+(profile.Dlc01?0:shiftY))/8);

  var specialPositionIndex=0;void AtAnchor(Asset asset,(int X,int Y) anchor,byte rotation)
  {
   var position=movedSpecialPositions is null?CorePosition(asset,rotation,anchor.X,anchor.Y):movedSpecialPositions[specialPositionIndex++];
   Add(asset,position.X+shiftX,position.Y+shiftY,rotation);layout?.Add(asset.Name,MapLayout.Special,-1,rotation,position.X+shiftX,position.Y+shiftY);
  }
  var raider=profile.LatiumSpecials.Single(special=>special.Kind=="Raider");
  AtAnchor(IslandMasks.Asset("roman_island_3rdparty_pirate_01"),(raider.X+160,raider.Y+160),(byte)(thirdPartyRaw[0]>>30));
  var traders=new[]{IslandMasks.Asset("roman_island_3rdparty_trader_01"),IslandMasks.Asset("roman_island_3rdparty_trader_02")};
  var traderAnchors=profile.LatiumSpecials.Where(special=>special.Kind=="Trader").Select(special=>(X:special.X+128,Y:special.Y+128)).ToArray();
  if((thirdPartyRaw[1]&1)==0)(traders[0],traders[1])=(traders[1],traders[0]);
  if((thirdPartyRaw[2]&1)==0)(traderAnchors[0],traderAnchors[1])=(traderAnchors[1],traderAnchors[0]);
  AtAnchor(traders[0],traderAnchors[0],(byte)(thirdPartyRaw[3]>>30));
  AtAnchor(traders[1],traderAnchors[1],(byte)(thirdPartyRaw[4]>>30));

  var n=width/16;var gridLength=n*n;var grid=scratch.GetGrid(gridLength);for(var i=0;i<gridLength;i++)grid[i]=i;r.Shuffle(grid.AsSpan(0,gridLength));
  var decorations=Enumerable.Range(1,6).Select(i=>IslandMasks.Asset($"roman_island_deco_{i:00}")).ToList();r.Shuffle(decorations);
  const int clearanceCells=2;
  bool Bounds((int X0,int Y0,int X1,int Y1) rect)
  {
   const int playableMin=20;var margin=clearanceCells*8;var playableMax=profile.Dlc01?2440:2020;
   var px0=playableMin-margin;var py0=playableMin-margin;var px1=playableMax+shiftX+margin;var py1=playableMax+shiftY+margin;
   // The playable rectangle is compared in whole 8-unit cells, so its far edges round up.
   px1=(px1+7)/8*8;py1=(py1+7)/8*8;
   // Without DLC the map is shrink-wrapped, and the playable rectangle is the whole map minus an 8-unit rim.
   if(!profile.Dlc01&&playable is {} area){px0=area.X0-margin;py0=area.Y0-margin;px1=area.X1+margin;py1=area.Y1+margin;}
   else if(!profile.Dlc01){px1=width-8;py1=width-8;}
   var overlaps=rect.X1>px0&&rect.Y1>py0&&rect.X0<px1&&rect.Y0<py1;
   if(overlaps)return rect.X0>=px0&&rect.Y0>=py0&&rect.X1<=px1&&rect.Y1<=py1;
   return false;
  }
  bool AxisClear(Asset asset,int x,int y,int axis)
  {
   var rotation=(byte)axis;var min=asset.Min(rotation);var size=asset.Size(rotation);
   var collisionMargin=clearanceCells*8;if(x-collisionMargin<=0||y-collisionMargin<=0||x+size.X+collisionMargin>=width||y+size.Y+collisionMargin>=width)return false;
   var mapW=rotation is 0 or 2?asset.W:asset.H;var mapH=rotation is 0 or 2?asset.H:asset.W;
   var full=(X0:x-min.X,Y0:y-min.Y,X1:x-min.X+mapW,Y1:y-min.Y+mapH);
   // A decoration turned a quarter turn (rotation 1) is tested in the clearance pass with the horizontal box offset of the
   // OPPOSITE quarter turn (rotation 3), as if the engine turned it the other way round; the check after the flip draw uses the
   // plain footprint. The offset is min(1).X - min(3).X: 16 for deco_01, 8 for deco_02/03/05, 0 for deco_04/06.
   var clearanceShift=rotation==1?min.X-asset.Min(3).X:0;
   if(!Bounds((full.X0+clearanceShift,full.Y0,full.X1+clearanceShift,full.Y1)))return false;
   const int scanFarBorderCells=clearanceCells+1;
   if(cardinalCollision){var mask=IslandMasks.Get(asset.Name);var fullX=(x-min.X)/8;var fullY=(y-min.Y)/8;foreach(var cell in mask.Cells[rotation]){var px=fullX+cell.X;var py=fullY+cell.Y;for(var dy=-scanFarBorderCells;dy<=scanFarBorderCells;dy++)for(var dx=-scanFarBorderCells;dx<=scanFarBorderCells;dx++)if(occupied.Any(px+dx,py+dy,px+dx,py+dy))return false;}return true;}
   // The native scan starts one 8-unit sample before the already-expanded
   // active rectangle and includes the matching sample at the far edge.
   var scanX0=x/8-scanFarBorderCells;var scanY0=y/8-scanFarBorderCells;var scanX1=(x+size.X)/8+scanFarBorderCells;var scanY1=(y+size.Y)/8+scanFarBorderCells;
   return !occupied.Any(scanX0,scanY0,scanX1,scanY1,true);
  }
  var preference=0;
  // The DLC adds four decoration islands: 14 with it, 10 without.
  var decorationCount=profile.Dlc01?14:10;
  for(var placedIndex=0;placedIndex<decorationCount;placedIndex++)
  {
   var asset=decorations[placedIndex%decorations.Count];var done=false;
   for(var gridIndex=0;gridIndex<gridLength;gridIndex++)
   {
    var cell=grid[gridIndex];
    var x=cell%n*16+8;var y=cell/n*16+8;var axis=preference;
    if(!AxisClear(asset,x,y,axis)){axis^=1;if(!AxisClear(asset,x,y,axis))continue;}
    var rotation=(byte)(axis+2*r.Scaled(2));var min=asset.Min(rotation);
    var mapW=rotation is 0 or 2?asset.W:asset.H;var mapH=rotation is 0 or 2?asset.H:asset.W;
    var finalBounds=(X0:x-min.X,Y0:y-min.Y,X1:x-min.X+mapW,Y1:y-min.Y+mapH);
    if(!Bounds(finalBounds)){trace?.Add($"retry|{placedIndex}|{asset.Name}|{gridIndex}|{x}|{y}|{rotation}");continue;}
    Add(asset,x-min.X,y-min.Y,rotation);layout?.Add(asset.Name,MapLayout.Decoration,-1,rotation,x-min.X,y-min.Y);trace?.Add($"placed|{placedIndex}|{asset.Name}|{gridIndex}|{x-min.X}|{y-min.Y}|{rotation}");preference^=1;done=true;break;
   }
   if(!done&&profile.Dlc01)throw new InvalidOperationException($"Dekorationsinsel {placedIndex+1} konnte nicht platziert werden.");
  }
 }
 static(int X,int Y)CorePosition(Asset asset,byte rotation,int targetX,int targetY)
 {
  var min=asset.Min(rotation);var size=asset.Size(rotation);
  var centerX=min.X+size.X/2;var centerY=min.Y+size.Y/2;
  var mapW=rotation is 0 or 2?asset.W:asset.H;var mapH=rotation is 0 or 2?asset.H:asset.W;
  centerX+=centerX>mapW/2?-(centerX%8):centerX%8;
  centerY+=centerY>mapH/2?-(centerY%8):centerY%8;
  return(targetX-centerX,targetY-centerY);
 }
 static int SlotHalf(string size)=>size switch{"Small"=>128,"Medium"=>160,"Large" or "XL"=>216,_=>throw new ArgumentOutOfRangeException(nameof(size))};
 static uint Rule(List<uint>x,int type){for(var i=0;i<x.Count;i++){var v=x[i];if(type==1?v==31312:v is 3656 or 14198){x.RemoveAt(i);x.Add(v);return v;}}throw new InvalidOperationException();}
 internal static readonly Dictionary<uint,uint> RoleOfVariant=new(){[41833]=31312,[41834]=3656,[41835]=14198,[145110]=144793,[41837]=31312,[41838]=3656,[41839]=14198,[145109]=144793};
 static List<uint> SettingRuleList(FertilitySetting setting)=>setting==FertilitySetting.Regular?[41833,41834,41835,145110]:[41837,41838,41839,145109];
 static uint RuleVariant(List<uint> x,int type,FertilitySetting setting)
 {
  var starter=setting==FertilitySetting.Regular?41833u:41837u;var tierA=setting==FertilitySetting.Regular?41834u:41838u;var tierB=setting==FertilitySetting.Regular?41835u:41839u;
  for(var i=0;i<x.Count;i++){var v=x[i];if(type==1?v==starter:v==tierA||v==tierB){x.RemoveAt(i);x.Add(v);return v;}}
  throw new InvalidOperationException();
 }
 static uint[] Assign(Asset island,uint[] defs,Dictionary<uint,List<uint>>bags,Rng r){var z=new List<uint>();foreach(var pool in defs){if(!Pools.TryGetValue(pool,out var src)){z.Add(pool);continue;}var rejected=0;var fresh=false;while(true){if(!bags.TryGetValue(pool,out var bag))bags[pool]=bag=[];if(bag.Count==0){bag.AddRange(src);r.Shuffle(bag);fresh=true;rejected=0;}var v=bag[^1];bag.RemoveAt(bag.Count-1);if(!z.Contains(v)&&(!(v is 8577 or 32027)||island.HasRiver)){z.Add(v);break;}if(bag.Count!=rejected){bag.Add(v);(bag[rejected],bag[^1])=(bag[^1],bag[rejected]);rejected++;continue;}if(!fresh){bag.Clear();continue;}throw new InvalidOperationException();}}return[..z];}
 static List<Asset>A(params string[]n)=>n.Select(x=>Assets[x]).ToList();
 static (int Type,string Size)PoolKey(int type,string size)=>type==1&&size!="XL"?(0,size):(type,size);
 static List<Asset>LatiumPool((int Type,string Size) key)=>key switch
 {
  (7,"Medium")=>A("roman_dlc01_island_medium_01","roman_dlc01_island_medium_02","roman_dlc01_island_medium_03"),
  (7,"Small")=>A("roman_dlc01_island_small_02","roman_dlc_01_island_small_01"),
  (1,"XL")=>A("roman_island_extralarge_01","roman_island_extralarge_02","roman_island_extralarge_03","roman_island_extralarge_04"),
  (0,"Large")=>A("roman_island_large_01","roman_island_large_02","roman_island_large_03","roman_island_large_04","roman_island_large_05","roman_island_large_06","roman_island_large_07","roman_island_large_09"),
  (0,"Medium")=>A(Enumerable.Range(1,8).Select(i=>$"roman_island_medium_{i:00}").ToArray()),
  (0,"Small")=>A(Enumerable.Range(1,7).Select(i=>$"roman_island_small_{i:00}").ToArray()),
  _=>throw new InvalidOperationException($"Kein Latium-Inselpool für Typ {key.Type}, Größe {key.Size}.")
 };
 // A starting island turns so that its "start coast" faces the middle of the map: the rotation is the quarter turn whose coast
 // direction is closest to the direction from the starting bay to the map centre. One rule for every template, with or without
 // DLC.
 static byte LatiumStarterRotation(MapProfile profile,Slot slot,Asset asset,IReadOnlyList<Slot> starters)
 {
  var half=SlotHalf(slot.Size);var centre=profile.LatiumTemplateSize/2d;var target=Math.Atan2(centre-(slot.Y+half),centre-(slot.X+half));if(target<0)target+=2*Math.PI;
  var direction=StartCoastDirection(asset.Name);var best=0;var distance=double.MaxValue;
  for(var rotation=0;rotation<4;rotation++){var delta=Math.Abs((direction+rotation*Math.PI/2-target)%(2*Math.PI));delta=Math.Min(delta,2*Math.PI-delta);if(delta<distance){distance=delta;best=rotation;}}
  return(byte)best;
 }
 static List<Slot>Slots(MapProfile profile)=>profile.LatiumSlots.Select(slot=>new Slot(slot.Index,slot.X,slot.Y,slot.Size,slot.Type)).ToList();
 sealed record Slot(int Index,int X,int Y,string Size,int Type);sealed record Placed(Asset Asset,Slot Slot,byte Rot);sealed record Record(Asset Asset,int Priority,uint Fixed,int SlotIndex,string Size);
}

internal static class AlbionGenerator
{
 static readonly Dictionary<uint,uint[]> Pools=new(){[31359]=[2212,2214],[144829]=[2217,4063],[91229]=[2217,4063,8487],[32460]=[4049,4066],[31361]=[2218,4082,2211,2202,2219,8432]};
 static readonly Dictionary<uint,uint[]> Sets=new()
 {
  [8174]=[31359,144829,8487,4049,31361,31361],[8179]=[31359,91229,32460,4064,31361,31361],[8181]=[31359,91229,32460,51212,31361,31361],
  [41852]=[31359,91229,91229,4049,31361],[41853]=[91229,4064,4066,31361,31361],[41854]=[31359,32460,51212,31361,31361],
  [41856]=[31359,91229,31361,31361],[41857]=[91229,32460,4064,31361],[41858]=[31359,32460,51212,31361],
 };
 static readonly Dictionary<string,AlbionAsset> Assets=AlbionAsset.All.ToDictionary(x=>x.Name,StringComparer.OrdinalIgnoreCase);
 [ThreadStatic]static WorldBits? decorationOccupied;
 [ThreadStatic]static int[]? decorationGrid;

 public static List<GeneratedIsland> Generate(uint seed,MapProfile? profile=null,int? debugPhaseDraws=null,FertilitySetting fertilitySetting=FertilitySetting.Abundant,SlotSetting slotSetting=SlotSetting.Abundant,MapLayout? layout=null)
 {
  profile??=MapProfiles.Default;
  if(profile.Retro)profile=MapProfiles.Get(profile.Template,profile.Size,false);
  var core=GenerateCore(seed,profile,debugPhaseDraws,slotSetting,layout:layout);var r=core.Rng;var placed=core.Placed;var sitesBySlot=core.SitesBySlot;
  var rules=new List<uint>{8174,8179,8181};r.Shuffle(rules);r.Shuffle(placed);placed=placed.OrderByDescending(x=>x.Slot.Type).ToList();var bags=new Dictionary<uint,List<uint>>();var result=new List<GeneratedIsland>();
  foreach(var item in placed){var set=Rule(rules,item.Slot.Type);var resolved=Generator.ResolveSet(set,fertilitySetting,profile);var assigned=Assign(item.Asset,Sets[resolved],bags,r);result.Add(new(item.Asset.Name,item.Slot.Index,item.Slot.Size,set,assigned,sitesBySlot[item.Slot.Index]));}return result;
 }

 internal static RegionMetrics GenerateMetricsTotals(uint seed,MapProfile? profile=null)
 {
  var core=GenerateCore(seed,profile??MapProfiles.Default);var sites=new SiteCounts();var area=0;var swamp=0;
  foreach(var item in core.Placed){sites+=core.SitesBySlot[item.Slot.Index];var value=IslandAreas.Get(item.Asset.Name);area+=value.Total;swamp+=value.Swamp;}
  return new(sites,0,0,0,0,0,area,swamp);
 }
 internal static int DebugWidth(uint seed,MapProfile profile)=>GenerateCore(seed,profile).Width;
 internal static int DebugInitialWidth(uint seed,MapProfile profile){var core=GeneratePlacementCore(seed,profile);return MapWidth(core.Placed,core.Specials,profile);}
 internal static int DebugChainWidth(uint seed,MapProfile profile,int movementAdvance)
 {
  var core=GeneratePlacementCore(seed,profile);var movement=core.Rng.Clone();movement.Advance(movementAdvance);var positions=MoveIslandChain(movement,core.Placed,core.Specials);return MapWidth(core.Placed,core.Specials,profile,positions.Core,positions.Specials);
 }

 static AlbionCore GenerateCore(uint seed,MapProfile profile,int? debugPhaseDraws=null,SlotSetting slotSetting=SlotSetting.Abundant,List<string>? debugDecorationTrace=null,MapLayout? layout=null)
 {
  var r=new Rng(seed);r.Advance(9);var original=Slots(profile);var starters=original.Where(x=>x.Type==1).ToList();r.Shuffle(starters);var slots=original.Where(x=>x.Type==0).Concat(starters).ToList();r.Shuffle(slots);slots=slots.OrderByDescending(x=>x.Type).ToList();
  var pools=new Dictionary<string,List<AlbionAsset>>{{"Large",A(Enumerable.Range(1,8).Select(i=>$"celtic_island_large_{i:00}"))},{"Medium",A(Enumerable.Range(1,7).Select(i=>$"celtic_island_medium_{i:00}"))},{"Small",A(Enumerable.Range(1,7).Select(i=>$"celtic_island_small_{i:00}"))}};
  var placed=new List<AlbionPlaced>();foreach(var slot in slots){var candidates=pools[slot.Size];if(candidates.Count==0)pools[slot.Size]=candidates=AlbionPool(slot.Size);var choice=candidates.Count==1?0:(int)r.Scaled((uint)candidates.Count);var asset=candidates[choice];candidates.RemoveAt(choice);var raw=r.Next();placed.Add(new(asset,slot,slot.Type==1?StarterRotation(profile,slot,asset,starters):(byte)(raw>>30)));}
  var specials=BuildSpecials(r,profile);
  var movedPositions=profile.Template==MapTemplateKind.IslandChains?MoveIslandChain(debugPhaseDraws is null?r:r.Clone(),placed,specials):default;
  var width=MapWidth(placed,specials,profile,movedPositions.Core,movedPositions.Specials);
  if(debugPhaseDraws is int draws)r.Advance(draws);
  else PlaceDecorations(r,placed,specials,profile,width,movedPositions.Core,movedPositions.Specials,debugDecorationTrace,layout:layout);
  var sitesBySlot=new Dictionary<int,SiteCounts>(placed.Count);foreach(var item in placed)sitesBySlot[item.Slot.Index]=SiteActivation.GenerateAlbion(r,item.Asset,slotSetting);
  return new(r,placed,sitesBySlot,width);
 }

 internal static string[] DebugPlacements(uint seed,MapProfile profile)
 {
  var r=new Rng(seed);r.Advance(9);var original=Slots(profile);var starters=original.Where(x=>x.Type==1).ToList();r.Shuffle(starters);var slots=original.Where(x=>x.Type==0).Concat(starters).ToList();r.Shuffle(slots);slots=slots.OrderByDescending(x=>x.Type).ToList();
  var pools=new Dictionary<string,List<AlbionAsset>>{{"Large",A(Enumerable.Range(1,8).Select(i=>$"celtic_island_large_{i:00}"))},{"Medium",A(Enumerable.Range(1,7).Select(i=>$"celtic_island_medium_{i:00}"))},{"Small",A(Enumerable.Range(1,7).Select(i=>$"celtic_island_small_{i:00}"))}};var result=new List<string>();
  foreach(var slot in slots){var candidates=pools[slot.Size];if(candidates.Count==0)pools[slot.Size]=candidates=AlbionPool(slot.Size);var choice=candidates.Count==1?0:(int)r.Scaled((uint)candidates.Count);var asset=candidates[choice];candidates.RemoveAt(choice);var raw=r.Next();var rotation=slot.Type==1?StarterRotation(profile,slot,asset,starters):(byte)(raw>>30);var position=Position(asset,rotation,slot.X+(slot.Size=="Large"?216:slot.Size=="Medium"?160:128),slot.Y+(slot.Size=="Large"?216:slot.Size=="Medium"?160:128));result.Add($"{asset.Name}|{rotation}|{slot.Index}|{position.X}|{position.Y}|{slot.Type}|{slot.X}|{slot.Y}");}return[..result];
 }

 internal static string[] DebugMovedPlacements(uint seed,MapProfile profile,int movementAdvance=0)
 {
  var core=GeneratePlacementCore(seed,profile);var movement=core.Rng.Clone();movement.Advance(movementAdvance);var moved=profile.Template==MapTemplateKind.IslandChains?MoveIslandChain(movement,core.Placed,core.Specials).Core:core.Placed.Select(Position).ToArray();
  return core.Placed.Select((item,index)=>$"{item.Asset.Name}|{item.Rotation}|{item.Slot.Index}|{moved[index].X}|{moved[index].Y}").ToArray();
 }
 internal static string[] DebugMovedSpecials(uint seed,MapProfile profile)
 {
  var core=GeneratePlacementCore(seed,profile);var moved=MoveIslandChain(core.Rng.Clone(),core.Placed,core.Specials);return core.Specials.Select((item,index)=>$"{item.Asset.Name}|{item.Rotation}|{moved.Specials[index].X}|{moved.Specials[index].Y}").ToArray();
 }
 internal static string[] DebugDecorations(uint seed,MapProfile profile){var trace=new List<string>();GenerateCore(seed,profile,null,SlotSetting.Abundant,trace);return[..trace];}
 internal static string[] DebugWigglePermutations(uint seed,MapProfile profile)
 {
  var core=GeneratePlacementCore(seed,profile);var permutation=Enumerable.Range(0,80).ToArray();var result=new List<string>();foreach(var item in core.Placed){if(item.Slot.Type==1)continue;core.Rng.Shuffle(permutation);result.Add($"{item.Asset.Name}|{string.Join(',',permutation)}");}return[..result];
 }

 internal static string[] DebugMatchSlots(MapProfile profile,IEnumerable<string> rows)
 {
  return rows.Where(row=>Assets.ContainsKey(row.Split('|')[0])).Select(row=>
  {
   var parts=row.Split('|');var name=parts[0];var rotation=byte.Parse(parts[1]);var actualX=int.Parse(parts[2]);var actualY=int.Parse(parts[3]);var asset=Assets[name];var size=name.Contains("_large_",StringComparison.Ordinal)?"Large":name.Contains("_medium_",StringComparison.Ordinal)?"Medium":"Small";
   var candidates=AlbionSlotsOf(profile).Where(slot=>slot.Size==size).Select(slot=>{var half=size=="Large"?216:size=="Medium"?160:128;var position=Position(asset,rotation,slot.X+half,slot.Y+half);return(Slot:slot,Distance:Math.Abs(position.X-actualX)+Math.Abs(position.Y-actualY),Position:position);}).ToArray();
   if(candidates.Length==0)return $"-1|-1|{name}|{rotation}|{actualX}|{actualY}|unmatched|unmatched";
   var match=candidates.MinBy(candidate=>candidate.Distance);
   return $"{match.Slot.Index}|{match.Distance}|{name}|{rotation}|{actualX}|{actualY}|{match.Position.X}|{match.Position.Y}";
  }).ToArray();
 }

 static int MapWidth(List<AlbionPlaced> placed,List<AlbionSpecialPlaced> specials,MapProfile profile,(int X,int Y)[]? positions=null,(int X,int Y)[]? specialPositions=null)
 {
  var first=placed[0];var initial=Bounds(first.Asset,first.Rotation,positions?[0]??Position(first));var minX=initial.X0;var minY=initial.Y0;var maxX=initial.X1;var maxY=initial.Y1;
  for(var i=1;i<placed.Count;i++){var item=placed[i];Expand(ref minX,ref minY,ref maxX,ref maxY,Bounds(item.Asset,item.Rotation,positions?[i]??Position(item)));}
  for(var index=0;index<specials.Count;index++){var special=specials[index];Expand(ref minX,ref minY,ref maxX,ref maxY,Bounds(special.Asset,special.Rotation,specialPositions?[index]??special.Position));}
  var width=(Math.Max(maxX-minX,maxY-minY)+111)/16*16;return width;
 }

 static (int X,int Y)[] AlbionStartPoints(MapProfile profile)
 {
  var size=profile.Size;
  return profile.Template switch
  {
   MapTemplateKind.Rift=>[(472,1560),(size==MapSizeKind.Large?1560:1552,480)],
   MapTemplateKind.Atoll=>[size switch{MapSizeKind.Large=>(1016,1000),MapSizeKind.Medium=>(1040,1016),_=>(1032,976)}],
   MapTemplateKind.Archipelago=>[size==MapSizeKind.Small?(952,1088):(952,1080)],
   MapTemplateKind.IslandChains=>[size switch{MapSizeKind.Large=>(1096,1040),MapSizeKind.Medium=>(1080,1048),_=>(1072,1056)}],
   _=>size switch
   {
    MapSizeKind.Large=>[(536,1504),(1496,544),(1496,1504),(536,544)],
    MapSizeKind.Medium=>[(568,1440),(576,608),(1464,1432),(1472,608)],
    _=>[(600,1408),(600,648),(1432,1416),(1432,640)]
   }
  };
 }
 static int PlaceDecorations(Rng r,List<AlbionPlaced> placed,List<AlbionSpecialPlaced> specials,MapProfile profile,int width,(int X,int Y)[]? positions,(int X,int Y)[]? specialPositions,List<string>? trace=null,bool cardinalCollision=false,MapLayout? layout=null)
 {
  var first=Bounds(placed[0].Asset,placed[0].Rotation,positions?[0]??Position(placed[0]));var minX=first.X0;var minY=first.Y0;var maxX=first.X1;var maxY=first.Y1;
  for(var index=1;index<placed.Count;index++)Expand(ref minX,ref minY,ref maxX,ref maxY,Bounds(placed[index].Asset,placed[index].Rotation,positions?[index]??Position(placed[index])));
  for(var index=0;index<specials.Count;index++)Expand(ref minX,ref minY,ref maxX,ref maxY,Bounds(specials[index].Asset,specials[index].Rotation,specialPositions?[index]??specials[index].Position));
  var spanX=maxX-minX;var spanY=maxY-minY;var maxSpan=Math.Max(spanX,spanY);
  int shiftX,shiftY;
  // The islands are centred on both axes: half the free space rounded up to a multiple of 8, with the same extra step as Latium's
  // FitOpenMap when the centring shift is negative and not a multiple of 8. The result is the IslandShift of the saved Albion
  // template.
  static int Centre(int free,int min){var half=free/2;var shift=(half+7)/8*8-min;return half<min&&(half-min)%8!=0?shift+8:shift;}
  shiftX=Centre(width-spanX,minX);shiftY=Centre(width-spanY,minY);
  var freeX=width-spanX;var freeY=width-spanY;var playableX0=minX+shiftX-(freeX-40)/2;var playableY0=minY+shiftY-(freeY-40)/2;var playableX1=maxX+shiftX+(freeX-40)/2;var playableY1=maxY+shiftY+(freeY-40)/2;

  var occupied=decorationOccupied??=new WorldBits();occupied.Reset(width/8);
  void Add(string name,(int X,int Y) position,byte rotation)=>occupied.Add(IslandMasks.Get(name),(position.X+shiftX)/8,(position.Y+shiftY)/8,rotation);
  for(var index=0;index<placed.Count;index++)Add(placed[index].Asset.Name,positions?[index]??Position(placed[index]),placed[index].Rotation);
  for(var index=0;index<specials.Count;index++)Add(specials[index].Asset.Name,specialPositions?[index]??specials[index].Position,specials[index].Rotation);
  // Player start points from celtic_province_*.a7tinfo. The ones stored in a savegame are not used: when Albion is the second
  // province the ship spawns at a third-party harbour instead.
  if(layout is not null)
  {
   layout.Width=width;layout.FullX=layout.FullY=0;layout.FullSize=width;layout.BaseSize=0;
   for(var index=0;index<placed.Count;index++){var position=positions?[index]??Position(placed[index]);layout.Add(placed[index].Asset.Name,MapLayout.Island,placed[index].Slot.Index,placed[index].Rotation,position.X+shiftX,position.Y+shiftY);}
   for(var index=0;index<specials.Count;index++){var position=specialPositions?[index]??specials[index].Position;layout.Add(specials[index].Asset.Name,MapLayout.Special,-1,specials[index].Rotation,position.X+shiftX,position.Y+shiftY);}
  }
  var fixedPoints=AlbionStartPoints(profile).Select(point=>(X:point.X+shiftX,Y:point.Y+shiftY)).ToArray();
  // A start point occupies its own grid cell in the same occupancy grid the decoration scan reads.
  foreach(var point in fixedPoints)occupied.Set(point.X/8,point.Y/8);

  var n=width/16;var length=n*n;if(decorationGrid is null||decorationGrid.Length<length)decorationGrid=new int[length];var grid=decorationGrid.AsSpan(0,length);for(var index=0;index<length;index++)grid[index]=index;r.Shuffle(grid);
  var decorations=new Asset[8];for(var index=0;index<decorations.Length;index++)decorations[index]=IslandMasks.Asset($"celtic_island_deco_{index+1:00}");r.Shuffle(decorations.AsSpan());
  const int clearanceCells=2;
  bool BoundsInside((int X0,int Y0,int X1,int Y1) bounds){var margin=clearanceCells*8;var x0=playableX0-margin;var y0=playableY0-margin;var x1=playableX1+margin;var y1=playableY1+margin;var overlaps=bounds.X1>x0&&bounds.Y1>y0&&bounds.X0<x1&&bounds.Y0<y1;return overlaps&&bounds.X0>=x0&&bounds.Y0>=y0&&bounds.X1<=x1&&bounds.Y1<=y1;}
  bool AxisClear(Asset asset,int x,int y,int axis){var rotation=(byte)axis;var min=asset.Min(rotation);var size=asset.Size(rotation);var mapWidth=rotation is 0 or 2?asset.W:asset.H;var mapHeight=rotation is 0 or 2?asset.H:asset.W;var full=(X0:x-min.X,Y0:y-min.Y,X1:x-min.X+mapWidth,Y1:y-min.Y+mapHeight);var clearanceShift=rotation==1?min.X-asset.Min(3).X:0;if(!BoundsInside((full.X0+clearanceShift,full.Y0,full.X1+clearanceShift,full.Y1)))return false;if(cardinalCollision){var mask=IslandMasks.Get(asset.Name);foreach(var cell in mask.Cells[rotation]){var px=full.X0/8+cell.X;var py=full.Y0/8+cell.Y;if(occupied.Any(px,py,px,py)||occupied.Any(px-clearanceCells,py,px-clearanceCells,py)||occupied.Any(px+clearanceCells,py,px+clearanceCells,py)||occupied.Any(px,py-clearanceCells,px,py-clearanceCells)||occupied.Any(px,py+clearanceCells,px,py+clearanceCells))return false;}return true;}var border=clearanceCells+1;var scanX0=x/8-border;var scanY0=y/8-border;var scanX1=(x+size.X)/8+border;var scanY1=(y+size.Y)/8+border;var cellsPerSide=width/8;
  // The game's occupancy grid has one cell more than the map is wide on the far (right/bottom) side, so a scan rectangle may reach
  // exactly one cell past it; anything further out, or past the near edge, is blocked. (Latium treats everything outside as free.)
  return !(scanX0<0||scanY0<0||scanX1>cellsPerSide||scanY1>cellsPerSide||occupied.Any(scanX0,scanY0,Math.Min(scanX1,cellsPerSide-1),Math.Min(scanY1,cellsPerSide-1),true));}
  var preference=0;
  var retries=0;
  for(var placedIndex=0;placedIndex<10;placedIndex++)
  {
   var asset=decorations[placedIndex%decorations.Length];
   for(var gridIndex=0;gridIndex<length;gridIndex++)
   {
    var cell=grid[gridIndex];var x=cell%n*16+8;var y=cell/n*16+8;var axis=preference;if(!AxisClear(asset,x,y,axis)){axis^=1;if(!AxisClear(asset,x,y,axis))continue;}var flip=(int)r.Scaled(2);var rotation=(byte)(axis+2*flip);var min=asset.Min(rotation);var mapWidth=rotation is 0 or 2?asset.W:asset.H;var mapHeight=rotation is 0 or 2?asset.H:asset.W;
    // The drawn flip is final: a candidate whose flipped box leaves the map is retried at the next grid cell; the game does not fall
    // back to the other axis.
    if(!BoundsInside((x-min.X,y-min.Y,x-min.X+mapWidth,y-min.Y+mapHeight))){retries++;trace?.Add($"retry|{placedIndex}|{asset.Name}|{gridIndex}|{x-min.X}|{y-min.Y}|{rotation}");continue;}occupied.Add(IslandMasks.Get(asset.Name),(x-min.X)/8,(y-min.Y)/8,rotation);layout?.Add(asset.Name,MapLayout.Decoration,-1,rotation,x-min.X,y-min.Y);trace?.Add($"placed|{placedIndex}|{asset.Name}|{gridIndex}|{x-min.X}|{y-min.Y}|{rotation}");preference^=1;break;
   }
  }
  return retries;
 }

 static (Rng Rng,List<AlbionPlaced> Placed,List<AlbionSpecialPlaced> Specials) GeneratePlacementCore(uint seed,MapProfile profile)
 {
  var r=new Rng(seed);r.Advance(9);var original=Slots(profile);var starters=original.Where(x=>x.Type==1).ToList();r.Shuffle(starters);var slots=original.Where(x=>x.Type==0).Concat(starters).ToList();r.Shuffle(slots);slots=slots.OrderByDescending(x=>x.Type).ToList();
  var pools=new Dictionary<string,List<AlbionAsset>>{{"Large",A(Enumerable.Range(1,8).Select(i=>$"celtic_island_large_{i:00}"))},{"Medium",A(Enumerable.Range(1,7).Select(i=>$"celtic_island_medium_{i:00}"))},{"Small",A(Enumerable.Range(1,7).Select(i=>$"celtic_island_small_{i:00}"))}};
  var placed=new List<AlbionPlaced>();foreach(var slot in slots){var candidates=pools[slot.Size];if(candidates.Count==0)pools[slot.Size]=candidates=AlbionPool(slot.Size);var choice=candidates.Count==1?0:(int)r.Scaled((uint)candidates.Count);var asset=candidates[choice];candidates.RemoveAt(choice);var raw=r.Next();placed.Add(new(asset,slot,slot.Type==1?StarterRotation(profile,slot,asset,starters):(byte)(raw>>30)));}
  var specials=BuildSpecials(r,profile);return(r,placed,specials);
 }

 static List<AlbionSpecialPlaced> BuildSpecials(Rng r,MapProfile profile)
 {
  var raw=Enumerable.Range(0,5).Select(_=>r.Next()).ToArray();var traderAssets=new[]{Assets["celtic_island_3rdparty_trader_01"],Assets["celtic_island_3rdparty_trader_02"]};var traderAnchors=profile.AlbionSpecials.Where(x=>x.Kind=="Trader").Select(x=>(X:x.X+128,Y:x.Y+128)).ToArray();if((raw[1]&1)==0)(traderAssets[0],traderAssets[1])=(traderAssets[1],traderAssets[0]);if((raw[2]&1)==0)(traderAnchors[0],traderAnchors[1])=(traderAnchors[1],traderAnchors[0]);var raider=profile.AlbionSpecials.Single(x=>x.Kind=="Raider");var pirate=Assets["celtic_island_3rdparty_pirate_01"];var result=new List<AlbionSpecialPlaced>{new(pirate,(byte)(raw[0]>>30),Position(pirate,(byte)(raw[0]>>30),raider.X+160,raider.Y+160))};for(var i=0;i<2;i++){var rotation=(byte)(raw[3+i]>>30);result.Add(new(traderAssets[i],rotation,Position(traderAssets[i],rotation,traderAnchors[i].X,traderAnchors[i].Y)));}return result;
 }

 static ((int X,int Y)[] Core,(int X,int Y)[] Specials) MoveIslandChain(Rng r,List<AlbionPlaced> placed,List<AlbionSpecialPlaced> specials)
 {
  var positions=placed.Select(Position).ToArray();var occupied=new WorldCounts();occupied.Reset(256);
  for(var i=0;i<placed.Count;i++)occupied.Add(IslandMasks.Get(placed[i].Asset.Name),positions[i].X/8,positions[i].Y/8,placed[i].Rotation);
  var specialPositions=specials.Select(special=>special.Position).ToArray();for(var index=0;index<specials.Count;index++)occupied.Add(IslandMasks.Get(specials[index].Asset.Name),specialPositions[index].X/8,specialPositions[index].Y/8,specials[index].Rotation);
  var attractions=placed.Select((item,index)=>(item,index)).Where(pair=>pair.item.Slot.Type==1).Select(pair=>positions[pair.index]).ToArray();
  var offsets=new List<(int X,int Y)>();for(var radius=24;radius<=96;radius+=24){for(var x=-radius;x<=radius;x+=24){offsets.Add((x,radius));offsets.Add((x,-radius));}for(var y=-radius+24;y<=radius-24;y+=24){offsets.Add((radius,y));offsets.Add((-radius,y));}}var shuffled=offsets.ToArray();
  const int mapMargin=16;const int playableMin=4;const int playableMax=2036;
  void Move(AlbionAsset asset,byte rotation,ref (int X,int Y) current)
  {
   r.Shuffle(shuffled.AsSpan());var mask=IslandMasks.Get(asset.Name);occupied.Remove(mask,current.X/8,current.Y/8,rotation);var min=asset.Min(rotation);var size=asset.Size(rotation);var mapWidth=rotation is 0 or 2?asset.W:asset.H;var mapHeight=rotation is 0 or 2?asset.H:asset.W;var center=(X:current.X+min.X+size.X/2,Y:current.Y+min.Y+size.Y/2);var currentDistance=attractions.Min(point=>Math.Abs(center.X-point.X)+Math.Abs(center.Y-point.Y));
   foreach(var offset in shuffled){var candidate=(X:current.X+offset.X,Y:current.Y+offset.Y);var distance=attractions.Min(point=>Math.Abs(center.X+offset.X-point.X)+Math.Abs(center.Y+offset.Y-point.Y));if(distance>=currentDistance)continue;var fullX1=candidate.X+mapWidth;var fullY1=candidate.Y+mapHeight;if(candidate.X<mapMargin||candidate.Y<mapMargin||fullX1>2048-mapMargin||fullY1>2048-mapMargin)continue;var x0=candidate.X+min.X;var y0=candidate.Y+min.Y;var x1=x0+size.X;var y1=y0+size.Y;if(x0<playableMin||y0<playableMin||x1>playableMax||y1>playableMax)continue;if(occupied.Overlaps(mask,candidate.X/8,candidate.Y/8,rotation,2))continue;current=candidate;break;}
   occupied.Add(mask,current.X/8,current.Y/8,rotation);
  }
  for(var index=0;index<placed.Count;index++){if(placed[index].Slot.Type==1)continue;var current=positions[index];Move(placed[index].Asset,placed[index].Rotation,ref current);positions[index]=current;}for(var index=0;index<specials.Count;index++){var current=specialPositions[index];Move(specials[index].Asset,specials[index].Rotation,ref current);specialPositions[index]=current;}return(positions,specialPositions);
 }
 static(int X,int Y)Position(AlbionPlaced x)
 {
  var half=x.Slot.Size switch{"Large"=>216,"Medium"=>160,_=>128};return Position(x.Asset,x.Rotation,x.Slot.X+half,x.Slot.Y+half);
 }
 static(int X,int Y)Position(AlbionAsset asset,byte rotation,int targetX,int targetY){var min=asset.Min(rotation);var size=asset.Size(rotation);var cx=min.X+size.X/2;var cy=min.Y+size.Y/2;var mw=rotation is 0 or 2?asset.W:asset.H;var mh=rotation is 0 or 2?asset.H:asset.W;cx+=cx>mw/2?-(cx%8):cx%8;cy+=cy>mh/2?-(cy%8):cy%8;return(targetX-cx,targetY-cy);}
 static(int X0,int Y0,int X1,int Y1)Bounds(AlbionAsset asset,byte rotation,(int X,int Y) position){var w=rotation is 0 or 2?asset.W:asset.H;var h=rotation is 0 or 2?asset.H:asset.W;return(position.X,position.Y,position.X+w,position.Y+h);}
 static void Expand(ref int minX,ref int minY,ref int maxX,ref int maxY,(int X0,int Y0,int X1,int Y1) bounds){minX=Math.Min(minX,bounds.X0);minY=Math.Min(minY,bounds.Y0);maxX=Math.Max(maxX,bounds.X1);maxY=Math.Max(maxY,bounds.Y1);}
 static byte StarterRotation(MapProfile profile,AlbionSlot slot,AlbionAsset asset,IReadOnlyList<AlbionSlot> starters)
 {
  // Same rule as Latium: the starting island faces the middle of the map (Albion's template is 2048 wide).
  var target=Math.Atan2(1024-(slot.Y+216),1024-(slot.X+216));if(target<0)target+=2*Math.PI;var best=0;var distance=double.MaxValue;
  for(var rotation=0;rotation<4;rotation++){var delta=Math.Abs((asset.StartCoastDirection+rotation*Math.PI/2-target)%(2*Math.PI));delta=Math.Min(delta,2*Math.PI-delta);if(delta<distance){distance=delta;best=rotation;}}
  return(byte)best;
 }
 static uint Rule(List<uint> rules,int type){for(var i=0;i<rules.Count;i++){var value=rules[i];if(type==1?value==8174:value is 8179 or 8181){rules.RemoveAt(i);rules.Add(value);return value;}}throw new InvalidOperationException();}
 static uint[] Assign(AlbionAsset island,uint[] definitions,Dictionary<uint,List<uint>> bags,Rng r)
 {
  var result=new List<uint>();foreach(var definition in definitions){if(!Pools.TryGetValue(definition,out var source)){result.Add(definition);continue;}var rejected=0;var fresh=false;while(true){if(!bags.TryGetValue(definition,out var bag))bags[definition]=bag=[];if(bag.Count==0){bag.AddRange(source);r.Shuffle(bag);fresh=true;rejected=0;}var value=bag[^1];bag.RemoveAt(bag.Count-1);if(!result.Contains(value)&&(island.HasMarsh||value is not(4082 or 2219))){result.Add(value);break;}if(bag.Count!=rejected){bag.Add(value);(bag[rejected],bag[^1])=(bag[^1],bag[rejected]);rejected++;continue;}if(!fresh){bag.Clear();continue;}throw new InvalidOperationException();}}return[..result];
 }
 static List<AlbionAsset>A(IEnumerable<string> names)=>names.Select(x=>Assets[x]).ToList();
 static List<AlbionAsset>AlbionPool(string size)=>size switch{"Large"=>A(Enumerable.Range(1,8).Select(i=>$"celtic_island_large_{i:00}")),"Medium"=>A(Enumerable.Range(1,7).Select(i=>$"celtic_island_medium_{i:00}")),"Small"=>A(Enumerable.Range(1,7).Select(i=>$"celtic_island_small_{i:00}")),_=>throw new InvalidOperationException($"Kein Albion-Inselpool für Größe {size}.")};
 static List<AlbionSlot>Slots(MapProfile profile)=>AlbionSlotsOf(profile).Select(slot=>new AlbionSlot(slot.Index,slot.X,slot.Y,slot.Size,slot.Type)).ToList();
 static IEnumerable<MapSlot> AlbionSlotsOf(MapProfile profile)=>profile.AlbionSlots;
 sealed record AlbionSlot(int Index,int X,int Y,string Size,int Type);sealed record AlbionPlaced(AlbionAsset Asset,AlbionSlot Slot,byte Rotation);sealed record AlbionSpecialPlaced(AlbionAsset Asset,byte Rotation,(int X,int Y) Position);sealed record AlbionCore(Rng Rng,List<AlbionPlaced> Placed,Dictionary<int,SiteCounts> SitesBySlot,int Width);
}

internal sealed record AlbionAsset(string Name,int W,int H,int X0,int Y0,int X1,int Y1,int MountainSlots,bool HasMarsh,double StartCoastDirection)
{
 public(int X,int Y)Min(byte r)=>r switch{0=>(X0,Y0),1=>(H-Y1,X0),2=>(W-X1,H-Y1),_=>(Y0,W-X1)};public(int X,int Y)Size(byte r)=>r is 0 or 2?(X1-X0,Y1-Y0):(Y1-Y0,X1-X0);
 public static readonly AlbionAsset[] All=[
 new("celtic_island_large_01",512,512,56,48,400,456,6,true,4.252961),new("celtic_island_large_02",512,512,72,56,424,464,6,true,5.445204),new("celtic_island_large_03",384,384,16,16,368,360,6,true,3.61409),new("celtic_island_large_04",512,512,64,56,400,448,6,true,1.892547),new("celtic_island_large_05",512,512,80,80,440,488,6,true,5.479608),new("celtic_island_large_06",384,384,0,0,376,384,7,true,4.957368),new("celtic_island_large_07",320,320,0,0,304,320,6,true,.9212469),new("celtic_island_large_08",384,384,16,16,384,368,6,true,4.073754),
 new("celtic_island_medium_01",256,256,16,24,224,232,3,true,0),new("celtic_island_medium_02",256,256,0,0,256,256,4,true,0),new("celtic_island_medium_03",256,256,0,8,248,256,3,true,0),new("celtic_island_medium_04",256,256,24,16,256,256,4,true,0),new("celtic_island_medium_05",320,320,8,32,304,320,4,true,0),new("celtic_island_medium_06",320,320,8,24,312,296,4,true,0),new("celtic_island_medium_07",256,256,24,8,232,256,4,true,0),
 new("celtic_island_small_01",256,256,0,0,256,256,0,false,0),new("celtic_island_small_02",256,256,24,16,256,232,2,false,0),new("celtic_island_small_03",256,256,40,8,224,248,1,false,0),new("celtic_island_small_04",256,256,16,16,240,232,0,false,0),new("celtic_island_small_05",256,256,40,0,216,232,2,true,0),new("celtic_island_small_06",192,192,32,16,184,144,0,true,0),new("celtic_island_small_07",192,192,0,0,192,192,1,false,0),
 new("celtic_island_3rdparty_pirate_01",320,320,24,0,312,320,0,false,0),new("celtic_island_3rdparty_trader_01",192,192,0,0,192,192,0,false,0),new("celtic_island_3rdparty_trader_02",192,192,0,0,192,192,0,false,0)];
}

internal static class SiteActivation
{
 sealed record Definition(int RandomMountain,int RandomRiver,int MountainMinimum,int MountainVariants,int RiverMinimum,int RiverVariants,int Marsh);

 static readonly Dictionary<string,Definition> Latium=new(StringComparer.OrdinalIgnoreCase)
 {
  ["roman_dlc01_island_medium_01"]=new(3,8,5,2,7,2,0),["roman_dlc01_island_medium_02"]=new(3,0,5,2,0,1,0),["roman_dlc01_island_medium_03"]=new(2,3,4,1,3,1,0),
  ["roman_dlc01_island_small_02"]=new(2,0,3,1,0,1,0),["roman_dlc_01_island_small_01"]=new(1,1,2,1,1,1,0),["roman_dlc01_island_continental_01"]=new(19,30,17,3,21,3,0),
  ["roman_island_extralarge_01"]=new(13,13,7,3,9,3,0),["roman_island_extralarge_02"]=new(6,13,7,3,9,3,0),["roman_island_extralarge_03"]=new(5,0,7,3,13,1,0),["roman_island_extralarge_04"]=new(5,0,7,3,13,1,0),
  ["roman_island_large_01"]=new(4,12,6,3,8,3,0),["roman_island_large_02"]=new(4,12,6,3,8,3,0),["roman_island_large_03"]=new(4,12,6,3,8,3,0),["roman_island_large_04"]=new(4,12,6,3,8,3,0),
  ["roman_island_large_05"]=new(4,12,6,3,8,3,0),["roman_island_large_06"]=new(5,12,6,3,8,3,0),["roman_island_large_07"]=new(7,10,6,3,8,3,0),["roman_island_large_09"]=new(7,0,6,3,12,1,0),
  ["roman_island_medium_01"]=new(4,4,5,3,4,1,0),["roman_island_medium_02"]=new(4,5,5,3,5,1,0),["roman_island_medium_03"]=new(3,2,5,2,2,1,0),["roman_island_medium_04"]=new(6,7,5,3,7,1,0),
  ["roman_island_medium_05"]=new(4,4,5,3,4,1,0),["roman_island_medium_06"]=new(2,2,5,1,2,1,0),["roman_island_medium_07"]=new(3,0,5,2,0,1,0),["roman_island_medium_08"]=new(5,5,5,3,5,1,0),
  ["roman_island_small_01"]=new(1,0,3,1,0,1,0),["roman_island_small_02"]=new(4,0,4,2,0,1,0),["roman_island_small_03"]=new(2,0,3,1,0,1,0),["roman_island_small_04"]=new(2,0,3,1,0,1,0),
  ["roman_island_small_05"]=new(3,0,4,1,0,1,0),["roman_island_small_06"]=new(3,0,4,1,0,1,0),["roman_island_small_07"]=new(3,1,4,1,2,1,0)
 };

 static readonly Dictionary<string,Definition> Albion=new(StringComparer.OrdinalIgnoreCase)
 {
  ["celtic_island_large_01"]=new(6,0,8,3,0,1,6),["celtic_island_large_02"]=new(6,0,8,3,0,1,6),["celtic_island_large_03"]=new(6,0,8,3,0,1,6),["celtic_island_large_04"]=new(6,0,8,3,0,1,6),
  ["celtic_island_large_05"]=new(6,0,8,3,0,1,8),["celtic_island_large_06"]=new(7,0,8,3,0,1,5),["celtic_island_large_07"]=new(6,0,8,3,0,1,6),["celtic_island_large_08"]=new(6,0,8,3,0,1,7),
  ["celtic_island_medium_01"]=new(3,0,6,1,0,1,5),["celtic_island_medium_02"]=new(4,0,7,1,0,1,3),["celtic_island_medium_03"]=new(3,0,6,1,0,1,5),["celtic_island_medium_04"]=new(4,0,7,2,0,1,2),
  ["celtic_island_medium_05"]=new(4,0,7,1,0,1,3),["celtic_island_medium_06"]=new(4,0,7,1,0,1,3),["celtic_island_medium_07"]=new(4,0,7,1,0,1,3),
  ["celtic_island_small_01"]=new(0,0,4,1,0,1,0),["celtic_island_small_02"]=new(2,0,4,1,0,1,0),["celtic_island_small_03"]=new(1,0,3,1,0,1,0),["celtic_island_small_04"]=new(0,0,2,1,0,1,0),
  ["celtic_island_small_05"]=new(2,0,3,1,0,1,1),["celtic_island_small_06"]=new(0,0,1,1,0,1,2),["celtic_island_small_07"]=new(1,0,2,1,0,1,0)
 };


 // Slot counts come from MapGeneratorSlotCount in assets.xml (slot assets 2882 Roman Mountain, 2969 Roman River, 5281 Celtic
 // Mountain; 5282 Celtic Marsh has no RandomMapObject and is therefore always the island's fixed number). The rows Low/Medium/High
 // are the game's sparse/regular/abundant options; the values below are each row's minimum per island size, and every row spans
 // three values except Roman Mountain / Small / Low, which is 2..3.
 // An island offers a number of fixed slots plus a number of random ones (FixedSlots and Definition.Random*, read from the island
 // files). The fixed ones are handed out first and the rest is filled up until the requested number is reached, so the result is
 // max(fixed, requested) capped by fixed + random.
 static readonly Dictionary<(bool Albion,bool River,string Size),(int Low,int Medium,int High)> SlotMinimums=new()
 {
  [(false,false,"Small")]=(2,3,4),[(false,false,"Medium")]=(3,4,5),[(false,false,"Large")]=(4,5,6),[(false,false,"XL")]=(5,6,7),[(false,false,"Continental")]=(12,16,17),
  [(false,true,"Small")]=(4,5,6),[(false,true,"Medium")]=(5,6,7),[(false,true,"Large")]=(6,7,8),[(false,true,"XL")]=(7,8,9),[(false,true,"Continental")]=(17,19,21),
  [(true,false,"Small")]=(4,5,6),[(true,false,"Medium")]=(5,6,7),[(true,false,"Large")]=(6,7,8),[(true,false,"XL")]=(7,8,9)
 };
 static readonly Dictionary<string,(int Mountain,int River)> FixedSlots=new(StringComparer.OrdinalIgnoreCase)
 {
  ["celtic_island_large_01"]=(6,0),["celtic_island_large_02"]=(6,0),["celtic_island_large_03"]=(6,0),["celtic_island_large_04"]=(6,0),
  ["celtic_island_large_05"]=(6,0),["celtic_island_large_06"]=(6,0),["celtic_island_large_07"]=(6,0),["celtic_island_large_08"]=(6,0),
  ["celtic_island_medium_01"]=(3,0),["celtic_island_medium_02"]=(3,0),["celtic_island_medium_03"]=(3,0),["celtic_island_medium_04"]=(4,0),
  ["celtic_island_medium_05"]=(3,0),["celtic_island_medium_06"]=(3,0),["celtic_island_medium_07"]=(3,0),["celtic_island_small_01"]=(4,0),
  ["celtic_island_small_02"]=(2,0),["celtic_island_small_03"]=(2,0),["celtic_island_small_04"]=(2,0),["celtic_island_small_05"]=(1,0),
  ["celtic_island_small_06"]=(1,0),["celtic_island_small_07"]=(1,0),["roman_dlc01_island_continental_01"]=(12,4),["roman_dlc01_island_medium_01"]=(3,0),
  ["roman_dlc01_island_medium_02"]=(3,0),["roman_dlc01_island_medium_03"]=(2,0),["roman_dlc01_island_small_02"]=(1,0),["roman_dlc_01_island_small_01"]=(1,0),
  ["roman_island_extralarge_01"]=(5,0),["roman_island_extralarge_02"]=(6,0),["roman_island_extralarge_03"]=(6,13),["roman_island_extralarge_04"]=(5,13),
  ["roman_island_large_01"]=(5,0),["roman_island_large_02"]=(5,0),["roman_island_large_03"]=(5,0),["roman_island_large_04"]=(5,0),
  ["roman_island_large_05"]=(5,0),["roman_island_large_06"]=(5,0),["roman_island_large_07"]=(4,1),["roman_island_large_09"]=(5,12),
  ["roman_island_medium_01"]=(3,0),["roman_island_medium_02"]=(3,0),["roman_island_medium_03"]=(3,0),["roman_island_medium_04"]=(3,0),
  ["roman_island_medium_05"]=(3,0),["roman_island_medium_06"]=(3,0),["roman_island_medium_07"]=(3,0),["roman_island_medium_08"]=(3,0),
  ["roman_island_small_01"]=(2,0),["roman_island_small_02"]=(1,0),["roman_island_small_03"]=(1,0),["roman_island_small_04"]=(1,0),
  ["roman_island_small_05"]=(1,0),["roman_island_small_06"]=(1,0),["roman_island_small_07"]=(1,1)
 };

 static string SizeOf(string name)
 {
  var n=name.ToLowerInvariant();
  return n.Contains("continental")?"Continental":n.Contains("extralarge")?"XL":n.Contains("large")?"Large":n.Contains("medium")?"Medium":"Small";
 }
 static(int Minimum,int Span)Requested(bool albion,bool river,string size,SlotSetting slots)
 {
  if(!SlotMinimums.TryGetValue((albion,river,size),out var rows))return(0,1);
  var minimum=slots switch{SlotSetting.Sparse=>rows.Low,SlotSetting.Regular=>rows.Medium,_=>rows.High};
  var span=!albion&&!river&&size=="Small"&&slots==SlotSetting.Sparse?2:3;
  return(minimum,span);
 }
 static SiteCounts Generate(Rng rng,string name,Definition definition,bool albion,SlotSetting slots)
 {
  var size=SizeOf(name);var fixedSlots=FixedSlots.TryGetValue(name,out var f)?f:(Mountain:0,River:0);
  var mountain=Activate(rng,definition.RandomMountain,fixedSlots.Mountain,Requested(albion,false,size,slots));
  var river=Activate(rng,definition.RandomRiver,fixedSlots.River,Requested(albion,true,size,slots));
  return new(mountain,river,definition.Marsh);
 }
 static int Activate(Rng rng,int random,int fixedSlots,(int Minimum,int Span) requested)
 {
  if(random==0)return fixedSlots;
  var remaining=random;var wanted=requested.Minimum;
  // The variant is drawn uniformly over the row's own span; the uneven split some islands show comes from clamping when one end of
  // the range is cut off by the fixed slots or the capacity.
  if(requested.Span>1){wanted+=(int)rng.Scaled((uint)requested.Span);remaining--;}
  rng.Advance(remaining);
  return Math.Min(Math.Max(fixedSlots,wanted),fixedSlots+random);
 }
 public static SiteCounts GenerateLatium(Rng rng,Asset asset,SlotSetting slots=SlotSetting.Abundant)=>Generate(rng,asset.Name,Latium[asset.Name],false,slots);
 public static SiteCounts GenerateAlbion(Rng rng,AlbionAsset asset,SlotSetting slots=SlotSetting.Abundant)=>Generate(rng,asset.Name,Albion[asset.Name],true,slots);

}

internal enum SlotSetting{Abundant,Regular,Sparse}
internal readonly record struct SiteCounts(int Mountain,int River,int Marsh=0)
{
 public static SiteCounts operator +(SiteCounts left,SiteCounts right)=>new(left.Mountain+right.Mountain,left.River+right.River,left.Marsh+right.Marsh);
}
internal sealed record GeneratedIsland(string Name,int SlotIndex,string Size,uint FertilitySet,uint[] Fertilities,SiteCounts Sites);
internal readonly record struct IslandArea(int Total,int Swamp=0,int Harbour=0);
internal static class IslandAreas
{
 static readonly Dictionary<string,IslandArea> Values=new(StringComparer.OrdinalIgnoreCase)
 {
  ["roman_island_extralarge_01"]=new(37009,0,6800),["roman_island_extralarge_02"]=new(38861,0,6310),["roman_island_extralarge_03"]=new(37806,0,7261),["roman_island_extralarge_04"]=new(41686,0,8142),
  ["roman_island_large_01"]=new(31573,0,7904),["roman_island_large_02"]=new(28392,0,5444),["roman_island_large_03"]=new(32617,0,4877),["roman_island_large_04"]=new(27653,0,5256),["roman_island_large_05"]=new(28377,0,5762),["roman_island_large_06"]=new(30314,0,4856),["roman_island_large_07"]=new(30792,0,5417),["roman_island_large_09"]=new(30089,0,4534),
  ["roman_island_medium_01"]=new(13369,0,7297),["roman_island_medium_02"]=new(12644,0,5368),["roman_island_medium_03"]=new(11332,0,2363),["roman_island_medium_04"]=new(11951,0,4143),["roman_island_medium_05"]=new(12807,0,4397),["roman_island_medium_06"]=new(11682,0,3500),["roman_island_medium_07"]=new(13458,0,3868),["roman_island_medium_08"]=new(17113,0,3251),
  ["roman_island_small_01"]=new(5019,0,1144),["roman_island_small_02"]=new(4145,0,1827),["roman_island_small_03"]=new(5286,0,2952),["roman_island_small_04"]=new(6816,0,2223),["roman_island_small_05"]=new(5622,0,4186),["roman_island_small_06"]=new(6346,0,2730),["roman_island_small_07"]=new(5860,0,3118),
  // Build-area tiles (total, of which swamp) per island from the island atlas, and harbour-area tiles from the game's island files (all 55 islands, base and DLC01).
  ["roman_dlc01_island_continental_01"]=new(101254,0,8258),["roman_dlc01_island_medium_01"]=new(11497,0,5403),["roman_dlc01_island_medium_02"]=new(11957,0,2803),["roman_dlc01_island_medium_03"]=new(14077,0,3576),["roman_dlc01_island_small_02"]=new(5914,0,3536),["roman_dlc_01_island_small_01"]=new(3863,0,958),
  ["celtic_island_large_01"]=new(22032,7144,2980),["celtic_island_large_02"]=new(22563,10182,4848),["celtic_island_large_03"]=new(23232,8834,4156),["celtic_island_large_04"]=new(20794,8676,3001),["celtic_island_large_05"]=new(22457,10939,3744),["celtic_island_large_06"]=new(22865,9224,3292),["celtic_island_large_07"]=new(20826,8757,3198),["celtic_island_large_08"]=new(20721,8279,5022),
  ["celtic_island_medium_01"]=new(8869,6695,2050),["celtic_island_medium_02"]=new(8881,2301,3657),["celtic_island_medium_03"]=new(14265,5912,2674),["celtic_island_medium_04"]=new(9850,2413,3581),["celtic_island_medium_05"]=new(9946,3187,3143),["celtic_island_medium_06"]=new(9634,2837,2449),["celtic_island_medium_07"]=new(10970,3300,2831),
  ["celtic_island_small_01"]=new(3498,0,2246),["celtic_island_small_02"]=new(4352,0,1725),["celtic_island_small_03"]=new(4216,0,2193),["celtic_island_small_04"]=new(3064,0,2509),["celtic_island_small_05"]=new(3239,1348,1245),["celtic_island_small_06"]=new(2174,1646,1911),["celtic_island_small_07"]=new(3616,0,3181)
 };
 public static IslandArea Get(string name)=>Values.TryGetValue(name,out var value)?value:throw new InvalidOperationException($"Für {name} fehlt die Bauflächenangabe.");
}
internal readonly record struct RegionMetrics(SiteCounts Sites,int GoldMineSites,int GoldSites,int SturgeonSites,int BeaverSites,int SmallBirdSites,int BuildableTiles,int SwampTiles,int MineralMineSites=0,int CopperMineSites=0,int SilverMineSites=0,int MarbleSites=0,int TinMineSites=0,
 int HarbourMurexTiles=0,int HarbourOysterTiles=0,int HarbourSaltwortTiles=0,int HarbourSeaShellTiles=0,int MarshSmallBirdTiles=0,int MarshBeaverTiles=0)
{
 public int Tiles(AdvancedFilter filter)=>filter switch
 {
  AdvancedFilter.LatiumHarbourMurex=>HarbourMurexTiles,AdvancedFilter.LatiumHarbourOysters=>HarbourOysterTiles,AdvancedFilter.AlbionHarbourSaltwort=>HarbourSaltwortTiles,
  AdvancedFilter.AlbionHarbourSeaShells=>HarbourSeaShellTiles,AdvancedFilter.AlbionMarshSmallBirds=>MarshSmallBirdTiles,_=>MarshBeaverTiles
 };
 public static RegionMetrics Calculate(IEnumerable<GeneratedIsland> islands)
 {
  var sites=new SiteCounts(0,0);var goldMines=0;var gold=0;var sturgeon=0;var beaver=0;var smallBirds=0;var buildable=0;var swamp=0;var minerals=0;var copper=0;var silver=0;var marble=0;var tin=0;var murex=0;var oysters=0;var saltwort=0;var seaShells=0;var smallBirdMarsh=0;var beaverMarsh=0;
  foreach(var island in islands)
  {
   sites+=island.Sites;
   var area=IslandAreas.Get(island.Name);buildable+=area.Total;swamp+=area.Swamp;
   if(island.Fertilities.Contains(32027u)){goldMines+=island.Sites.Mountain;gold+=island.Sites.River;}
   if(island.Fertilities.Contains(8577u))sturgeon+=island.Sites.River;
   if(island.Fertilities.Contains(4082u))beaver+=island.Sites.Marsh;
   if(island.Fertilities.Contains(2219u))smallBirds+=island.Sites.Marsh;
   if(island.Fertilities.Contains(4053u))minerals+=island.Sites.Mountain;
   if(island.Fertilities.Contains(4063u))copper+=island.Sites.Mountain;
   if(island.Fertilities.Contains(8487u))silver+=island.Sites.Mountain;
   if(island.Fertilities.Contains(4062u))marble+=island.Sites.Mountain;
   if(island.Fertilities.Contains(4064u))tin+=island.Sites.Mountain;
   // Harbour and marsh tiles of the islands that carry the fertility (see AdvancedFilters).
   if(island.Fertilities.Contains(4051u))murex+=area.Harbour;
   if(island.Fertilities.Contains(2208u))oysters+=area.Harbour;
   if(island.Fertilities.Contains(2218u))saltwort+=area.Harbour;
   if(island.Fertilities.Contains(8432u))seaShells+=area.Harbour;
   if(island.Fertilities.Contains(2219u))smallBirdMarsh+=area.Swamp;
   if(island.Fertilities.Contains(4082u))beaverMarsh+=area.Swamp;
  }
  return new(sites,goldMines,gold,sturgeon,beaver,smallBirds,buildable,swamp,minerals,copper,silver,marble,tin,murex,oysters,saltwort,seaShells,smallBirdMarsh,beaverMarsh);
 }
}
internal static class SiteRangeAnalyzer
{
 public static string[] Analyze(MapSizeKind size,int seeds,bool dlc01=true)
 {
  var rows=new List<string>();foreach(var template in Enum.GetValues<MapTemplateKind>())
  {
   var profile=MapProfiles.Get(template,size,dlc01);var gold=new int[seeds];var sturgeon=new int[seeds];var beaver=new int[seeds];var birds=new int[seeds];var latiumMountain=new int[seeds];var latiumRiver=new int[seeds];var albionMountain=new int[seeds];var latiumArea=new int[seeds];var albionArea=new int[seeds];var albionSwamp=new int[seeds];
   Parallel.For(0,seeds,()=>new GeneratorScratch(),(index,_,scratch)=>{var seed=(uint)(index+1);var latium=Generator.GenerateLatium(seed,scratch,profile).Metrics;var albion=RegionMetrics.Calculate(AlbionGenerator.Generate(seed,profile));gold[index]=latium.GoldSites;sturgeon[index]=latium.SturgeonSites;beaver[index]=albion.BeaverSites;birds[index]=albion.SmallBirdSites;latiumMountain[index]=latium.Sites.Mountain;latiumRiver[index]=latium.Sites.River;albionMountain[index]=albion.Sites.Mountain;latiumArea[index]=latium.BuildableTiles;albionArea[index]=albion.BuildableTiles;albionSwamp[index]=albion.SwampTiles;return scratch;},_=>{});
   rows.Add($"{template}|Gold={Stats(gold)}|Sturgeon={Stats(sturgeon)}|Beaver={Stats(beaver)}|SmallBirds={Stats(birds)}|LatiumMountain={Stats(latiumMountain)}|LatiumRiver={Stats(latiumRiver)}|AlbionMountain={Stats(albionMountain)}|LatiumArea={Stats(latiumArea)}|AlbionArea={Stats(albionArea)}|AlbionSwamp={Stats(albionSwamp)}");
  }
  return[..rows];
 }
 static string Stats(int[] values){Array.Sort(values);var average=values.Average();var middle=values.Length/2;var median=values.Length%2==0?(values[middle-1]+values[middle])/2d:values[middle];return $"{values[0]}-{values[^1]},avg={average:F2},med={median:F1}";}
}
internal sealed record AggregateSiteRange(int GoldMin,int GoldMax,int SturgeonMin,int SturgeonMax,int BeaverMin,int BeaverMax,int SmallBirdMin,int SmallBirdMax,double GoldAverage,double GoldMedian,double SturgeonAverage,double SturgeonMedian,double BeaverAverage,double BeaverMedian,double SmallBirdAverage,double SmallBirdMedian,
 int LatiumMountainMin=0,int LatiumMountainMax=0,int LatiumRiverMin=0,int LatiumRiverMax=0,int AlbionMountainMin=0,int AlbionMountainMax=0,double LatiumMountainAverage=0,double LatiumMountainMedian=0,double LatiumRiverAverage=0,double LatiumRiverMedian=0,double AlbionMountainAverage=0,double AlbionMountainMedian=0,
 int LatiumAreaMin=0,int LatiumAreaMax=0,int AlbionAreaMin=0,int AlbionAreaMax=0,int AlbionSwampMin=0,int AlbionSwampMax=0,int LatiumAreaAverage=0,int LatiumAreaMedian=0,int AlbionAreaAverage=0,int AlbionAreaMedian=0,int AlbionSwampAverage=0,int AlbionSwampMedian=0,
 int MineralMin=0,int MineralMax=0,double MineralAverage=0,double MineralMedian=0,int CopperMin=0,int CopperMax=0,double CopperAverage=0,double CopperMedian=0,int SilverMin=0,int SilverMax=0,double SilverAverage=0,double SilverMedian=0,
 int MarbleMin=0,int MarbleMax=0,double MarbleAverage=0,double MarbleMedian=0,
 int GoldMineMin=0,int GoldMineMax=0,double GoldMineAverage=0,double GoldMineMedian=0,
 int TinMin=0,int TinMax=0,double TinAverage=0,double TinMedian=0);
internal static class AggregateSiteRanges
{
 public static AggregateSiteRange For(MapProfile profile)
 {
  var range=profile.Dlc01?Poa(profile):Vanilla(profile);
  return TinRanges.TryGetValue((profile.Template,profile.Size),out var tin)?range with{TinMin=tin.Min,TinMax=tin.Max,TinAverage=tin.Average,TinMedian=tin.Median}:range;
 }
 // Albion tin-mine sites (mountain sites on islands with tin), seeds 1..N per template and size (--analyze-tin). Same for both DLC states.
 static readonly Dictionary<(MapTemplateKind,MapSizeKind),(int Min,int Max,double Average,double Median)> TinRanges=new()
 {
[(MapTemplateKind.Archipelago,MapSizeKind.Large)]=(29,55,41.15,41),
  [(MapTemplateKind.Atoll,MapSizeKind.Large)]=(29,54,41.13,41),
  [(MapTemplateKind.Rift,MapSizeKind.Large)]=(15,55,32.94,33),
  [(MapTemplateKind.Corners,MapSizeKind.Large)]=(20,55,37.04,37),
  [(MapTemplateKind.IslandChains,MapSizeKind.Large)]=(20,55,37.05,37),
  [(MapTemplateKind.Archipelago,MapSizeKind.Medium)]=(20,49,34.85,35),
  [(MapTemplateKind.Atoll,MapSizeKind.Medium)]=(20,49,34.85,35),
  [(MapTemplateKind.Rift,MapSizeKind.Medium)]=(15,45,29.66,30),
  [(MapTemplateKind.Corners,MapSizeKind.Medium)]=(20,44,32.67,33),
  [(MapTemplateKind.IslandChains,MapSizeKind.Medium)]=(15,49,30.77,31),
  [(MapTemplateKind.Archipelago,MapSizeKind.Small)]=(15,40,26.53,27),
  [(MapTemplateKind.Atoll,MapSizeKind.Small)]=(15,40,26.53,27),
  [(MapTemplateKind.Rift,MapSizeKind.Small)]=(12,37,24.47,24),
  [(MapTemplateKind.Corners,MapSizeKind.Small)]=(12,37,24.48,24),
  [(MapTemplateKind.IslandChains,MapSizeKind.Small)]=(12,37,24.47,24),
 };
 static AggregateSiteRange Poa(MapProfile profile)=>(profile.Template,profile.Size) switch
 {
  (MapTemplateKind.Archipelago,MapSizeKind.Large)=>new(33,116,32,115,11,39,10,39,78.40,79,78.43,80,25.15,25,25.14,25,135,163,124,155,108,128,148.43,148,140.14,140,118.30,118,511,535,238,247,91,99,524,525,244,244,95,96,35,68,49.22,49,31,59,45.43,46,48,79,63.42,64,51,86,67.23,67,29,89,66.23,67),(MapTemplateKind.Atoll,MapSizeKind.Large)=>new(35,116,31,117,10,39,10,39,79.32,80,79.32,80,25.12,25,25.13,25,139,166,123,156,108,129,152.10,152,140.41,141,118.30,118,517,540,238,247,91,99,530,530,244,244,95,96,38,69,51.06,51,31,60,45.44,46,48,78,63.43,64,55,86,69.05,69,31,92,67.25,68),(MapTemplateKind.Rift,MapSizeKind.Large)=>new(33,114,33,116,6,39,6,39,79.27,80,79.32,80,22.79,23,22.71,23,139,166,124,156,92,112,152.10,152,140.42,141,101.90,102,517,540,213,222,78,87,530,530,216,216,82,82,38,66,51.04,51,24,59,39.99,40,40,77,57.95,58,54,87,69.07,69,34,92,67.22,68),(MapTemplateKind.Corners,MapSizeKind.Large)=>new(40,117,39,119,7,39,8,39,85.28,86,85.30,86,24.11,24,24.11,24,132,158,139,173,100,121,145.34,145,156.14,156,110.10,110,566,579,224,236,82,94,572,572,230,231,88,88,33,64,47.65,48,24,58,42.72,43,41,78,60.71,61,51,84,65.70,66,33,90,65.86,67),(MapTemplateKind.IslandChains,MapSizeKind.Large)=>new(30,113,32,114,8,39,6,39,76.68,78,76.66,78,24.10,24,24.11,24,128,154,115,151,99,122,140.52,141,133.17,133,110.10,110,491,516,224,236,82,94,504,504,230,231,88,88,32,61,45.26,45,24,59,42.68,43,41,79,60.71,61,50,78,63.27,63,30,87,63.41,64),
  (MapTemplateKind.Archipelago,MapSizeKind.Medium)=>new(28,101,27,97,7,37,8,37,68.90,71,68.87,71,21.90,22,21.89,22,130,155,100,121,96,116,143.02,143,110.72,111,105.72,106,444,452,199,213,70,83,447,447,207,207,78,78,35,62,46.52,46,24,55,41.22,41,41,74,59.27,59,52,79,64.53,64,25,91,62.43,63),(MapTemplateKind.Atoll,MapSizeKind.Medium)=>new(28,107,31,115,8,37,8,37,73.63,75,73.64,75,21.88,22,21.93,22,133,158,109,136,97,115,145.42,145,122.23,122,105.73,106,472,488,199,213,70,83,481,481,207,207,78,78,35,64,47.70,48,24,55,41.25,41,42,75,59.22,59,53,83,65.70,66,28,93,63.79,65),(MapTemplateKind.Rift,MapSizeKind.Medium)=>new(26,108,26,107,6,34,5,34,72.14,73,72.27,73,19.20,19,19.17,19,131,157,108,143,88,104,143.87,144,125.22,125,95.33,95,473,485,173,189,57,72,480,480,182,182,66,66,36,65,48.91,49,24,52,37.77,38,40,71,55.80,56,53,83,66.95,67,27,89,62.28,63),(MapTemplateKind.Corners,MapSizeKind.Medium)=>new(32,115,31,116,8,33,8,32,76.74,78,76.71,78,19.70,20,19.69,20,128,154,115,152,93,110,140.52,141,133.15,133,101.33,101,491,516,177,193,61,75,504,504,184,184,68,68,32,61,45.26,45,25,50,39.76,40,41,69,57.78,58,49,80,63.26,63,30,89,63.43,64),(MapTemplateKind.IslandChains,MapSizeKind.Medium)=>new(24,104,23,105,6,37,6,37,71.13,72,71.16,72,20.37,20,20.38,20,123,146,99,131,89,107,133.87,134,114.98,115,97.52,98,445,465,186,200,63,78,455,455,193,193,71,71,31,57,41.93,42,24,55,38.51,38,40,75,56.50,56,48,75,59.91,60,26,85,60.04,61),
  (MapTemplateKind.Archipelago,MapSizeKind.Small)=>new(22,86,22,88,5,31,5,31,60.09,62,60.09,62,17.07,17,17.07,17,122,144,76,109,84,95,132.63,133,92.20,92,89.05,89,374,403,155,169,49,64,390,390,163,164,58,58,32,57,43.32,43,24,49,35.68,36,40,68,53.69,54,49,78,61.32,61,19,81,55.55,57),(MapTemplateKind.Atoll,MapSizeKind.Small)=>new(23,90,24,91,6,31,5,31,61.92,64,62.01,64,17.06,17,17.06,17,125,149,88,110,84,95,136.89,137,98.88,99,89.04,89,396,412,155,169,49,64,404,404,163,164,58,58,34,59,45.46,45,24,49,35.67,36,40,69,53.67,54,51,79,63.45,63,23,83,58.30,59),(MapTemplateKind.Rift,MapSizeKind.Small)=>new(23,99,20,101,5,30,5,31,66.08,67,66.12,67,16.02,16,16.04,16,126,149,89,126,78,92,137.19,137,107.05,107,84.96,85,418,442,147,164,46,62,431,431,156,156,54,54,33,62,45.57,45,22,49,34.32,34,39,68,52.34,52,50,81,63.60,64,23,85,58.48,59),(MapTemplateKind.Corners,MapSizeKind.Small)=>new(26,102,26,104,5,31,5,30,68.06,69,68.08,69,16.04,16,16.01,16,127,151,104,128,78,92,139.22,139,116.71,117,84.95,85,437,455,147,164,46,62,447,447,156,156,54,54,32,63,46.59,47,22,49,34.31,34,39,68,52.32,52,48,82,64.61,65,28,84,60.93,62),(MapTemplateKind.IslandChains,MapSizeKind.Small)=>new(21,90,21,91,5,31,5,30,60.15,62,60.17,62,16.04,16,16.01,16,117,139,81,108,78,92,127.43,127,94.99,95,84.95,85,375,394,147,164,46,62,386,386,156,156,54,54,30,55,40.71,41,22,49,34.31,34,39,67,52.30,52,47,76,58.73,59,18,80,55.72,57),
  _=>throw new InvalidOperationException("Für das Kartenprofil fehlen die analysierten Bauplatzbereiche.")
 };
 static AggregateSiteRange Vanilla(MapProfile profile)=>(profile.Template,profile.Size) switch
 {
  (MapTemplateKind.Archipelago,MapSizeKind.Large)=>new(21,84,22,85,11,39,10,39,53.71,54,53.65,54,25.15,25,25.14,25,94,117,85,113,108,128,105.17,105,99.72,100,118.30,118,349,372,238,247,91,99,360,360,244,244,95,96,26,51,36.58,36,31,59,45.43,46,48,79,63.42,64,26,50,36.59,37,18,63,43.51,44),
  (MapTemplateKind.Atoll,MapSizeKind.Large)=>new(24,83,24,84,10,39,10,39,54.99,55,55.01,55,25.12,25,25.13,25,97,122,93,119,108,129,109.42,109,106.40,106,118.30,118,363,386,238,247,91,99,375,376,244,244,95,96,28,53,38.70,39,31,60,45.44,46,48,78,63.43,64,28,51,38.72,39,19,64,45.80,46),
  (MapTemplateKind.Rift,MapSizeKind.Large)=>new(22,84,24,85,6,39,6,39,54.98,55,54.98,55,22.79,23,22.71,23,97,123,93,119,92,112,109.42,109,106.41,106,101.90,102,363,386,213,222,78,87,375,376,216,216,82,82,28,51,38.71,39,24,59,39.99,40,40,77,57.95,58,28,52,38.71,39,24,64,45.79,46),
  (MapTemplateKind.Corners,MapSizeKind.Large)=>new(27,82,29,87,7,39,8,39,61.14,62,61.11,62,24.11,24,24.11,24,91,115,112,132,100,121,102.66,103,122.15,122,110.10,110,415,419,224,236,82,94,417,417,230,231,88,88,25,48,35.35,35,24,58,42.72,43,41,78,60.71,61,25,48,35.35,35,20,59,44.63,45),
  (MapTemplateKind.IslandChains,MapSizeKind.Large)=>new(19,78,24,79,8,39,6,39,52.44,53,52.48,53,24.10,24,24.11,24,87,110,84,113,99,122,97.82,98,99.14,99,110.10,110,337,362,224,236,82,94,349,349,230,231,88,88,23,45,32.93,33,24,59,42.68,43,41,79,60.71,61,23,46,32.92,33,16,59,42.08,42),
  (MapTemplateKind.Archipelago,MapSizeKind.Medium)=>new(20,62,20,62,7,37,8,37,44.11,45,44.12,45,21.90,22,21.89,22,90,111,73,79,96,116,100.33,100,76.72,77,105.72,106,291,294,199,213,70,83,293,293,207,207,78,78,26,46,34.17,34,24,55,41.22,41,41,74,59.27,59,26,45,34.17,34,15,59,40.60,41),
  (MapTemplateKind.Atoll,MapSizeKind.Medium)=>new(22,75,21,75,8,37,8,37,48.94,49,48.90,49,21.88,22,21.93,22,92,113,77,99,97,115,102.76,103,88.21,88,105.73,106,317,335,199,213,70,83,327,327,207,207,78,78,26,48,35.37,35,24,55,41.25,41,42,75,59.22,59,26,48,35.38,35,15,63,42.05,42),
  (MapTemplateKind.Rift,MapSizeKind.Medium)=>new(18,76,18,77,6,34,5,34,48.00,48,48.03,48,19.20,19,19.17,19,90,113,76,104,88,104,101.17,101,91.23,91,95.33,95,319,331,173,189,57,72,325,325,182,182,66,66,26,51,36.58,37,24,52,37.77,38,40,71,55.80,56,26,51,36.59,37,14,60,40.81,41),
  (MapTemplateKind.Corners,MapSizeKind.Medium)=>new(22,79,21,79,8,33,8,32,52.49,53,52.43,53,19.70,20,19.69,20,87,110,84,113,93,110,97.84,98,99.14,99,101.33,101,337,361,177,193,61,75,349,349,184,184,68,68,23,45,32.91,33,25,50,39.76,40,41,69,57.78,58,23,46,32.93,33,19,59,42.09,42),
  (MapTemplateKind.IslandChains,MapSizeKind.Medium)=>new(19,72,20,72,6,37,6,37,46.14,46,46.20,47,20.37,20,20.38,20,82,101,68,93,89,107,91.18,91,80.96,81,97.52,98,291,311,186,200,63,78,301,301,193,193,71,71,21,42,29.60,30,24,55,38.51,38,40,75,56.50,56,21,43,29.58,30,14,60,38.06,38),
  (MapTemplateKind.Archipelago,MapSizeKind.Small)=>new(16,55,16,54,5,31,5,31,34.82,35,34.82,35,17.07,17,17.07,17,80,100,46,73,84,95,89.96,90,58.21,58,89.05,89,222,247,155,169,49,64,235,235,163,164,58,58,23,42,30.98,31,24,49,35.68,36,40,68,53.69,54,23,41,30.97,31,12,55,33.15,33),
  (MapTemplateKind.Atoll,MapSizeKind.Small)=>new(18,55,18,57,6,31,5,31,37.32,38,37.32,38,17.06,17,17.06,17,85,104,56,73,84,95,94.21,94,64.87,65,89.04,89,240,257,155,169,49,64,250,250,163,164,58,58,25,44,33.09,33,24,49,35.67,36,40,69,53.67,54,25,44,33.10,33,12,57,36.49,37),
  (MapTemplateKind.Rift,MapSizeKind.Small)=>new(17,67,16,68,5,30,5,31,41.32,41,41.31,41,16.02,16,16.04,16,85,105,58,86,78,92,94.50,94,73.05,73,84.96,85,266,286,147,164,46,62,276,276,156,156,54,54,24,46,33.26,33,22,49,34.32,34,39,68,52.34,52,24,46,33.24,33,12,58,36.50,37),
  (MapTemplateKind.Corners,MapSizeKind.Small)=>new(19,64,19,65,5,31,5,30,42.28,42,42.32,42,16.04,16,16.01,16,81,104,65,91,78,92,91.40,91,79.14,79,84.95,85,269,289,147,164,46,62,280,280,156,156,54,54,23,44,31.71,32,22,49,34.31,34,39,68,52.32,52,23,43,31.71,32,15,55,38.06,38),
  (MapTemplateKind.IslandChains,MapSizeKind.Small)=>new(16,53,16,53,5,31,5,30,35.56,36,35.56,36,16.04,16,16.01,16,75,95,49,71,78,92,84.76,85,60.97,61,84.95,85,220,241,147,164,46,62,231,231,156,156,54,54,21,39,28.37,28,22,49,34.31,34,39,67,52.30,52,21,39,28.39,28,13,53,33.93,34),
  _=>throw new InvalidOperationException("Für das Vanilla-Kartenprofil fehlen die analysierten Bauplatzbereiche.")
 };
}
internal sealed record LatiumGeneration(uint Seed,int Width,SiteCounts CinisSites,uint[] CinisFertilities,List<GeneratedIsland> Islands,RegionMetrics Metrics);
internal sealed record World(uint Seed,int Width,string[] Islands,Dictionary<string,SiteCounts> Sites,Dictionary<string,uint[]> Fertilities,List<GeneratedIsland> GeneratedIslands);
internal static class SearchProfile
{
 public static bool Matches(World world,uint cinisSlot1,uint[] requiredCinisPool,bool requireMaxSites,IReadOnlyList<IslandCondition>? conditions=null)
 {
  if(world.Fertilities.TryGetValue(Generator.CinisName,out var cinis))
  {
   if((cinisSlot1!=0&&cinis[0]!=cinisSlot1)||requiredCinisPool.Any(required=>!cinis.Skip(3).Contains(required)))return false;
   if(requireMaxSites&&world.Sites[Generator.CinisName] is not {Mountain:19,River:23})return false;
  }
  foreach(var condition in conditions??[])
  {
   if(CountMatches(world,condition)<condition.MinimumCount)return false;
   if(condition.RequiredSlotIndices is {Length:>0} positions)
   {
    var matchesAtPositions=world.GeneratedIslands.Count(island=>positions.Contains(island.SlotIndex)&&MatchesIsland(condition,island));
    if(matchesAtPositions<Math.Min(condition.MinimumCount,positions.Length))return false;
   }
  }
  return true;
 }
 internal static int CountMatches(World world,IslandCondition condition)
 {
  return world.GeneratedIslands.Count(island=>MatchesIsland(condition,island));
 }
 static bool MatchesIsland(IslandCondition condition,GeneratedIsland island)=>condition.Set==FertilitySetKind.AnyCombination
  ?FertilityDefinitions.IsRegularSet(condition.Region,island.FertilitySet)&&condition.Groups.All(group=>group.Required.All(island.Fertilities.Contains))
  :island.FertilitySet==FertilityDefinitions.SetGuid(condition.Region,condition.Set)&&condition.Groups.All(group=>GroupMatches(island.Fertilities,group));
 static bool GroupMatches(uint[] actual,SlotGroupCondition group)
 {
  var values=group.SlotIndices.Select(index=>actual[index]).ToArray();
  return group.Required.All(values.Contains);
 }
}

internal sealed record Asset(string Name,int W,int H,int X0,int Y0,int X1,int Y1,int RandomSlots)
{
 public bool HasRiver=>Name is "roman_island_extralarge_01" or "roman_island_extralarge_02" or "roman_island_extralarge_03" or "roman_island_extralarge_04" or "roman_island_large_01" or "roman_island_large_02" or "roman_island_large_03" or "roman_island_large_04" or "roman_island_large_05" or "roman_island_large_06" or "roman_island_large_07" or "roman_island_large_09" or "roman_island_medium_01" or "roman_island_medium_02" or "roman_island_medium_03" or "roman_island_medium_04" or "roman_island_medium_05" or "roman_island_medium_06" or "roman_island_medium_08" or "roman_island_small_07" or "roman_dlc01_island_continental_01" or "roman_dlc01_island_medium_01" or "roman_dlc01_island_medium_03" or "roman_dlc_01_island_small_01";
 public(int X,int Y)Min(byte r)=>r switch{0=>(X0,Y0),1=>(H-Y1,X0),2=>(W-X1,H-Y1),_=>(Y0,W-X1)};public(int X,int Y)Size(byte r)=>r is 0 or 2?(X1-X0,Y1-Y0):(Y1-Y0,X1-X0);
 public static readonly Asset[] All=[
new("roman_dlc01_island_medium_01",320,320,16,8,304,296,11),new("roman_dlc01_island_medium_02",320,320,0,0,320,320,3),new("roman_dlc01_island_medium_03",320,320,0,0,320,312,5),new("roman_dlc01_island_small_02",256,256,8,8,256,240,2),new("roman_dlc_01_island_small_01",256,256,16,24,216,240,2),new("roman_dlc01_island_continental_01",768,768,0,0,768,768,49),new("roman_island_extralarge_01",512,512,56,40,472,456,26),new("roman_island_extralarge_02",448,448,24,16,440,432,19),new("roman_island_extralarge_03",512,512,56,72,472,464,5),new("roman_island_extralarge_04",512,512,24,32,440,448,5),new("roman_island_large_01",512,512,32,48,448,464,16),new("roman_island_large_02",512,512,80,64,416,480,16),new("roman_island_large_03",512,512,48,56,432,472,16),new("roman_island_large_04",512,512,48,48,464,424,16),new("roman_island_large_05",512,512,80,72,456,448,16),new("roman_island_large_06",384,384,0,0,384,384,17),new("roman_island_large_07",512,512,80,48,488,432,17),new("roman_island_large_09",512,512,24,40,440,400,7),new("roman_island_medium_01",320,320,24,0,296,320,8),new("roman_island_medium_02",256,256,0,0,256,256,9),new("roman_island_medium_03",320,320,24,0,288,320,5),new("roman_island_medium_04",320,320,0,0,320,320,13),new("roman_island_medium_05",256,256,0,0,256,256,8),new("roman_island_medium_06",320,320,0,0,312,296,4),new("roman_island_medium_07",320,320,0,0,320,320,3),new("roman_island_medium_08",320,320,16,8,320,320,10),new("roman_island_small_01",256,256,0,0,256,240,1),new("roman_island_small_02",192,192,0,8,192,176,4),new("roman_island_small_03",256,256,40,16,200,208,2),new("roman_island_small_04",256,256,0,0,248,240,2),new("roman_island_small_05",256,256,8,8,248,232,3),new("roman_island_small_06",256,256,8,16,248,248,3),new("roman_island_small_07",256,256,16,0,256,248,4)];
}
internal sealed class IslandMask
{
 public Asset Asset{get;}public (short X,short Y)[][] Cells{get;}=new (short,short)[4][];
 public IslandMask(Asset asset,int gridWidth,int gridHeight,byte[] bits)
 {
  Asset=asset;var stride=(gridWidth+31)/32*4;
  for(byte rotation=0;rotation<4;rotation++)
  {
   var cells=new List<(short,short)>();
   for(var y=0;y<gridHeight;y++)for(var x=0;x<gridWidth;x++)
   {
    if((bits[y*stride+x/8]&(1<<(x&7)))==0)continue;
    var point=rotation switch{0=>(x,y),1=>(gridHeight-1-y,x),2=>(gridWidth-1-x,gridHeight-1-y),_=>(y,gridWidth-1-x)};
    cells.Add(((short)point.Item1,(short)point.Item2));
   }
   Cells[rotation]=[..cells];
  }
 }
}
internal static class IslandMasks
{
 static readonly Dictionary<string,IslandMask> Masks=Load();
 public static IslandMask Get(string name)=>Masks.TryGetValue(name,out var mask)?mask:throw new InvalidOperationException($"Inselmaske fehlt: {name}");
 public static Asset Asset(string name)=>Get(name).Asset;
 static Dictionary<string,IslandMask> Load()
 {
  var assembly=Assembly.GetExecutingAssembly();var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("island-masks.txt",StringComparison.OrdinalIgnoreCase));
  using var stream=assembly.GetManifestResourceStream(resource)??throw new InvalidOperationException("Eingebettete Inselmasken fehlen.");using var reader=new StreamReader(stream,Encoding.UTF8);
  var result=new Dictionary<string,IslandMask>(StringComparer.OrdinalIgnoreCase);string? line;
  while((line=reader.ReadLine())is not null)
  {
   if(line.Length==0||line[0]=='#')continue;var p=line.Split('|');
   var asset=new Asset(p[0],int.Parse(p[1]),int.Parse(p[2]),int.Parse(p[3]),int.Parse(p[4]),int.Parse(p[5]),int.Parse(p[6]),0);
   result.Add(asset.Name,new IslandMask(asset,int.Parse(p[7]),int.Parse(p[8]),Convert.FromBase64String(p[9])));
  }
  return result;
 }
}
internal sealed class WorldBits
{
 int width;int stride;ulong[] rows=[];
 public void Reset(int newWidth){width=newWidth;stride=(width+63)/64;var length=width*stride;if(rows.Length<length)rows=new ulong[length];else Array.Clear(rows,0,length);}
 public void Set(int x,int y){if((uint)x<(uint)width&&(uint)y<(uint)width)rows[y*stride+(x>>6)]|=1UL<<(x&63);}
 public void Add(IslandMask mask,int x,int y,byte rotation){foreach(var cell in mask.Cells[rotation])Set(x+cell.X,y+cell.Y);}
 public bool Overlaps(IslandMask mask,int x,int y,byte rotation){foreach(var cell in mask.Cells[rotation]){var px=x+cell.X;var py=y+cell.Y;if((uint)px>=(uint)width||(uint)py>=(uint)width||(rows[py*stride+(px>>6)]&(1UL<<(px&63)))!=0)return true;}return false;}
 public bool Any(int x0,int y0,int x1,int y1,bool outsideIsFree=false)
 {
  // Latium: cells outside the grid count as free (the map border is enforced by the separate edge and bounds checks).
  // Albion: a scan leaving the grid is blocked.
  if(outsideIsFree){x0=Math.Max(x0,0);y0=Math.Max(y0,0);x1=Math.Min(x1,width-1);y1=Math.Min(y1,width-1);if(x0>x1||y0>y1)return false;}
  else if(x0<0||y0<0||x1>=width||y1>=width)return true;
  var firstWord=x0>>6;var lastWord=x1>>6;var firstMask=ulong.MaxValue<<(x0&63);var lastMask=ulong.MaxValue>>(63-(x1&63));
  for(var y=y0;y<=y1;y++)
  {
   var offset=y*stride;
   if(firstWord==lastWord){if((rows[offset+firstWord]&firstMask&lastMask)!=0)return true;continue;}
   if((rows[offset+firstWord]&firstMask)!=0)return true;
   for(var word=firstWord+1;word<lastWord;word++)if(rows[offset+word]!=0)return true;
   if((rows[offset+lastWord]&lastMask)!=0)return true;
  }
  return false;
 }
}
internal sealed class WorldCounts
{
 int width;byte[] cells=[];
 public void Reset(int newWidth){width=newWidth;var length=width*width;if(cells.Length<length)cells=new byte[length];else Array.Clear(cells,0,length);}
 public void Set(int x,int y){if((uint)x<(uint)width&&(uint)y<(uint)width)cells[y*width+x]++;}
 public void Add(IslandMask mask,int x,int y,byte rotation){foreach(var cell in mask.Cells[rotation]){var px=x+cell.X;var py=y+cell.Y;if((uint)px<(uint)width&&(uint)py<(uint)width)cells[py*width+px]++;}}
 public void Remove(IslandMask mask,int x,int y,byte rotation){foreach(var cell in mask.Cells[rotation]){var px=x+cell.X;var py=y+cell.Y;if((uint)px<(uint)width&&(uint)py<(uint)width){var index=py*width+px;if(cells[index]==0)throw new InvalidOperationException("Kollisionszelle ist bereits leer.");cells[index]--;}}}
 public bool Overlaps(IslandMask mask,int x,int y,byte rotation){foreach(var cell in mask.Cells[rotation]){var px=x+cell.X;var py=y+cell.Y;if((uint)px>=(uint)width||(uint)py>=(uint)width||cells[py*width+px]!=0)return true;}return false;}
 public bool Overlaps(IslandMask mask,int x,int y,byte rotation,int border){foreach(var cell in mask.Cells[rotation]){var px=x+cell.X;var py=y+cell.Y;if((uint)px>=(uint)width||(uint)py>=(uint)width||cells[py*width+px]!=0)return true;var left=px-border;var right=px+border;var top=py-border;var bottom=py+border;if((uint)left<(uint)width&&cells[py*width+left]!=0)return true;if((uint)right<(uint)width&&cells[py*width+right]!=0)return true;if((uint)top<(uint)width&&cells[top*width+px]!=0)return true;if((uint)bottom<(uint)width&&cells[bottom*width+px]!=0)return true;}return false;}
}
internal sealed class GeneratorScratch
{
 int[] grid=[];(int X,int Y)[] positions=[]; (int X,int Y)[] attractionPoints=[]; (int X,int Y)[] wiggleOffsets=[];
 public WorldBits Occupied{get;}=new();
 public WorldCounts CountedOccupied{get;}=new();
 public int[] GetGrid(int length){if(grid.Length<length)grid=new int[length];return grid;}
 public (int X,int Y)[] GetPositions(int length){if(positions.Length<length)positions=new (int,int)[length];return positions;}
 public (int X,int Y)[] GetAttractionPoints(int length){if(attractionPoints.Length<length)attractionPoints=new (int,int)[length];return attractionPoints;}
 public (int X,int Y)[] GetWiggleOffsets(int length){if(wiggleOffsets.Length<length)wiggleOffsets=new (int,int)[length];return wiggleOffsets;}
}
internal sealed class Rng{readonly uint[]s=new uint[17];int a=1,b=11;public Rng(uint x){for(var i=0;i<17;i++)s[i]=x=unchecked(x*2891336453u+1);}Rng(Rng other){s=(uint[])other.s.Clone();a=other.a;b=other.b;}public Rng Clone()=>new(this);public uint Next(){a=(a+16)%17;b=(b+16)%17;return s[a]=unchecked(BitOperations.RotateLeft(s[a],9)+BitOperations.RotateLeft(s[b],13));}public uint Uniform(uint n){while(true){var x=Next();if(x/n<uint.MaxValue/n||uint.MaxValue%n==n-1)return x%n;}}public uint Scaled(uint n)=>(uint)(((ulong)Next()*n)>>32);public void Advance(int n){while(n-->0)Next();}public void ShuffleCount(int n){for(var i=1;i<n;i++)Uniform((uint)(i+1));}public void Shuffle<T>(IList<T>x){for(var i=1;i<x.Count;i++){var j=(int)Uniform((uint)(i+1));(x[i],x[j])=(x[j],x[i]);}}public void Shuffle<T>(Span<T>x){for(var i=1;i<x.Length;i++){var j=(int)Uniform((uint)(i+1));(x[i],x[j])=(x[j],x[i]);}}}
