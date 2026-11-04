// The game version the generator reproduces. Version 3.0 (the DLC03 "Dawn of the Delta" release, patch archive zz_patchfiles_300)
// changed three Roman islands' river slots and roman_island_extralarge_04's start coast; see SiteActivation and
// Generator.StartCoastDirection. 2.1 stays available for the pre-3.0 validation savegames and the stored 2.1 self-test references:
// set ANNO117_GAME_VERSION=2.1 for a whole run, or use Use21() for one block of code.
internal static class GameVersion
{
 internal static bool Is21{get;private set;}=Environment.GetEnvironmentVariable("ANNO117_GAME_VERSION")=="2.1";
 internal static string Name=>Is21?"2.1":"3.0";
 internal static IDisposable Use21()=>new Scope(true);
 internal static IDisposable Use30()=>new Scope(false);
 sealed class Scope:IDisposable
 {
  readonly bool previous=Is21;
  public Scope(bool is21)=>Is21=is21;
  public void Dispose()=>Is21=previous;
 }
}
