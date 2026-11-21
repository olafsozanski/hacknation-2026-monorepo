import { Button, CloseButton, Dialog, Field, Input, Portal, Stack } from '@chakra-ui/react'
import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/client'
import { notifySuccess } from '@/components/ui/toaster'
import { useChangePassword } from './session'

export function ChangePasswordDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const change = useChangePassword()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})

  const close = () => {
    setCurrentPassword('')
    setNewPassword('')
    setRepeat('')
    setErrors({})
    change.reset()
    onClose()
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (newPassword !== repeat) {
      setErrors({ repeat: 'Hasła nie są takie same.' })
      return
    }
    change.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => {
          notifySuccess('Hasło zostało zmienione')
          close()
        },
        onError: (error) =>
          setErrors(error instanceof ApiError ? error.fieldErrors : { newPassword: 'Nie udało się zmienić hasła.' }),
      },
    )
  }

  return (
    <Dialog.Root open={open} onOpenChange={(e) => !e.open && close()}>
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content as="form" onSubmit={submit}>
            <Dialog.Header>
              <Dialog.Title>Zmiana hasła</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              <Stack gap="4">
                <Field.Root required invalid={!!errors.currentPassword}>
                  <Field.Label>
                    Obecne hasło <Field.RequiredIndicator />
                  </Field.Label>
                  <Input
                    type="password"
                    autoComplete="current-password"
                    value={currentPassword}
                    onChange={(e) => setCurrentPassword(e.target.value)}
                  />
                  <Field.ErrorText>{errors.currentPassword}</Field.ErrorText>
                </Field.Root>
                <Field.Root required invalid={!!errors.newPassword}>
                  <Field.Label>
                    Nowe hasło <Field.RequiredIndicator />
                  </Field.Label>
                  <Input
                    type="password"
                    autoComplete="new-password"
                    minLength={12}
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                  />
                  <Field.HelperText>Co najmniej 12 znaków, w tym wielka i mała litera oraz cyfra.</Field.HelperText>
                  <Field.ErrorText>{errors.newPassword}</Field.ErrorText>
                </Field.Root>
                <Field.Root required invalid={!!errors.repeat}>
                  <Field.Label>
                    Powtórz nowe hasło <Field.RequiredIndicator />
                  </Field.Label>
                  <Input
                    type="password"
                    autoComplete="new-password"
                    value={repeat}
                    onChange={(e) => setRepeat(e.target.value)}
                  />
                  <Field.ErrorText>{errors.repeat}</Field.ErrorText>
                </Field.Root>
              </Stack>
            </Dialog.Body>
            <Dialog.Footer>
              <Button variant="outline" onClick={close}>
                Anuluj
              </Button>
              <Button type="submit" loading={change.isPending}>
                Zmień hasło
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
