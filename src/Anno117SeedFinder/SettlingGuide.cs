// Settling guide for the seed preview: which islands to settle, and in which order, to reach every fertility of a region with few,
// valuable islands close to each other. Idea and scoring model: ewjax, "Anno 117 Island Selection"
// (https://github.com/ewjax/Anno-117-IslandSelection). There, a settling order is walked until every fertility is covered; each
// island scores the fertilities that are still missing (fixed weights by the production chains that need them), its mine/river/
// marsh slots and its size, times 0.9 per step, minus a penalty per extra island; the orders are searched by simulated annealing.
// The app scores the same way and adds a penalty for open water between an island and the nearest island already settled. It
// searches every order exactly (depth-first, only islands that add a missing fertility), so a map always gets the same guide.
// After the main island the islands follow the population tiers: an island counts at the lowest tier among the fertilities it adds,
// and the next island always adds a fertility of the lowest tier still missing (tier 2 before tier 3 before tier 4).
internal sealed record GuideIsland(int Slot,string Label,string SizeClass,uint[] Fertilities,int Mountain,int River,int Marsh,bool Starter,(int X0,int Y0,int X1,int Y1) Box);
internal sealed record GuideStep(GuideIsland Island,uint[] NewFertilities);
// The map around a region's islands: the raider islands, the map area and the edges the player wants to be near (as seen in the
// preview: NorthWest = +y side, SouthEast = -y, SouthWest = -x, NorthEast = +x). Without it the plan ignores both.
internal enum MapEdge{NorthWest,SouthEast,SouthWest,NorthEast}
internal sealed record GuideMap(IReadOnlyList<(int X0,int Y0,int X1,int Y1)> Raiders,(int X0,int Y0,int X1,int Y1) Area,IReadOnlyList<MapEdge> Edges);
internal sealed record GuidePlan(IReadOnlyList<GuideStep> Steps,double Score);
// A region with two populations (Albion: Romans A, Celts B), solved one after the other: the first population's plan, then the
// second one's, which may not use the first plan's main island and gets a bonus for reusing its other islands.
internal sealed record PopulationGuide(bool PopulationAFirst,GuidePlan First,GuidePlan Second){public double Score=>First.Score+Second.Score;}

internal static class SettlingGuide
{
 // Latium: +70 per tier-2 chain, +50 per tier-3 chain, +30 per tier-4 chain; building materials count half of their chains.
 static readonly Dictionary<uint,double> LatiumWeights=new()
 {
  [2206]=70,[2209]=70,[51212]=70,[2210]=70,                 // mackerel (garum), lavender (soap), resin (amphorae), olives (olive oil)
  [2205]=50,[2202]=80,[4051]=80,[4052]=140,                 // grapes (wine), flax and murex (togas, loungers), sandarac (tablets, loungers, lyres, latrunculi)
  [4053]=125,[4062]=145,[2208]=30,[8577]=30,[32027]=90,     // minerals (fine glass, necklaces, mosaics), marble (statuettes, buildings), oysters and sturgeon (oysters with caviar), gold (necklaces, lyres, latrunculi)
  [4049]=50,                                                // iron (weapons and armour)
 };
 // Population tier of each fertility (the lowest tier that needs it). Latium: mackerel (garum) and lavender (soap) are optional at
 // tier 2 while resin and olives are not, so they count as 2.5: an island that adds only mackerel or lavender of the tier-2 goods
 // comes after the resin/olive island.
 static readonly Dictionary<uint,double> LatiumTiers=new()
 {
  [51212]=2,[2210]=2,[4049]=2,[2206]=2.5,[2209]=2.5,                // resin, olives, iron; mackerel, lavender
  [2205]=3,[2202]=3,[4051]=3,[4052]=3,[4053]=3,[4062]=3,            // grapes, flax, murex, sandarac, minerals, marble
  [2208]=4,[8577]=4,[32027]=4,                                      // oysters, sturgeon, gold
 };
 static readonly Dictionary<string,double> LatiumSize=new(){["C"]=500,["XL"]=300,["L"]=150,["M"]=75,["S"]=30};
 const uint Gold=32027,Sturgeon=8577,Minerals=4053;
 // Albion: the same scheme for the Celtic and the Roman chains.
 static readonly Dictionary<uint,double> AlbionWeights=new()
 {
  [2212]=70,[2217]=190,[4063]=190,[4064]=140,[2218]=100,   // barley (beer), dye plants (trousers, shields, cloaks), copper (torcs, shields, cloaks), tin (horns, shields), saltwort (beef, pelt hats)
  [4082]=50,[2211]=50,[2214]=70,[8487]=120,[51212]=120,   // beaver (pelt hats), ponies (chariots), herbs (sausage), silver (brooches, mirrors), resin (amphorae, wigs)
  [2219]=50,[2202]=50,[8432]=50,[4049]=50,[4066]=75,       // small birds (aspic), flax (wigs), sea shells (mirrors), iron (weapons, armour), granite
 };
 static readonly Dictionary<uint,double> AlbionTiers=new()
 {
  [2212]=2,[2217]=2,[4063]=2,[4064]=2,[4049]=2,[2214]=2,[8487]=2,[51212]=2,   // barley, dye plants, copper, tin, iron, herbs, silver, resin
  [2218]=3,[4082]=3,[2211]=3,[4066]=3,[2219]=3,[2202]=3,[8432]=3,            // saltwort, beaver, ponies, granite, small birds, flax, sea shells
 };
 static readonly Dictionary<string,double> AlbionSize=new(){["XL"]=400,["L"]=200,["M"]=100,["S"]=10};
 const uint Granite=4066;
 internal static readonly uint[] Celtic=[2212,2217,4063,4064,2218,4082,2211,4049,4066];
 internal static readonly uint[] Roman=[2214,8487,51212,2219,2202,8432,4049];
 // Tier-3 goods worth having on a population's first island (Albion: ponies, small birds, sea shells) get FirstIslandBonus.
 internal static double FirstIslandBonus=1000;
 static readonly HashSet<uint> AlbionFirstIslandGoods=[2211,2219,8432];
 const double Decay=0.9,LatiumExtraIsland=200,AlbionExtraIsland=100;
 // Open water between an island and the nearest island already settled, per map unit (a medium weight: a neighbour 100 units
 // away costs 40 points, an island on the other side of a Large map several hundred).
 internal const double DistanceWeight=0.4;
 // Supply routes: the main island (the first one settled) supplies every later island by a straight route. A route that comes closer
 // than RaiderRadius to a raider island costs RaiderWeight per map unit short of it (a route touching the raider: 500 points).
 internal const double RaiderRadius=500,RaiderWeight=1;
 // Preferred map edges: every island costs EdgeWeight per map unit of distance to the nearest preferred edge.
 internal const double EdgeWeight=0.075;
 const int MaxSteps=7;

