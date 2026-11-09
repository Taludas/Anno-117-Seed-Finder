// Aegyptus, the DLC03 "Dawn of the Delta" session (session GUID 149679). Reverse engineered from 46 DLC03 savegames of game
// version 3.0 (seeds 1-5, 802, 1204, 100987, 171090, 6121960, 24051963; every template, size and fertility setting); see
// DLC3-FINDINGS.md. The session is a fixed map (egypt_province_default_01.a7tinfo): every island sits at its template position
// with rotation 0 whatever the map template or size. Only four things are random, all from Rng(seed) - a stream of its own,
// independent of Latium's and Albion's:
//   1. which islands use their smaller "_layer01" variant (the map size decides how many);
//   2. how many of each island's random mine slots are active (the resource slot setting decides the range);
//   3. which non-starter islands get the 2nd and which the 3rd fertility set;
//   4. the fertilities themselves, drawn from shared bags exactly like Latium and Albion.
// MineSlots: the mine slots the island really has in the game. DrawnMineSlots: what the site phase activated, which is what the
// savegame lists; the two differ only on "_layer01" islands (see LayerSlots).
internal sealed record EgyptIsland(string Name,string Variant,bool Starter,bool Hard,uint FertilitySet,uint[] Fertilities,int MineSlots,int DrawnMineSlots);
// Lavender, grapes and olives grow on any buildable tile once it is irrigated, so they count all buildable tiles of their islands.
// Fertile ground (static tile grid value 10) is the native fertile area where only trees such as dates grow. Copper, tin and
// minerals need a mine slot. Egyptian gold has a coastal production (only on the coast line, not the whole harbour area) and a
// mine on mountain slots; the metrics give the number of gold islands and their mine slots.
internal readonly record struct EgyptMetrics(int BuildableTiles,int RiverbankTiles,int MineSlots,int HippoRiverbankTiles,int PomegranateTiles,int FertileGround,int LavenderTiles,int GrapeTiles,int OliveTiles,int CopperMines,int TinMines,int GoldIslands,int GoldMines,int MineralMines)
{
 public static EgyptMetrics Calculate(IReadOnlyList<EgyptIsland> islands)
 {
  int buildable=0,riverbank=0,mineSlots=0,hippo=0,pomegranate=0,fertile=0,lavender=0,grapes=0,olives=0,copper=0,tin=0,goldIslands=0,goldMines=0,minerals=0;
  foreach(var island in islands)
  {
   var tiles=EgyptGenerator.Tiles(island.Variant);var has=island.Fertilities;
   buildable+=tiles.Buildable;riverbank+=tiles.Riverbank;fertile+=tiles.Farmland;mineSlots+=island.MineSlots;
   if(has.Contains(EgyptGenerator.Hippos))hippo+=tiles.Riverbank;
   if(has.Contains(EgyptGenerator.Pomegranates))pomegranate+=tiles.Buildable;
   if(has.Contains(EgyptGenerator.Lavender))lavender+=tiles.Buildable;
   if(has.Contains(EgyptGenerator.Grapes))grapes+=tiles.Buildable;
   if(has.Contains(EgyptGenerator.Olives))olives+=tiles.Buildable;
   if(has.Contains(EgyptGenerator.Copper))copper+=island.MineSlots;
   if(has.Contains(EgyptGenerator.Tin))tin+=island.MineSlots;
   if(has.Contains(EgyptGenerator.Gold)){goldIslands++;goldMines+=island.MineSlots;}
   if(has.Contains(EgyptGenerator.Minerals))minerals+=island.MineSlots;
  }
  return new(buildable,riverbank,mineSlots,hippo,pomegranate,fertile,lavender,grapes,olives,copper,tin,goldIslands,goldMines,minerals);
 }
}
internal sealed record EgyptGeneration(uint Seed,IReadOnlyList<EgyptIsland> Islands,EgyptMetrics Metrics);
// Tiles of one island variant, from its .a7m (game 3.0): buildable land (AreaID low byte 1, the high bits are the construction
// area index on the parent island), of it the native fertile ground (static tile grid value 10) and the riverbank along the Nile
// (value 1: the band right next to the water, measured on every island),
// and harbour water.
internal readonly record struct EgyptTiles(int Buildable,int Farmland,int Riverbank,int Harbour);
// A fertility set an island can draw: its role and whether it can bring gold; and one group of its slots with what they can hold.
internal sealed record EgyptSetChoice(uint Guid,string Role,bool Gold);
internal sealed record EgyptSlotGroup(int First,int Last,uint[] Choices);

