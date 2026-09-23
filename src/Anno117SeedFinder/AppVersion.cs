// The game version this generator is validated against, and the app's own patch counter for that game version:
// starts at 0 for the first build and goes up by one for every later build (bug fix, feature, etc.), reset to 0
// whenever the game version below changes. Shown in the window title as "<GameVersion>.<Patch>", the same scheme
// the other Anno 117 tools use (e.g. the Layout Tool's "2.2.0.0").
internal static class AppVersion
{
 public const string GameVersion="2.1.0";
 public const int Patch=0;
 public static string WindowTitle=>$"Anno 117 Seed Finder v{GameVersion}.{Patch}";
}
