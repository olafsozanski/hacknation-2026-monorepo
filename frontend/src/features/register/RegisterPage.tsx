import { Button, Flex, NativeSelect, Stack, Tag, Text } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { FilePlus2, SearchX } from 'lucide-react'
import { useCallback, useMemo } from 'react'
import { Link as RouterLink, useSearchParams } from 'react-router'
import type { RequestStatus } from '@/api/types'
import { PageHeader } from '@/components/common/PageHeader'
import { EmptyHint, ErrorState, LoadingState } from '@/components/common/States'
import { useCurrentUser } from '@/features/auth/session'
import { useUsers } from '@/features/platform/api'
import { formatDate, plural } from '@/lib/dates'
import { statusOrder } from '@/lib/labels'
import { registerSummaryQuery, registerYearsQuery, requestListQuery } from './api'
import { DeadlineCalendar } from './components/DeadlineCalendar'
import { RegisterToolbar, type RegisterView } from './components/RegisterToolbar'
import { RequestTable } from './components/RequestTable'
import { SummaryTiles } from './components/SummaryTiles'
import { isQuickFilter, quickFilterPredicates, type QuickFilter } from './quickFilters'

const currentYear = new Date().getFullYear()

export function RegisterPage() {
  const user = useCurrentUser()
  const [params, setParams] = useSearchParams()
  const { data: users = [] } = useUsers()

  const year = Number(params.get('rok')) || currentYear
  const statusParam = params.get('status') ?? ''
  const status = (statusOrder as string[]).includes(statusParam) ? (statusParam as RequestStatus) : ''
  const assignee = params.get('prowadzacy') ?? ''
  const search = params.get('szukaj') ?? ''
  const dueOn = params.get('termin') ?? ''
  const view: RegisterView = params.get('widok') === 'kalendarz' ? 'kalendarz' : 'tabela'
  const quickParam = params.get('filtr')
  const quick: QuickFilter | null = isQuickFilter(quickParam) ? quickParam : null

  const update = useCallback(
    (changes: Record<string, string | null>) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          for (const [key, value] of Object.entries(changes)) {
            if (value) next.set(key, value)
            else next.delete(key)
          }
          return next
        },
        { replace: true },
      ),
    [setParams],
  )

  const filters = {
    year,
    status: status || undefined,
    assigneeId: assignee === 'moje' ? user.id : assignee || undefined,
    search: search || undefined,
    dueOn: dueOn || undefined,
  }
  const list = useQuery(requestListQuery(filters))
  const summary = useQuery(registerSummaryQuery(year))
  const years = useQuery(registerYearsQuery)

  const items = useMemo(
    () => (quick ? (list.data ?? []).filter(quickFilterPredicates[quick]) : (list.data ?? [])),
    [list.data, quick],
  )
  const hasFilters = Boolean(status || assignee || search || dueOn || quick)
  const onSearch = useCallback((value: string) => update({ szukaj: value || null }), [update])

  return (
    <>
      <PageHeader
        title="Rejestr wniosków"
        description="Wnioski o udostępnienie informacji publicznej. Terminy liczone są zgodnie z art. 13 ustawy o dostępie do informacji publicznej i art. 57 KPA."
        actions={
          <>
            <NativeSelect.Root width="auto" size="md">
              <NativeSelect.Field
                aria-label="Rok rejestru"
                value={year}
                onChange={(e) => update({ rok: e.target.value === String(currentYear) ? null : e.target.value })}
              >
                {(years.data ?? [currentYear]).map((y) => (
                  <option key={y} value={y}>
                    Rok {y}
                  </option>
                ))}
              </NativeSelect.Field>
              <NativeSelect.Indicator />
            </NativeSelect.Root>
            <Button asChild hideFrom="lg">
              <RouterLink to="/wnioski/nowy">
                <FilePlus2 size={18} aria-hidden /> Zarejestruj wniosek
              </RouterLink>
            </Button>
          </>
        }
      />
      <Stack gap="4">
        <SummaryTiles
          summary={summary.data}
          active={quick}
          onToggle={(filter) => update({ filtr: quick === filter ? null : filter })}
        />
        <RegisterToolbar
          search={search}
          status={status}
          assignee={assignee}
          view={view}
          users={users}
          hasFilters={hasFilters}
          onSearch={onSearch}
          onStatus={(value) => update({ status: value || null })}
          onAssignee={(value) => update({ prowadzacy: value || null })}
          onView={(value) => update({ widok: value === 'tabela' ? null : value })}
          onClear={() => update({ status: null, prowadzacy: null, szukaj: null, termin: null, filtr: null })}
        />
        <Flex justify="space-between" align="center" gap="3" wrap="wrap" minH="8">
          <Text color="fg.muted" fontSize="sm" role="status" aria-live="polite">
            {list.data && `Wyświetlono ${items.length} ${plural(items.length, 'wniosek', 'wnioski', 'wniosków')}`}
          </Text>
          {dueOn && (
            <Tag.Root size="lg" variant="subtle" colorPalette="brand">
              <Tag.Label>Termin: {formatDate(dueOn)}</Tag.Label>
              <Tag.EndElement>
                <Tag.CloseTrigger aria-label="Usuń filtr terminu" onClick={() => update({ termin: null })} />
              </Tag.EndElement>
            </Tag.Root>
          )}
        </Flex>
        {list.isPending ? (
          <LoadingState label="Wczytywanie rejestru…" />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : view === 'kalendarz' ? (
          <DeadlineCalendar items={items} onShowDay={(day) => update({ termin: day, widok: null })} />
        ) : items.length === 0 ? (
          <EmptyHint
            icon={<SearchX aria-hidden />}
            title={hasFilters ? 'Brak wniosków spełniających kryteria' : 'Rejestr jest pusty'}
            description={hasFilters ? 'Zmień lub wyczyść filtry.' : 'Zarejestruj pierwszy wniosek, przesyłając plik PDF lub DOCX.'}
          />
        ) : (
          <RequestTable items={items} caption={`Rejestr wniosków ${year}`} />
        )}
      </Stack>
    </>
  )
}
