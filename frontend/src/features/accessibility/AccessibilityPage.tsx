import { Heading, List, Stack, Text } from '@chakra-ui/react'
import { PageHeader } from '@/components/common/PageHeader'

export function AccessibilityPage() {
  return (
    <>
      <PageHeader title="Deklaracja dostępności" />
      <Stack gap="5" maxW="3xl" lineHeight="1.7">
        <Text>
          Asystent UDIP jest projektowany zgodnie z wytycznymi WCAG 2.1 na poziomie AA, wymaganymi przez ustawę z dnia 4
          kwietnia 2019 r. o dostępności cyfrowej stron internetowych i aplikacji mobilnych podmiotów publicznych.
        </Text>
        <Heading as="h2" size="lg">
          Ułatwienia
        </Heading>
        <List.Root ps="5" gap="1">
          <List.Item>zmiana wielkości tekstu (przyciski A− / A / A+ / A++ w nagłówku),</List.Item>
          <List.Item>wersja kontrastowa (przycisk z ikoną kontrastu),</List.Item>
          <List.Item>pełna obsługa z klawiatury i widoczny wskaźnik fokusu,</List.Item>
          <List.Item>link „Przejdź do treści głównej” na początku strony,</List.Item>
          <List.Item>statusy i terminy oznaczane tekstem i ikoną, nie tylko kolorem,</List.Item>
          <List.Item>komunikaty o wynikach operacji ogłaszane czytnikom ekranu,</List.Item>
          <List.Item>uwzględnienie ustawienia systemowego „ogranicz ruch”.</List.Item>
        </List.Root>
        <Heading as="h2" size="lg">
          Znane ograniczenia
        </Heading>
        <Text>
          Zaznaczanie fragmentów tekstu do anonimizacji myszą ma alternatywę dostępną z klawiatury: pole „Ukryj frazę w
          całym piśmie” w panelu anonimizacji. Dokumenty będące skanami (bez warstwy tekstowej) nie są odczytywane
          automatycznie — treść należy wprowadzić ręcznie.
        </Text>
        <Heading as="h2" size="lg">
          Kontakt
        </Heading>
        <Text>Problemy z dostępnością zgłaszaj do Wydziału Informatyki urzędu.</Text>
      </Stack>
    </>
  )
}
