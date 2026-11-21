import { Button, CloseButton, Dialog, Field, Input, Portal, Stack } from '@chakra-ui/react'
import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/client'
import { DialogForm } from '@/components/common/DialogForm'
import { FieldErrorText } from '@/components/common/FieldErrorText'
import { notifySuccess } from '@/components/ui/toaster'
import { collectErrors, focusFirstInvalid, hasErrors, useFieldErrors } from '@/lib/validation'
import { useChangePassword } from './session'

export function ChangePasswordDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const change = useChangePassword()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [errors, setErrors, clearErrors] = useFieldErrors()

  const close = () => {
    setCurrentPassword('')
    setNewPassword('')
    setRepeat('')
    setErrors({})
    change.reset()
    onClose()
  }

  const submit = (event: FormEvent<HTMLElement>) => {
    event.preventDefault()
    const found = collectErrors({
      currentPassword: !currentPassword && 'Podaj obecne hasło.',
      newPassword: !newPassword ? 'Podaj nowe hasło.' : newPassword.length < 12 && 'Hasło musi mieć co najmniej 12 znaków.',
      repeat: !repeat ? 'Powtórz nowe hasło.' : repeat !== newPassword && 'Hasła nie są takie same.',
    })
    setErrors(found)
    if (hasErrors(found)) {
      focusFirstInvalid(event.currentTarget)
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
          <DialogForm onSubmit={submit}>
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
                    onChange={(e) => {
                      setCurrentPassword(e.target.value)
                      clearErrors('currentPassword')
                    }}
                  />
                  <FieldErrorText>{errors.currentPassword}</FieldErrorText>
                </Field.Root>
                <Field.Root required invalid={!!errors.newPassword}>
                  <Field.Label>
                    Nowe hasło <Field.RequiredIndicator />
                  </Field.Label>
                  <Input
                    type="password"
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={(e) => {
                      setNewPassword(e.target.value)
                      clearErrors('newPassword')
                    }}
                  />
                  <Field.HelperText>Co najmniej 12 znaków, w tym wielka i mała litera oraz cyfra.</Field.HelperText>
                  <FieldErrorText>{errors.newPassword}</FieldErrorText>
                </Field.Root>
                <Field.Root required invalid={!!errors.repeat}>
                  <Field.Label>
                    Powtórz nowe hasło <Field.RequiredIndicator />
                  </Field.Label>
                  <Input
                    type="password"
                    autoComplete="new-password"
                    value={repeat}
                    onChange={(e) => {
                      setRepeat(e.target.value)
                      clearErrors('repeat')
                    }}
                  />
                  <FieldErrorText>{errors.repeat}</FieldErrorText>
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
          </DialogForm>
        </Dialog.Positioner>
      </Portal>
    </Dialog.Root>
  )
}
