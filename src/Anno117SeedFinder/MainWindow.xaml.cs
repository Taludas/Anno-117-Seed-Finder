using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

public partial class MainWindow:Window
{
 CancellationTokenSource? cancellation;
 string? lastOutput;
 readonly DispatcherTimer cinisWarningTimer=new(){Interval=TimeSpan.FromSeconds(3)};
 CheckBox[] cinisChecks=[];
 string? loadedSeedListPath;bool overwriteConfirmed;bool running;
 bool profileControlsReady;
 // The advanced fertility filters: one row per AdvancedFilter; the sliders count in steps of AdvancedStep tiles.
 const int AdvancedStep=500;
 sealed record AdvancedRow(AdvancedFilter Filter,CheckBox Enabled,Slider Slider,TextBlock Value,TextBlock Range,Button Median);
 AdvancedRow[] advancedRows=[];
 readonly HashSet<AdvancedFilter> configuredAdvanced=[];
 // Seed scoring: one weight control per ScoreMetric, found by name ("Wgt"+metric); see ScoreMetrics.cs/SCORING-PLAN.md.
 sealed record ScoreWeightRow(ScoreMetric Metric,Slider Slider,TextBlock Value,Func<bool> Applicable);
 ScoreWeightRow[] scoreRows=[];
 bool formattingSeedInput;
 MapProfile activeProfile=MapProfiles.Default;
 readonly ProfileChoice<MapTemplateKind>[] templateChoices=
 [new("Archipelago",MapTemplateKind.Archipelago),new("Atoll",MapTemplateKind.Atoll),new("Rift",MapTemplateKind.Rift),new("Corners",MapTemplateKind.Corners),new("Island Chains",MapTemplateKind.IslandChains)];
 readonly ProfileChoice<MapSizeKind>[] sizeChoices=[new("Small",MapSizeKind.Small),new("Medium",MapSizeKind.Medium),new("Large",MapSizeKind.Large)];

 public MainWindow()
 {
  InitializeComponent();
  advancedRows=[..AdvancedFilters.All.Select(definition=>{var name=definition.Filter.ToString();return new AdvancedRow(definition.Filter,(CheckBox)FindName("ChkAdv"+name),(Slider)FindName("SldAdv"+name),(TextBlock)FindName("LblAdv"+name),(TextBlock)FindName("LblAdv"+name+"Range"),(Button)FindName("BtnAdv"+name+"Median"));})];
  scoreRows=[..Enum.GetValues<ScoreMetric>().Select(metric=>new ScoreWeightRow(metric,(Slider)FindName("Wgt"+metric),(TextBlock)FindName("LblWgt"+metric),ScoreApplicability(metric)))];
  CmbMapTemplate.ItemsSource=templateChoices;CmbMapSize.ItemsSource=sizeChoices;
  CmbMapTemplate.SelectedItem=templateChoices.Single(choice=>choice.Value==MapTemplateKind.Corners);CmbMapSize.SelectedItem=sizeChoices.Single(choice=>choice.Value==MapSizeKind.Large);
  profileControlsReady=true;RefreshDlcPresentation();RefreshProfileLabels();
  TxtThreads.Text=Math.Max(1,Environment.ProcessorCount).ToString();
  TxtOutput.Text=Path.Combine(AppContext.BaseDirectory,"treffer.txt");
  FertilityIcons.Configure(CmbSlot1,[FertilityDefinitions.Choice(RegionKind.Latium,2206),FertilityDefinitions.Choice(RegionKind.Latium,2209)]);CmbSlot1.SelectedIndex=0;
  cinisChecks=[ChkGrapes,ChkFlax,ChkMurex,ChkOysters,ChkSturgeon,ChkGold];
  foreach(var box in cinisChecks)box.Checked+=CinisFertilityChecked;
  ((System.Collections.Specialized.INotifyCollectionChanged)GridResults.Items).CollectionChanged+=(_,_)=>UpdateTableSeedsOption();UpdateTableSeedsOption();
  cinisWarningTimer.Tick+=(_,_)=>{TxtCinisWarning.Visibility=Visibility.Collapsed;cinisWarningTimer.Stop();};
  ResultColumnsChanged(this,new RoutedEventArgs());
  UpdateConditionAddButtons();
  CmbLanguage.SelectedIndex=Localization.Instance.Language==AppLanguage.English?1:0;
 }

 void LanguageChanged(object sender,SelectionChangedEventArgs e)
 {
  Localization.Instance.Language=CmbLanguage.SelectedIndex==1?AppLanguage.English:AppLanguage.German;
  RefreshProfileLabels();UpdateConditionAddButtons();UpdateTableSeedsOption();
 }

 void BrowseOutput(object sender,RoutedEventArgs e)
 {
  var full=Path.GetFullPath(TxtOutput.Text);
  var dialog=new SaveFileDialog{Title=Localization.Instance["SelectOutputFileTitle"],Filter=Localization.Instance["TxtFilter"],FileName=Path.GetFileName(full),InitialDirectory=Path.GetDirectoryName(full)};
  if(dialog.ShowDialog(this)==true)TxtOutput.Text=dialog.FileName;
 }

 void SavePreset(object sender,RoutedEventArgs e)
 {
  try
  {
   var dialog=new SaveFileDialog{Title=Localization.Instance["SavePresetTitle"],Filter=Localization.Instance["PresetFilter"],FileName="mein-preset.anno117settings.json",InitialDirectory=AppContext.BaseDirectory};
   if(dialog.ShowDialog()!=true)return;
   var preset=new FinderSettingsPreset
   {
    Template=activeProfile.Template,Size=activeProfile.Size,StartMode=CmbStartMode.SelectedIndex==1?StartModeKind.StartIsland:StartModeKind.Flagship,Dlc01=activeProfile.Dlc01,DlcRetroactive=activeProfile.Retro,FertilitySetting=CmbFertilitySetting.SelectedIndex,SlotSetting=CmbSlotSetting.SelectedIndex,
     FirstSeed=SeedDigits(TxtFirstSeed.Text),LastSeed=SeedDigits(TxtMaxSeed.Text),Threads=TxtThreads.Text,MaximumHits=TxtLimit.Text,OutputPath=TxtOutput.Text,PreviewSeed=TxtPreviewSeed.Text,
    MinimumGoldSites=MinimumText(CmbMinGoldSites),MinimumSturgeonSites=MinimumText(CmbMinSturgeonSites),MinimumLatiumMountainSites=MinimumText(CmbMinLatiumMountainSites),MinimumLatiumRiverSites=MinimumText(CmbMinLatiumRiverSites),MinimumAlbionMountainSites=MinimumText(CmbMinAlbionMountainSites),
    GoldSitesEnabled=ChkMinGoldSites.IsChecked==true,SturgeonSitesEnabled=ChkMinSturgeonSites.IsChecked==true,LatiumMountainSitesEnabled=ChkMinLatiumMountainSites.IsChecked==true,LatiumRiverSitesEnabled=ChkMinLatiumRiverSites.IsChecked==true,AlbionMountainSitesEnabled=ChkMinAlbionMountainSites.IsChecked==true,
    MinimumLatiumAreaK=(int)SldMinLatiumArea.Value,MinimumAlbionAreaK=(int)SldMinAlbionArea.Value,MinimumAlbionSwampAreaK=(int)SldMinAlbionSwampArea.Value,LatiumAreaEnabled=ChkMinLatiumArea.IsChecked==true,AlbionAreaEnabled=ChkMinAlbionArea.IsChecked==true,AlbionSwampAreaEnabled=ChkMinAlbionSwampArea.IsChecked==true,
    MinimumGoldMines=MinimumText(CmbMinGoldMines),MinimumMarbleSites=MinimumText(CmbMinMarbleSites),MinimumMineralMines=MinimumText(CmbMinMineralMines),MinimumCopperMines=MinimumText(CmbMinCopperMines),MinimumSilverMines=MinimumText(CmbMinSilverMines),MinimumTinMines=MinimumText(CmbMinTinMines),
    GoldMinesEnabled=ChkMinGoldMines.IsChecked==true,MarbleSitesEnabled=ChkMinMarbleSites.IsChecked==true,MineralMinesEnabled=ChkMinMineralMines.IsChecked==true,CopperMinesEnabled=ChkMinCopperMines.IsChecked==true,SilverMinesEnabled=ChkMinSilverMines.IsChecked==true,TinMinesEnabled=ChkMinTinMines.IsChecked==true,
    ShowAdvancedFilters=ChkAdvanced.IsChecked==true,AdvancedMinimums=advancedRows.ToDictionary(row=>row.Filter.ToString(),row=>(int)row.Slider.Value*AdvancedStep),AdvancedEnabled=advancedRows.ToDictionary(row=>row.Filter.ToString(),row=>row.Enabled.IsChecked==true),
    EnableScoring=ChkScoring.IsChecked==true,ExtendScoringOutliers=ChkScoreOutliers.IsChecked==true,ScoreWeights=scoreRows.ToDictionary(row=>row.Metric.ToString(),row=>(int)row.Slider.Value),
    CinisSlot1=(CmbSlot1.SelectedItem as FertilityChoice)?.Guid??0u,CinisFertilities=[..cinisChecks.Where(box=>box.IsChecked==true).Select(box=>uint.Parse(box.Tag.ToString()!))],CinisMaximumSites=ChkMaxSites.IsChecked==true,
    Conditions=[..LatiumConditions.Children.OfType<ConditionRowControl>().Concat(AlbionConditions.Children.OfType<ConditionRowControl>()).Select(row=>row.ExportPreset())]
   };
   FinderSettingsStorage.Save(dialog.FileName,preset);LblProgress.Text=Localization.Instance.Format("PresetSavedFormat",Path.GetFileName(dialog.FileName));
  }
  catch(Exception error){MessageBox.Show(this,error.Message,Localization.Instance["PresetSaveFailedTitle"],MessageBoxButton.OK,MessageBoxImage.Error);}
 }

