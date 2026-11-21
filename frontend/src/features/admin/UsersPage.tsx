import { Badge, Button, Checkbox, CloseButton, Dialog, Field, Flex, Grid, Input, Portal, Stack, Table, Text } from '@chakra-ui/react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Pencil, UserPlus } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { ApiError, api } from '@/api/client'
import type { UserListItem } from '@/api/types'
import { PageHeader } from '@/components/common/PageHeader'
import { ErrorState, LoadingState } from '@/components/common/States'
import { notifySuccess } from '@/components/ui/toaster'

const usersKey = ['admin', 'users']

type DialogState = { mode: 'create' } | { mode: 'edit'; user: UserListItem } | { mode: 'password'; user: UserListItem } | null

function UserDialog({ state, onClose }: { state: Exclude<DialogState, null>; onClose: () => void }) {
  const queryClient = useQueryClient()
  const user = state.mode === 'create' ? null : state.user
  const [userName, setUserName] = useState('')
  const [fullName, setFullName] = useState(user?.fullName ?? '')
  const [initials, setInitials] = useState(user?.initials ?? '')
  const [password, setPassword] = useState('')
  const [isAdmin, setIsAdmin] = useState(user?.isAdmin ?? false)
  const [isActive, setIsActive] = useState(user?.isActive ?? true)
  const [errors, setErrors] = useState<Record<string, string>>({})

  const save = useMutation({
    mutationFn: () => {
      if (state.mode === 'create')
        return api('/api/admin/users', { method: 'POST', body: { userName, fullName, initials, password, isAdmin } })
      if (state.mode === 'password')
        return api(`/api/admin/users/${state.user.id}/password`, { method: 'POST', body: { newPassword: password } })
      return api(`/api/admin/users/${state.user.id}`, { method: 'PUT', body: { fullName, initials, isAdmin, isActive } })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: usersKey })
      void queryClient.invalidateQueries({ queryKey: ['users'] })
      notifySuccess(state.mode === 'password' ? 'Ustawiono nowe hasło' : 'Zapisano konto')
      onClose()
    },
    onError: (e) => setErrors(e instanceof ApiError ? { ...e.fieldErrors, form: e.message } : {}),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    save.mutate()
  }

  const title = state.mode === 'create' ? 'Nowe konto' : state.mode === 'password' ? `Nowe hasło: ${user?.fullName}` : `Edycja konta: ${user?.fullName}`

  return (
    <Dialog.Root open onOpenChange={(e) => !e.open && onClose()}>
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content as="form" onSubmit={submit}>
            <Dialog.Header>
              <Dialog.Title>{title}</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              <Stack gap="4">
                {errors.form && !Object.keys(errors).some((k) => k !== 'form') && (
                  <Text color="red.fg" role="alert">
                    {errors.form}
                  </Text>
                )}
                {state.mode === 'create' && (
                  <Field.Root required invalid={!!errors.userName}>
                    <Field.Label>Login</Field.Label>
                    <Input value={userName} onChange={(e) => setUserName(e.target.value)} autoComplete="off" />
                    <Field.ErrorText>{errors.userName}</Field.ErrorText>
                  </Field.Root>
                )}
                {state.mode !== 'password' && (
                  <Grid templateColumns="1fr 8rem" gap="3">
                    <Field.Root required invalid={!!errors.fullName}>
                      <Field.Label>Imię i nazwisko</Field.Label>
                      <Input value={fullName} onChange={(e) => setFullName(e.target.value)} />
                      <Field.ErrorText>{errors.fullName}</Field.ErrorText>
                    </Field.Root>
                    <Field.Root required invalid={!!errors.initials}>
                      <Field.Label>Inicjały</Field.Label>
                      <Input value={initials} maxLength={8} onChange={(e) => setInitials(e.target.value.toUpperCase())} />
                      <Field.ErrorText>{errors.initials}</Field.ErrorText>
                    </Field.Root>
                  </Grid>
                )}
                {state.mode !== 'edit' && (
                  <Field.Root required invalid={!!errors.password || !!errors.newPassword}>
                    <Field.Label>{state.mode === 'create' ? 'Hasło początkowe' : 'Nowe hasło'}</Field.Label>
                    <Input type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
                    <Field.HelperText>Co najmniej 12 znaków, wielka i mała litera oraz cyfra. Przekaż je użytkownikowi bezpiecznym kanałem.</Field.HelperText>
                    <Field.ErrorText>{errors.password ?? errors.newPassword}</Field.ErrorText>
                  </Field.Root>
                )}
                {state.mode !== 'password' && (
                  <Flex gap="6" wrap="wrap">
                    <Checkbox.Root checked={isAdmin} onCheckedChange={(e) => setIsAdmin(e.checked === true)}>
                      <Checkbox.HiddenInput />
                      <Checkbox.Control />
                      <Checkbox.Label>Administrator</Checkbox.Label>
                    </Checkbox.Root>
                    {state.mode === 'edit' && (
                      <Checkbox.Root checked={isActive} onCheckedChange={(e) => setIsActive(e.checked === true)}>
                        <Checkbox.HiddenInput />
                        <Checkbox.Control />
                        <Checkbox.Label>Konto aktywne</Checkbox.Label>
                      </Checkbox.Root>
                    )}
                  </Flex>
                )}
                {errors.isAdmin && <Text color="red.fg">{errors.isAdmin}</Text>}
              </Stack>
            </Dialog.Body>
            <Dialog.Footer>
              <Button variant="outline" onClick={onClose}>
                Anuluj
              </Button>
              <Button type="submit" loading={save.isPending}>
                Zapisz
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

export function UsersPage() {
  const users = useQuery({ queryKey: usersKey, queryFn: () => api<UserListItem[]>('/api/admin/users') })
  const [dialog, setDialog] = useState<DialogState>(null)

  return (
    <>
      <PageHeader
        title="Użytkownicy"
        description="Konta pracowników z dostępem do asystenta. Konta nie są tworzone samodzielnie — nadaje je administrator."
        actions={
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <UserPlus size={16} aria-hidden /> Dodaj konto
          </Button>
        }
      />
      {users.isPending ? (
        <LoadingState />
      ) : users.isError ? (
        <ErrorState error={users.error} onRetry={() => users.refetch()} />
      ) : (
        <Table.ScrollArea borderWidth="1px" borderColor="border" borderRadius="md" bg="bg.panel">
          <Table.Root>
            <Table.Caption srOnly>Konta użytkowników</Table.Caption>
            <Table.Header>
              <Table.Row bg="bg.muted">
                <Table.ColumnHeader>Imię i nazwisko</Table.ColumnHeader>
                <Table.ColumnHeader>Login</Table.ColumnHeader>
                <Table.ColumnHeader>Rola</Table.ColumnHeader>
                <Table.ColumnHeader>Stan</Table.ColumnHeader>
                <Table.ColumnHeader>
                  <Text as="span" srOnly>
                    Akcje
                  </Text>
                </Table.ColumnHeader>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {users.data.map((u) => (
                <Table.Row key={u.id}>
                  <Table.Cell fontWeight="semibold">
                    {u.fullName}{' '}
                    <Text as="span" color="fg.muted" fontWeight="normal">
                      ({u.initials})
                    </Text>
                  </Table.Cell>
                  <Table.Cell>{u.userName}</Table.Cell>
                  <Table.Cell>{u.isAdmin ? <Badge colorPalette="purple">Administrator</Badge> : <Badge variant="outline">Urzędnik</Badge>}</Table.Cell>
                  <Table.Cell>
                    {!u.isActive ? (
                      <Badge>Nieaktywne</Badge>
                    ) : u.isLockedOut ? (
                      <Badge colorPalette="red">Zablokowane</Badge>
                    ) : (
                      <Badge colorPalette="green" variant="subtle">
                        Aktywne
                      </Badge>
                    )}
                  </Table.Cell>
                  <Table.Cell textAlign="end" whiteSpace="nowrap">
                    <Button size="xs" variant="ghost" onClick={() => setDialog({ mode: 'edit', user: u })}>
                      <Pencil size={14} aria-hidden /> Edytuj
                    </Button>
                    <Button size="xs" variant="ghost" onClick={() => setDialog({ mode: 'password', user: u })}>
                      <KeyRound size={14} aria-hidden /> Hasło
                    </Button>
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table.Root>
        </Table.ScrollArea>
      )}
      {dialog && <UserDialog state={dialog} onClose={() => setDialog(null)} />}
    </>
  )
}
