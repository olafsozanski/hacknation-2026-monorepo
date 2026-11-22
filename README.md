# SOIP — System Obsługi Informacji Publicznej

Narzędzie wspierające urzędników Urzędu Miasta Bydgoszczy w obsłudze wniosków o udostępnienie
informacji publicznej (UDIP). Skraca przygotowanie odpowiedzi z kilku dni do kilkunastu minut:

- **Rejestr i kalendarz wniosków** — układ kolumn jak w kalendarium urzędu (Lp., numer sprawy,
  wnioskodawca, przedmiot, daty, osoba odpowiedzialna, pozostało dni); terminy liczone automatycznie
  (art. 13 UDIP, art. 57 KPA, dni wolne od pracy); wyróżnienie spraw po terminie, na dziś i na jutro;
  widok tabeli i kalendarza miesięcznego; wybór roku.
- **Kwerenda i doprecyzowanie** — przedłużenie terminu (limit 2 miesięcy) z gotowym projektem
  zawiadomienia albo wezwania do doprecyzowania wniosku, w formacie pism urzędu.
- **Rejestracja z pliku** — PDF/DOCX/TXT; asystent odczytuje treść, proponuje przedmiot i dane
  wnioskodawcy, znak sprawy oraz termin.
- **Podobne sprawy** — wyszukiwanie hybrydowe (pełnotekstowe + semantyczne) w rejestrze i archiwum.
- **Sugerowana ścieżka** — wydziały właściwe dla wniosku na podstawie zakresu działania (dane z BIP),
  historii podobnych spraw i analizy AI, ze średnim czasem odpowiedzi wydziału.
- **Projekt odpowiedzi** — pismo w układzie stosowanym przez Biuro Komunikacji Społecznej (nagłówek,
  znak sprawy, zwrot grzecznościowy, „komórka merytoryczna”, stopka), przygotowane wyłącznie z odpowiedzi
  wydziałów, z miejscami „DO UZUPEŁNIENIA” zamiast zmyślonych danych.
- **Anonimizacja** — według listy danych podlegających anonimizacji przekazanej przez urząd (dane
  osobowe, dane wrażliwe, księgi wieczyste, numery rejestracyjne, tajemnice prawnie chronione);
  propozycje (szare) zatwierdzane przez urzędnika (czarne), eksport DOCX/TXT. Dostępna w projekcie
  odpowiedzi oraz jako osobne narzędzie dla dowolnego dokumentu (bez zapisywania go w systemie).
- **Baza wiedzy** — akty prawne, zarządzenia i archiwalne odpowiedzi (także hurtowo), wykorzystywane
  w wyszukiwaniu i przy tworzeniu projektów.
- **Asystent w aplikacji** — odpowiada po polsku na pytania o działanie programu i zasady obsługi
  wniosków, widzi bieżący ekran i dane sprawy, sprawdza rejestr, archiwum i wydziały oraz proponuje
  działania (zmiana statusu, przedłużenie terminu, wydziały, daty zapytań), które wykonują się dopiero
  po zatwierdzeniu przez urzędnika; bez AI działa w trybie podstawowym. Rozmowy są przechowywane przez
  `ASSISTANT_RETENTION_DAYS` dni (domyślnie 30); dla modeli lokalnych bez wywoływania narzędzi ustaw
  `AI_ASSISTANT_TOOLS=false`.
- **Bez automatycznej wysyłki** — program przygotowuje treści do skopiowania; decyzję zawsze
  podejmuje urzędnik.

## Architektura

| Warstwa | Technologia |
|---|---|
| Backend | ASP.NET Core 10 (LTS), minimal API, EF Core, ASP.NET Identity |
| Baza danych | PostgreSQL 17 + pgvector (wektory), pg_trgm, wyszukiwanie pełnotekstowe, pliki w `bytea` |
| AI | API zgodne z OpenAI — prototyp: OpenAI, docelowo: lokalny model w Ollama (np. Bielik) |
| Frontend | React 19, React Router (data router), Chakra UI v3, TanStack Query |

Szczegóły struktury i konwencji: [AGENTS.md](AGENTS.md).

## Uruchomienie (Docker)

```bash
cp .env.example .env    # uzupełnij hasła
docker compose up -d --build
```

Aplikacja: <http://localhost:8080>, login `admin` i hasło z `ADMIN_PASSWORD`.
Dane demonstracyjne: `DEMO_DATA=true` i `DEMO_PASSWORD` w `.env` — 10 bieżących wniosków o różnych
terminach, archiwum z 10 przykładowymi (zanonimizowanymi) sprawami przekazanymi przez urząd
(`Seed/sample-cases.json`) oraz sprawy fikcyjne; konta urzędników `akowalska`, `pnowak`, `mwisniewska`.

### Tryb w pełni lokalny (bez Internetu)

```bash
docker compose --profile local-ai up -d
docker compose exec ollama ollama pull bge-m3
docker compose exec ollama ollama pull SpeakLeash/bielik-11b-v2.3-instruct:Q4_K_M
```