 // Latium: one plan. With startIsland the first island must be a starting island (start mode "starting island"); withoutCinis
 // leaves the Cinis out of the plan (for a player who does not want to settle it).
 public static GuidePlan? Latium(IReadOnlyList<GuideIsland> islands,bool startIsland,GuideMap? map=null,bool withoutCinis=false)
 {
  if(withoutCinis)islands=[..islands.Where(island=>island.SizeClass!="C")];
  double Score(GuideIsland island,HashSet<uint> needed,int step)
  {
   var value=0d;
   foreach(var fertility in island.Fertilities)
   {
    if(!needed.Contains(fertility)||!LatiumWeights.TryGetValue(fertility,out var weight))continue;
    value+=fertility is Gold or Sturgeon?weight*island.River/10d:fertility==Minerals?weight*island.Mountain/10d:weight;
   }
   return value+10*island.River+5*island.Mountain+LatiumSize.GetValueOrDefault(island.SizeClass);
  }
  var plan=Search(islands,[..LatiumWeights.Keys],LatiumTiers,Score,LatiumExtraIsland,startIsland,NoExclusion,[],map);
  return plan??(startIsland?Search(islands,[..LatiumWeights.Keys],LatiumTiers,Score,LatiumExtraIsland,false,NoExclusion,[],map):null);
 }

 // The original model's reuse bonus: +500 per new fertility after the Romans, +200 after the Celts.
 public static PopulationGuide? Albion(IReadOnlyList<GuideIsland> islands,bool romanFirst,GuideMap? map=null)=>TwoPopulations(islands,romanFirst,Roman,Celtic,AlbionTiers,AlbionScore,AlbionExtraIsland,romanFirst?500:200,map);

 // The second population keeps away from the first one's main island and gets reuseBonus per new fertility for every island it
 // shares with the first plan.
 static PopulationGuide? TwoPopulations(IReadOnlyList<GuideIsland> islands,bool aFirst,uint[] targetA,uint[] targetB,IReadOnlyDictionary<uint,double> tiers,Func<HashSet<int>?,double,Func<GuideIsland,HashSet<uint>,int,double>> score,double extraIsland,double reuseBonus,GuideMap? map)
 {
  var first=Search(islands,aFirst?targetA:targetB,tiers,score(null,0),extraIsland,false,NoExclusion,[],map);
  if(first is null)return null;
  var shared=first.Steps.Select(step=>step.Island.Slot).ToHashSet();
  var second=Search(islands,aFirst?targetB:targetA,tiers,score(shared,reuseBonus),extraIsland,false,first.Steps[0].Island.Slot,[..first.Steps.Select(step=>step.Island)],map);
  return second is null?null:new(aFirst,first,second);
 }