 void LoadPreset(object sender,RoutedEventArgs e)
 {
  try
  {
   var dialog=new OpenFileDialog{Title=Localization.Instance["LoadPresetTitle"],Filter=Localization.Instance["PresetFilter"],InitialDirectory=AppContext.BaseDirectory};
   if(dialog.ShowDialog()!=true)return;
   ApplyPreset(FinderSettingsStorage.Load(dialog.FileName));LblProgress.Text=Localization.Instance.Format("PresetLoadedFormat",Path.GetFileName(dialog.FileName));
  }
  catch(Exception error){MessageBox.Show(this,error.Message,Localization.Instance["PresetLoadFailedTitle"],MessageBoxButton.OK,MessageBoxImage.Error);}
 }

 void ApplyPreset(FinderSettingsPreset preset)
 {
  profileControlsReady=false;
  CmbMapTemplate.SelectedItem=templateChoices.Single(choice=>choice.Value==preset.Template);CmbMapSize.SelectedItem=sizeChoices.Single(choice=>choice.Value==preset.Size);CmbStartMode.SelectedIndex=preset.StartMode==StartModeKind.StartIsland?1:0;ChkDlc01.IsChecked=preset.Dlc01;ChkDlcRetro.IsChecked=preset.Dlc01&&preset.DlcRetroactive;activeProfile=MapProfiles.Get(preset.Template,preset.Size,preset.Dlc01,preset.Dlc01&&preset.DlcRetroactive);
  CmbFertilitySetting.SelectedIndex=Math.Clamp(preset.FertilitySetting,0,CmbFertilitySetting.Items.Count-1);CmbSlotSetting.SelectedIndex=Math.Clamp(preset.SlotSetting,0,CmbSlotSetting.Items.Count-1);
  TxtFirstSeed.Text=preset.FirstSeed;TxtMaxSeed.Text=preset.LastSeed;TxtThreads.Text=preset.Threads;TxtLimit.Text=preset.MaximumHits;TxtOutput.Text=preset.OutputPath;TxtPreviewSeed.Text=preset.PreviewSeed;
  CmbMinGoldSites.Tag=preset.MinimumGoldSites;CmbMinSturgeonSites.Tag=preset.MinimumSturgeonSites;CmbMinLatiumMountainSites.Tag=preset.MinimumLatiumMountainSites;CmbMinLatiumRiverSites.Tag=preset.MinimumLatiumRiverSites;CmbMinAlbionMountainSites.Tag=preset.MinimumAlbionMountainSites;
  ChkMinGoldSites.IsChecked=PresetFilterEnabled(preset.GoldSitesEnabled,preset.MinimumGoldSites);ChkMinSturgeonSites.IsChecked=PresetFilterEnabled(preset.SturgeonSitesEnabled,preset.MinimumSturgeonSites);ChkMinLatiumMountainSites.IsChecked=PresetFilterEnabled(preset.LatiumMountainSitesEnabled,preset.MinimumLatiumMountainSites);ChkMinLatiumRiverSites.IsChecked=PresetFilterEnabled(preset.LatiumRiverSitesEnabled,preset.MinimumLatiumRiverSites);ChkMinAlbionMountainSites.IsChecked=PresetFilterEnabled(preset.AlbionMountainSitesEnabled,preset.MinimumAlbionMountainSites);
  SldMinLatiumArea.Tag=preset.MinimumLatiumAreaK;SldMinAlbionArea.Tag=preset.MinimumAlbionAreaK;SldMinAlbionSwampArea.Tag=preset.MinimumAlbionSwampAreaK;ChkMinLatiumArea.IsChecked=preset.LatiumAreaEnabled==true;ChkMinAlbionArea.IsChecked=preset.AlbionAreaEnabled==true;ChkMinAlbionSwampArea.IsChecked=preset.AlbionSwampAreaEnabled==true;
  CmbMinGoldMines.Tag=preset.MinimumGoldMines;CmbMinMarbleSites.Tag=preset.MinimumMarbleSites;CmbMinMineralMines.Tag=preset.MinimumMineralMines;CmbMinCopperMines.Tag=preset.MinimumCopperMines;CmbMinSilverMines.Tag=preset.MinimumSilverMines;CmbMinTinMines.Tag=preset.MinimumTinMines;
  ChkMinGoldMines.IsChecked=PresetFilterEnabled(preset.GoldMinesEnabled,preset.MinimumGoldMines);ChkMinMarbleSites.IsChecked=PresetFilterEnabled(preset.MarbleSitesEnabled,preset.MinimumMarbleSites);ChkMinMineralMines.IsChecked=preset.MineralMinesEnabled;ChkMinCopperMines.IsChecked=preset.CopperMinesEnabled;ChkMinSilverMines.IsChecked=preset.SilverMinesEnabled;ChkMinTinMines.IsChecked=preset.TinMinesEnabled;
  // Sliders without a stored value fall back to the median when the ranges are refreshed below.
  ChkAdvanced.IsChecked=preset.ShowAdvancedFilters;configuredAdvanced.Clear();
  foreach(var row in advancedRows)
  {
   var key=row.Filter.ToString();
   if(preset.AdvancedMinimums.TryGetValue(key,out var tiles)&&tiles>0)row.Slider.Tag=tiles/AdvancedStep;
   row.Enabled.IsChecked=preset.AdvancedEnabled.TryGetValue(key,out var enabled)&&enabled;
  }
  CmbSlot1.SelectedItem=(CmbSlot1.Items.OfType<FertilityChoice>().First(choice=>choice.Guid==preset.CinisSlot1));
  foreach(var box in cinisChecks)box.IsChecked=preset.CinisFertilities.Contains(uint.Parse(box.Tag.ToString()!));ChkMaxSites.IsChecked=preset.CinisMaximumSites;
  ChkScoring.IsChecked=preset.EnableScoring;ChkScoreOutliers.IsChecked=preset.ExtendScoringOutliers;
  foreach(var row in scoreRows)row.Slider.Value=preset.ScoreWeights.TryGetValue(row.Metric.ToString(),out var weight)?Math.Clamp(weight,0,10):0;
  LatiumConditions.Children.Clear();AlbionConditions.Children.Clear();GridResults.ItemsSource=null;
  foreach(var condition in preset.Conditions)
  {
   var target=condition.Region==RegionKind.Latium?LatiumConditions:AlbionConditions;
   var row=AddCondition(condition.Region,condition.Set,target);row.ApplyPreset(condition);
  }
  profileControlsReady=true;RefreshDlcPresentation();RefreshProfileLabels();UpdateConditionAddButtons();
 }

 async void RunSearch(object sender,RoutedEventArgs e)
 {
  // For a search over the table's seeds: the table as it was, to bring back when nothing passes the filters.
  SearchResultRow[]? previousRows=null;var previousSorting=Array.Empty<System.ComponentModel.SortDescription>();
  try
  {
   var selected=activeProfile.Dlc01?cinisChecks.Where(x=>x.IsChecked==true).Select(x=>uint.Parse(x.Tag.ToString()!)).ToArray():[];
   var conditions=LatiumConditions.Children.OfType<ConditionRowControl>().Concat(AlbionConditions.Children.OfType<ConditionRowControl>()).Select(x=>x.BuildCondition()).ToArray();
   var cinisSlot1=activeProfile.Dlc01?(CmbSlot1.SelectedItem as FertilityChoice)?.Guid??0u:0u;
   // Sequential filtering: with the option on, the search runs over the seeds now in the table instead of a range.
   var tableSeeds=ChkTableSeeds.IsChecked==true?GridResults.Items.OfType<SearchResultRow>().Select(row=>row.Seed).Distinct().ToArray():null;
   if(tableSeeds is not null&&tableSeeds.Length==0)throw new ArgumentException(Localization.Instance["NoSeedsInTable"]);
   if(tableSeeds is not null&&!ConfirmOverwriteOfLoadedList())return;
   if(tableSeeds is not null){previousRows=GridResults.Items.OfType<SearchResultRow>().ToArray();previousSorting=GridResults.Items.SortDescriptions.ToArray();}
   var request=new SearchRequest(tableSeeds is null?ParseSeed(TxtFirstSeed,Localization.Instance["FirstSeed"]):(int)tableSeeds.Min(),tableSeeds is null?ParseSeed(TxtMaxSeed,Localization.Instance["LastSeed"]):(int)tableSeeds.Max(),ParsePositive(TxtThreads,Localization.Instance["Threads"]),ParseNonNegative(TxtLimit,Localization.Instance["MaxHits"]),TxtOutput.Text,cinisSlot1,selected,activeProfile.Dlc01&&ChkMaxSites.IsChecked==true,conditions,activeProfile,
    ActiveMinimum(ChkMinGoldSites,CmbMinGoldSites),ActiveMinimum(ChkMinSturgeonSites,CmbMinSturgeonSites),ActiveMinimum(ChkMinLatiumMountainSites,CmbMinLatiumMountainSites),ActiveMinimum(ChkMinLatiumRiverSites,CmbMinLatiumRiverSites),ActiveMinimum(ChkMinAlbionMountainSites,CmbMinAlbionMountainSites),ActiveAreaMinimum(ChkMinLatiumArea,SldMinLatiumArea),ActiveAreaMinimum(ChkMinAlbionArea,SldMinAlbionArea),ActiveAreaMinimum(ChkMinAlbionSwampArea,SldMinAlbionSwampArea),CurrentFertilitySetting(),CurrentSlotSetting(),
    ActiveMinimum(ChkMinMineralMines,CmbMinMineralMines),ActiveMinimum(ChkMinCopperMines,CmbMinCopperMines),ActiveMinimum(ChkMinSilverMines,CmbMinSilverMines),ActiveMinimum(ChkMinMarbleSites,CmbMinMarbleSites),ActiveMinimum(ChkMinGoldMines,CmbMinGoldMines),ActiveMinimum(ChkMinTinMines,CmbMinTinMines),tableSeeds,ActiveAdvancedMinimums());
   SetRunning(true);GridResults.ItemsSource=null;SearchProgress.Value=0;LblProgress.Text=Localization.Instance["SearchStarting"];
   cancellation=new CancellationTokenSource();
   var reporter=new Progress<SearchProgress>(x=>{SearchProgress.Value=Math.Min(100,(long)x.Processed*100/x.Total);LblProgress.Text=Localization.Instance.Format("ProgressFormat",x.Processed.ToString("N0"),x.Total.ToString("N0"),x.Hits.ToString("N0"));});
   var summary=await SeedSearcher.SearchAsync(request,reporter,cancellation.Token);
   ShowResults(summary.Hits);
   lastOutput=summary.Output;BtnOpen.IsEnabled=true;
   if(summary.Canceled)
   {
    SearchProgress.Value=Math.Min(100,(long)summary.Processed*100/request.SeedCount);
    LblProgress.Text=Localization.Instance.Format("CanceledFormat",summary.Processed.ToString("N0"),request.SeedCount.ToString("N0"),summary.FoundCount.ToString("N0"),summary.Strategy);
   }
   else if(previousRows is not null&&summary.Hits.Length==0){SearchProgress.Value=100;LblProgress.Text=Localization.Instance.Format("NoHitsRestoredFormat",previousRows.Length.ToString("N0"));}
   else{SearchProgress.Value=100;LblProgress.Text=Localization.Instance.Format("CompletedFormat",summary.FoundCount.ToString("N0"),summary.Elapsed.TotalSeconds.ToString("F2"),summary.Strategy);}
  }
  catch(OperationCanceledException){LblProgress.Text=Localization.Instance["SearchCanceled"];}
  catch(Exception error){LblProgress.Text=Localization.Instance["GenericError"];MessageBox.Show(this,error.Message,Localization.Instance["SearchStartFailedTitle"],MessageBoxButton.OK,MessageBoxImage.Error);}
  finally
  {
   cancellation?.Dispose();cancellation=null;
   // Nothing passed (or the search failed or was cancelled before a hit): the table goes back to the list it had before.
   if(previousRows is not null&&GridResults.Items.Count==0)RestoreTable(previousRows,previousSorting);
   SetRunning(false);
  }
 }