internal static class EgyptGenerator
{
 public const uint SessionGuid=149679;
 public const uint Barley=2212,Flax=2202,Lavender=2209,Grapes=2205,Iron=4049,Granite=4066,Copper=4063,Sesame=149818,Olives=2210,Minerals=4053,
  Lotus=149819,Hippos=149820,Pomegranates=150477,Tin=4064,Gold=150506;
 // The fertilities of the rules list shuffle begin at this draw in every savegame of every seed and setting. What consumes the
 // draws before it (a decoration-grid pass over the 2560-wide map is the likely bulk: 160x160 cells = 25,599 draws) does not
 // depend on the seed, so it is kept as one constant.
 const int FertilityPhaseStart=25669;
 // The mine slots are activated just before, from this draw on, one island after the other in template order (46 of 46 savegames).
 const int SlotPhaseStart=25608;
 // Mine slots of every island, (random, fixed) from its .a7minfo (slot assets 150580 Egyptian Mountain Random and 150581 Egyptian
 // Mountain Fixed; Aegyptus has no river slots). A "_layer01" island keeps the slots of its full island, so the draws do not
 // depend on the map size.
 static readonly Dictionary<string,(int Random,int Fixed)> Slots=new()
 {
  ["large_01"]=(5,5),["large_02"]=(5,5),["large_03"]=(5,6),["large_04"]=(6,6),["large_05"]=(5,6),["large_06"]=(5,6),["large_07"]=(0,12),
  ["large_08"]=(5,5),["large_09"]=(6,5),["large_10"]=(5,5),["medium_01"]=(3,5),["medium_02"]=(5,3),["medium_03"]=(0,8),["medium_04"]=(2,3),
  ["small_01"]=(4,0),["small_02"]=(0,2),["small_03"]=(0,6),["small_04"]=(0,4),
 };
 // A "_layer01" island still rolls the slots of its full island (the draws above, and the savegame's slot list with the full
 // island's slot ids), but the island on the map is the layer file with its own slot objects under other ids. None of them is in
 // that list, and in the game every one of them is there, whatever the setting (seed 27028: large_10_layer01 shows 9 = 5 random
 // + 4 fixed on all three settings, large_07_layer01 11 fixed, against 7/6/5 and 12 in the list). Random + fixed per layer file:
 static readonly Dictionary<string,int> LayerSlots=new()
 {
  ["large_01"]=5,["large_02"]=7,["large_03"]=11,["large_05"]=9,["large_06"]=8,["large_07"]=11,["large_08"]=7,["large_10"]=9,
  ["medium_01"]=4,["medium_02"]=5,["medium_03"]=6,["medium_04"]=4,
 };

 // MapGeneratorSlotCount of 150580 (assets.xml), region Egyptian: the minimum per island size of the Low/Medium/High rows (the game's
 // sparse/regular/abundant resource slot options); every row spans three values.
 static int SlotMinimum(string name,SlotSetting slots)
 {
  var(low,medium,high)=name.StartsWith("large")?(5,6,7):name.StartsWith("medium")?(4,5,6):(3,4,5);
  return slots switch{SlotSetting.Sparse=>low,SlotSetting.Regular=>medium,_=>high};
 }
 const string Prefix="egypt_dlc_03_valley_";

 // The 18 fertile islands in template order: starter (RandomIslandConfig/Type 0100) and difficulty (Hard 0100 -> the "no gold"
 // sets, Normal 0000 -> the "gold" sets), both straight from the template.
 static readonly (string Name,bool Starter,bool Hard)[] Fertile=
 [
  ("medium_02",false,true),("large_08",false,false),("small_02",false,true),("medium_04",false,false),("large_09",false,false),("small_04",false,true),
  ("medium_03",false,true),("large_07",true,true),("small_03",false,false),("large_06",true,false),("large_10",false,false),("large_04",false,true),
  ("large_03",false,false),("large_05",true,true),("large_01",true,false),("large_02",false,true),("medium_01",false,true),("small_01",false,true),
 ];
 // Islands that can switch to their "_layer01" variant, in template order. Right after the usual Advance(9) the game shuffles the
 // mediums, then the larges, and switches the first 0/2/4 mediums and 2/4/6 larges (Large/Medium/Small map).
 static readonly string[] LayerMediums=["medium_02","medium_04","medium_03","medium_01"];
 static readonly string[] LayerLarges=["large_08","large_07","large_06","large_10","large_03","large_05","large_01","large_02"];

