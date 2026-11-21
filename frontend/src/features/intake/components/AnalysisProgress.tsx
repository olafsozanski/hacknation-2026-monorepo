import { Card, Heading, List, Spinner, Stack, Text } from '@chakra-ui/react'

export function AnalysisProgress({ fileName }: { fileName?: string }) {
  return (
    <Card.Root variant="outline" bg="bg.panel" maxW="4xl" role="status" aria-live="polite">
      <Card.Body>
        <Stack direction="row" gap="4" align="flex-start">
          <Spinner size="lg" color="brand.solid" mt="1" />
          <Stack gap="2">
            <Heading as="h2" size="md">
              Analizuję wniosek{fileName ? ` „${fileName}”` : ''}…
            </Heading>
            <Text color="fg.muted">To zwykle trwa kilka sekund.</Text>
            <List.Root color="fg.muted" fontSize="sm" ps="4">
              <List.Item>odczyt treści dokumentu,</List.Item>
              <List.Item>propozycja przedmiotu i danych wnioskodawcy,</List.Item>
              <List.Item>wyszukiwanie podobnych spraw w rejestrze i archiwum,</List.Item>
              <List.Item>wskazanie wydziałów, które mogą przygotować odpowiedź.</List.Item>
            </List.Root>
          </Stack>
        </Stack>
      </Card.Body>
    </Card.Root>
  )
}
