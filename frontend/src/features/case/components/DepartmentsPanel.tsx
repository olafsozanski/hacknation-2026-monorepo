import { Badge, Box, Button, Flex, IconButton, Link, Stack, Table, Text } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { Mail, Pencil, ScrollText, Undo2 } from 'lucide-react'
import { useState } from 'react'
import type { RequestDepartment, RequestDetail } from '@/api/types'
import { Panel } from '@/components/common/Panel'
import { DepartmentSuggestionList } from '@/components/domain/DepartmentSuggestionList'
import { notifyError } from '@/components/ui/toaster'
import { days, formatDate, parseIsoDate, todayIso } from '@/lib/dates'
import { departmentSuggestionsQuery, useSetDepartments, useUpdateRequestDepartment } from '../api'
import { DepartmentQueryDialog } from './DepartmentQueryDialog'
import { SelectDepartmentsDialog } from './SelectDepartmentsDialog'

function waitingDays(askedOn: string) {
  return Math.max(0, Math.round((parseIsoDate(todayIso()).getTime() - parseIsoDate(askedOn).getTime()) / 86_400_000))
}

function SuggestedDepartments({ request }: { request: RequestDetail }) {
  const suggestions = useQuery(departmentSuggestionsQuery(request.id))
  const assign = useSetDepartments(request.id)
  const [selected, setSelected] = useState<string[]>([])

  if (suggestions.isPending) return <Text color="fg.muted">Szukam wydziałów właściwych dla wniosku…</Text>
  if (!suggestions.data?.length) return <Text color="fg.muted">Brak sugestii. Wybierz wydziały ręcznie.</Text>

  return (
    <Stack gap="3">
      <Text fontWeight="semibold">Sugerowana ścieżka</Text>
      <DepartmentSuggestionList
        suggestions={suggestions.data}
        selected={selected}
        onToggle={(id, checked) => setSelected((ids) => (checked ? [...ids, id] : ids.filter((x) => x !== id)))}
      />
      <Box>
        <Button
          disabled={selected.length === 0}
          loading={assign.isPending}
          onClick={() => assign.mutate(selected, { onError: (e) => notifyError(e) })}
        >
          Przypisz zaznaczone ({selected.length})
        </Button>
      </Box>
    </Stack>
  )
}

function DepartmentRow({ request, department }: { request: RequestDetail; department: RequestDepartment }) {
  const update = useUpdateRequestDepartment(request.id)
  const set = (askedOn: string | null, answeredOn: string | null) =>
    update.mutate({ departmentId: department.departmentId, askedOn, answeredOn }, { onError: (e) => notifyError(e) })
  const mailto = department.email
    ? `mailto:${department.email}?subject=${encodeURIComponent(`Wniosek o udostępnienie informacji publicznej – ${request.caseNumber}`)}`
    : null

  return (
    <Table.Row>
      <Table.Cell>
        <Text fontWeight="bold">{department.symbol}</Text>
        <Text fontSize="sm" color="fg.muted">
          {department.name}
        </Text>
      </Table.Cell>
      <Table.Cell whiteSpace="nowrap">
        {department.askedOn ? (
          <Flex align="center" gap="1">
            <Text>{formatDate(department.askedOn)}</Text>
            <IconButton
              size="xs"
              variant="ghost"
              aria-label={`Cofnij oznaczenie wysłania zapytania do ${department.symbol}`}
              onClick={() => set(null, department.answeredOn)}
            >
              <Undo2 size={14} aria-hidden />
            </IconButton>
          </Flex>
        ) : (
          <Button size="xs" variant="outline" loading={update.isPending} onClick={() => set(todayIso(), department.answeredOn)}>
            Oznacz jako wysłane
          </Button>
        )}
      </Table.Cell>
      <Table.Cell whiteSpace="nowrap">
        {department.answeredOn ? (
          <Badge colorPalette="green" variant="subtle">
            Otrzymano {formatDate(department.answeredOn)}
          </Badge>
        ) : department.askedOn ? (
          <Badge colorPalette="orange" variant="subtle">
            Oczekuje · {days(waitingDays(department.askedOn))}
          </Badge>
        ) : (
          <Text color="fg.subtle">—</Text>
        )}
      </Table.Cell>
      <Table.Cell textAlign="end">
        {mailto && (
          <Link href={mailto} display="inline-flex" alignItems="center" gap="1" color="brand.fg" fontSize="sm" textDecoration="underline">
            <Mail size={14} aria-hidden /> E-mail
            <Box as="span" srOnly>
              {` do ${department.name}`}
            </Box>
          </Link>
        )}
      </Table.Cell>
    </Table.Row>
  )
}

export function DepartmentsPanel({ request }: { request: RequestDetail }) {
  const [queryOpen, setQueryOpen] = useState(false)
  const [selecting, setSelecting] = useState(false)
  const hasDepartments = request.departments.length > 0

  return (
    <Panel
      id="wydzialy"
      step={1}
      title="Zapytania do wydziałów"
      description="Ustal, które komórki przygotują informacje, i oznacz wysłanie zapytań."
      padded={!hasDepartments}
      actions={
        <>
          <Button size="sm" variant="ghost" onClick={() => setSelecting(true)}>
            <Pencil size={14} aria-hidden /> {hasDepartments ? 'Zmień wydziały' : 'Wybierz ręcznie'}
          </Button>
          {hasDepartments && (
            <Button size="sm" variant="outline" onClick={() => setQueryOpen(true)}>
              <ScrollText size={14} aria-hidden /> Treść zapytania
            </Button>
          )}
        </>
      }
    >
      {hasDepartments ? (
        <Table.ScrollArea>
          <Table.Root size="sm">
            <Table.Header>
              <Table.Row bg="bg.muted">
                <Table.ColumnHeader ps={{ base: 4, md: 5 }}>Wydział / biuro</Table.ColumnHeader>
                <Table.ColumnHeader>Zapytanie wysłane</Table.ColumnHeader>
                <Table.ColumnHeader>Odpowiedź</Table.ColumnHeader>
                <Table.ColumnHeader pe={{ base: 4, md: 5 }}>
                  <Box as="span" srOnly>
                    Kontakt
                  </Box>
                </Table.ColumnHeader>
              </Table.Row>
            </Table.Header>
            <Table.Body css={{ '& td:first-of-type': { ps: { base: 4, md: 5 } }, '& td:last-of-type': { pe: { base: 4, md: 5 } } }}>
              {request.departments.map((d) => (
                <DepartmentRow key={d.departmentId} request={request} department={d} />
              ))}
            </Table.Body>
          </Table.Root>
        </Table.ScrollArea>
      ) : (
        <SuggestedDepartments request={request} />
      )}
      {queryOpen && <DepartmentQueryDialog request={request} open onClose={() => setQueryOpen(false)} />}
      {selecting && <SelectDepartmentsDialog request={request} open onClose={() => setSelecting(false)} />}
    </Panel>
  )
}
