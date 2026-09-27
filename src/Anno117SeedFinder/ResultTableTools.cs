using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

// The tools above the result table: find a seed, clear the sorting or the whole table, and compare seeds with a reference seed.
public partial class MainWindow
{
 // The comparison: one reference row and the rows compared with it. While rows are compared the table shows only these, and
 // every number of a compared row is tinted green (higher than the reference) or red (lower); the reference row is amber.
 SearchResultRow? referenceRow;
 readonly HashSet<SearchResultRow> comparedRows=new(ReferenceEqualityComparer.Instance);
 static readonly Brush ReferenceTint=Frozen(0xFE,0xF3,0xC7),BetterTint=Frozen(0xD1,0xFA,0xE5),WorseTint=Frozen(0xFE,0xE2,0xE2);
 static Brush Frozen(byte r,byte g,byte b){var brush=new SolidColorBrush(Color.FromRgb(r,g,b));brush.Freeze();return brush;}

 void SetUpResultTableTools()
 {
  // One cell style for the whole grid (never one per column, see CAVEATS.md #22): the background asks CompareBrush.
  var tint=new MultiBinding{Converter=new CompareTint(this)};
  tint.Bindings.Add(new Binding());tint.Bindings.Add(new Binding("Column"){RelativeSource=RelativeSource.Self});
  var style=new Style(typeof(DataGridCell));style.Setters.Add(new Setter(BackgroundProperty,tint));
  // The selection look has to be restated here: this setter outranks the theme's selection trigger for the background, while the
  // theme still turns the text white - a selected row would show white text on a white cell.
  var selected=new Trigger{Property=DataGridCell.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(BackgroundProperty,new DynamicResourceExtension(SystemColors.HighlightBrushKey)));style.Triggers.Add(selected);
  var inactive=new MultiTrigger();inactive.Conditions.Add(new Condition(DataGridCell.IsSelectedProperty,true));inactive.Conditions.Add(new Condition(System.Windows.Controls.Primitives.Selector.IsSelectionActiveProperty,false));
  inactive.Setters.Add(new Setter(BackgroundProperty,new DynamicResourceExtension(SystemColors.InactiveSelectionHighlightBrushKey)));style.Triggers.Add(inactive);
  GridResults.CellStyle=style;
  // A new table (search, seed list, cleared) ends the comparison; the same rows again (a seed added, a table restored) keep it.
  DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty,typeof(DataGrid)).AddValueChanged(GridResults,(_,_)=>TableSourceChanged());
  UpdateTableTools();
 }

 SearchResultRow[] TableRows()=>(GridResults.ItemsSource as IEnumerable<SearchResultRow>)?.ToArray()??[];
 // The row a toolbar or menu action is about: the one clicked last, else the selected one.
 SearchResultRow? ActionRow()=>GridResults.CurrentItem as SearchResultRow??GridResults.SelectedItem as SearchResultRow;

 void UpdateTableTools()
 {
  if(BtnClearTable is null)return;
  var rows=!running&&GridResults.ItemsSource is IEnumerable<SearchResultRow> source&&source.Any();
  TxtFindSeed.IsEnabled=rows;BtnFindSeed.IsEnabled=rows;BtnClearTable.IsEnabled=rows;BtnClearSorting.IsEnabled=rows;BtnSetReference.IsEnabled=rows;BtnCompareSelected.IsEnabled=rows&&referenceRow is not null;
  BtnEndComparison.IsEnabled=!running&&referenceRow is not null;CompareLegend.Visibility=referenceRow is null?Visibility.Collapsed:Visibility.Visible;
 }

 void TableSourceChanged()
 {
  var rows=TableRows();
  if(referenceRow is not null&&!rows.Contains(referenceRow,ReferenceEqualityComparer.Instance)){referenceRow=null;comparedRows.Clear();}
  comparedRows.IntersectWith(rows);ApplyComparison();
 }

 void ApplyComparison()
 {
  foreach(var row in TableRows())row.CompareMark=ReferenceEquals(row,referenceRow)?"★":"";
  if(GridResults.ItemsSource is not null)
  {
   GridResults.Items.Filter=referenceRow is not null&&comparedRows.Count>0?item=>ReferenceEquals(item,referenceRow)||comparedRows.Contains((SearchResultRow)item):null;
   GridResults.Items.Refresh();
  }
  UpdateTableTools();
 }

 internal Brush CompareBrush(SearchResultRow row,string? path)
 {
  if(referenceRow is null)return Brushes.Transparent;
  if(ReferenceEquals(row,referenceRow))return ReferenceTint;
  if(!comparedRows.Contains(row)||string.IsNullOrEmpty(path))return Brushes.Transparent;
  var value=CompareValue(row,path);var reference=CompareValue(referenceRow,path);
  if(value is null||reference is null||value==reference)return Brushes.Transparent;
  return value>reference?BetterTint:WorseTint;
 }
 // Every numeric column compares on its sort value (more is better everywhere); the score only when it is shown.
 static readonly Dictionary<string,Func<SearchResultRow,double?>> compareReaders=[];
 static double? CompareValue(SearchResultRow row,string path)
 {
  if(path==nameof(SearchResultRow.ScoreSortKey))return row.Score;
  if(!compareReaders.TryGetValue(path,out var read))
  {
   var property=typeof(SearchResultRow).GetProperty(path);
   read=property?.PropertyType==typeof(int)||property?.PropertyType==typeof(double)?item=>Convert.ToDouble(property.GetValue(item),CultureInfo.InvariantCulture):_=>null;
   compareReaders[path]=read;
  }
  return read(row);
 }

 void SetReferenceSeed(object sender,RoutedEventArgs e)
 {
  if(ActionRow() is not {} row){LblProgress.Text=Localization.Instance["SelectSeedFirst"];return;}
  referenceRow=row;comparedRows.Remove(row);ApplyComparison();
  LblProgress.Text=Localization.Instance.Format("ReferenceSetFormat",row.Seed.ToString("N0"));
 }

 void CompareSelectedSeeds(object sender,RoutedEventArgs e)
 {
  if(referenceRow is null){LblProgress.Text=Localization.Instance["NoReferenceSeed"];return;}
  var selected=GridResults.SelectedItems.OfType<SearchResultRow>().Where(row=>!ReferenceEquals(row,referenceRow)).ToArray();
  if(selected.Length==0){LblProgress.Text=Localization.Instance["NothingToCompare"];return;}
  comparedRows.Clear();comparedRows.UnionWith(selected);ApplyComparison();GridResults.UnselectAll();
  LblProgress.Text=Localization.Instance.Format("ComparingFormat",selected.Length.ToString("N0"),referenceRow.Seed.ToString("N0"));
 }

 void EndComparison(object sender,RoutedEventArgs e)
 {
  referenceRow=null;comparedRows.Clear();ApplyComparison();LblProgress.Text=Localization.Instance["ComparisonEnded"];
 }

 void ClearSorting(object sender,RoutedEventArgs e)
 {
  GridResults.Items.SortDescriptions.Clear();foreach(var column in GridResults.Columns)column.SortDirection=null;
  LblProgress.Text=Localization.Instance["SortingCleared"];
 }

 void ClearTable(object sender,RoutedEventArgs e)
 {
  var count=TableRows().Length;if(count==0)return;
  if(MessageBox.Show(this,Localization.Instance.Format("ClearTableConfirmFormat",count.ToString("N0")),Localization.Instance["ClearTableTitle"],MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  ClearTableRows();
 }
 void ClearTableRows(){GridResults.ItemsSource=null;BtnExportCsv.IsEnabled=false;LblProgress.Text=Localization.Instance["TableCleared"];}

 void FindSeedKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){FindSeed(sender,e);e.Handled=true;}}
 void FindSeed(object sender,RoutedEventArgs e)
 {
  // Separators and spaces are ignored, so "1.234.567" and "1,234,567" both find seed 1234567.
  if(!uint.TryParse(new string([..TxtFindSeed.Text.Where(char.IsDigit)]),out var seed)){LblProgress.Text=Localization.Instance["FindSeedInvalid"];return;}
  var row=TableRows().FirstOrDefault(candidate=>candidate.Seed==seed);
  if(row is null){LblProgress.Text=Localization.Instance.Format("SeedNotInTableFormat",seed.ToString("N0"));return;}
  var index=GridResults.Items.IndexOf(row);
  if(index<0){LblProgress.Text=Localization.Instance.Format("SeedHiddenByComparisonFormat",seed.ToString("N0"));return;}
  GridResults.SelectedItem=row;GridResults.CurrentItem=row;GridResults.ScrollIntoView(row);
  LblProgress.Text=Localization.Instance.Format("SeedFoundFormat",seed.ToString("N0"),(index+1).ToString("N0"),GridResults.Items.Count.ToString("N0"));
 }

 void PreviewActionRow(object sender,RoutedEventArgs e){if(ActionRow() is {} row)ShowPreview(row.Seed);}

 // Self-check: find, clear sorting, the comparison (filter and the real cell backgrounds) and clearing the table.
 internal bool SmokeTableTools()
 {
  ShowResults([..new uint[]{1,2,3,4}.Select(seed=>SeedSearcher.Describe(seed,activeProfile,FertilitySetting.Abundant))]);UpdateLayout();
  var rows=TableRows();
  TxtFindSeed.Text="3";FindSeed(this,new RoutedEventArgs());var findOk=GridResults.SelectedItem is SearchResultRow found&&found.Seed==3;
  TxtFindSeed.Text="999";FindSeed(this,new RoutedEventArgs());var missingOk=ReferenceEquals(GridResults.SelectedItem,rows[2]);
  var sortColumn=GridResults.Columns.First(column=>column.SortMemberPath=="LatiumAreaValue");
  GridResults.Items.SortDescriptions.Add(new SortDescription("LatiumAreaValue",ListSortDirection.Descending));sortColumn.SortDirection=ListSortDirection.Descending;
  ClearSorting(this,new RoutedEventArgs());var sortOk=GridResults.Items.SortDescriptions.Count==0&&GridResults.Columns.All(column=>column.SortDirection is null)&&ReferenceEquals(GridResults.Items[0],rows[0]);
  // With the grid-wide cell style in place, a click on a column header still sorts (the header's own click handler).
  var header=VisualDescendants(GridResults).OfType<System.Windows.Controls.Primitives.DataGridColumnHeader>().First(candidate=>candidate.Column==sortColumn);
  typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(header,null);
  var headerSortOk=GridResults.Items.SortDescriptions.Count==1&&sortColumn.SortDirection is not null;ClearSorting(this,new RoutedEventArgs());
  GridResults.SelectedItem=rows[0];GridResults.CurrentItem=rows[0];SetReferenceSeed(this,new RoutedEventArgs());
  var referenceOk=ReferenceEquals(referenceRow,rows[0])&&rows[0].CompareMark=="★"&&GridResults.Items.Count==4;
  GridResults.SelectedItems.Clear();GridResults.SelectedItems.Add(rows[1]);GridResults.SelectedItems.Add(rows[3]);CompareSelectedSeeds(this,new RoutedEventArgs());UpdateLayout();
  var filterOk=GridResults.Items.Count==3&&GridResults.Items.IndexOf(rows[2])<0;
  var expected=rows[1].LatiumAreaValue>rows[0].LatiumAreaValue?BetterTint:rows[1].LatiumAreaValue<rows[0].LatiumAreaValue?WorseTint:Brushes.Transparent;
  Brush? CellBackground(SearchResultRow row)=>VisualDescendants(GridResults).OfType<DataGridCell>().FirstOrDefault(cell=>cell.Column?.SortMemberPath=="LatiumAreaValue"&&ReferenceEquals(cell.DataContext,row))?.Background;
  GridResults.SelectedItem=rows[3];UpdateLayout();var selectedBackground=CellBackground(rows[3]) as SolidColorBrush;
  var selectionOk=selectedBackground is not null&&selectedBackground.Color.A>0&&selectedBackground.Color!=Colors.White&&selectedBackground!=BetterTint&&selectedBackground!=WorseTint;GridResults.UnselectAll();UpdateLayout();
  var tintOk=rows[1].LatiumAreaValue!=rows[0].LatiumAreaValue&&CellBackground(rows[1])==expected&&CellBackground(rows[0])==ReferenceTint;
  TxtFindSeed.Text="3";FindSeed(this,new RoutedEventArgs());var hiddenOk=GridResults.SelectedItem is null;
  EndComparison(this,new RoutedEventArgs());UpdateLayout();var endOk=referenceRow is null&&GridResults.Items.Count==4&&rows[0].CompareMark==""&&CellBackground(rows[1])==Brushes.Transparent;
  ClearTableRows();var clearOk=GridResults.Items.Count==0&&!BtnClearTable.IsEnabled&&!BtnExportCsv.IsEnabled;
  return findOk&&missingOk&&sortOk&&headerSortOk&&referenceOk&&filterOk&&selectionOk&&tintOk&&hiddenOk&&endOk&&clearOk;
 }
 // Preview aid: a comparison of three seeds with a reference seed.
 internal void SmokeShowComparison()
 {
  ShowResults([..new uint[]{6153,1,2,3,4}.Select(seed=>SeedSearcher.Describe(seed,activeProfile,FertilitySetting.Abundant))]);UpdateLayout();var rows=TableRows();
  GridResults.CurrentItem=rows[0];SetReferenceSeed(this,new RoutedEventArgs());
  GridResults.SelectedItems.Clear();foreach(var row in rows.Skip(1).Take(3))GridResults.SelectedItems.Add(row);CompareSelectedSeeds(this,new RoutedEventArgs());
 }

 sealed class CompareTint(MainWindow window):IMultiValueConverter
 {
  public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)=>values is [SearchResultRow row,DataGridColumn column,..]?window.CompareBrush(row,column.SortMemberPath):Brushes.Transparent;
  public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>throw new NotSupportedException();
 }
}