 static Func<GuideIsland,HashSet<uint>,int,double> AlbionScore(HashSet<int>? reused,double reuseBonus)=>(island,needed,step)=>
 {
  var value=0d;var bonus=reused is not null&&reused.Contains(island.Slot)?reuseBonus:0;
  foreach(var fertility in island.Fertilities)
  {
   if(!needed.Contains(fertility)||!AlbionWeights.TryGetValue(fertility,out var weight))continue;
   value+=bonus;
   if(step==0&&AlbionFirstIslandGoods.Contains(fertility))value+=FirstIslandBonus;
   value+=fertility==Granite?weight*island.Mountain/10d:weight;
  }
  return value+10*island.Mountain+5*island.Marsh+AlbionSize.GetValueOrDefault(island.SizeClass);
 };

 // Every order of islands that each add a missing fertility, until all of them are covered (at most MaxSteps islands). A target
 // fertility no island has is left out. After the first island, the next one must add a fertility of the lowest tier still missing. excludedSlot: an island the search may not use (NoExclusion: none; the Cinis has slot -1).
 const int NoExclusion=int.MinValue;
 static GuidePlan? Search(IReadOnlyList<GuideIsland> islands,IReadOnlyCollection<uint> target,IReadOnlyDictionary<uint,double> tiers,Func<GuideIsland,HashSet<uint>,int,double> score,double extraIsland,bool firstMustBeStarter,int excludedSlot,IReadOnlyList<GuideIsland> settledBefore,GuideMap? map=null)
 {
  var available=islands.Where(island=>island.Slot!=excludedSlot).ToArray();
  var needed=target.Where(fertility=>available.Any(island=>island.Fertilities.Contains(fertility))).ToHashSet();
  if(needed.Count==0)return null;
  // The scores are additive per fertility, so they are taken apart once per island: a base value (slots, size) and one value per
  // target fertility, for the first step and for later ones; the search then works on bit masks.
  var bits=needed.ToArray();var count=available.Length;var mask=new int[count];var baseFirst=new double[count];var baseLater=new double[count];
  var valueFirst=new double[count,bits.Length];var valueLater=new double[count,bits.Length];var gaps=new double[count,count];var gapBefore=new double[count];
  for(var i=0;i<count;i++)
  {
   var island=available[i];baseFirst[i]=score(island,[],0);baseLater[i]=score(island,[],1);
   for(var b=0;b<bits.Length;b++)
   {
    if(!island.Fertilities.Contains(bits[b]))continue;mask[i]|=1<<b;
    valueFirst[i,b]=score(island,[bits[b]],0)-baseFirst[i];valueLater[i,b]=score(island,[bits[b]],1)-baseLater[i];
   }
   for(var j=0;j<count;j++)gaps[i,j]=Gap(island.Box,available[j].Box);
   gapBefore[i]=settledBefore.Count==0?double.PositiveInfinity:settledBefore.Min(other=>Gap(island.Box,other.Box));
  }
  // Map ratings, per island (edges) and per pair main island -> island (supply route past a raider).
  var edgeCost=new double[count];var routeCost=new double[count,count];
  if(map is not null)
   for(var i=0;i<count;i++)
   {
    edgeCost[i]=EdgeWeight*EdgeDistance(available[i].Box,map);
    for(var j=0;j<count;j++)if(i!=j)routeCost[i,j]=RaiderWeight*Math.Max(0,RaiderRadius-RaiderDistance(Centre(available[i].Box),Centre(available[j].Box),map.Raiders));
   }
  var bitTier=bits.Select(fertility=>tiers.GetValueOrDefault(fertility,2)).ToArray();
  double LowestTier(int bitMask){var lowest=double.MaxValue;for(var b=0;b<bits.Length;b++)if((bitMask&(1<<b))!=0)lowest=Math.Min(lowest,bitTier[b]);return lowest;}
  var used=new bool[count];var sequence=new int[MaxSteps];double best=double.NegativeInfinity;int[]? bestSequence=null;
  void Walk(int step,int missing,double total)
  {
   if(step>=MaxSteps)return;
   // Upper bound of any completion: every further island adds a missing fertility, so at most popcount(missing) more steps, each
   // worth at most the best island value with every missing fertility and no distance penalty. No completion beats best: stop.
   if(bestSequence is not null)
   {
    var top=0d;
    for(var i=0;i<count;i++)
    {
     if(used[i]||(mask[i]&missing)==0)continue;
     var v=Math.Max(baseFirst[i],baseLater[i]);for(var b=0;b<bits.Length;b++)if((mask[i]&missing&(1<<b))!=0)v+=Math.Max(valueFirst[i,b],valueLater[i,b]);
     top=Math.Max(top,v);
    }
    var bound=0d;var remaining=System.Numerics.BitOperations.PopCount((uint)missing);
    for(var k=step;k<Math.Min(MaxSteps,step+remaining);k++)bound+=Math.Max(0,Math.Pow(Decay,k)*top-k*extraIsland);
    if(total+bound<=best)return;
   }
   var factor=Math.Pow(Decay,step);var neededTier=step==0?0:LowestTier(missing);
   for(var index=0;index<count;index++)
   {
    var gain=mask[index]&missing;
    if(used[index]||gain==0||step==0&&firstMustBeStarter&&!available[index].Starter||step>0&&LowestTier(gain)!=neededTier)continue;
    var value=step==0?baseFirst[index]:baseLater[index];
    for(var b=0;b<bits.Length;b++)if((gain&(1<<b))!=0)value+=step==0?valueFirst[index,b]:valueLater[index,b];
    value=total+factor*value-step*extraIsland;
    var gap=gapBefore[index];for(var s=0;s<step;s++)gap=Math.Min(gap,gaps[index,sequence[s]]);
    if(!double.IsPositiveInfinity(gap))value-=DistanceWeight*gap;
    value-=edgeCost[index];if(step>0)value-=routeCost[sequence[0],index];
    var next=missing&~mask[index];
    sequence[step]=index;
    if(next==0){if(value>best){best=value;bestSequence=sequence[..(step+1)];}}
    else{used[index]=true;Walk(step+1,next,value);used[index]=false;}
   }
  }
  Walk(0,(1<<bits.Length)-1,0);
  if(bestSequence is null)return null;
  // The steps with what each island newly covers.
  var steps=new List<GuideStep>();var open=new HashSet<uint>(needed);
  for(var position=0;position<bestSequence.Length;position++)
  {
   var island=available[bestSequence[position]];steps.Add(new(island,[..island.Fertilities.Where(open.Contains)]));open.ExceptWith(island.Fertilities);
  }
  return new(steps,best);
 }

