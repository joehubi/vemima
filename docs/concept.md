# Vemima – Konzept

> Entwurf. Punkte mit `TODO` sind noch offen. Dieses Dokument ist die zentrale Spezifikation des Projekts.

## 1. Ziel
Verwaltung des vermieteten Objekts **Riesenhaus** (1 Wohnpartei) in einer ersten Ausbaustufe. Auf Grundlage dieser Software sollen später weitere Objekte und kompliziertere Fälle ergänzt werden. Das Datenmodell führt Objekte deshalb als eigene Entität, in dieser Stufe gibt es aber nur Riesenhaus.

**Kernnutzen (Verbesserung gegenüber heute):** Heute werden Rechnungen von Hand in eine Tabelle (Excel) eingetragen und nach Kostenart getrennt. Künftig fotografierst du eine neue Rechnung mit dem Handy und überträgst sie über die Browser-Maske, oder du lädst eine per E-Mail erhaltene PDF direkt am PC hoch. Vemima erkennt automatisch Rechnungssteller, Kostenart, Rechnungsdatum und Rechnungsbetrag und trägt die Rechnung tabellarisch ein. Beispiel: Rechnung vom Schornsteinfeger → Kostenart Heizung (Abschnitt 5.0).

Erfasst werden:
- **Rechnungserkennung** per Handyfoto oder PDF (Kernfunktion, Abschnitt 5.0).
- **Dokumentenablage**: Rechnungen und Dokumente jederzeit verfügbar; schnelle Übertragung von Fotos/Scans vom Handy auf den PC bzw. in die Datenbank.
- **Mietzahlungen und Mietschulden**.
- **Nebenkostenabrechnung**.
- **Steuer-Auswertung** (Einnahmen, Ausgaben, Renovierungen).

Die Anwendung ist eine Eigenentwicklung für den privaten Gebrauch. Es gibt keine Veröffentlichung in einer Cloud oder im Internet.

## 2. Architektur
```
Smartphone (Chrome) --WLAN--> Windows-PC (Vemima, ASP.NET Core / Kestrel) --SMB--> WD My Cloud EX2 Ultra (Dokumente, Datenbank-Backup)
Windows-PC (Vemima-GUI, Avalonia) --> Datenbank (SQLite-Datei auf dem PC)
```
- Läuft auf dem Windows-PC als Desktop-Anwendung mit GUI. Kein Dienst, kein Autostart. Folge: Der Upload vom Smartphone funktioniert nur, solange Vemima auf dem PC gestartet ist.
- Technologie: C#, .NET 10, Avalonia UI (GUI), ASP.NET Core Minimal API mit statischer Upload-Maske (wie PhotoCloud).
- Keine Software auf dem Router oder dem NAS.
- Die Cloud (WD My Cloud EX2 Ultra, Freigabe `hubercloud`) ist der zentrale Speicher für Dokumente.

### 2.1 Datenbank
- Technik: SQLite-Datei auf dem Windows-PC.
- Sicherung: Kopie der Datenbank auf das NAS in den Ordner `\\hubercloud\hubercloud\02_Haushalt\vemima\Datenbank` (relativ zum Share: `02_Haushalt\vemima\Datenbank`). Der Ordner muss bereits existieren.
- Zeitpunkt: Da die Anwendung nur manuell gestartet wird (kein Autostart, kein Dienst), gibt es keinen Zeitplan im Hintergrund. Die Sicherung läuft beim Start und beim Beenden der Anwendung, höchstens einmal je Kalendertag und Zustand (nicht bei jedem Start).
- Versionen: Jede Sicherung hat ein Datum im Dateinamen und überschreibt keine ältere. Nur so schützt das Backup auch vor versehentlich gelöschten Daten. Aufbewahrung: die letzten 5 Sicherungen; ältere werden gelöscht.
- Begründung: Eine SQLite-Datei direkt auf einer SMB-Freigabe ist bei Sperren fehleranfällig. Die Dokumente selbst liegen dagegen auf dem NAS (Abschnitt 5.1).

