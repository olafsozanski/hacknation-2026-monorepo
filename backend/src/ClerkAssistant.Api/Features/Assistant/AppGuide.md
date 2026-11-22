# Przewodnik po aplikacji SOIP — System Obsługi Informacji Publicznej

## O aplikacji i układ ekranu

SOIP (System Obsługi Informacji Publicznej) wspiera urzędników w obsłudze wniosków o udostępnienie informacji publicznej: rejestr z terminami, zapytania do wydziałów, projekt odpowiedzi z anonimizacją, archiwum i baza wiedzy. Program niczego nie wysyła sam (pism, e-maili, zapytań) — przygotowuje treści do skopiowania lub pobrania, a decyzję zawsze podejmuje urzędnik. Wszystko, co proponuje AI, jest sugestią do sprawdzenia.

- Nagłówek: znaczek trybu AI, przyciski wielkości tekstu A− / A / A+ / A++, „Wersja kontrastowa” i menu użytkownika (imię i nazwisko) z „Zmień hasło” i „Wyloguj się”.
- Menu boczne: przycisk „Zarejestruj wniosek”, „Rejestr wniosków”, „Archiwum spraw”, „Anonimizacja”, „Baza wiedzy”, „Wydziały i biura”; administrator widzi też sekcję „Administracja” („Użytkownicy”, „Dziennik zdarzeń”). Na wąskim ekranie menu otwiera „Otwórz menu”.
- Daty wpisuje się w formacie dd.mm.rrrr. Asystent jest pod przyciskiem w prawym dolnym rogu i na stronie [Asystent](/asystent).

## Rejestr wniosków
Adres: /

Wnioski z wybranego roku (lista „Rok …”; wniosek należy do roku daty wpływu) oraz niezakończone wnioski z lat wcześniejszych. Najpierw sprawy otwarte od najbliższego terminu, potem zakończone.

- Kafelki szybkich filtrów: „Po terminie”, „Termin dziś”, „Termin jutro”, „W ciągu 3 dni”, „W toku” (niezakończone), „Zakończone”; kliknięcie włącza lub wyłącza filtr. Liczby dotyczą całego roku, niezależnie od innych filtrów.
- Filtry: „Szukaj” (znak sprawy, przedmiot lub wnioskodawca — bez treści; treść przeszukasz w [Archiwum spraw](/archiwum)), „Status”, „Prowadzący” („Wszyscy”, „Moje sprawy”, osoba), „Wyczyść”, widok „Tabela” / „Kalendarz”.
- Tabela: Lp., Numer sprawy, Wnioskodawca, Przedmiot zapytania (z symbolami wydziałów), Data wpływu, Termin / pozostało dni (po przedłużeniu także „ustawowo …”), Data odpowiedzi, Status, Prowadzi. Sortowanie nagłówkami Lp., Data wpływu, Termin, Status. Wiersze po terminie i na dziś są czerwone, na jutro pomarańczowe. Kliknięcie wiersza otwiera sprawę.
- Kalendarz: terminy w miesiącu, „Dziś”, „Poprzedni miesiąc”, „Następny miesiąc”; „+N więcej” pokazuje w tabeli sprawy z tym terminem (filtr „Termin: …” usuwa krzyżyk).
- Linki z filtrami: [po terminie](/?filtr=overdue), [termin dziś](/?filtr=today), [moje sprawy](/?prowadzacy=moje), [przedłużone](/?status=Extended), [kalendarz](/?widok=kalendarz).

## Rejestracja wniosku
Adres: /wnioski/nowy

Trzy sposoby: „Prześlij plik wniosku” (PDF, DOCX lub TXT do 20 MB), „Wklej treść wniosku” + „Analizuj treść” (od 20 znaków) albo „Wypełnij formularz ręcznie”. Analiza proponuje przedmiot, wnioskodawcę, adres i formę odpowiedzi (oznaczenie „propozycja AI”), listę „Wnioskodawca prosi o:”, podobne wnioski i wydziały; ostrzeżenia są w ramce „Zwróć uwagę”.