 void RestoreTable(SearchResultRow[] rows,System.ComponentModel.SortDescription[] sorting)
 {
  GridResults.ItemsSource=rows;
  foreach(var description in sorting)GridResults.Items.SortDescriptions.Add(description);
  BtnExportCsv.IsEnabled=rows.Length>0;RecomputeScores();
 }

 void ShowResults(IReadOnlyList<SearchHit> hits)
 {
  GridResults.ItemsSource=hits.Select(hit=>SearchResultRow.From(hit,activeProfile)).ToArray();
  BtnExportCsv.IsEnabled=hits.Count>0;RecomputeScores();
 }

 // Appends a single seed to whatever the table currently shows, so results from a search,
 // a loaded seed list and hand-picked seeds can be collected side by side.
 void AddSeedToResults(object sender,RoutedEventArgs e)
 {
  if(!uint.TryParse(TxtPreviewSeed.Text,out var seed)||seed<SeedLimits.Minimum||seed>SeedLimits.Maximum)
  {
   MessageBox.Show(this,Localization.Instance.Format("InvalidSeedFormat",SeedLimits.Minimum.ToString("N0"),SeedLimits.Maximum.ToString("N0")),Localization.Instance["InvalidSeedTitle"],MessageBoxButton.OK,MessageBoxImage.Information);return;
  }
  var rows=(GridResults.ItemsSource as IEnumerable<SearchResultRow>)?.ToList()??[];
  if(rows.Any(row=>row.Seed==seed)){LblProgress.Text=Localization.Instance.Format("SeedAlreadyInTableFormat",seed.ToString("N0"));return;}
  try
  {
   rows.Add(SearchResultRow.From(SeedSearcher.Describe(seed,activeProfile,CurrentFertilitySetting(),CurrentSlotSetting()),activeProfile));
   // Replacing ItemsSource builds a fresh view, so the active sorting is carried over by hand.
   var sorting=GridResults.Items.SortDescriptions.ToArray();
   GridResults.ItemsSource=rows.ToArray();
   foreach(var description in sorting)GridResults.Items.SortDescriptions.Add(description);
   BtnExportCsv.IsEnabled=true;RecomputeScores();LblProgress.Text=Localization.Instance.Format("SeedAddedFormat",seed.ToString("N0"),rows.Count.ToString("N0"));
  }
  catch(Exception error){MessageBox.Show(this,error.Message,Localization.Instance["SeedComputeFailedTitle"],MessageBoxButton.OK,MessageBoxImage.Error);}
 }

 void ResultColumnsChanged(object sender,RoutedEventArgs e)
 {
  if(GoldMinesColumn is null)return;
  MarbleSitesColumn.Visibility=ColumnVisibility(ChkColMarble);GoldMinesColumn.Visibility=ColumnVisibility(ChkColGoldMine);GoldRiverColumn.Visibility=ColumnVisibility(ChkColGoldRiver);SturgeonRiverColumn.Visibility=ColumnVisibility(ChkColSturgeon);
  MineralMinesColumn.Visibility=ColumnVisibility(ChkColMinerals);CopperMinesColumn.Visibility=ColumnVisibility(ChkColCopper);SilverMinesColumn.Visibility=ColumnVisibility(ChkColSilver);TinMinesColumn.Visibility=ColumnVisibility(ChkColTin);
  // One switch shows or hides all advanced filters together with their columns.
  var advanced=ChkAdvanced.IsChecked==true;AdvancedFilterPanel.Visibility=advanced?Visibility.Visible:Visibility.Collapsed;
  foreach(var column in AdvancedColumns)column.Visibility=advanced?Visibility.Visible:Visibility.Collapsed;
  RefreshScoreVisibility();RecomputeScores();
 }
 DataGridColumn[] AdvancedColumns=>[AdvColLatiumHarbourMurex,AdvColLatiumHarbourOysters,AdvColAlbionHarbourSaltwort,AdvColAlbionHarbourSeaShells,AdvColAlbionMarshSmallBirds,AdvColAlbionMarshBeaver];
 // The advanced filters only count while they are shown; each one also needs its own checkbox.
 int[] ActiveAdvancedMinimums()=>[..advancedRows.Select(row=>ChkAdvanced.IsChecked==true&&row.Enabled.IsChecked==true?(int)row.Slider.Value*AdvancedStep:0)];
 static Visibility ColumnVisibility(CheckBox box)=>box.IsChecked==true?Visibility.Visible:Visibility.Collapsed;