 static readonly Dictionary<uint,uint[]> Pools=new()
 {
  [149800]=[Barley,Flax],[150502]=[Lavender,Grapes],[150503]=[Iron,Granite],[150504]=[Copper,Sesame,Olives,Minerals],
  [150505]=[Lotus,Hippos,Pomegranates,Tin],[159841]=[Granite,Granite,Lavender,Grapes],[159842]=[Barley,Flax,Lavender,Grapes],
  [159843]=[Barley,Flax,Iron,Granite],[166385]=[Lotus,Hippos,Pomegranates,Tin,Gold,Gold,Gold,Gold,Gold,Gold],
 };
 static readonly Dictionary<uint,uint[]> Sets=new()
 {
  [149806]=[149800,150502,Iron,150505,150505,150505],[149807]=[149800,150502,150503,150504,150504,150505],
  [149808]=[149800,150504,150504,159841,150505,150505],[166396]=[149800,150502,Iron,150505,150505,166385],
  [166383]=[149800,150502,150503,150504,150504,166385],[166384]=[149800,150504,150504,159841,150505,Gold],
  [149821]=[149800,150502,Iron,150505,150505],[149822]=[159842,150503,150504,150504,150505],
  [149823]=[149800,159841,150504,150504,150505],[166386]=[159842,150503,150504,150504,166385],
  [166387]=[149800,159841,150504,150504,166385],
  [149824]=[149800,150502,150505,150505],[149825]=[159842,150503,150504,150505],
  [149826]=[159843,150504,150504,150505],[166392]=[159842,150503,150504,166385],[166393]=[159843,150504,150504,166385],
 };
 // (starter hard, starter normal, 2nd hard, 3rd hard, 2nd normal, 3rd normal). Regular and Sparse share one starter set.
 static (uint SH,uint SN,uint H2,uint H3,uint N2,uint N3) SetsFor(FertilitySetting setting)=>setting switch
 {
  FertilitySetting.Abundant=>(149806,166396,149807,149808,166383,166384),
  FertilitySetting.Regular=>(149821,149821,149822,149823,166386,166387),
  _=>(149824,149824,149825,149826,166392,166393),
 };

 public static EgyptGeneration Generate(uint seed,MapSizeKind size,FertilitySetting setting=FertilitySetting.Abundant,SlotSetting slots=SlotSetting.Abundant)
 {
  var r=new Rng(seed);r.Advance(9);
  var layered=LayeredIslands(r,size);
  // The site phase, as in Latium: every random slot costs one draw, the first of them picks the requested count; the fixed slots
  // come first and the result is max(fixed, requested) capped by fixed + random.
  r.Advance(SlotPhaseStart-19);var mines=new Dictionary<string,int>();var consumed=0;
  foreach(var island in Fertile)
  {
   var(random,fixedSlots)=Slots[island.Name];
   if(random==0){mines[island.Name]=fixedSlots;continue;}
   var wanted=SlotMinimum(island.Name,slots)+(int)r.Scaled(3);r.Advance(random-1);consumed+=random;
   mines[island.Name]=Math.Min(Math.Max(fixedSlots,wanted),fixedSlots+random);
  }
  // The rules list holds each distinct set of the setting once, sorted by GUID (Abundant: only this order fits all savegames;
  // Regular/Sparse: the same rule, consistent with every savegame). The shuffle consumes one draw per entry after the first,
  // which is why Regular/Sparse (5 entries) start the island shuffle one draw earlier than Abundant (6).
  var s=SetsFor(setting);
  var rules=new[]{s.SH,s.SN,s.H2,s.H3,s.N2,s.N3}.Distinct().OrderBy(x=>x).ToList();
  r.Advance(FertilityPhaseStart-SlotPhaseStart-consumed);r.Shuffle(rules);
  var records=Fertile.ToList();r.Shuffle(records);
  var hard=new HashSet<uint>{s.H2,s.H3};var starters=new HashSet<uint>{s.SH,s.SN};
  var bags=new Dictionary<uint,List<uint>>();var byName=new Dictionary<string,EgyptIsland>();
  foreach(var record in records.OrderByDescending(x=>x.Starter))
  {
   // Rule(): the first entry of the island's kind moves to the end, so each difficulty group alternates between its 2nd and 3rd set.
   var index=rules.FindIndex(set=>record.Starter?starters.Contains(set)&&(s.SH==s.SN||(set==s.SH)==record.Hard):!starters.Contains(set)&&hard.Contains(set)==record.Hard);
   var chosen=rules[index];rules.RemoveAt(index);rules.Add(chosen);
   var isLayered=layered.Contains(record.Name);var variant=isLayered?record.Name+"_layer01":record.Name;
   byName[record.Name]=new(record.Name,variant,record.Starter,record.Hard,chosen,Assign(Sets[chosen],bags,r),isLayered?LayerSlots[record.Name]:mines[record.Name],mines[record.Name]);
  }
  var islands=Fertile.Select(x=>byName[x.Name]).ToArray();
  return new(seed,islands,EgyptMetrics.Calculate(islands));
 }