- „Dane wniosku”: Przedmiot wniosku (wymagany), Data wpływu (domyślnie dziś, nie z przyszłości; „Data na piśmie” to tylko podpowiedź — termin liczy się od wpływu do urzędu), Termin odpowiedzi (wyliczany z daty wpływu; po ręcznej zmianie „Termin zmieniony ręcznie.”), Znak sprawy (nadawany automatycznie, można zmienić), Prowadzący sprawę (domyślnie Ty), Wnioskodawca, Adres do korespondencji, Forma udostępnienia odpowiedzi (E-mail, Poczta tradycyjna, ePUAP / e-Doręczenia, Odbiór osobisty, Inna).
- „Treść wniosku” („Pokaż / ukryj treść”) można poprawić; pusta oznacza tekst z pliku.
- „Wydziały do zapytania”: zaznacz sugestie albo „Dodaj inny wydział lub biuro”.
- „Zacznij od nowa” lub „Zarejestruj wniosek” — otwiera się sprawa ze statusem „Brak działań”. Plik przesłany, a niezarejestrowany, jest usuwany po 24 godzinach.

## Sprawa
Adres: /wnioski/:id

Nagłówek: znak, przedmiot, status, termin, „Prowadzi: …”. Pasek etapów: Rejestracja → Zapytania do wydziałów → Odpowiedź przygotowana → Odpowiedź wysłana.

- Menu „Działania”: „Edytuj dane sprawy”, „Przedłuż termin / doprecyzowanie” (nieaktywne w sprawie zakończonej), „Zmień status” (lista statusów).
- „Treść wniosku”: „Oryginał (…)” pobiera plik wniosku.
- Panel 1 „Zapytania do wydziałów”: bez wydziałów — „Sugerowana ścieżka” („Przypisz zaznaczone (N)”) lub „Wybierz ręcznie”; z wydziałami — tabela z „Zapytanie wysłane” (przycisk „Oznacz jako wysłane” albo data z ikoną cofnięcia), „Odpowiedź” („Otrzymano …” / „Oczekuje · N dni”), link „E-mail”, przyciski „Zmień wydziały” i „Treść zapytania”.
- Panel 2 „Odpowiedzi wydziałów i załączniki”: „Rodzaj dokumentu” (Odpowiedź wydziału / Załącznik), przy odpowiedzi „Wydział”, potem „Dodaj plik”. Przy dokumencie: „Tekst”, pobranie, usunięcie.
- Panel 3 „Odpowiedź dla wnioskodawcy”: „Przygotuj projekt odpowiedzi”; gdy projekt istnieje — „Otwórz projekt odpowiedzi”, „Pobierz DOCX”, „Przygotuj ponownie”, „Oznacz jako przygotowaną” (gdy status nie jest „Odpowiedź przygotowana” ani „Odpowiedź wysłana”) i „Oznacz jako wysłaną” (tylko przy „Odpowiedź przygotowana”).
- Z boku: „Dane sprawy” (przycisk „Edytuj”; m.in. Termin ustawowy, Termin po przedłużeniu, Najpóźniej po przedłużeniu, Data udzielenia odpowiedzi), „Podobne sprawy” (rejestr i archiwalne odpowiedzi, podobieństwo wysokie/średnie/niskie), „Historia sprawy” (kto i kiedy zmienił sprawę, dodał, pobrał lub wyświetlił dokument).

## Projekt odpowiedzi
Adres: /wnioski/:id/odpowiedz

Edytor pisma do wnioskodawcy (bez projektu — przycisk „Przygotuj projekt”). Ramka mówi, czy projekt przygotowało AI czy szablon, i ile jest miejsc „DO UZUPEŁNIENIA”.

