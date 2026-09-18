# Anno 117 – Offline Seed Finder

Das Werkzeug berechnet Karten und Fruchtbarkeiten selbstständig. Es liest keine Savegames und startet oder steuert Anno 117 nicht.

Unterstützte Rahmenbedingungen:

- alle drei Vorkommen: Im Überfluss / Regulär / Karg
- Start mit Flaggschiff oder Startinsel
- Prophecies of Ash (DLC01/PoA) aktiv oder deaktiviert
- Generatorstand: Anno 117 v2.0 / Save-Dateiversion 70

Wählbare Kartenprofile:

- Kartenskript: Archipelago, Atoll, Rift, Corners oder Island Chains
- Kartengröße: Small, Medium oder Large

Wählbare Fertility-Einstellung: Abundant, Regular oder Sparse. Die FertilitySet-Definitionen für Regular und Sparse stammen direkt aus den Spieldaten (`assets.xml`, extrahiert via RDAExplorer/FileDBReader) und sind für Latium und Albion gegen alle 45 (Kartenskript × Größe × Einstellung)-Kombinationen eines echten, modfreien v2.0-Spielstands (Seed 2) vollständig validiert - 45/45 exakt auf beiden Seiten.

## Start

`dist/Anno117SeedFinder/Anno117SeedFinder.exe` doppelklicken. Das Werkzeug ist eine native WPF-Anwendung und öffnet kein Konsolenfenster.

In der Oberfläche können eingestellt werden:

- erster und letzter zu prüfender Seed im vom Spiel unterstützten Bereich 1–999.999.999; `min` und `max` setzen die jeweilige Grenze direkt
- Startmodus Flaggschiff oder Startinsel
- DLC01 – PoA ein- oder ausschalten; ohne PoA wird der nicht verwendete Cinis-Bereich ausgeblendet
- DLC03 – DotD ist bereits sichtbar, bleibt bis zu belastbaren Forschungsdaten aber gesperrt
- Kartenskript und Kartengröße
- verwendete Threads
- maximale Trefferzahl (`0` bedeutet alle)
- Ausgabedatei
- Cinis Slot 1: `*` (egal, Standard), Mackerel oder Lavender
- vier unterschiedliche Fruchtbarkeiten für Cinis Slots 4–7
- optional ausschließlich Cinis mit 19 Berg- und 23 Flussbauplätzen
- Mindestzahlen für nutzbare Gold- und Stör-Flussbauplätze in Latium sowie für alle Berg- und Flussbauplätze in Latium und alle Bergbauplätze in Albion
- Mindestflächen für Latium, Albion insgesamt und den entwässerbaren Albion-Sumpfanteil
- beliebig viele Latium- und Albion-Regeln für Starter-, Secondary- und Tertiary-Inseln
- profilabhängige Positionsvorgaben und eine Vorschau für frei eingegebene oder gefundene Seeds

Jeder dieser fünf Platzsummenfilter besitzt eine eigene Aktivierungs-Checkbox. Nur angehakte Werte werden bei der Suche berücksichtigt; die Dropdowns enthalten ausschließlich Zahlen und reichen jeweils vom kleinsten bis zum größten Wert der profilabhängigen 100k-Auswertung. Daneben stehen Bereich, Durchschnitt und Median; ein eigener `Median`-Button übernimmt den Median direkt in das jeweilige Dropdown. Beim Wechsel des Kartenprofils werden Auswahl und Statistik passend aktualisiert. Alte Presets, in denen `*` den Filter deaktiviert hat, bleiben kompatibel.

Die drei Flächenfilter verwenden profilabhängige Slider in Schritten von 1.000 Kacheln, dargestellt als `k`. Minimum, Maximum, Durchschnitt und Median sind jeweils auf volle Tausender abgerundete Werte der 100k-Auswertung. Der Button `Median` setzt den zugehörigen Slider direkt auf diesen Wert. Nach dem Anklicken lässt sich ein Slider mit Pfeil links/rechts sowie Plus/Minus um jeweils 1k verändern. Auch diese Filter wirken nur mit gesetzter Checkbox.

Die Ergebnistabelle zeigt Goldminen, Gold-Flussbauplätze und Sturgeon-Flussbauplätze in getrennten Spalten. Ressourcen und Bauplatzarten werden in diesen Spaltenköpfen durch kombinierte Symbole dargestellt; die Bauplatzsymbole erhalten für guten Kontrast eine dunkelgraue Darstellung. Außerdem werden die Gesamtzahlen der Latium-Berg- und Flussbauplätze sowie der Albion-Bergbauplätze angezeigt. Die Tabelle zeigt zusätzlich die Gesamtfläche für Latium und Albion. Bei Albion zählen entwässerbare Sumpfkacheln zur Gesamtfläche; ihr Anteil steht in Klammern. Die Flächen sind mit `≈` gekennzeichnet: Die Werte der Grundinseln basieren auf im 90°-Raster vermessenen bebaubaren Kacheln ohne Berg- und Flussslots. Cinis und die fünf DLC-Inseln werden aus ihren Build-Area-Masken größenklassenspezifisch angenähert. Aktive Slots reduzieren die tatsächlich nutzbare Fläche eines konkreten Seeds geringfügig. Cinis-Fruchtbarkeiten stehen als Symbolreihe dauerhaft ganz rechts. In der Vorschau stehen die Bauplatzzahlen je Insel im Tooltip.