 // For the island filter: the fertile islands, whether an island can be its smaller variant on a map size, and which fertilities it
 // can get on a setting (the sets its kind can draw, with their pools opened up).
 internal static IReadOnlyList<(string Name,bool Starter,bool Hard)> FertileIslands=>Fertile;
 internal static bool IsStarter(string name)=>Array.Exists(Fertile,x=>x.Name==name&&x.Starter);
 internal const string AnyIsland="";
 // The sets an island can draw (any island: all of them), and a set's slots: neighbouring slots with the same pool form one group.
 internal static EgyptSetChoice[] CandidateSets(string name,FertilitySetting setting)
 {
  var s=SetsFor(setting);(uint Guid,string Role)[] all=[(s.SH,"Starter"),(s.SN,"Starter"),(s.H2,"Secondary"),(s.H3,"Tertiary"),(s.N2,"Secondary"),(s.N3,"Tertiary")];
  IEnumerable<(uint Guid,string Role)> picks=all;
  if(name!=AnyIsland){var island=Array.Find(Fertile,x=>x.Name==name);picks=island.Starter?[all[island.Hard?0:1]]:island.Hard?[all[2],all[3]]:[all[4],all[5]];}
  return[..picks.DistinctBy(x=>x.Guid).Select(x=>new EgyptSetChoice(x.Guid,x.Role,SlotGroups(x.Guid).Any(group=>group.Choices.Contains(Gold))))];
 }
 internal static EgyptSlotGroup[] SlotGroups(uint set)
 {
  var definitions=Sets[set];var result=new List<EgyptSlotGroup>();
  for(var first=0;first<definitions.Length;)
  {
   var last=first;while(last+1<definitions.Length&&definitions[last+1]==definitions[first])last++;
   result.Add(new(first,last,Pools.TryGetValue(definitions[first],out var pool)?[..pool.Distinct()]:[definitions[first]]));first=last+1;
  }
  return[..result];
 }
 internal static bool CanBeLayered(string name,MapSizeKind size)=>name==AnyIsland||LayerLarges.Contains(name)||size!=MapSizeKind.Large&&LayerMediums.Contains(name);
 // How many islands can be of a version at once: the map size decides how many use their smaller variant.
 internal static int MaximumIslands(MapSizeKind size,EgyptLayerMode layer)
 {
  var layered=size switch{MapSizeKind.Large=>2,MapSizeKind.Medium=>6,_=>10};
  return layer switch{EgyptLayerMode.Layer=>layered,EgyptLayerMode.Full=>Fertile.Length-layered,_=>Fertile.Length};
 }
 internal static int FertilitySlots(FertilitySetting setting)=>Sets[SetsFor(setting).SH].Length;
 internal static uint[] PossibleFertilities(string name,FertilitySetting setting)
 {
  if(name==AnyIsland)return[..Fertile.SelectMany(x=>PossibleFertilities(x.Name,setting)).Distinct()];
  var island=Array.Find(Fertile,x=>x.Name==name);var s=SetsFor(setting);
  uint[] sets=island.Starter?[island.Hard?s.SH:s.SN]:island.Hard?[s.H2,s.H3]:[s.N2,s.N3];
  return[..sets.SelectMany(set=>Sets[set]).SelectMany(entry=>Pools.TryGetValue(entry,out var pool)?pool:[entry]).Distinct()];
 }

