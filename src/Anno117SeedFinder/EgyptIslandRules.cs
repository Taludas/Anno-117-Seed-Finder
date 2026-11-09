﻿using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

// Aegyptus island filter: the map is fixed, so a rule names one island (or any island, at least Minimum of them) instead of positions.
// It can ask for the island's version (the full island or its smaller "_layer01" variant) and for fertilities. As in Latium and Albion
// the fertilities follow a fertility set, slot by slot, because each slot only draws from its own pool (no Gold in slot 1); which of
// its two sets a non-starter island gets is up to the seed, so the set is part of the rule. Set 0 is "Any Combination": no set, the
// fertilities may sit in any slot.
internal enum EgyptLayerMode{Any,Full,Layer}
internal sealed record EgyptIslandRequirement(int First,int Last,uint[] Required);
internal sealed record EgyptIslandRule(string Island,EgyptLayerMode Layer,uint Set,EgyptIslandRequirement[] Groups,int Minimum=1)
{
 // Any Combination: the fertilities anywhere on the island.
 public static EgyptIslandRule Anywhere(string island,EgyptLayerMode layer,uint[] fertilities,int minimum=1)=>new(island,layer,0,[new(0,int.MaxValue,fertilities)],minimum);

 public bool Passes(EgyptGeneration egypt)
 {
  var count=0;
  foreach(var island in egypt.Islands)if(Matches(island)&&++count>=Minimum)return true;
  return false;
 }
 internal bool Matches(EgyptIsland island)
 {
  if(Island!=EgyptGenerator.AnyIsland&&island.Name!=Island)return false;
  if(Set!=0&&island.FertilitySet!=Set)return false;
  if(Layer!=EgyptLayerMode.Any&&island.Variant.EndsWith("_layer01")!=(Layer==EgyptLayerMode.Layer))return false;
  foreach(var group in Groups)foreach(var fertility in group.Required)
  {
   var found=false;
   for(var slot=group.First;slot<=group.Last&&slot<island.Fertilities.Length&&!found;slot++)found=island.Fertilities[slot]==fertility;
   if(!found)return false;
  }
  return true;
 }

 // Whether the rule can occur at all on this map size and fertility setting.
 public bool IsValid(MapSizeKind size,FertilitySetting setting)
 {
  if(Island!=EgyptGenerator.AnyIsland&&!EgyptGenerator.FertileIslands.Any(island=>island.Name==Island))return false;
  if(Minimum<1||Minimum>(Island==EgyptGenerator.AnyIsland?EgyptGenerator.MaximumIslands(size,Layer):1))return false;
  if(Layer!=EgyptLayerMode.Any&&!EgyptGenerator.CanBeLayered(Island,size))return false;
  if(Set==0)
  {
   var possible=EgyptGenerator.PossibleFertilities(Island,setting);
   return Groups.All(group=>group.Required.All(possible.Contains));
  }
  if(!EgyptGenerator.CandidateSets(Island,setting).Any(choice=>choice.Guid==Set))return false;
  var slots=EgyptGenerator.SlotGroups(Set);
  return Groups.All(group=>slots.Any(slot=>slot.First==group.First&&slot.Last==group.Last&&group.Required.All(slot.Choices.Contains)));
 }
}

// One Aegyptus island rule in the main window. Its versions, sets and slots follow the island, the map size and the fertility setting.
internal sealed class EgyptIslandRuleRowControl:Border
{
 readonly ComboBox island=new(){Width=130,Margin=new Thickness(0,0,8,0)};
 readonly ComboBox layer=new(){Width=170,Margin=new Thickness(0,0,8,0)};
 readonly ComboBox set=new(){Width=170};
 readonly ComboBox minimum=new(){Width=58,Margin=new Thickness(6,0,6,0)};
 readonly TextBlock atLeast=new(){VerticalAlignment=VerticalAlignment.Center};
 readonly TextBlock islandsWord=new(){VerticalAlignment=VerticalAlignment.Center};
 readonly StackPanel minimumPanel=new(){Orientation=Orientation.Horizontal,Margin=new Thickness(14,0,0,0)};
 readonly WrapPanel slots=new(){Margin=new Thickness(0,5,0,0)};
 readonly Button remove=new(){Padding=new Thickness(9,4,9,4),Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Top};
 readonly TextBlock title=new(){FontWeight=FontWeights.SemiBold,Width=112,VerticalAlignment=VerticalAlignment.Center};
 readonly TextBlock warning=new(){Foreground=new SolidColorBrush(Color.FromRgb(198,40,40)),FontSize=11,Visibility=Visibility.Collapsed,Margin=new Thickness(10,3,0,0)};
 readonly DispatcherTimer warningTimer=new(){Interval=TimeSpan.FromSeconds(3)};
 readonly List<(int First,int Last,ComboBox[] Boxes)> groups=[];
 MapSizeKind size;FertilitySetting setting;
 bool updating;
 public event EventHandler? RemoveRequested;

