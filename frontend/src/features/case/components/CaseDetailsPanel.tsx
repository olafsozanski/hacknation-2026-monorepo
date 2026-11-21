import { Button, DataList } from '@chakra-ui/react'
import { Pencil } from 'lucide-react'
import { useState } from 'react'
import type { RequestDetail } from '@/api/types'
import { Panel } from '@/components/common/Panel'
import { formatDate } from '@/lib/dates'
import { extensionKindLabels, replyChannelLabels } from '@/lib/labels'
import { EditRequestDialog } from './EditRequestDialog'

export function CaseDetailsPanel({ request }: { request: RequestDetail }) {
  const [editing, setEditing] = useState(false)
  const rows: [string, string][] = [
    ['Znak sprawy', request.caseNumber],
    ['Lp. w rejestrze', `${request.sequenceNumber} / ${request.year}`],
    ['Data wpływu', formatDate(request.receivedOn)],
    ['Termin ustawowy', formatDate(request.dueOn)],
    ...(request.extendedDueOn
      ? ([
          [
            request.extensionKind ? `Termin po przedłużeniu (${extensionKindLabels[request.extensionKind].toLowerCase()})` : 'Termin po przedłużeniu',
            `${formatDate(request.extendedDueOn)} (+${request.extensionDays} dni)`,
          ],
        ] as [string, string][])
      : []),
    ['Najpóźniej po przedłużeniu', formatDate(request.latestExtendedDueOn)],
    ['Prowadzi', request.assignee ? `${request.assignee.fullName} (${request.assignee.initials})` : '—'],
    ['Wnioskodawca', request.requesterName ?? '—'],
    ['Adres do korespondencji', request.requesterContact ?? '—'],
    ['Forma odpowiedzi', replyChannelLabels[request.replyChannel]],
    ['Data udzielenia odpowiedzi', formatDate(request.respondedOn)],
  ]

  return (
    <Panel
      title="Dane sprawy"
      actions={
        <Button size="sm" variant="ghost" onClick={() => setEditing(true)}>
          <Pencil size={14} aria-hidden /> Edytuj
        </Button>
      }
    >
      <DataList.Root orientation="horizontal" size="sm" gap="2.5">
        {rows.map(([label, value]) => (
          <DataList.Item key={label} alignItems="flex-start">
            <DataList.ItemLabel minW="32" maxW="32" color="fg.muted">
              {label}
            </DataList.ItemLabel>
            <DataList.ItemValue fontWeight="medium" overflowWrap="anywhere">
              {value}
            </DataList.ItemValue>
          </DataList.Item>
        ))}
      </DataList.Root>
      {request.extensionReason && (
        <DataList.Root size="sm" mt="3">
          <DataList.Item>
            <DataList.ItemLabel color="fg.muted">Przyczyna przedłużenia</DataList.ItemLabel>
            <DataList.ItemValue>{request.extensionReason}</DataList.ItemValue>
          </DataList.Item>
        </DataList.Root>
      )}
      {editing && <EditRequestDialog request={request} open onClose={() => setEditing(false)} />}
    </Panel>
  )
}