W `.env` ustaw sekcję „Target: local model via Ollama” (`AI_IS_LOCAL=true`). Wymiar wektorów to 1024
(`bge-m3` lub `text-embedding-3-small` z parametrem `dimensions`). Bez skonfigurowanego modelu
aplikacja działa w trybie podstawowym: wyszukiwanie pełnotekstowe, reguły anonimizacji, szablony pism.

## Rozwój lokalny

Wymagania: .NET SDK 10, Node.js 22, PostgreSQL z rozszerzeniami `vector` i `pg_trgm`
(najprościej: `docker compose up -d db` i udostępnienie portu 5432).

```bash
cd backend/src/ClerkAssistant.Api && dotnet run --launch-profile http   # API: http://localhost:5080
cd frontend && npm install && npm run dev                               # UI:  http://localhost:5173
```

W trybie Development tworzone są konta demonstracyjne: `admin` / `Admin12345678` oraz urzędnicy
z hasłem `Urzednik12345` (patrz `appsettings.Development.json`).

Testy:

```bash
cd backend && dotnet test
cd frontend && npm run lint && npm run typecheck && npm test
```

## Architektura backendu

Każda funkcja to folder `Features/<Funkcja>` z kontraktami, cienkimi endpointami, serwisem z logiką
biznesową i mapperem (Mapperly, mapowanie generowane w czasie kompilacji). Szczegóły w [AGENTS.md](AGENTS.md).

## Bezpieczeństwo

- **Konta i sesje** — brak samodzielnej rejestracji (konta zakłada administrator); hasła min. 12 znaków;
  blokada po 5 nieudanych próbach i limit prób logowania; sesja w ciasteczku `HttpOnly`, `SameSite=Strict`,
  `Secure` z prefiksem `__Host-` (przy HTTPS), wygasa najpóźniej 12 godzin od zalogowania; wylogowanie,
  dezaktywacja konta, zmiana uprawnień lub hasła unieważniają wszystkie kopie sesji. Przykładowe hasła
  z dokumentacji są odrzucane poza trybem deweloperskim.
- **Uprawnienia** — każde żądanie API wymaga zalogowania; konta, dziennik zdarzeń i usuwanie dokumentów
  z bazy wiedzy — tylko administrator; oryginału wniosku nie można usunąć, inne dokumenty usuwa ich autor
  lub administrator.
- **Ochrona żądań** — CSRF dla wszystkich operacji zmieniających dane; nagłówki CSP, `X-Frame-Options`,
  `nosniff`, `no-store` dla API; limity liczby żądań (logowanie, AI, przesyłanie plików, wyszukiwanie);
  przy `REQUIRE_HTTPS=true` połączenia HTTP są odrzucane, a nagłówki `X-Forwarded-*` przyjmowane tylko
  od wskazanego serwera proxy.
- **Dziennik zdarzeń** — logowania (także nieudane), zmiany uprawnień, operacje na sprawach i dokumentach,
  pobrania dokumentów, eksport i anonimizacja; tabela tylko do dopisywania (wyzwalacz w bazie blokuje
  zmianę i usuwanie wpisów); podgląd dla administratora. Treść dokumentów i dane osobowe nie trafiają
  do logów technicznych.
- **Pliki** — limit 20 MB, weryfikacja sygnatury, ochrona przed „zip bomb”, limit czasu i równoległości
  odczytu, limit pamięci procesu w kontenerze; przechowywanie w bazie, pobieranie wyłącznie jako załącznik.
- **AI** — domyślnie wyłączone. Model lokalny działa tylko przy adresie w sieci wewnętrznej
  (`AI_IS_LOCAL=true`); zewnętrzne API wymaga jawnej zgody `AI_ALLOW_EXTERNAL_PROCESSING=true`
  (wyłącznie prototyp i dane testowe), a interfejs wyraźnie oznacza ten tryb. Treści dokumentów i odpowiedzi
  modelu są traktowane jako niezaufane (ograniczniki w promptach, brak renderowania HTML, propozycje AI
  zawsze zatwierdza urzędnik, ostrzeżenie przed eksportem pisma bez anonimizacji).
- **Klucze** — klucze ochrony danych ASP.NET przechowywane w bazie; opcjonalnie szyfrowane certyfikatem
  (`Security__DataProtectionCertificatePath`).
- **Wdrożenie** — kontener bez uprawnień roota, system plików tylko do odczytu, bez dodatkowych
  uprawnień jądra, z limitami pamięci i CPU; baza niedostępna z zewnątrz; domyślnie nasłuchiwanie tylko na
  `127.0.0.1`. Dostęp z innych komputerów wyłącznie przez HTTPS (reverse proxy) — patrz `.env.example`.

### Zalecenia przed wdrożeniem produkcyjnym

- logowanie przez Active Directory / Entra ID (MFA, centralne wyłączanie kont),
- odczyt dokumentów w osobnym procesie z limitami zasobów (izolacja parserów PDF/DOCX),
- pseudonimizacja danych przed przekazaniem do modelu (jeśli kiedykolwiek używany będzie model zewnętrzny),
- polityka retencji i archiwizacji zgodna z instrukcją kancelaryjną (JRWA kat. archiwalna dla klasy 1431),
- regularne kopie zapasowe bazy (zawiera dokumenty i klucze sesji).