- Tryby: „Anonimizacja” (zaznaczenia i panel decyzji), „Edycja treści”, „Podgląd po anonimizacji”. Po wyjściu z edycji zaznaczenia są dopasowywane, a nowe dane wykrywane („Aktualizuję anonimizację…”).
- „Zapisz” (też Ctrl+S; nieaktywny bez zmian), menu „Pobierz i więcej” („Dokument Word (.docx)”, „Tekst (.txt)”, „Przygotuj projekt ponownie”) i „Odpowiedź gotowa” — zapisuje, ustawia „Odpowiedź przygotowana” i wraca do sprawy.
- Wyjście z niezapisanymi zmianami pyta „Masz niezapisane zmiany”.

## Archiwum spraw
Adres: /archiwum

Wyszukiwarka wszystkich wniosków (każdy rok i status; przy zakończonych fragment odpowiedzi) i dokumentów bazy wiedzy. Opisz temat własnymi słowami w „Czego szukasz?” i „Szukaj”; zakres „Wszystko”, „Wnioski i odpowiedzi”, „Baza wiedzy”. Do 30 wyników z oceną podobieństwa. Pełnotekstowo, a z AI także semantycznie. Fraza do 1000 znaków.

## Anonimizacja dokumentu
Adres: /anonimizacja

Narzędzie dla dowolnego dokumentu spoza spraw; dokument nie jest zapisywany w systemie. „Prześlij dokument” (PDF, DOCX, TXT do 20 MB) albo „Wklej tekst” + „Wykryj dane do ukrycia” (od 3 znaków). Widoki „Weryfikacja” i „Podgląd po anonimizacji”, decyzje jak w projekcie odpowiedzi. „Pobierz zanonimizowany” (.docx / .txt), „Nowy dokument”. Gdy fragmenty czekają na decyzję lub nic nie zaznaczono do ukrycia, pojawia się „Pobrać dokument?”.

## Baza wiedzy
Adres: /baza-wiedzy

Ustawy, zarządzenia, wytyczne i archiwalne odpowiedzi. „Archiwalna odpowiedź” zasila „Podobne sprawy”, a „Akt prawny”, „Zarządzenie” i „Wytyczne” są kontekstem prawnym AI przy projekcie odpowiedzi; wszystko jest w wyszukiwarce archiwum.

- „Dodaj dokumenty”: „Wybierz pliki” (PDF, DOCX, TXT do 20 MB, wiele naraz), „Tytuł” (przy jednym pliku), „Rodzaj dokumentów” (Akt prawny, Zarządzenie, Archiwalna odpowiedź, Wytyczne, Inne), „Dodaj”.
- „Indeksowanie”: „W kolejce do indeksowania”, „Gotowy do wyszukiwania semantycznego” lub bez AI „Wyszukiwanie pełnotekstowe”.
- Dodaje i pobiera każdy; usuwa tylko administrator.

## Wydziały i biura
Adres: /wydzialy

Słownik aktywnych komórek urzędu (z BIP) z e-mailem i „Zakres działania”, wyszukiwanie po nazwie, symbolu lub zakresie. Zakres służy do dopasowania wydziałów do wniosku. Edycję (ołówek: Nazwa, E-mail, Zakres działania, „Aktywny (proponowany w sprawach)”) ma tylko administrator. Wydział nieaktywny znika z list i sugestii.

## Użytkownicy
Adres: /administracja/uzytkownicy

Tylko administrator (inni trafiają do rejestru). Konta zakłada wyłącznie administrator.

- „Dodaj konto”: Login (litery, cyfry, . - _ @), Imię i nazwisko, Inicjały, Hasło początkowe, „Administrator”.
- „Edytuj”: dane, „Administrator”, „Konto aktywne”. Nie można odebrać sobie uprawnień administratora ani dezaktywować własnego konta.
- „Hasło”: nowe hasło, zdejmuje też blokadę. Stany: „Aktywne”, „Zablokowane”, „Nieaktywne”.

## Dziennik zdarzeń
Adres: /administracja/dziennik

