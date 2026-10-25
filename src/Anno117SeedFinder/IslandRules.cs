using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Island filter: which island (by its asset, e.g. Latium M03) may or must sit on the map, optionally only in certain positions and
// with a certain rotation. A rule that excludes an island rejects every seed where it appears (in one of its positions, with its
// rotation); a rule that requires one keeps only seeds where it does.
// Slots: the positions it applies to (empty: anywhere). Rotation: quarter turns 0-3 (-1: any), as the preview shows them.
internal sealed record IslandRule(RegionKind Region,string Asset,bool Exclude,int[] Slots,int Rotation=-1)
{
 public bool Matches(GeneratedIsland island)=>island.Name==Asset&&(Slots.Length==0||Array.IndexOf(Slots,island.SlotIndex)>=0)&&(Rotation<0||island.Rotation==Rotation);
 public bool Passes(IReadOnlyList<GeneratedIsland> islands)
 {
  foreach(var island in islands)if(Matches(island))return !Exclude;
  return Exclude;
 }
}

// The islands a map profile can place, with a short name (XL01, L06, M03, PoA M01 …) and the slots that can hold each.
internal static class IslandCatalog
{
 internal sealed record Entry(string Asset,string Label,string Size);

 public static IReadOnlyList<Entry> For(MapProfile profile,RegionKind region)
 {
  var names=new HashSet<string>();
  foreach(var slot in profile.Slots(region))foreach(var asset in Candidates(region))if(Holds(region,slot,asset))names.Add(asset);
  return[..names.Select(name=>new Entry(name,Label(name),SizeOf(name))).OrderBy(entry=>SizeRank(entry.Size)).ThenBy(entry=>entry.Label,StringComparer.Ordinal)];
 }

 // The slot numbers of the profile where the island can be placed.
 public static int[] Slots(MapProfile profile,RegionKind region,string asset)=>[..profile.Slots(region).Where(slot=>Holds(region,slot,asset)).Select(slot=>slot.Index)];

 static bool Holds(RegionKind region,MapSlot slot,string asset)=>region==RegionKind.Latium?Generator.LatiumSlotHolds(slot.Type,slot.Size,asset):AlbionGenerator.AlbionSlotHolds(slot.Size,asset);

 static IEnumerable<string> Candidates(RegionKind region)
 {
  if(region==RegionKind.Albion)
  {
   foreach(var(size,count) in new[]{("large",8),("medium",7),("small",7)})for(var i=1;i<=count;i++)yield return $"celtic_island_{size}_{i:00}";
   yield break;
  }
  for(var i=1;i<=4;i++)yield return $"roman_island_extralarge_{i:00}";
  foreach(var i in new[]{1,2,3,4,5,6,7,9})yield return $"roman_island_large_{i:00}";
  for(var i=1;i<=8;i++)yield return $"roman_island_medium_{i:00}";
  for(var i=1;i<=7;i++)yield return $"roman_island_small_{i:00}";
  for(var i=1;i<=3;i++)yield return $"roman_dlc01_island_medium_{i:00}";
  yield return "roman_dlc_01_island_small_01";yield return "roman_dlc01_island_small_02";
 }

 // "roman_island_medium_03" → "M03"; the DLC01 islands get "PoA" in front.
 public static string Label(string asset)
 {
  var size=SizeOf(asset);var number=asset[^2..];
  return(asset.Contains("dlc")?"PoA ":"")+size+number;
 }
 static string SizeOf(string asset)=>MapSchematic.SizeClass(asset);
 static int SizeRank(string size)=>size switch{"XL"=>0,"L"=>1,"M"=>2,_=>3};
 public static string RotationLabel(int rotation)=>rotation<0?Localization.Instance["IslandRuleAnyRotation"]:$"{rotation*90}°";
}

// One island rule in the main window: must / must not appear, the island, its positions and its rotation.
internal sealed class IslandRuleRowControl:Border
{
 readonly MapProfile profile;
 readonly ComboBox mode=new(){Width=150,Margin=new Thickness(0,0,8,0)};
 readonly ComboBox island=new(){Width=110,Margin=new Thickness(0,0,8,0)};
 readonly ComboBox rotation=new(){Width=124,Margin=new Thickness(8,0,0,0)};
 readonly Button positions=new(){Padding=new Thickness(8,3,8,3)};
 readonly Button remove=new(){Padding=new Thickness(9,4,9,4),Margin=new Thickness(10,0,0,0)};
 int[] selectedSlots=[];
 public RegionKind Region{get;}
 public event EventHandler? RemoveRequested;

