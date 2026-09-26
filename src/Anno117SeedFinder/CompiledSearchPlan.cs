internal enum SearchRegionOrder{LatiumOnly,LatiumFirst,AlbionFirst}

internal sealed class CompiledSearchPlan
{
 readonly uint cinisSlot1;
 readonly uint[] cinisPool;
 readonly bool requireMaxSites;
 readonly bool hasCinis;
 readonly int minGoldSites;
 readonly int minSturgeonSites;
 readonly int minLatiumMountainSites;
 readonly int minLatiumRiverSites;
 readonly int minAlbionMountainSites;
 readonly int minLatiumBuildableTiles;
 readonly int minAlbionBuildableTiles;
 readonly int minAlbionSwampTiles;
 readonly int minLatiumGoldMines;
 readonly int minLatiumMarbleSites;
 readonly int minLatiumMineralMines;
 readonly int minAlbionCopperMines;
 readonly int minAlbionSilverMines;
 readonly int minAlbionTinMines;
 readonly int[] advancedLatium;
 readonly int[] advancedAlbion;
 readonly AdvancedFilter[] advancedLatiumFilters;
 readonly AdvancedFilter[] advancedAlbionFilters;
 readonly CompiledIslandCondition[] latium;
 readonly CompiledIslandCondition[] albion;

 CompiledSearchPlan(SearchRequest request)
 {
  hasCinis=request.Profile?.Dlc01??true;cinisSlot1=request.CinisSlot1;cinisPool=request.CinisPool;requireMaxSites=request.CinisMaxSites;
  minGoldSites=request.MinLatiumGoldSites;minSturgeonSites=request.MinLatiumSturgeonSites;
  minLatiumMountainSites=request.MinLatiumMountainSites;minLatiumRiverSites=request.MinLatiumRiverSites;minAlbionMountainSites=request.MinAlbionMountainSites;
  minLatiumBuildableTiles=request.MinLatiumBuildableTiles;minAlbionBuildableTiles=request.MinAlbionBuildableTiles;minAlbionSwampTiles=request.MinAlbionSwampTiles;
  minLatiumGoldMines=request.MinLatiumGoldMines;minLatiumMarbleSites=request.MinLatiumMarbleSites;minLatiumMineralMines=request.MinLatiumMineralMines;minAlbionCopperMines=request.MinAlbionCopperMines;minAlbionSilverMines=request.MinAlbionSilverMines;minAlbionTinMines=request.MinAlbionTinMines;
  advancedLatiumFilters=[..AdvancedFilters.All.Where(x=>x.Region==RegionKind.Latium&&request.AdvancedMinimum(x.Filter)>0).Select(x=>x.Filter)];advancedAlbionFilters=[..AdvancedFilters.All.Where(x=>x.Region==RegionKind.Albion&&request.AdvancedMinimum(x.Filter)>0).Select(x=>x.Filter)];
  advancedLatium=[..advancedLatiumFilters.Select(request.AdvancedMinimum)];advancedAlbion=[..advancedAlbionFilters.Select(request.AdvancedMinimum)];
  latium=(request.Conditions??[]).Where(x=>x.Region==RegionKind.Latium).Select(x=>new CompiledIslandCondition(x)).ToArray();
  albion=(request.Conditions??[]).Where(x=>x.Region==RegionKind.Albion).Select(x=>new CompiledIslandCondition(x)).ToArray();
 }

 public bool NeedsAlbion=>albion.Length!=0||minAlbionMountainSites>0||minAlbionBuildableTiles>0||minAlbionSwampTiles>0||minAlbionCopperMines>0||minAlbionSilverMines>0||minAlbionTinMines>0||advancedAlbionFilters.Length!=0;
 public static CompiledSearchPlan Create(SearchRequest request)=>new(request);


