import { Alert, Button, CloseButton, Dialog, Field, Input, Portal, RadioCard, Stack, Text, Textarea } from '@chakra-ui/react'
import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/client'
import type { ExtensionKind, ExtensionResult, RequestDetail } from '@/api/types'
import { CopyButton } from '@/components/common/CopyButton'
import { DialogForm } from '@/components/common/DialogForm'
import { FieldErrorText } from '@/components/common/FieldErrorText'
import { formatDate } from '@/lib/dates'
import { collectErrors, focusFirstInvalid, hasErrors, useFieldErrors } from '@/lib/validation'
import { useExtendDeadline } from '../api'

export function ExtendDeadlineDialog({ request, open, onClose }: { request: RequestDetail; open: boolean; onClose: () => void }) {
  const extend = useExtendDeadline(request.id)
  const [kind, setKind] = useState<ExtensionKind>('Query')
  const [days, setDays] = useState('30')
  const [reason, setReason] = useState('')
  const [errors, setErrors, clearErrors] = useFieldErrors()
  const [result, setResult] = useState<ExtensionResult | null>(null)

  const submit = (event: FormEvent<HTMLElement>) => {
    event.preventDefault()
    const value = Number(days)
    const found = collectErrors({
      days: (!days.trim() || !Number.isInteger(value) || value < 1 || value > 62) && 'Podaj liczbę dni od 1 do 62.',
      reason:
        !reason.trim() &&
        (kind === 'Query' ? 'Podaj przyczynę przedłużenia — zostanie wskazana w zawiadomieniu.' : 'Opisz, czego ma dotyczyć doprecyzowanie.'),
    })
    setErrors(found)
    if (hasErrors(found)) {
      focusFirstInvalid(event.currentTarget)
      return
    }
    extend.mutate(
      { days: value, reason: reason.trim(), version: request.version, kind },
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
          <DialogForm onSubmit={submit}>
            <Dialog.Header>
              <Dialog.Title>{result ? 'Termin przedłużony' : 'Przedłużenie terminu odpowiedzi'}</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              {result ? (
                <Stack gap="4">
                  <Alert.Root status="success" variant="subtle">
                    <Alert.Indicator />
                    <Alert.Title>Nowy termin odpowiedzi: {formatDate(result.extendedDueOn)}</Alert.Title>
                  </Alert.Root>
                  <Text>
                    {kind === 'Query'
                      ? 'Zgodnie z art. 13 ust. 2 UDIP powiadom wnioskodawcę o przyczynach opóźnienia i nowym terminie.'
                      : 'Wezwij wnioskodawcę do doprecyzowania wniosku w terminie 7 dni i wskaż nowy termin rozpatrzenia.'}{' '}
                    Poniżej projekt pisma — skopiuj go i wyślij właściwym kanałem.
                  </Text>
                  <Field.Root>
                    <Field.Label>{kind === 'Query' ? 'Projekt zawiadomienia' : 'Projekt wezwania do doprecyzowania'}</Field.Label>
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
                  <RadioCard.Root value={kind} onValueChange={(e) => setKind(e.value as ExtensionKind)} size="sm">
                    <RadioCard.Label>Rodzaj pisma</RadioCard.Label>
                    <Stack direction={{ base: 'column', sm: 'row' }} gap="2">
                      <RadioCard.Item value="Query" flex="1">
                        <RadioCard.ItemHiddenInput />
                        <RadioCard.ItemControl>
                          <RadioCard.ItemContent>
                            <RadioCard.ItemText>Kwerenda</RadioCard.ItemText>
                            <RadioCard.ItemDescription>Potrzeba więcej czasu na zebranie informacji</RadioCard.ItemDescription>
                          </RadioCard.ItemContent>
                          <RadioCard.ItemIndicator />
                        </RadioCard.ItemControl>
                      </RadioCard.Item>
                      <RadioCard.Item value="Clarification" flex="1">
                        <RadioCard.ItemHiddenInput />
                        <RadioCard.ItemControl>
                          <RadioCard.ItemContent>
                            <RadioCard.ItemText>Wezwanie do doprecyzowania</RadioCard.ItemText>
                            <RadioCard.ItemDescription>Zakres wniosku jest niejasny</RadioCard.ItemDescription>
                          </RadioCard.ItemContent>
                          <RadioCard.ItemIndicator />
                        </RadioCard.ItemControl>
                      </RadioCard.Item>
                    </Stack>
                  </RadioCard.Root>
                  <Field.Root required invalid={!!errors.days} maxW="48">
                    <Field.Label>
                      Liczba dni przedłużenia <Field.RequiredIndicator />
                    </Field.Label>
                    <Input type="number" inputMode="numeric" min={1} max={62} value={days} onChange={(e) => {
                        setDays(e.target.value)
                        clearErrors('days')
                      }}
                    />
                    <Field.HelperText>Liczone od terminu ustawowego ({formatDate(request.dueOn)}).</Field.HelperText>
                    <FieldErrorText>{errors.days}</FieldErrorText>
                  </Field.Root>
                  <Field.Root required invalid={!!errors.reason}>
                    <Field.Label>
                      {kind === 'Query' ? 'Przyczyna przedłużenia' : 'Czego ma dotyczyć doprecyzowanie'} <Field.RequiredIndicator />
                    </Field.Label>
                    <Textarea
                      rows={4}
                      maxLength={2000}
                      placeholder={
                        kind === 'Query'
                          ? 'Np. konieczność przeprowadzenia całościowej kwerendy w zakresie wnioskowanych danych'
                          : 'Np. wskazanie, o jakie dokładnie akta sprawy budowlanej chodzi'
                      }
                      value={reason}
                      onChange={(e) => {
                        setReason(e.target.value)
                        clearErrors('reason')
                      }}
                    />
                    <FieldErrorText>{errors.reason}</FieldErrorText>
                  </Field.Root>
                </Stack>
              )}
            </Dialog.Body>
            <Dialog.Footer>
              {result ? (
                <>
                  <CopyButton text={result.noticeText} label="Kopiuj pismo" />
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
          </DialogForm>
        </Dialog.Positioner>
      </Portal>
    </Dialog.Root>
  )
}
