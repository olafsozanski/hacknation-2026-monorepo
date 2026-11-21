import { Dialog } from '@chakra-ui/react'
import type { FormEvent, ReactNode } from 'react'

type DialogFormProps = Omit<Dialog.ContentProps, 'onSubmit' | 'asChild'> & {
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  children: ReactNode
}

export function DialogForm({ onSubmit, children, ...props }: DialogFormProps) {
  return (
    <Dialog.Content asChild {...props}>
      <form onSubmit={onSubmit} noValidate>
        {children}
      </form>
    </Dialog.Content>
  )
}
