// The real placement of a generated map (final map frame: template position + IslandShift): every island, third-party island
// and decoration with its rotation, for the preview window. Filled by the generators when a layout object is passed in.
internal readonly record struct LayoutItem(string Name,string Kind,int Slot,byte Rotation,int X,int Y);

internal sealed class MapLayout
{
 public const string Island="island";
 public const string Special="special";
 public const string Decoration="deco";
 // The whole map and, when there is a smaller regular map inside it (DLC01 area), that regular map: origin and side length.
 public int FullX,FullY,FullSize,BaseX,BaseY,BaseSize;
 public int Width;
 public List<LayoutItem> Items{get;}=[];
 public void Add(string name,string kind,int slot,byte rotation,int x,int y)=>Items.Add(new(name,kind,slot,rotation,x,y));
}
