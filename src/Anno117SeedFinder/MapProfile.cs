internal enum MapTemplateKind{Archipelago,Atoll,Rift,Corners,IslandChains}
internal enum MapSizeKind{Small,Medium,Large}
internal enum StartModeKind{Flagship,StartIsland}

internal sealed record MapSlot(int Index,int X,int Y,string Size,int Type);
internal sealed record MapSpecial(string Kind,int X,int Y);

internal sealed record MapProfile(
 MapTemplateKind Template,
 MapSizeKind Size,
 bool Dlc01,
 int LatiumTemplateSize,
 IReadOnlyList<MapSlot> LatiumSlots,
 IReadOnlyList<MapSlot> AlbionSlots,
 IReadOnlyList<MapSpecial> LatiumSpecials,
 IReadOnlyList<MapSpecial> AlbionSpecials,
 bool Retro=false)
{
 public string DisplayName=>$"{Template} / {Size} / {(Retro?"PoA (retroactive)":Dlc01?"PoA":"Vanilla")}";
 public IReadOnlyList<MapSlot> Slots(RegionKind region)=>region==RegionKind.Latium?LatiumSlots:AlbionSlots;
 public IReadOnlyList<MapSpecial> Specials(RegionKind region)=>region==RegionKind.Latium?LatiumSpecials:AlbionSpecials;
 public int StarterCount(RegionKind region)=>Slots(region).Count(slot=>slot.Type==1);
 public int RegularCount(RegionKind region)=>Slots(region).Count(slot=>slot.Type!=1);
 public int Capacity(RegionKind region,FertilitySetKind set)=>set switch
 {
  FertilitySetKind.Starter=>StarterCount(region),
  FertilitySetKind.Secondary or FertilitySetKind.Tertiary=>(RegularCount(region)+1)/2,
  _=>Slots(region).Count
 };
}

internal static class MapProfiles
{
 public static MapProfile Default=>Get(MapTemplateKind.Corners,MapSizeKind.Large,true);
 public static IEnumerable<MapProfile> All=>MapProfileData.All.Values;
 public static MapProfile Get(MapTemplateKind template,MapSizeKind size,bool dlc01=true,bool retro=false)
 {
  if(!retro||!dlc01)return MapProfileData.All[(template,size,dlc01)];
  lock(RetroProfiles)
  {
   if(RetroProfiles.TryGetValue((template,size),out var cached))return cached;
   // DLC01 switched on after the map was created: the map is the no-DLC map (its slots, third parties and Albion side),
   // the six slots that exist only in the DLC template come on top (they are the last six non-starter slots of the
   // DLC template) and Cinis is added. See GenerateLatiumRetro.
   var off=MapProfileData.All[(template,size,false)];var on=MapProfileData.All[(template,size,true)];
   var extra=on.LatiumSlots.Where(slot=>slot.Type!=1).TakeLast(RetroSlotCount).Select((slot,index)=>new MapSlot(off.LatiumSlots.Count+index,slot.X,slot.Y,slot.Size,slot.Type));
   return RetroProfiles[(template,size)]=new MapProfile(template,size,true,on.LatiumTemplateSize,[..off.LatiumSlots,..extra],off.AlbionSlots,off.LatiumSpecials,off.AlbionSpecials,true);
  }
 }
 public const int RetroSlotCount=6;
 static readonly Dictionary<(MapTemplateKind,MapSizeKind),MapProfile> RetroProfiles=new();
}