 // Only the metrics; the same work, kept as its own entry point for the search.
 public static EgyptMetrics Metrics(uint seed,MapSizeKind size,FertilitySetting setting,SlotSetting slots=SlotSetting.Abundant)=>Generate(seed,size,setting,slots).Metrics;

 static HashSet<string> LayeredIslands(Rng r,MapSizeKind size)
 {
  var mediums=LayerMediums.ToList();r.Shuffle(mediums);var larges=LayerLarges.ToList();r.Shuffle(larges);
  var(medium,large)=size switch{MapSizeKind.Large=>(0,2),MapSizeKind.Medium=>(2,4),_=>(4,6)};
  return[..mediums.Take(medium),..larges.Take(large)];
 }

 // The shared-bag draw of Generator.Assign / AlbionGenerator.Assign; Aegyptus has no island-specific rejection.
 static uint[] Assign(uint[] definitions,Dictionary<uint,List<uint>> bags,Rng r)
 {
  var result=new List<uint>();
  foreach(var definition in definitions)
  {
   if(!Pools.TryGetValue(definition,out var source)){result.Add(definition);continue;}
   var rejected=0;var fresh=false;
   while(true)
   {
    if(!bags.TryGetValue(definition,out var bag))bags[definition]=bag=[];
    if(bag.Count==0){bag.AddRange(source);r.Shuffle(bag);fresh=true;rejected=0;}
    var value=bag[^1];bag.RemoveAt(bag.Count-1);
    if(!result.Contains(value)){result.Add(value);break;}
    if(bag.Count!=rejected){bag.Add(value);(bag[rejected],bag[^1])=(bag[^1],bag[rejected]);rejected++;continue;}
    if(!fresh){bag.Clear();continue;}
    throw new InvalidOperationException("Aegyptus fertility bag ran dry.");
   }
  }
  return[..result];
 }

 // The preview frame: every element at its template position with rotation 0, framed by the square that holds all their land
 // (the 2560 map is mostly open sea) with the playable area (20..1890) drawn inside. The fertile islands use the variant this seed
 // picked; the pirate and the trader are the neutral parties.
 public static MapLayout Layout(EgyptGeneration generation)
 {
  var variants=generation.Islands.ToDictionary(x=>x.Name,x=>x.Variant);var extent=1890;
  var layout=new MapLayout{BaseX=20,BaseY=20,BaseSize=1870};
  foreach(var(name,x,y) in Elements)
  {
   var fertile=variants.TryGetValue(name,out var variant);var file=FileName(fertile?variant!:name);
   var kind=fertile?MapLayout.Island:name.StartsWith("pirate")||name.StartsWith("trader")?MapLayout.Special:MapLayout.Decoration;
   layout.Add(file,kind,-1,0,x,y);
   var asset=IslandMasks.Asset(file);extent=Math.Max(extent,Math.Max(x+asset.X1,y+asset.Y1));
  }
  layout.FullSize=extent;layout.Width=extent;
  return layout;
 }

