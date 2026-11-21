import { Box, Flex, Text } from '@chakra-ui/react'
import type { LucideIcon } from 'lucide-react'

export function NoticeLine({ icon: Icon, children, color = 'fg' }: { icon: LucideIcon; children: string; color?: string }) {
  return (
    <Flex gap="2" align="flex-start" fontSize="sm" color={color}>
      <Box as="span" flexShrink={0} mt="0.5" aria-hidden>
        <Icon size={16} />
      </Box>
      <Text>{children}</Text>
    </Flex>
  )
}