Tylko administrator. Logowania (także nieudane), zmiany kont i uprawnień, operacje na sprawach, dokumentach, projektach, anonimizacji i bazie wiedzy. Filtry „Rodzaj zdarzeń”, „Użytkownik (login)”, „Filtruj”; „Wczytaj starsze”. Wpisów nie można zmieniać ani usuwać.

## Deklaracja dostępności
Adres: /dostepnosc

Zgodność z WCAG 2.1 AA. Wielkość tekstu A− / A / A+ / A++ (87,5–125%; na telefonie przyciski są w menu), „Wersja kontrastowa” (ustawienia zapamiętuje przeglądarka), link „Przejdź do treści głównej”, obsługa klawiaturą, statusy oznaczane tekstem i ikoną. Zamiast zaznaczania myszą w anonimizacji działa pole „Ukryj frazę w całym piśmie”. Skany bez warstwy tekstowej nie są odczytywane. Problemy zgłasza się do Wydziału Informatyki.

## Asystent
Adres: /asystent

Asystent odpowiada po polsku na pytania o aplikację i zasady obsługi wniosków. Ta sama rozmowa jest w okienku spod przycisku w prawym dolnym rogu i na pełnej stronie. Widzi, na jakim ekranie jesteś (adres, nagłówek, otwarte okno, błędy pól, komunikaty) i może sprawdzać sprawy, rejestr, archiwum, bazę wiedzy i wydziały.

- Może zaproponować: zmianę statusu (także z datą wysłania odpowiedzi), przedłużenie terminu (z pismem do skopiowania), ustawienie wydziałów w sprawie, zapisanie dat wysłania zapytania i odpowiedzi wydziału. Propozycja to karta — wykona się dopiero po potwierdzeniu, można ją odrzucić; obowiązują te same zasady co ręcznie. Rozpatrzonej nie da się potwierdzić ponownie („Ta propozycja została już rozpatrzona.”).
- Nie rejestruje wniosków, nie edytuje projektów, nie dodaje i nie usuwa dokumentów, nie zarządza kontami, niczego nie wysyła.
- Rozmowy są osobne dla każdego użytkownika i przechowywane domyślnie 30 dni; widać do 50 ostatnich, można je usuwać. Wiadomość do 4000 znaków.
- Bez AI odpowiada w trybie podstawowym („Model AI jest wyłączony — odpowiadam w trybie podstawowym.”). W niektórych trybach (np. model lokalny bez narzędzi) nie sprawdza danych sam i nie proponuje działań („W tym trybie asystent nie proponuje działań…”). Odpowiedzi trzeba sprawdzać.

## Statusy spraw

- **Brak działań** — po rejestracji.
- **Zapytania wysłane do wydziałów** (w tabeli „W wydziałach”) — automatycznie przy pierwszym „Oznacz jako wysłane”, ale tylko ze statusu „Brak działań”; można też ręcznie. Cofnięcie daty zapytania nie zmienia statusu.
- **Termin przedłużony** (w tabeli „Kwerenda +N dni” / „Doprecyzowanie wniosku +N dni”) — ustawia go przedłużenie terminu. W „Zmień status” można go wybrać tylko, gdy sprawa ma już przedłużony termin. Zmiana na inny status nie anuluje przedłużonego terminu.
- **Odpowiedź przygotowana** (w tabeli „Odpowiedź gotowa”) — tylko ręcznie („Oznacz jako przygotowaną”, „Odpowiedź gotowa”, „Zmień status”); przygotowanie projektu nie zmienia statusu.
- **Odpowiedź wysłana** (w tabeli „Wysłano”) — zamyka sprawę („Zakończona”): „Oznacz jako wysłaną” (z potwierdzeniem) lub „Zmień status” (bez potwierdzenia). Data udzielenia odpowiedzi to dziś (lub wcześniej zapisana), nie później niż dziś i nie wcześniej niż data wpływu; inną datę ustawi propozycja asystenta.

Statusy można zmieniać w dowolnej kolejności; cofnięcie zamknięcia usuwa datę odpowiedzi. Zakończone sprawy z poprzednich lat nie są w rejestrze bieżącego roku — wybierz ich rok lub użyj archiwum.

