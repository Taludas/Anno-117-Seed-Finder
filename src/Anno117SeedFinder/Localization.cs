using System.ComponentModel;
using System.IO;

public enum AppLanguage{German,English}

// Number formatting follows the window language, not the Windows settings: German groups thousands with ".", English with ",".
// Applied by the windows only (the command-line switches keep their own formats). WPF bindings (StringFormat=N0) use the
// element's Language, not the thread culture, so the windows set both.
internal static class AppCulture
{
 public static System.Globalization.CultureInfo Current=>Localization.Instance.Language==AppLanguage.English?new("en-US"):new("de-DE");
 public static void Apply(System.Windows.FrameworkElement window)
 {
  var culture=Current;
  System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.CurrentUICulture=culture;
  System.Globalization.CultureInfo.DefaultThreadCurrentCulture=System.Globalization.CultureInfo.DefaultThreadCurrentUICulture=culture;
  window.Language=System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag);
 }
}

// Runtime UI language switch. XAML binds via the indexer with an explicit Source
// (works even on non-visual-tree objects like DataGridColumn, which have no DataContext
// to inherit): Text="{Binding [Key], Source={x:Static loc:Localization.Instance}}".
// Raising PropertyChanged("Item[]") is WPF's documented convention for invalidating
// every indexer binding at once, so a language switch repaints the whole window.
internal sealed class Localization:INotifyPropertyChanged
{
 // SettingsPath must be declared (and thus initialized) before Instance: static field
 // initializers run in textual order, and Instance's constructor calls Load(), which
 // reads SettingsPath - if Instance came first, SettingsPath would still be null then.
 static readonly string SettingsPath=Path.Combine(AppContext.BaseDirectory,"language.txt");
 public static readonly Localization Instance=new();
 public event PropertyChangedEventHandler? PropertyChanged;
 AppLanguage language=Load();