 void ExportCsv(object sender,RoutedEventArgs e)
 {
  if(GridResults.ItemsSource is not IEnumerable<SearchResultRow> rows)return;
  try
  {
   var dialog=new SaveFileDialog{Title=Localization.Instance["SaveCsvTitle"],Filter=Localization.Instance["CsvFilter"],FileName="seeds.csv",InitialDirectory=Path.GetDirectoryName(Path.GetFullPath(TxtOutput.Text))};
   if(dialog.ShowDialog(this)!=true)return;
   // Sorted exactly as displayed, so an exported table matches what the user sees.
   var ordered=GridResults.Items.OfType<SearchResultRow>().ToArray();
   var lines=new List<string>{SeedSearcher.CsvHeader+";Score"};
   lines.AddRange(ordered.Select(row=>string.Join(';',
    row.Seed,row.LatiumAreaValue,row.AlbionAreaValue,row.AlbionSwampValue,
    row.LatiumMountain,row.LatiumRiver,row.AlbionMountain,
    row.GoldRiverSites,row.SturgeonRiverSites,
    row.MarbleSites,row.MineralMines,row.GoldMines,row.SilverMines,row.TinMines,row.CopperMines,
    string.Join(';',row.AdvancedTiles),
    CinisNames(row.Seed),
    row.Score?.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)??"")));
   File.WriteAllLines(dialog.FileName,lines,new System.Text.UTF8Encoding(true));
   LblProgress.Text=Localization.Instance.Format("CsvSavedFormat",Path.GetFileName(dialog.FileName));
  }
  catch(Exception error){MessageBox.Show(this,error.Message,Localization.Instance["CsvSaveFailedTitle"],MessageBoxButton.OK,MessageBoxImage.Error);}
 }
 string CinisNames(uint seed)
 {
  if(!activeProfile.Dlc01)return "";
  try{return string.Join(' ',Generator.GenerateLatium(seed,new GeneratorScratch(),activeProfile,fertilitySetting:CurrentFertilitySetting()).CinisFertilities.Select(Generator.Label));}
  catch{return "";}
 }

 async void LoadSeedList(object sender,RoutedEventArgs e)
 {
  try
  {
   var dialog=new OpenFileDialog{Title=Localization.Instance["LoadSeedListTitle"],Filter=Localization.Instance["SeedListFilter"],InitialDirectory=Path.GetDirectoryName(Path.GetFullPath(TxtOutput.Text))};
   if(dialog.ShowDialog(this)!=true)return;
   // One seed per line; blank lines, comments and any trailing columns are ignored so a
   // previously exported CSV can be fed straight back in.
   var seeds=File.ReadLines(dialog.FileName)
    .Select(line=>new string(line.Trim().TakeWhile(char.IsDigit).ToArray()))
    .Where(text=>text.Length>0&&uint.TryParse(text,out var value)&&value>=SeedLimits.Minimum&&value<=SeedLimits.Maximum)
    .Select(uint.Parse).Distinct().ToArray();
   if(seeds.Length==0){MessageBox.Show(this,Localization.Instance["NoValidSeedFound"],Localization.Instance["NoSeedsFoundTitle"],MessageBoxButton.OK,MessageBoxImage.Information);return;}
   SetRunning(true);LblProgress.Text=Localization.Instance.Format("EvaluatingSeedsFormat",seeds.Length.ToString("N0"));
   var profile=activeProfile;var setting=CurrentFertilitySetting();var slots=CurrentSlotSetting();
   var hits=await Task.Run(()=>
   {
    var rows=new SearchHit[seeds.Length];
    Parallel.For(0,seeds.Length,index=>rows[index]=SeedSearcher.Describe(seeds[index],profile,setting,slots));
    return rows;
   });
   ShowResults(hits);loadedSeedListPath=Path.GetFullPath(dialog.FileName);overwriteConfirmed=false;SearchProgress.Value=100;LblProgress.Text=Localization.Instance.Format("SeedsLoadedFormat",hits.Length.ToString("N0"),Path.GetFileName(dialog.FileName));
  }
  catch(Exception error){LblProgress.Text=Localization.Instance["GenericError"];MessageBox.Show(this,error.Message,Localization.Instance["SeedListLoadFailedTitle"],MessageBoxButton.OK,MessageBoxImage.Error);}
  finally{SetRunning(false);}
 }

 static int ParseSeed(TextBox box,string label)=>int.TryParse(SeedDigits(box.Text),out var value)&&value is >=SeedLimits.Minimum and <=SeedLimits.Maximum?value:throw new ArgumentException(Localization.Instance.Format("ParseSeedFormat",label,SeedLimits.Minimum.ToString("N0"),SeedLimits.Maximum.ToString("N0")));
 static int ParsePositive(TextBox box,string label)=>int.TryParse(box.Text,out var value)&&value>0?value:throw new ArgumentException(Localization.Instance.Format("ParsePositiveFormat",label));
 static int ParseNonNegative(TextBox box,string label)=>int.TryParse(box.Text,out var value)&&value>=0?value:throw new ArgumentException(Localization.Instance.Format("ParseNonNegativeFormat",label));
 void NumericInputOnly(object sender,System.Windows.Input.TextCompositionEventArgs e)=>e.Handled=e.Text.Any(character=>character is <'0' or >'9');
 void NumericPasteOnly(object sender,DataObjectPastingEventArgs e)
 {
  if(!e.DataObject.GetDataPresent(DataFormats.UnicodeText)||e.DataObject.GetData(DataFormats.UnicodeText) is not string text||text.Any(character=>character is <'0' or >'9'&&(!(sender is TextBox box&&(box==TxtFirstSeed||box==TxtMaxSeed))||character!='.')))e.CancelCommand();
 }
 void FormatSeedInput(object sender,TextChangedEventArgs e)
 {
  if(formattingSeedInput||sender is not TextBox box)return;
  var caret=Math.Clamp(box.CaretIndex,0,box.Text.Length);var digitsBeforeCaret=box.Text[..caret].Count(char.IsDigit);var formatted=FormatSeedText(box.Text);
  if(formatted==box.Text)return;
  formattingSeedInput=true;box.Text=formatted;box.CaretIndex=CaretAfterDigits(formatted,digitsBeforeCaret);formattingSeedInput=false;
 }
 static string SeedDigits(string text)=>new(text.Where(char.IsDigit).ToArray());
 static string FormatSeedText(string text)
 {
  var digits=SeedDigits(text);if(digits.Length==0)return "";digits=digits.TrimStart('0');if(digits.Length==0)return "0";
  var first=digits.Length%3;if(first==0)first=3;var parts=new List<string>{digits[..first]};for(var index=first;index<digits.Length;index+=3)parts.Add(digits.Substring(index,3));return string.Join('.',parts);
 }
 static int CaretAfterDigits(string text,int digitCount)
 {
  if(digitCount<=0)return 0;var seen=0;for(var index=0;index<text.Length;index++)if(char.IsDigit(text[index])&&++seen==digitCount)return index+1;return text.Length;
 }
 // A random seed within the valid range, put into the preview seed box.
 void RandomPreviewSeed(object sender,RoutedEventArgs e)=>TxtPreviewSeed.Text=Random.Shared.Next(SeedLimits.Minimum,SeedLimits.Maximum+1).ToString();
 void SetMinimumSeed(object sender,RoutedEventArgs e)=>TxtFirstSeed.Text=SeedLimits.Minimum.ToString();
 void SetMaximumSeed(object sender,RoutedEventArgs e)=>TxtMaxSeed.Text=SeedLimits.Maximum.ToString();
 internal bool SmokePreparedProfileControls()=>CmbStartMode.IsEnabled&&ChkDlc01.IsChecked==true&&ChkDlc01.IsEnabled&&ChkDlc03.IsChecked==false&&!ChkDlc03.IsEnabled&&Math.Abs(StartProfilePanel.ActualHeight-MapProfilePanel.ActualHeight)<.1&&Math.Abs(DlcProfilePanel.ActualHeight-MapProfilePanel.ActualHeight)<.1;
 // The table option: off and disabled while the table is empty; on, and taking the seed range out of play, once seeds are in.
 internal bool SmokeTableSeeds()
 {
  ShowResults([]);var emptyOk=!ChkTableSeeds.IsEnabled&&ChkTableSeeds.IsChecked!=true&&TxtFirstSeed.IsEnabled;
  ShowResults([SeedSearcher.Describe(1,activeProfile,FertilitySetting.Abundant),SeedSearcher.Describe(2,activeProfile,FertilitySetting.Abundant)]);
  var filledOk=ChkTableSeeds.IsEnabled&&ChkTableSeeds.Content?.ToString()?.Contains('2')==true;
  ChkTableSeeds.IsChecked=true;var onOk=!TxtFirstSeed.IsEnabled&&!TxtMaxSeed.IsEnabled&&!BtnMinSeed.IsEnabled;
  SetRunning(true);var runningOk=!ChkTableSeeds.IsEnabled&&ChkTableSeeds.IsChecked==true;SetRunning(false);var backOk=ChkTableSeeds.IsEnabled&&!TxtFirstSeed.IsEnabled;
  ChkTableSeeds.IsChecked=false;var offOk=TxtFirstSeed.IsEnabled&&TxtMaxSeed.IsEnabled;
  // A search over the table that finds nothing brings the previous list back.
  var before=GridResults.Items.OfType<SearchResultRow>().ToArray();ShowResults([]);var clearedOk=GridResults.Items.Count==0;RestoreTable(before,[]);
  var restoredOk=clearedOk&&GridResults.Items.Count==before.Length&&BtnExportCsv.IsEnabled&&ChkTableSeeds.IsEnabled;
  ShowResults([]);return emptyOk&&filledOk&&onOk&&runningOk&&backOk&&offOk&&restoredOk&&!ChkTableSeeds.IsEnabled;
 }
 // The advanced filters, after the preset with a Murex minimum of 45,000 tiles has been applied: shown and applied only while the one checkbox is on.
 internal bool SmokeAdvancedFilters()
 {
  var murex=advancedRows[(int)AdvancedFilter.LatiumHarbourMurex];var beaver=advancedRows[(int)AdvancedFilter.AlbionMarshBeaver];
  var shownOk=ChkAdvanced.IsChecked==true&&AdvancedFilterPanel.Visibility==Visibility.Visible&&AdvancedColumns.All(column=>column.Visibility==Visibility.Visible);
  var loadedOk=murex.Enabled.IsChecked==true&&(int)murex.Slider.Value*AdvancedStep==45_000&&beaver.Enabled.IsChecked!=true&&(int)beaver.Slider.Value*AdvancedStep==30_000;
  var minimums=ActiveAdvancedMinimums();var appliedOk=minimums.Length==AdvancedFilters.Count&&minimums[(int)AdvancedFilter.LatiumHarbourMurex]==45_000&&minimums.Count(value=>value!=0)==1;
  murex.Median.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));var medianOk=(int)murex.Slider.Value==(int)murex.Median.Tag!;
  ChkAdvanced.IsChecked=false;var hiddenOk=AdvancedFilterPanel.Visibility==Visibility.Collapsed&&AdvancedColumns.All(column=>column.Visibility==Visibility.Collapsed)&&ActiveAdvancedMinimums().All(value=>value==0);
  ChkAdvanced.IsChecked=true;
  // The second toggle at the top of the additional-conditions card mirrors the one above the table, in both directions.
  var cardFollowsOk=ChkAdvancedCard.IsChecked==true;
  ChkAdvancedCard.IsChecked=false;var cardHidesOk=ChkAdvanced.IsChecked==false&&AdvancedFilterPanel.Visibility==Visibility.Collapsed;
  ChkAdvanced.IsChecked=true;var topShowsCardOk=ChkAdvancedCard.IsChecked==true;
  return shownOk&&loadedOk&&appliedOk&&medianOk&&hiddenOk&&cardFollowsOk&&cardHidesOk&&topShowsCardOk;
 }
 // Seed scoring (see ScoreMetrics.cs/SCORING-PLAN.md): weights are independent of any hard filter, an unscored
 // search shows "-" rather than 0, out-of-range values clamp unless the outlier checkbox is on, and ties at the
 // same rounded score are broken by whichever metric currently has the highest weight.
 internal bool SmokeScoring()
 {
  // The preset applied earlier in this test run turned scoring on with LatiumArea=8 and MarbleSites=5.
  var presetOk=ChkScoring.IsChecked==true&&(int)scoreRows.First(row=>row.Metric==ScoreMetric.LatiumArea).Slider.Value==8&&(int)scoreRows.First(row=>row.Metric==ScoreMetric.MarbleSites).Slider.Value==5;

  // The guiding example from the design discussion: Cinis fertility is not a hard filter here, but Murex/Oysters
  // (weight 10) are worth more than Sturgeon/Gold (weight 7), so the seed that has the former should win.
  foreach(var row in scoreRows)row.Slider.Value=0;
  scoreRows.First(row=>row.Metric==ScoreMetric.CinisMurex).Slider.Value=10;scoreRows.First(row=>row.Metric==ScoreMetric.CinisOysters).Slider.Value=10;
  scoreRows.First(row=>row.Metric==ScoreMetric.CinisSturgeon).Slider.Value=7;scoreRows.First(row=>row.Metric==ScoreMetric.CinisGold).Slider.Value=7;
  var latium=new RegionMetrics(new SiteCounts(150,150),0,0,0,0,0,570_000,0);var albion=new RegionMetrics(new SiteCounts(110,0),0,0,0,0,0,230_000,88_000);
  // Fertility IDs must be real (icon-bearing) values, never 0 - a generated seed's Cinis pool never leaves a slot empty.
  var hitA=new SearchHit(1001,[2206,4062,4049,4051,2208,2205,2202],new SiteCounts(19,23),latium,albion);
  var hitB=new SearchHit(1002,[2206,4062,4049,8577,32027,2205,2202],new SiteCounts(10,10),latium,albion);
  ShowResults([hitA,hitB]);
  var rows=GridResults.Items.OfType<SearchResultRow>().ToArray();
  var rowA=rows.Single(row=>row.Seed==1001);var rowB=rows.Single(row=>row.Seed==1002);
  // score = 10*(10+10)/34 = 5.9 ; 10*(7+7)/34 = 4.1
  var guidingExampleOk=rowA.Score==5.9&&rowB.Score==4.1&&rowA.Score>rowB.Score;

  // The master switch clears every score without touching the weights themselves, and restores them when turned
  // back on - it never re-runs the search, just the display-time combination.
  ChkScoring.IsChecked=false;var offOk=rows.All(row=>row.Score is null&&row.ScoreDisplay=="–");
  ChkScoring.IsChecked=true;var backOnOk=GridResults.Items.OfType<SearchResultRow>().All(row=>row.Score is not null);

  // Outlier extension: the 100k-seed range is a sample, so two seeds beyond it both clamp to the same value unless
  // the range is extended to fit this search's own hits, at which point the larger one pulls ahead.
  foreach(var row in scoreRows)row.Slider.Value=0;
  scoreRows.First(row=>row.Metric==ScoreMetric.LatiumArea).Slider.Value=10;
  var range=ScoreMetrics.PopulationRange(ScoreMetric.LatiumArea,activeProfile)!.Value;
  var noAlbion=new RegionMetrics(new SiteCounts(0,0),0,0,0,0,0,0,0);
  var outlierLow=new RegionMetrics(new SiteCounts(0,0),0,0,0,0,0,(int)range.Max+500,0);
  var outlierHigh=new RegionMetrics(new SiteCounts(0,0),0,0,0,0,0,(int)range.Max+5000,0);
  ChkScoreOutliers.IsChecked=false;ShowResults([new(2001,[],default,outlierLow,noAlbion),new(2002,[],default,outlierHigh,noAlbion)]);
  var clamped=GridResults.Items.OfType<SearchResultRow>().ToArray();
  var clampedRowA=clamped.Single(row=>row.Seed==2001);var clampedRowB=clamped.Single(row=>row.Seed==2002);
  // The sort key must be a genuine tie here too (not just the displayed score), or Shift+click on another column
  // to break the tie - the whole point of a tied score - would have nothing left to actually resolve.
  var clampedTieOk=clampedRowA.Score==clampedRowB.Score&&clampedRowA.ScoreSortKey==clampedRowB.ScoreSortKey;
  ChkScoreOutliers.IsChecked=true;
  var extended=GridResults.Items.OfType<SearchResultRow>().ToArray();
  var extendedDiffersOk=extended.Single(row=>row.Seed==2002).Score>extended.Single(row=>row.Seed==2001).Score;

  ChkScoreOutliers.IsChecked=false;foreach(var row in scoreRows)row.Slider.Value=0;ShowResults([]);
  return presetOk&&guidingExampleOk&&offOk&&backOnOk&&clampedTieOk&&extendedDiffersOk;
 }
 // Preview aid for the progress row with a message as long as a real "no hits, table restored" line on a huge range.
 internal void SmokeLongProgressLabel(){SearchProgress.Value=100;LblProgress.Text=Localization.Instance.Format("NoHitsRestoredFormat",(20_000_000).ToString("N0"));}
 internal void SmokeScrollFilters(){UpdateLayout();var top=AdvancedFilterPanel.TranslatePoint(new Point(0,0),FilterScroll).Y;FilterScroll.ScrollToVerticalOffset(FilterScroll.VerticalOffset+top-30);UpdateLayout();}
 internal void SmokeScrollToEnd(){UpdateLayout();FilterScroll.ScrollToEnd();UpdateLayout();}
 internal void SmokeShowAdvanced(){ChkAdvanced.IsChecked=true;foreach(var row in advancedRows)row.Enabled.IsChecked=true;ShowResults([SeedSearcher.Describe(6153,activeProfile,FertilitySetting.Abundant),SeedSearcher.Describe(1,activeProfile,FertilitySetting.Abundant),SeedSearcher.Describe(2,activeProfile,FertilitySetting.Abundant)]);}
 // Switching the language must also refresh labels that are set imperatively in code rather than through a XAML binding.
 internal bool SmokeLanguageRefresh()
 {
  ShowResults([SeedSearcher.Describe(1,activeProfile,FertilitySetting.Abundant),SeedSearcher.Describe(2,activeProfile,FertilitySetting.Abundant)]);
  var germanOk=ChkTableSeeds.Content?.ToString()?.Contains("Ergebnistabelle")==true;
  var wasEnglish=CmbLanguage.SelectedIndex;CmbLanguage.SelectedIndex=1;LanguageChanged(CmbLanguage,null!);
  var englishOk=ChkTableSeeds.Content?.ToString()?.Contains("results table")==true&&ChkTableSeeds.Content.ToString()!.Contains('2');
  CmbLanguage.SelectedIndex=wasEnglish;LanguageChanged(CmbLanguage,null!);ShowResults([]);
  return germanOk&&englishOk;
 }
 // A HeaderStyle or CellStyle on a DataGridColumn (instead of a plain Header/CellTemplate) silently breaks the grid's
 // native Shift+click multi-sort for every column, not just its own - regression guard so it's never set again.
 internal bool SmokeColumnStyles()=>GridResults.Columns.All(column=>column.HeaderStyle is null&&column.CellStyle is null);
 static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
 {
  var count=VisualTreeHelper.GetChildrenCount(root);
  for(var index=0;index<count;index++)
  {
   var child=VisualTreeHelper.GetChild(root,index);
   yield return child;
   foreach(var descendant in VisualDescendants(child))yield return descendant;
  }
 }
 // Every result-table filter column carries a "where does this seed stand" gauge tooltip. Checks both the data
 // (every numeric metric present, values matching) and the real binding (the actual cell's ToolTip resolves to a
 // RangeGauge at runtime, proving XAML's Gauges[LatiumArea] indexer syntax actually finds the dictionary entry).
 internal bool SmokeGaugeTooltips()
 {
  var latium=new RegionMetrics(new SiteCounts(0,0),0,0,0,0,0,571_940,0);
  var albion=new RegionMetrics(new SiteCounts(0,0),0,0,0,0,0,0,0);
  ShowResults([new SearchHit(9001,[],default,latium,albion)]);
  UpdateLayout();
  var row=GridResults.Items.OfType<SearchResultRow>().Single();
  var dictionaryOk=ScoreMetrics.NumericMetrics.All(metric=>row.Gauges.ContainsKey(metric.ToString()));
  var range=ScoreMetrics.PopulationRange(ScoreMetric.LatiumArea,activeProfile)!.Value;
  var dataOk=row.Gauges["LatiumArea"].Value==571_940&&row.Gauges["LatiumArea"].Range.Min==range.Min&&row.Gauges["LatiumArea"].Range.Max==range.Max;
  var cell=VisualDescendants(GridResults).OfType<DataGridCell>().First(candidate=>candidate.Column?.SortMemberPath=="LatiumAreaValue"&&ReferenceEquals(candidate.DataContext,row));
  var textBlock=VisualDescendants(cell).OfType<TextBlock>().First();
  var bindingOk=textBlock.ToolTip is RangeGauge gauge&&gauge.Value==571_940;
  ShowResults([]);
  return dictionaryOk&&dataOk&&bindingOk;
 }
 internal bool SmokeDlcToggle(){ChkDlc01.IsChecked=false;var hidden=!activeProfile.Dlc01&&CinisCard.Visibility==Visibility.Collapsed&&CinisResultsColumn.Visibility==Visibility.Collapsed&&Grid.GetColumnSpan(SearchRangeCard)==2&&!CmbSlot1.IsEnabled;ChkDlc01.IsChecked=true;return hidden&&activeProfile.Dlc01&&CinisCard.Visibility==Visibility.Visible&&CinisResultsColumn.Visibility==Visibility.Visible&&CmbSlot1.IsEnabled;}
 // The option needs seeds in the table; its label shows how many. While a search runs it stays disabled and keeps its label.
 void UpdateTableSeedsOption()
 {
  if(ChkTableSeeds is null)return;
  if(running){ChkTableSeeds.IsEnabled=false;return;}
  var count=GridResults.Items.Count;ChkTableSeeds.Content=Localization.Instance.Format("TableSeedsFormat",count.ToString("N0"));
  if(count==0)ChkTableSeeds.IsChecked=false;
  ChkTableSeeds.IsEnabled=count>0;
 }
 // Switching the option on takes the seed range out of play; off gives it back.
 void TableSeedsChanged(object sender,RoutedEventArgs e)
 {
  var rangeEditable=ChkTableSeeds.IsChecked!=true;BtnMinSeed.IsEnabled=rangeEditable;BtnMaxSeed.IsEnabled=rangeEditable;TxtFirstSeed.IsEnabled=rangeEditable;TxtMaxSeed.IsEnabled=rangeEditable;
 }
 // The search writes its hits to the output file. If that is the seed list that was just loaded, ask once before replacing it.
 bool ConfirmOverwriteOfLoadedList()
 {
  if(loadedSeedListPath is null||overwriteConfirmed||!string.Equals(Path.GetFullPath(TxtOutput.Text),loadedSeedListPath,StringComparison.OrdinalIgnoreCase))return true;
  var answer=MessageBox.Show(this,Localization.Instance.Format("OverwriteLoadedListFormat",loadedSeedListPath),Localization.Instance["OverwriteLoadedListTitle"],MessageBoxButton.YesNo,MessageBoxImage.Question);
  overwriteConfirmed=answer==MessageBoxResult.Yes;return overwriteConfirmed;
 }
 void SetRunning(bool value){running=value;BtnRun.IsEnabled=!value;BtnPreview.IsEnabled=!value;BtnCancel.IsEnabled=value;BtnLoadSeedList.IsEnabled=!value;BtnAddSeedToTable.IsEnabled=!value;BtnExportCsv.IsEnabled=!value&&GridResults.Items.Count>0;BtnLoadPreset.IsEnabled=!value;BtnSavePreset.IsEnabled=!value;var rangeEditable=!value&&ChkTableSeeds.IsChecked!=true;BtnMinSeed.IsEnabled=rangeEditable;BtnMaxSeed.IsEnabled=rangeEditable;TxtPreviewSeed.IsEnabled=!value;TxtFirstSeed.IsEnabled=rangeEditable;TxtMaxSeed.IsEnabled=rangeEditable;UpdateTableSeedsOption();TxtThreads.IsEnabled=!value;TxtLimit.IsEnabled=!value;TxtOutput.IsEnabled=!value;CmbStartMode.IsEnabled=!value;ChkDlc01.IsEnabled=!value;CmbFertilitySetting.IsEnabled=!value;CmbMapTemplate.IsEnabled=!value;CmbMapSize.IsEnabled=!value;CmbSlot1.IsEnabled=!value&&activeProfile.Dlc01;foreach(var box in cinisChecks)box.IsEnabled=!value&&activeProfile.Dlc01;ChkMaxSites.IsEnabled=!value&&activeProfile.Dlc01;ConditionsArea.IsEnabled=!value;}
 void CancelSearch(object sender,RoutedEventArgs e)=>cancellation?.Cancel();
 void OpenOutput(object sender,RoutedEventArgs e){if(lastOutput is not null&&File.Exists(lastOutput))Process.Start(new ProcessStartInfo(lastOutput){UseShellExecute=true});}

 void OpenSeedPreview(object sender,RoutedEventArgs e)
 {
  if(!int.TryParse(TxtPreviewSeed.Text,out var seed)||seed is <SeedLimits.Minimum or >SeedLimits.Maximum){MessageBox.Show(this,Localization.Instance.Format("InvalidSeedFormat",SeedLimits.Minimum.ToString("N0"),SeedLimits.Maximum.ToString("N0")),Localization.Instance["InvalidSeedTitle"],MessageBoxButton.OK,MessageBoxImage.Information);return;}
  ShowPreview((uint)seed);
 }
 void PreviewSelectedResult(object sender,System.Windows.Input.MouseButtonEventArgs e){if(GridResults.SelectedItem is SearchResultRow row)ShowPreview(row.Seed);}
 void ShowPreview(uint seed){new SeedPreviewWindow(seed,activeProfile,CurrentFertilitySetting(),CurrentSlotSetting()){Owner=this}.ShowDialog();}
 FertilitySetting CurrentFertilitySetting()=>(FertilitySetting)CmbFertilitySetting.SelectedIndex;
 SlotSetting CurrentSlotSetting()=>(SlotSetting)CmbSlotSetting.SelectedIndex;

 void MapProfileChanged(object sender,SelectionChangedEventArgs e)
 {
  if(!profileControlsReady||CmbMapTemplate.SelectedItem is not ProfileChoice<MapTemplateKind> template||CmbMapSize.SelectedItem is not ProfileChoice<MapSizeKind> size)return;
  var selected=MapProfiles.Get(template.Value,size.Value,ChkDlc01.IsChecked==true,ChkDlc01.IsChecked==true&&ChkDlcRetro.IsChecked==true);if(selected==activeProfile)return;
  activeProfile=selected;
  LatiumConditions.Children.Clear();AlbionConditions.Children.Clear();GridResults.ItemsSource=null;
  RefreshProfileLabels();UpdateConditionAddButtons();
 }

 void DlcProfileChanged(object sender,RoutedEventArgs e)
 {
  if(!profileControlsReady)return;RefreshDlcPresentation();
  if(CmbMapTemplate.SelectedItem is ProfileChoice<MapTemplateKind> template&&CmbMapSize.SelectedItem is ProfileChoice<MapSizeKind> size)
  {
   activeProfile=MapProfiles.Get(template.Value,size.Value,ChkDlc01.IsChecked==true,ChkDlc01.IsChecked==true&&ChkDlcRetro.IsChecked==true);LatiumConditions.Children.Clear();AlbionConditions.Children.Clear();GridResults.ItemsSource=null;RefreshProfileLabels();UpdateConditionAddButtons();
  }
 }
 void StartModeChanged(object sender,SelectionChangedEventArgs e){if(profileControlsReady)GridResults.ItemsSource=null;}
 void RefreshDlcPresentation()
 {
  var visible=ChkDlc01.IsChecked==true;ChkDlcRetro.IsEnabled=visible;if(!visible)ChkDlcRetro.IsChecked=false;CinisCard.Visibility=visible?Visibility.Visible:Visibility.Collapsed;CinisResultsColumn.Visibility=visible?Visibility.Visible:Visibility.Collapsed;Grid.SetColumnSpan(SearchRangeCard,visible?1:2);CmbSlot1.IsEnabled=visible;foreach(var box in cinisChecks)box.IsEnabled=visible;ChkMaxSites.IsEnabled=visible;
  RefreshScoreVisibility();RecomputeScores();
 }

 void RefreshProfileLabels()
 {
  LblLatiumProfile.Text=ProfileLabel(RegionKind.Latium);
  LblAlbionProfile.Text=ProfileLabel(RegionKind.Albion);
  var ranges=AggregateSiteRanges.For(activeProfile);
  ConfigureMinimumChoices(CmbMinGoldSites,ranges.GoldMin,ranges.GoldMax,ranges.GoldMedian);ConfigureMinimumChoices(CmbMinSturgeonSites,ranges.SturgeonMin,ranges.SturgeonMax,ranges.SturgeonMedian);
  ConfigureMinimumChoices(CmbMinLatiumMountainSites,ranges.LatiumMountainMin,ranges.LatiumMountainMax,ranges.LatiumMountainMedian);ConfigureMinimumChoices(CmbMinLatiumRiverSites,ranges.LatiumRiverMin,ranges.LatiumRiverMax,ranges.LatiumRiverMedian);ConfigureMinimumChoices(CmbMinAlbionMountainSites,ranges.AlbionMountainMin,ranges.AlbionMountainMax,ranges.AlbionMountainMedian);
  ConfigureMinimumChoices(CmbMinMineralMines,ranges.MineralMin,ranges.MineralMax,ranges.MineralMedian);ConfigureMinimumChoices(CmbMinCopperMines,ranges.CopperMin,ranges.CopperMax,ranges.CopperMedian);ConfigureMinimumChoices(CmbMinSilverMines,ranges.SilverMin,ranges.SilverMax,ranges.SilverMedian);ConfigureMinimumChoices(CmbMinTinMines,ranges.TinMin,ranges.TinMax,ranges.TinMedian);
  LblMineralMineRange.Text=StatisticsLabel(ranges.MineralMin,ranges.MineralMax,ranges.MineralAverage,ranges.MineralMedian);LblCopperMineRange.Text=StatisticsLabel(ranges.CopperMin,ranges.CopperMax,ranges.CopperAverage,ranges.CopperMedian);LblSilverMineRange.Text=StatisticsLabel(ranges.SilverMin,ranges.SilverMax,ranges.SilverAverage,ranges.SilverMedian);LblTinMineRange.Text=StatisticsLabel(ranges.TinMin,ranges.TinMax,ranges.TinAverage,ranges.TinMedian);
  ConfigureMinimumChoices(CmbMinGoldMines,ranges.GoldMineMin,ranges.GoldMineMax,ranges.GoldMineMedian);LblGoldMineRange.Text=StatisticsLabel(ranges.GoldMineMin,ranges.GoldMineMax,ranges.GoldMineAverage,ranges.GoldMineMedian);BtnGoldMinesMedian.Tag=(int)ranges.GoldMineMedian;
  ConfigureMinimumChoices(CmbMinMarbleSites,ranges.MarbleMin,ranges.MarbleMax,ranges.MarbleMedian);LblMarbleSiteRange.Text=StatisticsLabel(ranges.MarbleMin,ranges.MarbleMax,ranges.MarbleAverage,ranges.MarbleMedian);BtnMarbleSitesMedian.Tag=(int)ranges.MarbleMedian;
  BtnMineralMinesMedian.Tag=(int)ranges.MineralMedian;BtnCopperMinesMedian.Tag=(int)ranges.CopperMedian;BtnSilverMinesMedian.Tag=(int)ranges.SilverMedian;BtnTinMinesMedian.Tag=(int)ranges.TinMedian;
  LblGoldSiteRange.Text=StatisticsLabel(ranges.GoldMin,ranges.GoldMax,ranges.GoldAverage,ranges.GoldMedian);LblSturgeonSiteRange.Text=StatisticsLabel(ranges.SturgeonMin,ranges.SturgeonMax,ranges.SturgeonAverage,ranges.SturgeonMedian);
  LblLatiumMountainSiteRange.Text=StatisticsLabel(ranges.LatiumMountainMin,ranges.LatiumMountainMax,ranges.LatiumMountainAverage,ranges.LatiumMountainMedian);LblLatiumRiverSiteRange.Text=StatisticsLabel(ranges.LatiumRiverMin,ranges.LatiumRiverMax,ranges.LatiumRiverAverage,ranges.LatiumRiverMedian);LblAlbionMountainSiteRange.Text=StatisticsLabel(ranges.AlbionMountainMin,ranges.AlbionMountainMax,ranges.AlbionMountainAverage,ranges.AlbionMountainMedian);
  BtnGoldSitesMedian.Tag=(int)ranges.GoldMedian;BtnSturgeonSitesMedian.Tag=(int)ranges.SturgeonMedian;BtnLatiumMountainSitesMedian.Tag=(int)ranges.LatiumMountainMedian;BtnLatiumRiverSitesMedian.Tag=(int)ranges.LatiumRiverMedian;BtnAlbionMountainSitesMedian.Tag=(int)ranges.AlbionMountainMedian;
  ConfigureAreaSlider(SldMinLatiumArea,ranges.LatiumAreaMin,ranges.LatiumAreaMax);ConfigureAreaSlider(SldMinAlbionArea,ranges.AlbionAreaMin,ranges.AlbionAreaMax);ConfigureAreaSlider(SldMinAlbionSwampArea,ranges.AlbionSwampMin,ranges.AlbionSwampMax);
  LblLatiumAreaRange.Text=AreaRangeLabel(ranges.LatiumAreaMin,ranges.LatiumAreaMax,ranges.LatiumAreaAverage,ranges.LatiumAreaMedian);LblAlbionAreaRange.Text=AreaRangeLabel(ranges.AlbionAreaMin,ranges.AlbionAreaMax,ranges.AlbionAreaAverage,ranges.AlbionAreaMedian);LblAlbionSwampAreaRange.Text=AreaRangeLabel(ranges.AlbionSwampMin,ranges.AlbionSwampMax,ranges.AlbionSwampAverage,ranges.AlbionSwampMedian);
  BtnLatiumAreaMedian.Tag=ranges.LatiumAreaMedian;BtnAlbionAreaMedian.Tag=ranges.AlbionAreaMedian;BtnAlbionSwampAreaMedian.Tag=ranges.AlbionSwampMedian;
  RefreshAdvancedFilters();
 }
 // Slider range, median button and statistics of every advanced filter for the current profile. An existing setting is kept and clamped to the new range.
 void RefreshAdvancedFilters()
 {
  foreach(var row in advancedRows)
  {
   var range=AdvancedRanges.For(activeProfile,row.Filter);
   var minimum=range.Min/AdvancedStep;var maximum=(range.Max+AdvancedStep-1)/AdvancedStep;var median=Math.Clamp((int)range.Median/AdvancedStep,minimum,maximum);
   var requested=row.Slider.Tag is int stored?stored:configuredAdvanced.Contains(row.Filter)?(int)row.Slider.Value:median;
   row.Slider.Tag=null;row.Slider.Minimum=minimum;row.Slider.Maximum=maximum;row.Slider.Value=Math.Clamp(requested,minimum,maximum);configuredAdvanced.Add(row.Filter);
   row.Value.Text=((int)row.Slider.Value*AdvancedStep).ToString("N0");row.Median.Tag=median;
   row.Range.Text=$"100k: {range.Min:N0}–{range.Max:N0} · Ø {range.Average:N0} · Median {range.Median:N0}";
  }
 }
 void AdvancedSliderValueChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
 {
  var row=advancedRows.FirstOrDefault(candidate=>candidate.Slider==sender);
  if(row is not null)row.Value.Text=((int)e.NewValue*AdvancedStep).ToString("N0");
 }
 void SelectAdvancedMedian(object sender,RoutedEventArgs e)
 {
  var row=advancedRows.FirstOrDefault(candidate=>candidate.Median==sender);
  if(row?.Median.Tag is int median)row.Slider.Value=median;
 }
 static string StatisticsLabel(int minimum,int maximum,double average,double median)=>$"100k: {minimum}–{maximum} · Ø {average:F1} · Median {median:F0}";
 static int CurrentMinimum(ComboBox box)=>(box.SelectedItem as MinimumSiteChoice)?.Minimum??0;
 static int ActiveMinimum(CheckBox enabled,ComboBox box)=>enabled.IsChecked==true?CurrentMinimum(box):0;
 static int ActiveAreaMinimum(CheckBox enabled,Slider slider)=>enabled.IsChecked==true?checked((int)slider.Value*1000):0;
 static string MinimumText(ComboBox box)=>CurrentMinimum(box).ToString();
 static bool PresetFilterEnabled(bool? enabled,string value)=>enabled??value!="*";
 static void ConfigureMinimumChoices(ComboBox box,int minimum,int maximum,double defaultValue)
 {
  var current=box.Tag as string??(box.SelectedItem as MinimumSiteChoice)?.Minimum.ToString();box.Tag=null;
  var requested=int.TryParse(current,out var parsed)&&parsed>0?parsed:(int)Math.Round(defaultValue,MidpointRounding.AwayFromZero);
  var choices=Enumerable.Range(minimum,maximum-minimum+1).Select(value=>new MinimumSiteChoice(value.ToString(),value)).ToArray();
  box.ItemsSource=choices;box.SelectedItem=choices.FirstOrDefault(choice=>choice.Minimum==requested)??choices.OrderBy(choice=>Math.Abs(choice.Minimum-requested)).First();
 }
 static void ConfigureAreaSlider(Slider slider,int minimum,int maximum)
 {
  var requested=slider.Tag is int tagged&&tagged>0?tagged:(int)slider.Value;slider.Tag=null;
  slider.Minimum=minimum;slider.Maximum=maximum;slider.Value=Math.Clamp(requested>0?requested:minimum,minimum,maximum);
 }
 static string AreaRangeLabel(int minimum,int maximum,int average,int median)=>$"100k: {minimum}k–{maximum}k · Ø {average}k · Median {median}k";
 void SelectSiteMedian(object sender,RoutedEventArgs e)
 {
  if(sender is not Button button||button.Tag is not int median)return;
  var box=button==BtnGoldSitesMedian?CmbMinGoldSites:button==BtnSturgeonSitesMedian?CmbMinSturgeonSites:button==BtnLatiumMountainSitesMedian?CmbMinLatiumMountainSites:button==BtnLatiumRiverSitesMedian?CmbMinLatiumRiverSites:button==BtnGoldMinesMedian?CmbMinGoldMines:button==BtnMarbleSitesMedian?CmbMinMarbleSites:button==BtnMineralMinesMedian?CmbMinMineralMines:button==BtnCopperMinesMedian?CmbMinCopperMines:button==BtnSilverMinesMedian?CmbMinSilverMines:button==BtnTinMinesMedian?CmbMinTinMines:CmbMinAlbionMountainSites;
  box.SelectedItem=box.Items.OfType<MinimumSiteChoice>().First(choice=>choice.Minimum==median);
 }
 void SelectAreaMedian(object sender,RoutedEventArgs e)
 {
  if(sender is not Button button||button.Tag is not int median)return;
  var slider=button==BtnLatiumAreaMedian?SldMinLatiumArea:button==BtnAlbionAreaMedian?SldMinAlbionArea:SldMinAlbionSwampArea;slider.Value=median;
 }
 void AreaSliderValueChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
 {
  if(sender==SldMinLatiumArea&&LblMinLatiumArea is not null)LblMinLatiumArea.Text=$"{(int)e.NewValue}k";
  else if(sender==SldMinAlbionArea&&LblMinAlbionArea is not null)LblMinAlbionArea.Text=$"{(int)e.NewValue}k";
  else if(sender==SldMinAlbionSwampArea&&LblMinAlbionSwampArea is not null)LblMinAlbionSwampArea.Text=$"{(int)e.NewValue}k";
 }
 void AreaSliderKeyDown(object sender,System.Windows.Input.KeyEventArgs e)
 {
  if(sender is not Slider slider)return;
  var direction=e.Key is System.Windows.Input.Key.Add or System.Windows.Input.Key.OemPlus?1:e.Key is System.Windows.Input.Key.Subtract or System.Windows.Input.Key.OemMinus?-1:0;
  if(direction==0)return;slider.Value=Math.Clamp(slider.Value+direction*slider.SmallChange,slider.Minimum,slider.Maximum);e.Handled=true;
 }
 string ProfileLabel(RegionKind region)
 {
  var secondary=activeProfile.Capacity(region,FertilitySetKind.Secondary);var tertiary=activeProfile.Capacity(region,FertilitySetKind.Tertiary);
  var dlc=region==RegionKind.Latium?(activeProfile.Retro?" · PoA (+)":activeProfile.Dlc01?" · PoA":Localization.Instance["WithoutPoASuffix"]):"";
  return Localization.Instance.Format("ProfileLabelFormat",region,activeProfile.Template,activeProfile.Size,dlc,activeProfile.StarterCount(region),Localization.Instance["UpToWord"],secondary,tertiary);
 }

 // Fires while the ComboBox is still being parsed (its initial SelectedIndex is a real change from "none"), long
 // before later-declared fields such as ChkScoring exist yet - guarded the same way MapProfileChanged is.
 void Slot1Changed(object sender,SelectionChangedEventArgs e){if(!profileControlsReady)return;RefreshScoreVisibility();RecomputeScores();}

 void CinisFertilityChecked(object sender,RoutedEventArgs e)
 {
  if(cinisChecks.Count(x=>x.IsChecked==true)<=4)return;
  if(sender is CheckBox box)box.IsChecked=false;
  TxtCinisWarning.Visibility=Visibility.Visible;cinisWarningTimer.Stop();cinisWarningTimer.Start();
 }

 void AddLatiumCondition(object sender,RoutedEventArgs e)=>AddCondition(RegionKind.Latium,SetFromButton(sender),LatiumConditions);
 void AddAlbionCondition(object sender,RoutedEventArgs e)=>AddCondition(RegionKind.Albion,SetFromButton(sender),AlbionConditions);
 static FertilitySetKind SetFromButton(object sender)=>Enum.Parse<FertilitySetKind>(((Button)sender).Tag.ToString()!);
 ConditionRowControl AddCondition(RegionKind region,FertilitySetKind set,Panel target)
 {
  var maximum=activeProfile.Capacity(region,set);var row=new ConditionRowControl(FertilityDefinitions.Get(region,set),activeProfile,maximum);row.RemoveRequested+=(_,_)=>{target.Children.Remove(row);UpdateConditionAddButtons();};row.MinimumChanged+=(_,_)=>UpdateConditionAddButtons();target.Children.Add(row);UpdateConditionAddButtons();return row;
 }
 void UpdateConditionAddButtons()
 {
  RefreshMinimumLimits(RegionKind.Latium,LatiumConditions);
  RefreshMinimumLimits(RegionKind.Albion,AlbionConditions);
  UpdateAddButton(BtnAddLatiumStarter,RegionKind.Latium,FertilitySetKind.Starter,LatiumConditions);
  UpdateAddButton(BtnAddLatiumSecondary,RegionKind.Latium,FertilitySetKind.Secondary,LatiumConditions);
  UpdateAddButton(BtnAddLatiumTertiary,RegionKind.Latium,FertilitySetKind.Tertiary,LatiumConditions);
  UpdateAddButton(BtnAddLatiumAnyCombination,RegionKind.Latium,FertilitySetKind.AnyCombination,LatiumConditions);
  UpdateAddButton(BtnAddAlbionStarter,RegionKind.Albion,FertilitySetKind.Starter,AlbionConditions);
  UpdateAddButton(BtnAddAlbionSecondary,RegionKind.Albion,FertilitySetKind.Secondary,AlbionConditions);
  UpdateAddButton(BtnAddAlbionTertiary,RegionKind.Albion,FertilitySetKind.Tertiary,AlbionConditions);
  UpdateAddButton(BtnAddAlbionAnyCombination,RegionKind.Albion,FertilitySetKind.AnyCombination,AlbionConditions);
 }
 void RefreshMinimumLimits(RegionKind region,Panel target)
 {
  foreach(var set in Enum.GetValues<FertilitySetKind>())
  {
   var rows=target.Children.OfType<ConditionRowControl>().Where(row=>row.Set==set).ToArray();
   var capacity=activeProfile.Capacity(region,set);
   foreach(var row in rows)
   {
    var others=rows.Where(other=>other!=row).Sum(other=>other.Minimum);
    row.SetMaximumMinimum(capacity-others);
   }
  }
 }
 void UpdateAddButton(Button button,RegionKind region,FertilitySetKind set,Panel target)
 {
  var maximum=activeProfile.Capacity(region,set);
  var current=target.Children.OfType<ConditionRowControl>().Count(row=>row.Set==set);
  button.IsEnabled=current<maximum;
  button.ToolTip=Localization.Instance.Format("MaxConditionsTooltipFormat",maximum,set);
 }
 // Whether a metric's weight currently counts at all: advanced-filter metrics need their own panel shown, Cinis
 // metrics need DLC01, and the Cinis slot-1 match additionally needs an actual slot-1 fertility chosen (see
 // SCORING-PLAN.md §11.1) - otherwise every seed would trivially score the same for it.
 Func<bool> ScoreApplicability(ScoreMetric metric)
 {
  if(metric==ScoreMetric.CinisSlot1)return ()=>activeProfile.Dlc01&&(CmbSlot1.SelectedItem as FertilityChoice)?.Guid is > 0u;
  if(ScoreMetrics.IsCinis(metric))return ()=>activeProfile.Dlc01;
  if(ScoreMetrics.IsAdvanced(metric))return ()=>ChkAdvanced.IsChecked==true;
  return ()=>true;
 }
 // Shows or hides every weight control: only while scoring is enabled, and only for metrics that currently apply.
 void RefreshScoreVisibility()
 {
  var enabled=ChkScoring.IsChecked==true;
  foreach(var row in scoreRows)
  {
   var visible=enabled&&row.Applicable();
   row.Slider.Visibility=visible?Visibility.Visible:Visibility.Collapsed;row.Value.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
  }
  // The three small "Weight" column headers follow the same visibility as the rows underneath them.
  AreaWeightHeader.Visibility=enabled?Visibility.Visible:Visibility.Collapsed;
  SiteWeightHeader.Visibility=enabled?Visibility.Visible:Visibility.Collapsed;
  AdvWeightHeader.Visibility=enabled&&ChkAdvanced.IsChecked==true?Visibility.Visible:Visibility.Collapsed;
 }
 void ScoringChanged(object sender,RoutedEventArgs e){RefreshScoreVisibility();RecomputeScores();}
 void ScoringOutliersChanged(object sender,RoutedEventArgs e)=>RecomputeScores();
 void WeightSliderChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
 {
  var row=scoreRows.FirstOrDefault(candidate=>candidate.Slider==sender);
  if(row is not null)row.Value.Text=((int)e.NewValue).ToString();
  RecomputeScores();
 }
 // Only weights whose row is currently applicable count; a hidden or not-applicable row's leftover value is ignored
 // rather than cleared, so it's ready again if the row becomes applicable later (map profile, DLC01, slot 1, ...).
 ScoreWeights CurrentScoreWeights()=>new(scoreRows.Where(row=>row.Applicable()).ToDictionary(row=>row.Metric,row=>(int)row.Slider.Value),ChkScoreOutliers.IsChecked==true);
 void RecomputeScores()
 {
  if(GridResults.ItemsSource is not IEnumerable<SearchResultRow> source)return;
  var rows=source as SearchResultRow[]??source.ToArray();
  if(rows.Length==0)return;
  if(ChkScoring.IsChecked!=true){foreach(var row in rows)row.ClearScore();GridResults.Items.Refresh();return;}
  var slot1Choice=activeProfile.Dlc01?(CmbSlot1.SelectedItem as FertilityChoice)?.Guid??0u:0u;
  SeedScoring.Apply(rows,activeProfile,slot1Choice,CurrentScoreWeights());
  GridResults.Items.Refresh();
 }
 internal void AddPreviewConditions(){AddCondition(RegionKind.Latium,FertilitySetKind.Tertiary,LatiumConditions);AddCondition(RegionKind.Latium,FertilitySetKind.AnyCombination,LatiumConditions);AddCondition(RegionKind.Albion,FertilitySetKind.Secondary,AlbionConditions);AddCondition(RegionKind.Albion,FertilitySetKind.AnyCombination,AlbionConditions);CmbSlot1.SelectedIndex=1;}
}

