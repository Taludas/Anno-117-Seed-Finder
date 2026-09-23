using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;

internal sealed class ConditionRowControl:Border
{
 readonly FertilitySetDefinition definition;
 readonly MapProfile profile;
 readonly ComboBox minimum=new(){Width=58,Margin=new Thickness(6,0,14,0)};
 readonly List<(SlotGroupDefinition Definition,ComboBox[] Boxes,TextBlock Label)> groups=[];
 readonly TextBlock warning=new(){Foreground=new SolidColorBrush(Color.FromRgb(198,40,40)),FontSize=11,Visibility=Visibility.Collapsed,Margin=new Thickness(10,3,0,0)};
 readonly DispatcherTimer warningTimer=new(){Interval=TimeSpan.FromSeconds(3)};
 readonly Button? positionsButton;
 readonly Button removeButton;
 readonly TextBlock atLeastLabel;
 readonly TextBlock islandsLabel;
 int[] selectedPositions=[];
 bool reverting;
 bool updatingMinimum;
 public event EventHandler? RemoveRequested;
 public event EventHandler? MinimumChanged;
 public RegionKind Region=>definition.Region;
 public FertilitySetKind Set=>definition.Set;
 public int Minimum=>minimum.SelectedItem is int value?value:1;

 public ConditionRowControl(FertilitySetDefinition definition,MapProfile profile,int maximumCount)
 {
  this.definition=definition;this.profile=profile;
  Background=new SolidColorBrush(Color.FromRgb(248,250,252));BorderBrush=new SolidColorBrush(Color.FromRgb(226,232,240));BorderThickness=new Thickness(1);CornerRadius=new CornerRadius(7);Padding=new Thickness(7);Margin=new Thickness(0,0,0,5);
  for(var i=1;i<=maximumCount;i++)minimum.Items.Add(i);minimum.SelectedIndex=0;minimum.SelectionChanged+=(_,_)=>{if(!updatingMinimum)MinimumChanged?.Invoke(this,EventArgs.Empty);};
  warningTimer.Tick+=(_,_)=>{warning.Visibility=Visibility.Collapsed;warningTimer.Stop();};

  var root=new DockPanel();
  var remove=new Button{Content=Localization.Instance["Remove"],Padding=new Thickness(9,4,9,4),Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Top};remove.Click+=(_,_)=>RemoveRequested?.Invoke(this,EventArgs.Empty);DockPanel.SetDock(remove,Dock.Right);root.Children.Add(remove);removeButton=remove;
  var body=new StackPanel();root.Children.Add(body);
  var top=new StackPanel{Orientation=Orientation.Horizontal};top.Children.Add(new TextBlock{Text=definition.Set==FertilitySetKind.AnyCombination?"Any Combination":definition.Set.ToString(),FontWeight=FontWeights.SemiBold,Width=112,VerticalAlignment=VerticalAlignment.Center});
  atLeastLabel=new TextBlock{Text=Localization.Instance["AtLeast"],VerticalAlignment=VerticalAlignment.Center};top.Children.Add(atLeastLabel);top.Children.Add(minimum);
  islandsLabel=new TextBlock{Text=Localization.Instance["IslandsWord"],VerticalAlignment=VerticalAlignment.Center};top.Children.Add(islandsLabel);
  positionsButton=new Button{Padding=new Thickness(8,3,8,3),Margin=new Thickness(12,0,0,0)};positionsButton.Click+=PickPositions;top.Children.Add(positionsButton);RefreshPositionsButton();
  top.Children.Add(warning);body.Children.Add(top);
  var fields=new WrapPanel{Margin=new Thickness(0,5,0,0)};body.Children.Add(fields);
  foreach(var group in definition.Groups)
  {
   var groupLabel=new TextBlock{Text=GroupLabel(group),Foreground=new SolidColorBrush(Color.FromRgb(102,112,133)),FontSize=11,Margin=new Thickness(0,0,0,1)};
   var panel=new StackPanel{Margin=new Thickness(0,0,18,2)};panel.Children.Add(groupLabel);
   var boxesPanel=new StackPanel{Orientation=Orientation.Horizontal};var boxes=new ComboBox[definition.IsAnyCombination?2:group.SlotIndices.Length];
   for(var i=0;i<boxes.Length;i++)
   {
    var box=new ComboBox{Width=112,Margin=new Thickness(0,0,5,0)};FertilityIcons.Configure(box,group.Choices);box.SelectedIndex=0;box.SelectionChanged+=SelectionChanged;boxes[i]=box;boxesPanel.Children.Add(box);
   }
   groups.Add((group,boxes,groupLabel));panel.Children.Add(boxesPanel);fields.Children.Add(panel);
  }
  Child=root;
  // Built imperatively (not via binding), so an already-added row needs to be told
  // explicitly when the language changes; unhooked once the row is removed.
  Localization.Instance.PropertyChanged+=OnLanguageChanged;
  RemoveRequested+=(_,_)=>Localization.Instance.PropertyChanged-=OnLanguageChanged;
 }