 public AppLanguage Language
 {
  get=>language;
  set
  {
   if(language==value)return;
   language=value;
   try{File.WriteAllText(SettingsPath,value.ToString());}catch{}
   PropertyChanged?.Invoke(this,new PropertyChangedEventArgs("Item[]"));
   PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Language)));
  }
 }

 public string this[string key]=>Strings.TryGetValue(key,out var pair)?(language==AppLanguage.English?pair.En:pair.De):key;
 public string Format(string key,params object[] args)=>string.Format(this[key],args);

 static AppLanguage Load()
 {
  try{if(File.Exists(SettingsPath)&&Enum.TryParse<AppLanguage>(File.ReadAllText(SettingsPath).Trim(),true,out var value))return value;}catch{}
  return AppLanguage.German;
 }

 // Only strings that actually differ between German and English are listed here.
 // Identical tokens (Median, Latium, Albion, Cinis, Starter/Secondary/Tertiary, PoA,
 // DLC01/DLC03, Slot N, min/max, CSV, TXT, fertility names) stay as plain XAML/code
 // literals - adding a key for a word that reads the same in both languages would
 // just be indirection with no payoff.
 static readonly Dictionary<string,(string De,string En)> Strings=new()
 {
  // Header
  ["Subtitle"]=("Offline-Weltgenerator für frei wählbare Kartenprofile","Offline world generator for freely configurable map profiles"),
  ["StartHeader"]=("START","START"),
  ["StartModeTooltip"]=("Die Startart ändert nach den Referenztests nicht die erzeugte Karte.","According to the reference tests, the start mode does not change the generated map."),
  ["Flagship"]=("Flaggschiff","Flagship"),
  ["StartIsland"]=("Startinsel","Starter Island"),
  ["DlcHeader"]=("DLCs","DLCs"),
  ["SlotSettingTooltip"]=("Anzahl der Berg- und Flussplaetze auf den Inseln (Kartenoption Rohstoffvorkommen)","How many mountain and river slots the islands have (map option for resource deposits)"),
  ["Dlc01Tooltip"]=("Prophecies of Ash ein- oder ausschalten","Toggle Prophecies of Ash on or off"),
  ["DlcRetroLabel"]=("nachträglich aktiviert (experimentell)","activated later (experimental)"),
  ["DlcRetroTooltip"]=("Karte ohne DLC erstellt, Prophecies of Ash erst danach im Spiel aktiviert. Alte Inseln bleiben, nur die neuen Inseln und Cinis kommen dazu. Neue Inseln: Fruchtbarkeit nur für gemessene Erweiterungsgrößen sicher.","Map created without DLC, Prophecies of Ash switched on afterwards. Old islands stay, only the new islands and Cinis are added. New islands: fertility is only reliable for measured enlargement sizes."),
  ["Dlc03Tooltip"]=("Dawn of the Delta · vorbereitet für eine spätere Generatorerweiterung","Dawn of the Delta · prepared for a future generator extension"),
  ["PreviewTileLegend"]=("Kachelkarte: sandfarben = Baufläche, oliv = Sumpf (Albion), blau = Fluss, hellblau = Hafen, braun = nicht bebaubar.","Tile map: sand = buildable, olive = marsh (Albion), blue = river, light blue = harbour, brown = not buildable."),
  ["PreviewStyleLabel"]=("Inseln:","Islands:"),
  ["PreviewStyleTiles"]=("Kachelkarte","Tile map"),
  ["PreviewStyleArtwork"]=("Spielgrafik","Game artwork"),
  ["PreviewZoomLabel"]=("Zoom:","Zoom:"),
  ["PreviewZoomFit"]=("Einpassen","Fit"),
  ["PreviewZoomHint"]=("Strg + Mausrad zoomt, Ziehen mit der linken Maustaste verschiebt","Ctrl + mouse wheel zooms, drag with the left mouse button to pan"),
  ["MapProfileHeader"]=("KARTENPROFIL","MAP PROFILE"),
  ["MapTemplateLabel"]=("KARTENFORM","SHAPE"),
  ["MapSizeLabel"]=("GRÖSSE","SIZE"),
  ["SlotsLabel"]=("ROHSTOFFE","RESOURCES"),
  ["FertilityLabel"]=("FRUCHTBARKEIT","FERTILITY"),
  ["FertilitySettingTooltip"]=("Fruchtbarkeits-Einstellung (Vorkommen im Überfluss / Regulär / Karg).","Fertility setting (Abundant / Regular / Sparse)."),
  ["LoadPreset"]=("Preset laden …","Load preset …"),
  ["SavePreset"]=("Preset speichern …","Save preset …"),
  ["LanguageHeader"]=("SPRACHE","LANGUAGE"),
  ["German"]=("Deutsch","Deutsch"),
  ["EnglishName"]=("English","English"),

  // Search range card
  ["SearchRange"]=("Suchbereich","Search range"),
  ["FirstSeed"]=("Erster Seed","First seed"),
  ["MinSeedTooltip"]=("Kleinsten möglichen Seed (1) einsetzen","Set the smallest possible seed (1)"),
  ["LastSeed"]=("Letzter Seed","Last seed"),
  ["MaxSeedTooltip"]=("Größten möglichen Seed (999.999.999) einsetzen","Set the largest possible seed (999,999,999)"),
  ["Threads"]=("Threads","Threads"),
  ["MaxHits"]=("Max. Treffer","Max. hits"),
  ["ZeroMeansAll"]=("0 = alle","0 = all"),
  ["OutputFile"]=("Ausgabedatei","Output file"),
  ["Browse"]=("Wählen …","Browse …"),

  // Cinis card
  ["Slot1"]=("Slot 1","Slot 1"),
  ["CinisSlotsHeader"]=("Slots 4–7 (bis 4)","Slots 4–7 (up to 4)"),
  ["CinisSlotsTooltip"]=("Nur ausgewählte Fertilities werden verlangt; nicht ausgewählte sind egal.","Only the checked fertilities are required; unchecked ones don't matter."),
  ["MaxSites"]=("Nur maximale Bauplätze (19 Berg / 23 Fluss)","Only maximum building sites (19 mountain / 23 river)"),
  ["CinisWarning"]=("Für Slots 4–7 können höchstens vier Fertilities gewählt werden.","At most four fertilities can be selected for slots 4–7."),

  // Additional island conditions card
  ["AdditionalConditions"]=("Weitere Inselbedingungen","Additional island conditions"),
  ["AreaMinimumsHeader"]=("Flächen-Mindestwerte · Schritte zu je 1k Kacheln","Minimum area values · steps of 1k tiles"),
  ["EnableLatiumArea"]=("Latium-Gesamtfläche als Filter aktivieren","Enable Latium total area as a filter"),
  ["TotalAreaLatium"]=("Gesamtfläche · Latium","Total area · Latium"),
  ["SetToRoundedMedian"]=("Auf den abgerundeten Median setzen","Set to the rounded-down median"),
  ["EnableAlbionArea"]=("Albion-Gesamtfläche als Filter aktivieren","Enable Albion total area as a filter"),
  ["TotalAreaAlbion"]=("Gesamtfläche · Albion","Total area · Albion"),
  ["EnableAlbionSwampArea"]=("Albion-Sumpffläche als Filter aktivieren","Enable Albion swamp area as a filter"),
  ["OfWhichSwampAlbion"]=("davon Sumpf · Albion","of which swamp · Albion"),
  ["SiteMinimumsHeader"]=("Bauplatz-Mindestzahlen · aktivierte Werte werden berücksichtigt","Minimum building site counts · enabled values are applied"),
  ["EnableLatiumMountainSites"]=("Allgemeine Bergbauplätze in Latium als Filter aktivieren","Enable general mountain sites in Latium as a filter"),
  ["MountainSitesLatium"]=("Bergbauplätze · Latium","Mountain sites · Latium"),
  ["SetToMedian"]=("Auf den Median setzen","Set to the median"),
  ["EnableLatiumRiverSites"]=("Allgemeine Flussbauplätze in Latium als Filter aktivieren","Enable general river sites in Latium as a filter"),
  ["RiverSitesLatium"]=("Flussbauplätze · Latium","River sites · Latium"),
  ["EnableAlbionMountainSites"]=("Allgemeine Bergbauplätze in Albion als Filter aktivieren","Enable general mountain sites in Albion as a filter"),
  ["MountainSitesAlbion"]=("Bergbauplätze · Albion","Mountain sites · Albion"),
  ["EnableGoldSites"]=("Gold-Flussbauplätze als Filter aktivieren","Enable gold river sites as a filter"),
  ["GoldRiverLatium"]=("Gold-Fluss · Latium","Gold river · Latium"),
  ["EnableSturgeonSites"]=("Sturgeon-Flussbauplätze als Filter aktivieren","Enable sturgeon river sites as a filter"),
  ["SturgeonRiverLatium"]=("Stör-Fluss · Latium","Sturgeon river · Latium"),
  ["EnableGoldMines"]=("Gold-Minen als Filter aktivieren","Enable gold mines as a filter"),
  ["GoldMinesLatium"]=("Gold-Minen · Latium","Gold mines · Latium"),
  ["EnableMarbleSites"]=("Rohmarmor-Steinbrüche als Filter aktivieren","Enable Raw Marble mines as a filter"),
  ["RawMarbleIconTooltip"]=("Rohmarmor","Raw Marble"),
  ["MarbleMinesLatium"]=("Rohmarmor-Steinbrüche · Latium","Raw Marble mines · Latium"),
  ["EnableMineralMines"]=("Mineralien-Minen als Filter aktivieren","Enable mineral mines as a filter"),
  ["MineralsIconTooltip"]=("Mineralien","Minerals"),
  ["MineralMinesLatium"]=("Mineralien-Minen · Latium","Mineral mines · Latium"),
  ["EnableCopperMines"]=("Kupfer-Minen als Filter aktivieren","Enable copper mines as a filter"),
  ["CopperIconTooltip"]=("Kupfer","Copper"),
  ["CopperMinesAlbion"]=("Kupfer-Minen · Albion","Copper mines · Albion"),
  ["EnableSilverMines"]=("Silber-Minen als Filter aktivieren","Enable silver mines as a filter"),
  ["SilverIconTooltip"]=("Silber","Silver"),
  ["SilverMinesAlbion"]=("Silber-Minen · Albion","Silver mines · Albion"),
  ["EnableTinMines"]=("Zinn-Minen als Filter aktivieren","Enable tin mines as a filter"),
  ["TinIconTooltip"]=("Zinn","Tin"),
  ["TinMinesAlbion"]=("Zinn-Minen · Albion","Tin mines · Albion"),
  ["AdvancedFiltersHeader"]=("Erweiterte Fruchtbarkeitsfilter · Kacheln der Inseln mit der Fruchtbarkeit · Schritte zu je 500 Kacheln","Advanced fertility filters · tiles of the islands that carry the fertility · steps of 500 tiles"),
  ["ShowAdvancedFilters"]=("Erweiterte Fruchtbarkeitsfilter anzeigen","Show advanced fertility filters"),
  ["ShowAdvancedFiltersTooltip"]=("Blendet die erweiterten Filter (Hafen- und Sumpfkacheln der Inseln mit einer bestimmten Fruchtbarkeit) und alle fruchtbarkeitsbezogenen Spalten und Filterzeilen ein: Gold- und Störfluss, die einzelnen Minen und die Spalten der erweiterten Filter. Ohne den Haken zeigt die Tabelle nur Flächen und Bauplätze gesamt. Ausgeblendete Filter werden bei der Suche nicht angewendet.","Shows the advanced filters (harbour and swamp tiles of the islands that carry a given fertility) and every fertility-specific column and filter row: gold and sturgeon river, the single mines and the advanced filter columns. Without it the table shows only areas and total slots. Hidden filters are not applied to the search."),
  ["AdvLatiumHarbourMurex"]=("Hafenfläche · Purpurschnecken · Latium","Harbour tiles · Murex · Latium"),
  ["AdvEnableLatiumHarbourMurex"]=("Hafenfläche der Purpurschnecken-Inseln in Latium als Filter aktivieren","Enable the harbour tiles of the Murex islands in Latium as a filter"),
  ["AdvColumnLatiumHarbourMurex"]=("Hafenfläche der Inseln mit Purpurschnecken · Latium","Harbour tiles of the islands with Murex · Latium"),
  ["AdvLatiumHarbourOysters"]=("Hafenfläche · Austern · Latium","Harbour tiles · Oysters · Latium"),
  ["AdvEnableLatiumHarbourOysters"]=("Hafenfläche der Austern-Inseln in Latium als Filter aktivieren","Enable the harbour tiles of the Oysters islands in Latium as a filter"),
  ["AdvColumnLatiumHarbourOysters"]=("Hafenfläche der Inseln mit Austern · Latium","Harbour tiles of the islands with Oysters · Latium"),
  ["AdvAlbionHarbourSaltwort"]=("Hafenfläche · Salzkraut · Albion","Harbour tiles · Saltwort · Albion"),
  ["AdvEnableAlbionHarbourSaltwort"]=("Hafenfläche der Salzkraut-Inseln in Albion als Filter aktivieren","Enable the harbour tiles of the Saltwort islands in Albion as a filter"),
  ["AdvColumnAlbionHarbourSaltwort"]=("Hafenfläche der Inseln mit Salzkraut · Albion","Harbour tiles of the islands with Saltwort · Albion"),
  ["AdvAlbionHarbourSeaShells"]=("Hafenfläche · Kammmuscheln · Albion","Harbour tiles · Sea Shells · Albion"),
  ["AdvEnableAlbionHarbourSeaShells"]=("Hafenfläche der Kammmuscheln-Inseln in Albion als Filter aktivieren","Enable the harbour tiles of the Sea Shells islands in Albion as a filter"),
  ["AdvColumnAlbionHarbourSeaShells"]=("Hafenfläche der Inseln mit Kammmuscheln · Albion","Harbour tiles of the islands with Sea Shells · Albion"),
  ["AdvAlbionMarshSmallBirds"]=("Sumpffläche · Kleine Vögel · Albion","Swamp tiles · Small Birds · Albion"),
  ["AdvEnableAlbionMarshSmallBirds"]=("Sumpffläche der Kleine Vögel-Inseln in Albion als Filter aktivieren","Enable the swamp tiles of the Small Birds islands in Albion as a filter"),
  ["AdvColumnAlbionMarshSmallBirds"]=("Sumpffläche der Inseln mit Kleine Vögel · Albion","Swamp tiles of the islands with Small Birds · Albion"),
  ["AdvAlbionMarshBeaver"]=("Sumpffläche · Biber · Albion","Swamp tiles · Beaver · Albion"),
  ["AdvEnableAlbionMarshBeaver"]=("Sumpffläche der Biber-Inseln in Albion als Filter aktivieren","Enable the swamp tiles of the Beaver islands in Albion as a filter"),
  ["AdvColumnAlbionMarshBeaver"]=("Sumpffläche der Inseln mit Biber · Albion","Swamp tiles of the islands with Beaver · Albion"),
  ["AdvFertilityLatiumHarbourMurex"]=("Purpurschnecken","Murex"),
  ["AdvFertilityLatiumHarbourOysters"]=("Austern","Oysters"),
  ["AdvFertilityAlbionHarbourSaltwort"]=("Salzkraut","Saltwort"),
  ["AdvFertilityAlbionHarbourSeaShells"]=("Kammmuscheln","Sea Shells"),
  ["AdvFertilityAlbionMarshSmallBirds"]=("Kleine Vögel","Small Birds"),
  ["AdvFertilityAlbionMarshBeaver"]=("Biber","Beaver"),
  ["ScoreLatiumArea"]=("Gesamtfläche · Latium","Total area · Latium"),
  ["ScoreAlbionArea"]=("Gesamtfläche · Albion","Total area · Albion"),
  ["ScoreAlbionSwampArea"]=("davon Sumpf · Albion","of which swamp · Albion"),
  ["ScoreLatiumMountainSites"]=("Bergbauplätze · Latium","Mountain sites · Latium"),
  ["ScoreLatiumRiverSites"]=("Flussbauplätze · Latium","River sites · Latium"),
  ["ScoreAlbionMountainSites"]=("Bergbauplätze · Albion","Mountain sites · Albion"),
  ["ScoreGoldRiverSites"]=("Gold-Fluss · Latium","Gold river · Latium"),
  ["ScoreSturgeonRiverSites"]=("Stör-Fluss · Latium","Sturgeon river · Latium"),
  ["ScoreMarbleSites"]=("Rohmarmor-Steinbrüche · Latium","Raw Marble mines · Latium"),
  ["ScoreMineralMines"]=("Mineralien-Minen · Latium","Mineral mines · Latium"),
  ["ScoreGoldMines"]=("Gold-Minen · Latium","Gold mines · Latium"),
  ["ScoreSilverMines"]=("Silber-Minen · Albion","Silver mines · Albion"),
  ["ScoreTinMines"]=("Zinn-Minen · Albion","Tin mines · Albion"),
  ["ScoreCopperMines"]=("Kupfer-Minen · Albion","Copper mines · Albion"),
  ["ScoreAdvLatiumHarbourMurex"]=("Hafenfläche · Purpurschnecken · Latium","Harbour tiles · Murex · Latium"),
  ["ScoreAdvLatiumHarbourOysters"]=("Hafenfläche · Austern · Latium","Harbour tiles · Oysters · Latium"),
  ["ScoreAdvAlbionHarbourSaltwort"]=("Hafenfläche · Salzkraut · Albion","Harbour tiles · Saltwort · Albion"),
  ["ScoreAdvAlbionHarbourSeaShells"]=("Hafenfläche · Kammmuscheln · Albion","Harbour tiles · Sea Shells · Albion"),
  ["ScoreAdvAlbionMarshSmallBirds"]=("Sumpffläche · Kleine Vögel · Albion","Swamp tiles · Small Birds · Albion"),
  ["ScoreAdvAlbionMarshBeaver"]=("Sumpffläche · Biber · Albion","Swamp tiles · Beaver · Albion"),
  ["ScoreCinisGrapes"]=("Cinis-Pool · Grapes","Cinis pool · Grapes"),
  ["ScoreCinisFlax"]=("Cinis-Pool · Flax","Cinis pool · Flax"),
  ["ScoreCinisMurex"]=("Cinis-Pool · Murex","Cinis pool · Murex"),
  ["ScoreCinisOysters"]=("Cinis-Pool · Oysters","Cinis pool · Oysters"),
  ["ScoreCinisSturgeon"]=("Cinis-Pool · Sturgeon","Cinis pool · Sturgeon"),
  ["ScoreCinisGold"]=("Cinis-Pool · Gold","Cinis pool · Gold"),
  ["ScoreCinisSlot1"]=("Cinis Slot 1","Cinis slot 1"),
  ["ScoreCinisMaxSites"]=("Cinis maximale Bauplätze","Cinis maximum sites"),
  ["EnableScoring"]=("Seed-Bewertungsfunktion aktivieren","Enable seed scoring feature"),
  ["EnableScoringTooltip"]=("Vergib jedem Filter eine Wichtigkeit (0-10); am Ende bekommt jeder Seed eine Bewertung (x,x von 10) aus allen gewichteten Werten. Hart-Filter (Häkchen + Mindestwert) schließen weiterhin Seeds aus; die Bewertung ordnet nur die verbleibenden.","Give each filter an importance (0-10); every seed then gets one score (x.x of 10) from all weighted values. Hard filters (checkbox + minimum) still exclude seeds; scoring only ranks what is left."),
  ["ExtendScoringOutliers"]=("Bewertungsbereich bei Ausreißern erweitern","Extend the scoring range for outliers"),
  ["ExtendScoringOutliersTooltip"]=("Die 100.000-Seed-Statistik ist eine Stichprobe; liegt ein Treffer außerhalb davon, wird der Bereich für diese Suche erweitert, statt den Wert einfach auf 0 oder 1 zu kappen.","The 100k-seed statistic is a sample; if a hit falls outside it, the range is extended for this search instead of just clamping the value to 0 or 1."),
  ["WeightTooltip"]=("Wichtigkeit für die Seed-Bewertung (0 = nicht bewertet, 10 = am wichtigsten). Unabhängig vom Häkchen links.","Importance for seed scoring (0 = not scored, 10 = most important). Independent of the checkbox on the left."),
  ["WeightColumnHeader"]=("Gewichtung","Weight"),
  ["WeightColumnHeaderTooltip"]=("Wie stark dieser Filter in die Seed-Bewertung eingeht (0-10 je Zeile); nur sichtbar, solange \"Seed-Bewertungsfunktion aktivieren\" angehakt ist. Unabhängig vom Häkchen links in jeder Zeile.","How much this filter counts toward the seed score (0-10 per row); only shown while \"Enable seed scoring feature\" is ticked. Independent of the checkbox on the left of each row."),
  ["ScoreColumnHeader"]=("Bewertung","Score"),
  ["ScoreColumnTooltip"]=("Bewertung aus den vergebenen Wichtigkeiten (0-10 von 10); Mauszeiger für die Aufschlüsselung. Ein Strich bedeutet: Seed-Bewertung ist aus oder es ist keine Wichtigkeit vergeben.","Score from the assigned weights (0-10 of 10); hover for the breakdown. A dash means seed scoring is off or no weight is set."),
  ["ScoreBreakdownLineFormat"]=("{0}: {1} - {2} % erreicht - Gewicht {3}","{0}: {1} - {2}% reached - weight {3}"),
  ["GaugeSummaryFormat"]=("100.000 Seeds: {0}-{1} · Ø {2} · Median {3}","100k seeds: {0}-{1} · avg {2} · median {3}"),
  ["MaxConditionsTooltipFormat"]=("Maximal {0} {1}-Bedingungen für das aktuelle Profil.","At most {0} {1} conditions for the current profile."),

  // Bottom bar
  ["SeedPreview"]=("Seed-Vorschau","Seed preview"),
  ["RandomSeed"]=("Zufall","Random"),
  ["RandomSeedTooltip"]=("Zufälligen gültigen Seed (1 bis 999.999.999) einsetzen","Fill in a random valid seed (1 to 999,999,999)"),
  ["Preview"]=("Vorschau","Preview"),
  ["AddToTable"]=("In Tabelle übernehmen","Add to table"),
  ["AddToTableTooltip"]=("Den eingetragenen Seed berechnen und als Zeile an die Ergebnistabelle anhängen.","Compute the entered seed and append it as a row to the results table."),
  ["LoadSeedList"]=("Seedliste laden …","Load seed list …"),
  ["LoadSeedListTooltip"]=("Eine Textdatei mit einem Seed pro Zeile (z. B. treffer.txt) einlesen und als Tabelle anzeigen.","Read a text file with one seed per line (e.g. treffer.txt) and show it as a table."),
  ["ExportCsv"]=("CSV exportieren","Export CSV"),
  ["ExportCsvTooltip"]=("Die Tabelle als CSV-Datei speichern.","Save the table as a CSV file."),
  ["OpenTxt"]=("TXT öffnen","Open TXT"),
  ["Cancel"]=("Abbrechen","Cancel"),
  ["StartSearch"]=("Suche starten","Start search"),
  ["Ready"]=("Bereit","Ready"),
  ["ColumnsHeader"]=("Spalten:","Columns:"),
  ["GoldRiverColumn"]=("Gold-Fluss","Gold river"),
  ["SturgeonRiverColumn"]=("Stör-Fluss","Sturgeon river"),
  ["GoldMinesColumn"]=("Gold-Minen","Gold mines"),
  ["MarbleMinesColumn"]=("Rohmarmor-Steinbrüche","Raw Marble mines"),
  ["MineralMinesColumn"]=("Mineralien-Minen","Mineral mines"),
  ["CopperMinesColumn"]=("Kupfer-Minen","Copper mines"),
  ["SilverMinesColumn"]=("Silber-Minen","Silver mines"),
  ["TinMinesColumn"]=("Zinn-Minen","Tin mines"),
  ["MultiSortHint"]=("· Mehrfachsortierung: Umschalt+Klick auf weitere Spaltenköpfe","· Multi-sort: Shift+click additional column headers"),

  // Results table
  ["GridTooltip"]=("Doppelklick öffnet die Weltvorschau für den gewählten Seed.","Double-click opens the world preview for the selected seed."),
  ["Seed"]=("Seed","Seed"),
  ["LatiumAreaHeader"]=("▦ Latium","▦ Latium"),
  ["AlbionAreaHeader"]=("▦ Albion","▦ Albion"),
  ["SwampAreaHeader"]=("▦ Sumpf","▦ Swamp"),
  ["MountainSitesInLatiumTooltip"]=("Bergbauplätze in Latium","Mountain sites in Latium"),
  ["RiverSitesInLatiumTooltip"]=("Flussbauplätze in Latium","River sites in Latium"),
  ["MountainSitesInAlbionTooltip"]=("Bergbauplätze in Albion","Mountain sites in Albion"),
  ["GoldRiverSitesTooltip"]=("Gold-Flussbauplätze","Gold river sites"),
  ["SturgeonRiverSitesTooltip"]=("Sturgeon-Flussbauplätze","Sturgeon river sites"),
  ["GoldMinesTooltip"]=("Goldminen","Gold mines"),
  ["MarbleSitesTooltip"]=("Rohmarmor-Bauplätze · Latium","Raw Marble sites · Latium"),
  ["MineralMinesTooltip"]=("Mineralien-Minen · Latium","Mineral mines · Latium"),
  ["CopperMinesTooltip"]=("Kupfer-Minen · Albion","Copper mines · Albion"),
  ["SilverMinesTooltip"]=("Silber-Minen · Albion","Silver mines · Albion"),
  ["TinMinesTooltip"]=("Zinn-Minen · Albion","Tin mines · Albion"),
  ["CinisFertilitiesHeader"]=("Cinis-Fruchtbarkeiten","Cinis fertilities"),

  // Dialog titles / filters (code-behind)
  ["SelectOutputFileTitle"]=("Trefferdatei auswählen","Select results file"),
  ["TxtFilter"]=("Textdatei (*.txt)|*.txt|Alle Dateien (*.*)|*.*","Text file (*.txt)|*.txt|All files (*.*)|*.*"),
  ["SavePresetTitle"]=("Seed-Finder-Preset speichern","Save seed finder preset"),
  ["PresetFilter"]=("Seed-Finder-Preset (*.anno117settings.json)|*.anno117settings.json|JSON-Datei (*.json)|*.json","Seed finder preset (*.anno117settings.json)|*.anno117settings.json|JSON file (*.json)|*.json"),
  ["PresetSavedFormat"]=("Preset gespeichert: {0}","Preset saved: {0}"),
  ["PresetSaveFailedTitle"]=("Preset konnte nicht gespeichert werden","Could not save preset"),
  ["LoadPresetTitle"]=("Seed-Finder-Preset laden","Load seed finder preset"),
  ["PresetLoadedFormat"]=("Preset geladen: {0}","Preset loaded: {0}"),
  ["PresetLoadFailedTitle"]=("Preset konnte nicht geladen werden","Could not load preset"),
  ["SearchStarting"]=("Suche wird gestartet …","Starting search …"),
  ["ProgressFormat"]=("{0} / {1} Seeds · {2} Treffer","{0} / {1} seeds · {2} hits"),
  ["CanceledFormat"]=("Abgebrochen · {0} / {1} Seeds · {2} Treffer · {3}","Cancelled · {0} / {1} seeds · {2} hits · {3}"),
  ["CompletedFormat"]=("{0} Treffer in {1} s · {2}","{0} hits in {1} s · {2}"),
  ["SearchCanceled"]=("Suche abgebrochen","Search cancelled"),
  ["GenericError"]=("Fehler","Error"),
  ["SearchStartFailedTitle"]=("Suche konnte nicht gestartet werden","Could not start search"),
  ["InvalidSeedFormat"]=("Bitte einen Seed von {0} bis {1} eingeben.","Please enter a seed from {0} to {1}."),
  ["InvalidSeedTitle"]=("Ungültiger Seed","Invalid seed"),
  ["SeedAlreadyInTableFormat"]=("Seed {0} steht bereits in der Tabelle","Seed {0} is already in the table"),
  ["SeedAddedFormat"]=("Seed {0} übernommen · {1} Zeilen","Seed {0} added · {1} rows"),
  ["SeedComputeFailedTitle"]=("Seed konnte nicht berechnet werden","Could not compute seed"),
  ["SaveCsvTitle"]=("Tabelle als CSV speichern","Save table as CSV"),
  ["CsvFilter"]=("CSV-Datei (*.csv)|*.csv|Alle Dateien (*.*)|*.*","CSV file (*.csv)|*.csv|All files (*.*)|*.*"),
  ["CsvSavedFormat"]=("CSV gespeichert: {0}","CSV saved: {0}"),
  ["CsvSaveFailedTitle"]=("CSV konnte nicht gespeichert werden","Could not save CSV"),
  ["LoadSeedListTitle"]=("Seedliste laden","Load seed list"),
  ["SeedListFilter"]=("Textdatei (*.txt)|*.txt|CSV-Datei (*.csv)|*.csv|Alle Dateien (*.*)|*.*","Text file (*.txt)|*.txt|CSV file (*.csv)|*.csv|All files (*.*)|*.*"),
  ["NoValidSeedFound"]=("In der Datei wurde kein gültiger Seed gefunden.","No valid seed was found in the file."),
  ["NoSeedsFoundTitle"]=("Keine Seeds gefunden","No seeds found"),
  ["EvaluatingSeedsFormat"]=("{0} Seeds werden ausgewertet …","Evaluating {0} seeds …"),
  ["SeedsLoadedFormat"]=("{0} Seeds aus {1} geladen","{0} seeds loaded from {1}"),
  ["TableSeedsFormat"]=("Nur die Seeds der Ergebnistabelle durchsuchen ({0})","Search only the seeds in the results table ({0})"),
  ["TableSeedsTooltip"]=("Die Suche läuft nur über die Seeds, die gerade in der Tabelle stehen (zum Beispiel eine geladene treffer.txt oder CSV-Datei), und behält die, die auch die aktuellen Filter erfüllen. So lässt sich schrittweise filtern. Dasselbe Kartenprofil einstellen, mit dem die Seeds gefunden wurden.","The search runs only over the seeds currently in the table (for example a loaded treffer.txt or CSV file) and keeps those that also pass the current filters, so you can filter step by step. Set the same map profile the seeds were found with."),
  ["NoHitsRestoredFormat"]=("Kein Seed hat die Filter bestanden. Die Tabelle zeigt wieder die vorherige Liste ({0} Seeds); Filter ändern und erneut versuchen.","No seed passed the filters. The table shows the previous list again ({0} seeds); change the filters and try again."),
  ["NoSeedsInTable"]=("Die Ergebnistabelle enthält keine Seeds, die durchsucht werden könnten.","The results table has no seeds to search."),
  ["SeedListLoadFailedTitle"]=("Seedliste konnte nicht geladen werden","Could not load seed list"),
  ["ParseSeedFormat"]=("{0}: Bitte eine ganze Zahl von {1} bis {2} eingeben.","{0}: Please enter a whole number from {1} to {2}."),
  ["ParsePositiveFormat"]=("{0}: Bitte eine ganze Zahl größer als 0 eingeben.","{0}: Please enter a whole number greater than 0."),
  ["ParseNonNegativeFormat"]=("{0}: Bitte 0 oder eine positive ganze Zahl eingeben.","{0}: Please enter 0 or a positive whole number."),
  ["WithoutPoASuffix"]=(" · ohne PoA"," · without PoA"),
  ["UpToWord"]=("bis","up to"),
  ["ProfileLabelFormat"]=("{0} · {1} / {2}{3}: {4} Starter · {5} {6} Secondary · {5} {7} Tertiary","{0} · {1} / {2}{3}: {4} Starter · {5} {6} Secondary · {5} {7} Tertiary"),

  // ConditionRowControl
  ["Remove"]=("Entfernen","Remove"),
  ["AtLeast"]=("mindestens","at least"),
  ["IslandsWord"]=("Insel(n)","island(s)"),
  ["DuplicateFertilityInGroup"]=("Dieselbe Fertility kann innerhalb dieser Slotgruppe nicht doppelt vorkommen.","The same fertility cannot appear twice within this slot group."),
  ["MinimumTooltipFormat"]=("Zusammen mit den anderen {0}-Bedingungen sind höchstens {1} Insel(n) möglich.","Together with the other {0} conditions, at most {1} island(s) are possible."),
  ["WildcardIgnoredSuffix"]=(" · * wird ignoriert"," · * is ignored"),
  ["PositionsButton"]=("Positionen …","Positions …"),
  ["PositionsButtonCountFormat"]=("Positionen ({0}) …","Positions ({0}) …"),
  ["NoPositionRequirement"]=("Keine Positionsvorgabe","No position requirement"),
  ["RequiredPositionsFormat"]=("Erforderliche Positionen: {0}","Required positions: {0}"),
  ["ConditionMismatch"]=("Die Bedingung passt nicht zu dieser Preset-Zeile.","This condition does not match this preset row."),
  ["FertilityUnavailable"]=("Eine gespeicherte Fertility ist für dieses Profil nicht verfügbar.","A saved fertility is not available for this profile."),

  // SeedPreviewWindow
  ["PreviewTitleFormat"]=("Seed {0} – Weltvorschau","Seed {0} – World preview"),
  ["PreviewSubtitleDlcFormat"]=("{0} / {1} · Echte Inselpositionen und Drehungen; der PoA-Zusatzbereich in Latium ist eingezeichnet. Bewege die Maus über eine Insel für Details.","{0} / {1} · Real island positions and rotations; the PoA extension area in Latium is outlined. Hover over an island for details."),
  ["PreviewSubtitleNoDlcFormat"]=("{0} / {1} · Ohne Prophecies of Ash · Echte Inselpositionen und Drehungen. Bewege die Maus über eine Insel für Details.","{0} / {1} · Without Prophecies of Ash · Real island positions and rotations. Hover over an island for details."),
  ["SwampLabel"]=("Sumpf","Swamp"),

  // PositionPickerWindow
  ["PositionPickerTitleFormat"]=("{0} · {1} – Positionen","{0} · {1} – Positions"),
  ["Apply"]=("Übernehmen","Apply"),
  ["PoAActive"]=("Prophecies of Ash aktiv","Prophecies of Ash active"),
  ["WithoutPoA"]=("ohne Prophecies of Ash","without Prophecies of Ash"),
  ["PickerHintAnyCombination"]=("Klicke die gewünschten Positionen an. Hell-türkise Außenlinie = wählbar; Gold = gewählt.","Click the desired positions. Light turquoise outline = selectable; gold = selected."),
  ["PickerHintRole"]=("Hell-türkise Außenlinie = für diese Inselrolle wählbar; Gold = gewählt. Nicht passende Inseln sind abgeschwächt.","Light turquoise outline = selectable for this island role; gold = selected. Non-matching islands are dimmed."),
  ["PickerHintCinisDlc"]=("Cinis dient nur der Orientierung. Die zusätzlichen Prophecies-of-Ash-Inseln sind für reguläre Inselbedingungen auswählbar.","Cinis is shown for orientation only. The additional Prophecies of Ash islands are selectable for regular island conditions."),
  ["PickerHintNoDlc"]=("Angezeigt werden ausschließlich die Inselplätze des Latium-Basistemplates.","Only the island slots of the base Latium template are shown."),
  ["PickerHintAlbion"]=("Gold umrandete Inseln werden nach dem Übernehmen als Positionsvorgabe verwendet.","Islands outlined in gold will be used as the position requirement after clicking Apply."),
  ["PositionTooltipFormat"]=("Position {0}","Position {0}"),
  ["SelectedFormat"]=("Gewählt: {0}","Selected: {0}"),
  ["NoPositionRequirementLong"]=("Keine Positionsvorgabe – jede passende Position ist zulässig.","No position requirement – any matching position is allowed."),

  // FertilityDefinitions
  ["SlotsRangeAnyOrderFormat"]=("Slots {0} · Reihenfolge egal","Slots {0} · order doesn't matter"),
  ["AnySlotsAnyOrder"]=("Beliebige Slots · Reihenfolge egal","Any slots · order doesn't matter"),

  // FinderSettingsPreset / SeedSearcher validation
  ["PresetNoSettings"]=("Die Preset-Datei enthält keine Einstellungen.","The preset file does not contain any settings."),
  ["UnsupportedPresetVersionFormat"]=("Nicht unterstützte Preset-Version: {0}.","Unsupported preset version: {0}."),
  ["InvalidStartMode"]=("Ungültige Startart im Preset.","Invalid start mode in the preset."),
  ["LabelGoldRiverSites"]=("Gold-Flussplätze","Gold river sites"),
  ["LabelSturgeonRiverSites"]=("Sturgeon-Flussplätze","Sturgeon river sites"),
  ["LabelLatiumMountainSites"]=("Latium-Bergbauplätze","Latium mountain sites"),
  ["LabelLatiumRiverSites"]=("Latium-Flussbauplätze","Latium river sites"),
  ["LabelAlbionMountainSites"]=("Albion-Bergbauplätze","Albion mountain sites"),
  ["LabelGoldMines"]=("Gold-Minen","Gold mines"),
  ["LabelMarbleMines"]=("Rohmarmor-Steinbrüche","Raw Marble mines"),
  ["LabelMineralMines"]=("Mineralien-Minen","Mineral mines"),
  ["LabelCopperMines"]=("Kupfer-Minen","Copper mines"),
  ["LabelSilverMines"]=("Silber-Minen","Silver mines"),
  ["LabelTinMines"]=("Zinn-Minen","Tin mines"),
  ["LabelLatiumArea"]=("Latium-Fläche","Latium area"),
  ["LabelAlbionArea"]=("Albion-Fläche","Albion area"),
  ["LabelAlbionSwampArea"]=("Albion-Sumpffläche","Albion swamp area"),
  ["InvalidCinisSlot1"]=("Ungültige Cinis-Fertility für Slot 1.","Invalid Cinis fertility for slot 1."),
  ["InvalidCinisPresetCount"]=("Das Cinis-Preset darf bis zu vier unterschiedliche Fertilities für Slots 4–7 enthalten.","The Cinis preset may contain up to four different fertilities for slots 4–7."),
  ["InvalidMinimumCountFormat"]=("Ungültige Mindestanzahl für {0} / {1}.","Invalid minimum count for {0} / {1}."),
  ["InvalidFertilityGroupsFormat"]=("Ungültige Fertility-Gruppen für {0} / {1}.","Invalid fertility groups for {0} / {1}."),
  ["InvalidFertilityCountFormat"]=("Ungültige Fertility-Anzahl für {0} / {1}.","Invalid fertility count for {0} / {1}."),
  ["InvalidFertilityInPresetFormat"]=("Ungültige Fertility im Preset für {0} / {1}.","Invalid fertility in the preset for {0} / {1}."),
  ["InvalidPositionRequirementFormat"]=("Ungültige Positionsvorgabe für {0} / {1}.","Invalid position requirement for {0} / {1}."),
  ["InvalidMinimumFormat"]=("Ungültige Mindestzahl für {0}: Erlaubt sind * oder 1 bis {1}.","Invalid minimum for {0}: allowed are * or 1 to {1}."),
  ["InvalidAreaMinimumFormat"]=("Ungültiger Mindestwert für {0}: Erlaubt sind {1}k bis {2}k.","Invalid minimum for {0}: allowed are {1}k to {2}k."),
  ["FirstSeedMustBeAtLeastFormat"]=("Der erste Seed muss mindestens {0} sein.","The first seed must be at least {0}."),
  ["LastSeedMustBeAtMostFormat"]=("Der letzte Seed darf höchstens {0} sein.","The last seed must be at most {0}."),
  ["LastSeedMustBeGreaterOrEqual"]=("Der letzte Seed muss größer oder gleich dem ersten Seed sein.","The last seed must be greater than or equal to the first seed."),
  ["AtLeastOneThreadRequired"]=("Mindestens ein Thread ist erforderlich.","At least one thread is required."),
  ["InvalidCinisSlot1Choice"]=("Für Cinis Slot 1 sind nur *, Mackerel oder Lavender möglich.","For Cinis slot 1, only *, Mackerel or Lavender are allowed."),
  ["InvalidCinisPoolCount"]=("Für Cinis können bis zu vier unterschiedliche Fruchtbarkeiten für Slots 4–7 gewählt werden.","For Cinis, up to four different fertilities can be chosen for slots 4–7."),
  ["OutputFileRequired"]=("Bitte eine Ausgabedatei wählen.","Please select an output file."),
  ["MinimumsNotNegative"]=("Mindestwerte dürfen nicht negativ sein.","Minimum values must not be negative."),
  ["InvalidPositionForProfile"]=("Ungültige Positionsvorgabe für das gewählte Kartenprofil.","Invalid position requirement for the selected map profile."),
  ["StrategyLatiumOnly"]=("nur Latium","Latium only"),
  ["StrategyLatiumFirst"]=("Latium → Albion","Latium → Albion"),
  ["StrategyAlbionFirst"]=("Albion → Latium","Albion → Latium"),
 };
}
