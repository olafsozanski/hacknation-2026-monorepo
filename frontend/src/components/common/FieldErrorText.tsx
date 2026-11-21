import { Field, Icon } from '@chakra-ui/react'
import { CircleAlert } from 'lucide-react'
import type { ReactNode } from 'react'

export function FieldErrorText({ children }: { children?: ReactNode }) {
  if (!children) return null
  return (
    <Field.ErrorText alignItems="flex-start" gap="1.5" textStyle="sm">
      <Icon boxSize="4" flexShrink="0" mt="0.5">
        <CircleAlert aria-hidden />
      </Icon>
      <span>{children}</span>
    </Field.ErrorText>
  )
}