 static (double X,double Y) Centre((int X0,int Y0,int X1,int Y1) box)=>((box.X0+box.X1)/2d,(box.Y0+box.Y1)/2d);
 // Distance from an island's outline box to the nearest preferred edge of the map area (0 when it reaches the edge).
 internal static double EdgeDistance((int X0,int Y0,int X1,int Y1) box,GuideMap map)
 {
  if(map.Edges.Count==0)return 0;
  return map.Edges.Min(edge=>Math.Max(0,edge switch{MapEdge.NorthWest=>map.Area.Y1-box.Y1,MapEdge.SouthEast=>box.Y0-map.Area.Y0,MapEdge.SouthWest=>box.X0-map.Area.X0,_=>map.Area.X1-box.X1}));
 }
 // Closest approach of the straight route a-b to any raider island's box.
 internal static double RaiderDistance((double X,double Y) a,(double X,double Y) b,IReadOnlyList<(int X0,int Y0,int X1,int Y1)> raiders)
 {
  var best=double.PositiveInfinity;
  foreach(var raider in raiders)
   for(var sample=0;sample<=64;sample++)
   {
    var t=sample/64d;var x=a.X+(b.X-a.X)*t;var y=a.Y+(b.Y-a.Y)*t;
    var dx=Math.Max(0,Math.Max(raider.X0-x,x-raider.X1));var dy=Math.Max(0,Math.Max(raider.Y0-y,y-raider.Y1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy));
   }
  return best;
 }

 // Open water between two outline boxes (0 when they touch or overlap).
 internal static double Gap((int X0,int Y0,int X1,int Y1) a,(int X0,int Y0,int X1,int Y1) b)
 {
  var dx=Math.Max(0,Math.Max(a.X0-b.X1,b.X0-a.X1));var dy=Math.Max(0,Math.Max(a.Y0-b.Y1,b.Y0-a.Y1));
  return Math.Sqrt((double)dx*dx+(double)dy*dy);
 }
}
