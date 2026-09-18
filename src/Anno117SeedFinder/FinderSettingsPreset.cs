using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

internal sealed class FinderSettingsPreset
{
 public int Version { get; set; }=1;
 public MapTemplateKind Template { get; set; }=MapTemplateKind.Corners;
 public MapSizeKind Size { get; set; }=MapSizeKind.Large;
 public StartModeKind StartMode { get; set; }=StartModeKind.Flagship;
 public bool Dlc01 { get; set; }=true;
 public int FertilitySetting { get; set; }
 public string FirstSeed { get; set; }="1";
 public string LastSeed { get; set; }="10000";
 public string Threads { get; set; }="1";
 public string MaximumHits { get; set; }="0";
 public string OutputPath { get; set; }="";
 public string PreviewSeed { get; set; }="1";
 public string MinimumGoldSites { get; set; }="*";
 public string MinimumSturgeonSites { get; set; }="*";
 public string MinimumLatiumMountainSites { get; set; }="*";
 public string MinimumLatiumRiverSites { get; set; }="*";
 public string MinimumAlbionMountainSites { get; set; }="*";
 public bool? GoldSitesEnabled { get; set; }
 public bool? SturgeonSitesEnabled { get; set; }
 public bool? LatiumMountainSitesEnabled { get; set; }
 public bool? LatiumRiverSitesEnabled { get; set; }
 public bool? AlbionMountainSitesEnabled { get; set; }
 public int MinimumLatiumAreaK { get; set; }
 public int MinimumAlbionAreaK { get; set; }
 public int MinimumAlbionSwampAreaK { get; set; }
 public bool? LatiumAreaEnabled { get; set; }
 public bool? AlbionAreaEnabled { get; set; }
 public bool? AlbionSwampAreaEnabled { get; set; }
 public string MinimumGoldMines { get; set; }="*";
 public bool? GoldMinesEnabled { get; set; }
 public string MinimumMarbleSites { get; set; }="*";
 public bool? MarbleSitesEnabled { get; set; }
 public string MinimumMineralMines { get; set; }="*";
 public string MinimumCopperMines { get; set; }="*";
 public string MinimumSilverMines { get; set; }="*";
 public bool MineralMinesEnabled { get; set; }
 public bool CopperMinesEnabled { get; set; }
 public bool SilverMinesEnabled { get; set; }
 public uint CinisSlot1 { get; set; }
 public uint[] CinisFertilities { get; set; }=[];
 public bool CinisMaximumSites { get; set; }
 public List<ConditionPreset> Conditions { get; set; }=[];
}

internal sealed class ConditionPreset
{
 public RegionKind Region { get; set; }
 public FertilitySetKind Set { get; set; }
 public int Minimum { get; set; }=1;
 public uint[][] RequiredByGroup { get; set; }=[];
 public int[] Positions { get; set; }=[];
}

internal static class FinderSettingsStorage
{
 static readonly JsonSerializerOptions Options=new()
 {
  WriteIndented=true,
  Converters={new JsonStringEnumConverter()}
 };

 public static void Save(string path,FinderSettingsPreset preset)
 {
  File.WriteAllText(path,JsonSerializer.Serialize(preset,Options));
 }

 public static FinderSettingsPreset Load(string path)
 {
  var preset=JsonSerializer.Deserialize<FinderSettingsPreset>(File.ReadAllText(path),Options)??throw new InvalidDataException(Localization.Instance["PresetNoSettings"]);
  Validate(preset);return preset;
 }