 // Box of an island's buildable land and harbour in island units (x0,y0,x1,y1): the preview's hover outline. The pieces of the delta
 // carry wide unbuildable desert around it, so their full boxes would overlap each other.
 public static(int X0,int Y0,int X1,int Y1)Footprint(string variant)=>Footprints[variant];
 static readonly Dictionary<string,(int X0,int Y0,int X1,int Y1)> Footprints=new(StringComparer.OrdinalIgnoreCase)
 {
  ["large_01"]=(66,31,398,226), ["large_01_layer01"]=(190,78,398,226), ["large_02"]=(71,51,288,336), ["large_02_layer01"]=(122,82,288,336),
  ["large_03"]=(46,26,495,168), ["large_03_layer01"]=(46,26,488,168), ["large_04"]=(70,43,329,237), ["large_04_layer01"]=(70,43,329,237),
  ["large_05"]=(83,185,410,336), ["large_05_layer01"]=(104,185,289,336), ["large_06"]=(82,51,337,254), ["large_06_layer01"]=(82,103,247,254),
  ["large_07"]=(16,323,349,492), ["large_07_layer01"]=(30,344,349,492), ["large_08"]=(221,55,462,275), ["large_08_layer01"]=(272,59,412,253),
  ["large_09"]=(103,59,421,321), ["large_09_layer01"]=(103,59,421,321), ["large_10"]=(146,86,340,374), ["large_10_layer01"]=(188,110,317,301),
  ["medium_01"]=(174,201,309,353), ["medium_01_layer01"]=(235,201,309,353), ["medium_02"]=(93,159,320,306), ["medium_02_layer01"]=(93,159,234,255),
  ["medium_03"]=(175,248,371,380), ["medium_03_layer01"]=(199,248,304,380), ["medium_04"]=(57,264,307,422), ["medium_04_layer01"]=(91,299,234,404),
  ["small_01"]=(221,133,311,225), ["small_01_layer01"]=(221,133,311,225), ["small_02"]=(93,76,174,210), ["small_02_layer01"]=(93,76,174,210),
  ["small_03"]=(221,198,330,303), ["small_03_layer01"]=(221,198,330,303), ["small_04"]=(113,234,231,338), ["small_04_layer01"]=(113,234,231,338),
 };

 public static EgyptTiles Tiles(string variant)=>TileTable.TryGetValue(variant,out var tiles)?tiles:throw new InvalidOperationException($"No tile data for {variant}.");
 public static string FileName(string variant)=>Prefix+variant;

 // Every island element of the template with its fixed position (rotation is always 0).
 public static readonly (string Name,int X,int Y)[] Elements=
 [
  ("deco_07",1344,1344),("deco_06",1536,1232),("medium_02",960,1408),("large_08",448,280),("small_02",1400,1120),("village_01",1152,1120),("medium_04",448,448),("large_09",384,352),("deco_05",1472,960),("deco_08",1472,0),("small_04",1472,512),("village_02",960,704),("medium_03",1280,360),("large_07",1184,448),("deco_09",1664,360),("small_03",960,392),("large_06",1144,176),("deco_11",1152,0),("deco_10",960,0),("deco_15",576,0),("pirate_01",272,0),("deco_12",0,0),("vilage_03",0,136),("large_10",0,512),("deco_14",128,224),("deco_13",336,576),("large_04",960,1120),("large_03",960,960),("large_05",960,1216),("deco_01",0,960),("deco_02",0,1408),("deco_03",448,1408),("deco_04",576,960),("large_01",448,960),("large_02",272,960),("medium_01",448,1072),("small_01",448,1376),("trader_02",216,1600),("sectorproject_01",0,1168)
 ];

 static readonly Dictionary<string,EgyptTiles> TileTable=new(StringComparer.OrdinalIgnoreCase)
 {
  ["large_01"]=new(24832,7089,6948,992),["large_01_layer01"]=new(14959,4783,3542,992),["large_02"]=new(25161,7802,5131,1437),["large_02_layer01"]=new(14299,6855,2762,1437),
  ["large_03"]=new(27624,10368,9206,1020),["large_03_layer01"]=new(15518,4977,3373,1020),["large_04"]=new(26457,8832,7396,1030),["large_05"]=new(27316,8097,5816,543),
  ["large_05_layer01"]=new(14255,6005,3278,543),["large_06"]=new(30256,6541,7300,631),["large_06_layer01"]=new(14295,5185,2990,631),["large_07"]=new(29317,8457,6906,1050),
  ["large_07_layer01"]=new(14987,4375,3213,1050),["large_08"]=new(25937,8852,8665,896),["large_08_layer01"]=new(15434,5420,5167,896),["large_09"]=new(27838,7567,10519,638),
  ["large_10"]=new(22421,6333,5133,653),["large_10_layer01"]=new(13656,5025,3072,653),["medium_01"]=new(10233,3429,1853,505),["medium_01_layer01"]=new(4387,1392,1216,505),
  ["medium_02"]=new(12763,3702,2736,274),["medium_02_layer01"]=new(5002,1891,1772,274),["medium_03"]=new(14143,4957,2611,1063),["medium_03_layer01"]=new(7536,2978,989,1063),
  ["medium_04"]=new(14497,4392,4675,466),["medium_04_layer01"]=new(6987,2455,2366,466),["small_01"]=new(4713,1324,1604,494),["small_02"]=new(4378,1481,654,538),
  ["small_03"]=new(6278,2070,1371,672),["small_04"]=new(6647,2327,1616,739),
 };
}

