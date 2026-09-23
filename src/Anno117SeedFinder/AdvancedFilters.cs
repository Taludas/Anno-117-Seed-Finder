// Advanced fertility filters: the harbour or marsh tiles of a region, summed over the islands that carry a given fertility.
internal enum AdvancedFilter{LatiumHarbourMurex,LatiumHarbourOysters,AlbionHarbourSaltwort,AlbionHarbourSeaShells,AlbionMarshSmallBirds,AlbionMarshBeaver}
internal enum IslandTileKind{Harbour,Marsh}
internal sealed record AdvancedFilterDefinition(AdvancedFilter Filter,RegionKind Region,uint Fertility,IslandTileKind Tiles);

internal static class AdvancedFilters
{
 public const int Count=6;
 // The order matches the AdvancedFilter enum, so a definition can be found by its index.
 public static readonly AdvancedFilterDefinition[] All=
 [
  new(AdvancedFilter.LatiumHarbourMurex,RegionKind.Latium,4051,IslandTileKind.Harbour),
  new(AdvancedFilter.LatiumHarbourOysters,RegionKind.Latium,2208,IslandTileKind.Harbour),
  new(AdvancedFilter.AlbionHarbourSaltwort,RegionKind.Albion,2218,IslandTileKind.Harbour),
  new(AdvancedFilter.AlbionHarbourSeaShells,RegionKind.Albion,8432,IslandTileKind.Harbour),
  new(AdvancedFilter.AlbionMarshSmallBirds,RegionKind.Albion,2219,IslandTileKind.Marsh),
  new(AdvancedFilter.AlbionMarshBeaver,RegionKind.Albion,4082,IslandTileKind.Marsh)
 ];
 public static AdvancedFilterDefinition Get(AdvancedFilter filter)=>All[(int)filter];

 // The same value RegionMetrics.Calculate accumulates, computed straight from the definition.
 public static int Tiles(AdvancedFilterDefinition definition,IEnumerable<GeneratedIsland> islands)
 {
  var total=0;
  foreach(var island in islands)
  {
   if(!island.Fertilities.Contains(definition.Fertility))continue;
   var area=IslandAreas.Get(island.Name);total+=definition.Tiles==IslandTileKind.Harbour?area.Harbour:area.Swamp;
  }
  return total;
 }
}
