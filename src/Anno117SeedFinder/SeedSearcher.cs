using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal sealed record SearchRequest(int FirstSeed,int MaxSeed,int Threads,int Limit,string Output,uint CinisSlot1,uint[] CinisPool,bool CinisMaxSites,IReadOnlyList<IslandCondition>? Conditions=null,MapProfile? Profile=null,int MinLatiumGoldSites=0,int MinLatiumSturgeonSites=0,int MinLatiumMountainSites=0,int MinLatiumRiverSites=0,int MinAlbionMountainSites=0,int MinLatiumBuildableTiles=0,int MinAlbionBuildableTiles=0,int MinAlbionSwampTiles=0,FertilitySetting FertilitySetting=FertilitySetting.Abundant,SlotSetting SlotSetting=SlotSetting.Abundant,int MinLatiumMineralMines=0,int MinAlbionCopperMines=0,int MinAlbionSilverMines=0,int MinLatiumMarbleSites=0,int MinLatiumGoldMines=0,int MinAlbionTinMines=0,IReadOnlyList<uint>? SeedList=null,IReadOnlyList<int>? AdvancedMinimums=null
 )
{
 // Minimum tile counts of the advanced fertility filters, indexed by AdvancedFilter; 0 (or no list) means the filter is off.
 public int AdvancedMinimum(AdvancedFilter filter)=>AdvancedMinimums is null||(int)filter>=AdvancedMinimums.Count?0:AdvancedMinimums[(int)filter];
 // With a seed list the search runs over exactly those seeds (FirstSeed and MaxSeed are then only their smallest and largest);
 // without one it runs over the range FirstSeed..MaxSeed. Positions 0..SeedCount-1 address either.
 public int SeedCount=>SeedList?.Count??MaxSeed-FirstSeed+1;
 public uint SeedAt(int position)=>SeedList is null?(uint)(FirstSeed+position):SeedList[position];
}
internal sealed record SearchHit(uint Seed,uint[] Fertilities,SiteCounts CinisSites,RegionMetrics Latium,RegionMetrics Albion);
internal sealed record PartialSearchHit(uint Seed,uint[] Fertilities,SiteCounts CinisSites,RegionMetrics Latium,RegionMetrics? Albion);
internal sealed record SearchProgress(int Processed,int Total,int Hits);
internal sealed record SearchSummary(SearchHit[] Hits,TimeSpan Elapsed,string Output,string Strategy,bool Canceled,int Processed,int FoundCount);
internal sealed record SelfTestResult(bool Success,int Passed,int Total,uint[] Failed);

internal static class SeedLimits
{
 public const int Minimum=1;
 public const int Maximum=999_999_999;
}

