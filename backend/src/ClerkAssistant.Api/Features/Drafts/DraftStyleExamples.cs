using ClerkAssistant.Api.Infrastructure.Letters;

namespace ClerkAssistant.Api.Features.Drafts;

public static class DraftStyleExamples
{
    public const string Answer = """
        w odpowiedzi na wystosowany przez Pana wniosek o udostępnienie informacji publicznej w zakresie liczby mieszkańców na podstawie opłat za gospodarowanie odpadami komunalnymi na rzecz miasta Bydgoszczy z następujących bloków i posesji prywatnych leżących na terenie parafii św. Maksymiliana Kolbego w Bydgoszczy, na podstawie danych przygotowanych przez komórkę merytoryczną, tj. Wydział Gospodarki Odpadami Urzędu Miasta Bydgoszczy, informuję, że liczba zadeklarowanych mieszkańców z wymienionych we wniosku ulic wynosi:

        5697 osób.
        """;

    public const string NoData = """
        w związku z wystosowanym przez Pana zapytaniem w zakresie: Ile punktów sprzedaży alkoholu przybyło w Bydgoszczy od czasu objęcia funkcji Prezydenta do dnia dzisiejszego?, na podstawie informacji przygotowanych przez komórkę merytoryczną, tj. Wydział Spraw Obywatelskich Urzędu Miasta Bydgoszczy, informuję, że Urząd Miasta Bydgoszczy nie prowadzi statystyk, które umożliwiłyby udzielenie odpowiedzi.
        """;

    public const string NotTheHolder = $"""
        w odpowiedzi na wystosowany przez Pana wniosek o udostępnienie informacji publicznej dotyczący aktualnego stanu i lokalizacji przechowywania rzeźby Ludmiły Stehnovej, uprzejmie informuję, iż Urząd Miasta Bydgoszczy nie jest dysponentem wnioskowanych danych.

        Jednocześnie wskazuję, iż zgodnie z zasadą dostępu do informacji publicznej, wyrażoną w art. 4 ust. 3 {LetterLayout.ActCitation}, obowiązane do udostępnienia informacji publicznej są podmioty publiczne będące w posiadaniu tych informacji.

        W związku z powyższym Urząd Miasta Bydgoszczy uprzejmie informuje, iż Wnioskodawca w zakresie informacji dotyczących aktualnego stanu i lokalizacji przechowywania rzeźby Ludmiły Stehnovej winien się zwrócić do Kujawsko-Pomorskiego Wojewódzkiego Konserwatora Zabytków jako właściwego dysponenta danych.

        Poniżej zamieszczam dane do kontaktu:

        Kujawsko-Pomorski Wojewódzki Konserwator Zabytków
        Delegatura w Bydgoszczy
        ul. Jezuicka 2
        85-102 Bydgoszcz
        e-mail: bydgoszcz@torun.wkz.gov.pl
        """;
}
