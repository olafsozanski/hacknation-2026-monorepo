import { Badge, Button, Field, Flex, Input, NativeSelect, Stack, Table, Text } from '@chakra-ui/react'
import { useInfiniteQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api, query } from '@/api/client'
import type { AuditEntry } from '@/api/types'
import { PageHeader } from '@/components/common/PageHeader'
import { EmptyHint, ErrorState, LoadingState } from '@/components/common/States'
import { formatDateTime } from '@/lib/dates'
import { auditActionLabels } from '@/lib/labels'

const pageSize = 100

const categories = [
  { value: '', label: 'Wszystkie zdarzenia' },
  { value: 'auth.', label: 'Logowanie i sesje' },
  { value: 'user.', label: 'Konta użytkowników' },
  { value: 'request.', label: 'Sprawy' },
  { value: 'document.', label: 'Dokumenty' },
  { value: 'draft.', label: 'Projekty odpowiedzi' },
  { value: 'anonymization.', label: 'Anonimizacja' },
  { value: 'knowledge.', label: 'Baza wiedzy' },
]

const alarming = new Set(['auth.login-failed', 'auth.locked-out', 'user.role-changed', 'user.deactivated', 'document.deleted'])

export function AuditPage() {
  const [action, setAction] = useState('')
  const [userName, setUserName] = useState('')
  const [appliedUser, setAppliedUser] = useState('')

  const entries = useInfiniteQuery({
    queryKey: ['admin', 'audit', action, appliedUser],
    queryFn: ({ pageParam }) =>
      api<AuditEntry[]>(`/api/admin/audit${query({ action, userName: appliedUser, take: pageSize, before: pageParam })}`),
    initialPageParam: undefined as number | undefined,
    getNextPageParam: (last) => (last.length === pageSize ? last[last.length - 1].id : undefined),
  })
  const rows = entries.data?.pages.flat() ?? []

  return (
    <>
      <PageHeader
        title="Dziennik zdarzeń"
        description="Rejestr działań w systemie: logowania, zmiany uprawnień, operacje na sprawach i dokumentach. Wpisów nie można zmieniać ani usuwać."
      />
      <Stack gap="4">
        <Flex
          as="form"
          gap="3"
          wrap="wrap"
          align="flex-end"
          bg="bg.panel"
          borderWidth="1px"
          borderColor="border"
          borderRadius="md"
          p="3"
          role="search"
          aria-label="Filtrowanie dziennika"
          onSubmit={(e) => {
            e.preventDefault()
            setAppliedUser(userName.trim())
          }}
        >
          <Field.Root w="auto" minW="56">
            <Field.Label fontSize="sm">Rodzaj zdarzeń</Field.Label>
            <NativeSelect.Root>
              <NativeSelect.Field value={action} onChange={(e) => setAction(e.target.value)}>
                {categories.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </NativeSelect.Field>
              <NativeSelect.Indicator />
            </NativeSelect.Root>
          </Field.Root>
          <Field.Root w="auto" minW="56">
            <Field.Label fontSize="sm">Użytkownik (login)</Field.Label>
            <Input value={userName} onChange={(e) => setUserName(e.target.value)} placeholder="np. akowalska" />
          </Field.Root>
          <Button type="submit" variant="outline">
            Filtruj
          </Button>
        </Flex>

        {entries.isPending ? (
          <LoadingState />
        ) : entries.isError ? (
          <ErrorState error={entries.error} onRetry={() => entries.refetch()} />
        ) : rows.length === 0 ? (
          <EmptyHint title="Brak zdarzeń" description="Zmień filtry, aby zobaczyć inne wpisy." />
        ) : (
          <>
            <Table.ScrollArea borderWidth="1px" borderColor="border" borderRadius="md" bg="bg.panel">
              <Table.Root size="sm">
                <Table.Caption srOnly>Dziennik zdarzeń</Table.Caption>
                <Table.Header>
                  <Table.Row bg="bg.muted">
                    <Table.ColumnHeader>Data i godzina</Table.ColumnHeader>
                    <Table.ColumnHeader>Użytkownik</Table.ColumnHeader>
                    <Table.ColumnHeader>Zdarzenie</Table.ColumnHeader>
                    <Table.ColumnHeader>Szczegóły</Table.ColumnHeader>
                  </Table.Row>
                </Table.Header>
                <Table.Body>
                  {rows.map((entry) => (
                    <Table.Row key={entry.id}>
                      <Table.Cell whiteSpace="nowrap" fontVariantNumeric="tabular-nums">
                        {formatDateTime(entry.at)}
                      </Table.Cell>
                      <Table.Cell>{entry.userName}</Table.Cell>
                      <Table.Cell>
                        {alarming.has(entry.action) ? (
                          <Badge colorPalette="orange" variant="subtle">
                            {auditActionLabels[entry.action] ?? entry.action}
                          </Badge>
                        ) : (
                          (auditActionLabels[entry.action] ?? entry.action)
                        )}
                      </Table.Cell>
                      <Table.Cell color="fg.muted">{entry.details ?? '—'}</Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table.Root>
            </Table.ScrollArea>
            <Flex justify="space-between" align="center">
              <Text fontSize="sm" color="fg.muted">
                Wyświetlono {rows.length} wpisów
              </Text>
              {entries.hasNextPage && (
                <Button variant="outline" size="sm" onClick={() => entries.fetchNextPage()} loading={entries.isFetchingNextPage}>
                  Wczytaj starsze
                </Button>
              )}
            </Flex>
          </>
        )}
      </Stack>
    </>
  )
}
