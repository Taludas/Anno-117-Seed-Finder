// Settling guide for the seed preview: which islands to settle, and in which order, to reach every fertility of a region with few,
// valuable islands close to each other. Idea and scoring model: ewjax, "Anno 117 Island Selection"
// (https://github.com/ewjax/Anno-117-IslandSelection). There, a settling order is walked until every fertility is covered; each
// island scores the fertilities that are still missing (fixed weights by the production chains that need them), its mine/river/
// marsh slots and its size, times 0.9 per step, minus a penalty per extra island; the orders are searched by simulated annealing.
// The app scores the same way and adds a penalty for the open water between an island and its population's main island, which
// supplies it. It
// searches every order exactly (depth-first, only islands that add a missing fertility), so a map always gets the same guide.
// After the main island the islands follow the population tiers: an island counts at the lowest tier among the fertilities it adds,
// and the next island always adds a fertility of the lowest tier still missing (tier 2 before tier 3 before tier 4).
internal sealed record GuideIsland(int Slot,string Label,string SizeClass,uint[] Fertilities,int Mountain,int River,int Marsh,bool Starter,(int X0,int Y0,int X1,int Y1) Box);
internal sealed record GuideStep(GuideIsland Island,uint[] NewFertilities);
// The map around a region's islands: the raider islands, the map area and the edges the player wants to be near (as seen in the
// preview: NorthWest = +y side, SouthEast = -y, SouthWest = -x, NorthEast = +x). Without it the plan ignores both.
internal enum MapEdge{NorthWest,SouthEast,SouthWest,NorthEast}
// RouteLength (optional): the length of the sea route between two islands (by slot) where it differs from the straight gap
// between their outline boxes; +infinity or none means the straight gap.
internal sealed record GuideMap(IReadOnlyList<(int X0,int Y0,int X1,int Y1)> Raiders,(int X0,int Y0,int X1,int Y1) Area,IReadOnlyList<MapEdge> Edges,Func<int,int,double>? RouteLength=null);
internal sealed record GuidePlan(IReadOnlyList<GuideStep> Steps,double Score);
// A region with two populations (Albion: Romans A, Celts B), solved one after the other: the first population's plan, then the
// second one's, which may not use the first plan's main island and gets a bonus for reusing its other islands.
internal sealed record PopulationGuide(bool PopulationAFirst,GuidePlan First,GuidePlan Second){public double Score=>First.Score+Second.Score;}