## Terminy odpowiedzi

- 14 dni od dnia wpływu (art. 13 ust. 1 UDIP); gdy koniec wypada w sobotę, niedzielę lub święto — najbliższy dzień roboczy (art. 57 § 4 KPA).
- Dni wolne: soboty, niedziele, 1 i 6 stycznia, Wielkanoc i Poniedziałek Wielkanocny, 1 i 3 maja, Zielone Świątki, Boże Ciało, 15 sierpnia, 1 i 11 listopada, 24 grudnia (od 2025 r.), 25 i 26 grudnia.
- Termin wylicza się przy rejestracji i można go zmienić ręcznie. Po zmianie daty wpływu w edycji termin nie przelicza się — popraw go sam.
- Oznaczenia: „N dni po terminie”, „Termin dziś”, „Termin jutro”, „Za N dni” (2–3 dni = „W ciągu 3 dni”), „Zakończona”; dni kalendarzowe do terminu obowiązującego (po przedłużeniu — przedłużonego).

## Przedłużenie terminu i wezwanie do doprecyzowania

Działania → „Przedłuż termin / doprecyzowanie” → okno „Przedłużenie terminu odpowiedzi” (obecny termin i data „najpóźniej do”).

- Rodzaj pisma: „Kwerenda” (więcej czasu na zebranie informacji) lub „Wezwanie do doprecyzowania” (niejasny zakres wniosku).
- „Liczba dni przedłużenia” 1–62 (domyślnie 30), liczona od terminu ustawowego, z przesunięciem na dzień roboczy; nowy termin najpóźniej 2 miesiące od złożenia wniosku (art. 13 ust. 2 UDIP).
- Wymagane „Przyczyna przedłużenia” (trafia do zawiadomienia) lub „Czego ma dotyczyć doprecyzowanie”, do 2000 znaków.
- Po „Przedłuż termin” status to „Termin przedłużony”, a okno pokazuje projekt pisma: zawiadomienie o przyczynie i nowym terminie albo wezwanie do doprecyzowania w 7 dni (pod rygorem pozostawienia wniosku bez rozpoznania) z nowym terminem. „Kopiuj pismo” i wyślij je sam — aplikacja go nie wysyła ani nie przechowuje.
- Kolejne przedłużenie zastępuje poprzednie (liczone znów od terminu ustawowego). Sprawy zakończonej nie da się przedłużyć.

## Dane sprawy, znak i prowadzący

- Edycja: Działania → „Edytuj dane sprawy” lub „Edytuj” w „Dane sprawy” → „Zapisz zmiany”. Przedmiot i znak są obowiązkowe.
- Znak automatyczny: `BKS.IV.1431.<Lp.>.<rok>` (Lp. — kolejny numer w roku wpływu). Własny znak (do 64 znaków) musi być unikalny.
- Prowadzący musi mieć aktywne konto; przy rejestracji domyślnie osoba rejestrująca, w edycji można wybrać „— nieprzypisana —”.
- Każdy zalogowany widzi i obsługuje wszystkie sprawy. Spraw nie można usuwać.

## Wydziały i zapytania

- Sugestie (do 5, tylko wydziały aktywne) z zakresu działania, historii podobnych spraw i AI; widać źródło („Zakres działania”, „Historia spraw”, „Analiza AI”) i średni czas odpowiedzi wydziału.
- „Wybierz ręcznie” / „Zmień wydziały” → zaznacz komórki → „Zapisz”.
- „Treść zapytania” → „Zapytanie do wydziałów”: adresaci, temat i treść z pełnym tekstem wniosku i prośbą o odpowiedź na 3 dni robocze przed terminem (nie wcześniej niż dziś). „Kopiuj temat”, „Kopiuj treść”, wyślij pocztą lub przez EZD, potem „Oznacz jako wysłane (N)”.
- Dodanie pliku „Odpowiedź wydziału” ze wskazanym wydziałem zapisuje datę otrzymania (dziś), jeśli jej nie było. Daty nie mogą być z przyszłości, a odpowiedź nie wcześniejsza niż zapytanie.

