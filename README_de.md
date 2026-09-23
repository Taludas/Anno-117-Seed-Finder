# Anno 117 – Seed Finder

Finde die Karte, die du willst - **bevor** du ein neues Spiel startest.

Wenn du in Anno 117: Pax Romana ein neues Spiel beginnst, gibst du eine Seed-Zahl ein, wählst ein Kartentemplate, eine Größe und ein paar weitere Optionen - und das Spiel baut daraus eine Welt. Was dabei herauskommt, stammt vom sogenannten „World Generator", der von einigen für den Spieler sichtbaren Vorgaben wie den Kartentemplates gesteuert wird, daneben aber hauptsächlich von einem Zufallsgenerator: welche Inseln in ihren vorgesehenen Plätzen erscheinen, wie sie gedreht sind, welche Fruchtbarkeiten auf ihnen vorhanden sind und wie viele Berg- und Flussplätze sie haben.

Diese App baut diesen „World Generator" nach, **ohne das Spiel zu nutzen**. Es kann in wenigen Minuten Millionen von Seeds durchgehen und dir sagen, welche davon die Karte ergeben, die du suchst - „eine Startinsel mit Trauben und Gold-Flussbauplatz", „so viele bebaubare Kacheln wie möglich", „mindestens hundert Bergbauplätze in Albion" und so weiter.

Der "World Generator" ist aus echten Spielständen und aus den Dateien des Spiels rekonstruiert und gegen 90 Spielstände geprüft, die jede Kombination von Einstellungen abdecken. In diesen Karten gibt es **jede Insel, jede Position, jede Drehung, jede Fruchtbarkeit, jeden Bauplatz und jede Dekoinsel exakt** wieder - 11.844 Einzelprüfungen ohne eine einzige Abweichung.

---

## Inhalt