// Water routes between the Aegyptus islands for the settling guide. The pieces form one landscape whose land (playable or not) only
// leaves the Nile arms open, so a ship's route between two islands winds around it: a grid of Cell map units over the playable area,
// land where the pieces' tile images (1 pixel per map unit; transparent, shallow and harbour pixels are water) are mostly land, and
// the shortest 8-connected water path between the water cells along two islands' coasts. Islands that touch share coast cells (0).
internal sealed class EgyptWaterRoutes
{
 const int Cell=4;
 readonly int size,originX,originY;readonly bool[] water;readonly int[] owner;
 readonly Dictionary<int,int[]> ports=[];readonly Dictionary<int,float[]> fields=[];
 static readonly Dictionary<string,(int W,int H,bool[] Land)?> Pixels=new(StringComparer.OrdinalIgnoreCase);

 // slotOfFile: the guide slot of every fertile island by its asset file; every layout item blocks with its land.
 public EgyptWaterRoutes(MapLayout layout,IReadOnlyDictionary<string,int> slotOfFile)
 {
  originX=layout.BaseX;originY=layout.BaseY;size=(layout.BaseSize+Cell-1)/Cell;
  var landCount=new int[size*size];owner=new int[size*size];Array.Fill(owner,int.MinValue);
  foreach(var item in layout.Items)
  {
   var asset=IslandMasks.Asset(item.Name);var image=Load(item.Name);if(image is null)continue;
   var(w,h,land)=image.Value;int x0=item.X+asset.X0,y1=item.Y+asset.Y1;var slot=slotOfFile.TryGetValue(item.Name,out var s)?s:int.MinValue;
   for(var py=0;py<h;py++)
   {
    var y=y1-1-py;if(y<originY)continue;var cy=(y-originY)/Cell;if(cy>=size)continue;
    for(var px=0;px<w;px++)
    {
     if(!land[py*w+px])continue;var x=x0+px;if(x<originX)continue;var cx=(x-originX)/Cell;if(cx>=size)continue;
     var cell=cy*size+cx;landCount[cell]++;if(slot!=int.MinValue&&owner[cell]==int.MinValue)owner[cell]=slot;
    }
   }
  }
  water=new bool[size*size];for(var cell=0;cell<water.Length;cell++)water[cell]=landCount[cell]*2<Cell*Cell;
  // Coast cells of each island: water cells next to one of its land cells.
  var coast=new Dictionary<int,HashSet<int>>();
  for(var cy=0;cy<size;cy++)for(var cx=0;cx<size;cx++)
  {
   var cell=cy*size+cx;if(!water[cell])continue;
   for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
   {
    int nx=cx+dx,ny=cy+dy;if((uint)nx>=(uint)size||(uint)ny>=(uint)size)continue;var slot=owner[ny*size+nx];
    if(slot==int.MinValue||water[ny*size+nx])continue;if(!coast.TryGetValue(slot,out var set))coast[slot]=set=[];set.Add(cell);
   }
  }
  foreach(var(slot,set) in coast)ports[slot]=[..set];
 }

 // Route length in map units between two islands' coasts, or +infinity when no water path connects them.
 public double Length(int fromSlot,int toSlot)
 {
  if(fromSlot==toSlot)return 0;if(!ports.ContainsKey(fromSlot)||!ports.TryGetValue(toSlot,out var targets))return double.PositiveInfinity;
  var field=Field(fromSlot);var best=float.PositiveInfinity;foreach(var cell in targets)best=Math.Min(best,field[cell]);
  return float.IsPositiveInfinity(best)?double.PositiveInfinity:best*Cell;
 }

