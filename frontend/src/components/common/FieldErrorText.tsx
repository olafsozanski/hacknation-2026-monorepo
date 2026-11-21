import { Field, Icon } from '@chakra-ui/react'
import { createLucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

const CircleAsterisk = createLucideIcon({
  name: 'circle-asterisk',
  node: [
    ['circle', { cx: '12', cy: '12', r: '10', key: 'circle' }],
    ['path', { d: 'M12 7v10', key: 'stem' }],
    ['path', { d: 'm7.67 9.5 8.66 5', key: 'falling' }],
    ['path', { d: 'm7.67 14.5 8.66-5', key: 'rising' }],
  ],
})

export function FieldErrorText({ children }: { children?: ReactNode }) {
  if (!children) return null
  return (
    <Field.ErrorText alignItems="flex-start" gap="1.5" textStyle="sm">
      <Icon boxSize="4" flexShrink="0" mt="0.5">
        <CircleAsterisk aria-hidden />
      </Icon>
      <span>{children}</span>
    </Field.ErrorText>
  )
}
