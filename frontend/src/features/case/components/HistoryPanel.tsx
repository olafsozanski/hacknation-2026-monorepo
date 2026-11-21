import { Button, Text, Timeline } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Panel } from '@/components/common/Panel'
import { formatDateTime } from '@/lib/dates'
import { auditActionLabels } from '@/lib/labels'
import { historyQuery } from '../api'

const pageSize = 6

export function HistoryPanel({ requestId }: { requestId: string }) {
  const history = useQuery(historyQuery(requestId))
  const [showAll, setShowAll] = useState(false)
  const items = history.data ?? []
  const visible = showAll ? items : items.slice(0, pageSize)

  return (
    <Panel title="Historia sprawy">
      {history.isPending ? (
        <Text color="fg.muted">Wczytywanie…</Text>
      ) : items.length === 0 ? (
        <Text color="fg.muted" fontSize="sm">
          Brak zdarzeń.
        </Text>
      ) : (
        <>
          <Timeline.Root size="sm" variant="subtle">
            {visible.map((item, index) => (
              <Timeline.Item key={`${item.at}-${index}`}>
                <Timeline.Connector>
                  <Timeline.Separator />
                  <Timeline.Indicator />
                </Timeline.Connector>
                <Timeline.Content gap="0.5" pb="3">
                  <Timeline.Title fontWeight="semibold">{auditActionLabels[item.action] ?? item.action}</Timeline.Title>
                  {item.details && (
                    <Text fontSize="sm" color="fg.muted">
                      {item.details}
                    </Text>
                  )}
                  <Timeline.Description fontSize="xs">
                    {formatDateTime(item.at)} · {item.userName}
                  </Timeline.Description>
                </Timeline.Content>
              </Timeline.Item>
            ))}
          </Timeline.Root>
          {items.length > pageSize && (
            <Button size="sm" variant="plain" color="brand.fg" px="0" onClick={() => setShowAll((s) => !s)}>
              {showAll ? 'Pokaż mniej' : `Pokaż wszystkie (${items.length})`}
            </Button>
          )}
        </>
      )}
    </Panel>
  )
}
