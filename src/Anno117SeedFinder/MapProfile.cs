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
 IReadOnlyList<MapSpecial> AlbionSpecials)
{
 public string DisplayName=>$"{Template} / {Size} / {(Dlc01?"PoA":"Vanilla")}";
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
 public static MapProfile Get(MapTemplateKind template,MapSizeKind size,bool dlc01=true)=>MapProfileData.All[(template,size,dlc01)];
}