 public bool MatchesLatium(LatiumGeneration region)
 {
  if(hasCinis)
  {
   var cinis=region.CinisFertilities;
   if(cinisSlot1!=0&&cinis[0]!=cinisSlot1)return false;
   foreach(var required in cinisPool)
   {
    var found=false;for(var slot=3;slot<=6;slot++)if(cinis[slot]==required){found=true;break;}
    if(!found)return false;
   }
   if(requireMaxSites&&region.CinisSites is not {Mountain:19,River:23})return false;
  }
  if(region.Metrics.GoldSites<minGoldSites||region.Metrics.SturgeonSites<minSturgeonSites||region.Metrics.Sites.Mountain<minLatiumMountainSites||region.Metrics.Sites.River<minLatiumRiverSites||region.Metrics.BuildableTiles<minLatiumBuildableTiles||region.Metrics.MineralMineSites<minLatiumMineralMines||region.Metrics.MarbleSites<minLatiumMarbleSites||region.Metrics.GoldMineSites<minLatiumGoldMines)return false;
  for(var index=0;index<advancedLatiumFilters.Length;index++)if(region.Metrics.Tiles(advancedLatiumFilters[index])<advancedLatium[index])return false;
  foreach(var condition in latium)if(!condition.Matches(region.Islands))return false;
  return true;
 }

 public bool MatchesAlbion(IReadOnlyList<GeneratedIsland> region)=>MatchesAlbion(region,out _);
 public bool MatchesAlbion(IReadOnlyList<GeneratedIsland> region,out RegionMetrics metrics)
 {
  metrics=RegionMetrics.Calculate(region);
  if(metrics.Sites.Mountain<minAlbionMountainSites||metrics.BuildableTiles<minAlbionBuildableTiles||metrics.SwampTiles<minAlbionSwampTiles||metrics.CopperMineSites<minAlbionCopperMines||metrics.SilverMineSites<minAlbionSilverMines||metrics.TinMineSites<minAlbionTinMines)return false;
  for(var index=0;index<advancedAlbionFilters.Length;index++)if(metrics.Tiles(advancedAlbionFilters[index])<advancedAlbion[index])return false;
  foreach(var condition in albion)if(!condition.Matches(region))return false;
  return true;
 }
}

internal sealed class CompiledIslandCondition
{
 readonly bool anyCombination;
 readonly RegionKind region;
 readonly uint fertilitySet;
 readonly int minimum;
 readonly int requiredPositionMatches;
 readonly ulong positionMask;
 readonly CompiledSlotGroup[] groups;

 public CompiledIslandCondition(IslandCondition source)
 {
  region=source.Region;anyCombination=source.Set==FertilitySetKind.AnyCombination;
  fertilitySet=anyCombination?0:FertilityDefinitions.SetGuid(source.Region,source.Set);
  minimum=source.MinimumCount;
  groups=source.Groups.Select(x=>new CompiledSlotGroup(x.SlotIndices,x.Required,anyCombination)).ToArray();
  var positions=source.RequiredSlotIndices??[];
  requiredPositionMatches=Math.Min(minimum,positions.Length);
  foreach(var position in positions)positionMask|=1UL<<position;
 }

 public bool Matches(IReadOnlyList<GeneratedIsland> islands)
 {
  var total=0;var positioned=0;
  foreach(var island in islands)
  {
   if(!MatchesIsland(island))continue;
   total++;
   if(island.SlotIndex>=0&&(positionMask&(1UL<<island.SlotIndex))!=0)positioned++;
   if(total>=minimum&&positioned>=requiredPositionMatches)return true;
  }
  return total>=minimum&&positioned>=requiredPositionMatches;
 }

 bool MatchesIsland(GeneratedIsland island)
 {
  if(anyCombination)
  {
   if(!FertilityDefinitions.IsRegularSet(region,island.FertilitySet))return false;
  }
  else if(island.FertilitySet!=fertilitySet)return false;
  foreach(var group in groups)if(!group.Matches(island.Fertilities))return false;
  return true;
 }
}

internal sealed class CompiledSlotGroup(int[] slots,uint[] required,bool searchAllSlots)
{
 public bool Matches(uint[] actual)
 {
  foreach(var value in required)
  {
   var found=false;
   if(searchAllSlots)
   {
    for(var index=0;index<actual.Length;index++)if(actual[index]==value){found=true;break;}
   }
   else
   {
    foreach(var index in slots)if(actual[index]==value){found=true;break;}
   }
   if(!found)return false;
  }
  return true;
 }
}