 static void Validate(FinderSettingsPreset preset)
 {
  if(preset.Version!=1)throw new InvalidDataException(Localization.Instance.Format("UnsupportedPresetVersionFormat",preset.Version));
  if(!Enum.IsDefined(preset.StartMode))throw new InvalidDataException(Localization.Instance["InvalidStartMode"]);
  var profile=MapProfiles.Get(preset.Template,preset.Size,preset.Dlc01);
  var ranges=AggregateSiteRanges.For(profile);
  ValidateMinimum(preset.MinimumGoldSites,ranges.GoldMax,Localization.Instance["LabelGoldRiverSites"]);ValidateMinimum(preset.MinimumSturgeonSites,ranges.SturgeonMax,Localization.Instance["LabelSturgeonRiverSites"]);
  ValidateMinimum(preset.MinimumLatiumMountainSites,ranges.LatiumMountainMax,Localization.Instance["LabelLatiumMountainSites"]);ValidateMinimum(preset.MinimumLatiumRiverSites,ranges.LatiumRiverMax,Localization.Instance["LabelLatiumRiverSites"]);ValidateMinimum(preset.MinimumAlbionMountainSites,ranges.AlbionMountainMax,Localization.Instance["LabelAlbionMountainSites"]);
  ValidateMinimum(preset.MinimumGoldMines,ranges.GoldMineMax,Localization.Instance["LabelGoldMines"]);ValidateMinimum(preset.MinimumMarbleSites,ranges.MarbleMax,Localization.Instance["LabelMarbleMines"]);ValidateMinimum(preset.MinimumMineralMines,ranges.MineralMax,Localization.Instance["LabelMineralMines"]);ValidateMinimum(preset.MinimumCopperMines,ranges.CopperMax,Localization.Instance["LabelCopperMines"]);ValidateMinimum(preset.MinimumSilverMines,ranges.SilverMax,Localization.Instance["LabelSilverMines"]);
  ValidateArea(preset.MinimumLatiumAreaK,ranges.LatiumAreaMin,ranges.LatiumAreaMax,Localization.Instance["LabelLatiumArea"]);ValidateArea(preset.MinimumAlbionAreaK,ranges.AlbionAreaMin,ranges.AlbionAreaMax,Localization.Instance["LabelAlbionArea"]);ValidateArea(preset.MinimumAlbionSwampAreaK,ranges.AlbionSwampMin,ranges.AlbionSwampMax,Localization.Instance["LabelAlbionSwampArea"]);
  if(preset.CinisSlot1 is not(0 or 2206 or 2209))throw new InvalidDataException(Localization.Instance["InvalidCinisSlot1"]);
  if(preset.CinisFertilities.Length>4||preset.CinisFertilities.Distinct().Count()!=preset.CinisFertilities.Length||preset.CinisFertilities.Any(value=>!Generator.Pool6.Contains(value)))throw new InvalidDataException(Localization.Instance["InvalidCinisPresetCount"]);
  foreach(var condition in preset.Conditions)
  {
   var definition=FertilityDefinitions.Get(condition.Region,condition.Set);
   if(condition.Minimum<1||condition.Minimum>profile.Capacity(condition.Region,condition.Set))throw new InvalidDataException(Localization.Instance.Format("InvalidMinimumCountFormat",condition.Region,condition.Set));
   if(condition.RequiredByGroup.Length!=definition.Groups.Length)throw new InvalidDataException(Localization.Instance.Format("InvalidFertilityGroupsFormat",condition.Region,condition.Set));
   for(var index=0;index<definition.Groups.Length;index++)
   {
    var expected=definition.IsAnyCombination?2:definition.Groups[index].SlotIndices.Length;var values=condition.RequiredByGroup[index]??[];
    if(values.Length!=expected)throw new InvalidDataException(Localization.Instance.Format("InvalidFertilityCountFormat",condition.Region,condition.Set));
    if(values.Any(value=>value!=0&&!definition.Groups[index].Choices.Any(choice=>choice.Guid==value)))throw new InvalidDataException(Localization.Instance.Format("InvalidFertilityInPresetFormat",condition.Region,condition.Set));
    if(values.Where(value=>value!=0).Distinct().Count()!=values.Count(value=>value!=0))throw new InvalidDataException(Localization.Instance["DuplicateFertilityInGroup"]);
   }
   if(definition.IsAnyCombination&&(condition.RequiredByGroup[0].Any(value=>value==0)||condition.RequiredByGroup[0].Distinct().Count()!=2))throw new InvalidDataException(Localization.Instance["AnyCombinationNeedsTwo"]);
   var positions=condition.Positions??[];
   if(positions.Distinct().Count()!=positions.Length||positions.Any(position=>!MapLayoutPositions.For(profile,condition.Region,condition.Set).Any(slot=>slot.SlotIndex==position)))throw new InvalidDataException(Localization.Instance.Format("InvalidPositionRequirementFormat",condition.Region,condition.Set));
  }
 }

 static void ValidateMinimum(string text,int maximum,string label)
 {
  if(text=="*")return;
  if(!int.TryParse(text,out var value)||value<1||value>maximum)throw new InvalidDataException(Localization.Instance.Format("InvalidMinimumFormat",label,maximum));
 }
 static void ValidateArea(int value,int minimum,int maximum,string label)
 {
  if(value==0)return;
  if(value<minimum||value>maximum)throw new InvalidDataException(Localization.Instance.Format("InvalidAreaMinimumFormat",label,minimum,maximum));
 }
}