- [Was ein Seed ist](#was-ein-seed-ist)
- [Installieren und starten](#installieren-und-starten)
- [Die App benutzen](#die-app-benutzen)
- [Wie Anno 117 eine Karte baut](#wie-anno-117-eine-karte-baut)
- [Wie genau ist die App?](#wie-genau-ist-das)
- [Bekannte Grenzen](#bekannte-grenzen)
- [Selbst bauen](#selbst-bauen)
- [Credits](#credits)

---

## Was ein Seed ist

Ein Seed ist einfach eine Zahl. Das Spiel startet damit eine lange Kette von „Zufallszahlen". Die Kette ist nicht wirklich zufällig: derselbe Seed erzeugt immer genau dieselbe Kette und damit genau dieselbe Karte. Erst das macht einen Seed-Finder überhaupt möglich - wer die Regeln kennt, kann die Karte aus der Zahl berechnen, ohne zu spielen.

Ändere irgendetwas anderes - Kartenform, Größe, DLC, die beiden Kartenoptionen - und derselbe Seed ergibt eine völlig andere Welt, weil die Regeln die Zahlenkette anders verbrauchen. Deshalb reagiert diese App sehr empfindlich auf Spiel-Updates. Besonders die Fruchtbarkeitsverteilung hängt stark davon ab, dass sich irgendwo in der Kette nichts Kleines ändert.

---

## Installieren und starten

**Was du brauchst**

- Windows
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (die „Desktop"-Variante, nicht nur die
  Konsolenversion)

**Starten**

`Anno117SeedFinder.exe` ausführen. Es gibt kein Installationsprogramm, und außerhalb des Ordners wird nichts geschrieben. Die App braucht weder das Spiel noch dessen Dateien noch eine Internetverbindung.

Die Oberfläche gibt es auf **Deutsch und Englisch**; oben rechts umschaltbar. Die Wahl wird in den Voreinstellungen gespeichert.

---

## Die App benutzen

Das Fenster besteht aus drei Teilen: dem **Kartenprofil** oben, den **Suchbedingungen** in der Mitte und den **Ergebnissen** unten.

### 1. Das Kartenprofil - beschreibe das Spiel, das du starten wirst

Diese Einstellungen müssen zu dem passen, was du später im Menü „Neues Spiel" wählst. Passen sie nicht, erzeugen die gefundenen Seeds eine andere Karte.

| Einstellung | Bedeutung |
|---|---|
| **Kartentemplate** | Die Form der Welt: Archipel, Atoll, Ecken, Inselketten oder Graben. Jede verteilt die Inseln nach einem völlig anderen, eigenständigen Muster. |
| **Kartengröße** | Klein, mittel oder groß. Größere Karten haben mehr Inseln und mehr Platz dazwischen. |
| **DLC01 – PoA** | Ob das DLC *Verheißung des Vulkans* aktiv ist. Damit wird Latium um die Kontinentalinsel Cinis erweitert und bekommt mehrere zusätzliche Inseln in der nördlichen Ecke der Karte. |
| **nachträglich aktiviert (experimentell)** | Für Karten, die **ohne** DLC01 erstellt und bei denen es danach eingeschaltet wurde. Siehe [DLC nachträglich einschalten](#dlc-nachträglich-einschalten). |
| **DLC03 – DotD** | Ausgegraut. Für das kommende DLC03 *Erwachen des Deltas* vorgesehen, hat derzeit keine Wirkung. |
| **Startart** | Flaggschiff oder Startinsel. Das ist die Wahl des Spiels, wie du ankommst; sie ändert die erzeugte Karte **nicht**. Geprüft mit eingeschalteter Startinsel-Option, mit erzwungener kontinentaler Insel als Start und mit DLC01 an und aus (Archipel und Ecken, groß): alle sechs Spielstände sind mit den normalen identisch. |

### 2. Weitere Kartenoptionen

Das Spiel bietet beim Erstellen einer Karte noch ein paar weitere Wahlmöglichkeiten. Zwei davon ändern, was dieses Tool berechnet:

| Option | Wirkung |
|---|---|
| **Rohstoffvorkommen** (reichlich / normal / spärlich) | Wie viele Berg- und Flussbauplätze die Inseln bekommen. |
| **Fruchtbarkeit** (reichlich / normal / spärlich) | Wie viele Fruchtbarkeiten auf jeder Insel gestzt werden. |

Eine dritte Option, **Waldgröße** (wie groß die Waldflächen auf einer Insel sind), wurde ausgemessen und hat *keine Wirkung** auf Inseln, Positionen, Fruchtbarkeit oder Bauplätze - sie wird hier deshalb bewusst nicht angeboten.

### 3. Wonach gesucht wird - die Bedingungen

Alles in der Fenstermitte ist freiwillig. Lässt du es leer, passt jeder Seed.

**Inselbedingungen.** Für jede Region (Latium und Albion) kannst du bestimmte Eigenschaften auf bestimmten Inselarten verlangen:

- **Startinsel** – die Insel, auf der du tatsächlich beginnst
- **Sekundär** und **Tertiär** – die beiden Gruppen, in die die übrigen Inseln aufgeteilt werden (erklärt
  [weiter unten](#schritt-9-welche-fruchtbarkeiten-wo-entstehen))
- **Beliebige Kombination** – die Fruchtbarkeit muss in der Region einfach irgendwo vorkommen

Mit den „Hinzufügen"-Schaltflächen legst du eine Bedingungszeile an und wählst dann die Fruchtbarkeitsverteilung für die Insel. Du kannst beliebig viele Zeilen anlegen; ein Seed muss alle erfüllen. Über die Positionsauswahl neben einer Zeile kannst du die Insel auf einem **bestimmten Inselplatz** der Karte verlangen, wenn dir die Lage wichtig ist.

**Cinis (nur mit DLC01).** Die Vulkaninsel hat einen eigenen Bereich: du kannst dort bestimmte Fruchtbarkeiten fordern, eine bestimmte im ersten Feld verlangen oder die maximale Zahl an Bauplätzen auf der Insel.

**Mindestgrößen.** Drei Regler verlangen eine Mindestmenge nutzbarer Kacheln: bebaubare Fläche in Latium, in Albion und Sumpffläche in Albion. Die Schaltfläche „Median" daneben trägt den typischen Wert für das aktuelle Kartenprofil ein - so fragst du schnell nach „besser als Durchschnitt" (aus 100.000 Seeds ermittelt).

**Mindestzahl an Bauplätzen.** Eine Reihe von Feldern verlangt eine Mindestzahl an Bergbauplätzen, Flussbauplätzen, Goldflussbauplätzen, Störflussbauplätzen, Goldminen, Rohmarmor-Steinbrüchen und Mineralienminen in Latium sowie Silber-, Zinn- und Kupferminen in Albion. Auch hier gibt es je eine Median-Schaltfläche.

**Erweiterte Fruchtbarkeitsfilter.** Das Kästchen **Erweiterte Fruchtbarkeitsfilter anzeigen** über der Ergebnistabelle blendet sechs weitere Regler ein und fügt der Tabelle ihre Spalten hinzu. Jeder Regler addiert die Hafen- oder Sumpfkacheln aller Inseln der Region, die eine bestimmte Fruchtbarkeit tragen: Hafenkacheln der Inseln mit Purpurschnecken und mit Austern in Latium; Hafenkacheln der Inseln mit Salzkraut und mit Kammmuscheln in Albion; Sumpfkacheln der Inseln mit Kleinen Vögeln und mit Bibern in Albion. Die Regler springen in Schritten zu 500 Kacheln und haben wie die anderen eine Median-Schaltfläche. Die Filter wirken nur, solange das Kästchen angehakt ist; das Kästchen wird in den Presets mitgespeichert.

**Seed-Bewertung.** Der Minimum-Filter kennt nur Alles-oder-nichts; die Seed-Bewertung hingegen lässt dich die durchgekommenen Seeds danach ordnen, wie gut sie zu dem passen, was dir wirklich wichtig ist, statt einen Seed zu verwerfen, der 2.000 Kacheln unter einem Grenzwert liegt, aber sonst hervorragend ist. Hake **Seed-Bewertung aktivieren** an (neben „Nur die Seeds der Ergebnistabelle durchsuchen"), um neben jedem Flächen-, Bauplatz-, Minen- und erweiterten Fruchtbarkeitsfilter, neben den sechs Cinis-Pool-Fruchtbarkeiten, neben der Cinis-Slot-1-Wahl und neben „Nur maximale Bauplätze" je einen Bewertungssregler (0-10) einzublenden - unabhängig davon, ob dasselbe auch als Minimum-Filter aktiv ist. Jeder Seed bekommt dann eine Bewertung aus 10 in einer eigenen Spalte (sortierbar, auch bei Mehrfachsortierung); ein Strich bedeutet, dass keine Bewertung vergeben ist. Die Bewertungen werden gegen dieselbe 100.000-Seed-Statistik normiert wie die Filter, sodass eine 10 auf „meist volle Bandbreite" und eine 10 auf „schmale Bandbreite" gleich zählen. **Bewertungsbereich bei Ausreißern erweitern** (neben „Erweiterte Fruchtbarkeitsfilter anzeigen") dehnt den Bereich einer Kennzahl auf einen ungewöhnlich extremen Treffer *dieser* Suche aus, statt ihn einfach bei 1,0 zu kappen - nützlich bei Kennzahlen mit schmaler 100.000-Seed-Spanne wie Zinnminen. Mauszeiger über eine Bewertung zeigt die Aufschlüsselung je Kennzahl. Bewertungen und beide Schalter werden mit den Presets gespeichert und in die CSV exportiert.

### 4. Eine Suche starten

| Feld | Bedeutung |
|---|---|
| **Erster / letzter Seed** | Der zu durchsuchende Bereich. Die Schaltflächen daneben springen auf den kleinsten und größten vom Spiel akzeptierten Seed. |
| **Threads** | Wie viele Prozessorthreads genutzt werden. Mehr ist schneller; lass dem Rechner einen Thread, wenn du nebenher arbeiten willst. |
| **Maximale Treffer** | Nach so vielen passenden Seeds anhalten. `0` heißt „alle finden". |
| **Ausgabedatei** | Wohin die passenden Seeds geschrieben werden. Der Zeitpunkt, zu dem die Suche startet, wird in den tatsächlichen Dateinamen eingetragen (`treffer.txt` wird zu `treffer_2026-09-23_15-04-05-123.txt`); ein unverändert gelassenes Feld überschreibt so nie das Ergebnis der vorherigen Suche. |
| **Nur die Seeds der Ergebnistabelle durchsuchen** | Lässt die Suche über die Seeds laufen, die gerade in der Tabelle stehen, statt über den Bereich (siehe „Schrittweise filtern“ unten). Nur verfügbar, solange die Tabelle Seeds enthält. |

**Start** führt die Suche aus, **Abbrechen** hält sie vorzeitig an. Treffer erscheinen laufend in der Tabelle.

### 5. Ergebnisse

Die Tabelle listet jeden passenden Seed mit seinen Kennzahlen: bebaubare Kacheln je Region, Sumpffläche, Zahl der Bauplätze und die Fruchtbarkeitsverteilung auf Cinis. Mit den Kästchen über der Tabelle blendest du einzelne Spalten ein und aus, damit sie lesbar bleibt. Der Mauszeiger über einer dieser Zahlen zeigt eine kleine Anzeige: wo der Wert dieses Seeds zwischen dem Minimum und Maximum der 100.000-Seed-Statistik liegt, mit markiertem Median - so bekommt eine nackte Zahl wie „145" Einordnung, ohne den zugehörigen Filter zu öffnen.

- **Vorschau** – Seed eintippen und Vorschau drücken, um beide Regionen als Karte zu sehen: jede Insel genau dort, wo das Spiel sie hinsetzt, und so gedreht, wie das Spiel sie dreht, mit ihrem Draufsicht-Bild, dazu die Dekorationsinseln und die Drittanbieter-Inseln (Händler und Räuber). Beim Darüberfahren erscheinen Fruchtbarkeiten und Bauplätze; die Rahmenfarbe zeigt die Rolle. Der Rand der normalen Karte und der des Prophecies-of-Ash-Bereichs sind als echte Rechtecke eingezeichnet. Das geht für jeden Seed, auch ohne Suche. **Zufall** setzt einen zufälligen gültigen Seed in das Feld.
- **Seed hinzufügen** – einen einzelnen, bestimmten Seed ohne Suche in die Tabelle aufnehmen.
- **Seed-Liste laden** – eine Liste von Seeds aus einer Datei einlesen und alle in der Ergebnistabelle auswerten. Eine Textdatei mit einem Seed pro Zeile (wie die Ausgabedatei `treffer.txt`) und eine von der App exportierte CSV-Datei funktionieren beide; weitere Spalten werden ignoriert. Die Seeds tragen kein Kartenprofil: stelle das Profil ein, mit dem sie gefunden wurden, bevor du sie lädst.
- **CSV exportieren** – die Ergebnistabelle als Tabellendatei schreiben. Der vorgeschlagene Dateiname trägt denselben
  Zeitstempel, sodass ein Klick auf Speichern ohne Umbenennen trotzdem jede Exportdatei behält.

#### Schrittweise filtern

Eine Suche lässt sich in Stufen eingrenzen, auch über mehrere Sitzungen hinweg:

1. Eine Suche laufen lassen (oder mit **Seed-Liste laden** eine frühere `treffer.txt` / CSV-Datei laden). Die Tabelle enthält nun eine Menge Seeds.
2. Die Filter ändern, **Nur die Seeds der Ergebnistabelle durchsuchen** anhaken und **Start** drücken. Es werden nur diese Seeds untersucht, und die Tabelle wird durch die ersetzt, die auch die neuen Filter erfüllen.
3. Beliebig oft mit anderen Filtern wiederholen, zum Beispiel zuerst alle Seeds mit den meisten bebaubaren Kacheln, davon dann die mit den meisten Goldplätzen.

Besteht kein Seed die neuen Filter, zeigt die Tabelle wieder die vorherige Liste und die Ausgabedatei bleibt unverändert; du kannst also die Filter ändern und es erneut versuchen, ohne etwas neu zu laden. Erster und letzter Seed sind bei eingeschalteter Option gesperrt. Die Treffer werden wie gewohnt in die Ausgabedatei geschrieben, unter ihrem eigenen zeitgestempelten Namen (siehe „Ausgabedatei" oben); das rührt also nie die geladene Datei an, auch wenn das Ausgabefeld noch ihren Namen trägt.

### 6. Voreinstellungen

**Speichern** und **Laden** legen alles in einer kleinen Datei ab - Profil, Optionen, sämtliche Bedingungen, den Suchbereich und die Sprache. So kannst du später weitermachen oder eine Suche weitergeben.

---

## Wie Anno 117 eine Karte baut

Das ist der Teil, den das Tool nachbauen musste. Hier in verständlicher Sprache beschrieben; zum Benutzen der App brauchst du nichts davon, aber es erklärt, was die Einstellungen wirklich bewirken.

### Die Würfel

Alles beginnt mit dem Seed. Das Spiel macht daraus einen Satz von **17 Zahlen**: es nimmt den Seed und multipliziert und addiert wiederholt einen festen Betrag, bis alle 17 Plätze nacheinander gefüllt sind.

Von da an läuft jeder „Würfelwurf" gleich: das Spiel nimmt zwei dieser 17 Zahlen, rotiert ihre Bits (die eine um neun, die andere um dreizehn Stellen), addiert sie und schreibt das Ergebnis über eine der beiden. Ein Zeiger rückt weiter, der nächste Wurf nimmt das nächste Paar. So rühren die 17 Zahlen einander ständig durch, und heraus kommt ein Zahlenstrom, der zufällig aussieht, aber vollständig vom Seed bestimmt ist.

Zwei Dinge sind zum Verständnis des Restes wichtig:

1. **Reihenfolge ist alles.** Jeder Schritt der Kartenerzeugung nimmt eine bestimmte Anzahl Würfe aus diesem Strom. Nimmt ein Schritt einen Wurf zu viel oder zu wenig, verschiebt sich alles danach und die Karte ändert sich völlig. Deshalb kann eine einzige falsch platzierte Dekorationsinsel die Fruchtbarkeitsverteilung einer ganzen Karte ruinieren.
2. **Die ersten neun Würfe werden übersprungen.** Das Spiel verbraucht sie vor der Kartenerzeugung anderswo.

### Schritt 1: die Vorlage

Jede Kartenform und -größe ist eine Datei im Spiel, die **Plätze** auflistet - feste Stellen, an die eine Insel kommen kann, jede mit einer Größe (klein, mittel, groß, sehr groß) und einer Markierung, ob es ein *Startplatz* ist. Die Vorlage enthält auch die Stellen der drei neutralen Parteien und die Spieler-Startpunkte.

Mit *Verheißung des Vulkans* nutzt Latium eine größere Vorlage mit zusätzlichen Plätzen und der festen Stelle für Cinis.

### Schritt 2: welche Insel wohin kommt

Das Spiel mischt die Liste der Plätze (Startplätze werden getrennt behandelt und kommen zuletzt) und geht sie dann durch. Für jeden Platz zieht es eine Insel aus dem Vorrat der passenden Größe und entfernt sie, damit dieselbe Insel nicht zweimal vorkommt - bis der Vorrat leer ist; dann wird er neu gefüllt und eine Insel kann ein zweites Mal erscheinen.

### Schritt 3: die Inseln drehen

Jede Insel bekommt eine Vierteldrehung: 0, 90, 180 oder 270 Grad.

- Gewöhnliche Inseln werden zufällig gedreht.
- **Startinseln werden so gedreht, dass ihre Startbucht zur Kartenmitte zeigt.** Das ist überhaupt nicht zufällig: das Spiel misst die Richtung von der Bucht zur Kartenmitte und nimmt die am besten passende Vierteldrehung.

### Schritt 4: die neutralen Parteien

Drei Inseln gehören NPCs: der Pirat und zwei Händler. Sie haben eigene feste Stellen in der Vorlage, und das Spiel würfelt fünf Zahlen für ihre Drehungen und dafür, welcher Händler wohin kommt.

### Schritt 5: die Inseln zusammenschieben

Bei Archipel, Atoll und Rift **schiebt** das Spiel die Inseln anschließend **näher zusammen** (Wiggle). Es geht alle beweglichen Inseln zweimal durch. Für jede mischt es eine Liste möglicher Versatzwerte und probiert sie der Reihe nach; der erste, der die Insel näher an eine Startinsel bringt, ohne irgendwo anzustoßen, wird genommen.

Inselketten hat einen eigenen, einfachen Durchgang. Ecken verschiebt seine Inseln gar nicht.

### Schritt 6: die Karte zuschneiden

- **Mit DLC01** hat die Latium-Karte immer dieselbe feste Größe.
- **Ohne DLC01** wird die Karte eng zugeschnitten: das Spiel misst, wie viel Platz die Inseln tatsächlich einnehmen, rundet auf und zentriert die ganze Anordnung darin. Deshalb sind Karten ohne DLC je nach Seed etwas unterschiedlich groß.

Das Spielfeld - der befahrbare Teil - ist dann die belegte Fläche mit einem gleich breiten Rand auf jeder Seite.

### Schritt 7: die Dekorationsinseln

Zuletzt streut das Spiel kleine, unbesiedelbare Inseln ein: **14 mit DLC, 10 ohne**.

Es legt ein Raster aus 16-Einheiten-Zellen über die Karte, mischt die Zellen in eine zufällige Reihenfolge und geht für jede Dekoration diese Reihenfolge durch, bis eine Zelle passt: weit genug von anderen Inseln, innerhalb des Spielfelds und nicht zu nah an einem Spieler-Startpunkt. Ein weiterer Wurf entscheidet dann, welche der beiden passenden Vierteldrehungen sie bekommt.

Diese Inseln haben keine Fruchtbarkeiten und keine Plätze. Sie sind hier nur wichtig, weil **das Finden einer Zelle einen Würfelwurf verbraucht** - ein Fehler dabei verschiebt alles Folgende.

### Schritt 8: Berg- und Flussbauplätze

Jede Inseldatei führt zwei Arten von Bauplätzen: **feste**, die immer da sind, und **zufällige**, die genutzt werden können oder nicht. Die Datentabellen des Spiels sagen, wie viele Plätze eine Insel einer bestimmten Größe haben soll - mit drei Zeilen für spärlich, normal und reichlich, passend zur gewählten Kartenoption.

Die Regel lautet: nimm die Zahl aus der Tabelle (plus eine kleine zufällige Abweichung), aber nie weniger als die festen Plätze und nie mehr, als die Insel überhaupt hergibt. Deshalb ändern sich manche Inseln gar nicht, wenn du die Einstellung senkst - sie waren schon an ihrer Grenze.

Sumpfplätze in Albion sind immer fest und von der Option nicht betroffen.

### Schritt 9: welche Fruchtbarkeiten wo entstehen

Das ist der Teil, um den es den meisten Suchen geht.

Zuerst gibt das Spiel jeder Insel eine **Rolle**:

- **Starter** – die Startinseln
- **Sekundär** und **Tertiär** – die gewöhnlichen Inseln, abwechselnd (die beiden müssen aber nicht zwingend gleich häufig vorkommen)
- **Kontinental** – nur Cinis

Dafür mischt es eine kurze Liste der Rollen und geht dann die Inseln durch: eine Startinsel nimmt die Starterrolle, jede andere Insel nimmt die der beiden gewöhnlichen Rollen, die in der Liste zuerst steht, und diese Rolle wandert ans Ende - dadurch wechseln sie sich ab. Weil die Liste vorher gemischt wurde, hängt es vom Seed ab, welche zuerst kommt.

Jede Rolle hat dann einen **Fruchtbarkeitssatz**: eine Liste von Fruchtbarkeitsgruppen, etwa „eines der beiden Getreide, eines der beiden Erze, dann vier aus dem großen Vorrat". Für jeden Eintrag zieht das Spiel eine Fruchtbarkeit aus dieser Gruppe, ohne eine zu wiederholen, die die Insel schon hat.

Die **Fruchtbarkeitsoption** ändert die Fruchtbarkeitssätze, nicht die Rollen: auf den niedrigeren Stufen bekommt jede Rolle eine kürzere Liste, die Inseln tragen also weniger Fruchtbarkeiten. Welche Insel Starter, Sekundär oder Tertiär ist, bleibt genau gleich.

### DLC nachträglich einschalten

Das Spiel erlaubt es, *Verheißung des Vulkans* auf einer ohne DLC erstellten Karte zu aktivieren. Es baut die Welt dabei nicht neu:

- die bestehende Karte bleibt exakt wie sie war, samt ihrer eng zugeschnittenen Größe;
- die Karte wird im Norden an zwei Seiten erweitert, um Platz zu schaffen;
- ein **neuer Würfeldurchgang mit demselben Seed** setzt nur die neuen Inseln - die DLC-Inseln und Cinis - auf die Plätze, die es nun in der DLC-Vorlage gibt, und wählt dabei aus den noch nicht benutzten Inseln;
- diese neuen Inseln bekommen anschließend wie üblich ihre Plätze und Fruchtbarkeiten.

Die App unterstützt das mit dem Kästchen **„nachträglich aktiviert (experimentell)"**. Das „experimentell" hat einen Grund - siehe Grenzen weiter unten.

---

## Wie genau ist das alles?

Jede Regel wurde aus echten Spielständen und aus den Insel- und Vorlagendateien des Spiels abgeleitet und dann gegen einen Prüfsatz von **90 Spielständen** geprüft, der alle fünf Vorlagen, alle drei Größen, beide DLC-Zustände und alle drei Fruchtbarkeitsstufen abdeckt.

| Verglichen wurde | Latium | Albion |
|---|---|---|
| Inseltyp, Position und Drehung | 1818 / 1818 | 1440 / 1440 |
| Fruchtbarkeit | 1863 / 1863 | 1440 / 1440 |
| Zahl der Bauplätze | 1863 / 1863 | 1440 / 1440 |
| Dekorationsinseln | 1080 / 1080 | 900 / 900 |

**11.844 Prüfungen, keine Abweichung.** Zusätzlich dient ein größeres Archiv mehrerer hundert älterer Spielstände nach
jeder Änderung als Regressionstest, und die drei Bauplatz-Stufen sowie das nachträgliche DLC wurden jeweils gegen eigene
Spielstände geprüft.

---

## Bekannte Grenzen

- **„DLC nachträglich aktiviert" ist experimentell.** Die zusätzlichen Würfe, die das Spiel vor dem Setzen der neuen
  Inseln macht, sind nur für die Kartengrößen bekannt, die in unseren Spielständen vorkommen. Bei einer ungetesteten
  Größe greift eine Formel, die für die meisten Werte stimmt, für manche aber nicht - dann können die
  **Fruchtbarkeiten auf den neu hinzugekommenen Inseln** falsch sein. Der alte Teil der Karte stimmt immer. Mit den
  Stufen normal und spärlich wurde es außerdem noch nicht geprüft.
- **DLC03** ist nicht umgesetzt; das Kästchen ist inaktiv.

---

## Selbst bauen

Die App ist ein einzelnes C#-Projekt. Mit installiertem
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```
dotnet build src/Anno117SeedFinder/Anno117SeedFinder.csproj -c Debug
```

Die ausführbare Datei liegt danach in `src/Anno117SeedFinder/bin/Debug/net10.0-windows/`. Für ein Paket, das du weitergeben
kannst:

```
dotnet publish src/Anno117SeedFinder/Anno117SeedFinder.csproj -c Release --self-contained false -o publish
```

Das Ergebnis braucht auf dem Rechner, auf dem es läuft, die
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64).

Fünf Selbsttests sind eingebaut und der schnellste Weg, einen Build zu prüfen. Jeder liefert bei Erfolg den Exit-Code 0:

```
Anno117SeedFinder.exe --self-test                # Generator gegen hinterlegte Referenzergebnisse
Anno117SeedFinder.exe --smoke-test               # Suche, Filter und Ergebnistabelle
Anno117SeedFinder.exe --all-profile-smoke-test   # jedes Kartenprofil erzeugt fehlerfrei
Anno117SeedFinder.exe --preview-smoke-test       # das Vorschaufenster baut sich auf, die Tooltips öffnen sich
Anno117SeedFinder.exe --settings-smoke-test      # Voreinstellungen laden und speichern über das Fenster
```

Der Ordner `tools` enthält die Skripte, die den Generator mit deinen eigenen Spielständen vergleichen (`tools/README.md`); zum Benutzen oder Bauen der App werden sie nicht gebraucht, und es sind weder Spielstände noch Spieldateien enthalten. `FALLSTRICKE.md` beschreibt die Fallstricke auf dem Weg dorthin, `OFFENE-PROBLEME.md` listet, was noch offen ist.

---

## Credits

Diese App begann ursprünglich als Projekt von **Drullo321**. Er hat rekonstruiert, wie der World Generator von Anno 117 tatsächlich arbeitet - die Zufallskette hinter den Inselplätzen, Drehungen, Fruchtbarkeiten und Bauplätzen, die in diesem Dokument beschrieben sind, und die erste funktionierende Version davon gebaut: die anfängliche Nutzeroberfläche sowie den ersten Generator für das Kartentemplate Ecken und, darauf aufbauend, für die Templates Archipel und Inselkette. Dieses Reverse Engineering ist das Fundament, auf dem alles andere hier aufgebaut ist.

Danach hat er das Projekt wegen diverser Gründe zur Fertigstellung an mich übergeben. Seitdem wurde es erweitert, um es gegen jedes Kartentemplate, jede Größe und jede Spieleinstellung zu validieren, nicht nur die oben genannten Templates; wurde auf die 2.1-Kartentemplates aktualisiert; wurde erweitert, um DLC01 korrekt zu behandeln, sowohl wenn es von Anfang an deaktiviert ist als auch wenn es nachträglich mitten im Spiel aktiviert wird; und um die danach hinzugekommenen Funktionen ergänzt - die erweiterten Fruchtbarkeitsfilter, die Seed-Bewertungsfunktion und die Nutzerbberflächen rund um die beiden.