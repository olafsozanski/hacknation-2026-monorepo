import { Alert, Button, CloseButton, Dialog, Field, Input, Portal, Stack, Text, Textarea } from '@chakra-ui/react'
import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/client'
import type { ExtensionResult, RequestDetail } from '@/api/types'
import { CopyButton } from '@/components/common/CopyButton'
import { formatDate } from '@/lib/dates'
import { useExtendDeadline } from '../api'

export function ExtendDeadlineDialog({ request, open, onClose }: { request: RequestDetail; open: boolean; onClose: () => void }) {
  const extend = useExtendDeadline(request.id)
  const [days, setDays] = useState('30')
  const [reason, setReason] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [result, setResult] = useState<ExtensionResult | null>(null)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const value = Number(days)
    if (!Number.isInteger(value) || value < 1 || value > 62) {
      setErrors({ days: 'Podaj liczbę dni od 1 do 62.' })
      return
    }
    if (!reason.trim()) {
      setErrors({ reason: 'Podaj przyczynę przedłużenia — zostanie wskazana w zawiadomieniu.' })
      return
    }
    extend.mutate(
      { days: value, reason: reason.trim(), version: request.version },
      {
        onSuccess: setResult,
        onError: (error) => setErrors(error instanceof ApiError ? { ...error.fieldErrors, form: error.message } : {}),
      },
    )
  }

  return (
    <Dialog.Root open={open} onOpenChange={(e) => !e.open && onClose()} size="lg" scrollBehavior="inside">
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content as="form" onSubmit={submit}>
            <Dialog.Header>
              <Dialog.Title>{result ? 'Termin przedłużony' : 'Przedłużenie terminu (kwerenda)'}</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              {result ? (
                <Stack gap="4">
                  <Alert.Root status="success" variant="subtle">
                    <Alert.Indicator />
                    <Alert.Title>Nowy termin odpowiedzi: {formatDate(result.extendedDueOn)}</Alert.Title>
                  </Alert.Root>
                  <Text>
                    Zgodnie z art. 13 ust. 2 UDIP powiadom wnioskodawcę o przyczynach opóźnienia i nowym terminie. Poniżej
                    projekt zawiadomienia — skopiuj go i wyślij właściwym kanałem.
                  </Text>
                  <Field.Root>
                    <Field.Label>Projekt zawiadomienia</Field.Label>
                    <Textarea readOnly value={result.noticeText} rows={14} fontSize="sm" lineHeight="1.6" />
                  </Field.Root>
                </Stack>
              ) : (
                <Stack gap="4">
                  <Text color="fg.muted">
                    Obecny termin: <strong>{formatDate(request.effectiveDueOn)}</strong>. Termin można przedłużyć najpóźniej do{' '}
                    <strong>{formatDate(request.latestExtendedDueOn)}</strong> (2 miesiące od dnia złożenia wniosku).
                  </Text>
                  {errors.form && (
                    <Alert.Root status="error" variant="outline" role="alert">
                      <Alert.Indicator />
                      <Alert.Title>{errors.form}</Alert.Title>
                    </Alert.Root>
                  )}
                  <Field.Root required invalid={!!errors.days} maxW="48">
                    <Field.Label>
                      Liczba dni przedłużenia <Field.RequiredIndicator />
                    </Field.Label>
                    <Input type="number" inputMode="numeric" min={1} max={62} value={days} onChange={(e) => setDays(e.target.value)} />
                    <Field.HelperText>Liczone od terminu ustawowego ({formatDate(request.dueOn)}).</Field.HelperText>
                    <Field.ErrorText>{errors.days}</Field.ErrorText>
                  </Field.Root>
                  <Field.Root required invalid={!!errors.reason}>
                    <Field.Label>
                      Przyczyna przedłużenia <Field.RequiredIndicator />
                    </Field.Label>
                    <Textarea
                      rows={4}
                      maxLength={2000}
                      placeholder="Np. konieczność przeprowadzenia kwerendy w archiwum zakładowym i zebrania danych z kilku wydziałów"
                      value={reason}
                      onChange={(e) => setReason(e.target.value)}
                    />
                    <Field.ErrorText>{errors.reason}</Field.ErrorText>
                  </Field.Root>
                </Stack>
              )}
            </Dialog.Body>
            <Dialog.Footer>
              {result ? (
                <>
                  <CopyButton text={result.noticeText} label="Kopiuj zawiadomienie" />
                  <Button onClick={onClose}>Zamknij</Button>
                </>
              ) : (
                <>
                  <Button variant="outline" onClick={onClose}>
                    Anuluj
                  </Button>
                  <Button type="submit" loading={extend.isPending}>
                    Przedłuż termin
                  </Button>
                </>
              )}
            </Dialog.Footer>
            <Dialog.CloseTrigger asChild>
              <CloseButton size="sm" aria-label="Zamknij" />
            </Dialog.CloseTrigger>
          </Dialog.Content>
        </Dialog.Positioner>
      </Portal>
    </Dialog.Root>
  )
}
