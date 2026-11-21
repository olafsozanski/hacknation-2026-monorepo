import { Box, Card, Flex, Heading, Text } from '@chakra-ui/react'
import type { ReactNode } from 'react'

type PanelProps = {
  title: ReactNode
  headingLevel?: 'h2' | 'h3'
  step?: number
  description?: ReactNode
  actions?: ReactNode
  children: ReactNode
  id?: string
  padded?: boolean
}

export function Panel({ title, headingLevel = 'h2', step, description, actions, children, id, padded = true }: PanelProps) {
  const headingId = id ? `${id}-heading` : undefined
  return (
    <Card.Root as="section" variant="outline" bg="bg.panel" borderRadius="md" aria-labelledby={headingId} id={id}>
      <Flex
        px={{ base: 4, md: 5 }}
        pt="4"
        pb={padded ? 0 : 4}
        gap="3"
        align={{ base: 'flex-start', sm: 'center' }}
        direction={{ base: 'column', sm: 'row' }}
      >
        <Flex gap="3" align="center" flex="1" minW="0">
          {step !== undefined && (
            <Flex
              aria-hidden
              boxSize="7"
              flexShrink={0}
              borderRadius="full"
              bg="brand.subtle"
              color="brand.fg"
              fontWeight="bold"
              fontSize="sm"
              align="center"
              justify="center"
              borderWidth="1px"
              borderColor="brand.muted"
            >
              {step}
            </Flex>
          )}
          <Box minW="0">
            <Heading as={headingLevel} id={headingId} size="md">
              {step !== undefined && <Box as="span" srOnly>{`Krok ${step}: `}</Box>}
              {title}
            </Heading>
            {description && (
              <Text fontSize="sm" color="fg.muted" mt="0.5">
                {description}
              </Text>
            )}
          </Box>
        </Flex>
        {actions && (
          <Flex gap="2" wrap="wrap" flexShrink={0}>
            {actions}
          </Flex>
        )}
      </Flex>
      <Box px={padded ? { base: 4, md: 5 } : 0} py={padded ? 4 : 0}>
        {children}
      </Box>
    </Card.Root>
  )
}