## 3. Übernahme aus PhotoCloud
Folgende Bausteine werden aus dem Repository [joehubi/photocloud](https://github.com/joehubi/photocloud) übernommen bzw. angepasst:

| Baustein | Übernahme |
|---|---|
| SMB-Zugriff auf das NAS | Übernehmen: UNC-Pfad `\\Host\Share`, Anmeldung mit User/Passwort, Erreichbarkeitsprüfung |
| Streaming-Upload | Übernehmen: keine vollständige Pufferung im RAM; unvollständige Datei bei Abbruch löschen |
| Dateinamen | Übernehmen: Datum-Präfix `YYYYMMDD_`, keine Überschreibung (`name (1).jpg`), threadsicher. Das Datum kommt bei Rechnungen aus der Erkennung (Rechnungsdatum, Abschnitt 5.0); EXIF und Änderungsdatum gelten nur als Ersatz für Dokumente ohne erkennbares Datum |
| Konfiguration | Übernehmen: JSON-Datei mit Klartext-Passwort, `.gitignore`, `config.example.json` ohne Geheimnisse |
| Config-Speicherung in GUI | Übernehmen: temporäre Datei, dann Ersetzen (atomar) |
| Zugriffsschutz Web | Übernehmen: IP-Whitelist (`AllowedClientIps`), kein Login, Firewall-Regel nur Profil „Privat“ |
| Web-Upload-Maske | Übernehmen: Mehrfachauswahl über System-Picker, Fortschritt pro Datei, „Neu beginnen“ |
| Logging | Übernehmen: Tagesdatei `logs\...`, Stufen `Info`/`Error`, Live-Anzeige in GUI, Filter |
| Projektstruktur | Übernehmen: `Core` (ohne UI), `Web` (Minimal API + Maske), `App` (Avalonia), `tests` (xUnit) |

Anpassungen gegenüber PhotoCloud:
- Ziel der Uploads sind nicht nur feste Bildordner, sondern Dokumente. Der Client wählt am Smartphone keinen Ordner; jede Datei landet im Eingang (Abschnitt 5.0) und wird danach zugeordnet.
- Erlaubte Dateiendungen: `.pdf`, `.jpg`, `.jpeg`, `.png`, `.heic`, `.webp`.
- Zielordner sind über die Ablagestruktur (Abschnitt 5.1) bestimmt und nicht frei aus einer Liste. Dateien dürfen nur in den Eingang geschrieben werden; Pfadangaben vom Client werden abgelehnt.

## 4. Fachliches Modell (Entwurf)
- **Objekt**: Ein Gebäude. In dieser Stufe nur Riesenhaus. Adresse, Eigentümer-Daten.
- **Wohnpartei / Mieter**: Person(en) mit Kontaktdaten, zugeordnet zu einem Objekt und einer Wohneinheit. Aktuell eine Wohnpartei.
- **Wohneinheit**: Wohnung innerhalb eines Objekts (Fläche in m²).
- **Mietvertrag**: Gültigkeitszeitraum, Kaution und ein Verlauf von Kaltmiete und Nebenkostenvorauszahlung mit „gültig ab“-Datum (Abschnitt 5.2).
- **Mietzahlung**: Zahlungseingang mit Datum, Betrag, Kommentar und Zuordnung zu einem Sollmonat oder zu einer Forderung (Nachzahlung). Eine Zahlung kann auf mehrere Monate aufgeteilt werden.
- **Forderung**: Zusätzliches Soll oder Guthaben außerhalb der Monatsmiete, z. B. die Nachzahlung aus einer Nebenkostenabrechnung, mit Betrag, Fälligkeit und Verweis auf die Abrechnung.
- **Mietkonto / Mietschulden**: Soll (Kaltmiete + Vorauszahlung je Monat, die „Warmmiete“, plus Forderungen) minus Ist (Zahlungen) je Monat; Saldo = Summe der Differenzen bis heute. Negativer Saldo = offener Betrag.
- **Dokument**: Datei auf dem NAS mit Metadaten (Dokumentdatum, Kategorie, Objekt, Herkunft Smartphone/PC). Kategorien: Rechnung, Mahnung, Mietvertrag, Nebenkostenabrechnung, Sonstiges. Erzeugte PDFs (Abrechnung, Steuer-Auswertung) sind ebenfalls Dokumente.
- **Kostenart und Posten**: Die Kostenart gruppiert (Heizung, Wasser, Grundsteuer, Versicherung, Müll, Renovierung). Ein **Posten** ist eine Zeile der Abrechnung und gehört zu genau einer Kostenart, z. B. „Wohngebäudeversicherung“ und „Haus- und Grundbesitzerhaftpflicht“ unter der Kostenart Versicherung. Jede Rechnung gehört zu einem Posten (die Kostenart ergibt sich daraus). Umlagefähigkeit und Umlageschlüssel gelten je Posten.
- **Rechnung**: Eintrag zu einem Dokument mit Rechnungsdatum, Rechnungssteller, Rechnungs- bzw. Referenznummer, Rechnungsbetrag (brutto), Abrechnungszeitraum, Abrechnungsjahr, Kommentar, Posten (Kostenart), Kennzeichen umlagefähig, Kennzeichen steuerlich absetzbar (§35a), Dokumenttyp und Status (Abschnitt 5.0). Entspricht einer Zeile der heutigen Excel-Tabelle.
- **Nebenkostenabrechnung**: Je Objekt, Wohnpartei und Abrechnungsjahr: Kosten je Posten (nur bestätigte, umlagefähige Rechnungen des Abrechnungsjahres), Anteil (Umlageschlüssel), Vorauszahlungen, Ergebnis (Nachzahlung oder Guthaben), neue Vorauszahlung mit Gültig-ab-Datum, Zahlungsfrist, erzeugtes PDF. Das Ergebnis erzeugt eine Forderung im Mietkonto.

Währung: EUR. Beträge als Dezimal (keine Gleitkommazahlen).

## 5. Funktionale Anforderungen

### 5.0 Rechnungserkennung per Handyfoto (Muss, Kernfunktion)

**Eingangskanäle (beide führen in denselben Ablauf)**
- **Smartphone**: Foto über die Web-Maske (Chrome), siehe Schritt 1.
- **PC**: Digital erhaltene Rechnungen (z. B. PDF per E-Mail) werden in der GUI direkt in den Eingang hochgeladen, per Dateiauswahl oder Drag & Drop. Mehrere Dateien gleichzeitig sind möglich. Erlaubt sind die Dateitypen aus Abschnitt 3 (`.pdf` und Bilder). Der PC-Upload braucht keinen Webserver und keine IP-Freigabe, die Datei geht direkt in den Eingang auf dem NAS.
- Ein PDF mit eingebettetem Text wird ohne OCR direkt ausgelesen (genauer als bei einem Foto). Nur gescannte oder fotografierte PDFs laufen durch die Texterkennung.

**Ablauf**
1. **Eingang**: Am Handy die neue Rechnung abfotografieren und über die Web-Maske (Chrome) hochladen, oder am PC die Datei (z. B. PDF) in der GUI hochladen. Es ist nichts auszuwählen: kein Zielordner, keine Kategorie. Dokumente, die keine Rechnung sind (z. B. Mietvertrag), laufen ebenfalls über den Eingang und werden danach in der GUI einer Kategorie zugeordnet (Abschnitt 5.1).
2. **Ablage im Eingang**: Die Datei wird zunächst im Eingangsordner `02_Haushalt\vemima\Riesenhaus\Eingang` auf dem NAS abgelegt, weil das Jahr (Ordner) erst nach der Erkennung und Prüfung des Rechnungsdatums feststeht. Der Ordner muss existieren.
3. **Erkennung auf dem PC**: Vemima liest den Text aus dem Bild (bei PDF: aus dem PDF) und ermittelt:
   - **Rechnungssteller** (z. B. Schornsteinfeger)
   - **Rechnungsdatum**
   - **Rechnungsbetrag** (Betrag für den Abrechnungszeitraum, brutto; welche Beschriftung gilt, hängt vom Dokumenttyp ab, siehe „Erkennungsprofile“)
   - **Kommentar** (Leistung/Verwendungszweck, z. B. Kehrarbeiten)
   - **Posten/Kostenart** (z. B. Heizung) und **umlagefähig** ja/nein
4. **Posten-Zuordnung**:
   - Regelbasiert über Rechnungssteller und Stichwörter (z. B. Schornsteinfeger, Kehrarbeiten → Wartung Heizungsanlage (Heizung); Stadtwerke, Wasser → Kaltwasser/Abwasser (Wasser); AVR, Abfall → Müllentsorgung (Müll); Wohngebäude → Wohngebäudeversicherung; Haftpflicht → Haus- und Grundbesitzerhaftpflicht (beide Versicherung); Grundsteuer → Grundsteuer).
   - Eine vom Benutzer bestätigte oder korrigierte Zuordnung wird gemerkt (Rechnungssteller → Posten), sodass dieselbe Firma künftig automatisch richtig erkannt wird.
   - Wird der Posten nicht erkannt, ist er „Unklar“ und wird in der GUI manuell gewählt.
5. **Ablage**: Mit der ersten Bestätigung (Prüfung) wird die Datei aus dem Eingang nach `Riesenhaus\<Jahr des Rechnungsdatums>\<Dateiname>` verschoben. Dateiname bei Rechnungen: `YYYYMMDD_<Rechnungssteller>.<Endung>`, bei anderen Dokumenten `YYYYMMDD_<Originalname>.<Endung>`; bei Namenskollision wird ein Zähler angehängt (Regeln wie in 5.1). Solange eine Rechnung nicht bestätigt ist oder das Datum fehlt, bleibt die Datei im Eingang. Spätere Änderungen von Datum oder Rechnungssteller verschieben oder benennen die Datei nicht mehr um.
6. **Rechnungstabelle**: Die Rechnungen erscheinen tabellarisch je Abrechnungsjahr (Datum, Rechnungssteller, Rechnung, Kommentar, Umlagefähig), gruppiert nach Kostenart/Posten mit Summe je Posten, wie in der heutigen Excel-Tabelle. Noch nicht bestätigte Rechnungen sind sichtbar markiert und werden in den Summen separat ausgewiesen.

**Prüfung**
- Jede erkannte Rechnung hat einen Status: *Zu prüfen* (automatisch erkannt) oder *Bestätigt* (vom Benutzer geprüft).
- In der GUI werden erkanntes Bild und erkannte Werte nebeneinander angezeigt; Korrektur ist möglich, mit einem Klick bestätigen.
- Nur bestätigte Rechnungen gehen in Nebenkostenabrechnung und Steuer-Auswertung ein.
- Ist ein Feld nicht lesbar, bleibt es leer und die Rechnung bleibt *Zu prüfen*. Die Datei geht nie verloren.

**Rechnungen ändern und löschen**
- Jede Rechnung kann in der GUI jederzeit geändert werden, auch nach der Bestätigung: Datum, Rechnungssteller, Nummer, Betrag, Abrechnungszeitraum, Abrechnungsjahr, Kommentar, Kostenart, umlagefähig, §35a. Eine geänderte Rechnung gilt als vom Benutzer geprüft (Status *Bestätigt*).
- Rechnungen können auch von Hand neu angelegt werden (ohne Dokument).
- Jede Rechnung kann gelöscht werden, nach Rückfrage. Beim Löschen wird gewählt:
  - *Nur den Eintrag löschen*: die Datei bleibt auf dem NAS und ist weiter als Dokument auffindbar (Standard).
  - *Eintrag und Datei löschen*: die Datei wird zusätzlich vom NAS gelöscht, nach einer zweiten, ausdrücklichen Bestätigung.
- Nach jeder Änderung oder Löschung sind Rechnungstabelle, Summen je Kostenart, Nebenkostenabrechnung und Steuer-Auswertung sofort aktuell (die Werte werden aus den Rechnungen berechnet, nicht separat gespeichert).
- Eine Änderung des Datums oder des Rechnungsstellers verschiebt oder benennt die Datei auf dem NAS nicht um, sobald sie aus dem Eingang abgelegt wurde (Schritt 5).
- Änderungen und Löschungen werden im Log als `Info` protokolliert (Rechnung, geänderte Felder, alter und neuer Wert).
- Erfolgte die Löschung versehentlich, lässt sich die Rechnung über das noch vorhandene Dokument neu erkennen („Erneut erkennen“).

**Technik**
- Alles läuft lokal auf dem Windows-PC. Die Dokumente werden nicht an Cloud-Dienste oder das Internet übertragen.
- Lokale Texterkennung (OCR) mit regelbasierter Auswertung der Felder (feste Muster für Betrag, Datum, Rechnungssteller und Stichwörter für die Kostenart). Keine KI, keine Cloud. Welche OCR-Bibliothek verwendet wird (z. B. Windows-OCR oder Tesseract; PDF-Text direkt aus dem PDF), wird bei der Umsetzung anhand von Testrechnungen entschieden.
- Fotos vom Handy werden vor der Erkennung aufbereitet (Dokument finden, entzerren, drehen, Kontrast), siehe „Fotoqualität“.

**Felderkennung (Regeln, abgeleitet aus der ersten Beispielrechnung eines Schornsteinfegers)**

| Feld | Quelle auf der Rechnung | Ergebnis im Beispiel |
|---|---|---|
| Rechnungssteller | Kopfzeile bzw. Absenderzeile über dem Adressfeld (Name der Firma/Person) | Name des Schornsteinfegermeisters |
| Rechnungsdatum | Feld „Datum“ im Rechnungskopf | 16.11.2026 |
| Rechnungsbetrag | Betrag mit Beschriftung „Betrag zu zahlen“ (brutto) | 178,93 € |
| Kommentar | Zeile unter „Leistungsbezeichnung“ | Jahresgebühr BImSchV-Messung |
| Kostenart | Stichwörter Schornsteinfeger/Schornsteinreinigung/Immissionsschutzmessung im Text | Heizung, umlagefähig |
| Rechnungsnummer | Feld „Rechnung-Nr.“ | 263590 |

Regeln und Fallstricke, die daraus folgen:
- **Betrag**: Maßgeblich ist der **Rechnungsbetrag für den Leistungszeitraum (brutto)**, nicht der Betrag, der am Ende überwiesen wird. Der Zahlbetrag weicht oft ab, etwa durch Vorauszahlungen, Raten, Guthaben oder Mahngebühren (Beispiele unten: Wasser, Müll, Grundsteuer, Mahnung). Auf der Schornsteinfeger-Rechnung gibt es mehrere Beträge (Netto 150,36, MwSt 28,57, Betrag zu zahlen 178,93); dort ist der Bruttobetrag richtig. „Netto“ und „MwSt“ werden nie als Rechnungsbetrag genommen. Beschriftungen wie „Betrag zu zahlen“ oder „Gesamtbetrag“ sind nicht überall richtig; die Wahl der Beschriftung hängt vom Dokumenttyp ab (siehe „Erkennungsprofile“).
- **Datum**: Es gibt mehrere Datumsangaben (Rechnungsdatum 16.11.2026, Zahlungsziel „Zahlbar bis 26.11.2026“, außerdem ein Feld „Rechnungs-Jahr“). Maßgeblich ist das Datum mit der Beschriftung „Datum“ bzw. „Rechnungsdatum“, oder bei Briefen das Datum hinter dem Ort im Briefkopf (z. B. „Stuttgart, 04.02.2025“). Fälligkeitsdaten („zahlbar bis“, „fällig am“, „innerhalb von zwei Wochen“) und Zeiträume werden nicht als Rechnungsdatum gewertet.
- **Rechnungssteller vs. Empfänger**: Auf der Rechnung stehen mehrere Namen und Adressen (Rechnungssteller, Empfänger, Liegenschaft). Der Rechnungssteller steht in Kopf/Absenderzeile, nicht im Adressfeld des Empfängers.
- **Rechnungsnummer und Duplikate**: Eine doppelt hochgeladene Rechnung wird als Duplikat gemeldet und nicht doppelt gezählt. Ein Duplikat ist: gleicher Rechnungssteller + gleiche Rechnungs- bzw. Referenznummer + gleicher Abrechnungszeitraum (bzw. gleiches Datum, wenn kein Zeitraum angegeben ist). Der Zeitraum gehört zwingend dazu, weil sich Kunden-, Buchungs- und Versicherungsnummern jedes Jahr wiederholen.
- Zahlen im deutschen Format (`1.234,56`, Komma als Dezimaltrenner) und Daten im Format `TT.MM.JJJJ`.
- Zusätzliche Nummern (IBAN, Kunden-Nr., USt-Id.) werden nicht als Betrag oder Datum gewertet.
- Die Rechnung nennt außerdem eine **Liegenschaft** (Adresse des Objekts). In dieser Ausbaustufe wird sie nicht ausgewertet, da es nur ein Objekt gibt. Bei mehreren Objekten kann sie später die Zuordnung zum Objekt liefern.
- Auf der Rechnung steht ein Hinweis, dass sie nach §35a EStG steuerlich absetzbar ist. Vemima erfasst dafür das Kennzeichen „steuerlich absetzbar (§35a)“. Es wird bei Erkennung des Hinweises „§35a“ im Text vorbelegt, bei der Prüfung bestätigt oder geändert und in der Steuer-Auswertung (5.5) angezeigt.

**Zweite Beispielrechnung (Wohngebäudeversicherung, Handyfoto einer Mahnung)**

| Feld | Quelle auf dem Dokument | Ergebnis im Beispiel |
|---|---|---|
| Rechnungssteller | Logo/Kopf und Fußzeile (Name des Versicherers) | Sparkassen-Versicherung |
| Datum | Briefkopf, „Ort, Datum“ | 04.02.2025 |
| Rechnungsbetrag | Spalte „Rechnungsbetrag EUR“ der Zeile Wohngebäudeversicherung | 596,24 |
| Kommentar | Spalte „Sparte“ | Wohngebäudeversicherung |
| Kostenart | Stichwort Versicherung/Wohngebäudeversicherung | Versicherung, umlagefähig |
| Referenznummer | „Nr.“ hinter dem Produktnamen (Versicherungsnummer), hier keine Rechnungsnummer | Versicherungsnummer |
| Abrechnungszeitraum | Spalte „Abrechnungszeitraum“ | 01.01.2025 bis 01.01.2026 |

Erkenntnisse daraus:
- **Dokumenttyp**: Das Dokument ist eine **Mahnung**, keine Rechnung. Vemima erkennt den Dokumenttyp (Rechnung, Mahnung/Zahlungserinnerung, sonstiges Schreiben) an Stichwörtern wie „Mahnung“, „Zahlungserinnerung“, „Mahngebühren“.
- **Mahnung**: Der Rechnungsbetrag ist 596,24 €. Der Gesamtbetrag 601,24 € enthält 5,00 € Mahngebühren. Als Rechnungsbetrag wird 596,24 € übernommen, nicht 601,24 €. Mahngebühren sind keine Versicherungskosten und werden nicht erfasst.
  - Aus einer Mahnung wird eine Rechnung angelegt (Rechnungsbetrag ohne Mahngebühren), markiert als „aus Mahnung“.
  - Existiert die Originalrechnung bereits (gleicher Rechnungssteller, gleiche Referenznummer, gleicher Zeitraum), wird die Mahnung nur als weiteres Dokument an diese Rechnung gehängt und keine zweite Rechnung angelegt.
  - Trifft die Originalrechnung erst nach der Mahnung ein, wird sie als Duplikat der „aus Mahnung“-Rechnung erkannt: Das neue Dokument wird an die bestehende Rechnung gehängt (Rechnung bleibt eine), Datum und Beträge werden bei der Prüfung aus der Originalrechnung übernommen.
- **Referenznummer**: Bei Versicherungen gibt es keine Rechnungsnummer, sondern eine Versicherungsnummer. Die Duplikatprüfung nutzt deshalb Rechnungssteller + Rechnungs- oder Referenznummer + Abrechnungszeitraum bzw. Datum.
- **Abrechnungsjahr**: Rechnungsdatum (04.02.2025) und Leistungszeitraum (2025) können auseinanderfallen. Das Abrechnungsjahr einer Rechnung (für Nebenkostenabrechnung und Steuer) legst du pro Rechnung selbst fest. Vemima schlägt es vor (Jahr des Abrechnungszeitraums, sonst Jahr des Rechnungsdatums); du bestätigst oder änderst es bei der Prüfung. Der Abrechnungszeitraum wird dafür mit ausgelesen.
- **Versicherungsort/Liegenschaft**: Wie bei der ersten Rechnung steht die Adresse des Objekts auf dem Dokument („Versicherungsort“ bzw. „Liegenschaft“). Der Empfänger kann eine andere Adresse haben als das Objekt.

**Weitere Beispiele: Wasser, Müll, Grundsteuer (Scans) und Vertragsdaten (Screenshot)**

Die drei Bescheide liegen als Scans (Bilddateien) vor. Alle drei zeigen, dass der zu überweisende Betrag nicht der Rechnungsbetrag ist.

| Dokument | Rechnungssteller | Datum | Rechnungsbetrag (richtig) | Betrag, der **nicht** zu nehmen ist | Zeitraum | Referenz |
|---|---|---|---|---|---|---|
| Jahresendabrechnung/Gebührenbescheid Wasser | Stadtwerke Sinsheim | 05.01.2026 („Sinsheim, den …“) | 474,68 € = Gesamtkosten brutto (Wasser 245,76 + Abwasser 228,92) | „Zu zahlender Betrag“ 141,68 € (nach Abzug der Zahlungen von 333,00 €); „Neuer Abschlagsbetrag“ 138,00 € | 01.01.2025 bis 31.12.2025 | Rechnungsnummer 21233486 |
| Abfallgebührenbescheid | AVR Kommunal AöR | 23.02.2026 (Feld „Datum“) | 175,00 € („werden die Abfallgebühren festgesetzt auf“) | Nachforderung 1,00 €; geleistete Vorauszahlung 174,00 €; Vorauszahlung 2026 (196,00 € bzw. 2 × 98,00 €); Raten 99,00 € | 01.01.2025 bis 31.12.2025 | Kundennummer |
| Grundsteuerbescheid (Jahresbescheid) | Stadt Sinsheim (Stadtverwaltung) | 10.01.2025 (Feld „Datum“) | 132,43 € (Grundsteuer B für das Jahr) | Einzelne Raten 33,10 € (Summe der Raten ergibt den Jahresbetrag); Messbetrag 63,06 €; Hebesatz 210 % | 01.01.2025 bis 31.12.2025 | Buchungszeichen |

Erkenntnisse daraus:
- **Der Rechnungsbetrag ist der Betrag für das Abrechnungsjahr**, hier immer der Jahresbetrag. Der Zahlbetrag, Raten, Nachforderungen und Vorauszahlungen für das Folgejahr werden nicht als Rechnungsbetrag genommen. Bei Wasser ergibt „Gesamtkosten (brutto)“ 474,68 €, das entspricht deinem Excel-Wert; „Zu zahlender Betrag“ 141,68 € wäre falsch.
- **Mehrere Zeiträume**: Der AVR-Bescheid nennt zwei Zeiträume (Abrechnung 2025 und Vorauszahlung 2026). Maßgeblich ist der Zeitraum der **Abrechnung** (2025), nicht der der Vorauszahlung.
- **Rechnungsdatum und Abrechnungsjahr weichen ab**: Alle drei Bescheide betreffen 2025, sind aber teils 2026 datiert (Wasser 05.01.2026, Müll 23.02.2026). Das bestätigt die Entscheidung, das Abrechnungsjahr pro Rechnung festzulegen. Deine Excel-Tabelle nutzt teils andere Daten (z. B. 31.12.25 bei Wasser, 01.05.25 bei Grundsteuer); Vemima übernimmt das Datum, das auf dem Dokument steht.
- **Rechnungssteller**: Die kleine Absenderzeile über der Adresse nennt beim Wasserbescheid „Stadt Sinsheim“, der Rechnungssteller laut Logo und Fußzeile ist aber „Stadtwerke Sinsheim“. Die Absenderzeile ist daher unzuverlässig. Rechnungssteller wird aus Logo/Kopf, Fußzeile und der Zuordnung bekannter Firmen ermittelt.
- **Mehrseitige Dokumente**: Der Wasserbescheid hat 5 Seiten, der Grundsteuerbescheid 2 Seiten (mit weiteren Raten und Hinweisen auf der Folgeseite). Die Hauptwerte stehen auf Seite 1, trotzdem müssen alle Seiten als ein Dokument hochgeladen werden können.
- **Scanqualität**: Die Scans enthalten Lochmarken, Streifen, Rasterfelder (Moiré in farbigen Kästen) und einen unteren leeren Bereich. Die Erkennung muss damit zurechtkommen; wichtige Werte stehen oben und in der Mitte.
- **Kostenart-Stichwörter** aus den Beispielen: Wasser, Schmutzwasser, Niederschlagswasser, Stadtwerke → Wasser; Abfallgebühren, AVR → Müll; Grundsteuer, Grundsteuerbescheid → Grundsteuer.

**Vertragsdaten-Screenshot (Grundbesitzerhaftpflicht)**
- Das Bild ist ein Screenshot aus einem Versicherungsportal (Vertragsdaten), **keine Rechnung**: kein Datum, kein sichtbarer Rechnungssteller, kein Rechnungsbetrag. Zu sehen sind Vertragsnummer (V 90 034 985/320), Vertragsbeginn 04.08.2009, Jahresbeitrag 96,80 € (inkl. Versicherungssteuer) und ein Teilbeitrag 25,30 € je Risikoadresse.
- Vemima erkennt: Dokumenttyp „Vertragsdaten/sonstiges Schreiben“, Kostenart Versicherung, umlagefähig (Stichwort „Haftpflicht“, „Grundbesitzerhaftpflicht“, „Versicherungsteuer“). Es werden aber **nicht** automatisch Datum und Betrag in eine Rechnung eingetragen, weil sie fehlen bzw. mehrdeutig sind (96,80 € Gesamtbeitrag oder 25,30 € Teilbeitrag).
- Solche Dokumente werden abgelegt und als *Zu prüfen* markiert; Datum, Betrag und Abrechnungsjahr trägst du bei der Prüfung selbst ein. Fehlende Felder bleiben leer und werden nie geraten.

**Erkennungsprofile**
- Weil sich die Beträge je Dokumenttyp unterscheiden, nutzt die Erkennung Profile je Dokumenttyp bzw. Rechnungssteller. Jedes Profil legt fest, welche Beschriftung den Rechnungsbetrag liefert:

| Profil | Beschriftung für den Rechnungsbetrag |
|---|---|
| Handwerker/Dienstleister (z. B. Schornsteinfeger) | „Betrag zu zahlen“ bzw. Brutto-Gesamtbetrag |
| Wasser/Abwasser (Stadtwerke) | „Ihre Gesamtkosten (brutto)“: Gesamtgebühren des Jahres = Wasserversorgung + Abwasser (245,76 + 228,92 = 474,68 €) |
| Abfall (AVR) | „festgesetzt auf“ (Abrechnung des Jahres) |
| Grundsteuer | „Grundsteuer in EUR“, Spalte „neu“, Jahresbetrag |
| Versicherung | Spalte „Rechnungsbetrag“, bei Mahnung ohne Mahngebühren |
| Allgemein (Fallback) | Bruttobetrag mit Beschriftung „Rechnungsbetrag“ oder „Gesamtbetrag“; im Zweifel Status *Zu prüfen* |

- Eine bestätigte oder korrigierte Betragswahl wird für den Rechnungssteller gemerkt (welche Beschriftung zum richtigen Betrag führte), sodass dieselbe Firma im nächsten Jahr richtig erkannt wird.
- **Plausibilitätsprüfung**: Wo möglich werden Beträge gegengerechnet (z. B. Wasser + Abwasser = Gesamtkosten, Raten × Anzahl = Jahresbetrag). Stimmt die Rechnung nicht, bleibt die Rechnung *Zu prüfen* mit Hinweis.

**Fotoqualität (Erkenntnisse aus dem Handyfoto)**
- Das Beispielfoto ist schräg aufgenommen (Perspektive), das Papier ist gewölbt, auf dem Foto sind Hintergrund (Ordner, Ringe, Tisch) und der Papierrand zu sehen.
- Vor der Texterkennung wird deshalb das Dokument im Bild gesucht, ausgeschnitten, entzerrt und gedreht, und der Kontrast wird verbessert. Kleine Schrift (Fußzeilen) muss nicht erkannt werden, wichtig sind die Felder oben.
- In der Web-Maske steht ein kurzer Aufnahmehinweis: Dokument flach auf einen dunklen Untergrund legen, gerade von oben fotografieren, gutes Licht, ganze Seite im Bild. Alternativ kann die Dokumenten-Scan-Funktion des Handys (erzeugt eine entzerrte PDF) für den Upload genutzt werden.

**Mehrseitige Rechnungen**
- In der Web-Maske können mehrere Fotos zu einer Rechnung zusammengefasst werden. Sie werden als ein Dokument abgelegt und gemeinsam erkannt.

### 5.1 Dokumentenablage (Muss)
- Upload vom Smartphone über die Web-Maske (Fotos und Dateien über den System-Picker).
- Upload am PC über die GUI (Dateiauswahl und Drag & Drop, mehrere Dateien gleichzeitig). Rechnungen durchlaufen ebenfalls die Erkennung (5.0).
- Dokumente, die keine Rechnung sind (z. B. Mietvertrag), laufen wie Rechnungen über den Eingang (5.0) und werden danach in der GUI zugeordnet: Kategorie und Dokumentdatum (Pflicht, bestimmt den Jahresordner), optional Wohnpartei. Bei Rechnungen ergibt sich die Zuordnung aus der Erkennung. Am Smartphone wird nichts ausgewählt.
- Dokumente lassen sich in der GUI löschen (nur Eintrag oder Eintrag und Datei, mit Rückfrage wie bei Rechnungen).
- Ablage auf dem NAS nach Objekt und Jahr (Kategorie, Mieter usw. stehen nur in der Datenbank, nicht im Ordnernamen):
  ```
  \\hubercloud\hubercloud\02_Haushalt\vemima\
      Datenbank\
      Riesenhaus\Eingang\                (neue Uploads, noch nicht zugeordnet)
      Riesenhaus\2025\20250109_Schornsteinfeger.pdf
  ```
  - Basisordner (relativ zum Share): `02_Haushalt\vemima`. Objektordner: `Riesenhaus`.
  - Das Jahr ist das Rechnungs- bzw. Dokumentdatum (nicht das Uploaddatum und nicht das Abrechnungsjahr).
  - Die Anwendung legt keine Ordner an, mit einer Ausnahme: Den Jahresordner (`2025`) legt sie selbst an, wenn er fehlt. Basis-, Objekt- und Eingangsordner müssen existieren.
- Dateinamen: Datum-Präfix, keine Überschreibung (Details in 5.0, Schritt 5).
- Metadaten der Datei werden in der Datenbank gespeichert. Die Datei kann über die Datenbank wiedergefunden werden.
- Suche und Filter nach Objekt, Mieter, Kategorie, Zeitraum.

### 5.2 Mietzahlungen und Mietschulden (Muss)
Ersetzt die heutige Excel-Tabelle „Mietkonto“ (Spalten Jahr, Monat, Mieteinnahmen, Nebenkosten/Vorauszahlung, Warmmiete, Überweisung, Bezahlt?, Differenz, Kommentar). Alles ist in der GUI erfassbar und änderbar.

**Mietvertrag und Verlauf**
- Je Wohneinheit gibt es einen Mietvertrag mit einem **Verlauf** von Beträgen mit „gültig ab“-Datum: Kaltmiete und Nebenkostenvorauszahlung. Beispiel aus deinem Mietkonto: Juni 2019 Kaltmiete 500,00 € + Vorauszahlung 70,00 €; ab 2025 Kaltmiete 650,00 € + 85,00 €; ab 01.01.2027 Kaltmiete 650,00 € + 115,00 €.
- **Warmmiete** (Soll je Monat) = Kaltmiete + Nebenkostenvorauszahlung, wird berechnet (735,00 € bzw. ab 2027 765,00 €).
- Eine Änderung der Miete oder der Vorauszahlung ist ein neuer Eintrag im Verlauf; frühere Monate bleiben unverändert.
- Die Vorauszahlung je Monat des Abrechnungsjahres bildet die Grundlage für die Nebenkostenabrechnung (z. B. 12 × 85,00 €, Abschnitt 5.3).

**Zahlungen erfassen**
- Eine Zahlung hat: Datum, Betrag, Zuordnung (Sollmonat, für den gezahlt wurde, oder eine Forderung), Kommentar (z. B. „inkl. Rückzahlung“).
- Pro Monat sind mehrere Zahlungen möglich; ein Monat kann auch von einer höheren oder niedrigeren Zahlung abweichen (z. B. 800,00 € statt 735,00 €).
- Eine Zahlung für mehrere Monate (Sammelzahlung) wird beim Erfassen auf die Monate aufgeteilt; eine Überzahlung bleibt als Differenz im Monat stehen.
- Zahlungen können jederzeit geändert und gelöscht werden.

**Mietkonto-Ansicht (GUI)**
- Tabelle je Monat und Jahr wie in der Excel-Vorlage: Jahr, Monat, Kaltmiete, Vorauszahlung, Warmmiete (Soll), Überweisung (Ist), Bezahlt?, Differenz, Kommentar.
- **Bezahlt?** wird berechnet und nicht von Hand gesetzt (in der Excel-Tabelle ist die Spalte manuell gepflegt und teils widersprüchlich): *Bezahlt* (Ist ≥ Soll), *Teilweise* (0 < Ist < Soll), *Offen* (kein Zahlungseingang eingetragen, Monat nicht später als der aktuelle), *Zukünftig* (späterer Monat). Farbig dargestellt (grün, gelb, rot, grau) statt der manuellen Farben in Excel. Eigene Anmerkungen gehören in das Kommentarfeld der Zahlung.
- **Differenz** je Monat = Ist − Soll (positiv: zu viel gezahlt, negativ: offen). Zusätzlich wird der **Saldo** (Summe aller Differenzen bis heute) angezeigt.
- Filter nach Jahr; zukünftige Monate sind sichtbar, zählen aber nicht zum Rückstand.
- Ein Monat gilt als *Offen*, sobald keine (ausreichende) Zahlung eingetragen ist, einschließlich des laufenden Monats. Es gibt keinen Fälligkeitstag, weil Zahlungen von Hand und nicht immer am Monatsersten eingetragen werden. Spätere Monate sind *Zukünftig*.
- Schnellerfassung: Zahlung mit Datum und Betrag, der Sollmonat wird vorgeschlagen (ältester offener Monat).

**Mietschulden**
- Anzeige des aktuellen Rückstands (Saldo) mit den offenen Monaten.
- Reine Anzeige der Rückstände. Keine Mahnschwelle, keine Mahnungen, keine automatischen Hinweise.

**Forderungen und Gutschriften aus der Nebenkostenabrechnung**: Eine Nachzahlung (oder ein Guthaben) aus der Abrechnung erscheint als eigene Forderung im Mietkonto (z. B. „Nebenkosten-Nachzahlung 2025“) mit Fälligkeit (Zahlungsfrist aus der Abrechnung). Eine Nachzahlung ist zusätzliches Soll, ein Guthaben eine Gutschrift. Zahlungen werden der Forderung zugeordnet und verrechnet; die Nachzahlung muss nicht mehr nebenher geführt werden. Vorzeichen: In der Abrechnung ist Nachzahlung = Kosten − Vorauszahlungen (positiv = Mieter zahlt); im Mietkonto ist die Differenz Ist − Soll (negativ = offen). Eine Nachzahlung von 516,00 € erscheint deshalb im Mietkonto als Soll von 516,00 €.

**Altdaten**: Es gibt keinen Import aus der bestehenden Excel-Tabelle. Das Mietkonto wird ab Januar 2026 geführt (Abschnitt 5.6), damit die Vorauszahlungen und Zahlungen des Abrechnungsjahres 2026 und die Nachzahlung aus der Abrechnung 2025 (fällig Dezember 2026) enthalten sind. Der Mietvertragsverlauf wird zusätzlich ab Januar 2025 erfasst, weil die Abrechnung 2025 die Vorauszahlungen von 2025 braucht.

### 5.3 Nebenkostenabrechnung (Muss)
- Erstellung der jährlichen Abrechnung für Riesenhaus und die Wohnpartei.
- Kostenarten (heutige Trennung in Tabellenform, Stand Screenshot):

| Kostenart | Beispiel Rechnungssteller | Kommentar | Umlagefähig |
|---|---|---|---|
| Heizung | Schornsteinfeger | Kehrarbeiten (mehrmals im Jahr) | Ja |
| Wasser | Stadtwerke | Wasser, Schmutz- und Niederschlagswasser | Ja |
| Grundsteuer | Stadt | Jahresgebühr Grundsteuer | Ja |
| Versicherung | Versicherer | Wohngebäudeversicherung, Grundbesitzerhaftpflicht (zwei Posten) | Ja |
| Müll | Entsorger | Abfallgebühren | Ja |
| Renovierung | diverse | Instandhaltung/Renovierung, steuerrelevant | Nein (in der Regel) |

- Die Rechnungsfelder stehen in Abschnitt 4. Rechnungen werden in der Regel automatisch aus dem Handyfoto oder der PDF erzeugt (Abschnitt 5.0) und können zusätzlich von Hand erfasst werden. Pro Posten wird die Summe angezeigt.
- Die Kostenarten und Posten sind eine feste Liste, die in den Einstellungen erweiterbar ist.
- **In die Abrechnung gehen nur Rechnungen, die bestätigt und umlagefähig sind und deren Abrechnungsjahr dem Abrechnungsjahr entspricht.** Renovierungen sind in der Regel nicht umlagefähig, werden aber erfasst, weil sie für die Steuer wichtig sind; sie fließen nicht in die Abrechnung der Mieter ein. Auswertung je Objekt und Jahr für die Steuer: siehe 5.5.

#### Umlageschlüssel (Prozentanteile)
- Der Umlageschlüssel wird in einer Maske der GUI eingetragen, nicht im Code festgelegt.
- Die Maske hat je Objekt, Posten und Wohneinheit einen Prozentwert (Anteil der Wohneinheit am Posten).
- Standardwert überall: **100 %**. Die Werte lassen sich später anpassen. Die Maske zeigt pro Posten die Summe der Prozentwerte an, damit Abweichungen auffallen.

#### Vorauszahlungen und Ausgabe
- Die Nebenkostenvorauszahlung steht im Mietvertrag (Abschnitt 4) und wird in der Abrechnung den Kosten gegenübergestellt (Nachzahlung/Guthaben). Vorauszahlungen werden je Monat des Abrechnungsjahres berücksichtigt (Änderungen der Vorauszahlung im Jahr sind möglich). Es zählt die **Soll-Vorauszahlung laut Mietvertragsverlauf** (z. B. 12 × 85,00 €), nicht die tatsächlich gezahlten Beträge. Fehlende Zahlungen erscheinen deshalb als Rückstand im Mietkonto (5.2) und nicht als Nachzahlung in der Abrechnung.
- Die Abrechnung wird als PDF ausgegeben (pro Wohnpartei) und kann zusätzlich im Dokumentenordner des Objekts abgelegt werden.

**Inhalt der Abrechnung (abgeleitet aus der heutigen Excel-Abrechnung)**
- Kopf: Titel „Nebenkostenabrechnung <Jahr>“, Abrechnungszeitraum, Vermieter (Absender), Mieter (Empfänger), Objekt, Wohneinheit, Erstellungsdatum.
- **Ergebnis** (groß): Nachzahlung oder Guthaben, mit Kosten, Vorauszahlungen und Zahlungsfrist. Rechnung: Nachzahlung = Kosten − Vorauszahlungen (positiv = Mieter zahlt nach, negativ = Guthaben).
- **Kostentabelle je Posten**: Bezeichnung (z. B. „Wartung Heizungsanlage inkl. Kamin“, „Kaltwasser / Abwasser“, „Grundsteuer“, „Wohngebäudeversicherung“, „Haus- und Grundbesitzerhaftpflicht“, „Müllentsorgung (AVR)“), Anteil (Umlageschlüssel in %), Kosten (Summe der bestätigten Rechnungen des Abrechnungsjahres), Veränderung zum Vorjahr (Betrag). Darunter die Summe.
- **Abrechnung**: Nebenkosten, abzüglich Vorauszahlungen (z. B. 12 × 85,00 €), Ergebnis.
- **Neue Vorauszahlung** (zweite Seite): die vom Eigentümer festgelegte neue monatliche Vorauszahlung (groß), Vergleich mit der bisherigen, Gültig-ab-Datum, dazu die Kosten des Abrechnungsjahres je Posten als Grundlage (pro Jahr und pro Monat).
- Hinweis auf Belegeinsicht und Zahlungsdaten (Konto, Verwendungszweck) für eine Nachzahlung.
- Optional: Hinweis auf steuerlich absetzbare Posten (§35a) mit Betrag, wenn die Rechnung das Kennzeichen trägt.
- **Posten und Kostenarten**: Ein Posten ist eine Zeile der Abrechnung (Abschnitt 4). Er ist feiner als die Kostenart: Die Kostenart „Versicherung“ hat zwei Posten, „Wohngebäudeversicherung“ und „Haus- und Grundbesitzerhaftpflicht“. Die Bezeichnung des Postens ist in den Einstellungen änderbar.
- **Vorjahresvergleich**: Die Veränderung zum Vorjahr wird automatisch aus den bestätigten Rechnungen des Vorjahres berechnet (heute steht sie von Hand als Kommentar in der Excel-Tabelle). Da keine Altdaten importiert werden, ist die Spalte leer („–“), solange für das Vorjahr keine Rechnungen erfasst sind. Im Layout-Entwurf stehen Beispielwerte.
- **Neue Vorauszahlung**: Der Eigentümer trägt die neue monatliche Vorauszahlung von Hand ein (heute: 115,00 € ab 01.01.2027, bisher 85,00 €). Sie wird nicht automatisch berechnet. Vemima zeigt zur Orientierung die Jahreskosten ÷ 12 (hier 128,00 €) an. Die festgelegte Vorauszahlung wird als neuer Eintrag im Verlauf des Mietvertrags gespeichert (Abschnitt 5.2).

**Layout-Entwurf**: [mockups/nebenkostenabrechnung-entwurf.html](mockups/nebenkostenabrechnung-entwurf.html) (im Browser öffnen, mit Strg+P als PDF speichern). Gestaltung: A4, zwei Seiten, farbiger Kopfbereich, große Ergebnis-Karte (orange bei Nachzahlung, grün bei Guthaben), Tabelle mit abwechselnden Zeilen, Summenzeile hervorgehoben, Beispieldaten mit fiktivem Mieter. `TODO`: Entwurf vom Eigentümer bestätigen oder anpassen.

### 5.4 Benutzeroberfläche (GUI)
- Bereiche: Objekt/Mieter, Mietkonto (Monatstabelle mit Zahlungserfassung und Mietverlauf), Rechnungen, Dokumente, Einstellungen, Upload-Log.
- Upload-Log wie in PhotoCloud (Zeitstempel, Datei, Ziel, Stufe `Info`/`Error`).

### 5.5 Steuer-Auswertung (Muss)
- Auswertung je Objekt und Jahr: Einnahmen (Mietzahlungen) und Ausgaben nach Kostenart, getrennt nach umlagefähig und nicht umlagefähig. Rechnungen mit Kennzeichen „steuerlich absetzbar (§35a)“ werden gesondert aufgeführt.
- `TODO`: Bezugsjahr klären. Die Abrechnung nutzt das Abrechnungsjahr einer Rechnung, für die Steuer kann das Jahr der tatsächlichen Zahlung maßgeblich sein (Zu- und Abfluss). Dafür müsste die Rechnung ein Zahlungsdatum erhalten. Mit dem Steuerberater klären.
- Renovierungen sind für die Steuer wichtig: Liste aller Renovierungsrechnungen je Objekt und Jahr mit Summe, jeweils mit Verweis auf das abgelegte Dokument.
- Ausgabe als PDF (je Objekt und Jahr). Inhalt: Einnahmen, Ausgaben nach Kostenart (umlagefähig/nicht umlagefähig) und Renovierungsliste. `TODO`: Layout der PDF.

### 5.6 Einführung: Erprobung und Produktivstart
- Es gibt **eine** Datenbank und **einen** Ablageordner (`02_Haushalt\vemima`), keine getrennte Testumgebung.
- Die Erprobung läuft mit echten Daten. Weil sich alles wieder löschen lässt, sind Fehlversuche unkritisch: Rechnungen (5.0), Zahlungen (5.2) und Dokumente (5.1) lassen sich einzeln löschen; beim Löschen eines Dokuments kann die Datei auf dem NAS mit entfernt werden.
- Es gibt keinen Import aus der Excel-Tabelle. Das Mietkonto wird ab Januar 2026 geführt (Abschnitt 5.2); Einträge davor sind nicht nötig. Der Mietvertragsverlauf beginnt mit Januar 2025.
- Rechnungen des Abrechnungsjahres 2026 werden schon jetzt erfasst, damit die Nebenkostenabrechnung 2026 im Jahr 2027 daraus erstellt werden kann.
- Das Datenbank-Backup (Abschnitt 2.1) läuft von Anfang an. Weil jede Sicherung ein eigenes Datum trägt und keine ältere überschreibt, lässt sich ein früherer Stand wiederherstellen, auch nach versehentlichem Löschen.

## 6. Nicht-funktionale Anforderungen
- Nur Heimnetz. Kein Port-Forwarding, kein Internetzugriff, keine Cloud-Dienste von Drittanbietern.
- Passwörter nicht in Git und nicht im Log.
- Datensicherung: Die Datenbank liegt auf dem PC, die Dokumente liegen auf dem NAS (das NAS ist selbst Speicher). Das Datenbank-Backup liegt in `02_Haushalt\vemima\Datenbank` (Abschnitt 2.1).
- Ist das NAS nicht erreichbar, startet die Anwendung trotzdem. Uploads schlagen mit `Error` fehl, bis das NAS wieder da ist.
- Datenschutz: Mietdaten sind personenbezogen. Der Zugriff ist auf das Heimnetz und die freigegebenen Geräte beschränkt.

## 7. Projektstruktur (Entwurf)
- `src\Vemima.Core`: Domänenmodell, Datenbankzugriff, Storage (SMB), Mietkonto-Logik, Abrechnungs-Logik, Rechnungserkennung (Textauslesen, OCR, Regeln, Profile), PDF-Erzeugung, Logging. Ohne GUI- und Web-Abhängigkeiten. `TODO`: Erkennung und PDF-Erzeugung ggf. als eigene Bibliotheken auslagern, wenn die Abhängigkeiten (OCR, PDF) groß werden.
- `src\Vemima.Web`: Upload-Maske und Minimal API (schreibt nur in den Eingang).
- `src\Vemima.App`: Avalonia-GUI.
- `tests\Vemima.Tests`: xUnit-Tests.

### 7.1 Konfiguration
Eine JSON-Datei wie bei PhotoCloud (mit `config.example.json` ohne Geheimnisse, Klartext-Passwort, per `.gitignore` ausgeschlossen), bearbeitbar in der GUI. Einstellungen:
- NAS: Host, Share, Benutzer, Passwort (wie PhotoCloud).
- Webserver: Port, erlaubte Geräte-IPs (wie PhotoCloud), maximale Dateigröße, erlaubte Dateiendungen.
- Pfade: Basisordner (`02_Haushalt\vemima`), Objektordner, Eingangsordner, Backup-Ordner, Pfad der SQLite-Datenbank auf dem PC.
- Backup: Aufbewahrung der letzten 5 Sicherungen.

Die Mietkonto- und Saldenberechnung liegt in `Core` und wird mit Unit-Tests abgedeckt (reine Logik, ohne NAS).

## 8. Tests
- Unit-Tests: Saldo-Berechnung, Sollmonat-Zuordnung (inkl. Sammelzahlung), Forderungen, Dateinamen-Logik (Datum-Präfix, Kollision), Pfad-Bereinigung, Ändern und Löschen von Rechnungen (Summen je Posten aktualisieren sich), Nebenkostenabrechnung (Nachzahlung = Kosten − Vorauszahlungen; nur bestätigte, umlagefähige Rechnungen des Abrechnungsjahres), Duplikaterkennung (inkl. Mahnung/Originalrechnung).
- Erkennungstests: Beispielrechnungen (echte Rechnungen, z. B. Schornsteinfeger, Stadtwerke, Versicherung, AVR) dienen als Testdaten. Sie sind personenbezogen und liegen deshalb nicht im Git, sondern lokal (Pfad per Umgebungsvariable, ohne Testdaten werden die Tests übersprungen). Geprüft werden Rechnungssteller, Datum, Betrag und Kostenart.
- Integrationstest: Datei auf das NAS schreiben und prüfen (wie PhotoCloud, übersprungen ohne Konfiguration).

## 9. Abnahmekriterien (Entwurf)
- Eine eingetragene Rechnung lässt sich in der GUI ändern oder löschen; die Summen je Kostenart sind danach sofort korrekt.
- Eine per E-Mail erhaltene PDF-Rechnung wird am PC in der GUI hochgeladen und genauso erkannt und eingetragen wie ein Handyfoto.
- Ein Foto einer Rechnung (z. B. vom Schornsteinfeger) wird per WLAN übertragen und landet zuerst im Eingang. Vemima erkennt Rechnungssteller, Datum und Betrag richtig und ordnet den Posten Heizung zu. Nach der Bestätigung liegt die Datei im richtigen Jahresordner auf dem NAS, und die Rechnung steht in der Rechnungstabelle.
- Beispielrechnungen (Schornsteinfeger, Wasser, Müll, Grundsteuer, Versicherungs-Mahnung) liefern die richtigen Beträge (z. B. Wasser 474,68 €, nicht 141,68 €).
- Mietzahlungen lassen sich in der GUI erfassen; Warmmiete, Differenz und Saldo je Monat stimmen mit der bisherigen Excel-Tabelle überein (Beispiel: Soll 735,00 €, Zahlung 800,00 € → Differenz + 65,00 €).
- Offene Mietschulden werden in der Übersicht angezeigt.
- Nur freigegebene Geräte können hochladen.
- Die Nebenkostenabrechnung für das Beispieljahr 2025 ergibt Kosten 1.536,00 €, Vorauszahlungen 1.020,00 € und eine Nachzahlung von 516,00 € und erzeugt ein PDF im vereinbarten Layout (Abschnitt 5.3).

## 10. Entscheidungen und offene Fragen

### Entschieden
- **Datenbank**: SQLite-Datei auf dem Windows-PC, Backup auf das NAS (Abschnitt 2.1).
- **Dokumenttypen**: `.pdf`, `.jpg`, `.jpeg`, `.png`, `.heic`, `.webp`.
- **Nutzer**: Nur der Eigentümer (keine Mehrbenutzer-Verwaltung nötig).
- **Kernfunktion**: Rechnung per Handyfoto (Web-Maske) oder als PDF direkt am PC (GUI) hochladen, Rechnungssteller/Kostenart/Datum/Betrag automatisch erkennen und tabellarisch erfassen (Abschnitt 5.0). Verarbeitung nur lokal.
- **Erkennungstechnik**: lokale OCR + Regeln, keine KI, keine Cloud. Konkrete OCR-Bibliothek wird anhand von Testrechnungen gewählt.
- **Prüfung**: Jede erkannte Rechnung wird vom Benutzer bestätigt; nur bestätigte Rechnungen gehen in Abrechnung und Steuer.
- **Mehrseitige Rechnungen**: werden unterstützt (mehrere Fotos zu einer Rechnung).
- **Mahnung**: Es wird eine Rechnung mit dem Rechnungsbetrag ohne Mahngebühren angelegt („aus Mahnung“); existiert die Originalrechnung, wird die Mahnung nur angehängt.
- **Abrechnungsjahr**: wird pro Rechnung vom Benutzer festgelegt, Vemima schlägt es vor.
- **§35a**: Kennzeichen „steuerlich absetzbar (§35a)“ wird erfasst und in der Steuer-Auswertung angezeigt.
- **Rechnungen ändern/löschen**: jederzeit in der GUI möglich. Löschen standardmäßig nur der Eintrag (Datei bleibt), die Datei nur auf ausdrücklichen Wunsch (Abschnitt 5.0).
- **Objekt**: In dieser Stufe nur Riesenhaus (1 Wohnpartei). Weitere Objekte und kompliziertere Fälle folgen in späteren Ausbaustufen.
- **Backup-Ziel**: `\\hubercloud\hubercloud\02_Haushalt\vemima\Datenbank`.
- **Ablagestruktur**: `02_Haushalt\vemima\<Objekt>\<Jahr>\` (Abschnitt 5.1), Objektordner `Riesenhaus`. Der Jahresordner wird automatisch angelegt.
- **Mietschulden**: reine Anzeige, keine Mahnschwelle.
- **Umlageschlüssel**: Prozentwerte je Objekt, Kostenart und Wohneinheit, einstellbar in einer GUI-Maske, vorläufig überall 100 % (Abschnitt 5.3).
- **Ausgabe**: Nebenkostenabrechnung und Steuer-Auswertung als PDF.
- **Renovierung**: in der Regel nicht umlagefähig, aber erfasst wegen der Steuer (Abschnitte 5.3 und 5.5).
- **Neue Vorauszahlung**: wird vom Eigentümer von Hand festgelegt (115,00 € ab 01.01.2027), nicht berechnet.
- **Nachzahlung/Guthaben**: erscheint als eigener Posten im Mietkonto (Abschnitt 5.2).
- **Mietkonto-Status**: „Bezahlt?“ wird berechnet, nicht von Hand gesetzt.
- **Einführung**: eine Datenbank und ein Ablageordner, keine getrennte Testumgebung; alles ist löschbar. Rechnungen 2026 werden sofort erfasst, Mietkonto ab Januar 2026, kein Excel-Import (Abschnitt 5.6).
- **Posten**: Rechnungen gehören zu einem Posten (Zeile der Abrechnung), der zu einer Kostenart gehört (Abschnitt 4).
- **Ablage**: Dateien liegen zuerst im Eingang und werden mit der ersten Bestätigung in den Jahresordner verschoben (Abschnitt 5.0).
- **Backup**: beim Start und Beenden der Anwendung, mit Datum im Dateinamen, ohne Überschreiben, die letzten 5 Sicherungen bleiben (Abschnitt 2.1).
- **Mietkonto**: Start Januar 2026; „Offen“ gilt für jeden Monat bis einschließlich des laufenden ohne Zahlung, kein Fälligkeitstag (Abschnitt 5.2).
- **Vorauszahlung in der Abrechnung**: Soll laut Mietvertragsverlauf, nicht die tatsächlich gezahlten Beträge (Abschnitt 5.3).

### Offen
1. **Weitere Testrechnungen**: Vorhanden sind eine Schornsteinfeger-PDF, ein Handyfoto einer Versicherungs-Mahnung, Scans von Wasser, Müll und Grundsteuer sowie ein Screenshot der Grundbesitzerhaftpflicht. Die PDF `Grundbesitzerhaftpflicht_2.pdf` konnte nicht gelesen werden und muss erneut bereitgestellt werden. Noch benötigt: weitere Handyfotos. Die Testrechnungen enthalten Namen, Adressen und IBAN und kommen deshalb nicht ins Git.
2. **PDF-Layout** der Nebenkostenabrechnung (Entwurf liegt vor, Abschnitt 5.3) und der Steuer-Auswertung (Abschnitt 5.5).
3. **Umlageschlüssel**: Die echten Prozentwerte folgen später (vorläufig überall 100 %).
4. **Steuer-Auswertung**: Bezugsjahr (Abrechnungsjahr oder Zahlungsjahr) mit dem Steuerberater klären (Abschnitt 5.5).
