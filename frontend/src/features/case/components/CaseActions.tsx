import { Button, Menu, Portal } from '@chakra-ui/react'
import { ChevronDown, Hourglass, ListChecks, MoreHorizontal, Pencil } from 'lucide-react'
import { useState } from 'react'
import type { RequestDetail, RequestStatus } from '@/api/types'
import { notifyError, notifySuccess } from '@/components/ui/toaster'
import { statusLabels, statusOrder } from '@/lib/labels'
import { useChangeStatus } from '../api'
import { EditRequestDialog } from './EditRequestDialog'
import { ExtendDeadlineDialog } from './ExtendDeadlineDialog'

export function CaseActions({ request }: { request: RequestDetail }) {
  const changeStatus = useChangeStatus(request.id)
  const [dialog, setDialog] = useState<'edit' | 'extend' | null>(null)

  const setStatus = (status: RequestStatus) =>
    status !== request.status &&
    changeStatus.mutate(
      { status, version: request.version },
      {
        onSuccess: () => notifySuccess('Zmieniono status', statusLabels[status]),
        onError: (e) => notifyError(e, 'Nie udało się zmienić statusu'),
      },
    )

  return (
    <>
      <Menu.Root positioning={{ placement: 'bottom-end' }}>
        <Menu.Trigger asChild>
          <Button variant="outline">
            <MoreHorizontal size={16} aria-hidden /> Działania <ChevronDown size={14} aria-hidden />
          </Button>
        </Menu.Trigger>
        <Portal>
          <Menu.Positioner>
            <Menu.Content minW="64">
              <Menu.Item value="edit" onSelect={() => setDialog('edit')}>
                <Pencil size={16} aria-hidden /> Edytuj dane sprawy
              </Menu.Item>
              <Menu.Item value="extend" onSelect={() => setDialog('extend')} disabled={request.status === 'ResponseSent'}>
                <Hourglass size={16} aria-hidden /> Przedłuż termin / doprecyzowanie
              </Menu.Item>
              <Menu.Separator />
              <Menu.ItemGroup>
                <Menu.ItemGroupLabel display="flex" alignItems="center" gap="2">
                  <ListChecks size={14} aria-hidden /> Zmień status
                </Menu.ItemGroupLabel>
                <Menu.RadioItemGroup value={request.status} onValueChange={(e) => setStatus(e.value as RequestStatus)}>
                  {statusOrder.map((status) => (
                    <Menu.RadioItem key={status} value={status}>
                      <Menu.ItemIndicator />
                      {statusLabels[status]}
                    </Menu.RadioItem>
                  ))}
                </Menu.RadioItemGroup>
              </Menu.ItemGroup>
            </Menu.Content>
          </Menu.Positioner>
        </Portal>
      </Menu.Root>
      {dialog === 'edit' && <EditRequestDialog request={request} open onClose={() => setDialog(null)} />}
      {dialog === 'extend' && <ExtendDeadlineDialog request={request} open onClose={() => setDialog(null)} />}
    </>
  )
}
