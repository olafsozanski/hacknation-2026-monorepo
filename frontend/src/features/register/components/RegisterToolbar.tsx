import { Box, Button, Flex, Input, InputGroup, NativeSelect, SegmentGroup, Field } from '@chakra-ui/react'
import { CalendarDays, Search, Table2, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import type { RequestStatus, UserSummary } from '@/api/types'
import { statusLabels, statusOrder } from '@/lib/labels'

export type RegisterView = 'tabela' | 'kalendarz'

type RegisterToolbarProps = {
  search: string
  status: RequestStatus | ''
  assignee: string
  view: RegisterView
  users: UserSummary[]
  hasFilters: boolean
  onSearch: (value: string) => void
  onStatus: (value: RequestStatus | '') => void
  onAssignee: (value: string) => void
  onView: (value: RegisterView) => void
  onClear: () => void
}

export function RegisterToolbar(props: RegisterToolbarProps) {
  const [search, setSearch] = useState(props.search)
  const [syncedSearch, setSyncedSearch] = useState(props.search)
  const { onSearch } = props

  if (props.search !== syncedSearch) {
    setSyncedSearch(props.search)
    setSearch(props.search)
  }
  useEffect(() => {
    const timer = window.setTimeout(() => onSearch(search.trim()), 300)
    return () => window.clearTimeout(timer)
  }, [search, onSearch])

  return (
    <Flex
      gap="3"
      align={{ base: 'stretch', lg: 'flex-end' }}
      direction={{ base: 'column', lg: 'row' }}
      bg="bg.panel"
      borderWidth="1px"
      borderColor="border"
      borderRadius="md"
      p="3"
      role="search"
      aria-label="Filtrowanie rejestru"
    >
      <Field.Root flex="2" minW="0">
        <Field.Label fontSize="sm">Szukaj</Field.Label>
        <InputGroup startElement={<Search size={16} aria-hidden />}>
          <Input
            type="search"
            placeholder="Znak sprawy, przedmiot lub wnioskodawca"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </InputGroup>
      </Field.Root>
      <Field.Root flex="1" minW="44">
        <Field.Label fontSize="sm">Status</Field.Label>
        <NativeSelect.Root>
          <NativeSelect.Field value={props.status} onChange={(e) => props.onStatus(e.target.value as RequestStatus | '')}>
            <option value="">Wszystkie statusy</option>
            {statusOrder.map((status) => (
              <option key={status} value={status}>
                {statusLabels[status]}
              </option>
            ))}
          </NativeSelect.Field>
          <NativeSelect.Indicator />
        </NativeSelect.Root>
      </Field.Root>
      <Field.Root flex="1" minW="44">
        <Field.Label fontSize="sm">Prowadzący</Field.Label>
        <NativeSelect.Root>
          <NativeSelect.Field value={props.assignee} onChange={(e) => props.onAssignee(e.target.value)}>
            <option value="">Wszyscy</option>
            <option value="moje">Moje sprawy</option>
            {props.users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.fullName} ({user.initials})
              </option>
            ))}
          </NativeSelect.Field>
          <NativeSelect.Indicator />
        </NativeSelect.Root>
      </Field.Root>
      <Flex gap="2" align="flex-end" justify="space-between">
        {props.hasFilters && (
          <Button variant="ghost" onClick={props.onClear}>
            <X size={16} aria-hidden /> Wyczyść
          </Button>
        )}
        <Box>
          <SegmentGroup.Root
            value={props.view}
            onValueChange={(e) => props.onView(e.value as RegisterView)}
            aria-label="Widok rejestru"
            size="md"
          >
            <SegmentGroup.Indicator />
            <SegmentGroup.Item value="tabela">
              <SegmentGroup.ItemText display="inline-flex" alignItems="center" gap="1.5">
                <Table2 size={16} aria-hidden /> Tabela
              </SegmentGroup.ItemText>
              <SegmentGroup.ItemHiddenInput />
            </SegmentGroup.Item>
            <SegmentGroup.Item value="kalendarz">
              <SegmentGroup.ItemText display="inline-flex" alignItems="center" gap="1.5">
                <CalendarDays size={16} aria-hidden /> Kalendarz
              </SegmentGroup.ItemText>
              <SegmentGroup.ItemHiddenInput />
            </SegmentGroup.Item>
          </SegmentGroup.Root>
        </Box>
      </Flex>
    </Flex>
  )
}
