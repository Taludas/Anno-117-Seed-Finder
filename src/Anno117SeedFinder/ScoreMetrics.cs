// Player-assigned importance (0-10) per existing filter, combined into one score per seed. See SCORING-PLAN.md.
// Deliberately a pure, UI-independent layer: it reads the values a search already produced (SearchResultRow),
// never gates which seeds appear (that stays the hard filters' job), and never re-generates anything.
internal enum ScoreMetric
{
 LatiumArea,AlbionArea,AlbionSwampArea,
 LatiumMountainSites,LatiumRiverSites,AlbionMountainSites,GoldRiverSites,SturgeonRiverSites,
 MarbleSites,MineralMines,GoldMines,SilverMines,TinMines,CopperMines,
 AdvLatiumHarbourMurex,AdvLatiumHarbourOysters,AdvAlbionHarbourSaltwort,AdvAlbionHarbourSeaShells,AdvAlbionMarshSmallBirds,AdvAlbionMarshBeaver,
 CinisGrapes,CinisFlax,CinisMurex,CinisOysters,CinisSturgeon,CinisGold,
 CinisSlot1,CinisMaxSites
}

// A metric's known value range over 100k seeds (population statistics), used to normalize a raw value to 0..1 for
// scoring, and (Average/Median included) to draw the "where does this seed stand" gauge tooltip on a result column.
internal readonly record struct ScoreRange(double Min,double Max,double Average=0,double Median=0)
{
 public double Normalize(double value)=>Max<=Min?1:System.Math.Clamp((value-Min)/(Max-Min),0,1);
 // Only Min/Max are ever extended (see SCORING-PLAN.md §4) - Average/Median stay the population's own, unaffected.
 public ScoreRange Extend(double value)=>this with{Min=System.Math.Min(Min,value),Max=System.Math.Max(Max,value)};
}

// One result-table column's tooltip: this seed's value plotted between the population's min and max, with the
// median marked for context. Rendered by the DataTemplate keyed to this type in MainWindow.xaml.
internal readonly record struct RangeGauge(string Label,double Value,ScoreRange Range)
{
 public const double BarWidth=180;
 double Fraction(double value)=>Range.Max<=Range.Min?1:System.Math.Clamp((value-Range.Min)/(Range.Max-Range.Min),0,1);
 public double FillWidth=>BarWidth*Fraction(Value);
 public System.Windows.Thickness MedianMarkerMargin=>new(BarWidth*Fraction(Range.Median),0,0,0);
 public string ValueText=>Value.ToString("N0");
 public string SummaryText=>Localization.Instance.Format("GaugeSummaryFormat",Range.Min.ToString("N0"),Range.Max.ToString("N0"),Range.Average.ToString("N0"),Range.Median.ToString("N0"));
}

internal static class ScoreMetrics
{
 // The six Cinis pool fertilities (slots 4-7), same set as Generator.Pool6 / the ChkGrapes..ChkGold checkboxes.
 static readonly Dictionary<ScoreMetric,uint> CinisPoolFertility=new()
 {
  [ScoreMetric.CinisGrapes]=2205,[ScoreMetric.CinisFlax]=2202,[ScoreMetric.CinisMurex]=4051,
  [ScoreMetric.CinisOysters]=2208,[ScoreMetric.CinisSturgeon]=8577,[ScoreMetric.CinisGold]=32027,
 };
 static readonly Dictionary<ScoreMetric,AdvancedFilter> Advanced=new()
 {
  [ScoreMetric.AdvLatiumHarbourMurex]=AdvancedFilter.LatiumHarbourMurex,[ScoreMetric.AdvLatiumHarbourOysters]=AdvancedFilter.LatiumHarbourOysters,
  [ScoreMetric.AdvAlbionHarbourSaltwort]=AdvancedFilter.AlbionHarbourSaltwort,[ScoreMetric.AdvAlbionHarbourSeaShells]=AdvancedFilter.AlbionHarbourSeaShells,
  [ScoreMetric.AdvAlbionMarshSmallBirds]=AdvancedFilter.AlbionMarshSmallBirds,[ScoreMetric.AdvAlbionMarshBeaver]=AdvancedFilter.AlbionMarshBeaver,
 };