 public IslandRuleRowControl(RegionKind region,MapProfile profile)
 {
  Region=region;this.profile=profile;
  Background=new SolidColorBrush(Color.FromRgb(248,250,252));BorderBrush=new SolidColorBrush(Color.FromRgb(226,232,240));BorderThickness=new Thickness(1);CornerRadius=new CornerRadius(7);Padding=new Thickness(7);Margin=new Thickness(0,0,0,5);
  var root=new DockPanel();Child=root;
  DockPanel.SetDock(remove,Dock.Right);root.Children.Add(remove);remove.Click+=(_,_)=>RemoveRequested?.Invoke(this,EventArgs.Empty);
  var row=new StackPanel{Orientation=Orientation.Horizontal};root.Children.Add(row);
  row.Children.Add(new TextBlock{Text=Localization.Instance["IslandRuleTitle"],FontWeight=FontWeights.SemiBold,Width=112,VerticalAlignment=VerticalAlignment.Center});
  row.Children.Add(mode);row.Children.Add(island);row.Children.Add(positions);row.Children.Add(rotation);
  foreach(var entry in IslandCatalog.For(profile,region))island.Items.Add(new ComboBoxItem{Content=entry.Label,Tag=entry.Asset,ToolTip=entry.Asset});
  island.SelectedIndex=0;island.SelectionChanged+=(_,_)=>{selectedSlots=[..selectedSlots.Intersect(IslandCatalog.Slots(profile,Region,Asset))];Refresh();};
  for(var r=-1;r<=3;r++)rotation.Items.Add(new ComboBoxItem{Tag=r});rotation.SelectedIndex=0;
  mode.Items.Add(new ComboBoxItem());mode.Items.Add(new ComboBoxItem());mode.SelectedIndex=0;mode.SelectionChanged+=(_,_)=>Refresh();
  positions.Click+=PickPositions;
  Refresh();
  Localization.Instance.PropertyChanged+=OnLanguageChanged;RemoveRequested+=(_,_)=>Localization.Instance.PropertyChanged-=OnLanguageChanged;
 }

 string Asset=>(island.SelectedItem as ComboBoxItem)?.Tag as string??"";
 bool Exclude=>mode.SelectedIndex==1;
 int Rotation=>(rotation.SelectedItem as ComboBoxItem)?.Tag is int value?value:-1;

 void OnLanguageChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)=>Refresh();

 void Refresh()
 {
  ((ComboBoxItem)mode.Items[0]).Content=Localization.Instance["IslandRuleMustAppear"];((ComboBoxItem)mode.Items[1]).Content=Localization.Instance["IslandRuleMustNotAppear"];
  foreach(ComboBoxItem item in rotation.Items)item.Content=IslandCatalog.RotationLabel((int)item.Tag);
  rotation.ToolTip=Localization.Instance["IslandRuleRotationTooltip"];remove.Content=Localization.Instance["Remove"];
  positions.Content=selectedSlots.Length==0?Localization.Instance["IslandRuleAnywhere"]:Localization.Instance.Format("PositionsButtonCountFormat",selectedSlots.Length);
  positions.ToolTip=selectedSlots.Length==0?Localization.Instance["IslandRuleAnywhereTooltip"]:Localization.Instance.Format("RequiredPositionsFormat",string.Join(", ",selectedSlots.Select(slot=>"#"+slot)));
 }

 void PickPositions(object sender,RoutedEventArgs e)
 {
  var allowed=IslandCatalog.Slots(profile,Region,Asset).ToHashSet();
  var dialog=new PositionPickerWindow(Region,FertilitySetKind.AnyCombination,selectedSlots,allowed.Count,profile,allowed,Localization.Instance.Format("IslandRulePickerHintFormat",IslandCatalog.Label(Asset))){Owner=Window.GetWindow(this)};
  if(dialog.ShowDialog()!=true)return;
  selectedSlots=dialog.SelectedSlots;Refresh();
 }

 public IslandRule BuildRule()=>new(Region,Asset,Exclude,selectedSlots,Rotation);

 public IslandRulePreset ExportPreset()=>new(){Region=Region,Asset=Asset,Exclude=Exclude,Slots=[..selectedSlots],Rotation=Rotation};

 public void ApplyPreset(IslandRulePreset preset)
 {
  var item=island.Items.OfType<ComboBoxItem>().FirstOrDefault(x=>(string)x.Tag==preset.Asset)??throw new System.IO.InvalidDataException(Localization.Instance.Format("IslandRuleUnavailableFormat",IslandCatalog.Label(preset.Asset)));
  island.SelectedItem=item;mode.SelectedIndex=preset.Exclude?1:0;rotation.SelectedIndex=Math.Clamp(preset.Rotation+1,0,4);
  selectedSlots=[..preset.Slots.Intersect(IslandCatalog.Slots(profile,Region,preset.Asset))];Refresh();
 }
}

internal sealed class IslandRulePreset
{
 public RegionKind Region { get; set; }
 public string Asset { get; set; }="";
 public bool Exclude { get; set; }
 public int[] Slots { get; set; }=[];
 public int Rotation { get; set; }=-1;
}