 // Any Combination accepts one or two fertilities: a slot left on * is ignored rather than
 // rejected, so the header says so instead of the search silently matching everything.
 string GroupLabel(SlotGroupDefinition group)=>definition.IsAnyCombination?group.Label+Localization.Instance["WildcardIgnoredSuffix"]:group.Label;

 void OnLanguageChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
 {
  removeButton.Content=Localization.Instance["Remove"];atLeastLabel.Text=Localization.Instance["AtLeast"];islandsLabel.Text=Localization.Instance["IslandsWord"];
  minimum.ToolTip=Localization.Instance.Format("MinimumTooltipFormat",definition.Set,minimum.Items.Count);
  RefreshPositionsButton();
  // Group headers ("Slots 3–6 · ...") were captured once from FertilityDefinitions.Get
  // at construction time; re-fetch it fresh (same Groups order, now in the new language)
  // and copy each label across.
  var freshGroups=FertilityDefinitions.Get(definition.Region,definition.Set).Groups;
  for(var i=0;i<groups.Count&&i<freshGroups.Length;i++)groups[i].Label.Text=GroupLabel(freshGroups[i]);
 }

 void SelectionChanged(object sender,SelectionChangedEventArgs e)
 {
  if(reverting||sender is not ComboBox changed)return;
  var group=groups.FirstOrDefault(x=>x.Boxes.Contains(changed));if(group.Boxes is null)return;
  var selected=group.Boxes.Select(x=>x.SelectedItem).OfType<FertilityChoice>().Where(choice=>!choice.IsWildcard).ToArray();
  if(selected.Select(x=>x.Guid).Distinct().Count()==selected.Length)return;
  reverting=true;changed.SelectedIndex=0;reverting=false;ShowWarning(Localization.Instance["DuplicateFertilityInGroup"]);
 }

 void ShowWarning(string text){warning.Text=text;warning.Visibility=Visibility.Visible;warningTimer.Stop();warningTimer.Start();}

 public void SetMaximumMinimum(int maximum)
 {
  maximum=Math.Max(1,maximum);
  var selected=Math.Min(Minimum,maximum);
  updatingMinimum=true;
  minimum.Items.Clear();
  for(var i=1;i<=maximum;i++)minimum.Items.Add(i);
  minimum.SelectedItem=selected;
  updatingMinimum=false;
  minimum.ToolTip=Localization.Instance.Format("MinimumTooltipFormat",definition.Set,maximum);
 }

 void PickPositions(object sender,RoutedEventArgs e)
 {
  var dialog=new PositionPickerWindow(definition.Region,definition.Set,selectedPositions,MapLayoutPositions.For(profile,definition.Region,definition.Set).Count,profile){Owner=Window.GetWindow(this)};
  if(dialog.ShowDialog()!=true)return;
  selectedPositions=dialog.SelectedSlots;
  RefreshPositionsButton();
 }

 void RefreshPositionsButton()
 {
  if(positionsButton is null)return;
  positionsButton.Content=selectedPositions.Length==0?Localization.Instance["PositionsButton"]:Localization.Instance.Format("PositionsButtonCountFormat",selectedPositions.Length);
  positionsButton.ToolTip=selectedPositions.Length==0?Localization.Instance["NoPositionRequirement"]:Localization.Instance.Format("RequiredPositionsFormat",string.Join(", ",selectedPositions.Select(slot=>"#"+slot)));
 }

 public IslandCondition BuildCondition()
 {
  var configured=groups.Select(x=>new SlotGroupCondition(x.Definition.SlotIndices,x.Boxes.Select(b=>b.SelectedItem).OfType<FertilityChoice>().Where(choice=>!choice.IsWildcard).Select(c=>c.Guid).ToArray())).ToArray();
  return new(definition.Region,definition.Set,(int)minimum.SelectedItem,configured,selectedPositions);
 }

 public ConditionPreset ExportPreset()=>new()
 {
  Region=definition.Region,Set=definition.Set,Minimum=Minimum,
  RequiredByGroup=[..groups.Select(group=>group.Boxes.Select(box=>(box.SelectedItem as FertilityChoice)?.Guid??0u).ToArray())],
  Positions=[..selectedPositions]
 };

 public void ApplyPreset(ConditionPreset preset)
 {
  if(preset.Region!=definition.Region||preset.Set!=definition.Set)throw new InvalidDataException(Localization.Instance["ConditionMismatch"]);
  for(var groupIndex=0;groupIndex<groups.Count;groupIndex++)for(var boxIndex=0;boxIndex<groups[groupIndex].Boxes.Length;boxIndex++)
  {
   var value=preset.RequiredByGroup[groupIndex][boxIndex];var box=groups[groupIndex].Boxes[boxIndex];
   var item=box.Items.OfType<FertilityChoice>().FirstOrDefault(choice=>choice.Guid==value)??throw new InvalidDataException(Localization.Instance["FertilityUnavailable"]);
   box.SelectedItem=item;
  }
  SetMaximumMinimum(Math.Max(Minimum,preset.Minimum));
  minimum.SelectedItem=Math.Min(preset.Minimum,minimum.Items.Count);
  selectedPositions=[..preset.Positions];RefreshPositionsButton();
 }
}