Referenz für Messmethode und Grundinselwerte: [Anno Companion – Island Atlas](https://anno-companion.com/117/tools/island-atlas/).

`Suche starten` führt die Berechnung aus. Treffer werden in der Tabelle angezeigt und zugleich als reine Seednummern zeilenweise in der gewählten TXT-Datei gespeichert. Eine laufende Suche kann abgebrochen werden.

## Presets speichern und laden

Über `Preset speichern …` im Kartenprofil-Bereich lässt sich der vollständige aktuelle Suchstand als `*.anno117settings.json` sichern. Gespeichert werden Kartenskript, Größe, Seedbereich, Threads, Trefferlimit, Ausgabedatei, sämtliche Platzsummenfilter samt Aktivierungsstatus, Cinis-Einstellungen, Vorschau-Seed sowie sämtliche Latium- und Albion-Bedingungen einschließlich gewählter Positionen. `Preset laden …` stellt diesen Stand wieder her. Die Preset-Datei kann beliebig abgelegt, kopiert und mit anderen Rechnern geteilt werden.

Bei der Einstellung „Vorkommen im Überfluss“ besitzt Cinis 17–19 Bergbauplätze und 21–23 Flussbauplätze.

Bei Cinis können höchstens vier der sechs möglichen Fertilities gleichzeitig gewählt werden. Eine fünfte Auswahl wird von der UI zurückgenommen und mit einer roten Meldung erklärt.

Unter „Weitere Inselbedingungen“ lassen sich beliebig viele Zeilen ergänzen. Jede Zeile definiert das Fertility Set, eine Mindestanzahl passender Inseln und optional gewünschte Fertilities. `*` ist der Joker und bedeutet „egal“. Gemeinsame Poolgruppen sind ausdrücklich als „Reihenfolge egal“ beschriftet; beispielsweise findet eine Tertiary-Regel mit Oysters und Sturgeon beide internen Reihenfolgen in Slots 3–4. Unmögliche Doppelauswahlen innerhalb einer solchen Gruppe werden verhindert.

Die vollständigen Abundant-Pools für Latium und Albion sind in der Oberfläche hinterlegt und werden vom Offline-Generator berechnet. Inselanzahl, mögliche Positionen sowie die Grenzen für Plus-Buttons und kumulierte Mindestanzahlen werden aus dem gewählten Profil abgeleitet. Beim Wechsel von Kartenskript oder Größe werden ausschließlich die davon abhängigen Inselbedingungen und alten Ergebniszeilen zurückgesetzt; Suchbereich und Cinis bleiben erhalten.

## Validierung

Seed 2827 wird vollständig und exakt rekonstruiert:

```text
Mackerel | Marble | Iron | Gold | Sturgeon | Oysters | Grapes
```

Der erweiterte Test reproduziert alle 20 unabhängigen Latium-v2.0-Referenzkarten exakt, einschließlich Cinis-Berg- und Flussbauplatzzahlen. Zusätzlich stimmen für diese Referenzen sämtliche Dekorationsinseln in Auswahl, Position und Rotation. Seed 2841 ist vollständig ausgeschlossen, weil er mit einer älteren Generator-/Save-Version erstellt wurde.

Albion ist separat gegen fünf frisch erstellte v2.0-Saves (2827, 2831, 2835, 2848 und 2855) validiert. Für alle 80 Inselrecords stimmen Inselauswahl, Starter/Secondary/Tertiary-Zuordnung und sämtliche 480 Fertility-Slots exakt. Der Test deckt außerdem die Albion-Sumpf-Zulässigkeit sowie eine UI-nahe Tertiary-Bedingung ab. Der integrierte Selbsttest umfasst damit 25/25 Referenzfälle.

Die Profilerweiterung ist gegen 15 Latium-Referenzkarten geprüft: Archipelago Large mit 2.500, 5.000, 6.153, 9.999, 999.999, 3.886.198 und 99.999.999; Atoll, Rift und Island Chains jeweils mit 2.500 und 5.000; außerdem Corners Medium/Small mit 2.500. Alle Inselrecords und Fertilities stimmen exakt. Für Seed 6.153 sind zusätzlich alle 23 Latium- und 16 Albion-Inselassets ihren korrekten Kartenslots zugeordnet, sämtliche Albion-Fertilities bestätigt und die Cinis-Bauplätze mit 19 Berg und 23 Fluss geprüft. Die seedabhängigen ein bis zwei Attraktionsschritte von Archipelago sind dabei ebenso nachgebildet wie die größere Albion-Mindestbreite dieses Profils.

Albion Archipelago Large stimmt für 9.999, 999.999 und 99.999.999 vollständig; frühere Referenzen für Archipelago Large und Corners Medium/Small mit Seed 2.500 bleiben ebenfalls exakt. Corners Large 1.531.943 ist zusätzlich vollständig gegen getrennte Latium- und Albion-Spielstände validiert. In Latium stimmen 23/23 Inselrecords und Bauplatzdatensätze, 22/22 Inselmodelle, Rotationen und Slotzuordnungen sowie sämtliche Fruchtbarkeiten; die Karte besitzt insgesamt 144 Berg- und 154 Flussbauplätze. In Albion stimmen 16/16 Inselrecords, Bauplatzdatensätze, Modelle, Rotationen, Slotzuordnungen und sämtliche Fruchtbarkeiten; dort sind es 112 Berg- und 65 Sumpfbauplätze. Der integrierte Selbsttest umfasst einschließlich dieser gemeinsamen Regression weiterhin 28/28 Referenzfälle. Ein separater Gesamttest erzeugt Latium und Albion für alle 15 Kombinationen aus Kartenskript und Größe. Neue Referenz-Saves werden in `.research/validation_saves/<Patch-Build>/...` nach Spielversion abgelegt; die zugehörigen kompakten Prüfdaten stehen unter `data/regression/<Patch-Build>/`. Nicht-Corners-Profile sind damit deutlich breiter gegen reale Saves geprüft, aber naturgemäß nicht für jeden der möglichen Seeds formal bewiesen.

Der Referenztest prüft zusätzlich die Anzahl der Tertiary-Inseln mit der reihenfolgeunabhängigen Kombination Oysters + Sturgeon in Slots 3–4. Ein Such-Smoke-Test mit mindestens zwei solchen Inseln liefert unter Seeds 1–1.000 exakt Seed 290.

Das Savepaket vom 12.09.2026 ergänzt 86 gültige Referenz-Saves für alle fünf Kartentemplates und alle drei Größen. Sämtliche Saves wurden als Dateiversion 70, PoA aktiv und Flaggschiff-Start verifiziert und nach ihren internen Settings einsortiert; acht zustandsabhängig fehlerhafte Hot-Reload-Saves wurden vollständig ausgeschlossen. Von den 86 Saves sind 49 Abundant-Saves direkt berechenbar und stimmen für Latium **49/49** sowie Albion **49/49** exakt.

Ein Testlauf über Seeds 1–10.000 benötigt auf dem Entwicklungssystem mit 8 Threads etwa 0,9 Sekunden und liefert für die obige Cinis-Bedingung 443 Treffer; Seed 2827 ist enthalten.

## Zusammenführung mit der parallel entwickelten Fruchtbarkeits-Erweiterung (12.09.2026)

Eine parallel an dieser Codebasis entwickelte Erweiterung hatte unabhängig Regular/Sparse-Unterstützung sowie eine tiefgehende Untersuchung der Island-Chains- und Rift/Small-Generierung erarbeitet, bevor diese Version verfügbar war. Direkter Abgleich beider Stände gegen einen gemeinsamen Satz modfreier Referenz-Spielstände (45 Latium- und 45 Albion-Kombinationen bei Seed 2, sowie mehrere Multi-Seed-Sätze für Island Chains, Rift/Small und Corners/Small) ergab:

- **Diese Version war bei Island Chains und Rift/Small bereits weiter**, auf beiden Regionen: Island Chains erreicht 23 von 24 realen Mehrfach-Seed-Referenzen exakt (Latium *und* Albion), die einzige Abweichung ist ein einzelner Spielstand mit nachweislich abweichender Rolle-Zuordnung - Signatur des Community Bug Fix Mods, kein Generatorfehler. Rift/Small - zuvor über viele Iterationen ungelöst - erreicht 8 von 8 exakt.
- **Corners/Small bleibt in beiden Ständen ungelöst** und schlägt bei exakt denselben Seeds fehl (2/10 der zusätzlich bereitgestellten Referenzen) - kein Rückschritt, aber auch keine neue Erkenntnis auf dieser Seite.
- Die Regular/Sparse-Fruchtbarkeitsstufen (`ResolveSet`/`SetVariants`/`SwapProfiles`-Mechanik samt der zugehörigen FertilitySet-Pooldaten aus `assets.xml`) existierten nur im älteren Stand und wurden hier nachgezogen - validiert gegen alle 45 (Kartenskript × Größe × Einstellung)-Kombinationen für Latium und Albion, 45/45 exakt auf beiden Seiten.
- Ein bekannter, aber in keinem der bisher geprüften Fälle wirksam gewordener Mangel bleibt offen: `MapProfileData.g.cs` enthält für Atoll/Small, Island Chains/Large und alle drei Rift-Größen Latium-Slotkoordinaten aus einer älteren, unkorrigierten `.a7tinfo`-Extraktion (Abweichungen um Vielfache von 8 Einheiten). Das hat in keinem der oben genannten Tests zu einem falschen Ergebnis geführt, ist aber nicht behoben.

Praktisch bedeutet das: Dieser Stand basiert jetzt auf der weiterentwickelten Island-Chains-/Rift-Generatorlogik dieser Version, ergänzt um die Regular/Sparse-Unterstützung der älteren Codebasis.
