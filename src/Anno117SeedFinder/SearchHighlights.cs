// Island highlight in the seed preview: the islands that meet the island filters of the last search (its island conditions, the
// islands that must appear and the Cinis filter), so a hit shows which islands made it pass. A rule that keeps an island away and
// the area and slot minimums concern no single island, so they mark nothing.
internal sealed class SearchHighlights
{
 readonly CompiledIslandCondition[] conditions;
 readonly IslandRule[] rules;
 readonly bool cinis;
#if !GAME21
 readonly EgyptIslandRule[] egyptRules;
#endif

 SearchHighlights(SearchRequest request)
 {
  conditions=[..(request.Conditions??[]).Select(condition=>new CompiledIslandCondition(condition))];
  rules=[..(request.IslandRules??[]).Where(rule=>!rule.Exclude)];
  cinis=(request.Profile??MapProfiles.Default).Dlc01&&(request.CinisSlot1!=0||request.CinisPool.Length>0||request.CinisMaxSites);
#if !GAME21
  egyptRules=[..request.EgyptIslandRules??[]];
#endif
 }

 // null when the search had no island filter at all.
#if GAME21
 public static SearchHighlights? From(SearchRequest request){var highlights=new SearchHighlights(request);return highlights.conditions.Length+highlights.rules.Length>0||highlights.cinis?highlights:null;}
#else
 public static SearchHighlights? From(SearchRequest request){var highlights=new SearchHighlights(request);return highlights.conditions.Length+highlights.rules.Length+highlights.egyptRules.Length>0||highlights.cinis?highlights:null;}
#endif

 // The Cinis has slot -1 in Latium.
 public bool Matches(RegionKind region,GeneratedIsland island)=>island.SlotIndex<0?region==RegionKind.Latium&&cinis:
  conditions.Any(condition=>condition.Region==region&&condition.MatchesIsland(island))||rules.Any(rule=>rule.Region==region&&rule.Matches(island));
#if !GAME21
 public bool Matches(EgyptIsland island)=>egyptRules.Any(rule=>rule.Matches(island));
#endif
}
