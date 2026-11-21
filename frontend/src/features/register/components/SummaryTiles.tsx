import { Box, Flex, SimpleGrid, Skeleton, Text, chakra } from '@chakra-ui/react'
import { AlarmClock, AlertTriangle, CheckCircle2, Clock, FolderOpen, Sunrise, type LucideIcon } from 'lucide-react'
import type { RegisterSummary } from '@/api/types'
import { plural } from '@/lib/dates'
import type { QuickFilter } from '../quickFilters'

type Tile = { filter: QuickFilter; label: string; palette: string; icon: LucideIcon; count: (s: RegisterSummary) => number; urgent?: boolean }

const tiles: Tile[] = [
  { filter: 'overdue', label: 'Po terminie', palette: 'red', icon: AlertTriangle, count: (s) => s.overdue, urgent: true },
  { filter: 'today', label: 'Termin dziś', palette: 'red', icon: AlarmClock, count: (s) => s.dueToday, urgent: true },
  { filter: 'tomorrow', label: 'Termin jutro', palette: 'orange', icon: Sunrise, count: (s) => s.dueTomorrow, urgent: true },
  { filter: 'soon', label: 'W ciągu 3 dni', palette: 'yellow', icon: Clock, count: (s) => s.dueSoon },
  { filter: 'open', label: 'W toku', palette: 'blue', icon: FolderOpen, count: (s) => s.open },
  { filter: 'closed', label: 'Zakończone', palette: 'green', icon: CheckCircle2, count: (s) => s.closed },
]

type SummaryTilesProps = {
  summary: RegisterSummary | undefined
  active: QuickFilter | null
  onToggle: (filter: QuickFilter) => void
}

export function SummaryTiles({ summary, active, onToggle }: SummaryTilesProps) {
  return (
    <SimpleGrid columns={{ base: 2, sm: 3, xl: 6 }} gap="3" role="group" aria-label="Szybkie filtry według terminu">
      {tiles.map((tile) => {
        const count = summary ? tile.count(summary) : 0
        const highlighted = tile.urgent && count > 0
        const pressed = active === tile.filter
        const Icon = tile.icon
        return (
          <chakra.button
            key={tile.filter}
            type="button"
            aria-pressed={pressed}
            onClick={() => onToggle(tile.filter)}
            textAlign="left"
            bg={pressed ? `${tile.palette}.subtle` : 'bg.panel'}
            borderWidth="1px"
            borderColor={pressed ? `${tile.palette}.solid` : 'border'}
            boxShadow={pressed ? `inset 0 0 0 1px var(--chakra-colors-${tile.palette}-solid)` : undefined}
            borderRadius="md"
            px="4"
            py="3"
            cursor="pointer"
            transition="border-color 0.15s, background 0.15s"
            _hover={{ borderColor: `${tile.palette}.solid` }}
            position="relative"
            overflow="hidden"
          >
            {highlighted && <Box position="absolute" insetY="0" left="0" w="1" bg={`${tile.palette}.solid`} aria-hidden />}
            <Flex align="center" gap="2" color={highlighted || pressed ? `${tile.palette}.fg` : 'fg.muted'}>
              <Icon size={16} aria-hidden />
              <Text fontSize="sm" fontWeight="semibold">
                {tile.label}
              </Text>
            </Flex>
            {summary ? (
              <Text fontSize="3xl" fontWeight="bold" lineHeight="1.1" mt="1" color={highlighted || pressed ? `${tile.palette}.fg` : 'fg'}>
                {count}
                <Box as="span" srOnly>
                  {` ${plural(count, 'wniosek', 'wnioski', 'wniosków')}`}
                  {pressed ? ', filtr aktywny' : ''}
                </Box>
              </Text>
            ) : (
              <Skeleton h="9" w="12" mt="1" />
            )}
          </chakra.button>
        )
      })}
    </SimpleGrid>
  )
}
