using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class FertilityIcons
{
 static readonly Dictionary<uint,ImageSource> Icons=[];

 public static ImageSource Get(uint guid)
 {
  if(Icons.TryGetValue(guid,out var icon))return icon;
  var image=new BitmapImage(new Uri($"pack://application:,,,/icons/{guid}.png",UriKind.Absolute));image.Freeze();Icons.Add(guid,image);return image;
 }

 public static void Configure(ComboBox box,IEnumerable<FertilityChoice> choices)
 {
  box.Items.Clear();box.ItemTemplate=ItemTemplate();box.Items.Add(FertilityChoice.Wildcard);foreach(var choice in choices)box.Items.Add(choice);
 }

 static DataTemplate ItemTemplate()
 {
  var panel=new FrameworkElementFactory(typeof(StackPanel));panel.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);panel.SetValue(StackPanel.VerticalAlignmentProperty,VerticalAlignment.Center);
  var image=new FrameworkElementFactory(typeof(Image));image.SetValue(Image.WidthProperty,16d);image.SetValue(Image.HeightProperty,16d);image.SetValue(Image.MarginProperty,new Thickness(0,0,6,0));image.SetValue(Image.VerticalAlignmentProperty,VerticalAlignment.Center);image.SetBinding(Image.SourceProperty,new Binding(nameof(FertilityChoice.Icon)));panel.AppendChild(image);
  var text=new FrameworkElementFactory(typeof(TextBlock));text.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);text.SetBinding(TextBlock.TextProperty,new Binding(nameof(FertilityChoice.Name)));panel.AppendChild(text);
  return new DataTemplate{VisualTree=panel};
 }
}
