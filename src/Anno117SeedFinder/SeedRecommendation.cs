using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Recommended seeds: rates a seed for three playstyles and writes its description for the anno-companion.com Seed Atlas (an
// Elementor template, English and German). The criteria come from the Seed Atlas authors' list (Recommended Seeds Exporter sheet):
//  Casual players   good fertility spread and an easy settling order on large islands close to each other,
//  Full supply      building space, river slots, coastal fertilities and enough mines to supply every residence,
//  Record builders  the most building space and river slots, then marble and tin.
internal enum Playstyle{Casual,FullSupply,RecordBuilder}

// One rated property: its raw value, its rating 0..1 and its weight. Fraction values (fits) are shown as percent.
// Median: the population median of a metric (0 when the part has none).
internal sealed record ScorePart(string Key,double Value,double Normalized,double Weight,bool Fraction=false,double Median=0);
// A playstyle's rating 0..100 (weighted mean of the parts) and the minimums it misses (empty: the seed qualifies).
internal sealed record PlaystyleScore(Playstyle Playstyle,double Score,IReadOnlyList<ScorePart> Parts,IReadOnlyList<string> Failed){public bool Passes=>Failed.Count==0;}

// One seed with everything the ratings and the description need. Without `full` only the islands and their goods are generated
// (fast, for sweeps); with it also the map layouts and the settling guides.
internal sealed class SeedAnalysis
{
 public uint Seed{get;}public MapProfile Profile{get;}public FertilitySetting Fertility{get;}public SlotSetting Slots{get;}
 public StartModeKind StartMode{get;}public bool WithoutCinis{get;}
 public LatiumGeneration Latium{get;}public List<GeneratedIsland> Albion{get;}public RegionMetrics AlbionMetrics{get;}
 public MapLayout? LatiumLayout{get;}public MapLayout? AlbionLayout{get;}
 public List<GuideIsland>? LatiumIslands{get;}public List<GuideIsland>? AlbionIslands{get;}
 public GuideMap? LatiumMap{get;}public GuideMap? AlbionMap{get;}
 public GuidePlan? LatiumPlan{get;}public PopulationGuide? RomansFirst{get;}public PopulationGuide? CeltsFirst{get;}
 public bool Full=>LatiumLayout is not null;

 public SeedAnalysis(uint seed,MapProfile profile,FertilitySetting fertility,SlotSetting slots,bool full,StartModeKind startMode=StartModeKind.Flagship,bool withoutCinis=false)
 {
  Seed=seed;Profile=profile;Fertility=fertility;Slots=slots;StartMode=startMode;WithoutCinis=withoutCinis&&profile.Dlc01;
  LatiumLayout=full?new():null;AlbionLayout=full?new():null;
  Latium=Generator.GenerateLatium(seed,new GeneratorScratch(),profile,fertilitySetting:fertility,slotSetting:slots,layout:LatiumLayout);
  Albion=AlbionGenerator.Generate(seed,profile,fertilitySetting:fertility,slotSetting:slots,layout:AlbionLayout);AlbionMetrics=RegionMetrics.Calculate(Albion);
  if(!full)return;
  LatiumIslands=SeedPreviewWindow.GuideIslands(Latium.Islands,LatiumLayout!,RegionKind.Latium);LatiumMap=SeedPreviewWindow.GuideMapOf(LatiumLayout!,RegionKind.Latium);
  AlbionIslands=SeedPreviewWindow.GuideIslands(Albion,AlbionLayout!,RegionKind.Albion);AlbionMap=SeedPreviewWindow.GuideMapOf(AlbionLayout!,RegionKind.Albion);
  LatiumPlan=SettlingGuide.Latium(LatiumIslands,startMode==StartModeKind.StartIsland,LatiumMap,WithoutCinis);
  RomansFirst=SettlingGuide.Albion(AlbionIslands,true,AlbionMap);CeltsFirst=SettlingGuide.Albion(AlbionIslands,false,AlbionMap);
 }

 // The author's own choice of islands per region (Seed Atlas export); when set, descriptions and export images show it instead of
 // the settling guide. The ratings always use the guide.
 public GuidePlan? LatiumChoice{get;set;}public PopulationGuide? AlbionChoice{get;set;}
 public GuidePlan? ShownLatium=>LatiumChoice??LatiumPlan;
 public PopulationGuide? ShownAlbion(bool? romanFirst=null)=>AlbionChoice??AlbionGuide(romanFirst);
 // The order a description and the map show: the chosen one, else the better one.
 public PopulationGuide? AlbionGuide(bool? romanFirst=null)=>romanFirst switch{true=>RomansFirst??CeltsFirst,false=>CeltsFirst??RomansFirst,_=>Better(RomansFirst,CeltsFirst)};
 static PopulationGuide? Better(PopulationGuide? a,PopulationGuide? b)=>b is null||a is not null&&a.Score>=b.Score?a:b;
}

internal static class SeedRecommendation
{
 // ---- Fertilities ------------------------------------------------------------------------------------------------------------
 internal const uint Mackerel=2206,Lavender=2209,Resin=51212,Olives=2210,Grapes=2205,Flax=2202,Murex=4051,Sandarac=4052,Oysters=2208,Sturgeon=8577,Iron=4049,
  Marble=4062,Minerals=4053,Gold=32027,Barley=2212,Herbs=2214,DyePlants=2217,Saltwort=2218,SmallBirds=2219,Beaver=4082,Ponies=2211,SeaShells=8432,Copper=4063,
  Silver=8487,Tin=4064,Granite=4066;
 // Names (English, German) in the order a description lists them.
 static readonly (uint Guid,string En,string De)[] Names=
 [
  (Mackerel,"Mackerel","Makrelen"),(Lavender,"Lavender","Lavendel"),(Olives,"Olives","Oliven"),(Resin,"Resin","Harz"),(Grapes,"Grapes","Trauben"),(Flax,"Flax","Flachs"),
  (Murex,"Murex","Murex"),(Oysters,"Oysters","Austern"),(Sturgeon,"Sturgeon","Stör"),(Sandarac,"Sandarac","Sandarak"),(Minerals,"Minerals","Mineralien"),
  (Marble,"Marble","Marmor"),(Gold,"Gold","Gold"),
  (Barley,"Barley","Gerste"),(Herbs,"Herbs","Kräuter"),(DyePlants,"Dye Plants","Färberpflanzen"),(Silver,"Silver","Silber"),(Copper,"Copper","Kupfer"),(Tin,"Tin","Zinn"),
  (SmallBirds,"Small Birds","Singvögel"),(SeaShells,"Sea Shells","Kammmuscheln"),(Saltwort,"Saltwort","Salzkraut"),(Beaver,"Beavers","Biber"),(Ponies,"Ponies","Ponys"),
  (Granite,"Granite","Granit"),
  (Iron,"Iron","Eisen"),
 ];
 static readonly Dictionary<uint,int> NameOrder=Names.Select((name,index)=>(name.Guid,index)).ToDictionary(x=>x.Guid,x=>x.index);
 // German after "mit" (dative plural) where it differs.
 static readonly Dictionary<uint,string> Dative=new(){[Herbs]="Kräutern",[SmallBirds]="Singvögeln",[Beaver]="Bibern",
 };
 internal static string FertilityName(uint guid,bool german)=>Names.FirstOrDefault(name=>name.Guid==guid) is var name&&name.En is not null?(german?name.De:name.En):guid.ToString();

 // Ideal islands of the sheet: Minerva and Neptune starting islands, the Ceres secondary island and the tertiary island (Latium);
 // the Roman starting island and three Celtic ones (Albion).
 static readonly uint[] Minerva=[Flax,Murex,Sturgeon,Oysters],Neptune=[Mackerel,Murex,Oysters,Sturgeon],Ceres=[Lavender,Olives,Grapes,Marble],Tertiary=[Flax,Gold,Sandarac,Minerals];
 static readonly uint[] RomanStarter=[Herbs,Resin,Silver,Flax,SmallBirds];
 static readonly uint[][] CelticStarters=[[Barley,DyePlants,Saltwort,Ponies],[Barley,Copper,Granite,Tin,Saltwort,Ponies],[Barley,Beaver,Ponies,Copper,Tin,Granite]];
 static readonly uint[] CinisWanted=[Sturgeon,Oysters,Lavender,Gold];
 // Patron-god islands named in a description: Minerva only on an XL starting island; Neptune and Ceres on any island.
 static bool PerfectMinerva(GuideIsland island)=>island.SizeClass=="XL"&&island.Starter&&Has(island.Fertilities,Murex,Flax,Sturgeon,Oysters);
 static bool PerfectNeptune(GuideIsland island)=>Has(island.Fertilities,Mackerel,Sturgeon,Oysters,Murex);
 static bool PerfectCeres(IReadOnlyCollection<uint> f)=>Has(f,Lavender,Grapes,Olives);
 internal static double Fit(IReadOnlyCollection<uint> fertilities,uint[] wanted)=>wanted.Count(fertilities.Contains)/(double)wanted.Length;
 static string SizeOf(GeneratedIsland island)=>MapSchematic.SizeClass(island.Name);
 internal static double CelticFit(IReadOnlyCollection<uint> fertilities)=>CelticStarters.Max(set=>Fit(fertilities,set));
 static bool CinisMaxSlots(SeedAnalysis a)=>a.Latium.CinisSites is{Mountain:19,River:23};
 internal static double CinisFit(SeedAnalysis a)
 {
  if(!a.Profile.Dlc01)return 0;
  return Fit(a.Latium.CinisFertilities,Neptune)>=1?1:.6*Fit(a.Latium.CinisFertilities,CinisWanted)+(CinisMaxSlots(a)?.4:0);
 }

 // ---- Ratings -----------------------------------------------------------------------------------------------------------------
 static double Norm(double value,double min,double max)=>max<=min?1:Math.Clamp((value-min)/(max-min),0,1);
 // Ranges of the map's own fertility and resource slot settings where they differ from Abundant (PlaystyleRanges).
 static ScoreRange Range(SeedAnalysis a,ScoreMetric metric)=>PlaystyleRanges.For(a.Profile,a.Fertility,a.Slots,metric)??
  ScoreMetrics.PopulationRange(metric,a.Profile)!.Value;
 static ScorePart Metric(SeedAnalysis a,string key,ScoreMetric metric,double value,double weight){var range=Range(a,metric);return new(key,value,range.Normalize(value),weight,false,range.Median);}
 public static PlaystyleScore Score(SeedAnalysis a,Playstyle style)
 {
  var parts=new List<ScorePart>();var failed=new List<string>();
  var latium=a.Latium.Metrics;var albion=a.AlbionMetrics;
  ScorePart Combined(string key,ScoreMetric metric,int regionValue,double weight)=>Metric(a,key,metric,regionValue,weight);
  switch(style)
  {
   case Playstyle.Casual:
    CasualParts(a,parts);break;
   case Playstyle.FullSupply:
    parts.Add(Metric(a,"LatiumArea",ScoreMetric.LatiumArea,latium.BuildableTiles,10));
    parts.Add(Metric(a,"LatiumRiver",ScoreMetric.LatiumRiverSites,latium.Sites.River,10));
    parts.Add(Metric(a,"HarbourMurex",ScoreMetric.AdvLatiumHarbourMurex,latium.HarbourMurexTiles,6));
    parts.Add(Metric(a,"HarbourOysters",ScoreMetric.AdvLatiumHarbourOysters,latium.HarbourOysterTiles,4));
    parts.Add(Metric(a,"Marble",ScoreMetric.MarbleSites,latium.MarbleSites,6));
    parts.Add(Metric(a,"AlbionArea",ScoreMetric.AlbionArea,albion.BuildableTiles,10));
    parts.Add(Metric(a,"HarbourSaltwort",ScoreMetric.AdvAlbionHarbourSaltwort,albion.HarbourSaltwortTiles,4));
    parts.Add(Metric(a,"MarshSmallBirds",ScoreMetric.AdvAlbionMarshSmallBirds,albion.MarshSmallBirdTiles,8));
    parts.Add(Metric(a,"MarshBeaver",ScoreMetric.AdvAlbionMarshBeaver,albion.MarshBeaverTiles,3));
    parts.Add(Metric(a,"HarbourSeaShells",ScoreMetric.AdvAlbionHarbourSeaShells,albion.HarbourSeaShellTiles,6));
    parts.Add(Metric(a,"Silver",ScoreMetric.SilverMines,albion.SilverMineSites,8));
    parts.Add(Combined("Minerals",ScoreMetric.MineralMines,latium.MineralMineSites,6));
    parts.Add(Combined("GoldMines",ScoreMetric.GoldMines,latium.GoldMineSites,6));
    parts.Add(Combined("Copper",ScoreMetric.CopperMines,albion.CopperMineSites,6));
    parts.Add(Combined("Tin",ScoreMetric.TinMines,albion.TinMineSites,6));
    break;
   case Playstyle.RecordBuilder:
    parts.Add(Metric(a,"LatiumArea",ScoreMetric.LatiumArea,latium.BuildableTiles,10));
    parts.Add(Metric(a,"LatiumRiver",ScoreMetric.LatiumRiverSites,latium.Sites.River,10));
    parts.Add(Metric(a,"HarbourMurex",ScoreMetric.AdvLatiumHarbourMurex,latium.HarbourMurexTiles,4));
    parts.Add(Metric(a,"HarbourOysters",ScoreMetric.AdvLatiumHarbourOysters,latium.HarbourOysterTiles,2));
    parts.Add(Metric(a,"Marble",ScoreMetric.MarbleSites,latium.MarbleSites,6));
    if(a.Profile.Dlc01)
    {
     parts.Add(new("CinisMurex",a.Latium.CinisFertilities.Contains(Murex)?1:0,a.Latium.CinisFertilities.Contains(Murex)?1:0,2,true));
     parts.Add(new("CinisOysters",a.Latium.CinisFertilities.Contains(Oysters)?1:0,a.Latium.CinisFertilities.Contains(Oysters)?1:0,2,true));
    }
    parts.Add(Metric(a,"AlbionArea",ScoreMetric.AlbionArea,albion.BuildableTiles,10));
    parts.Add(Metric(a,"HarbourSaltwort",ScoreMetric.AdvAlbionHarbourSaltwort,albion.HarbourSaltwortTiles,2));
    // Resin spread over several islands (large and medium ones).
    var resinIslands=a.Albion.Count(island=>island.Fertilities.Contains(Resin)&&SizeOf(island) is "L" or "M");
    parts.Add(new("ResinSpread",resinIslands,Norm(resinIslands,1,4),6));
    parts.Add(Combined("Tin",ScoreMetric.TinMines,albion.TinMineSites,6));
    // Building space near the top of the map's range in both regions: at least RecordAreaMinimum of the way from the fewest to the
    // most tiles (the rating of the building space).
    foreach(var(key,metric,value) in new[]{("LatiumArea",ScoreMetric.LatiumArea,latium.BuildableTiles),("AlbionArea",ScoreMetric.AlbionArea,albion.BuildableTiles)})
     if(Range(a,metric).Normalize(value) is var rating&&rating<RecordAreaMinimum)failed.Add($"{key} {rating*100:F0} % < {RecordAreaMinimum*100:F0} %");
    break;
  }
  var total=parts.Sum(part=>part.Weight);
  return new(style,total<=0?0:Math.Round(100*parts.Sum(part=>part.Weight*part.Normalized)/total,1),parts,failed);
 }
 internal static double RecordAreaMinimum=.95;

 // Casual players: the ideal islands of the sheet (fast) and, with the guides, how short and compact the settling order is.
 static void CasualParts(SeedAnalysis a,List<ScorePart> parts)
 {
  var latium=a.Latium.Islands.Where(island=>!a.WithoutCinis||island.SlotIndex>=0).ToList();
  // Ideal starting island (Minerva on XL or Cinis; Neptune on XL, Cinis or L), then the best Ceres and tertiary island of the rest.
  GeneratedIsland? starter=null;var starterFit=0d;
  foreach(var island in latium)
  {
   var size=SizeOf(island);var fit=Math.Max(size is "XL" or "C"?Fit(island.Fertilities,Minerva):0,size is "XL" or "C" or "L"?Fit(island.Fertilities,Neptune):0);
   if(fit>starterFit){starterFit=fit;starter=island;}
  }
  parts.Add(new("LatiumStarter",starterFit,starterFit,10,true));
  var ceres=latium.Where(island=>island!=starter&&SizeOf(island) is "L" or "XL").Select(island=>(island,Fit(island.Fertilities,Ceres))).DefaultIfEmpty().MaxBy(x=>x.Item2);
  parts.Add(new("LatiumCeres",ceres.Item2,ceres.Item2,8,true));
  var tertiary=latium.Where(island=>island!=starter&&island!=ceres.island&&SizeOf(island) is "L" or "XL").Select(island=>Fit(island.Fertilities,Tertiary)).DefaultIfEmpty().Max();
  parts.Add(new("LatiumTertiary",tertiary,tertiary,6,true));
  var gold=Range(a,ScoreMetric.GoldRiverSites);var sturgeon=Range(a,ScoreMetric.SturgeonRiverSites);var rivers=a.Latium.Metrics.GoldSites+a.Latium.Metrics.SturgeonSites;
  parts.Add(new("GoldSturgeonRivers",rivers,Norm(rivers,gold.Min+sturgeon.Min,gold.Max+sturgeon.Max),5,false,gold.Median+sturgeon.Median));
  if(a.Profile.Dlc01&&!a.WithoutCinis){var cinis=CinisFit(a);parts.Add(new("Cinis",cinis,cinis,5,true));}
  // Albion: the ideal Roman starting island (and sea shells on another large or medium island) and the best Celtic one.
  var roman=a.Albion.Where(island=>SizeOf(island) is "L" or "XL").Select(island=>.8*Fit(island.Fertilities,RomanStarter)+(a.Albion.Any(other=>other!=island&&SizeOf(other) is "L" or "M"&&other.Fertilities.Contains(SeaShells))?.2:0)).DefaultIfEmpty().Max();
  parts.Add(new("RomanStarter",roman,roman,8,true));
  var celtic=a.Albion.Where(island=>SizeOf(island) is "L" or "XL").Select(island=>CelticFit(island.Fertilities)).DefaultIfEmpty().Max();
  parts.Add(new("CelticStarter",celtic,celtic,8,true));
  if(!a.Full)return;
  // The settling guide: main island (Cinis, XL01 or XL04 best), number of islands, their sizes, open water to the main island
  // and the distance from the main island to the nearest raider.
  if(a.LatiumPlan is {} plan)
  {
   var main=plan.Steps[0].Island;var asset=a.Latium.Islands.FirstOrDefault(island=>island.SlotIndex==main.Slot)?.Name??"";
   var mainValue=main.SizeClass=="C"||asset.EndsWith("extralarge_01")||asset.EndsWith("extralarge_04")?1:main.SizeClass=="XL"?.6:main.SizeClass=="L"?.3:0;
   parts.Add(new("LatiumMain",mainValue,mainValue,4,true));
   AddPlanParts(parts,"Latium",[plan],8,6,6,500,null);
   var raider=a.LatiumMap!.Raiders.Count==0?1000:a.LatiumMap.Raiders.Min(box=>SettlingGuide.Gap(main.Box,box));
   parts.Add(new("LatiumRaiders",raider,Norm(raider,0,600),3));
  }
  else parts.Add(new("LatiumIslands",0,0,8));
  if(a.AlbionGuide() is {} albion)AddPlanParts(parts,"Albion",[albion.First,albion.Second],7,5,5,500,null);
  else parts.Add(new("AlbionIslands",0,0,7));
 }
 // Islands of the plans (Latium: 3 or fewer is ideal, two populations: 5 or fewer), their sizes and the open water to the main island.
 static void AddPlanParts(List<ScorePart> parts,string region,GuidePlan[] plans,double countWeight,double sizeWeight,double waterWeight,double farWater,Func<int,int,double>? route)
 {
  var islands=plans.SelectMany(plan=>plan.Steps).Select(step=>step.Island.Slot).Distinct().Count();
  var ideal=plans.Length==1?3:5;var count=islands<=ideal?1:Math.Max(0,1-.3*(islands-ideal));
  parts.Add(new(region+"Islands",islands,count,countWeight));
  var sizes=plans.SelectMany(plan=>plan.Steps).Select(step=>step.Island.SizeClass switch{"C" or "XL" or "L"=>1,"M"=>.55,_=>.15}).Average();
  parts.Add(new(region+"Sizes",sizes,sizes,sizeWeight,true));
  var gaps=plans.SelectMany(plan=>plan.Steps.Skip(1).Select(step=>route?.Invoke(plan.Steps[0].Island.Slot,step.Island.Slot) is double length&&!double.IsPositiveInfinity(length)?length:SettlingGuide.Gap(plan.Steps[0].Island.Box,step.Island.Box))).DefaultIfEmpty(0).Average();
  parts.Add(new(region+"Water",gaps,1-Norm(gaps,0,farWater),waterWeight));
 }

 // ---- Labels of the rated properties ------------------------------------------------------------------------------------------
 static readonly Dictionary<string,(string En,string De)> PartNames=new()
 {
  ["LatiumStarter"]=("Ideal Minerva/Neptune starting island","Ideale Minerva-/Neptun-Startinsel"),["LatiumCeres"]=("Ideal Ceres island","Ideale Ceres-Insel"),
  ["LatiumTertiary"]=("Ideal third island (Flax, Gold, Sandarac, Minerals)","Ideale dritte Insel (Flachs, Gold, Sandarak, Mineralien)"),
  ["GoldSturgeonRivers"]=("Gold and Sturgeon river slots","Gold- und Stör-Flussbauplätze"),["Cinis"]=("Cinis (slots, Sturgeon, Oysters, Lavender, Gold)","Cinis (Bauplätze, Stör, Austern, Lavendel, Gold)"),
  ["RomanStarter"]=("Ideal Roman starting island","Ideale römische Startinsel"),["CelticStarter"]=("Ideal Celtic starting island","Ideale keltische Startinsel"),
  ["LatiumMain"]=("Main island Cinis, XL01 or XL04","Hauptinsel Cinis, XL01 oder XL04"),["LatiumIslands"]=("Latium: islands in the settling order","Latium: Inseln im Siedlungsplan"),
  ["LatiumSizes"]=("Latium: island sizes in the order","Latium: Inselgrößen im Plan"),["LatiumWater"]=("Latium: open water to the main island","Latium: offenes Wasser zur Hauptinsel"),
  ["LatiumRaiders"]=("Latium: distance from the main island to raiders","Latium: Abstand der Hauptinsel zu Piraten"),
  ["AlbionIslands"]=("Albion: islands in the settling order","Albion: Inseln im Siedlungsplan"),["AlbionSizes"]=("Albion: island sizes in the order","Albion: Inselgrößen im Plan"),
  ["AlbionWater"]=("Albion: open water to the main islands","Albion: offenes Wasser zu den Hauptinseln"),
  ["HarbourMurex"]=("Harbour tiles of Murex islands","Hafenkacheln der Murex-Inseln"),["HarbourOysters"]=("Harbour tiles of Oyster islands","Hafenkacheln der Austern-Inseln"),
  ["HarbourSaltwort"]=("Harbour tiles of Saltwort islands","Hafenkacheln der Salzkraut-Inseln"),["MarshSmallBirds"]=("Marsh of Small Bird islands","Sumpf der Singvogel-Inseln"),
  ["MarshBeaver"]=("Marsh of Beaver islands","Sumpf der Biber-Inseln"),["HarbourSeaShells"]=("Harbour tiles of Sea Shell islands","Hafenkacheln der Kammmuschel-Inseln"),
  ["LatiumArea"]=("Latium building space (tiles)","Latium Baufläche (Kacheln)"),["AlbionArea"]=("Albion building space (tiles)","Albion Baufläche (Kacheln)"),
  ["LatiumRiver"]=("Latium river slots","Latium Flussbauplätze"),["Marble"]=("Marble slots","Marmor-Bauplätze"),["Silver"]=("Silver mines","Silberminen"),
  ["CinisMurex"]=("Murex on Cinis","Murex auf der Cinis"),["CinisOysters"]=("Oysters on Cinis","Austern auf der Cinis"),
  ["ResinSpread"]=("Albion islands with Resin","Albion-Inseln mit Harz"),
  ["Minerals"]=("Mineral mines","Mineralienminen"),["GoldMines"]=("Gold mines","Goldminen"),["Copper"]=("Copper mines","Kupferminen"),["Tin"]=("Tin mines","Zinnminen"),
 };
 internal static string PartName(string key,bool german)=>PartNames.TryGetValue(key,out var name)?(german?name.De:name.En):key;
 internal static string PartValue(ScorePart part,bool german)=>part.Fraction?$"{part.Value*100:F0} %":part.Value.ToString(part.Value%1==0?"N0":"N1",Culture(german));
 internal static string StyleName(Playstyle style,bool german)=>style switch
 {
  Playstyle.Casual=>german?"Casual-Spieler":"Casual players",Playstyle.FullSupply=>german?"Vollversorger":"Full supply",_=>german?"Rekordbauer":"Record builders"
 };
 static string StyleSummary(Playstyle style,bool german)=>style switch
 {
  Playstyle.Casual=>german?"gute Fruchtbarkeitsverteilung und ein einfacher Siedlungsplan auf großen, nah beieinander liegenden Inseln":"good fertility distribution and an easy settling order on large islands close to each other",
  Playstyle.FullSupply=>german?"viel Baufläche, viele Flussbauplätze, gute Küsten für Murex und Austern und genug Minen, um jede Residenz voll zu versorgen":"lots of building space and river slots, good coasts for Murex and Oysters and enough mines to fully supply every residence",
  _=>german?"maximale Baufläche und Flussbauplätze, dazu viel Marmor und Zinn":"maximum building space and river slots, plus plenty of Marble and Tin"
 };

 // ---- Description -------------------------------------------------------------------------------------------------------------
 static string T(bool de,string en,string german)=>de?german:en;
 static string Join(bool de,IReadOnlyList<string> items)=>items.Count switch{0=>"",1=>items[0],_=>string.Join(", ",items.Take(items.Count-1))+T(de," and "," und ")+items[^1]};
 static string List(bool de,IEnumerable<uint> fertilities,bool dative=false)=>Join(de,[..fertilities.Distinct().OrderBy(f=>NameOrder.GetValueOrDefault(f,999)).Select(f=>de&&dative&&Dative.TryGetValue(f,out var name)?name:FertilityName(f,de))]);
 // After "with" / "mit".
 static string With(bool de,IEnumerable<uint> fertilities)=>$"{T(de,"with","mit")} {List(de,fertilities,true)}";
 static string Adjective(double fit,bool de)=>fit>=1?T(de,"Perfect","Perfekte"):fit>=.75?T(de,"Very good","Sehr gute"):fit>=.5?T(de,"Good","Gute"):T(de,"Mediocre","Mittelmäßige");
 static string Ordinal(int index,bool de)=>index switch{1=>T(de,"Secondary","Zweite"),2=>T(de,"Third","Dritte"),3=>T(de,"Fourth","Vierte"),4=>T(de,"Fifth","Fünfte"),_=>T(de,"Additional","Weitere")};
 static string OrdinalLower(int index,bool de)=>index switch{1=>T(de,"secondary","zweite"),2=>T(de,"third","dritte"),3=>T(de,"fourth","vierte"),4=>T(de,"fifth","fünfte"),_=>T(de,"additional","weitere")};
 static bool Has(IReadOnlyCollection<uint> fertilities,params uint[] all)=>all.All(fertilities.Contains);
 // Production chains an island covers by itself.
 static List<string> Chains(IReadOnlyCollection<uint> f,bool de,bool lateGame)
 {
  var chains=new List<string>();
  if(!lateGame){if(Has(f,Flax,Murex))chains.Add(T(de,"Toga Chain","Toga-Kette"));if(Has(f,Oysters,Sturgeon))chains.Add(T(de,"Oysters with Caviar Chain","Austern mit Kaviar-Kette"));}
  else{if(Has(f,Minerals,Gold))chains.Add(T(de,"Necklaces","Halsketten"));if(Has(f,Sandarac,Gold))chains.Add(T(de,"Lyres","Leiern"));if(Has(f,Sandarac,Flax))chains.Add(T(de,"Loungers","Liegen"));}
  return chains;
 }
 static string RiverNote(GuideIsland island,bool de)=>island.River<=0?"":island.Fertilities.Contains(Gold)?T(de,", use River Slots mainly for Gold",", Flussbauplätze hauptsächlich für Gold nutzen"):island.Fertilities.Contains(Sturgeon)?T(de,", use River Slots for Sturgeon",", Flussbauplätze für Stör nutzen"):"";

 internal static List<string> LatiumBullets(SeedAnalysis a,bool de)
 {
  var bullets=new List<string>();var plan=a.ShownLatium;
  if(plan is null)return[T(de,"No settling order reaches every fertility.","Keine Siedlungsreihenfolge erreicht alle Fruchtbarkeiten.")];
  for(var index=0;index<plan.Steps.Count;index++)
  {
   var step=plan.Steps[index];var island=step.Island;var f=island.Fertilities;string text;
   var others=f.Except(step.NewFertilities).Where(x=>x is Gold or Sturgeon or Marble or Minerals or Oysters or Murex).ToList();
   var also=others.Count>0&&index>0?T(de,$" (plus {List(de,others)})",$" (dazu {List(de,others)})"):"";
   if(index==0&&island.SizeClass=="C")
   {
    // The Cinis has a name of its own: an adjective only when it is good.
    var cinisFit=CinisFit(a);text=$"{(cinisFit>=.5?Adjective(cinisFit,de)+" ":"")}{T(de,"Cinis as main island","Cinis als Hauptinsel")}{(PerfectNeptune(island)?T(de,", a perfect Neptune island,",", eine perfekte Neptun-Insel,"):"")} {With(de,f)}";
    if(Chains(f,de,false) is {Count:>0} chains)text+=$" ({Join(de,chains)})";
    if(CinisMaxSlots(a))text+=T(de,", maximum slots (19 mountain, 23 river)",", maximale Bauplätze (19 Berg, 23 Fluss)");
   }
   else if(index==0)
   {
    // A patron god only for a perfect fit; otherwise the island's fertilities speak for themselves.
    var god=PerfectMinerva(island)?"Minerva":PerfectNeptune(island)?T(de,"Neptune","Neptun"):null;
    text=god is null?$"{T(de,"Starter Island","Starterinsel")} ({island.Label}) {With(de,f)}":$"{T(de,"Perfect","Perfekte")} {god}{T(de," Starter Island","-Starterinsel")} ({island.Label}) {With(de,f)}";
    if(Chains(f,de,false) is {Count:>0} chains)text+=$" ({Join(de,chains)})";
    var asset=a.Latium.Islands.FirstOrDefault(x=>x.SlotIndex==island.Slot)?.Name??"";
    if(asset.EndsWith("extralarge_04"))text+=T(de,", also the largest XL island",", außerdem die größte XL-Insel");
   }
   else if(PerfectCeres(f))
    text=index==1?$"{T(de,"Perfect Ceres secondary island","Perfekte Ceres-Nebeninsel")} ({island.Label}) {With(de,f.Where(x=>x!=Iron))}"
     :$"{T(de,"Perfect Ceres island","Perfekte Ceres-Insel")} ({island.Label}) {T(de,$"as {OrdinalLower(index,de)} island",$"als {OrdinalLower(index,de)} Insel")} {With(de,f.Where(x=>x!=Iron))}";
   else
   {
    text=$"{Ordinal(index,de)} {T(de,"island","Insel")} ({island.Label}) {With(de,step.NewFertilities.Length>0?step.NewFertilities:f)}{(step.NewFertilities.Length>0?also:"")}";
    if(Chains(f,de,true) is {Count:>0} late)text+=T(de,$" – good combination for {Join(de,late)} in the late game",$" – gute Kombination für {Join(de,late)} im Late Game");
    else if(Has(f,Sandarac,Minerals))text+=T(de," – both Sandarac and Minerals for Tier 4"," – Sandarak und Mineralien für Tier 4");
   }
   bullets.Add(text+RiverNote(island,de));
  }
  var main=plan.Steps[0].Island;
  if(a.LatiumMap is {Raiders.Count:>0} map&&map.Raiders.Min(box=>SettlingGuide.Gap(main.Box,box))<250)
   bullets.Add(T(de,"Raider base close to the main island – build your defences early","Piratenbasis nahe der Hauptinsel – baut früh eure Verteidigung"));
  return bullets;
 }

 // Two populations: each population's plan (first the one settled first); an empty string separates the two.
 // Names: (English, German after "den", German at the start of a sentence).
 static List<string> PopulationBullets(PopulationGuide? guide,bool de,uint[] populationA,uint[] populationB,(string En,string De,string DeStart) nameA,(string En,string De,string DeStart) nameB,Func<IReadOnlyCollection<uint>,bool,double> fitA,Func<IReadOnlyCollection<uint>,bool,double> fitB,Func<IReadOnlyCollection<uint>,bool,bool,string> mainNote)
 {
  if(guide is null)return[T(de,"No settling order reaches every fertility.","Keine Siedlungsreihenfolge erreicht alle Fruchtbarkeiten.")];
  var bullets=new List<string>();var firstSlots=guide.First.Steps.Select(step=>step.Island.Slot).ToHashSet();
  foreach(var(plan,isA,second) in new[]{(guide.First,guide.PopulationAFirst,false),(guide.Second,!guide.PopulationAFirst,true)})
  {
   if(plan.Steps.Count==0)continue;
   if(second&&bullets.Count>0)bullets.Add("");
   var name=isA?nameA:nameB;var own=isA?populationA:populationB;
   for(var index=0;index<plan.Steps.Count;index++)
   {
    var step=plan.Steps[index];var island=step.Island;var f=island.Fertilities;var reused=second&&firstSlots.Contains(island.Slot);
    if(index==0&&reused)
     bullets.Add($"{(de?name.DeStart:name.En)} {T(de,"path from","Pfad ab")} {island.Label} ({T(de,"already settled","bereits besiedelt")}) {With(de,f.Where(own.Contains))}{mainNote(f,isA,de)}");
    else if(index==0)
     bullets.Add($"{Adjective((isA?fitA:fitB)(f,de),de)} {T(de,"Starter Island for the","Startinsel für den")} {(de?name.De:name.En)} {T(de,"path","Pfad")} ({island.Label}) {With(de,f.Where(own.Contains))}{mainNote(f,isA,de)}");
    else
    {
     var bronze=Has(f,Tin,Copper)&&(step.NewFertilities.Contains(Tin)||step.NewFertilities.Contains(Copper))?T(de," – both Tin and Copper for Bronze production"," – Zinn und Kupfer für die Bronzeherstellung"):"";
     bullets.Add($"{island.Label}{(reused?T(de," (already settled)"," (bereits besiedelt)"):"")} {T(de,"as","als")} {OrdinalLower(index,de)} {(step.NewFertilities.Length>0?$"{T(de,"island for","Insel für")} {List(de,step.NewFertilities)}":$"{T(de,"island","Insel")} {With(de,f.Where(own.Contains))}")}{bronze}");
    }
   }
  }
  return bullets;
 }
 internal static List<string> AlbionBullets(SeedAnalysis a,bool de,bool? romanFirst=null)=>PopulationBullets(a.ShownAlbion(romanFirst),de,SettlingGuide.Roman,SettlingGuide.Celtic,("Roman","römischen","Römischer"),("Celtic","keltischen","Keltischer"),
  (f,_)=>Fit(f,RomanStarter),(f,_)=>CelticFit(f),(f,roman,de)=>
  {
   if(!roman)return Has(f,Copper,Tin)?T(de," – ideal for Bronze production"," – ideal für die Bronzeherstellung"):"";
   var extras=new List<string>();
   if(Has(f,Herbs,Silver,Resin))extras.Add(T(de,"all Tier 2 productions available","alle Tier-2-Produktionen verfügbar"));
   if(Has(f,Resin,Flax))extras.Add(T(de,"Wig chain","Perückenkette"));
   if(f.Contains(SmallBirds))extras.Add(T(de,"Bird Tongues in Aspic","Vogelzungen in Aspik"));
   var note=extras.Count>0?$" ({string.Join(" + ",extras)})":"";
   if(Has(f,Herbs,Silver,Resin,SmallBirds,Flax,SeaShells))note+=T(de," – T3 is possible with only this island!"," – T3 ist allein mit dieser Insel möglich!");
   return note;
  });

 // Strong points of the seed for a playstyle: its best-rated properties (rating 0.7 or more) as short phrases.
 internal static List<string> Highlights(PlaystyleScore score,bool de,IReadOnlySet<string>? ownChoice=null,int count=5)=>
  [..score.Parts.Where(part=>part.Normalized>=.7&&!(ownChoice is not null&&GuidePart(part.Key) is {} region&&ownChoice.Contains(region))&&(!IdealNames.ContainsKey(part.Key)||part.Value>=1)&&part.Key is not("LatiumStarter" or "LatiumCeres")).OrderByDescending(part=>part.Weight*part.Normalized).Take(count)
   .OrderBy(part=>Order(score,part)).Select(part=>Highlight(part,de))];
 // The region of a property that comes from the settling guide (null for the others).
 static string? GuidePart(string key)=>key is "LatiumMain" or "LatiumRaiders"?"Latium":key.EndsWith("Islands")||key.EndsWith("Sizes")||key.EndsWith("Water")?RegionOf(key):null;
 static string RegionOf(string key)=>key.StartsWith("Latium")?"Latium":"Albion";
 // Highlights keep the order of the rated properties, so Latium comes before Albion.
 static int Order(PlaystyleScore score,ScorePart part)=>score.Parts.ToList().IndexOf(part);
 // Islands of the ideal kinds (their fit as adjective), the settling order, and the slot and tile counts with the median for comparison.
 static readonly Dictionary<string,(string En,string De)> IdealNames=new()
 {
  ["LatiumStarter"]=("Minerva or Neptune starting island","Minerva- oder Neptun-Startinsel"),["LatiumCeres"]=("Ceres island","Ceres-Insel"),
  ["LatiumTertiary"]=("third island for the late game (Flax, Gold, Sandarac, Minerals)","dritte Insel für das Late Game (Flachs, Gold, Sandarak, Mineralien)"),
  ["RomanStarter"]=("starting island for the Roman path","Startinsel für den römischen Pfad"),["CelticStarter"]=("starting island for the Celtic path","Startinsel für den keltischen Pfad"),
  ["Cinis"]=("Cinis","Cinis"),
 };
 static string Highlight(ScorePart part,bool de)
 {
  if(IdealNames.TryGetValue(part.Key,out var ideal))return $"{Adjective(part.Value,de)} {(de?ideal.De:ideal.En)}";
  if(part.Key is "LatiumArea" or "AlbionArea")return $"{PartName(part.Key,de)}: {PartValue(part,de)} ({part.Normalized*100:F0} %)";
  var region=part.Key.StartsWith("Latium")?"Latium":"Albion";
  if(part.Key.EndsWith("Islands"))return T(de,$"{region}: every fertility on only {part.Value:F0} islands",$"{region}: alle Fruchtbarkeiten auf nur {part.Value:F0} Inseln");
  if(part.Key.EndsWith("Sizes"))return T(de,$"{region}: large islands in the settling order",$"{region}: große Inseln im Siedlungsplan");
  if(part.Key.EndsWith("Water"))return T(de,$"{region}: short routes to the main island",$"{region}: kurze Wege zur Hauptinsel");
  return part.Key switch
  {
   "LatiumMain"=>T(de,"Main island: Cinis or one of the best XL islands (XL01, XL04)","Hauptinsel: die Cinis oder eine der besten XL-Inseln (XL01, XL04)"),
   "LatiumRaiders"=>T(de,"Main island far from the raiders","Hauptinsel weit weg von den Piraten"),
   "CinisMurex"=>T(de,"Murex on Cinis","Murex auf der Cinis"),"CinisOysters"=>T(de,"Oysters on Cinis","Austern auf der Cinis"),
   "ResinSpread"=>T(de,$"Resin on {part.Value:F0} Albion islands",$"Harz auf {part.Value:F0} Albion-Inseln"),
   _=>part.Median>0?$"{PartName(part.Key,de)}: {PartValue(part,de)} ({T(de,"median","Median")} {new ScorePart(part.Key,part.Median,0,0).Value.ToString("N0",Culture(de))})":$"{PartName(part.Key,de)}: {PartValue(part,de)}"
  };
 }
 // Latium islands with a perfect patron-god fit, named by their label (the same rules as the description).
 static IEnumerable<string> GodIslands(SeedAnalysis a,bool de)
 {
  foreach(var island in a.LatiumIslands??[])
  {
   if(a.WithoutCinis&&island.SizeClass=="C")continue;
   if(PerfectMinerva(island))yield return T(de,$"Perfect Minerva starting island ({island.Label})",$"Perfekte Minerva-Startinsel ({island.Label})");
   else if(PerfectNeptune(island))yield return T(de,$"Perfect Neptune island ({island.Label})",$"Perfekte Neptun-Insel ({island.Label})");
   if(PerfectCeres(island.Fertilities))yield return T(de,$"Perfect Ceres island ({island.Label})",$"Perfekte Ceres-Insel ({island.Label})");
  }
 }
 // Copper and mineral mines of the seed (record builders), over every region that has them.
 static IEnumerable<string> MineCounts(SeedAnalysis a,bool de)
 {
  var copper=a.AlbionMetrics.CopperMineSites;var minerals=a.Latium.Metrics.MineralMineSites;
  yield return T(de,$"Copper mines (Albion): {copper}",$"Kupferminen (Albion): {copper}");
  yield return T(de,$"Mineral mines (Latium): {minerals}",$"Mineralienminen (Latium): {minerals}");
 }
 static System.Globalization.CultureInfo Culture(bool de)=>de?new("de-DE"):new("en-US");

 // ---- Export ------------------------------------------------------------------------------------------------------------------
 internal static string TemplateName(MapTemplateKind template)=>template==MapTemplateKind.IslandChains?"Island Chain":template.ToString();
 // The game calls the medium map size "Regular".
 internal static string SizeName(MapSizeKind size,bool de)=>size switch{MapSizeKind.Small=>T(de,"Small","Klein"),MapSizeKind.Medium=>T(de,"Regular","Regulär"),_=>T(de,"Large","Groß")};
 static string SettingName(FertilitySetting setting,bool de)=>setting switch{FertilitySetting.Abundant=>T(de,"Abundant","Reichlich"),FertilitySetting.Regular=>T(de,"Regular","Normal"),_=>T(de,"Sparse","Spärlich")};
 internal static string FileStem(SeedAnalysis a)=>$"{TemplateName(a.Profile.Template).Replace(" ","").ToLowerInvariant()}_{a.Seed}_{SizeName(a.Profile.Size,false).ToLowerInvariant()}";
 static string Html(string text)=>text.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");

 // The description cell: optional playstyle block, one underlined section per region with its bullets, and the author line.
 internal static string DescriptionHtml(SeedAnalysis a,bool de,Playstyle? style,string author,bool? romanFirst=null)
 {
  var html=new StringBuilder();
  html.Append($"<p style=\"text-align: center;\"><strong>{T(de,"Description","Beschreibung")}</strong></p>\n");
  if(style is Playstyle playstyle)
  {
   var score=Score(a,playstyle);
   html.Append($"<p><strong>{T(de,"Recommended for","Empfohlen für")}: {StyleName(playstyle,de)}</strong> – {Html(StyleSummary(playstyle,de))}</p>\n");
   // The guide's claims (islands, sizes, water, main island) don't describe an own choice of islands, so they go for such a region.
   var own=new HashSet<string>();if(a.LatiumChoice is not null)own.Add("Latium");if(a.AlbionChoice is not null)own.Add("Albion");
   var highlights=Highlights(score,de,own);
   if(playstyle==Playstyle.Casual)highlights.InsertRange(0,GodIslands(a,de));
   if(playstyle==Playstyle.RecordBuilder)highlights.AddRange(MineCounts(a,de));
   if(highlights.Count>0){html.Append("<ul>\n");foreach(var line in highlights)html.Append($" \t<li>{Html(line)}</li>\n");html.Append("</ul>\n");}
  }
  void Section(string title,List<string> bullets)
  {
   html.Append($"<span style=\"text-decoration: underline;\"><strong>{title}</strong></span>\n<ul>\n");
   foreach(var bullet in bullets)html.Append(bullet.Length==0?"<br>\n":$" \t<li>{Html(bullet)}</li>\n");
   html.Append("</ul>\n");
  }
  Section("Latium",LatiumBullets(a,de));Section("Albion",AlbionBullets(a,de,romanFirst));
  html.Append("<p style=\"text-align: center;\"></p>\n");
  if(author.Trim().Length>0)html.Append($"<p style=\"text-align: center;\">{T(de,"Recommended by","Empfohlen von")}: <strong>{Html(author.Trim())}</strong></p>");
  return html.ToString();
 }

 // The Elementor template (the Seed Atlas v1.3 layout): the map images on the left, the settings table with the description on the right.
 internal static string ElementorJson(SeedAnalysis a,bool de,Playstyle? style,string author,string version,IReadOnlyList<string> imageUrls,bool? romanFirst=null)
 {
  var random=new Random(unchecked((int)a.Seed*31+(de?1:0)));string Id()=>random.Next(0x1000000,int.MaxValue).ToString("x8")[..8];
  JsonObject Heading(string id,string text,bool label,bool first=false)
  {
   var heading=new JsonObject{["_id"]=id,["heading_text"]=text};
   if(!label)heading["cell_align_head_indi"]="center";
   heading["__globals__"]=new JsonObject{["single_heading_background_color"]=label?"globals/colors?id=82c83f3":"globals/colors?id=2dd691f"};
   if(first){((JsonObject)heading["__globals__"]!)["single_heading_color"]="globals/colors?id=text";if(label)heading["heading_row_width"]=new JsonObject{["unit"]="px",["size"]=300,["sizes"]=new JsonArray()};}
   return heading;
  }
  JsonObject Row(string id)=>new(){["header_content_type"]="row",["_id"]=id};
  var images=new JsonArray();
  foreach(var url in imageUrls)
   images.Add(new JsonObject
   {
    ["id"]=Id(),["settings"]=new JsonObject{["image"]=new JsonObject{["id"]="",["url"]=url,["alt"]="",["source"]="library",["size"]=""},["image_size"]="full",["link_to"]="file",["open_lightbox"]="yes",
     ["image_box_shadow_box_shadow_type"]="yes",["image_box_shadow_box_shadow"]=new JsonObject{["horizontal"]=2,["vertical"]=2,["blur"]=10,["spread"]=1,["color"]="rgba(0,0,0,0.5)"}},
    ["elements"]=new JsonArray(),["isInner"]=false,["widgetType"]="image",["elType"]="widget"
   });
  var headings=new JsonArray
  {
   Row("7501c01"),Heading("9d5ce34",T(de,"Game mode","Spielmodus"),true,true),Heading("48b50bb",T(de,"Sandbox","Endlos"),false,true),
   Row("b857e84"),Heading("98ef68d","Seed",true),Heading("5199acb",a.Seed.ToString(),false),
   Row("a51f732"),Heading("86f36ae",T(de,"Island Size","Inselgröße"),true),Heading("6dfaba8",SizeName(a.Profile.Size,de),false),
   Row("4f0bc0b"),Heading("893cdcb",T(de,"Fertilities and Lodes","Fruchtbarkeiten und Vorkommen"),true),Heading("ac5a344",SettingName(a.Fertility,de),false),
  };
  ((JsonObject)headings[^2]!)["cell_align_head_indi"]="flex-start";
  var description=DescriptionHtml(a,de,style,author,romanFirst);
  var table=new JsonObject
  {
   ["id"]=Id(),
   ["settings"]=new JsonObject
   {
    ["table_headings"]=headings,
    ["table_content"]=new JsonArray{new JsonObject{["_id"]="a8f9282",["content_type"]="row",["cell_button_text"]="Click here"},new JsonObject{["_id"]="ea8e891",["cell_text"]=description,["cell_button_text"]="Click here",["cell_align"]="left",["cell_span"]=2}},
    ["searchable_label"]="Search",
    ["__globals__"]=new JsonObject{["cell_border_body_color"]="globals/colors?id=secondary",["tos_background_color"]="globals/colors?id=82c83f3",["striped_effect_odd"]="globals/colors?id=82c83f3",["striped_effect_even"]="globals/colors?id=82c83f3",["cell_background"]="globals/colors?id=8410377",["cell_border_head_color"]="globals/colors?id=secondary"},
    ["tos_background_background"]="classic",["_transform_scale_effect"]=new JsonObject{["unit"]="px",["size"]=1.2,["sizes"]=new JsonArray()},
   },
   ["elements"]=new JsonArray(),["isInner"]=false,["widgetType"]="tp-table",["elType"]="widget"
  };
  var root=new JsonObject
  {
   ["content"]=new JsonArray
   {
    new JsonObject
    {
     ["id"]=Id(),["settings"]=new JsonObject{["flex_direction"]="row"},
     ["elements"]=new JsonArray
     {
      new JsonObject
      {
       ["id"]=Id(),["settings"]=new JsonObject{["content_width"]="full",["flex_direction"]="row"},
       ["elements"]=new JsonArray
       {
        new JsonObject{["id"]=Id(),["settings"]=new JsonObject{["content_width"]="full",["margin"]=new JsonObject{["unit"]="px",["top"]="0",["right"]="-30",["bottom"]="0",["left"]="0",["isLinked"]=false},["flex_justify_content"]="flex-start"},["elements"]=images,["isInner"]=true,["elType"]="container"},
        new JsonObject{["id"]=Id(),["settings"]=new JsonObject{["content_width"]="full",["flex_gap"]=new JsonObject{["column"]="0",["row"]="0",["isLinked"]=true,["unit"]="px",["size"]=0},["__globals__"]=new JsonObject{["background_color"]="globals/colors?id=82c83f3"},["flex_justify_content"]="flex-start"},["elements"]=new JsonArray{table},["isInner"]=true,["elType"]="container"},
       },
       ["isInner"]=true,["elType"]="container"
      }
     },
     ["isInner"]=false,["elType"]="container"
    }
   },
   ["page_settings"]=new JsonArray(),["version"]="0.4",
   ["title"]=$"Seed Atlas {TemplateName(a.Profile.Template)} {SizeName(a.Profile.Size,false)} {a.Seed}{(version.Trim().Length>0?" "+version.Trim():"")}{(de?" DE":"")}",
   ["type"]="container"
  };
  return root.ToJsonString(new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
 }
}
