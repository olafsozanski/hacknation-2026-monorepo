import { Button, Dialog, Portal, Text } from '@chakra-ui/react'
import type { ReactNode } from 'react'

type ConfirmDialogProps = {
  open: boolean
  title: string
  children: ReactNode
  confirmLabel: string
  destructive?: boolean
  loading?: boolean
  onConfirm: () => void
  onCancel: () => void
  finalFocus?: () => HTMLElement | null
}

export function ConfirmDialog({ open, title, children, confirmLabel, destructive, loading, onConfirm, onCancel, finalFocus }: ConfirmDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={(e) => !e.open && onCancel()} role="alertdialog" finalFocusEl={finalFocus}>
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content>
            <Dialog.Header>
              <Dialog.Title>{title}</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              {typeof children === 'string' ? <Text>{children}</Text> : children}
            </Dialog.Body>
            <Dialog.Footer>
              <Button variant="outline" onClick={onCancel}>
                Anuluj
              </Button>
              <Button colorPalette={destructive ? 'red' : 'brand'} onClick={onConfirm} loading={loading}>
                {confirmLabel}
              </Button>
            </Dialog.Footer>
          </Dialog.Content>
        </Dialog.Positioner>
      </Portal>
    </Dialog.Root>
  )
}
