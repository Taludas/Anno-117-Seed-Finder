using System.Windows.Media;

internal enum RegionKind{Latium,Albion}
internal enum FertilitySetKind{Starter,Secondary,Tertiary,AnyCombination}
internal sealed record FertilityChoice(string Name,uint Guid)
{
 public static readonly FertilityChoice Wildcard=new("*",0);
 public bool IsWildcard=>Guid==0;
 public ImageSource? Icon=>IsWildcard?null:FertilityIcons.Get(Guid);
 public override string ToString()=>Name;
}
internal sealed record SlotGroupDefinition(string Label,int[] SlotIndices,FertilityChoice[] Choices);
internal sealed record FertilitySetDefinition(RegionKind Region,FertilitySetKind Set,int MaximumCount,SlotGroupDefinition[] Groups,bool IsAnyCombination=false);
internal sealed record SlotGroupCondition(int[] SlotIndices,uint[] Required);
internal sealed record IslandCondition(RegionKind Region,FertilitySetKind Set,int MinimumCount,SlotGroupCondition[] Groups,int[]? RequiredSlotIndices=null);

internal static class FertilityDefinitions
{
 static FertilityChoice F(string name,uint guid)=>new(name,guid);
 static readonly FertilityChoice[] Fish=[F("Mackerel",2206),F("Lavender",2209)];
 static readonly FertilityChoice[] RomanGeneral=[F("Grapes",2205),F("Flax",2202),F("Murex",4051),F("Oysters",2208),F("Sturgeon",8577),F("Gold",32027)];
 static readonly FertilityChoice[] CelticFirst=[F("Barley",2212),F("Herbs",2214)];
 static readonly FertilityChoice[] CelticMining=[F("Dye Plants",2217),F("Copper",4063),F("Silver",8487)];
 static readonly FertilityChoice[] CelticGeneral=[F("Saltwort",2218),F("Beaver",4082),F("Ponies",2211),F("Flax",2202),F("Small Birds",2219),F("Sea Shells",8432)];
 static SlotGroupDefinition G(string label,int[] slots,FertilityChoice[] choices)=>new(label,slots,choices);

 public static FertilitySetDefinition Get(RegionKind region,FertilitySetKind set)=>(region,set) switch
 {
  (RegionKind.Latium,FertilitySetKind.Starter)=>new(region,set,4,[G("Slot 1",[0],Fish),G("Slot 2",[1],[F("Iron",4049)]),G(Localization.Instance.Format("SlotsRangeAnyOrderFormat","3–6"),[2,3,4,5],RomanGeneral)]),
  (RegionKind.Latium,FertilitySetKind.Secondary)=>new(region,set,9,[G("Slot 1",[0],Fish),G(Localization.Instance.Format("SlotsRangeAnyOrderFormat","2–3"),[1,2],[F("Olives",2210),F("Resin",51212)]),G("Slot 4",[3],[F("Iron",4049),F("Marble",4062)]),G(Localization.Instance.Format("SlotsRangeAnyOrderFormat","5–6"),[4,5],RomanGeneral)]),
  (RegionKind.Latium,FertilitySetKind.Tertiary)=>new(region,set,9,[G("Slot 1",[0],Fish),G("Slot 2",[1],[F("Iron",4049),F("Marble",4062)]),G(Localization.Instance.Format("SlotsRangeAnyOrderFormat","3–4"),[2,3],RomanGeneral),G(Localization.Instance.Format("SlotsRangeAnyOrderFormat","5–6"),[4,5],[F("Sandarac",4052),F("Minerals",4053)])]),
  (RegionKind.Albion,FertilitySetKind.Starter)=>new(region,set,4,[G("Slot 1",[0],CelticFirst),G("Slot 2",[1],[F("Dye Plants",2217),F("Copper",4063)]),G("Slot 3",[2],[F("Silver",8487)]),G("Slot 4",[3],[F("Iron",4049)]),G(Localization.Instance.Format("SlotsRangeAnyOrderFormat","5–6"),[4,5],CelticGeneral)]),
  (RegionKind.Albion,FertilitySetKind.Secondary)=>new(region,set,6,[G("Slot 1",[0],CelticFirst),G("Slot 2",[1],CelticMining),G("Slot 3",[2],[F("Iron",4049),F("Granite",4066)]),G("Slot 4",[3],[F("Tin",4064)]),G(Localization.Instance.Format("SlotsRangeAnyOrderFormat","5–6"),[4,5],CelticGeneral)]),
  (RegionKind.Albion,FertilitySetKind.Tertiary)=>new(region,set,6,[G("Slot 1",[0],CelticFirst),G("Slot 2",[1],CelticMining),G("Slot 3",[2],[F("Iron",4049),F("Granite",4066)]),G("Slot 4",[3],[F("Resin",51212)]),G(Localization.Instance.Format("SlotsRangeAnyOrderFormat","5–6"),[4,5],CelticGeneral)]),
  (RegionKind.Latium,FertilitySetKind.AnyCombination)=>new(region,set,TotalIslandCount(region),[G(Localization.Instance["AnySlotsAnyOrder"],[0,1],AllChoices(region))],true),
  (RegionKind.Albion,FertilitySetKind.AnyCombination)=>new(region,set,TotalIslandCount(region),[G(Localization.Instance["AnySlotsAnyOrder"],[0,1],AllChoices(region))],true),
  _=>throw new ArgumentOutOfRangeException()
 };

 static int TotalIslandCount(RegionKind region)=>Get(region,FertilitySetKind.Starter).MaximumCount+Get(region,FertilitySetKind.Secondary).MaximumCount+Get(region,FertilitySetKind.Tertiary).MaximumCount;
 static FertilityChoice[] AllChoices(RegionKind region)=>[..
  Get(region,FertilitySetKind.Starter).Groups
   .Concat(Get(region,FertilitySetKind.Secondary).Groups)
   .Concat(Get(region,FertilitySetKind.Tertiary).Groups)
   .SelectMany(group=>group.Choices)
   .GroupBy(choice=>choice.Guid).Select(group=>group.First()).OrderBy(choice=>choice.Name,StringComparer.Ordinal)];

 public static FertilityChoice Choice(RegionKind region,uint guid)=>Get(region,FertilitySetKind.Starter).Groups.Concat(Get(region,FertilitySetKind.Secondary).Groups).Concat(Get(region,FertilitySetKind.Tertiary).Groups).SelectMany(group=>group.Choices).First(choice=>choice.Guid==guid);

 public static uint SetGuid(RegionKind region,FertilitySetKind set)=>(region,set) switch
 {
  (RegionKind.Latium,FertilitySetKind.Starter)=>31312,(RegionKind.Latium,FertilitySetKind.Secondary)=>3656,(RegionKind.Latium,FertilitySetKind.Tertiary)=>14198,
  (RegionKind.Albion,FertilitySetKind.Starter)=>8174,(RegionKind.Albion,FertilitySetKind.Secondary)=>8179,(RegionKind.Albion,FertilitySetKind.Tertiary)=>8181,
  _=>throw new ArgumentOutOfRangeException()
 };

 public static bool IsRegularSet(RegionKind region,uint setGuid)=>setGuid==SetGuid(region,FertilitySetKind.Starter)||setGuid==SetGuid(region,FertilitySetKind.Secondary)||setGuid==SetGuid(region,FertilitySetKind.Tertiary);
}
