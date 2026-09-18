using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

internal sealed class PositionPickerWindow:Window
{
 readonly Dictionary<ToggleButton,MapPositionDefinition> buttons=[];
 readonly TextBlock selectedText=new(){Margin=new Thickness(0,10,0,0),TextWrapping=TextWrapping.Wrap};
 readonly int maximum;
 static readonly ControlTemplate PositionButtonTemplate=CreatePositionButtonTemplate();

 public int[] SelectedSlots=>buttons.Where(pair=>pair.Key.IsChecked==true).Select(pair=>pair.Value.SlotIndex).Order().ToArray();

 public PositionPickerWindow(RegionKind region,FertilitySetKind set,IEnumerable<int> selected,int maximum,MapProfile? profile=null)
 {
  profile??=MapProfiles.Default;
  this.maximum=maximum;
  Title=Localization.Instance.Format("PositionPickerTitleFormat",region,set);Width=800;Height=900;MinWidth=720;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Brushes.White;
  var root=new DockPanel{Margin=new Thickness(18)};Content=root;
  var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
  var cancel=new Button{Content=Localization.Instance["Cancel"],Padding=new Thickness(14,7,14,7),Margin=new Thickness(0,0,8,0)};cancel.Click+=(_,_)=>{DialogResult=false;Close();};footer.Children.Add(cancel);
  var accept=new Button{Content=Localization.Instance["Apply"],Padding=new Thickness(14,7,14,7),IsDefault=true};accept.Click+=(_,_)=>{DialogResult=true;Close();};footer.Children.Add(accept);
  DockPanel.SetDock(selectedText,Dock.Bottom);root.Children.Add(selectedText);
  var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  header.Children.Add(new TextBlock{Text=region==RegionKind.Latium?$"{profile.Template} / {profile.Size} · {(profile.Dlc01?Localization.Instance["PoAActive"]:Localization.Instance["WithoutPoA"])}":$"{profile.Template} / {profile.Size}",FontWeight=FontWeights.SemiBold,FontSize=16});
  header.Children.Add(new TextBlock{Text=set==FertilitySetKind.AnyCombination?Localization.Instance["PickerHintAnyCombination"]:Localization.Instance["PickerHintRole"],Foreground=new SolidColorBrush(Color.FromRgb(102,112,133)),Margin=new Thickness(0,3,0,1)});
  if(region==RegionKind.Latium&&profile.Dlc01)header.Children.Add(new TextBlock{Text=Localization.Instance["PickerHintCinisDlc"],Foreground=new SolidColorBrush(Color.FromRgb(102,112,133)),Margin=new Thickness(0,0,0,8)});
  else if(region==RegionKind.Latium)header.Children.Add(new TextBlock{Text=Localization.Instance["PickerHintNoDlc"],Foreground=new SolidColorBrush(Color.FromRgb(102,112,133)),Margin=new Thickness(0,0,0,8)});
  else header.Children.Add(new TextBlock{Text=Localization.Instance["PickerHintAlbion"],Foreground=new SolidColorBrush(Color.FromRgb(102,112,133)),Margin=new Thickness(0,0,0,8)});
  var viewport=new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(viewport);
  var canvas=MapSchematic.CreateCanvas(region,profile);viewport.Content=canvas;
  var selectablePositions=MapLayoutPositions.For(profile,region,set);
  var selectableSlots=selectablePositions.Select(position=>position.SlotIndex).ToHashSet();
  foreach(var position in MapLayoutPositions.ForPreview(profile,region))
  {
   var tile=MapSchematic.CreateLayoutTile(position);
   // Die Rollenzuordnung soll schon vor dem ersten Klick ablesbar sein: nicht
   // verfügbare Inseln treten zurück, auswählbare behalten ihre volle Helligkeit.
   if(!selectableSlots.Contains(position.SlotIndex))tile.Opacity=.26;
   MapSchematic.AddTile(canvas,position,tile);
  }
  var chosen=selected.ToHashSet();
  foreach(var position in selectablePositions)
  {
   var side=MapSchematic.TileSize(position.Size);
   var outline=new Border{Width=side,Height=side,BorderThickness=new Thickness(3),Background=Brushes.Transparent,IsHitTestVisible=false,RenderTransform=new RotateTransform(45),RenderTransformOrigin=new Point(.5,.5),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
   var button=new ToggleButton{Width=MapSchematic.MarkerSize(position.Size),Height=MapSchematic.MarkerSize(position.Size),Padding=new Thickness(0),Background=Brushes.Transparent,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Content=outline,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,ToolTip=Localization.Instance.Format("PositionTooltipFormat",position.Label),IsChecked=chosen.Contains(position.SlotIndex),Tag=outline,Template=PositionButtonTemplate,Focusable=false};
   button.Checked+=Changed;button.Unchecked+=Changed;buttons.Add(button,position);MapSchematic.AddTile(canvas,position,button);
  }
  RefreshSelectionVisuals();RefreshSelectedText();
 }

 static ControlTemplate CreatePositionButtonTemplate()
 {
  // Kein Standard-Button-Template: dessen Hover-Zustand würde ein ungedrehtes
  // Rechteck über die Insel zeichnen. Der Content ist ausschließlich die Raute.
  // Der transparente Border ist trotzdem nötig, damit die gesamte Markerfläche
  // zuverlässig am Hit-Test teilnimmt und die Klicks beim ToggleButton ankommen.
  var root=new FrameworkElementFactory(typeof(Border));
  root.SetValue(Border.BackgroundProperty,Brushes.Transparent);
  var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
  presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);
  presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);
  presenter.SetBinding(ContentPresenter.ContentProperty,new Binding(nameof(ContentControl.Content)){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});
  root.AppendChild(presenter);
  return new ControlTemplate(typeof(ToggleButton)){VisualTree=root};
 }

 void Changed(object sender,RoutedEventArgs e)
 {
  if(sender is ToggleButton changed&&changed.IsChecked==true&&SelectedSlots.Length>maximum){changed.IsChecked=false;return;}
  RefreshSelectionVisuals();
  RefreshSelectedText();
 }

 void RefreshSelectionVisuals()
 {
  foreach(var pair in buttons)
  {
   if(pair.Key.Tag is not Border outline)continue;
   var selected=pair.Key.IsChecked==true;
   // Hell-türkise Außenlinie = diese Insel ist für die aktive Rolle zulässig.
   // Gold plus Fläche = tatsächlich als Bedingung gewählt.
   outline.BorderBrush=selected?new SolidColorBrush(Color.FromRgb(236,192,94)):new SolidColorBrush(Color.FromRgb(126,238,221));
   outline.Background=selected?new SolidColorBrush(Color.FromArgb(70,138,91,36)):Brushes.Transparent;
  }
 }

 void RefreshSelectedText(){var selected=SelectedSlots.Select(slot=>buttons.Values.Single(position=>position.SlotIndex==slot).Label);selectedText.Text=selected.Any()?Localization.Instance.Format("SelectedFormat",string.Join(", ",selected)):Localization.Instance["NoPositionRequirementLong"];}
}
