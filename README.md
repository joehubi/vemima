# Vemima

Windows-Anwendung zur Verwaltung vermieteter Objekte (zunächst Riesenhaus): Rechnungen per Handyfoto oder PDF erfassen, Mietkonto, Nebenkostenabrechnung und Steuer-Auswertung. Alles läuft lokal im Heimnetz.

Die vollständige Spezifikation steht in [docs/concept.md](docs/concept.md).

## Projektstruktur

| Projekt | Inhalt |
|---|---|
| `src\Vemima.Core` | Konfiguration, SMB-Zugriff, SQLite-Datenmodell und Mietkonto-Berechnung |
| `src\Vemima.Web` | Eingebetteter Webserver mit IPv4-Whitelist und Platzhalterseite |
| `src\Vemima.App` | Avalonia-GUI, hostet den Webserver |
| `tests\Vemima.Tests` | xUnit-Tests |

## Entwicklungsstand

Das Datenmodell und die Mietkonto-Berechnung sind als Kernbibliothek vorhanden, aber noch nicht mit der GUI verbunden. Upload, OCR, Nebenkostenabrechnung und PDF-Ausgabe sind noch nicht implementiert. Die SQLite-Datenbank wird über `VemimaDatabase.Open` lokal angelegt; produktive Einbindung, automatische Sicherungen und Datenbankmigrationen fehlen noch. Unbekannte Datenbankversionen werden nicht verändert.

Zahlungen und ihre Zuordnungen müssen über `PaymentStore` angelegt, bearbeitet oder gelöscht werden. Nach einem fehlgeschlagenen Speichervorgang sollte der Datenbankkontext verworfen werden. Die Mietkonto-Berechnung liefert einen **Zeitraumsaldo**, keinen vollständigen Kontosaldo; unzugeordnete Zahlungsteile werden getrennt ausgewiesen. Für den gesamten Saldo muss der gesamte zu führende Zeitraum einbezogen werden. Zukünftige Monate und noch nicht fällige Forderungen zählen nicht zum Saldo.

Der aktuelle Berechnungskern unterstützt nur vollständige Mietmonate und Mietänderungen zum Monatsersten. Bei abweichenden Daten meldet er einen Fehler; eine anteilige Berechnung ist noch nicht festgelegt. Diese technische Einschränkung ist keine Festlegung für die spätere Anwendung.

## Konfiguration

1. `config.example.json` nach `vemima.config.json` kopieren (liegt im Repo-Root, ist von Git ausgeschlossen) und NAS-Zugangsdaten sowie erlaubte Geräte-IPs eintragen. Alternativ den Pfad über die Umgebungsvariable `VEMIMA_CONFIG` angeben.
2. Auf dem NAS müssen die Ordner `02_Haushalt\vemima\Riesenhaus\Eingang` und `02_Haushalt\vemima\Datenbank` bereits existieren; die Anwendung legt keine Ordner an (Ausnahme: Jahresordner).

## Bauen, testen, starten

```powershell
dotnet build Vemima.sln
dotnet test tests\Vemima.Tests
dotnet run --project src\Vemima.App
```