 public static bool IsAdvanced(ScoreMetric metric)=>Advanced.ContainsKey(metric);
 public static bool IsCinis(ScoreMetric metric)=>CinisPoolFertility.ContainsKey(metric)||metric is ScoreMetric.CinisSlot1 or ScoreMetric.CinisMaxSites;

 // The raw value read straight from an already-built result row. Cinis metrics are always 0 or 1 (see PopulationRange).
 public static double RawValue(ScoreMetric metric,SearchResultRow row,uint slot1Choice)
 {
  if(Advanced.TryGetValue(metric,out var filter))return row.AdvancedTiles[(int)filter];
  if(CinisPoolFertility.TryGetValue(metric,out var fertility))return row.CinisFertilityIds.Skip(3).Contains(fertility)?1:0;
  return metric switch
  {
   ScoreMetric.LatiumArea=>row.LatiumAreaValue,
   ScoreMetric.AlbionArea=>row.AlbionAreaValue,
   ScoreMetric.AlbionSwampArea=>row.AlbionSwampValue,
   ScoreMetric.LatiumMountainSites=>row.LatiumMountain,
   ScoreMetric.LatiumRiverSites=>row.LatiumRiver,
   ScoreMetric.AlbionMountainSites=>row.AlbionMountain,
   ScoreMetric.GoldRiverSites=>row.GoldRiverSites,
   ScoreMetric.SturgeonRiverSites=>row.SturgeonRiverSites,
   ScoreMetric.MarbleSites=>row.MarbleSites,
   ScoreMetric.MineralMines=>row.MineralMines,
   ScoreMetric.GoldMines=>row.GoldMines,
   ScoreMetric.SilverMines=>row.SilverMines,
   ScoreMetric.TinMines=>row.TinMines,
   ScoreMetric.CopperMines=>row.CopperMines,
   ScoreMetric.CinisSlot1=>slot1Choice!=0&&row.CinisFertilityIds.Length>0&&row.CinisFertilityIds[0]==slot1Choice?1:0,
   ScoreMetric.CinisMaxSites=>row.CinisSiteCounts is{Mountain:19,River:23}?1:0,
   _=>throw new ArgumentOutOfRangeException(nameof(metric))
  };
 }

 // The 100k-seed population range for the four numeric groups; null for the Cinis metrics, which are already 0/1
 // and need no range at all.
 public static ScoreRange? PopulationRange(ScoreMetric metric,MapProfile profile)
 {
  if(Advanced.TryGetValue(metric,out var filter)){var range=AdvancedRanges.For(profile,filter);return new(range.Min,range.Max,range.Average,range.Median);}
  var ranges=AggregateSiteRanges.For(profile);
  return metric switch
  {
   ScoreMetric.LatiumArea=>new(ranges.LatiumAreaMin*1000,ranges.LatiumAreaMax*1000,ranges.LatiumAreaAverage*1000,ranges.LatiumAreaMedian*1000),
   ScoreMetric.AlbionArea=>new(ranges.AlbionAreaMin*1000,ranges.AlbionAreaMax*1000,ranges.AlbionAreaAverage*1000,ranges.AlbionAreaMedian*1000),
   ScoreMetric.AlbionSwampArea=>new(ranges.AlbionSwampMin*1000,ranges.AlbionSwampMax*1000,ranges.AlbionSwampAverage*1000,ranges.AlbionSwampMedian*1000),
   ScoreMetric.LatiumMountainSites=>new(ranges.LatiumMountainMin,ranges.LatiumMountainMax,ranges.LatiumMountainAverage,ranges.LatiumMountainMedian),
   ScoreMetric.LatiumRiverSites=>new(ranges.LatiumRiverMin,ranges.LatiumRiverMax,ranges.LatiumRiverAverage,ranges.LatiumRiverMedian),
   ScoreMetric.AlbionMountainSites=>new(ranges.AlbionMountainMin,ranges.AlbionMountainMax,ranges.AlbionMountainAverage,ranges.AlbionMountainMedian),
   ScoreMetric.GoldRiverSites=>new(ranges.GoldMin,ranges.GoldMax,ranges.GoldAverage,ranges.GoldMedian),
   ScoreMetric.SturgeonRiverSites=>new(ranges.SturgeonMin,ranges.SturgeonMax,ranges.SturgeonAverage,ranges.SturgeonMedian),
   ScoreMetric.MarbleSites=>new(ranges.MarbleMin,ranges.MarbleMax,ranges.MarbleAverage,ranges.MarbleMedian),
   ScoreMetric.MineralMines=>new(ranges.MineralMin,ranges.MineralMax,ranges.MineralAverage,ranges.MineralMedian),
   ScoreMetric.GoldMines=>new(ranges.GoldMineMin,ranges.GoldMineMax,ranges.GoldMineAverage,ranges.GoldMineMedian),
   ScoreMetric.SilverMines=>new(ranges.SilverMin,ranges.SilverMax,ranges.SilverAverage,ranges.SilverMedian),
   ScoreMetric.TinMines=>new(ranges.TinMin,ranges.TinMax,ranges.TinAverage,ranges.TinMedian),
   ScoreMetric.CopperMines=>new(ranges.CopperMin,ranges.CopperMax,ranges.CopperAverage,ranges.CopperMedian),
   _=>null
  };
 }

