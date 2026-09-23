internal sealed record MapPositionDefinition(int SlotIndex,string Label,string Size,double X,double Y,int RawX,int RawY);

internal static class MapLayoutPositions
{
 public static IReadOnlyList<MapPositionDefinition> For(MapProfile profile,RegionKind region,FertilitySetKind set)
 {
  var positions=ForPreview(profile,region).Where(position=>position.SlotIndex>=0);
  if(set==FertilitySetKind.AnyCombination)return positions.ToArray();
  var starter=set==FertilitySetKind.Starter;
  var starterSlots=profile.Slots(region).Where(slot=>slot.Type==1).Select(slot=>slot.Index).ToHashSet();
  return positions.Where(position=>starterSlots.Contains(position.SlotIndex)==starter).ToArray();
 }

 public static IReadOnlyList<MapPositionDefinition> ForPreview(MapProfile profile,RegionKind region)
 {
  var projection=CreateProjection(profile,region);
  var result=profile.Slots(region).Select(slot=>
  {
   var size=ShortSize(slot.Size);var half=Half(slot.Size);var point=projection(slot.X+half,slot.Y+half);
   return new MapPositionDefinition(slot.Index,$"#{slot.Index} · {size}",size,point.X,point.Y,slot.X,slot.Y);
  }).ToList();
  if(region==RegionKind.Latium&&profile.Dlc01)
  {
   var cinis=projection(1920+384,1920+384);
   result.Add(new(-1,"Cinis","C",cinis.X,cinis.Y,1920,1920));
  }
  return result;
 }

 public static IReadOnlyList<(string Kind,double X,double Y)> SpecialPoints(MapProfile profile,RegionKind region)
 {
  var projection=CreateProjection(profile,region);
  return profile.Specials(region).Select(special=>
  {
   var half=special.Kind=="Raider"?160:128;var point=projection(special.X+half,special.Y+half);
   return(special.Kind,point.X,point.Y);
  }).ToArray();
 }

 // One fixed isometric projection for every template, size and region: all maps share the same world size (2048 units, 2688 with
 // DLC01), so a world rectangle always lands on the same canvas rectangle and the map borders can be drawn exactly.
// Map corner (0,0) is the bottom vertex; x grows towards the right vertex, y towards the left vertex.
 internal const double WorldOriginX=350;
 internal const double WorldOriginY=880;
 internal const double ScaleX=.17;
 internal const double ScaleY=.172;
 internal static (double X,double Y) Project(double x,double y)=>(WorldOriginX+(x-y)*ScaleX,WorldOriginY-(x+y)*ScaleY);

 static Func<int,int,(double X,double Y)> CreateProjection(MapProfile profile,RegionKind region)=>(x,y)=>Project(x,y);

 static int Half(string size)=>size switch{"Small"=>128,"Medium"=>160,"Large" or "XL"=>216,_=>128};
 static string ShortSize(string size)=>size switch{"Small"=>"S","Medium"=>"M","Large"=>"L",_=>size};
}
