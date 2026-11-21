import { Alert, Button, CloseButton, Dialog, Field, Portal, Stack, Textarea } from '@chakra-ui/react'
import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/client'
import type { RequestDetail } from '@/api/types'
import { RequestFormFields, type RequestFormValues } from '@/components/domain/RequestFormFields'
import { notifySuccess } from '@/components/ui/toaster'
import { useUsers } from '@/features/platform/api'
import { useUpdateRequest } from '../api'

function toValues(request: RequestDetail): RequestFormValues {
  return {
    subject: request.subject,
    requesterName: request.requesterName ?? '',
    requesterContact: request.requesterContact ?? '',
    replyChannel: request.replyChannel,
    receivedOn: request.receivedOn,
    dueOn: request.dueOn,
    caseNumber: request.caseNumber,
    assigneeId: request.assignee?.id ?? '',
  }
}

export function EditRequestDialog({ request, open, onClose }: { request: RequestDetail; open: boolean; onClose: () => void }) {
  const update = useUpdateRequest(request.id)
  const { data: users = [] } = useUsers()
  const [values, setValues] = useState(() => toValues(request))
  const [content, setContent] = useState(request.content)
  const [errors, setErrors] = useState<Record<string, string>>({})

  const submit = (event: FormEvent) => {
    event.preventDefault()
    update.mutate(
      {
        subject: values.subject.trim(),
        content: content.trim() || null,
        requesterName: values.requesterName.trim() || null,
        requesterContact: values.requesterContact.trim() || null,
        replyChannel: values.replyChannel,
        receivedOn: values.receivedOn,
        dueOn: values.dueOn,
        caseNumber: values.caseNumber.trim(),
        assigneeId: values.assigneeId || null,
        version: request.version,
      },
      {
        onSuccess: () => {
          notifySuccess('Zapisano zmiany')
          onClose()
        },
        onError: (error) =>
          setErrors(error instanceof ApiError ? { ...error.fieldErrors, form: error.message } : { form: 'Nie udało się zapisać zmian.' }),
      },
    )
  }

  return (
    <Dialog.Root open={open} onOpenChange={(e) => !e.open && onClose()} size="xl" scrollBehavior="inside">
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content as="form" onSubmit={submit}>
            <Dialog.Header>
              <Dialog.Title>Edycja danych sprawy</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              <Stack gap="4">
                {errors.form && (
                  <Alert.Root status="error" variant="outline" role="alert">
                    <Alert.Indicator />
                    <Alert.Title>{errors.form}</Alert.Title>
                  </Alert.Root>
                )}
                <RequestFormFields values={values} onChange={(c) => setValues((v) => ({ ...v, ...c }))} errors={errors} users={users} />
                <Field.Root>
                  <Field.Label>Treść wniosku</Field.Label>
                  <Textarea rows={8} value={content} onChange={(e) => setContent(e.target.value)} fontSize="sm" />
                </Field.Root>
              </Stack>
            </Dialog.Body>
            <Dialog.Footer>
              <Button variant="outline" onClick={onClose}>
                Anuluj
              </Button>
              <Button type="submit" loading={update.isPending}>
                Zapisz zmiany
              </Button>
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
