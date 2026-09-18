internal sealed record MapPositionDefinition(int SlotIndex,string Label,string Size,double X,double Y,int RawX,int RawY);

internal static class MapLayoutPositions
{
 const double Width=700;
 const double Height=920;
 const double MarginX=58;
 const double MarginY=58;

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

 static Func<int,int,(double X,double Y)> CreateProjection(MapProfile profile,RegionKind region)
 {
  // Das bereits gegen das Spiel abgeglichene Corners/Large-Schema bleibt
  // pixelstabil. Die übrigen Profile verwenden dieselbe isometrische
  // Projektion, automatisch auf ihre jeweiligen technischen Slots skaliert.
  if(profile.Template==MapTemplateKind.Corners&&profile.Size==MapSizeKind.Large)
   return (x,y)=>(350d+(x-y)*.17d,870d-(x+y)*.172d);
  var centers=profile.Slots(region).Select(slot=>(X:slot.X+Half(slot.Size),Y:slot.Y+Half(slot.Size))).ToList();
  centers.AddRange(profile.Specials(region).Select(special=>(special.X+(special.Kind=="Raider"?160:128),special.Y+(special.Kind=="Raider"?160:128))));
  if(region==RegionKind.Latium&&profile.Dlc01)centers.Add((2304,2304));
  var u=centers.Select(point=>(double)point.X-point.Y).ToArray();
  var v=centers.Select(point=>(double)point.X+point.Y).ToArray();
  var minU=u.Min();var maxU=u.Max();var minV=v.Min();var maxV=v.Max();
  var spanU=Math.Max(1,maxU-minU);var spanV=Math.Max(1,maxV-minV);
  var availableX=Width-2*MarginX;var availableY=Height-2*MarginY;
  return (x,y)=>(MarginX+((x-y)-minU)/spanU*availableX,MarginY+(maxV-(x+y))/spanV*availableY);
 }

 static int Half(string size)=>size switch{"Small"=>128,"Medium"=>160,"Large" or "XL"=>216,_=>128};
 static string ShortSize(string size)=>size switch{"Small"=>"S","Medium"=>"M","Large"=>"L",_=>size};
}