 float[] Field(int slot)
 {
  if(fields.TryGetValue(slot,out var known))return known;
  var dist=new float[size*size];Array.Fill(dist,float.PositiveInfinity);var queue=new PriorityQueue<int,float>();
  foreach(var cell in ports[slot]){dist[cell]=0;queue.Enqueue(cell,0);}
  while(queue.TryDequeue(out var cell,out var d))
  {
   if(d>dist[cell])continue;int cx=cell%size,cy=cell/size;
   for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
   {
    if(dx==0&&dy==0)continue;int nx=cx+dx,ny=cy+dy;if((uint)nx>=(uint)size||(uint)ny>=(uint)size)continue;var next=ny*size+nx;if(!water[next])continue;
    // A diagonal step may not cut a land corner.
    if(dx!=0&&dy!=0&&(!water[cy*size+nx]||!water[ny*size+cx]))continue;
    var nd=d+(dx!=0&&dy!=0?1.41421356f:1f);if(nd<dist[next]){dist[next]=nd;queue.Enqueue(next,nd);}
   }
  }
  fields[slot]=dist;return dist;
 }

 // The water path from one island to another as map points (for checking the routes; empty when there is none).
 public List<(double X,double Y)> Path(int fromSlot,int toSlot)
 {
  var points=new List<(double X,double Y)>();if(!ports.ContainsKey(fromSlot)||!ports.TryGetValue(toSlot,out var targets))return points;
  var field=Field(fromSlot);var cell=targets.MinBy(c=>field[c]);if(float.IsPositiveInfinity(field[cell]))return points;
  while(true)
  {
   points.Add((originX+(cell%size+0.5)*Cell,originY+(cell/size+0.5)*Cell));if(field[cell]==0)break;
   int cx=cell%size,cy=cell/size,bestCell=cell;var bestValue=field[cell];
   for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++){int nx=cx+dx,ny=cy+dy;if((uint)nx>=(uint)size||(uint)ny>=(uint)size)continue;var n=ny*size+nx;if(field[n]<bestValue){bestValue=field[n];bestCell=n;}}
   if(bestCell==cell)break;cell=bestCell;
  }
  return points;
 }

 // The grid as an image (north up): water blue, fertile islands sand, other land brown, the given paths red. For checking only.
 public void SavePng(string path,IEnumerable<List<(double X,double Y)>> paths)
 {
  var bgra=new byte[size*size*4];
  for(var cy=0;cy<size;cy++)for(var cx=0;cx<size;cx++)
  {
   var o=((size-1-cy)*size+cx)*4;var cell=cy*size+cx;
   var(r,g,b)=water[cell]?(40,70,110):owner[cell]!=int.MinValue?(214,186,132):(120,104,88);
   bgra[o]=(byte)b;bgra[o+1]=(byte)g;bgra[o+2]=(byte)r;bgra[o+3]=255;
  }
  foreach(var route in paths)foreach(var(x,y) in route)
  {
   int cx=(int)((x-originX)/Cell),cy=(int)((y-originY)/Cell);if((uint)cx>=(uint)size||(uint)cy>=(uint)size)continue;var o=((size-1-cy)*size+cx)*4;bgra[o]=0;bgra[o+1]=0;bgra[o+2]=230;
  }
  var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(size,size,96,96,System.Windows.Media.PixelFormats.Bgra32,null,bgra,size*4);
  var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
  using var stream=System.IO.File.Create(path);encoder.Save(stream);
 }

 // Land pixels of a piece's tile image (null without an image).
 static (int W,int H,bool[] Land)? Load(string asset)
 {
  lock(Pixels)
  {
   if(Pixels.TryGetValue(asset,out var known))return known;(int W,int H,bool[] Land)? result=null;
   try
   {
    var frame=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri($"pack://application:,,,/island_tiles/{asset}.png",UriKind.Absolute),System.Windows.Media.Imaging.BitmapCreateOptions.None,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
    var converted=new System.Windows.Media.Imaging.FormatConvertedBitmap(frame,System.Windows.Media.PixelFormats.Bgra32,null,0);
    int w=converted.PixelWidth,h=converted.PixelHeight;var bytes=new byte[w*h*4];converted.CopyPixels(bytes,w*4,0);var land=new bool[w*h];
    // Land: opaque and not harbour (light blue 120,196,204); transparent pixels and the half-transparent shallows are water.
    for(var i=0;i<w*h;i++){int b=bytes[i*4],g=bytes[i*4+1],r=bytes[i*4+2],a=bytes[i*4+3];land[i]=a==255&&!(r==120&&g==196&&b==204);}
    result=(w,h,land);
   }
   catch(Exception ex) when(ex is System.IO.IOException or UriFormatException or NotSupportedException or InvalidOperationException){result=null;}
   Pixels[asset]=result;return result;
  }
 }
}