internal static class SettlingGuide
{
 // Latium: +70 per tier-2 chain, +50 per tier-3 chain, +30 per tier-4 chain; building materials count half of their chains.
 // A fertility counts its weight whatever slots the island has (the same in every region): the slots score on their own, through
 // the slot points of each island.
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
 static readonly Dictionary<string,double> LatiumSize=new(){["C"]=500,["XL"]=450,["L"]=150,["M"]=75,["S"]=30};
 const uint Minerals=4053;
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
 const uint Tin=4064,Copper=4063;
 // Tin and copper on one island (Albion): most useful together, so each of them is worth half of TinCopperBonus more there.
 internal static double TinCopperBonus=100;
 // The Albion bonuses (tin and copper together, tier-3 goods on the first island) pay off with the space to produce them: they
 // scale with the island's building space, a large Celtic island (about 22,000 tiles) taking the full bonus, a medium one (about
 // 10,000) half of it and a small one (about 3,500) 0.15.
 // false: the full bonuses on every island size (for comparing, --guide-seed with ALBION_BONUS_SCALE=0).
 internal static bool ScaleAlbionBonuses=true;
 internal static double AlbionBonusScale(string sizeClass)=>!ScaleAlbionBonuses?1:sizeClass switch{"M"=>.5,"S"=>.15,_=>1};
 internal static readonly uint[] Celtic=[2212,2217,4063,4064,2218,4082,2211,4049,4066];
 internal static readonly uint[] Roman=[2214,8487,51212,2219,2202,8432,4049];
 // Tier-3 goods worth having on a population's first island (Albion: ponies, small birds, sea shells) get FirstIslandBonus.
 internal static double FirstIslandBonus=1000;
 static readonly HashSet<uint> AlbionFirstIslandGoods=[2211,2219,8432];
 const double Decay=0.9,LatiumExtraIsland=200;
 // Extra-island penalties above the original model's 100 (Albion) so that a plan aims for about three islands per population.
 internal static double AlbionExtraIsland=150;
 // Regions with two populations: at most PopulationMaxIslands islands per population; only if no such plan covers every
 // fertility, the search allows up to MaxSteps.
 internal const int PopulationMaxIslands=4;
 // Open water between an island and its population's main island (the hub that supplies it), per map unit (a medium weight: a
 // neighbour 100 units away costs 20 points, an island on the other side of a Large map a few hundred).
 internal const double DistanceWeight=0.2;
 // Supply routes: the main island (the first one settled) supplies every later island by a straight route. A route that comes closer
 // than RaiderRadius to a raider island costs RaiderWeight per map unit short of it (a route touching the raider: 500 points).
 internal const double RaiderRadius=500,RaiderWeight=1;
 // Preferred map edges: every island costs EdgeWeight per map unit of distance to the nearest preferred edge.
 internal const double EdgeWeight=0.05;
 // A population's main island is its hub: players rarely start on a medium or small island when a large one fits nearly as well, so
 // a medium main island costs MainMediumPenalty and a small one MainSmallPenalty.
 internal static double MainMediumPenalty=150,MainSmallPenalty=300;
 // Latium: a large main island costs MainLargePenalty while an extra-large island or the Cinis could be the main island instead
 // (the guide would otherwise often start on a central large island, as the size bonus counts wherever an island comes in the order).
 internal static double MainLargePenalty=100;
 static double MainSizePenalty(string sizeClass)=>sizeClass switch{"M"=>MainMediumPenalty,"S"=>MainSmallPenalty,_=>0};
 const int MaxSteps=7;
 // Iron (weapons and armour) is not needed to upgrade a population: it counts when an island brings it along, but no island is
 // settled for iron alone and it does not decide the tier order.
 static readonly uint[] IronOptional=[4049];

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
    value+=weight;
   }
   return value+10*island.River+5*island.Mountain+LatiumSize.GetValueOrDefault(island.SizeClass);
  }
  var plan=Search(islands,[..LatiumWeights.Keys],LatiumTiers,Score,LatiumExtraIsland,startIsland,NoExclusion,map,IronOptional,MainLargePenalty);
  return plan??(startIsland?Search(islands,[..LatiumWeights.Keys],LatiumTiers,Score,LatiumExtraIsland,false,NoExclusion,map,IronOptional,MainLargePenalty):null);
 }

 // The original model's reuse bonus: +500 per new fertility after the Romans, +200 after the Celts.
 internal static double ReuseAfterRomans=500,ReuseAfterCelts=200;
 public static PopulationGuide? Albion(IReadOnlyList<GuideIsland> islands,bool romanFirst,GuideMap? map=null)=>TwoPopulations(islands,romanFirst,Roman,Celtic,AlbionTiers,AlbionScore,AlbionExtraIsland,romanFirst?ReuseAfterRomans:ReuseAfterCelts,map,IronOptional);

 // The second population keeps away from the first one's main island and gets reuseBonus per new fertility for every island it
 // shares with the first plan. Its main island is a hub of its own: open water counts from it, as the first population does not
 // supply it. Each population's plan is the best one for that population: first the first population's, then the second's on top.
 static PopulationGuide? TwoPopulations(IReadOnlyList<GuideIsland> islands,bool aFirst,uint[] targetA,uint[] targetB,IReadOnlyDictionary<uint,double> tiers,Func<HashSet<int>?,double,Func<GuideIsland,HashSet<uint>,int,double>> score,double extraIsland,double reuseBonus,GuideMap? map,IReadOnlyCollection<uint>? optional=null)
 {
  List<GuidePlan> Capped(IReadOnlyCollection<uint> target,Func<GuideIsland,HashSet<uint>,int,double> populationScore,int excluded,int keep)=>
   SearchTop(islands,target,tiers,populationScore,extraIsland,false,excluded,map,optional,PopulationMaxIslands,keep) is {Count:>0} capped?capped:SearchTop(islands,target,tiers,populationScore,extraIsland,false,excluded,map,optional,MaxSteps,keep);
  if(Capped(aFirst?targetA:targetB,score(null,0),NoExclusion,1) is not [var first,..])return null;
  var shared=first.Steps.Select(step=>step.Island.Slot).ToHashSet();
  return Capped(aFirst?targetB:targetA,score(shared,reuseBonus),first.Steps[0].Island.Slot,1) is [var second,..]?new(aFirst,first,second):null;
 }

 static Func<GuideIsland,HashSet<uint>,int,double> AlbionScore(HashSet<int>? reused,double reuseBonus)=>(island,needed,step)=>
 {
  var value=0d;var bonus=reused is not null&&reused.Contains(island.Slot)?reuseBonus:0;
  foreach(var fertility in island.Fertilities)
  {
   if(!needed.Contains(fertility)||!AlbionWeights.TryGetValue(fertility,out var weight))continue;
   value+=bonus;
   if(step==0&&AlbionFirstIslandGoods.Contains(fertility))value+=FirstIslandBonus*AlbionBonusScale(island.SizeClass);
   if(fertility is Tin or Copper&&island.Fertilities.Contains(Tin)&&island.Fertilities.Contains(Copper))value+=TinCopperBonus/2*AlbionBonusScale(island.SizeClass);
   value+=weight;
  }
  return value+10*island.Mountain+5*island.Marsh+AlbionSize.GetValueOrDefault(island.SizeClass);
 };

 // Every order of islands that each add a missing fertility, until all of them are covered (at most MaxSteps islands). A target
 // fertility no island has is left out. After the first island, the next one must add a fertility of the lowest tier still missing.
 // excludedSlot: an island the search may not use (NoExclusion: none; the Cinis has slot -1). Optional fertilities score when an
 // island brings them, but never make an island worth settling on their own, and a plan is complete without them.
 const int NoExclusion=int.MinValue;
 static GuidePlan? Search(IReadOnlyList<GuideIsland> islands,IReadOnlyCollection<uint> target,IReadOnlyDictionary<uint,double> tiers,Func<GuideIsland,HashSet<uint>,int,double> score,double extraIsland,bool firstMustBeStarter,int excludedSlot,GuideMap? map=null,IReadOnlyCollection<uint>? optional=null,double largeMainPenalty=0)=>
  SearchTop(islands,target,tiers,score,extraIsland,firstMustBeStarter,excludedSlot,map,optional,MaxSteps,1,largeMainPenalty) is [var plan,..]?plan:null;
 // The `keep` best plans, best first (equal scores: the one found first). A small island is never a population's main island: players
 // don't build their hub on one, however many tier-3 goods it carries. Only when no plan works without one may the main island be small.
 // largeMainPenalty: the cost of a large main island while an extra-large one or the Cinis could start the plan (Latium).
 static List<GuidePlan> SearchTop(IReadOnlyList<GuideIsland> islands,IReadOnlyCollection<uint> target,IReadOnlyDictionary<uint,double> tiers,Func<GuideIsland,HashSet<uint>,int,double> score,double extraIsland,bool firstMustBeStarter,int excludedSlot,GuideMap? map,IReadOnlyCollection<uint>? optional,int maxSteps,int keep,double largeMainPenalty=0)=>
  SearchCore(islands,target,tiers,score,extraIsland,firstMustBeStarter,excludedSlot,map,optional,maxSteps,keep,false,largeMainPenalty) is {Count:>0} plans?plans:SearchCore(islands,target,tiers,score,extraIsland,firstMustBeStarter,excludedSlot,map,optional,maxSteps,keep,true,largeMainPenalty);
 static List<GuidePlan> SearchCore(IReadOnlyList<GuideIsland> islands,IReadOnlyCollection<uint> target,IReadOnlyDictionary<uint,double> tiers,Func<GuideIsland,HashSet<uint>,int,double> score,double extraIsland,bool firstMustBeStarter,int excludedSlot,GuideMap? map,IReadOnlyCollection<uint>? optional,int maxSteps,int keep,bool smallMain,double largeMainPenalty)
 {
  var available=islands.Where(island=>island.Slot!=excludedSlot).ToArray();
  // Only an island that may start the plan makes a large main island second best.
  var largerMain=largeMainPenalty>0&&available.Any(island=>island.SizeClass is "XL" or "C"&&(!firstMustBeStarter||island.Starter));
  var needed=target.Where(fertility=>available.Any(island=>island.Fertilities.Contains(fertility))).ToHashSet();
  if(needed.Count==0)return [];
  // The scores are additive per fertility, so they are taken apart once per island: a base value (slots, size) and one value per
  // target fertility, for the first step and for later ones; the search then works on bit masks.
  var bits=needed.ToArray();var count=available.Length;var mask=new int[count];var baseFirst=new double[count];var baseLater=new double[count];
  var valueFirst=new double[count,bits.Length];var valueLater=new double[count,bits.Length];var gaps=new double[count,count];
  for(var i=0;i<count;i++)
  {
   var island=available[i];baseFirst[i]=score(island,[],0);baseLater[i]=score(island,[],1);
   for(var b=0;b<bits.Length;b++)
   {
    if(!island.Fertilities.Contains(bits[b]))continue;mask[i]|=1<<b;
    valueFirst[i,b]=score(island,[bits[b]],0)-baseFirst[i];valueLater[i,b]=score(island,[bits[b]],1)-baseLater[i];
   }
   for(var j=0;j<count;j++){var route=map?.RouteLength?.Invoke(island.Slot,available[j].Slot)??double.PositiveInfinity;gaps[i,j]=double.IsPositiveInfinity(route)?Gap(island.Box,available[j].Box):route;}
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
  var required=0;for(var b=0;b<bits.Length;b++)if(optional is null||!optional.Contains(bits[b]))required|=1<<b;
  if(required==0)return [];
  var used=new bool[count];var sequence=new int[MaxSteps];var found=new List<(double Score,int[] Sequence)>();
  void Walk(int step,int missing,double total)
  {
   if(step>=maxSteps)return;
   // Upper bound of any completion: every further island adds a missing fertility, so at most popcount(missing) more steps, each
   // worth at most the best island value with every missing fertility and no distance penalty. No completion beats the kept plans: stop.
   if(found.Count>=keep)
   {
    var top=0d;
    for(var i=0;i<count;i++)
    {
     if(used[i]||(mask[i]&missing&required)==0)continue;
     var v=Math.Max(baseFirst[i],baseLater[i]);for(var b=0;b<bits.Length;b++)if((mask[i]&missing&(1<<b))!=0)v+=Math.Max(valueFirst[i,b],valueLater[i,b]);
     top=Math.Max(top,v);
    }
    var bound=0d;var remaining=System.Numerics.BitOperations.PopCount((uint)(missing&required));
    for(var k=step;k<Math.Min(maxSteps,step+remaining);k++)bound+=Math.Max(0,Math.Pow(Decay,k)*top-k*extraIsland);
    if(total+bound<=found[^1].Score)return;
   }
   var factor=Math.Pow(Decay,step);var neededTier=step==0?0:LowestTier(missing&required);
   for(var index=0;index<count;index++)
   {
    var gain=mask[index]&missing;
    if(used[index]||(gain&required)==0||step==0&&(firstMustBeStarter&&!available[index].Starter||!smallMain&&available[index].SizeClass=="S")||step>0&&LowestTier(gain&required)!=neededTier)continue;
    var value=step==0?baseFirst[index]:baseLater[index];
    for(var b=0;b<bits.Length;b++)if((gain&(1<<b))!=0)value+=step==0?valueFirst[index,b]:valueLater[index,b];
    value=total+factor*value-step*extraIsland;
    if(step>0)value-=DistanceWeight*gaps[index,sequence[0]];
    value-=edgeCost[index];if(step>0)value-=routeCost[sequence[0],index];else value-=MainSizePenalty(available[index].SizeClass)+(largerMain&&available[index].SizeClass=="L"?largeMainPenalty:0);
    var next=missing&~mask[index];
    sequence[step]=index;
    if((next&required)==0)
    {
     // Every island of a plan must be needed: an island whose fertilities the others cover anyway would only be there for its
     // slots, its size or a bonus (for example to give the second population a reuse bonus).
     var needless=false;
     for(var s=0;s<=step&&!needless;s++){var others=0;for(var t=0;t<=step;t++)if(t!=s)others|=mask[sequence[t]];needless=(others&required)==required;}
     if(!needless&&(found.Count<keep||value>found[^1].Score)){var at=found.FindIndex(plan=>plan.Score<value);found.Insert(at<0?found.Count:at,(value,sequence[..(step+1)]));if(found.Count>keep)found.RemoveAt(keep);}
    }
    else{used[index]=true;Walk(step+1,next,value);used[index]=false;}
   }
  }
  Walk(0,(1<<bits.Length)-1,0);
  // The steps with what each island newly covers.
  return[..found.Select(plan=>
  {
   var steps=new List<GuideStep>();var open=new HashSet<uint>(needed);
   foreach(var index in plan.Sequence){var island=available[index];steps.Add(new(island,[..island.Fertilities.Where(open.Contains)]));open.ExceptWith(island.Fertilities);}
   return new GuidePlan(steps,plan.Score);
  })];
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
