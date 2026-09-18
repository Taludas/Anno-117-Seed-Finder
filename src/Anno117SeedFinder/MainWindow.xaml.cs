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
 bool profileControlsReady;
 bool formattingSeedInput;
 MapProfile activeProfile=MapProfiles.Default;
 readonly ProfileChoice<MapTemplateKind>[] templateChoices=
 [new("Archipelago",MapTemplateKind.Archipelago),new("Atoll",MapTemplateKind.Atoll),new("Rift",MapTemplateKind.Rift),new("Corners",MapTemplateKind.Corners),new("Island Chains",MapTemplateKind.IslandChains)];
 readonly ProfileChoice<MapSizeKind>[] sizeChoices=[new("Small",MapSizeKind.Small),new("Medium",MapSizeKind.Medium),new("Large",MapSizeKind.Large)];

 public MainWindow()
 {
  InitializeComponent();
  CmbMapTemplate.ItemsSource=templateChoices;CmbMapSize.ItemsSource=sizeChoices;
  CmbMapTemplate.SelectedItem=templateChoices.Single(choice=>choice.Value==MapTemplateKind.Corners);CmbMapSize.SelectedItem=sizeChoices.Single(choice=>choice.Value==MapSizeKind.Large);
  profileControlsReady=true;RefreshDlcPresentation();RefreshProfileLabels();
  TxtThreads.Text=Math.Max(1,Environment.ProcessorCount).ToString();
  TxtOutput.Text=Path.Combine(AppContext.BaseDirectory,"treffer.txt");
  FertilityIcons.Configure(CmbSlot1,[FertilityDefinitions.Choice(RegionKind.Latium,2206),FertilityDefinitions.Choice(RegionKind.Latium,2209)]);CmbSlot1.SelectedIndex=0;
  cinisChecks=[ChkGrapes,ChkFlax,ChkMurex,ChkOysters,ChkSturgeon,ChkGold];
  foreach(var box in cinisChecks)box.Checked+=CinisFertilityChecked;
  cinisWarningTimer.Tick+=(_,_)=>{TxtCinisWarning.Visibility=Visibility.Collapsed;cinisWarningTimer.Stop();};
  ResultColumnsChanged(this,new RoutedEventArgs());
  UpdateConditionAddButtons();
  CmbLanguage.SelectedIndex=Localization.Instance.Language==AppLanguage.English?1:0;
 }

 void LanguageChanged(object sender,SelectionChangedEventArgs e)
 {
  Localization.Instance.Language=CmbLanguage.SelectedIndex==1?AppLanguage.English:AppLanguage.German;
  RefreshProfileLabels();UpdateConditionAddButtons();
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
    Template=activeProfile.Template,Size=activeProfile.Size,StartMode=CmbStartMode.SelectedIndex==1?StartModeKind.StartIsland:StartModeKind.Flagship,Dlc01=activeProfile.Dlc01,FertilitySetting=CmbFertilitySetting.SelectedIndex,
     FirstSeed=SeedDigits(TxtFirstSeed.Text),LastSeed=SeedDigits(TxtMaxSeed.Text),Threads=TxtThreads.Text,MaximumHits=TxtLimit.Text,OutputPath=TxtOutput.Text,PreviewSeed=TxtPreviewSeed.Text,
    MinimumGoldSites=MinimumText(CmbMinGoldSites),MinimumSturgeonSites=MinimumText(CmbMinSturgeonSites),MinimumLatiumMountainSites=MinimumText(CmbMinLatiumMountainSites),MinimumLatiumRiverSites=MinimumText(CmbMinLatiumRiverSites),MinimumAlbionMountainSites=MinimumText(CmbMinAlbionMountainSites),
    GoldSitesEnabled=ChkMinGoldSites.IsChecked==true,SturgeonSitesEnabled=ChkMinSturgeonSites.IsChecked==true,LatiumMountainSitesEnabled=ChkMinLatiumMountainSites.IsChecked==true,LatiumRiverSitesEnabled=ChkMinLatiumRiverSites.IsChecked==true,AlbionMountainSitesEnabled=ChkMinAlbionMountainSites.IsChecked==true,
    MinimumLatiumAreaK=(int)SldMinLatiumArea.Value,MinimumAlbionAreaK=(int)SldMinAlbionArea.Value,MinimumAlbionSwampAreaK=(int)SldMinAlbionSwampArea.Value,LatiumAreaEnabled=ChkMinLatiumArea.IsChecked==true,AlbionAreaEnabled=ChkMinAlbionArea.IsChecked==true,AlbionSwampAreaEnabled=ChkMinAlbionSwampArea.IsChecked==true,
    MinimumGoldMines=MinimumText(CmbMinGoldMines),MinimumMarbleSites=MinimumText(CmbMinMarbleSites),MinimumMineralMines=MinimumText(CmbMinMineralMines),MinimumCopperMines=MinimumText(CmbMinCopperMines),MinimumSilverMines=MinimumText(CmbMinSilverMines),
    GoldMinesEnabled=ChkMinGoldMines.IsChecked==true,MarbleSitesEnabled=ChkMinMarbleSites.IsChecked==true,MineralMinesEnabled=ChkMinMineralMines.IsChecked==true,CopperMinesEnabled=ChkMinCopperMines.IsChecked==true,SilverMinesEnabled=ChkMinSilverMines.IsChecked==true,
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
  CmbMapTemplate.SelectedItem=templateChoices.Single(choice=>choice.Value==preset.Template);CmbMapSize.SelectedItem=sizeChoices.Single(choice=>choice.Value==preset.Size);CmbStartMode.SelectedIndex=preset.StartMode==StartModeKind.StartIsland?1:0;ChkDlc01.IsChecked=preset.Dlc01;activeProfile=MapProfiles.Get(preset.Template,preset.Size,preset.Dlc01);
  CmbFertilitySetting.SelectedIndex=Math.Clamp(preset.FertilitySetting,0,CmbFertilitySetting.Items.Count-1);
  TxtFirstSeed.Text=preset.FirstSeed;TxtMaxSeed.Text=preset.LastSeed;TxtThreads.Text=preset.Threads;TxtLimit.Text=preset.MaximumHits;TxtOutput.Text=preset.OutputPath;TxtPreviewSeed.Text=preset.PreviewSeed;
  CmbMinGoldSites.Tag=preset.MinimumGoldSites;CmbMinSturgeonSites.Tag=preset.MinimumSturgeonSites;CmbMinLatiumMountainSites.Tag=preset.MinimumLatiumMountainSites;CmbMinLatiumRiverSites.Tag=preset.MinimumLatiumRiverSites;CmbMinAlbionMountainSites.Tag=preset.MinimumAlbionMountainSites;
  ChkMinGoldSites.IsChecked=PresetFilterEnabled(preset.GoldSitesEnabled,preset.MinimumGoldSites);ChkMinSturgeonSites.IsChecked=PresetFilterEnabled(preset.SturgeonSitesEnabled,preset.MinimumSturgeonSites);ChkMinLatiumMountainSites.IsChecked=PresetFilterEnabled(preset.LatiumMountainSitesEnabled,preset.MinimumLatiumMountainSites);ChkMinLatiumRiverSites.IsChecked=PresetFilterEnabled(preset.LatiumRiverSitesEnabled,preset.MinimumLatiumRiverSites);ChkMinAlbionMountainSites.IsChecked=PresetFilterEnabled(preset.AlbionMountainSitesEnabled,preset.MinimumAlbionMountainSites);
  SldMinLatiumArea.Tag=preset.MinimumLatiumAreaK;SldMinAlbionArea.Tag=preset.MinimumAlbionAreaK;SldMinAlbionSwampArea.Tag=preset.MinimumAlbionSwampAreaK;ChkMinLatiumArea.IsChecked=preset.LatiumAreaEnabled==true;ChkMinAlbionArea.IsChecked=preset.AlbionAreaEnabled==true;ChkMinAlbionSwampArea.IsChecked=preset.AlbionSwampAreaEnabled==true;
  CmbMinGoldMines.Tag=preset.MinimumGoldMines;CmbMinMarbleSites.Tag=preset.MinimumMarbleSites;CmbMinMineralMines.Tag=preset.MinimumMineralMines;CmbMinCopperMines.Tag=preset.MinimumCopperMines;CmbMinSilverMines.Tag=preset.MinimumSilverMines;
  ChkMinGoldMines.IsChecked=PresetFilterEnabled(preset.GoldMinesEnabled,preset.MinimumGoldMines);ChkMinMarbleSites.IsChecked=PresetFilterEnabled(preset.MarbleSitesEnabled,preset.MinimumMarbleSites);ChkMinMineralMines.IsChecked=preset.MineralMinesEnabled;ChkMinCopperMines.IsChecked=preset.CopperMinesEnabled;ChkMinSilverMines.IsChecked=preset.SilverMinesEnabled;
  CmbSlot1.SelectedItem=(CmbSlot1.Items.OfType<FertilityChoice>().First(choice=>choice.Guid==preset.CinisSlot1));
  foreach(var box in cinisChecks)box.IsChecked=preset.CinisFertilities.Contains(uint.Parse(box.Tag.ToString()!));ChkMaxSites.IsChecked=preset.CinisMaximumSites;
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
  try
  {
   var selected=activeProfile.Dlc01?cinisChecks.Where(x=>x.IsChecked==true).Select(x=>uint.Parse(x.Tag.ToString()!)).ToArray():[];
   var conditions=LatiumConditions.Children.OfType<ConditionRowControl>().Concat(AlbionConditions.Children.OfType<ConditionRowControl>()).Select(x=>x.BuildCondition()).ToArray();
   var cinisSlot1=activeProfile.Dlc01?(CmbSlot1.SelectedItem as FertilityChoice)?.Guid??0u:0u;
   var request=new SearchRequest(ParseSeed(TxtFirstSeed,Localization.Instance["FirstSeed"]),ParseSeed(TxtMaxSeed,Localization.Instance["LastSeed"]),ParsePositive(TxtThreads,Localization.Instance["Threads"]),ParseNonNegative(TxtLimit,Localization.Instance["MaxHits"]),TxtOutput.Text,cinisSlot1,selected,activeProfile.Dlc01&&ChkMaxSites.IsChecked==true,conditions,activeProfile,
    ActiveMinimum(ChkMinGoldSites,CmbMinGoldSites),ActiveMinimum(ChkMinSturgeonSites,CmbMinSturgeonSites),ActiveMinimum(ChkMinLatiumMountainSites,CmbMinLatiumMountainSites),ActiveMinimum(ChkMinLatiumRiverSites,CmbMinLatiumRiverSites),ActiveMinimum(ChkMinAlbionMountainSites,CmbMinAlbionMountainSites),ActiveAreaMinimum(ChkMinLatiumArea,SldMinLatiumArea),ActiveAreaMinimum(ChkMinAlbionArea,SldMinAlbionArea),ActiveAreaMinimum(ChkMinAlbionSwampArea,SldMinAlbionSwampArea),CurrentFertilitySetting(),
    ActiveMinimum(ChkMinMineralMines,CmbMinMineralMines),ActiveMinimum(ChkMinCopperMines,CmbMinCopperMines),ActiveMinimum(ChkMinSilverMines,CmbMinSilverMines),ActiveMinimum(ChkMinMarbleSites,CmbMinMarbleSites),ActiveMinimum(ChkMinGoldMines,CmbMinGoldMines));
   SetRunning(true);GridResults.ItemsSource=null;SearchProgress.Value=0;LblProgress.Text=Localization.Instance["SearchStarting"];
   cancellation=new CancellationTokenSource();
   var reporter=new Progress<SearchProgress>(x=>{SearchProgress.Value=Math.Min(100,(long)x.Processed*100/x.Total);LblProgress.Text=Localization.Instance.Format("ProgressFormat",x.Processed.ToString("N0"),x.Total.ToString("N0"),x.Hits.ToString("N0"));});
   var summary=await SeedSearcher.SearchAsync(request,reporter,cancellation.Token);
   ShowResults(summary.Hits);
   lastOutput=summary.Output;BtnOpen.IsEnabled=true;
   if(summary.Canceled)
   {
    SearchProgress.Value=Math.Min(100,(long)summary.Processed*100/(request.MaxSeed-request.FirstSeed+1));
    LblProgress.Text=Localization.Instance.Format("CanceledFormat",summary.Processed.ToString("N0"),(request.MaxSeed-request.FirstSeed+1).ToString("N0"),summary.FoundCount.ToString("N0"),summary.Strategy);
   }
   else{SearchProgress.Value=100;LblProgress.Text=Localization.Instance.Format("CompletedFormat",summary.FoundCount.ToString("N0"),summary.Elapsed.TotalSeconds.ToString("F2"),summary.Strategy);}
  }
  catch(OperationCanceledException){LblProgress.Text=Localization.Instance["SearchCanceled"];}
  catch(Exception error){LblProgress.Text=Localization.Instance["GenericError"];MessageBox.Show(this,error.Message,Localization.Instance["SearchStartFailedTitle"],MessageBoxButton.OK,MessageBoxImage.Error);}
  finally{cancellation?.Dispose();cancellation=null;SetRunning(false);}
 }

 void ShowResults(IReadOnlyList<SearchHit> hits)
 {
  GridResults.ItemsSource=hits.Select(SearchResultRow.From).ToArray();
  BtnExportCsv.IsEnabled=hits.Count>0;
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
   rows.Add(SearchResultRow.From(SeedSearcher.Describe(seed,activeProfile,CurrentFertilitySetting())));
   // Replacing ItemsSource builds a fresh view, so the active sorting is carried over by hand.
   var sorting=GridResults.Items.SortDescriptions.ToArray();
   GridResults.ItemsSource=rows.ToArray();
   foreach(var description in sorting)GridResults.Items.SortDescriptions.Add(description);
   BtnExportCsv.IsEnabled=true;LblProgress.Text=Localization.Instance.Format("SeedAddedFormat",seed.ToString("N0"),rows.Count.ToString("N0"));
  }
  catch(Exception error){MessageBox.Show(this,error.Message,Localization.Instance["SeedComputeFailedTitle"],MessageBoxButton.OK,MessageBoxImage.Error);}
 }

 void ResultColumnsChanged(object sender,RoutedEventArgs e)
 {
  if(GoldMinesColumn is null)return;
  MarbleSitesColumn.Visibility=ColumnVisibility(ChkColMarble);GoldMinesColumn.Visibility=ColumnVisibility(ChkColGoldMine);GoldRiverColumn.Visibility=ColumnVisibility(ChkColGoldRiver);SturgeonRiverColumn.Visibility=ColumnVisibility(ChkColSturgeon);
  MineralMinesColumn.Visibility=ColumnVisibility(ChkColMinerals);CopperMinesColumn.Visibility=ColumnVisibility(ChkColCopper);SilverMinesColumn.Visibility=ColumnVisibility(ChkColSilver);
 }
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
   var lines=new List<string>{SeedSearcher.CsvHeader};
   lines.AddRange(ordered.Select(row=>string.Join(';',
    row.Seed,row.LatiumAreaValue,row.AlbionAreaValue,row.AlbionSwampValue,
    row.LatiumMountain,row.LatiumRiver,row.AlbionMountain,
    row.GoldRiverSites,row.SturgeonRiverSites,
    row.GoldMines,row.MarbleSites,row.MineralMines,row.CopperMines,row.SilverMines,
    CinisNames(row.Seed),row.NeedsVerification?"ja":"nein")));
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
   var profile=activeProfile;var setting=CurrentFertilitySetting();
   var hits=await Task.Run(()=>
   {
    var rows=new SearchHit[seeds.Length];
    Parallel.For(0,seeds.Length,index=>rows[index]=SeedSearcher.Describe(seeds[index],profile,setting));
    return rows;
   });
   ShowResults(hits);SearchProgress.Value=100;LblProgress.Text=Localization.Instance.Format("SeedsLoadedFormat",hits.Length.ToString("N0"),Path.GetFileName(dialog.FileName));
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
 void SetMinimumSeed(object sender,RoutedEventArgs e)=>TxtFirstSeed.Text=SeedLimits.Minimum.ToString();
 void SetMaximumSeed(object sender,RoutedEventArgs e)=>TxtMaxSeed.Text=SeedLimits.Maximum.ToString();
 internal bool SmokePreparedProfileControls()=>CmbStartMode.IsEnabled&&ChkDlc01.IsChecked==true&&ChkDlc01.IsEnabled&&ChkDlc03.IsChecked==false&&!ChkDlc03.IsEnabled&&Math.Abs(StartProfilePanel.ActualHeight-MapProfilePanel.ActualHeight)<.1&&Math.Abs(DlcProfilePanel.ActualHeight-MapProfilePanel.ActualHeight)<.1;
 internal bool SmokeDlcToggle(){ChkDlc01.IsChecked=false;var hidden=!activeProfile.Dlc01&&CinisCard.Visibility==Visibility.Collapsed&&CinisResultsColumn.Visibility==Visibility.Collapsed&&Grid.GetColumnSpan(SearchRangeCard)==2&&!CmbSlot1.IsEnabled;ChkDlc01.IsChecked=true;return hidden&&activeProfile.Dlc01&&CinisCard.Visibility==Visibility.Visible&&CinisResultsColumn.Visibility==Visibility.Visible&&CmbSlot1.IsEnabled;}
 void SetRunning(bool value){BtnRun.IsEnabled=!value;BtnPreview.IsEnabled=!value;BtnCancel.IsEnabled=value;BtnLoadSeedList.IsEnabled=!value;BtnAddSeedToTable.IsEnabled=!value;BtnExportCsv.IsEnabled=!value&&GridResults.Items.Count>0;BtnLoadPreset.IsEnabled=!value;BtnSavePreset.IsEnabled=!value;BtnMinSeed.IsEnabled=!value;BtnMaxSeed.IsEnabled=!value;TxtPreviewSeed.IsEnabled=!value;TxtFirstSeed.IsEnabled=!value;TxtMaxSeed.IsEnabled=!value;TxtThreads.IsEnabled=!value;TxtLimit.IsEnabled=!value;TxtOutput.IsEnabled=!value;CmbStartMode.IsEnabled=!value;ChkDlc01.IsEnabled=!value;CmbFertilitySetting.IsEnabled=!value;CmbMapTemplate.IsEnabled=!value;CmbMapSize.IsEnabled=!value;CmbSlot1.IsEnabled=!value&&activeProfile.Dlc01;foreach(var box in cinisChecks)box.IsEnabled=!value&&activeProfile.Dlc01;ChkMaxSites.IsEnabled=!value&&activeProfile.Dlc01;ConditionsArea.IsEnabled=!value;}
 void CancelSearch(object sender,RoutedEventArgs e)=>cancellation?.Cancel();
 void OpenOutput(object sender,RoutedEventArgs e){if(lastOutput is not null&&File.Exists(lastOutput))Process.Start(new ProcessStartInfo(lastOutput){UseShellExecute=true});}

 void OpenSeedPreview(object sender,RoutedEventArgs e)
 {
  if(!int.TryParse(TxtPreviewSeed.Text,out var seed)||seed is <SeedLimits.Minimum or >SeedLimits.Maximum){MessageBox.Show(this,Localization.Instance.Format("InvalidSeedFormat",SeedLimits.Minimum.ToString("N0"),SeedLimits.Maximum.ToString("N0")),Localization.Instance["InvalidSeedTitle"],MessageBoxButton.OK,MessageBoxImage.Information);return;}
  ShowPreview((uint)seed);
 }
 void PreviewSelectedResult(object sender,System.Windows.Input.MouseButtonEventArgs e){if(GridResults.SelectedItem is SearchResultRow row)ShowPreview(row.Seed);}
 void ShowPreview(uint seed){new SeedPreviewWindow(seed,activeProfile,CurrentFertilitySetting()){Owner=this}.ShowDialog();}
 FertilitySetting CurrentFertilitySetting()=>(FertilitySetting)CmbFertilitySetting.SelectedIndex;

 void MapProfileChanged(object sender,SelectionChangedEventArgs e)
 {
  if(!profileControlsReady||CmbMapTemplate.SelectedItem is not ProfileChoice<MapTemplateKind> template||CmbMapSize.SelectedItem is not ProfileChoice<MapSizeKind> size)return;
  var selected=MapProfiles.Get(template.Value,size.Value,ChkDlc01.IsChecked==true);if(selected==activeProfile)return;
  activeProfile=selected;
  LatiumConditions.Children.Clear();AlbionConditions.Children.Clear();GridResults.ItemsSource=null;
  RefreshProfileLabels();UpdateConditionAddButtons();
 }

 void DlcProfileChanged(object sender,RoutedEventArgs e)
 {
  if(!profileControlsReady)return;RefreshDlcPresentation();
  if(CmbMapTemplate.SelectedItem is ProfileChoice<MapTemplateKind> template&&CmbMapSize.SelectedItem is ProfileChoice<MapSizeKind> size)
  {
   activeProfile=MapProfiles.Get(template.Value,size.Value,ChkDlc01.IsChecked==true);LatiumConditions.Children.Clear();AlbionConditions.Children.Clear();GridResults.ItemsSource=null;RefreshProfileLabels();UpdateConditionAddButtons();
  }
 }
 void StartModeChanged(object sender,SelectionChangedEventArgs e){if(profileControlsReady)GridResults.ItemsSource=null;}
 void RefreshDlcPresentation()
 {
  var visible=ChkDlc01.IsChecked==true;CinisCard.Visibility=visible?Visibility.Visible:Visibility.Collapsed;CinisResultsColumn.Visibility=visible?Visibility.Visible:Visibility.Collapsed;Grid.SetColumnSpan(SearchRangeCard,visible?1:2);CmbSlot1.IsEnabled=visible;foreach(var box in cinisChecks)box.IsEnabled=visible;ChkMaxSites.IsEnabled=visible;
 }

 void RefreshProfileLabels()
 {
  LblLatiumProfile.Text=ProfileLabel(RegionKind.Latium);
  LblAlbionProfile.Text=ProfileLabel(RegionKind.Albion);
  var ranges=AggregateSiteRanges.For(activeProfile);
  ConfigureMinimumChoices(CmbMinGoldSites,ranges.GoldMin,ranges.GoldMax,ranges.GoldMedian);ConfigureMinimumChoices(CmbMinSturgeonSites,ranges.SturgeonMin,ranges.SturgeonMax,ranges.SturgeonMedian);
  ConfigureMinimumChoices(CmbMinLatiumMountainSites,ranges.LatiumMountainMin,ranges.LatiumMountainMax,ranges.LatiumMountainMedian);ConfigureMinimumChoices(CmbMinLatiumRiverSites,ranges.LatiumRiverMin,ranges.LatiumRiverMax,ranges.LatiumRiverMedian);ConfigureMinimumChoices(CmbMinAlbionMountainSites,ranges.AlbionMountainMin,ranges.AlbionMountainMax,ranges.AlbionMountainMedian);
  ConfigureMinimumChoices(CmbMinMineralMines,ranges.MineralMin,ranges.MineralMax,ranges.MineralMedian);ConfigureMinimumChoices(CmbMinCopperMines,ranges.CopperMin,ranges.CopperMax,ranges.CopperMedian);ConfigureMinimumChoices(CmbMinSilverMines,ranges.SilverMin,ranges.SilverMax,ranges.SilverMedian);
  LblMineralMineRange.Text=StatisticsLabel(ranges.MineralMin,ranges.MineralMax,ranges.MineralAverage,ranges.MineralMedian);LblCopperMineRange.Text=StatisticsLabel(ranges.CopperMin,ranges.CopperMax,ranges.CopperAverage,ranges.CopperMedian);LblSilverMineRange.Text=StatisticsLabel(ranges.SilverMin,ranges.SilverMax,ranges.SilverAverage,ranges.SilverMedian);
  ConfigureMinimumChoices(CmbMinGoldMines,ranges.GoldMineMin,ranges.GoldMineMax,ranges.GoldMineMedian);LblGoldMineRange.Text=StatisticsLabel(ranges.GoldMineMin,ranges.GoldMineMax,ranges.GoldMineAverage,ranges.GoldMineMedian);BtnGoldMinesMedian.Tag=(int)ranges.GoldMineMedian;
  ConfigureMinimumChoices(CmbMinMarbleSites,ranges.MarbleMin,ranges.MarbleMax,ranges.MarbleMedian);LblMarbleSiteRange.Text=StatisticsLabel(ranges.MarbleMin,ranges.MarbleMax,ranges.MarbleAverage,ranges.MarbleMedian);BtnMarbleSitesMedian.Tag=(int)ranges.MarbleMedian;
  BtnMineralMinesMedian.Tag=(int)ranges.MineralMedian;BtnCopperMinesMedian.Tag=(int)ranges.CopperMedian;BtnSilverMinesMedian.Tag=(int)ranges.SilverMedian;
  LblGoldSiteRange.Text=StatisticsLabel(ranges.GoldMin,ranges.GoldMax,ranges.GoldAverage,ranges.GoldMedian);LblSturgeonSiteRange.Text=StatisticsLabel(ranges.SturgeonMin,ranges.SturgeonMax,ranges.SturgeonAverage,ranges.SturgeonMedian);
  LblLatiumMountainSiteRange.Text=StatisticsLabel(ranges.LatiumMountainMin,ranges.LatiumMountainMax,ranges.LatiumMountainAverage,ranges.LatiumMountainMedian);LblLatiumRiverSiteRange.Text=StatisticsLabel(ranges.LatiumRiverMin,ranges.LatiumRiverMax,ranges.LatiumRiverAverage,ranges.LatiumRiverMedian);LblAlbionMountainSiteRange.Text=StatisticsLabel(ranges.AlbionMountainMin,ranges.AlbionMountainMax,ranges.AlbionMountainAverage,ranges.AlbionMountainMedian);
  BtnGoldSitesMedian.Tag=(int)ranges.GoldMedian;BtnSturgeonSitesMedian.Tag=(int)ranges.SturgeonMedian;BtnLatiumMountainSitesMedian.Tag=(int)ranges.LatiumMountainMedian;BtnLatiumRiverSitesMedian.Tag=(int)ranges.LatiumRiverMedian;BtnAlbionMountainSitesMedian.Tag=(int)ranges.AlbionMountainMedian;
  ConfigureAreaSlider(SldMinLatiumArea,ranges.LatiumAreaMin,ranges.LatiumAreaMax);ConfigureAreaSlider(SldMinAlbionArea,ranges.AlbionAreaMin,ranges.AlbionAreaMax);ConfigureAreaSlider(SldMinAlbionSwampArea,ranges.AlbionSwampMin,ranges.AlbionSwampMax);
  LblLatiumAreaRange.Text=AreaRangeLabel(ranges.LatiumAreaMin,ranges.LatiumAreaMax,ranges.LatiumAreaAverage,ranges.LatiumAreaMedian);LblAlbionAreaRange.Text=AreaRangeLabel(ranges.AlbionAreaMin,ranges.AlbionAreaMax,ranges.AlbionAreaAverage,ranges.AlbionAreaMedian);LblAlbionSwampAreaRange.Text=AreaRangeLabel(ranges.AlbionSwampMin,ranges.AlbionSwampMax,ranges.AlbionSwampAverage,ranges.AlbionSwampMedian);
  BtnLatiumAreaMedian.Tag=ranges.LatiumAreaMedian;BtnAlbionAreaMedian.Tag=ranges.AlbionAreaMedian;BtnAlbionSwampAreaMedian.Tag=ranges.AlbionSwampMedian;
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
  var box=button==BtnGoldSitesMedian?CmbMinGoldSites:button==BtnSturgeonSitesMedian?CmbMinSturgeonSites:button==BtnLatiumMountainSitesMedian?CmbMinLatiumMountainSites:button==BtnLatiumRiverSitesMedian?CmbMinLatiumRiverSites:button==BtnGoldMinesMedian?CmbMinGoldMines:button==BtnMarbleSitesMedian?CmbMinMarbleSites:button==BtnMineralMinesMedian?CmbMinMineralMines:button==BtnCopperMinesMedian?CmbMinCopperMines:button==BtnSilverMinesMedian?CmbMinSilverMines:CmbMinAlbionMountainSites;
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
  var dlc=region==RegionKind.Latium?(activeProfile.Dlc01?" · PoA":Localization.Instance["WithoutPoASuffix"]):"";
  return Localization.Instance.Format("ProfileLabelFormat",region,activeProfile.Template,activeProfile.Size,dlc,activeProfile.StarterCount(region),Localization.Instance["UpToWord"],secondary,tertiary);
 }

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
 internal void AddPreviewConditions(){AddCondition(RegionKind.Latium,FertilitySetKind.Tertiary,LatiumConditions);AddCondition(RegionKind.Latium,FertilitySetKind.AnyCombination,LatiumConditions);AddCondition(RegionKind.Albion,FertilitySetKind.Secondary,AlbionConditions);AddCondition(RegionKind.Albion,FertilitySetKind.AnyCombination,AlbionConditions);CmbSlot1.SelectedIndex=1;}
}

