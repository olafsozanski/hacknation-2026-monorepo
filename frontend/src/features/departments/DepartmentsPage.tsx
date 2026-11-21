import { Badge, Box, Button, CloseButton, Collapsible, Dialog, Field, Flex, Input, InputGroup, Link, Portal, Stack, Switch, Text, Textarea } from '@chakra-ui/react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, Mail, Pencil, Search } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { ApiError, api } from '@/api/client'
import type { Department } from '@/api/types'
import { PageHeader } from '@/components/common/PageHeader'
import { ErrorState, LoadingState } from '@/components/common/States'
import { notifySuccess } from '@/components/ui/toaster'
import { useCurrentUser } from '@/features/auth/session'
import { departmentsQuery, useDepartments } from '@/features/platform/api'

function EditDepartmentDialog({ department, onClose }: { department: Department; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(department.name)
  const [scope, setScope] = useState(department.scope)
  const [email, setEmail] = useState(department.email ?? '')
  const [isActive, setIsActive] = useState(department.isActive)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useMutation({
    mutationFn: () =>
      api<void>(`/api/admin/departments/${department.id}`, {
        method: 'PUT',
        body: { name: name.trim(), scope: scope.trim(), email: email.trim() || null, isActive },
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: departmentsQuery.queryKey })
      notifySuccess('Zapisano dane wydziału')
      onClose()
    },
    onError: (e) => setErrors(e instanceof ApiError ? { ...e.fieldErrors, form: e.message } : {}),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    save.mutate()
  }

  return (
    <Dialog.Root open onOpenChange={(e) => !e.open && onClose()} size="lg" scrollBehavior="inside">
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content as="form" onSubmit={submit}>
            <Dialog.Header>
              <Dialog.Title>
                {department.symbol} — edycja
              </Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              <Stack gap="4">
                <Field.Root required invalid={!!errors.name}>
                  <Field.Label>Nazwa</Field.Label>
                  <Input value={name} onChange={(e) => setName(e.target.value)} />
                  <Field.ErrorText>{errors.name}</Field.ErrorText>
                </Field.Root>
                <Field.Root invalid={!!errors.email}>
                  <Field.Label>E-mail</Field.Label>
                  <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
                  <Field.ErrorText>{errors.email}</Field.ErrorText>
                </Field.Root>
                <Field.Root>
                  <Field.Label>Zakres działania</Field.Label>
                  <Textarea rows={10} value={scope} onChange={(e) => setScope(e.target.value)} fontSize="sm" />
                  <Field.HelperText>Na podstawie zakresu asystent dopasowuje wydział do treści wniosku.</Field.HelperText>
                </Field.Root>
                <Switch.Root checked={isActive} onCheckedChange={(e) => setIsActive(e.checked)}>
                  <Switch.HiddenInput />
                  <Switch.Control />
                  <Switch.Label>Aktywny (proponowany w sprawach)</Switch.Label>
                </Switch.Root>
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

export function DepartmentsPage() {
  const user = useCurrentUser()
  const departments = useDepartments()
  const [filter, setFilter] = useState('')
  const [editing, setEditing] = useState<Department | null>(null)

  const visible = useMemo(() => {
    const needle = filter.trim().toLowerCase()
    const all = departments.data ?? []
    return needle ? all.filter((d) => `${d.symbol} ${d.name} ${d.scope}`.toLowerCase().includes(needle)) : all
  }, [departments.data, filter])

  return (
    <>
      <PageHeader
        title="Wydziały i biura"
        description="Komórki organizacyjne urzędu wraz z zakresem działania (na podstawie BIP). Pomagają ustalić, kto przygotuje informacje do odpowiedzi."
      />
      <InputGroup startElement={<Search size={16} aria-hidden />} mb="4" maxW="lg">
        <Input
          type="search"
          aria-label="Szukaj wydziału po nazwie, symbolu lub zakresie działania"
          placeholder="Szukaj po nazwie, symbolu lub zakresie działania"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
      </InputGroup>
      {departments.isPending ? (
        <LoadingState />
      ) : departments.isError ? (
        <ErrorState error={departments.error} onRetry={() => departments.refetch()} />
      ) : (
        <Stack as="ul" gap="2" m="0" p="0" listStyleType="none">
          {visible.map((d) => (
            <Box as="li" key={d.id} bg="bg.panel" borderWidth="1px" borderColor="border" borderRadius="md" px="4" py="3">
              <Collapsible.Root>
                <Flex gap="3" align="center" wrap="wrap">
                  <Badge variant="solid" colorPalette={d.kind === 'Department' ? 'brand' : 'gray'} minW="14" justifyContent="center">
                    {d.symbol}
                  </Badge>
                  <Box flex="1" minW="48">
                    <Text fontWeight="semibold">{d.name}</Text>
                    {!d.isActive && (
                      <Text fontSize="xs" color="fg.muted">
                        nieaktywny
                      </Text>
                    )}
                  </Box>
                  {d.email && (
                    <Link href={`mailto:${d.email}`} color="brand.fg" fontSize="sm" display="inline-flex" alignItems="center" gap="1">
                      <Mail size={14} aria-hidden /> {d.email}
                    </Link>
                  )}
                  <Collapsible.Trigger asChild>
                    <Button size="xs" variant="ghost">
                      <ChevronDown size={14} aria-hidden /> Zakres działania
                    </Button>
                  </Collapsible.Trigger>
                  {user.isAdmin && (
                    <Button size="xs" variant="ghost" onClick={() => setEditing(d)} aria-label={`Edytuj ${d.name}`}>
                      <Pencil size={14} aria-hidden />
                    </Button>
                  )}
                </Flex>
                <Collapsible.Content>
                  <Text mt="3" fontSize="sm" color="fg.muted" lineHeight="1.7">
                    {d.scope || 'Brak opisu zakresu działania.'}
                  </Text>
                </Collapsible.Content>
              </Collapsible.Root>
            </Box>
          ))}
        </Stack>
      )}
      {editing && <EditDepartmentDialog department={editing} onClose={() => setEditing(null)} />}
    </>
  )
}
