import { Avatar, Box, Flex, HStack, Link, Table, Tag, Text, chakra } from '@chakra-ui/react'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link as RouterLink, useNavigate } from 'react-router'
import type { RequestListItem } from '@/api/types'
import { DeadlineBadge } from '@/components/common/DeadlineBadge'
import { urgencyRowStyle } from '@/components/common/urgency'
import { StatusBadge } from '@/components/common/StatusBadge'
import { formatDate } from '@/lib/dates'
import { statusOrder } from '@/lib/labels'

type SortKey = 'sequenceNumber' | 'receivedOn' | 'effectiveDueOn' | 'status'
type Sort = { key: SortKey; direction: 'asc' | 'desc' } | null

const comparators: Record<SortKey, (a: RequestListItem, b: RequestListItem) => number> = {
  sequenceNumber: (a, b) => a.sequenceNumber - b.sequenceNumber,
  receivedOn: (a, b) => a.receivedOn.localeCompare(b.receivedOn) || a.sequenceNumber - b.sequenceNumber,
  effectiveDueOn: (a, b) => a.effectiveDueOn.localeCompare(b.effectiveDueOn) || a.sequenceNumber - b.sequenceNumber,
  status: (a, b) => statusOrder.indexOf(a.status) - statusOrder.indexOf(b.status),
}

function SortHeader({ label, sortKey, sort, onSort }: { label: string; sortKey: SortKey; sort: Sort; onSort: (key: SortKey) => void }) {
  const active = sort?.key === sortKey
  const Icon = !active ? ArrowUpDown : sort.direction === 'asc' ? ArrowUp : ArrowDown
  return (
    <Table.ColumnHeader aria-sort={active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'} whiteSpace="nowrap">
      <chakra.button
        type="button"
        onClick={() => onSort(sortKey)}
        display="inline-flex"
        alignItems="center"
        gap="1"
        fontWeight="inherit"
        color="inherit"
        borderRadius="xs"
        _hover={{ color: 'brand.fg' }}
      >
        {label}
        <Icon size={14} aria-hidden opacity={active ? 1 : 0.5} />
      </chakra.button>
    </Table.ColumnHeader>
  )
}

export function RequestTable({ items, caption }: { items: RequestListItem[]; caption: string }) {
  const navigate = useNavigate()
  const [sort, setSort] = useState<Sort>(null)

  const sorted = useMemo(() => {
    if (!sort) return items
    const compare = comparators[sort.key]
    return [...items].sort((a, b) => (sort.direction === 'asc' ? compare(a, b) : compare(b, a)))
  }, [items, sort])

  const toggleSort = (key: SortKey) =>
    setSort((current) =>
      current?.key !== key ? { key, direction: 'asc' } : current.direction === 'asc' ? { key, direction: 'desc' } : null,
    )

  return (
    <Table.ScrollArea borderWidth="1px" borderColor="border" borderRadius="md" bg="bg.panel">
      <Table.Root size="md" interactive stickyHeader>
        <Table.Caption srOnly>{caption}</Table.Caption>
        <Table.Header>
          <Table.Row bg="bg.muted">
            <SortHeader label="Lp." sortKey="sequenceNumber" sort={sort} onSort={toggleSort} />
            <Table.ColumnHeader>Znak sprawy i przedmiot wniosku</Table.ColumnHeader>
            <SortHeader label="Wpłynął" sortKey="receivedOn" sort={sort} onSort={toggleSort} />
            <SortHeader label="Termin odpowiedzi" sortKey="effectiveDueOn" sort={sort} onSort={toggleSort} />
            <SortHeader label="Status" sortKey="status" sort={sort} onSort={toggleSort} />
            <Table.ColumnHeader hideBelow="xl">Wydziały</Table.ColumnHeader>
            <Table.ColumnHeader textAlign="center">Prowadzi</Table.ColumnHeader>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {sorted.map((item) => {
            const highlight = urgencyRowStyle[item.urgency]
            return (
              <Table.Row
                key={item.id}
                onClick={(event) => {
                  if ((event.target as HTMLElement).closest('a, button')) return
                  navigate(`/wnioski/${item.id}`)
                }}
                cursor="pointer"
                bg={highlight ? highlight.bg : undefined}
                _hover={{ bg: highlight ? highlight.bg : 'bg.subtle', filter: highlight ? 'brightness(0.98)' : undefined }}
              >
                <Table.Cell
                  fontVariantNumeric="tabular-nums"
                  color="fg.muted"
                  boxShadow={highlight ? `inset 4px 0 0 var(--chakra-colors-${highlight.accent.replace('.', '-')})` : undefined}
                  w="16"
                >
                  {item.sequenceNumber}
                </Table.Cell>
                <Table.Cell minW="80" maxW="xl">
                  <Text fontSize="xs" color="fg.muted" fontWeight="semibold" letterSpacing="wide">
                    {item.caseNumber}
                  </Text>
                  <Link asChild fontWeight="semibold" color="fg" lineClamp={2} _hover={{ color: 'brand.fg', textDecoration: 'underline' }}>
                    <RouterLink to={`/wnioski/${item.id}`}>{item.subject}</RouterLink>
                  </Link>
                  {item.requesterName && (
                    <Text fontSize="sm" color="fg.muted" truncate>
                      {item.requesterName}
                    </Text>
                  )}
                </Table.Cell>
                <Table.Cell whiteSpace="nowrap" fontVariantNumeric="tabular-nums">
                  {formatDate(item.receivedOn)}
                </Table.Cell>
                <Table.Cell whiteSpace="nowrap">
                  <Flex direction="column" gap="1" align="flex-start">
                    <Text fontVariantNumeric="tabular-nums" fontWeight="semibold">
                      {item.status === 'ResponseSent' ? formatDate(item.respondedOn) : formatDate(item.effectiveDueOn)}
                      {item.status === 'ResponseSent' && (
                        <Box as="span" srOnly>
                          {' '}
                          (data udzielenia odpowiedzi)
                        </Box>
                      )}
                    </Text>
                    <DeadlineBadge urgency={item.urgency} daysLeft={item.daysLeft} />
                  </Flex>
                </Table.Cell>
                <Table.Cell>
                  <StatusBadge status={item.status} extensionDays={item.extensionDays} short />
                </Table.Cell>
                <Table.Cell hideBelow="xl">
                  <HStack gap="1" wrap="wrap">
                    {item.departmentSymbols.length === 0 ? (
                      <Text color="fg.subtle" fontSize="sm">
                        —
                      </Text>
                    ) : (
                      item.departmentSymbols.map((symbol) => (
                        <Tag.Root key={symbol} size="sm" variant="outline">
                          <Tag.Label>{symbol}</Tag.Label>
                        </Tag.Root>
                      ))
                    )}
                  </HStack>
                </Table.Cell>
                <Table.Cell textAlign="center">
                  {item.assignee ? (
                    <Avatar.Root size="xs" variant="subtle" title={item.assignee.fullName}>
                      <Avatar.Fallback aria-hidden>{item.assignee.initials}</Avatar.Fallback>
                      <Box as="span" srOnly>
                        {item.assignee.fullName}
                      </Box>
                    </Avatar.Root>
                  ) : (
                    <Text color="fg.subtle">—</Text>
                  )}
                </Table.Cell>
              </Table.Row>
            )
          })}
        </Table.Body>
      </Table.Root>
    </Table.ScrollArea>
  )
}
