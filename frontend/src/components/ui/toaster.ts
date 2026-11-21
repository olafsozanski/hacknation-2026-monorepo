import { createToaster } from '@chakra-ui/react'

export const toaster = createToaster({
  placement: 'bottom-end',
  pauseOnPageIdle: true,
  duration: 6000,
})

export function notifyError(error: unknown, title = 'Nie udało się wykonać operacji') {
  toaster.create({
    type: 'error',
    title,
    description: error instanceof Error ? error.message : undefined,
    closable: true,
  })
}

export function notifySuccess(title: string, description?: string) {
  toaster.create({ type: 'success', title, description, closable: true })
}