internal sealed record SearchResultRow(uint Seed,int MarbleSites,int GoldMines,int GoldRiverSites,int SturgeonRiverSites,int MineralMines,int CopperMines,int SilverMines,int TinMines,int LatiumAreaValue,int AlbionAreaValue,int AlbionSwampValue,int LatiumMountain,int LatiumRiver,int AlbionMountain,int[] AdvancedTiles,uint[] CinisFertilityIds,SiteCounts CinisSiteCounts,ImageSource[] CinisFertilities)
{
 // Seed scoring (see ScoreMetrics.cs/SCORING-PLAN.md): computed after the row is built, from settings that live on
 // the window rather than the search, so these are plain mutable properties instead of constructor parameters.
 public double? Score { get; private set; }
 // Exactly the displayed value, not a finer-grained one: seeds shown with the same score must sort as genuine ties,
 // or Shift+click on another column would have nothing left to resolve them by.
 public double ScoreSortKey => Score ?? double.NegativeInfinity;
 public string? ScoreBreakdown { get; private set; }
 public string ScoreDisplay => Score is null ? "–" : Score.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
 public void SetScore(double score, string breakdown) { Score = score; ScoreBreakdown = breakdown; }
 public void ClearScore() { Score = null; ScoreBreakdown = null; }

 // The "where does this seed stand" gauge for every result column, keyed by ScoreMetric name (e.g. "LatiumArea")
 // for XAML's indexer binding syntax. Fixed once at construction: unlike Score it needs no weights or toggles, only
 // the profile the row was generated with, so there is nothing for it to be recomputed against later.
 public IReadOnlyDictionary<string, RangeGauge> Gauges { get; private set; } = new Dictionary<string, RangeGauge>();
 void ComputeGauges(MapProfile profile) => Gauges = ScoreMetrics.NumericMetrics.ToDictionary(metric => metric.ToString(), metric => ScoreMetrics.Gauge(metric, this, profile)!.Value);