 public EgyptIslandRuleRowControl(MapSizeKind size,FertilitySetting setting)
 {
  this.size=size;this.setting=setting;
  Background=new SolidColorBrush(Color.FromRgb(248,250,252));BorderBrush=new SolidColorBrush(Color.FromRgb(226,232,240));BorderThickness=new Thickness(1);CornerRadius=new CornerRadius(7);Padding=new Thickness(7);Margin=new Thickness(0,0,0,5);
  warningTimer.Tick+=(_,_)=>{warning.Visibility=Visibility.Collapsed;warningTimer.Stop();};
  var root=new DockPanel();Child=root;
  DockPanel.SetDock(remove,Dock.Right);root.Children.Add(remove);remove.Click+=(_,_)=>RemoveRequested?.Invoke(this,EventArgs.Empty);
  var body=new StackPanel();root.Children.Add(body);
  var top=new StackPanel{Orientation=Orientation.Horizontal};body.Children.Add(top);
  minimumPanel.Children.Add(atLeast);minimumPanel.Children.Add(minimum);minimumPanel.Children.Add(islandsWord);
  top.Children.Add(title);top.Children.Add(island);top.Children.Add(layer);top.Children.Add(set);top.Children.Add(minimumPanel);top.Children.Add(warning);
  body.Children.Add(slots);
  island.Items.Add(new ComboBoxItem{Tag=EgyptGenerator.AnyIsland});
  foreach(var entry in EgyptGenerator.FertileIslands.OrderBy(x=>x.Name,StringComparer.Ordinal))island.Items.Add(new ComboBoxItem{Tag=entry.Name});
  island.SelectedIndex=0;
  for(var mode=0;mode<3;mode++)layer.Items.Add(new ComboBoxItem());layer.SelectedIndex=0;
  minimum.Items.Add(1);minimum.SelectedIndex=0;
  island.SelectionChanged+=(_,_)=>{if(!updating)RefreshChoices();};
  layer.SelectionChanged+=(_,_)=>{if(!updating)RefreshMinimum(Minimum);};
  set.SelectionChanged+=(_,_)=>{if(!updating)BuildSlots(BoxValues(),false);};
  RefreshChoices();RefreshTexts();
  Localization.Instance.PropertyChanged+=OnLanguageChanged;RemoveRequested+=(_,_)=>Localization.Instance.PropertyChanged-=OnLanguageChanged;
 }

 string Island=>(island.SelectedItem as ComboBoxItem)?.Tag as string??"";
 EgyptLayerMode Layer=>(EgyptLayerMode)Math.Max(0,layer.SelectedIndex);
 int Minimum=>minimum.SelectedItem is int value?value:1;
 uint Set=>(set.SelectedItem as ComboBoxItem)?.Tag is EgyptSetChoice choice?choice.Guid:0;
 IEnumerable<ComboBox> Boxes=>groups.SelectMany(group=>group.Boxes);
 uint[] BoxValues()=>[..Boxes.Select(box=>(box.SelectedItem as FertilityChoice)?.Guid??0u)];
 internal int FertilityBoxCount=>Boxes.Count();
 internal bool LayerSelectable=>layer.IsEnabled;
 // What one slot group offers, for the checks.
 internal uint[] GroupChoices(int group)=>[..groups[group].Boxes[0].Items.OfType<FertilityChoice>().Where(choice=>!choice.IsWildcard).Select(choice=>choice.Guid)];