internal sealed record SearchResultRow(uint Seed,int MarbleSites,int GoldMines,int GoldRiverSites,int SturgeonRiverSites,int MineralMines,int CopperMines,int SilverMines,int LatiumAreaValue,int AlbionAreaValue,int AlbionSwampValue,int LatiumMountain,int LatiumRiver,int AlbionMountain,ImageSource[] CinisFertilities,bool NeedsVerification=false)
{
 // The grid shows grouped numbers but sorts on the raw values, so "highest area first"
 // orders numerically instead of lexicographically.
 public string LatiumArea=>$"{LatiumAreaValue:N0}";
 public string AlbionArea=>$"{AlbionAreaValue:N0}";
 public string AlbionSwampArea=>$"{AlbionSwampValue:N0}";
 public static SearchResultRow From(SearchHit hit)=>new(hit.Seed,hit.Latium.MarbleSites,hit.Latium.GoldMineSites,hit.Latium.GoldSites,hit.Latium.SturgeonSites,hit.Latium.MineralMineSites,hit.Albion.CopperMineSites,hit.Albion.SilverMineSites,hit.Latium.BuildableTiles,hit.Albion.BuildableTiles,hit.Albion.SwampTiles,hit.Latium.Sites.Mountain,hit.Latium.Sites.River,hit.Albion.Sites.Mountain,[..hit.Fertilities.Select(FertilityIcons.Get)],hit.NeedsVerification);
}
internal sealed record MinimumSiteChoice(string Label,int Minimum){public override string ToString()=>Label;}
internal sealed record ProfileChoice<T>(string Label,T Value){public override string ToString()=>Label;}