## Dokumenty sprawy

- PDF, DOCX, TXT do 20 MB (bez .doc). Ze skanu nie da się odczytać tekstu — trzeba go uzupełnić ręcznie.
- Projekt odpowiedzi powstaje z odpowiedzi wydziałów; załączniki nie są w nim używane.
- Oryginału wniosku nie można usunąć. Inny dokument usuwa osoba, która go dodała, lub administrator; usunięcie jest trwałe. Pobrania i podglądy tekstu trafiają do historii sprawy.

## Projekt odpowiedzi i anonimizacja — zasady

- Pismo w układzie urzędu: nagłówek, znak, zwrot grzecznościowy, treść, „Łączę wyrazy szacunku,”, stopka; przy „Poczta tradycyjna” także adres wnioskodawcy. Z AI treść redaguje model, bez AI powstaje szablon z tekstem odpowiedzi wydziałów; bez odpowiedzi — sam szablon.
- Braki są oznaczone „[DO UZUPEŁNIENIA: …]” — uzupełnij w „Edycja treści”. Gdy wydziały odpowiadają, że urząd nie ma informacji, powstaje wzór pisma wskazującego właściwy podmiot.
- „Przygotuj ponownie” zastępuje treść i decyzje anonimizacji.
- Wykrywanie: reguły (PESEL, NIP, dokumenty tożsamości, rachunki, telefony, e-maile, adresy, daty urodzenia, nazwiska, księgi wieczyste, numery rejestracyjne, dane wnioskodawcy) i AI. Dane kontaktowe urzędu nie są zaznaczane.
- Szare — „Do weryfikacji”, czarne — „Zostanie ukryte”, oraz „Pozostawione jawne”. Przy fragmencie: „Pokaż w tekście”, „Ukryj”, „Pozostaw jawne”, „Przywróć do weryfikacji”; kliknięcie w tekście przełącza szare/czarne, zaznaczenie myszą dodaje fragment. Także „Ukryj wszystkie sugestie”, „Wykryj ponownie”, „Ukryj frazę w całym piśmie”.
- Eksport DOCX/TXT zamienia tylko czarne fragmenty na „[dane zanonimizowane]” i dodaje akapit o wyłączeniu danych (art. 5 ust. 2 UDIP); szare NIE są ukrywane. W edytorze „Pobrać pismo mimo uwag?” ostrzega o fragmentach bez decyzji, miejscach do uzupełnienia lub braku zaznaczeń. „Pobierz DOCX” na stronie sprawy pobiera bez tych ostrzeżeń.

## Uprawnienia

Role: urzędnik i administrator. Wszyscy mają rejestr, sprawy, rejestrację, projekty, anonimizację, archiwum, bazę wiedzy (bez usuwania) i słownik wydziałów (bez edycji). Tylko administrator: „Użytkownicy”, „Dziennik zdarzeń”, edycja wydziałów, usuwanie z bazy wiedzy i usuwanie cudzych dokumentów spraw.

## Tryby AI

- „AI wyłączone” (domyślnie): tryb podstawowy — wyszukiwanie pełnotekstowe, reguły anonimizacji, szablony pism.
- „Model lokalny”: dane nie opuszczają sieci urzędu.
- „Model zewnętrzny”: prototyp, treści przetwarza zewnętrzna usługa — nie używaj z prawdziwymi danymi osobowymi.
- Gdy model nie odpowiada: „Model AI jest niedostępny — użyto trybu podstawowego.”

## Konto, logowanie i sesja

