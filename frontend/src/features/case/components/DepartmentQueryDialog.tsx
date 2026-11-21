import { Alert, Button, CloseButton, Dialog, Field, Input, Portal, Stack, Textarea } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { Send } from 'lucide-react'
import type { RequestDetail } from '@/api/types'
import { CopyButton } from '@/components/common/CopyButton'
import { ErrorState, LoadingState } from '@/components/common/States'
import { notifyError, notifySuccess } from '@/components/ui/toaster'
import { formatDate, todayIso } from '@/lib/dates'
import { departmentQueryQuery, useUpdateRequestDepartment } from '../api'

export function DepartmentQueryDialog({ request, open, onClose }: { request: RequestDetail; open: boolean; onClose: () => void }) {
  const memo = useQuery({ ...departmentQueryQuery(request.id), enabled: open })
  const update = useUpdateRequestDepartment(request.id)
  const pending = request.departments.filter((d) => !d.askedOn)
  const recipients = request.departments.map((d) => d.email).filter(Boolean).join('; ')

  const markAllSent = async () => {
    try {
      for (const department of pending) {
        await update.mutateAsync({ departmentId: department.departmentId, askedOn: todayIso(), answeredOn: department.answeredOn })
      }
      notifySuccess('Oznaczono zapytania jako wysłane')
      onClose()
    } catch (error) {
      notifyError(error)
    }
  }

  return (
    <Dialog.Root open={open} onOpenChange={(e) => !e.open && onClose()} size="xl" scrollBehavior="inside">
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content>
            <Dialog.Header>
              <Dialog.Title>Zapytanie do wydziałów</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              <Stack gap="4">
                <Alert.Root status="info" variant="subtle">
                  <Alert.Indicator />
                  <Alert.Description>
                    Asystent nie wysyła wiadomości. Skopiuj treść do poczty elektronicznej lub systemu EZD, a po wysłaniu
                    oznacz zapytania jako wysłane.
                  </Alert.Description>
                </Alert.Root>
                {memo.isPending ? (
                  <LoadingState label="Przygotowuję treść zapytania…" />
                ) : memo.isError ? (
                  <ErrorState error={memo.error} onRetry={() => memo.refetch()} />
                ) : (
                  <>
                    {recipients && (
                      <Field.Root>
                        <Field.Label>Adresaci</Field.Label>
                        <Input readOnly value={recipients} />
                      </Field.Root>
                    )}
                    <Field.Root>
                      <Field.Label>Temat</Field.Label>
                      <Input readOnly value={memo.data.subject} />
                    </Field.Root>
                    <Field.Root>
                      <Field.Label>Treść</Field.Label>
                      <Textarea readOnly value={memo.data.body} rows={14} fontSize="sm" lineHeight="1.6" />
                      <Field.HelperText>Prośba o odpowiedź do {formatDate(memo.data.replyBy)}.</Field.HelperText>
                    </Field.Root>
                  </>
                )}
              </Stack>
            </Dialog.Body>
            <Dialog.Footer flexWrap="wrap">
              {memo.data && (
                <>
                  <CopyButton text={memo.data.subject} label="Kopiuj temat" size="sm" />
                  <CopyButton text={memo.data.body} label="Kopiuj treść" size="sm" />
                </>
              )}
              {pending.length > 0 && (
                <Button size="sm" onClick={markAllSent} loading={update.isPending}>
                  <Send size={14} aria-hidden /> Oznacz jako wysłane ({pending.length})
                </Button>
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
