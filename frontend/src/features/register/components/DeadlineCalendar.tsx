import { Box, Button, Flex, Heading, IconButton, Link, Text, chakra } from '@chakra-ui/react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link as RouterLink } from 'react-router'
import type { RequestListItem, Urgency } from '@/api/types'
import { formatMonth, parseIsoDate, plural, toIsoDate, todayIso } from '@/lib/dates'
import { deadlineLabel } from '@/lib/labels'

const weekdays = [
  ['Pn', 'poniedziałek'],
  ['Wt', 'wtorek'],
  ['Śr', 'środa'],
  ['Cz', 'czwartek'],
  ['Pt', 'piątek'],
  ['So', 'sobota'],
  ['Nd', 'niedziela'],
] as const

const chipPalette: Record<Urgency, string> = {
  Overdue: 'red',
  DueToday: 'red',
  DueTomorrow: 'orange',
  DueSoon: 'yellow',
  OnTrack: 'blue',
  Closed: 'green',
}

const maxChips = 3

function monthMatrix(month: Date) {
  const first = new Date(month.getFullYear(), month.getMonth(), 1)
  const offset = (first.getDay() + 6) % 7
  const start = new Date(first.getFullYear(), first.getMonth(), 1 - offset)
  const weeks: Date[][] = []
  for (let week = 0; week < 6; week++) {
    const days = Array.from({ length: 7 }, (_, day) => new Date(start.getFullYear(), start.getMonth(), start.getDate() + week * 7 + day))
    if (week > 3 && days[0].getMonth() !== month.getMonth()) break
    weeks.push(days)
  }
  return weeks
}

type DeadlineCalendarProps = {
  items: RequestListItem[]
  onShowDay: (isoDate: string) => void
}

export function DeadlineCalendar({ items, onShowDay }: DeadlineCalendarProps) {
  const today = todayIso()
  const [month, setMonth] = useState(() => {
    const now = parseIsoDate(today)
    return new Date(now.getFullYear(), now.getMonth(), 1)
  })

  const byDay = useMemo(() => {
    const map = new Map<string, RequestListItem[]>()
    for (const item of items) {
      if (item.status === 'ResponseSent') continue
      const list = map.get(item.effectiveDueOn) ?? []
      list.push(item)
      map.set(item.effectiveDueOn, list)
    }
    return map
  }, [items])

  const weeks = monthMatrix(month)
  const shift = (delta: number) => setMonth((m) => new Date(m.getFullYear(), m.getMonth() + delta, 1))
  const monthName = formatMonth(month)

  return (
    <Box borderWidth="1px" borderColor="border" borderRadius="md" bg="bg.panel" overflow="hidden">
      <Flex align="center" gap="2" px="4" py="3" borderBottomWidth="1px" borderColor="border" wrap="wrap">
        <Heading as="h2" size="md" textTransform="capitalize" flex="1" aria-live="polite">
          {monthName}
        </Heading>
        <Button size="sm" variant="outline" onClick={() => setMonth(new Date(parseIsoDate(today).getFullYear(), parseIsoDate(today).getMonth(), 1))}>
          Dziś
        </Button>
        <IconButton size="sm" variant="ghost" aria-label="Poprzedni miesiąc" onClick={() => shift(-1)}>
          <ChevronLeft aria-hidden />
        </IconButton>
        <IconButton size="sm" variant="ghost" aria-label="Następny miesiąc" onClick={() => shift(1)}>
          <ChevronRight aria-hidden />
        </IconButton>
      </Flex>
      <Box overflowX="auto">
        <chakra.table w="full" minW="720px" style={{ tableLayout: 'fixed', borderCollapse: 'collapse' }}>
          <chakra.caption srOnly>Terminy odpowiedzi na wnioski — {monthName}</chakra.caption>
          <thead>
            <tr>
              {weekdays.map(([short, full], index) => (
                <chakra.th
                  key={short}
                  scope="col"
                  py="2"
                  fontSize="xs"
                  fontWeight="bold"
                  color={index >= 5 ? 'fg.subtle' : 'fg.muted'}
                  textTransform="uppercase"
                  bg="bg.muted"
                  borderBottomWidth="1px"
                  borderColor="border"
                >
                  <abbr title={full} style={{ textDecoration: 'none' }}>
                    {short}
                  </abbr>
                </chakra.th>
              ))}
            </tr>
          </thead>
          <tbody>
            {weeks.map((week) => (
              <tr key={toIsoDate(week[0])}>
                {week.map((day, index) => {
                  const iso = toIsoDate(day)
                  const dayItems = byDay.get(iso) ?? []
                  const inMonth = day.getMonth() === month.getMonth()
                  const isToday = iso === today
                  const weekend = index >= 5
                  return (
                    <chakra.td
                      key={iso}
                      verticalAlign="top"
                      h="24"
                      p="1.5"
                      borderWidth="1px"
                      borderColor="border.muted"
                      bg={weekend ? 'bg.subtle' : undefined}
                      aria-current={isToday ? 'date' : undefined}
                    >
                      <Flex justify="space-between" align="center" mb="1">
                        <Text
                          as="span"
                          fontSize="sm"
                          fontWeight={isToday ? 'bold' : 'medium'}
                          bg={isToday ? 'brand.solid' : undefined}
                          color={isToday ? 'brand.contrast' : weekend || !inMonth ? 'fg.subtle' : 'fg'}
                          borderRadius="full"
                          minW="6"
                          h="6"
                          display="inline-flex"
                          alignItems="center"
                          justifyContent="center"
                          px="1"
                        >
                          {day.getDate()}
                          {isToday && (
                            <Box as="span" srOnly>
                              {' '}
                              (dziś)
                            </Box>
                          )}
                        </Text>
                        {dayItems.length > 0 && (
                          <Text as="span" srOnly>
                            {`${dayItems.length} ${plural(dayItems.length, 'termin', 'terminy', 'terminów')}`}
                          </Text>
                        )}
                      </Flex>
                      <chakra.ul display="flex" flexDirection="column" gap="1" listStyleType="none" m="0" p="0">
                        {dayItems.slice(0, maxChips).map((item) => {
                          const palette = chipPalette[item.urgency]
                          return (
                            <li key={item.id}>
                              <Link
                                asChild
                                display="block"
                                fontSize="xs"
                                lineHeight="1.3"
                                px="1.5"
                                py="1"
                                borderRadius="xs"
                                bg={`${palette}.subtle`}
                                color={`${palette}.fg`}
                                borderLeftWidth="3px"
                                borderLeftColor={`${palette}.solid`}
                                textDecoration="none"
                                _hover={{ textDecoration: 'underline' }}
                              >
                                <RouterLink to={`/wnioski/${item.id}`}>
                                  <Text as="span" fontWeight="bold" display="block" truncate>
                                    {item.caseNumber}
                                  </Text>
                                  <Text as="span" display="block" truncate>
                                    {item.subject}
                                  </Text>
                                  <Text as="span" srOnly>
                                    {`. ${deadlineLabel(item.urgency, item.daysLeft)}`}
                                  </Text>
                                </RouterLink>
                              </Link>
                            </li>
                          )
                        })}
                        {dayItems.length > maxChips && (
                          <li>
                            <Button size="2xs" variant="plain" color="brand.fg" px="1" onClick={() => onShowDay(iso)}>
                              {`+${dayItems.length - maxChips} więcej`}
                            </Button>
                          </li>
                        )}
                      </chakra.ul>
                    </chakra.td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </chakra.table>
      </Box>
    </Box>
  )
}