- Konto nadaje administrator. Hasło: min. 12 znaków, wielka i mała litera, cyfra. Zmiana: menu użytkownika → „Zmień hasło”.
- 5 nieudanych logowań blokuje konto na 15 minut; wcześniej odblokuje je administrator, ustawiając nowe hasło.
- Sesja wygasa po 8 godzinach bezczynności i najpóźniej 12 godzin od zalogowania; aplikacja przechodzi wtedy do ekranu logowania, a niezapisane zmiany przepadają. Wylogowanie, dezaktywacja, zmiana uprawnień lub hasła unieważniają sesje na innych urządzeniach.
- Limity: logowanie 10 prób / 5 min; operacje AI (analiza wniosku, projekt, wykrywanie danych, sugestie wydziałów, anonimizacja, asystent) 30 / min; przesyłanie plików 30 / min; wyszukiwanie 120 / min.

## Najczęstsze problemy

- „Podaj przedmiot wniosku.”, „Data wpływu nie może być późniejsza niż dzisiejsza.”, „Termin odpowiedzi nie może być wcześniejszy niż data wpływu.” — popraw pole.
- „Sprawa o znaku … jest już zarejestrowana.” — znak zajęty, np. ktoś zarejestrował wniosek po Twojej analizie i wziął zaproponowany numer. Wyczyść „Znak sprawy” (nada się automatycznie) lub wpisz inny; sprawdź, czy wniosek nie jest już w rejestrze.
- „Nie znaleziono pliku wniosku. Prześlij go ponownie.” — minęły 24 godziny od analizy; „Zacznij od nowa”. „Ten plik jest już dołączony do innej sprawy lub do bazy wiedzy.” — wniosek z pliku już zarejestrowano.
- „Wybrana osoba prowadząca nie istnieje lub jej konto jest nieaktywne.” — wybierz inną osobę.
- „Analizuj treść” nieaktywne — tekst ma mniej niż 20 znaków.
- „Nie udało się zmienić statusu” / „Sprawdź poprawność wprowadzonych danych.” — zwykle wybrano „Termin przedłużony” bez przedłużenia; użyj „Przedłuż termin / doprecyzowanie”.
- „Przedłuż termin / doprecyzowanie” nieaktywne — status „Odpowiedź wysłana”; jeśli omyłkowo, zmień status.
- „Podaj liczbę dni od 1 do 62.”, „Podaj przyczynę przedłużenia — zostanie wskazana w zawiadomieniu.”, „Opisz, czego ma dotyczyć doprecyzowanie.” — uzupełnij okno. „Termin można przedłużyć najpóźniej do … r.” — zmniejsz liczbę dni.
- Zamknięto okno i zginęło zawiadomienie — przedłuż ponownie z tymi samymi danymi albo poproś asystenta; pismo powstanie jeszcze raz.
- „Oznacz jako wysłane” kończy się „Sprawdź poprawność wprowadzonych danych.” — odpowiedź wydziału ma datę wcześniejszą niż dziś, a zapytanie nie może być późniejsze; poproś asystenta o zapisanie wcześniejszej daty zapytania.
- Brak „Oznacz jako wysłaną” — wymaga projektu i statusu „Odpowiedź przygotowana”; inaczej Działania → Zmień status → „Odpowiedź wysłana”.
- „Dane zostały w międzyczasie zmienione przez innego użytkownika.” / „Odśwież stronę, aby zobaczyć aktualną wersję, i wprowadź zmiany ponownie.” (konflikt 409) — sprawę lub projekt zmienił ktoś inny, Ty w innej karcie lub automatyczna zmiana statusu. Odśwież stronę i powtórz.
- Plik odrzucony: „Plik jest za duży. Maksymalny rozmiar to 20 MB.”, „Nieobsługiwany format pliku. Dozwolone są pliki PDF, DOCX i TXT.”, „Format .doc nie jest obsługiwany. Zapisz dokument jako .docx lub PDF.”, „Plik jest pusty.”, uszkodzony lub zaszyfrowany PDF, ponad 500 stron, „podejrzana struktura” DOCX — zapisz plik ponownie albo wklej treść. „Dokument jest zbyt złożony do automatycznego odczytu…” — wklej treść. „System przetwarza obecnie inne dokumenty…” — spróbuj za chwilę.
- „Nie odczytano tekstu z dokumentu — może to być skan…” — brak warstwy tekstowej; wpisz lub wklej treść. Skanu nie dodasz do bazy wiedzy.
- „Nie można usunąć oryginału wniosku.”, „Dokument może usunąć tylko osoba, która go dodała, albo administrator.”, „Nie masz uprawnień do wykonania tej operacji.” — działanie wymaga autora lub administratora.
- Ostrzeżenia projektu: „Brak odpowiedzi wydziałów — projekt zawiera tylko szablon pisma.” (dodaj odpowiedzi, „Przygotuj ponownie”), „Nie odczytano tekstu z pliku „…”…”, „Projekt był zbyt długi i został skrócony…”, „Nie wykryto danych do anonimizacji — przed wysłaniem przejrzyj pismo samodzielnie.”, „Dokument jest długi — model AI przeanalizował tylko jego początkową część…”.
- „Zapisz” w edytorze nieaktywny — brak niezapisanych zmian.
- „Nieprawidłowa nazwa użytkownika lub hasło.” (także konto nieaktywne); „Konto zostało czasowo zablokowane… Spróbuj ponownie za 15 minut.”
- Zmiana hasła: „Obecne hasło jest nieprawidłowe.” (pod polem Nowe hasło), „Hasła nie są takie same.”, „Hasło musi mieć co najmniej 12 znaków.”, „Hasło musi zawierać cyfrę / małą literę / wielką literę.”
- „Sesja wygasła. Zaloguj się ponownie.” — zaloguj się; „Sesja bezpieczeństwa wygasła. Odśwież stronę i spróbuj ponownie.” — odśwież.
- „Zbyt wiele żądań. Odczekaj chwilę i spróbuj ponownie.” — limit; odczekaj minutę.
- „Brak połączenia z serwerem…”, „Wystąpił błąd serwera…” — sieć lub serwer; spróbuj ponownie.
- Konta: „Użytkownik o takiej nazwie już istnieje.”, „Nazwa użytkownika może zawierać tylko litery, cyfry oraz znaki . - _ @”, „Nie możesz odebrać sobie uprawnień administratora ani dezaktywować własnego konta.”
- Wyszukiwanie: „Wpisz szukaną frazę.”, „Szukana fraza może mieć maksymalnie 1000 znaków.”