 // The advanced filter values, in AdvancedFilter order; the properties give the grid one column each.
 public int LatiumHarbourMurex=>AdvancedTiles[(int)AdvancedFilter.LatiumHarbourMurex];
 public int LatiumHarbourOysters=>AdvancedTiles[(int)AdvancedFilter.LatiumHarbourOysters];
 public int AlbionHarbourSaltwort=>AdvancedTiles[(int)AdvancedFilter.AlbionHarbourSaltwort];
 public int AlbionHarbourSeaShells=>AdvancedTiles[(int)AdvancedFilter.AlbionHarbourSeaShells];
 public int AlbionMarshSmallBirds=>AdvancedTiles[(int)AdvancedFilter.AlbionMarshSmallBirds];
 public int AlbionMarshBeaver=>AdvancedTiles[(int)AdvancedFilter.AlbionMarshBeaver];

 // The grid shows grouped numbers but sorts on the raw values, so "highest area first"
 // orders numerically instead of lexicographically.
 public string LatiumArea=>$"{LatiumAreaValue:N0}";
 public string AlbionArea=>$"{AlbionAreaValue:N0}";
 public string AlbionSwampArea=>$"{AlbionSwampValue:N0}";
 public static SearchResultRow From(SearchHit hit,MapProfile profile)
 {
  var row=new SearchResultRow(hit.Seed,hit.Latium.MarbleSites,hit.Latium.GoldMineSites,hit.Latium.GoldSites,hit.Latium.SturgeonSites,hit.Latium.MineralMineSites,hit.Albion.CopperMineSites,hit.Albion.SilverMineSites,hit.Albion.TinMineSites,hit.Latium.BuildableTiles,hit.Albion.BuildableTiles,hit.Albion.SwampTiles,hit.Latium.Sites.Mountain,hit.Latium.Sites.River,hit.Albion.Sites.Mountain,[..AdvancedFilters.All.Select(definition=>(definition.Region==RegionKind.Latium?hit.Latium:hit.Albion).Tiles(definition.Filter))],hit.Fertilities,hit.CinisSites,[..hit.Fertilities.Select(FertilityIcons.Get)]);
  row.ComputeGauges(profile);
  return row;
 }
}
internal sealed record MinimumSiteChoice(string Label,int Minimum){public override string ToString()=>Label;}
internal sealed record ProfileChoice<T>(string Label,T Value){public override string ToString()=>Label;}