 void OnLanguageChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)=>RefreshTexts();

 static string SetName(EgyptSetChoice choice)=>$"{choice.Role} · {Localization.Instance[choice.Gold?"EgyptGold":"EgyptNoGold"]}";

 void RefreshTexts()
 {
  title.Text=Localization.Instance["IslandRuleTitle"];remove.Content=Localization.Instance["Remove"];
  atLeast.Text=Localization.Instance["AtLeast"];islandsWord.Text=Localization.Instance["IslandsWord"];
  foreach(ComboBoxItem item in island.Items)
  {
   var name=(string)item.Tag;
   item.Content=name==EgyptGenerator.AnyIsland?Localization.Instance["EgyptRuleAnyIsland"]:$"{MapSchematic.SizeClass(EgyptGenerator.FileName(name))} {name[^2..]}"+(EgyptGenerator.IsStarter(name)?Localization.Instance["EgyptRuleStarterSuffix"]:"");
  }
  ((ComboBoxItem)layer.Items[0]).Content=Localization.Instance["EgyptRuleAnyVersion"];((ComboBoxItem)layer.Items[1]).Content=Localization.Instance["EgyptRuleFullVersion"];((ComboBoxItem)layer.Items[2]).Content=Localization.Instance["EgyptLayerVariant"];
  layer.ToolTip=Localization.Instance[layer.IsEnabled?"EgyptRuleLayerTooltip":"EgyptRuleNoLayerTooltip"];
  foreach(ComboBoxItem item in set.Items)item.Content=item.Tag is EgyptSetChoice choice?SetName(choice):"Any Combination";
  set.ToolTip=Localization.Instance["EgyptRuleSetTooltip"];
  BuildSlots(BoxValues(),false);
 }

 // The map size or the fertility setting changed: a version, set and fertilities that still fit stay selected.
 public void SetMap(MapSizeKind size,FertilitySetting setting){this.size=size;this.setting=setting;RefreshChoices();}

 void RefreshChoices()
 {
  var canSwitch=EgyptGenerator.CanBeLayered(Island,size);
  updating=true;if(!canSwitch)layer.SelectedIndex=0;updating=false;layer.IsEnabled=canSwitch;
  layer.ToolTip=Localization.Instance[canSwitch?"EgyptRuleLayerTooltip":"EgyptRuleNoLayerTooltip"];
  minimumPanel.Visibility=Island==EgyptGenerator.AnyIsland?Visibility.Visible:Visibility.Collapsed;
  RefreshMinimum(Minimum);
  var wanted=BoxValues();var previous=(set.SelectedItem as ComboBoxItem)?.Tag as EgyptSetChoice;
  updating=true;set.Items.Clear();set.Items.Add(new ComboBoxItem{Content="Any Combination",Tag=null});
  foreach(var choice in EgyptGenerator.CandidateSets(Island,setting))set.Items.Add(new ComboBoxItem{Content=SetName(choice),Tag=choice});
  set.SelectedItem=set.Items.OfType<ComboBoxItem>().FirstOrDefault(item=>item.Tag is EgyptSetChoice choice&&previous is not null&&choice.Role==previous.Role&&choice.Gold==previous.Gold)??set.Items[0];
  updating=false;
  BuildSlots(wanted,false);
 }

 // At least N islands: only for "any island", and at most as many as can be of the chosen version on this map size.
 void RefreshMinimum(int wanted)
 {
  var maximum=Island==EgyptGenerator.AnyIsland?EgyptGenerator.MaximumIslands(size,Layer):1;
  minimum.Items.Clear();for(var count=1;count<=maximum;count++)minimum.Items.Add(count);
  minimum.SelectedItem=Math.Clamp(wanted,1,maximum);
 }

 // One box per slot of the set, slots with the same pool together in any order as in Latium and Albion; any combination: free boxes.
 // exact: the values belong to these boxes by position (a preset). Otherwise a value stays in its box if that box offers it, or moves
 // to the first free box that does, and is dropped when none does.
 void BuildSlots(uint[] wanted,bool exact)
 {
  var definitions=Set==0?[new EgyptSlotGroup(0,EgyptGenerator.FertilitySlots(setting)-1,EgyptGenerator.PossibleFertilities(Island,setting))]:EgyptGenerator.SlotGroups(Set);
  groups.Clear();slots.Children.Clear();
  foreach(var definition in definitions)
  {
   var label=Set==0?Localization.Instance["AnySlotsAnyOrder"]+Localization.Instance["WildcardIgnoredSuffix"]:definition.First==definition.Last?$"Slot {definition.First+1}":Localization.Instance.Format("SlotsRangeAnyOrderFormat",$"{definition.First+1}–{definition.Last+1}");
   var panel=new StackPanel{Margin=new Thickness(0,0,18,2)};panel.Children.Add(new TextBlock{Text=label,Foreground=new SolidColorBrush(Color.FromRgb(102,112,133)),FontSize=11,Margin=new Thickness(0,0,0,1)});
   var row=new StackPanel{Orientation=Orientation.Horizontal};panel.Children.Add(row);
   var choices=definition.Choices.Select(guid=>new FertilityChoice(SeedRecommendation.FertilityName(guid,false),guid)).OrderBy(choice=>choice.Name,StringComparer.Ordinal).ToArray();
   var boxes=new ComboBox[definition.Last-definition.First+1];
   for(var index=0;index<boxes.Length;index++){var box=new ComboBox{Width=112,Margin=new Thickness(0,0,5,0)};FertilityIcons.Configure(box,choices);box.SelectedIndex=0;box.SelectionChanged+=FertilityChanged;boxes[index]=box;row.Children.Add(box);}
   groups.Add((definition.First,definition.Last,boxes));slots.Children.Add(panel);
  }
  updating=true;
  var all=Boxes.ToArray();var open=new List<uint>();
  for(var index=0;index<wanted.Length;index++)
  {
   if(wanted[index]==0)continue;
   if(index<all.Length&&Offer(all[index],wanted[index]) is {} item)all[index].SelectedItem=item;
   else if(exact)throw new InvalidDataException(Localization.Instance["EgyptRuleInvalid"]);
   else open.Add(wanted[index]);
  }
  foreach(var value in open)if(all.FirstOrDefault(box=>box.SelectedIndex==0&&Offer(box,value) is not null) is {} free)free.SelectedItem=Offer(free,value);
  updating=false;
 }
 static FertilityChoice? Offer(ComboBox box,uint guid)=>box.Items.OfType<FertilityChoice>().FirstOrDefault(choice=>choice.Guid==guid);

 void FertilityChanged(object sender,SelectionChangedEventArgs e)
 {
  if(updating||sender is not ComboBox changed)return;
  var selected=BoxValues().Where(value=>value!=0).ToArray();if(selected.Distinct().Count()==selected.Length)return;
  updating=true;changed.SelectedIndex=0;updating=false;
  warning.Text=Localization.Instance["DuplicateFertilityInGroup"];warning.Visibility=Visibility.Visible;warningTimer.Stop();warningTimer.Start();
 }

 public EgyptIslandRule BuildRule()
 {
  EgyptIslandRequirement Requirement((int First,int Last,ComboBox[] Boxes) group)=>new(group.First,group.Last,[..group.Boxes.Select(box=>box.SelectedItem).OfType<FertilityChoice>().Where(choice=>!choice.IsWildcard).Select(choice=>choice.Guid)]);
  return new(Island,Layer,Set,[..groups.Select(Requirement)],Minimum);
 }

 public EgyptIslandRulePreset ExportPreset()=>new(){Island=Island,Layer=Layer,Set=Set,Fertilities=BoxValues(),Minimum=Minimum};

 public void ApplyPreset(EgyptIslandRulePreset preset)
 {
  var item=island.Items.OfType<ComboBoxItem>().FirstOrDefault(x=>(string)x.Tag==preset.Island)??throw new InvalidDataException(Localization.Instance["EgyptRuleInvalid"]);
  if(!Enum.IsDefined(preset.Layer)||!new EgyptIslandRule(preset.Island,preset.Layer,preset.Set,[],preset.Minimum).IsValid(size,setting))throw new InvalidDataException(Localization.Instance["EgyptRuleInvalid"]);
  island.SelectedItem=item;
  layer.SelectedIndex=(int)preset.Layer;RefreshMinimum(preset.Minimum);
  updating=true;set.SelectedItem=set.Items.OfType<ComboBoxItem>().First(x=>preset.Set==0?x.Tag is null:x.Tag is EgyptSetChoice choice&&choice.Guid==preset.Set);updating=false;
  BuildSlots(preset.Fertilities,true);
 }
}

internal sealed class EgyptIslandRulePreset
{
 public string Island { get; set; }="";
 public EgyptLayerMode Layer { get; set; }
 // The fertility set GUID the slots follow (0: Any Combination), and one value per box of the row in order (0: *).
 public uint Set { get; set; }
 public uint[] Fertilities { get; set; }=[];
 public int Minimum { get; set; }=1;
}