internal static class SeedSearcher
{
 public static Task<SearchSummary> SearchAsync(SearchRequest request,IProgress<SearchProgress>? progress,CancellationToken cancellationToken)
 {
  Validate(request);
  return Task.Run(()=>
  {
   var profile=request.Profile??MapProfiles.Default;
   var plan=CompiledSearchPlan.Create(request);
   var hits=new ConcurrentBag<PartialSearchHit>();
   var processed=0;var found=0;var canceled=false;var order=SearchRegionOrder.LatiumFirst;
   var sw=Stopwatch.StartNew();var totalSeeds=request.SeedCount;
   try
   {
    order=ChooseOrder(request,plan,cancellationToken);
    var options=new ParallelOptions{MaxDegreeOfParallelism=request.Threads,CancellationToken=cancellationToken};
    var limited=request.Limit>0;var chunkSize=limited?512:Math.Min(4096,Math.Max(1,totalSeeds/(request.Threads*4)));var batchSize=limited?Math.Max(chunkSize,chunkSize*request.Threads):totalSeeds;
    for(var batchStart=0;batchStart<totalSeeds;batchStart+=batchSize)
    {
     var batchEnd=Math.Min(totalSeeds,batchStart+batchSize);
     Parallel.ForEach(Partitioner.Create(batchStart,batchEnd,chunkSize),options,()=>new GeneratorScratch(),(range,_,scratch)=>
     {
      var completedInRange=0;
      try
      {
       for(var position=range.Item1;position<range.Item2;position++)
       {
        var seed=request.SeedAt(position);
        options.CancellationToken.ThrowIfCancellationRequested();LatiumGeneration? latium=null;List<GeneratedIsland>? albion=null;RegionMetrics? albionMetrics=null;var matches=false;
        if(order==SearchRegionOrder.AlbionFirst)
        {
         albion=AlbionGenerator.Generate(seed,profile,fertilitySetting:request.FertilitySetting,slotSetting:request.SlotSetting);
         if(plan.MatchesAlbion(albion,out var measured)){albionMetrics=measured;matches=MatchLatium(plan,seed,scratch,profile,request.FertilitySetting,out latium,request.SlotSetting);}
        }
        else
        {
         matches=MatchLatium(plan,seed,scratch,profile,request.FertilitySetting,out latium,request.SlotSetting);
         if(matches&&order==SearchRegionOrder.LatiumFirst){albion=AlbionGenerator.Generate(seed,profile,fertilitySetting:request.FertilitySetting,slotSetting:request.SlotSetting);matches=plan.MatchesAlbion(albion,out var measured);albionMetrics=measured;}
        }
        if(matches&&latium is not null){hits.Add(new(seed,latium.CinisFertilities,latium.CinisSites,latium.Metrics,albionMetrics));Interlocked.Increment(ref found);}
        completedInRange++;
       }
      }
      finally
      {
       var done=Interlocked.Add(ref processed,completedInRange);var currentFound=Volatile.Read(ref found);progress?.Report(new(done,totalSeeds,limited?Math.Min(currentFound,request.Limit):currentFound));
      }
      return scratch;
     },_=>{});
     if(limited&&Volatile.Read(ref found)>=request.Limit)break;
    }
   }
   catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){canceled=true;}
   var selected=hits.OrderBy(x=>x.Seed).Take(request.Limit<=0?int.MaxValue:request.Limit).ToArray();
   var result=new SearchHit[selected.Length];Parallel.For(0,selected.Length,new ParallelOptions{MaxDegreeOfParallelism=request.Threads},index=>
   {
    // The displayed rows carry fertility-derived counts (copper, silver, beaver …), so the
    // shown hits always use the full Albion generation rather than the totals-only fast path.
    var hit=selected[index];var albion=hit.Albion??RegionMetrics.Calculate(AlbionGenerator.Generate(hit.Seed,profile,fertilitySetting:request.FertilitySetting,slotSetting:request.SlotSetting));result[index]=new(hit.Seed,hit.Fertilities,hit.CinisSites,hit.Latium,albion);
   });
   var output=Path.GetFullPath(request.Output);
   Directory.CreateDirectory(Path.GetDirectoryName(output)!);
   // A search over a seed list that finds nothing keeps the existing output file: it may be the list that was loaded.
   if(request.SeedList is null||result.Length>0)File.WriteAllLines(output,result.Select(x=>x.Seed.ToString()));
   var reportedFound=request.Limit>0?result.Length:found;return new SearchSummary(result,sw.Elapsed,output,order switch{SearchRegionOrder.LatiumOnly=>Localization.Instance["StrategyLatiumOnly"],SearchRegionOrder.LatiumFirst=>Localization.Instance["StrategyLatiumFirst"],_=>Localization.Instance["StrategyAlbionFirst"]},canceled,processed,reportedFound);
  });
 }

 static bool MatchLatium(CompiledSearchPlan plan,uint seed,GeneratorScratch scratch,MapProfile profile,FertilitySetting setting,out LatiumGeneration? latium,SlotSetting slots=SlotSetting.Abundant)
 {
  latium=Generator.GenerateLatium(seed,scratch,profile,fertilitySetting:setting,slotSetting:slots);return plan.MatchesLatium(latium);
 }

 // Builds the full result row for a single seed, ignoring every filter. Used by the
 // "load seeds from file" path, which reports whatever the listed seeds happen to be.
 public static SearchHit Describe(uint seed,MapProfile profile,FertilitySetting setting,SlotSetting slots=SlotSetting.Abundant)
 {
  var latium=Generator.GenerateLatium(seed,new GeneratorScratch(),profile,fertilitySetting:setting,slotSetting:slots);
  var albion=RegionMetrics.Calculate(AlbionGenerator.Generate(seed,profile,fertilitySetting:setting,slotSetting:slots));
  return new(seed,latium.CinisFertilities,latium.CinisSites,latium.Metrics,albion);
 }

 // Shared by the window's CSV export and the headless --describe-seeds switch so both
 // always produce the same columns.
 // Same order as the result table: tiles, total slots, fertility-specific slots, Cinis.
 public const string CsvHeader="Seed;LatiumFlaeche;AlbionFlaeche;AlbionSumpf;LatiumBerg;LatiumFluss;AlbionBerg;GoldFluss;StoerFluss;RohmarmorMinen;MineralienMinen;GoldMinen;SilberMinen;ZinnMinen;KupferMinen;LatiumHafenMurex;LatiumHafenOysters;AlbionHafenSaltwort;AlbionHafenSeaShells;AlbionSumpfSmallBirds;AlbionSumpfBeaver;CinisFruchtbarkeiten";
 public static string CsvRow(SearchHit hit)=>string.Join(';',
  hit.Seed,hit.Latium.BuildableTiles,hit.Albion.BuildableTiles,hit.Albion.SwampTiles,
  hit.Latium.Sites.Mountain,hit.Latium.Sites.River,hit.Albion.Sites.Mountain,
  hit.Latium.GoldSites,hit.Latium.SturgeonSites,
  hit.Latium.MarbleSites,hit.Latium.MineralMineSites,hit.Latium.GoldMineSites,hit.Albion.SilverMineSites,hit.Albion.TinMineSites,hit.Albion.CopperMineSites,
  string.Join(';',AdvancedFilters.All.Select(definition=>(definition.Region==RegionKind.Latium?hit.Latium:hit.Albion).Tiles(definition.Filter))),
  string.Join(' ',hit.Fertilities.Select(Generator.Label)));

 static SearchRegionOrder ChooseOrder(SearchRequest request,CompiledSearchPlan plan,CancellationToken cancellationToken)
 {
  var profile=request.Profile??MapProfiles.Default;
  if(!plan.NeedsAlbion)return SearchRegionOrder.LatiumOnly;
  var total=request.SeedCount;if(total<100_000)return SearchRegionOrder.LatiumFirst;
  const int samples=256;var scratch=new GeneratorScratch();var latiumPass=0;var albionPass=0;
  var latiumWatch=Stopwatch.StartNew();
  for(var index=0;index<samples;index++)
  {
   cancellationToken.ThrowIfCancellationRequested();var seed=request.SeedAt((int)((long)index*total/samples));
   if(plan.MatchesLatium(Generator.GenerateLatium(seed,scratch,profile,fertilitySetting:request.FertilitySetting,slotSetting:request.SlotSetting)))latiumPass++;
  }
  latiumWatch.Stop();var albionWatch=Stopwatch.StartNew();
  for(var index=0;index<samples;index++)
  {
   cancellationToken.ThrowIfCancellationRequested();var seed=request.SeedAt((int)((long)index*total/samples));
   if(plan.MatchesAlbion(AlbionGenerator.Generate(seed,profile,fertilitySetting:request.FertilitySetting,slotSetting:request.SlotSetting)))albionPass++;
  }
  albionWatch.Stop();
  var latiumRate=(latiumPass+.5)/(samples+1d);var albionRate=(albionPass+.5)/(samples+1d);
  var latiumFirst=latiumWatch.ElapsedTicks+latiumRate*albionWatch.ElapsedTicks;
  var albionFirst=albionWatch.ElapsedTicks+albionRate*latiumWatch.ElapsedTicks;
  return albionFirst<latiumFirst?SearchRegionOrder.AlbionFirst:SearchRegionOrder.LatiumFirst;
 }

 static void Validate(SearchRequest request)
 {
  if(request.SeedList is null)
  {
   if(request.FirstSeed<SeedLimits.Minimum)throw new ArgumentException(Localization.Instance.Format("FirstSeedMustBeAtLeastFormat",SeedLimits.Minimum.ToString("N0")));
   if(request.MaxSeed>SeedLimits.Maximum)throw new ArgumentException(Localization.Instance.Format("LastSeedMustBeAtMostFormat",SeedLimits.Maximum.ToString("N0")));
   if(request.MaxSeed<request.FirstSeed)throw new ArgumentException(Localization.Instance["LastSeedMustBeGreaterOrEqual"]);
  }
  else if(request.SeedList.Count==0||request.SeedList.Any(seed=>seed<SeedLimits.Minimum||seed>SeedLimits.Maximum))throw new ArgumentException(Localization.Instance["NoSeedsInTable"]);
  if(request.Threads<1)throw new ArgumentException(Localization.Instance["AtLeastOneThreadRequired"]);
  if(request.CinisSlot1 is not(0 or 2206 or 2209))throw new ArgumentException(Localization.Instance["InvalidCinisSlot1Choice"]);
  if(request.CinisPool.Length>4||request.CinisPool.Distinct().Count()!=request.CinisPool.Length||request.CinisPool.Any(x=>!Generator.Pool6.Contains(x)))
   throw new ArgumentException(Localization.Instance["InvalidCinisPoolCount"]);
  if(string.IsNullOrWhiteSpace(request.Output))throw new ArgumentException(Localization.Instance["OutputFileRequired"]);
  if(request.MinLatiumGoldMines<0||request.MinLatiumMarbleSites<0||request.MinLatiumMineralMines<0||request.MinAlbionCopperMines<0||request.MinAlbionSilverMines<0||request.MinAlbionTinMines<0||request.MinLatiumGoldSites<0||request.MinLatiumSturgeonSites<0||request.MinLatiumMountainSites<0||request.MinLatiumRiverSites<0||request.MinAlbionMountainSites<0||request.MinLatiumBuildableTiles<0||request.MinAlbionBuildableTiles<0||request.MinAlbionSwampTiles<0||(request.AdvancedMinimums??[]).Any(value=>value<0))throw new ArgumentException(Localization.Instance["MinimumsNotNegative"]);
  foreach(var condition in request.Conditions??[])
  {
   var positions=condition.RequiredSlotIndices??[];
   var profile=request.Profile??MapProfiles.Default;
   if(positions.Distinct().Count()!=positions.Length||positions.Any(slot=>!MapLayoutPositions.For(profile,condition.Region,condition.Set).Any(position=>position.SlotIndex==slot)))throw new ArgumentException(Localization.Instance["InvalidPositionForProfile"]);
  }
 }


 public static SelfTestResult RunSelfTest()
 {
  var refs=new Dictionary<uint,(uint[] Fertilities,SiteCounts Sites)>
  {
   [2827]=([2206,4062,4049,32027,8577,2208,2205],new(18,22)),[2829]=([2209,4062,4049,2202,8577,2205,4051],new(18,23)),
   [2831]=([2209,4062,4049,8577,2205,4051,2208],new(17,22)),[2832]=([2209,4049,4062,32027,2205,4051,2202],new(18,21)),
   [2833]=([2209,4049,4062,2208,8577,32027,2205],new(19,23)),[2834]=([2206,4062,4049,2208,4051,2202,32027],new(17,23)),
   [2835]=([2209,4062,4049,32027,2208,2205,8577],new(17,23)),[2836]=([2209,4062,4049,2202,32027,4051,2208],new(18,22)),
   [2837]=([2206,4049,4062,32027,4051,2205,2202],new(18,22)),[2838]=([2209,4062,4049,2208,32027,2202,4051],new(19,23)),
   [2839]=([2209,4062,4049,8577,2208,2202,2205],new(18,21)),[2840]=([2206,4062,4049,2202,2208,2205,8577],new(18,21)),
   [2845]=([2209,4062,4049,2205,2202,4051,32027],new(19,23)),[2848]=([2206,4049,4062,2208,4051,2205,2202],new(18,21)),
   [2849]=([2209,4062,4049,32027,2208,4051,8577],new(18,21)),[2850]=([2206,4062,4049,32027,2202,4051,2205],new(17,21)),
   [2852]=([2209,4062,4049,2202,32027,8577,4051],new(17,21)),[2853]=([2206,4062,4049,2202,2205,8577,2208],new(19,23)),
   [2854]=([2206,4049,4062,4051,8577,32027,2208],new(18,23)),[2855]=([2206,4062,4049,32027,8577,2208,2202],new(17,21))
  };
  var tertiaryCounts=new Dictionary<uint,int>{[2827]=3,[2829]=1,[2831]=1,[2832]=0,[2833]=1,[2834]=1,[2835]=1,[2836]=0,[2837]=2,[2838]=0,[2839]=2,[2840]=1,[2845]=1,[2848]=0,[2849]=0,[2850]=0,[2852]=0,[2853]=1,[2854]=2,[2855]=0};
  var tertiaryRule=new IslandCondition(RegionKind.Latium,FertilitySetKind.Tertiary,0,[new([2,3],[2208,8577])]);
  var positionedTertiaryRule=tertiaryRule with{MinimumCount=1,RequiredSlotIndices=[6,8]};
  var anyLatiumRule=new IslandCondition(RegionKind.Latium,FertilitySetKind.AnyCombination,0,[new([0,1],[2206,2205])]);
  var positionedAnyLatiumRule=anyLatiumRule with{MinimumCount=1,RequiredSlotIndices=[6,8]};
  var failedLatium=refs.Where(x=>{var world=Generator.Generate(x.Key);var cinisPool=world.Fertilities[Generator.CinisName].Skip(3).ToArray();var expectedAny=world.GeneratedIslands.Count(island=>island.FertilitySet is 31312 or 3656 or 14198&&island.Fertilities.Contains(2206u)&&island.Fertilities.Contains(2205u));var expectedPosition=world.GeneratedIslands.Any(island=>island.SlotIndex is 6 or 8&&island.FertilitySet==14198&&island.Fertilities[2..4].Contains(2208u)&&island.Fertilities[2..4].Contains(8577u));var expectedAnyPosition=world.GeneratedIslands.Any(island=>island.SlotIndex is 6 or 8&&island.FertilitySet is 31312 or 3656 or 14198&&island.Fertilities.Contains(2206u)&&island.Fertilities.Contains(2205u));return !world.Fertilities[Generator.CinisName].SequenceEqual(x.Value.Fertilities)||world.Sites[Generator.CinisName]!=x.Value.Sites||SearchProfile.CountMatches(world,tertiaryRule)!=tertiaryCounts[x.Key]||SearchProfile.CountMatches(world,anyLatiumRule)!=expectedAny||SearchProfile.Matches(world,0,cinisPool,false,[positionedTertiaryRule])!=expectedPosition||SearchProfile.Matches(world,0,cinisPool,false,[positionedAnyLatiumRule])!=expectedAnyPosition;}).Select(x=>x.Key).ToArray();
  var albionRefs=new Dictionary<uint,string>{{2827,"3809085ACD55DC59BC12D80F62AAF2CCAA599BC423657AC850FAFB6E50AA6C7F"},{2831,"1FCDE2249A48C99742957D2E03E548C24114F0D2B3B394FB6CC73B489CBF693D"},{2835,"325A7C2F0285583A63A38A1B755C793294BDD1CF65FA68D9BE51B6B3804F5738"},{2848,"AB7B2C75126A546CB5B2F230E7B76691F7CA5011A736546F704103EF7DE51078"},{2855,"8F70171D0B9621009A0BC3C3573AAA5BA60D1C21CE3DC827168904CF91B92CC1"}};
  var albionShellCounts=new Dictionary<uint,int>{{2827,3},{2831,2},{2835,1},{2848,0},{2855,1}};var albionRule=new IslandCondition(RegionKind.Albion,FertilitySetKind.Tertiary,0,[new([4,5],[8432])]);
  var anyAlbionRule=new IslandCondition(RegionKind.Albion,FertilitySetKind.AnyCombination,0,[new([0,1],[2212,8432])]);
  var failedAlbion=albionRefs.Where(x=>{var world=Generator.Generate(x.Key);var expectedAny=world.GeneratedIslands.Count(island=>island.FertilitySet is 8174 or 8179 or 8181&&island.Fertilities.Contains(2212u)&&island.Fertilities.Contains(8432u));return AlbionDigest(world.GeneratedIslands)!=x.Value||SearchProfile.CountMatches(world,albionRule)!=albionShellCounts[x.Key]||SearchProfile.CountMatches(world,anyAlbionRule)!=expectedAny;}).Select(x=>x.Key).ToArray();
  const uint archipelagoAlbionSeed=8941790;var archipelagoProfile=MapProfiles.Get(MapTemplateKind.Archipelago,MapSizeKind.Large);var archipelagoAlbion=AlbionGenerator.Generate(archipelagoAlbionSeed,archipelagoProfile);var failedArchipelagoAlbion=AlbionDigest(archipelagoAlbion)!="36988EBDFF24F6129800B4312438C32DF3CEE31FB5C8A06F81BC32B0489888E0"?[archipelagoAlbionSeed]:Array.Empty<uint>();
  const uint archipelagoLatiumSeed=6153;var archipelagoLatium=Generator.GenerateLatium(archipelagoLatiumSeed,new GeneratorScratch(),archipelagoProfile);var archipelago6153Albion=AlbionGenerator.Generate(archipelagoLatiumSeed,archipelagoProfile);var archipelagoAlbionMetrics=RegionMetrics.Calculate(archipelago6153Albion);
  var siteRequest=new SearchRequest(1,1,1,0,"unused.txt",0,[],false,Profile:archipelagoProfile,MinLatiumGoldSites:archipelagoLatium.Metrics.GoldSites,MinLatiumSturgeonSites:archipelagoLatium.Metrics.SturgeonSites,MinLatiumMountainSites:archipelagoLatium.Metrics.Sites.Mountain,MinLatiumRiverSites:archipelagoLatium.Metrics.Sites.River,MinAlbionMountainSites:archipelagoAlbionMetrics.Sites.Mountain,MinLatiumBuildableTiles:archipelagoLatium.Metrics.BuildableTiles,MinAlbionBuildableTiles:archipelagoAlbionMetrics.BuildableTiles,MinAlbionSwampTiles:archipelagoAlbionMetrics.SwampTiles);var sitePlan=CompiledSearchPlan.Create(siteRequest);
  var quickAlbion=AlbionGenerator.GenerateMetricsTotals(archipelagoLatiumSeed,archipelagoProfile);
  var failedArchipelagoLatium=LatiumDigest(archipelagoLatium.Islands)!="A92F1A83DE3795904C7766C40106F8E31EF309E1D750E1524143B3BD8BB81D61"||AlbionSlotDigest(archipelago6153Albion)!="D34B31F3F16C0E5441D439A6A6B97C46564E316ECBE303D14370A60C60B8DE24"||archipelagoLatium.CinisSites is not {Mountain:19,River:23}||archipelagoLatium.Metrics.Sites is not {Mountain:146,River:143}||archipelagoAlbionMetrics.Sites is not {Mountain:116,Marsh:72}||quickAlbion.Sites!=archipelagoAlbionMetrics.Sites||quickAlbion.BuildableTiles!=archipelagoAlbionMetrics.BuildableTiles||quickAlbion.SwampTiles!=archipelagoAlbionMetrics.SwampTiles||archipelagoLatium.Metrics.BuildableTiles<=0||archipelagoAlbionMetrics.BuildableTiles<=archipelagoAlbionMetrics.SwampTiles||archipelagoLatium.Islands.Single(x=>x.Name=="roman_island_medium_07").Sites.Mountain!=5||archipelago6153Albion.Single(x=>x.Name=="celtic_island_medium_06").Sites.Mountain!=7||!sitePlan.MatchesLatium(archipelagoLatium)||!sitePlan.MatchesAlbion(archipelago6153Albion)||CompiledSearchPlan.Create(siteRequest with{MinLatiumMountainSites=archipelagoLatium.Metrics.Sites.Mountain+1}).MatchesLatium(archipelagoLatium)||CompiledSearchPlan.Create(siteRequest with{MinLatiumRiverSites=archipelagoLatium.Metrics.Sites.River+1}).MatchesLatium(archipelagoLatium)||CompiledSearchPlan.Create(siteRequest with{MinAlbionMountainSites=archipelagoAlbionMetrics.Sites.Mountain+1}).MatchesAlbion(archipelago6153Albion)||CompiledSearchPlan.Create(siteRequest with{MinLatiumBuildableTiles=archipelagoLatium.Metrics.BuildableTiles+1}).MatchesLatium(archipelagoLatium)||CompiledSearchPlan.Create(siteRequest with{MinAlbionBuildableTiles=archipelagoAlbionMetrics.BuildableTiles+1}).MatchesAlbion(archipelago6153Albion)||CompiledSearchPlan.Create(siteRequest with{MinAlbionSwampTiles=archipelagoAlbionMetrics.SwampTiles+1}).MatchesAlbion(archipelago6153Albion)?[archipelagoLatiumSeed]:Array.Empty<uint>();
  const uint cornersLatiumSeed=1_531_943;var cornersProfile=MapProfiles.Get(MapTemplateKind.Corners,MapSizeKind.Large);var cornersLatium=Generator.GenerateLatium(cornersLatiumSeed,new GeneratorScratch(),cornersProfile);var cornersAlbion=AlbionGenerator.Generate(cornersLatiumSeed,cornersProfile);var cornersAlbionMetrics=RegionMetrics.Calculate(cornersAlbion);
  var failedCornersLatium=LatiumDigest(cornersLatium.Islands)!="0B215AD4078992E44E451B2AFA74C8AC9BDB62335D00EBA9958A3E1BED6D5858"||PlacementDigest(Generator.DebugPlacements(cornersLatiumSeed,cornersProfile))!="ACE62924019EF72B00D2C453F06ED58A7E93C5C83A777995729FD132C53B0069"||SiteDigest(cornersLatium.Islands)!="A942252CC25391E51E5948FADCD09CA22A2DB90F7B6ECE91AF0B3DC249422731"||cornersLatium.Metrics.Sites is not {Mountain:144,River:154}||cornersLatium.CinisSites is not {Mountain:19,River:23}||AlbionSlotDigest(cornersAlbion)!="5DAF895C9D6BCC022DF8F651384BF28546BADA6565D7E040A5B03B7D2CE5DC9A"||PlacementDigest(AlbionGenerator.DebugPlacements(cornersLatiumSeed,cornersProfile))!="23FE8DB441C1AD623FFAC52D3DBEAFCFEB225C12285545D179F372E37E1D87F7"||SiteDigest(cornersAlbion)!="4847B99F1A21C1578C4572B5C710AD2C967689FBAD0D82C31240E229110FA6CE"||cornersAlbionMetrics.Sites is not {Mountain:112,Marsh:65}?[cornersLatiumSeed]:Array.Empty<uint>();
  var vanillaArchProfile=MapProfiles.Get(MapTemplateKind.Archipelago,MapSizeKind.Large,false);var vanillaArch=Generator.GenerateLatium(6153,new GeneratorScratch(),vanillaArchProfile);
  var vanillaCinisPlan=CompiledSearchPlan.Create(new SearchRequest(1,1,1,0,"unused.txt",2206,[32027],true,Profile:vanillaArchProfile));
  var saveReferences=new[]{(Seed:6153u,Profile:MapProfiles.Get(MapTemplateKind.Corners,MapSizeKind.Large,true),Count:23,Hash:"C8A4F892E491F1644168B2E604C8CD026DEE180B6ACD828094D2B58944764697",Cinis:true),(Seed:6153u,Profile:archipelagoProfile,Count:24,Hash:"06761B2A0BE172A5E395FA2FA851C769B9CFEBEB97E42C44F6AAAB4579753B33",Cinis:true),(Seed:6153u,Profile:vanillaArchProfile,Count:18,Hash:"215FA5D8C51DED3E614D5291F31DC1BADFDE4D56E49323E94F17B48A3F7E0CEF",Cinis:false),(Seed:2500u,Profile:MapProfiles.Get(MapTemplateKind.Corners,MapSizeKind.Small,false),Count:16,Hash:"2BA670840F94788B1BF7FBEC084F6F2E3DEE9143C10D53A5DCB93237074CAE50",Cinis:false)};
  var failedSaveReferences=saveReferences.Where(reference=>{var generated=Generator.GenerateLatium(reference.Seed,new GeneratorScratch(),reference.Profile);return generated.Islands.Count!=reference.Count||(generated.CinisFertilities.Length!=0)!=reference.Cinis||FertilityDigest(generated.Islands)!=reference.Hash;}).Select(reference=>reference.Seed).ToArray();if(!vanillaCinisPlan.MatchesLatium(vanillaArch))failedSaveReferences=[..failedSaveReferences,6153];
  // Reference maps for the Regular and Sparse fertility settings: the digest of each generated map's fertilities is checked.
  var fertilitySettingReferences=new[]
  {
   (Template:MapTemplateKind.Corners,Size:MapSizeKind.Large,Seed:2u,Setting:FertilitySetting.Regular,Count:23,Hash:"A89484202772E7D38D0BD7FDFFBC918742370914B4AC0CF66519458B99901847"),
   (Template:MapTemplateKind.Corners,Size:MapSizeKind.Large,Seed:2u,Setting:FertilitySetting.Sparse,Count:23,Hash:"FB7F9696B01D664BD02BC5F6EF3FC99A63474661A7109F6835E4004160DC0B98"),
   (Template:MapTemplateKind.Rift,Size:MapSizeKind.Small,Seed:2u,Setting:FertilitySetting.Regular,Count:25,Hash:"B6AB585216119972DF612E307E5B73CEC1467913DA548B8E6DB386DB24BB9B86"),
  };
  var failedFertilitySettings=fertilitySettingReferences.Where(reference=>{var profile=MapProfiles.Get(reference.Template,reference.Size);var generated=Generator.GenerateLatium(reference.Seed,new GeneratorScratch(),profile,fertilitySetting:reference.Setting);return generated.Islands.Count!=reference.Count||FertilityDigest(generated.Islands)!=reference.Hash;}).Select(reference=>reference.Seed).ToArray();
  var failedAdvanced=FailedAdvancedFilters();
  var failureCount=failedAdvanced.Length+failedLatium.Length+failedAlbion.Length+failedArchipelagoAlbion.Length+failedArchipelagoLatium.Length+failedCornersLatium.Length+failedSaveReferences.Length+failedFertilitySettings.Length;var total=refs.Count+albionRefs.Count+8+fertilitySettingReferences.Length;
  return new(failureCount==0,total-failureCount,total,failedAdvanced.Concat(failedLatium).Concat(failedAlbion).Concat(failedArchipelagoAlbion).Concat(failedArchipelagoLatium).Concat(failedCornersLatium).Concat(failedSaveReferences).Concat(failedFertilitySettings).Distinct().ToArray());
 }

 // Advanced filters on three maps: each value equals the tiles summed straight from the island definitions and lies inside the analysed range,
 // the compiled plan accepts the map at exactly that value and rejects it one tile above, and only the Albion filters make the search need Albion.
 static uint[] FailedAdvancedFilters()
 {
  var failed=new List<uint>();
  var cases=new[]{(Seed:6153u,Profile:MapProfiles.Get(MapTemplateKind.Archipelago,MapSizeKind.Large)),(Seed:2500u,Profile:MapProfiles.Get(MapTemplateKind.Corners,MapSizeKind.Small,false)),(Seed:6153u,Profile:MapProfiles.Get(MapTemplateKind.Rift,MapSizeKind.Medium,false))};
  foreach(var (seed,profile) in cases)
  {
   var latium=Generator.GenerateLatium(seed,new GeneratorScratch(),profile);var albion=AlbionGenerator.Generate(seed,profile);var albionMetrics=RegionMetrics.Calculate(albion);
   foreach(var definition in AdvancedFilters.All)
   {
    var inLatium=definition.Region==RegionKind.Latium;var value=(inLatium?latium.Metrics:albionMetrics).Tiles(definition.Filter);var range=AdvancedRanges.For(profile,definition.Filter);
    CompiledSearchPlan PlanFor(int minimum){var minimums=new int[AdvancedFilters.Count];minimums[(int)definition.Filter]=minimum;return CompiledSearchPlan.Create(new SearchRequest(1,1,1,0,"unused.txt",0,[],false,Profile:profile,AdvancedMinimums:minimums));}
    bool Passes(int minimum){var plan=PlanFor(minimum);return inLatium?plan.MatchesLatium(latium):plan.MatchesAlbion(albion);}
    if(value!=AdvancedFilters.Tiles(definition,inLatium?latium.Islands:albion)||value<range.Min||value>range.Max||!Passes(value)||Passes(value+1)||PlanFor(1).NeedsAlbion!=!inLatium)failed.Add(seed);
   }
  }
  return [..failed.Distinct()];
 }

 static string LatiumDigest(IEnumerable<GeneratedIsland> islands)
 {
  var rows=islands.Where(x=>x.FertilitySet is 31312 or 3656 or 14198 or 144793).OrderBy(x=>x.Name,StringComparer.Ordinal).Select(x=>$"{x.Name}|{x.SlotIndex}|{x.FertilitySet}|{string.Join(',',x.Fertilities)}");var canonical=string.Join(';',rows);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
 }

 static string FertilityDigest(IEnumerable<GeneratedIsland> islands)
 {
  var rows=islands.OrderBy(x=>x.Name,StringComparer.Ordinal).Select(x=>$"{x.Name}|{x.FertilitySet}|{string.Join(',',x.Fertilities)}");var canonical=string.Join(';',rows);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
 }

 static string PlacementDigest(IEnumerable<string> placements)
 {
  var rows=placements.Select(row=>row.Split('|')).Select(parts=>$"{parts[0]}|{parts[1]}|{parts[2]}").OrderBy(row=>row,StringComparer.Ordinal);var canonical=string.Join(';',rows);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
 }

 static string SiteDigest(IEnumerable<GeneratedIsland> islands)
 {
  var rows=islands.OrderBy(x=>x.Name,StringComparer.Ordinal).Select(x=>$"{x.Name}|{x.Sites.Mountain}|{x.Sites.River}|{x.Sites.Marsh}");var canonical=string.Join(';',rows);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
 }

 static string AlbionDigest(IEnumerable<GeneratedIsland> islands)
 {
  var rows=islands.Where(x=>x.FertilitySet is 8174 or 8179 or 8181).OrderBy(x=>x.Name,StringComparer.Ordinal).Select(x=>$"{x.Name}|{x.FertilitySet}|{string.Join(',',x.Fertilities)}");var canonical=string.Join(';',rows);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
 }

 static string AlbionSlotDigest(IEnumerable<GeneratedIsland> islands)
 {
  var rows=islands.Where(x=>x.FertilitySet is 8174 or 8179 or 8181).OrderBy(x=>x.Name,StringComparer.Ordinal).Select(x=>$"{x.Name}|{x.SlotIndex}|{x.FertilitySet}|{string.Join(',',x.Fertilities)}");var canonical=string.Join(';',rows);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
 }
}
