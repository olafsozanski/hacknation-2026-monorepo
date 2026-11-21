import { Button, type ButtonProps } from '@chakra-ui/react'

export function LinkButton(props: ButtonProps) {
  return (
    <Button
      variant="plain"
      size="sm"
      px="0"
      h="auto"
      minH="8"
      color="brand.fg"
      textDecoration="underline"
      textUnderlineOffset="3px"
      _hover={{ textDecorationThickness: '2px' }}
      {...props}
    />
  )
}