 // The localization key for a metric's row label, e.g. ScoreMetric.LatiumArea -> "ScoreLatiumArea".
 public static string LabelKey(ScoreMetric metric)=>"Score"+metric;

 // The 20 metrics that have a result-table column and a population range - i.e. everything except the 8 Cinis ones.
 public static readonly ScoreMetric[] NumericMetrics=[..Enum.GetValues<ScoreMetric>().Where(metric=>!IsCinis(metric))];

 // The gauge for one column of one row: null only if the metric has no known population range (never happens for
 // NumericMetrics, but PopulationRange is the single source of truth so this stays consistent with it).
 public static RangeGauge? Gauge(ScoreMetric metric,SearchResultRow row,MapProfile profile)
 {
  var range=PopulationRange(metric,profile);
  if(range is null)return null;
  return new(Localization.Instance[LabelKey(metric)],RawValue(metric,row,0),range.Value);
 }
}

// One weight (0 = off, 1-10 = importance) per metric that currently has one, plus whether out-of-sample values
// should extend a metric's range instead of just clamping (see SCORING-PLAN.md §4).
internal sealed record ScoreWeights(IReadOnlyDictionary<ScoreMetric,int> Values,bool ExtendOutliers)
{
 public static readonly ScoreWeights None=new(new Dictionary<ScoreMetric,int>(),false);
}

internal static class SeedScoring
{
 // Computes and writes Score/ScoreSortKey/ScoreBreakdown on every row in place. With no active weights every row
 // is cleared to "not scored" (Score=null) rather than 0, so an unscored search doesn't look like every seed lost.
 public static void Apply(IReadOnlyList<SearchResultRow> rows,MapProfile profile,uint slot1Choice,ScoreWeights weights)
 {
  var active=weights.Values.Where(x=>x.Value>0).ToArray();
  if(active.Length==0){foreach(var row in rows)row.ClearScore();return;}

  // Effective range per numeric metric: the 100k-seed population range, optionally extended to cover any of this
  // search's own hits that fall outside it (a real possibility since 100k is a sample, not the true extremes).
  var ranges=new Dictionary<ScoreMetric,ScoreRange>();
  foreach(var (metric,_) in active)
  {
   var range=ScoreMetrics.PopulationRange(metric,profile);
   if(range is null)continue;
   var effective=range.Value;
   if(weights.ExtendOutliers)foreach(var row in rows)effective=effective.Extend(ScoreMetrics.RawValue(metric,row,slot1Choice));
   ranges[metric]=effective;
  }

  var totalWeight=active.Sum(x=>x.Value);

  foreach(var row in rows)
  {
   var sum=0.0;var lines=new List<string>();
   foreach(var (metric,weight) in active)
   {
    var raw=ScoreMetrics.RawValue(metric,row,slot1Choice);
    var normalized=ranges.TryGetValue(metric,out var range)?range.Normalize(raw):raw;
    sum+=weight*normalized;
    lines.Add(Localization.Instance.Format("ScoreBreakdownLineFormat",Localization.Instance[ScoreMetrics.LabelKey(metric)],raw.ToString("N0"),(normalized*100).ToString("F0"),weight));
   }
   var score=Math.Round(10*sum/totalWeight,1);
   row.SetScore(score,string.Join(Environment.NewLine,lines));
  }
 }
}