## Gdzie mogę…? Czy mogę…?

- Zarejestrować wniosek — [Rejestracja wniosku](/wnioski/nowy); bez pliku: wklej treść lub „Wypełnij formularz ręcznie”.
- Zmienić prowadzącego, znak, daty, treść — Działania → „Edytuj dane sprawy”.
- Przedłużyć termin lub wezwać do doprecyzowania — Działania → „Przedłuż termin / doprecyzowanie”.
- Przygotować zapytanie do wydziałów — „Zapytania do wydziałów” → „Treść zapytania”.
- Dodać odpowiedź wydziału — „Odpowiedzi wydziałów i załączniki” → „Odpowiedź wydziału”, wydział, „Dodaj plik”.
- Znaleźć dawną odpowiedź — [Archiwum spraw](/archiwum) lub „Podobne sprawy”. Zanonimizować inny dokument — [Anonimizacja](/anonimizacja).
- Czy aplikacja wyśle pismo lub e-mail? Nie — kopiujesz lub pobierasz treść i wysyłasz sam.
- Czy mogę usunąć sprawę? Nie ma takiej funkcji. Cofnąć zamknięcie? Tak, „Zmień status”.
- Czy mogę przedłużyć termin dwa razy? Tak, nowe przedłużenie zastępuje poprzednie, limit 2 miesięcy od wpływu.
- Czy mogę obsługiwać sprawę innego urzędnika? Tak, sprawy są dostępne dla wszystkich zalogowanych.
- Czy mogę edytować wydziały, zakładać konta, usuwać z bazy wiedzy? Tylko administrator.
